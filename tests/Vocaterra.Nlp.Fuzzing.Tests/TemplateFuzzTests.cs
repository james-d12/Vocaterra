using System.Text;
using System.Text.RegularExpressions;
using CsCheck;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Response.Templates;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O12: every dialogue template of the bundle renders for slot maps that hold the slots it reads, and the output is clean text: no braces,
/// balanced inverted marks, no stray whitespace, the noun in the number its quantity asks for. The only tolerated exception is the one the
/// engine throws for a slot that is missing; seeded variant selection is stable; and the response planner only ever throws that exception too.
/// </summary>
public sealed partial class TemplateFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O12";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    private static readonly Regex PluralIf = PluralIfPattern();

    [Fact]
    public void EveryTemplate_RendersCleanText_ForSlotMapsThatHoldItsPlaceholders()
    {
        var gen = SlotMapGen.Templates(Harness);
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);
        var engine = Harness.Bundle.TemplateEngine;
        var diagnostics = new FuzzDiagnostics();

        gen.Sample(
            ((DialogueTemplate Template, SlotMap Slots, bool Messy) input) =>
            {
                var (template, slots, messy) = input;
                var context = new { template = template.Text, lineId = template.LineId, slots = slots.ToDictionary(static p => p.Key, static p => p.Value) };

                string output1;
                try
                {
                    output1 = engine.Render(template, slots);
                }
                catch (TemplateException ex)
                {
                    throw Determinism.Fail(Oracle, $"A complete slot map made the template throw TemplateException for '{ex.Placeholder}': {ex.Message}", context);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"Render threw {ex.GetType().Name}: {ex.Message}", context);
                }

                CheckCleanText(template, output1, messy, context);
                if (!messy)
                {
                    CheckPluralAgreement(template, slots, output1, context, diagnostics);
                }

                Determinism.AssertSame(Oracle, "Render", context, () => new RenderedText(output1), () => new RenderedText(engine.Render(template, slots)));
                CheckMissingSlot(template, slots, engine, context);
                diagnostics.Count(messy ? "render:messy" : "render:clean");
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Template diagnostics:\n" + diagnostics.Report());
        Assert.True(
            diagnostics.Get("plural-if:plural") > 0 && diagnostics.Get("plural-if:singular") > 0,
            "The plural-if agreement was not exercised in both numbers; does the bundle still use plural-if?\n" + diagnostics.Report());
    }

    [Fact]
    public void SeededVariantSelection_IsStable_AndInRange()
    {
        var pools = Harness.Bundle.Dialogue.Pools.Values.ToArray();
        var gen = Gen.Select(Gen.OneOfConst(pools), Gen.ULong, Gen.Long, static (pool, seed, counter) => (pool, seed, counter));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);

        gen.Sample(
            ((VariantPool Pool, ulong Seed, long Counter) input) =>
            {
                var (pool, seed, counter) = input;
                var first = DialogueSelector.Select(pool, new SeededRng(seed));
                var second = DialogueSelector.Select(pool, new SeededRng(seed));
                var expected = pool.Variants[new SeededRng(seed).NextInt(pool.Variants.Count)];
                if (first != second || first != expected || !pool.Variants.Contains(first))
                {
                    throw Determinism.Fail(Oracle, $"Selection from pool '{pool.PoolId}' with seed {seed} is not stable or not the variant at NextInt(count).", new { pool = pool.PoolId, seed });
                }

                var interaction = SeededRng.ForInteraction(seed, "blacksmith", counter);
                var again = SeededRng.ForInteraction(seed, "blacksmith", counter);
                if (interaction.NextUInt64() != again.NextUInt64())
                {
                    throw Determinism.Fail(Oracle, "SeededRng.ForInteraction is not a function of its arguments.", new { seed, counter });
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);
    }

    [Fact]
    public void ResponsePlanner_OnlyThrowsTheMissingSlotException_AndPlansAtMostTwoLines()
    {
        var catalog = Harness.Bundle.Dialogue;
        var replyIntents = catalog.Pools.Keys.Where(static k => !k.StartsWith("feedback.", StringComparison.Ordinal)).ToArray();
        var kinds = new[] { FeedbackKinds.Recast, FeedbackKinds.Hint, FeedbackKinds.ExplicitCorrection, FeedbackKinds.ModelPronunciation, FeedbackKinds.SlowRepeat };
        var gen = Gen.Select(Gen.OneOfConst(replyIntents), Gen.OneOfConst(kinds), Gen.ULong, Gen.ULong, Gen.Bool, static (intent, kind, slotSeed, rngSeed, withFeedback) => (intent, kind, slotSeed, rngSeed, withFeedback));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);
        var planner = new ResponsePlanner(Harness.Bundle.TemplateEngine);

        gen.Sample(
            ((string Intent, string Kind, ulong SlotSeed, ulong RngSeed, bool WithFeedback) input) =>
            {
                var rng = new SeededRng(input.SlotSeed);

                // The reply slots must satisfy every variant of the reply pool, so the union of what its templates read is filled in.
                var slots = SlotMap.Empty;
                foreach (var template in catalog.Pools[input.Intent].Variants)
                {
                    foreach (var pair in SlotMapGen.For(template, Harness.Words, rng))
                    {
                        slots = slots.With(pair.Key, pair.Value);
                    }
                }

                var verdict = new Verdict(new IntentId(input.Intent), slots, true, 1.0, [], [], 1.0, ResolvedBy.Grammar, VersionStamp.Empty);
                var tag = ErrorTags.All[rng.NextInt(ErrorTags.All.Count)];
                var request = input.WithFeedback ? new FeedbackRequest(tag, rng.Pick(Harness.Words.AllForms), rng.Pick(Harness.Words.AllForms), input.Kind) : null;
                var context = new { input.Intent, input.Kind, input.RngSeed, slots = slots.ToDictionary(static p => p.Key, static p => p.Value), request };

                ResponsePlan plan;
                try
                {
                    plan = planner.Plan(verdict, request, catalog, new SeededRng(input.RngSeed), input.Intent);
                }
                catch (TemplateException ex) when (KnownTolerances.IsMissingSlot(ex, SlotMap.From(slots.Concat(ReservedFeedbackSlots(request)))))
                {
                    // Feedback pools may read slots the reply pool never needed (feedback.recast.conj.person reads verb); the engine says so.
                    return;
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"Plan threw {ex.GetType().Name}: {ex.Message}", context);
                }

                if (plan.Lines.Count is < 1 or > 2 || plan.Lines.Any(static l => l.Text.Length == 0 || l.LineId.Length == 0))
                {
                    throw Determinism.Fail(Oracle, $"The plan has {plan.Lines.Count} lines or an empty line.", context);
                }

                Determinism.AssertSame(Oracle, "ResponsePlanner.Plan", context, () => plan, () => planner.Plan(verdict, request, catalog, new SeededRng(input.RngSeed), input.Intent));
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);
    }

    private static IEnumerable<KeyValuePair<string, string>> ReservedFeedbackSlots(FeedbackRequest? request)
    {
        if (request is null)
        {
            return [];
        }

        return
        [
            new("tag", request.Tag),
            new("expected", request.Expected),
            new("actual", request.Actual),
            new("recast", request.Expected),
        ];
    }

    private static void CheckCleanText(DialogueTemplate template, string text, bool messy, object context)
    {
        var literalBraces = template.Text.Contains("{{", StringComparison.Ordinal) || template.Text.Contains("}}", StringComparison.Ordinal);
        if (!literalBraces && (text.Contains('{', StringComparison.Ordinal) || text.Contains('}', StringComparison.Ordinal)))
        {
            throw Determinism.Fail(Oracle, $"The output '{text}' contains a brace although the template has no literal braces.", context);
        }

        if (!text.IsNormalized(NormalizationForm.FormC))
        {
            throw Determinism.Fail(Oracle, $"The output '{text}' is not in NFC.", context);
        }

        if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]) || text.Contains("  ", StringComparison.Ordinal)))
        {
            throw Determinism.Fail(Oracle, $"The output '{text}' has leading, trailing or doubled whitespace.", context);
        }

        // Noisy slot values may carry marks of their own; the balancing claim is about text assembled from clean parts.
        if (!messy && (Count(text, '¿') != Count(text, '?') || Count(text, '¡') != Count(text, '!')))
        {
            throw Determinism.Fail(Oracle, $"The inverted marks of '{text}' are not balanced.", context);
        }
    }

    private static int Count(string text, char c)
    {
        return text.Count(x => x == c);
    }

    // plural-if(qty) must put the noun in the number the quantity asks for: plural unless the quantity is one.
    private static void CheckPluralAgreement(DialogueTemplate template, SlotMap slots, string text, object context, FuzzDiagnostics diagnostics)
    {
        var words = Regex.Split(text, @"[^\p{L}]+").Where(static w => w.Length > 0).ToArray();
        foreach (Match match in PluralIf.Matches(template.Text))
        {
            var noun = slots[match.Groups["noun"].Value].Trim();
            var quantity = slots[match.Groups["qty"].Value].Trim();
            var plural = !(IsOne(quantity));
            var expected = Harness.Morphology.InflectNoun(noun, plural ? LexiconSchema.Plural : LexiconSchema.Singular);
            if (!words.Contains(expected, StringComparer.OrdinalIgnoreCase))
            {
                throw Determinism.Fail(Oracle, $"With quantity '{quantity}' the noun '{noun}' should appear as '{expected}', but the output is '{text}'.", context);
            }

            diagnostics.Count(plural ? "plural-if:plural" : "plural-if:singular");
        }
    }

    private static bool IsOne(string quantity)
    {
        if (quantity.All(char.IsAsciiDigit))
        {
            return quantity.TrimStart('0') == "1";
        }

        return SpanishNumbers.TryParse(quantity.ToLowerInvariant(), out var value) && value == 1;
    }

    // Dropping a slot the template reads must give TemplateException naming a slot that really is missing, and nothing else.
    private static void CheckMissingSlot(DialogueTemplate template, SlotMap slots, TemplateEngine engine, object context)
    {
        var names = SlotMapGen.UsesOf(template).Keys.ToArray();
        if (names.Length == 0)
        {
            return;
        }

        var dropped = names[(int)(DeterministicHash.Seed64(template.LineId, string.Join('|', slots.Values)) % (ulong)names.Length)];
        var reduced = SlotMap.From(slots.Where(pair => pair.Key != dropped));
        try
        {
            engine.Render(template, reduced);
            throw Determinism.Fail(Oracle, $"Rendering without the slot '{dropped}' did not fail.", context);
        }
        catch (TemplateException ex) when (KnownTolerances.IsMissingSlot(ex, reduced))
        {
            // The designed outcome.
        }
        catch (TemplateException ex)
        {
            throw Determinism.Fail(Oracle, $"Rendering without '{dropped}' threw TemplateException for '{ex.Placeholder}', which is not a missing slot: {ex.Message}", context);
        }
    }

    [GeneratedRegex(@"\{(?<noun>[^{}:]+)(?::[^{}]*?)?plural-if\((?<qty>[^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex PluralIfPattern();

    /// <summary>A wrapper so the determinism helper can serialize plain text.</summary>
    /// <param name="Text">The text.</param>
    public sealed record RenderedText(string Text);
}
