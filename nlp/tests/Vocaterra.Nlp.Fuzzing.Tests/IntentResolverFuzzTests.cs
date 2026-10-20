using CsCheck;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Nlp.Intent;
using Vocaterra.Nlp.Nlp.SlotGrammar;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O7: the intent resolver never crashes, respects the prompt's allowed intents whatever their order or content, follows the ladder (a
/// grammar the analyzer matched decides first; the rung it reports is one it really took), reports an unresolved result as margin 0 and
/// confidence 0, and gives the same resolution on a fresh instance.
/// </summary>
public sealed class IntentResolverFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O7";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    private static readonly IntentResolver WithoutEmbeddings = Harness.NewResolver(embeddings: false);

    private static readonly IntentResolver WithEmbeddings = Harness.NewResolver(embeddings: true);

    [Fact]
    public void Resolve_NeverCrashes_AndRespectsTheLadderAndTheAllowedIntents()
    {
        var gen = Gen.Select(
            SentenceGen.Recipe(Harness),
            Gen.ULong,
            Gen.Int[0, 9],
            SpanishTextGen.Text(Harness.Words),
            static (recipe, promptSeed, mode, text) => new ResolveCase(recipe, promptSeed, mode, text));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);
        var diagnostics = new FuzzDiagnostics();
        var run = 0;

        gen.Sample(
            (ResolveCase input) =>
            {
                var prompt = MakePrompt(new SeededRng(input.PromptSeed));

                // Half the time a realised grammar sentence, a fifth free text, and three in ten an example utterance of the catalog with
                // its words shuffled or dropped, which no grammar matches but the embedding rung may still recognise.
                var sentence = input.Mode <= 4 ? SentenceGen.Realize(Harness, input.Recipe, diagnostics: diagnostics) : null;
                var text = input.Mode switch
                {
                    <= 4 => sentence?.Text ?? input.Text,
                    <= 6 => input.Text,
                    _ => PerturbedExample(new SeededRng(input.PromptSeed ^ 0x9E3779B97F4A7C15UL)),
                };
                var normalized = PipelineHarness.Normalize(text);

                var candidates = CandidatesOf(prompt);
                var grammars = candidates.SelectMany(static i => i.Grammars).DistinctBy(static g => g.Id).ToArray();
                var analysis = Harness.Analyzer.Analyze(normalized, grammars);
                var context = new { text, prompt, matchedGrammar = analysis.MatchedGrammarId, recipe = input.Recipe.ToString() };

                foreach (var (resolver, label) in new[] { (WithoutEmbeddings, "without embeddings"), (WithEmbeddings, "with embeddings") })
                {
                    IntentResolution resolution;
                    try
                    {
                        resolution = resolver.Resolve(normalized, analysis, prompt);
                    }
                    catch (Exception ex)
                    {
                        throw Determinism.Fail(Oracle, $"Resolve ({label}) threw {ex.GetType().Name}: {ex.Message}", context);
                    }

                    CheckResolution(resolution, label == "with embeddings", candidates, grammars, analysis, sentence, context);
                    diagnostics.Count($"rung:{resolution.Rung}");

                    // The same prompt with its allowed intents reordered and repeated resolves identically; and every eighth time a resolver
                    // built from scratch agrees.
                    var reordered = prompt with { AllowedIntentIds = [.. prompt.AllowedIntentIds.Reverse().Concat(prompt.AllowedIntentIds)] };
                    Determinism.AssertSame(Oracle, $"Resolve ({label}) with reordered allowed intents", context, () => resolution, () => resolver.Resolve(normalized, analysis, reordered));
                    if (Interlocked.Increment(ref run) % 16 == 0)
                    {
                        var fresh = Harness.NewResolver(label == "with embeddings");
                        Determinism.AssertSame(Oracle, $"Resolve ({label}) on a fresh resolver", context, () => resolution, () => fresh.Resolve(normalized, analysis, prompt));
                    }
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Resolver diagnostics:\n" + diagnostics.Report());
        Assert.True(
            diagnostics.Get("rung:Grammar") > 0 && diagnostics.Get("rung:Rules") > 0 && diagnostics.Get("rung:Embedding") > 0 && diagnostics.Get("rung:Unresolved") > 0,
            "Every rung of the ladder must be reached.\n" + diagnostics.Report());
    }

    private static List<IntentDefinition> CandidatesOf(PromptSpec prompt)
    {
        // The resolver's own documented rule: an empty list allows every intent, otherwise only the listed ones that the catalog has.
        var allowed = prompt.AllowedIntentIds;
        return [.. Harness.Bundle.Intents.Intents.Where(i => allowed.Count == 0 || allowed.Contains(i.Id.Value))];
    }

    private static void CheckResolution(
        IntentResolution resolution,
        bool hasEmbeddings,
        List<IntentDefinition> candidates,
        IReadOnlyList<SlotGrammar> grammars,
        AnalysisResult analysis,
        GeneratedSentence? sentence,
        object context)
    {
        if (double.IsNaN(resolution.Confidence) || double.IsNaN(resolution.Margin) || resolution.Confidence < 0 || resolution.Margin < 0 || resolution.Margin > 1.0 + 1e-9 || resolution.Confidence > 1.0 + 1e-9)
        {
            throw Determinism.Fail(Oracle, $"Margin {resolution.Margin} or confidence {resolution.Confidence} is outside [0, 1].", context);
        }

        var unresolved = resolution.Rung == IntentRung.Unresolved;
        if (unresolved != resolution.Intent.IsUnknown)
        {
            throw Determinism.Fail(Oracle, $"Rung {resolution.Rung} and intent '{resolution.Intent}' disagree about whether anything was resolved.", context);
        }

        if (unresolved)
        {
            if (resolution.Margin != 0.0 || resolution.Confidence != 0.0 || resolution.Slots.Count != 0)
            {
                throw Determinism.Fail(Oracle, $"An unresolved result must have margin 0, confidence 0 and no slots, but has {resolution.Margin}, {resolution.Confidence} and {resolution.Slots.Count} slots.", context);
            }
        }
        else if (!candidates.Any(c => c.Id == resolution.Intent))
        {
            throw Determinism.Fail(Oracle, $"The resolved intent '{resolution.Intent}' is not one of the allowed intents.", context);
        }

        var someGrammarMatches = grammars.Any(g => SlotGrammarMatcher.Match(analysis.Tokens, g) is not null);
        switch (resolution.Rung)
        {
            case IntentRung.Grammar:
                if (!someGrammarMatches || resolution.Margin != 1.0)
                {
                    throw Determinism.Fail(Oracle, "The grammar rung decided although no allowed grammar matches, or its margin is not 1.", context);
                }

                break;
            case IntentRung.Rules:
                if (someGrammarMatches || resolution.Margin != 1.0 || resolution.Confidence != IntentResolverOptions.Default.RulesConfidence)
                {
                    throw Determinism.Fail(Oracle, "The rules rung decided although a grammar matches, or its margin or confidence is not the documented one.", context);
                }

                break;
            case IntentRung.Embedding:
                if (!hasEmbeddings || someGrammarMatches
                    || IntentResolverOptions.Default.EmbeddingTop1.Evaluate(resolution.Confidence) != MarginDecision.Accept
                    || IntentResolverOptions.Default.EmbeddingGap.Evaluate(resolution.Margin) != MarginDecision.Accept)
                {
                    throw Determinism.Fail(Oracle, "The embedding rung decided without a model, past a matching grammar, or without clearing both margins.", context);
                }

                break;
            default:
                break;
        }

        // The ladder's first rung: a grammar the analyzer matched (it was given only the allowed intents' grammars) decides, and the intent
        // is the first allowed intent that owns it.
        if (analysis.MatchedGrammarId is { } matched)
        {
            var owner = candidates.FirstOrDefault(c => c.Grammars.Any(g => g.Id == matched));
            if (resolution.Rung != IntentRung.Grammar || owner is null || resolution.Intent != owner.Id)
            {
                throw Determinism.Fail(Oracle, $"The analyzer matched '{matched}' (owned by '{owner?.Id}') but the resolution is {resolution.Rung} '{resolution.Intent}'.", context);
            }
        }
        else if (sentence is { PatternSource: "catalog" } && candidates.Any(c => c.Grammars.Any(g => g.Id == sentence.PatternText)))
        {
            throw Determinism.Fail(Oracle, $"A sentence built from the allowed grammar '{sentence.PatternText}' matched no grammar.", context);
        }
    }

    private static string PerturbedExample(SeededRng rng)
    {
        var examples = Harness.Bundle.Intents.Intents.SelectMany(static i => i.Examples).ToArray();
        var words = rng.Pick(examples).Split(' ').ToList();
        switch (rng.NextInt(3))
        {
            case 0:
                words.Reverse();
                break;
            case 1:
                if (words.Count > 1)
                {
                    words.RemoveAt(rng.NextInt(words.Count));
                }

                break;
            default:
                rng.Shuffle(words);
                break;
        }

        return string.Join(' ', words);
    }

    private static PromptSpec MakePrompt(SeededRng rng)
    {
        var prompt = rng.Pick(Harness.Bundle.Prompts.Values.ToArray());
        var intents = Harness.Bundle.Intents.Intents.Select(static i => i.Id.Value).ToArray();
        List<string> allowed;
        switch (rng.NextInt(10))
        {
            case 0:
            case 1:
            case 2:
            case 3:
                allowed = [.. prompt.AllowedIntentIds];
                break;
            case 4:
                allowed = [];
                break;
            case 5:
            case 6:
            case 7:
                allowed = [.. Enumerable.Range(0, 1 + rng.NextInt(4)).Select(_ => rng.Pick(intents))];
                break;
            case 8:
                allowed = [.. Enumerable.Range(0, 1 + rng.NextInt(3)).Select(_ => rng.Pick(intents)), "no_such_intent"];
                break;
            default:
                allowed = ["no_such_intent", "unknown"];
                break;
        }

        rng.Shuffle(allowed);
        return prompt with { AllowedIntentIds = allowed, Difficulty = (Difficulty)rng.NextInt(3) };
    }

    private sealed record ResolveCase(SentenceRecipe Recipe, ulong PromptSeed, int Mode, string Text);
}
