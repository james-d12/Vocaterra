using System.Globalization;
using CsCheck;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Xunit.Abstractions;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O3: a sentence realised correctly from a grammar analyzes clean (no grammar errors, every word known), matches its own grammar and
/// yields the slot values it was built from. Noise that cannot change the normalizer's output must not change the analysis either.
/// Since M72 it also has a verdict leg: graded with <see cref="TextGrader"/> at an agent's constrained prompt that does not take its intent, the sentence gets no
/// error it does not contain (<see cref="CheckAtAPromptThatDoesNotTakeIt"/>).
/// </summary>
public sealed class CorrectSentenceFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O3";

    // Below this many iterations the shares of the homographs are too noisy to assert, and an accent is too rarely dropped.
    private const int MinimumIterationsForCoverage = 1000;

    // The fixed recipes the coverage pass (M71) gives each catalog grammar the random sample left out. Every grammar of content/game-v1 gave a sentence from at least
    // 90 % of its recipes when M71 measured it (README, "Bounds the suite needs"), so the first of these nearly always does, and a grammar none of them realises is
    // one the realiser cannot.
    private const int TargetedRecipes = 50;

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    // The verdict leg (M72): the grader of the content lint and of a host's typed input, and the analyzer it uses, over the same bundle.
    private static readonly Lazy<TextGrader> Grader = new(static () => new TextGrader(Harness.Bundle));

    private static readonly Lazy<LinguisticAnalyzer> GraderAnalyzer = new(static () =>
        new LinguisticAnalyzer(Harness.Bundle.Lexicon, Harness.Bundle.LanguageMorphology, Harness.Bundle.Language, Harness.Bundle.AnalysisRules));

    // The prompts of the leg: every constrained prompt of the bundle that an agent asks and that lists the intents it takes (an NPC's open prompt, a stall's, a
    // lesson step's), in ordinal order.
    private static readonly Lazy<PromptSpec[]> AgentPrompts = new(static () =>
        [.. Harness.Bundle.Prompts.Values
            .Where(static p => p.Mode == RecognitionMode.Constrained && p.AgentId is not null && p.AllowedIntentIds.Count > 0)
            .OrderBy(static p => p.PromptId, StringComparer.Ordinal)]);

    // The realiser seeds and the noise of the coverage pass: drawn once from the generators O3 samples, one case per fixed seed (CsCheck seeds only the first case of a
    // run, as SkipBudgetTests and FuzzLanguageStreamTests note), so the pass is the same on every run.
    private static readonly Lazy<IReadOnlyList<(ulong Seed, TextNoise Noise)>> TargetedInputs = new(static () =>
    {
        var inputs = new List<(ulong Seed, TextNoise Noise)>(TargetedRecipes);
        var gen = Gen.Select(Gen.ULong, SentenceGen.SafeNoise, static (seed, noise) => (seed, noise));
        for (var i = 0; i < TargetedRecipes; i++)
        {
            gen.Sample(inputs.Add, iter: 1, threads: 1, seed: "M71T" + i.ToString("D8", CultureInfo.InvariantCulture));
        }

        return inputs;
    });

    [Fact]
    public void CorrectSentences_AnalyzeClean_MatchTheirGrammar_AndYieldTheirSlots()
    {
        var diagnostics = new FuzzDiagnostics();
        var gen = Gen.Select(SentenceGen.Recipe(Harness), SentenceGen.SafeNoise, static (recipe, noise) => (recipe, noise));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);

        gen.Sample(
            ((SentenceRecipe Recipe, TextNoise Noise) input) =>
            {
                // A statement, not a predicate: a recipe the realiser skips is counted, not failed.
                _ = CheckRecipe(input.Recipe, input.Noise, diagnostics, string.Empty);
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        foreach (var pair in Harness.Words.Diagnostics)
        {
            diagnostics.Add(pair.Key, pair.Value);
        }

        output.WriteLine("Sentence generator diagnostics:\n" + diagnostics.Report());
        var realized = diagnostics.Sum("sentence:");
        var skipped = diagnostics.Sum("skip:");
        Assert.True(realized * 10 >= (realized + skipped) * 6, $"Only {realized} of {realized + skipped} recipes could be realised; the generator is no longer honest enough.\n{diagnostics.Report()}");

        // The homographs of the lexicon are generated, not skipped: the analyzer resolves the part of speech of each word (M18).
        Assert.True(diagnostics.HomographSkipsAreRare, $"{diagnostics.HomographSkips} of {diagnostics.Recipes} recipes were skipped as homographs.\n{diagnostics.Report()}");

        if (iterations >= MinimumIterationsForCoverage)
        {
            // Homographs must really be drawn, or the line above would hold for a generator that stopped using them.
            foreach (var kind in new[] { "a-noun-that-is-also-a-verb-form", "a-verb-form-that-is-also-a-noun", "a-noun-of-both-genders" })
            {
                Assert.True(
                    diagnostics.Get($"homograph:sentence-with-{kind}") * 50 >= realized,
                    $"Fewer than one sentence in fifty holds {kind.Replace('-', ' ')}.\n{diagnostics.Report()}");
            }
        }

        // The verdict leg (M72) must really grade sentences off their prompts, and some of them must be understood off the prompt.
        Assert.True(diagnostics.Get("verdict-leg:graded") > 0, $"No correct sentence was graded at a prompt that does not take it.\n{diagnostics.Report()}");
        if (iterations >= MinimumIterationsForCoverage)
        {
            Assert.True(diagnostics.Get("verdict-leg:off-prompt") > 0, $"No correct sentence graded off its prompt carried an off-prompt intent.\n{diagnostics.Report()}");
        }

        // Every grammar word must be in the lexicon: a catalog grammar with an unknown literal (the old `cuál` gap) would be skipped here, and the
        // utterance a learner says for it would be reported as vocab.unknown.
        Assert.True(diagnostics.Sum("skip:unknown-literal") == 0, $"A literal word of a catalog or generated grammar is missing from the lexicon.\n{diagnostics.Report()}");

        // Every grammar of the catalog is realised at least once, through the checks above (M71). The sample draws a catalog grammar in two recipes of five, about seven
        // recipes for each grammar of content/game-v1 at the default depth (110 grammars at M71), and left one of them out in about one run in thirteen; so each grammar
        // it left out is realised from fixed recipes, and only one that none of them realises fails, with its rejections listed. The counts and assertions above are
        // those of the sample alone.
        var coverage = new FuzzDiagnostics();
        var unrealised = RealiseWhatTheSampleLeftOut(Harness.Grammars.Grammars.Select(static g => g.Pattern), diagnostics, coverage);
        output.WriteLine("Coverage pass:\n" + coverage.Report());
        Assert.True(unrealised.Count == 0, $"A catalog grammar was never realised:\n  {string.Join("\n  ", unrealised)}\n{diagnostics.Report()}");
    }

    /// <summary>
    /// The coverage pass of O3 can fail (M71): a grammar the realiser cannot realise from any recipe (here one with a literal the lexicon lacks) is reported with the
    /// reasons of its rejections, a grammar it can realise is not, and a grammar the sample already realised is not given the pass.
    /// </summary>
    [Fact]
    public void TheCoveragePass_ReportsOnlyAGrammarTheRealiserCannotRealise()
    {
        var sample = new FuzzDiagnostics();
        var realisedBefore = GrammarPattern.Parse("[VERB:comer] [ART] [NOUN:item]");
        sample.Count("catalog:" + realisedBefore.Render());
        var patterns = new[] { realisedBefore, GrammarPattern.Parse("muchas gracias"), GrammarPattern.Parse("zqxjv [NOUN:item]") };
        var coverage = new FuzzDiagnostics();

        var unrealised = RealiseWhatTheSampleLeftOut(patterns, sample, coverage);

        var gap = Assert.Single(unrealised);
        Assert.StartsWith("'zqxjv [NOUN:item]': no sentence from the sample nor from " + TargetedRecipes.ToString(CultureInfo.InvariantCulture) + " fixed recipes", gap, StringComparison.Ordinal);
        Assert.Contains("skip:unknown-literal:zqxjv = " + TargetedRecipes.ToString(CultureInfo.InvariantCulture), gap, StringComparison.Ordinal);
        Assert.Equal(2, coverage.Get("coverage:grammars-the-sample-left-out"));
        Assert.Equal(1, coverage.Get("coverage:recipes-to-realise:muchas gracias"));
    }

    // The coverage pass (M71): each pattern the sample did not realise (no catalog: count in it) is realised from the fixed recipes of TargetedInputs, one after another
    // until one gives a sentence, which goes through the same checks as the sample's. Returns one line for each pattern that no recipe realised, with its rejections.
    private static List<string> RealiseWhatTheSampleLeftOut(IEnumerable<GrammarPattern> patterns, FuzzDiagnostics sample, FuzzDiagnostics coverage)
    {
        var unrealised = new List<string>();
        foreach (var pattern in patterns)
        {
            var rendered = pattern.Render();
            if (sample.Get("catalog:" + rendered) > 0)
            {
                continue;
            }

            var own = new FuzzDiagnostics();
            var recipes = 0;
            while (recipes < TargetedRecipes && own.Get("catalog:" + rendered) == 0)
            {
                var (seed, noise) = TargetedInputs.Value[recipes++];
                _ = CheckRecipe(new SentenceRecipe("catalog", pattern, seed), noise, own, $" coverage-pass-recipe={recipes}");
            }

            coverage.Count("coverage:grammars-the-sample-left-out");
            coverage.Add("coverage:recipes-to-realise:" + rendered, recipes);
            if (own.Get("catalog:" + rendered) == 0)
            {
                var rejections = own.Report().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static line => line.Trim())
                    .Where(static line => line.StartsWith(FuzzDiagnostics.RejectedPrefix, StringComparison.Ordinal) || line.StartsWith("skip:", StringComparison.Ordinal));
                unrealised.Add($"'{rendered}': no sentence from the sample nor from {recipes} fixed recipes; {string.Join(", ", rejections)}");
            }
        }

        return unrealised;
    }

    // Realises a recipe and checks the sentence: it analyzes clean, also under noise the normalizer removes, matches its own grammar and yields its slot values. Returns
    // false when the realiser gave no sentence (counted in the diagnostics). The origin names the coverage pass in a failure's context.
    private static bool CheckRecipe(SentenceRecipe recipe, TextNoise noise, FuzzDiagnostics diagnostics, string origin)
    {
        var sentence = SentenceGen.Realize(Harness, recipe, diagnostics: diagnostics);
        if (sentence is null)
        {
            return false;
        }

        var grammar = SlotGrammarType.Parse(recipe.Pattern.Render());
        var noisy = noise.Apply(sentence.Text);
        var normalized = PipelineHarness.Normalize(noisy);
        var analysis = Harness.Analyzer.Analyze(normalized, [grammar]);
        var context = $"{sentence.Describe()} noisy=\"{noisy}\" seed={recipe.Seed}{origin}";

        var errors = analysis.Errors.Where(static e => !ErrorTags.IsPronunciation(e.Tag)).ToArray();
        if (errors.Length > 0)
        {
            throw Determinism.Fail(Oracle, $"A correct sentence produced grammar errors: {string.Join("; ", errors.Select(Describe))}.", context);
        }

        if (!string.Equals(normalized.Text, sentence.NormalizedText, StringComparison.Ordinal))
        {
            throw Determinism.Fail(Oracle, $"Noise changed the normalized text: '{normalized.Text}' instead of '{sentence.NormalizedText}'.", context);
        }

        if (!string.Equals(analysis.MatchedGrammarId, grammar.Id, StringComparison.Ordinal))
        {
            throw Determinism.Fail(Oracle, $"The sentence did not match its own grammar; matched '{analysis.MatchedGrammarId ?? "(none)"}'.", context);
        }

        var slots = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slot in analysis.Slots)
        {
            slots.TryAdd(slot.Name, slot.Value);
        }

        if (!SlotsAgree(slots, sentence, recipe.Pattern.HasAmbiguousSlots(Harness.Words.PatternClassesOf) || HomographCompetes(recipe.Pattern, sentence)))
        {
            throw Determinism.Fail(
                Oracle,
                $"Slot values differ. Expected {{{string.Join(", ", sentence.ExpectedSlots.Select(p => $"{p.Key}={p.Value}"))}}} but got {{{string.Join(", ", slots.Select(p => $"{p.Key}={p.Value}"))}}}.",
                context);
        }

        if (analysis.Confidence != 1.0)
        {
            throw Determinism.Fail(Oracle, $"Confidence is {analysis.Confidence}, but every word of the sentence is in the lexicon.", context);
        }

        CheckAtAPromptThatDoesNotTakeIt(recipe, grammar.Id, noisy, normalized, analysis, diagnostics, context);
        return true;
    }

    /// <summary>
    /// The verdict leg of O3 (M72, a widening): the sentence, graded with <see cref="TextGrader"/> at a constrained prompt an agent asks that does not take its
    /// intent, gets no error that the sentence does not contain. Its intents are the intents that own its grammar (a catalog grammar) and the intent the resolver
    /// gives it with every intent allowed; the prompt, drawn from the recipe's seed, allows none of them. What a sentence contains is what the analyzer finds in its
    /// words alone, with no grammar of any prompt: for a correct sentence nothing but <c>vocab.unknown</c> on a name only a <c>[WORD]</c> of a grammar reads (no
    /// lexicon has it). Errors are compared by tag and span. A pronunciation tag is not grammar and is left out on both sides, as in O3's first check (the grader
    /// scores no pronunciation, so none arises). Before M72 the derived <c>vocab.wrong</c> fell on such a sentence at every prompt whose answers it missed.
    /// </summary>
    private static void CheckAtAPromptThatDoesNotTakeIt(
        SentenceRecipe recipe,
        string grammarId,
        string text,
        NormalizedText normalized,
        AnalysisResult analysis,
        FuzzDiagnostics diagnostics,
        string context)
    {
        var intents = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grammar in Harness.Grammars.Grammars.Where(g => string.Equals(g.Id, grammarId, StringComparison.Ordinal)))
        {
            intents.UnionWith(grammar.IntentIds);
        }

        var everyIntent = Harness.NewResolver(embeddings: false).Resolve(normalized, analysis, AgentPrompts.Value[0] with { AllowedIntentIds = [] });
        if (!everyIntent.Intent.IsUnknown)
        {
            intents.Add(everyIntent.Intent.Value);
        }

        var prompts = AgentPrompts.Value.Where(p => !p.AllowedIntentIds.Any(intents.Contains)).ToArray();
        if (prompts.Length == 0)
        {
            diagnostics.Count("verdict-leg:every-prompt-takes-it");
            return;
        }

        var prompt = new SeededRng(recipe.Seed ^ 0x4D37_3256_4C45_4731UL).Pick(prompts);
        var verdict = Grader.Value.Grade(text, prompt);
        var contained = GraderAnalyzer.Value.Analyze(normalized, []).Errors
            .Where(static e => !ErrorTags.IsPronunciation(e.Tag))
            .Select(static e => (e.Tag, e.Span))
            .ToHashSet();
        var added = verdict.Errors.Where(e => !ErrorTags.IsPronunciation(e.Tag) && !contained.Contains((e.Tag, e.Span))).ToArray();
        if (added.Length > 0)
        {
            throw Determinism.Fail(
                Oracle,
                $"A correct sentence graded at {prompt.PromptId}, which takes none of its intents [{string.Join(", ", intents.Order(StringComparer.Ordinal))}], got errors it does not contain: "
                + $"{string.Join("; ", added.Select(Describe))} (intent {verdict.Intent}, off the prompt {verdict.OffPromptIntent?.Value ?? "none"}).",
                context);
        }

        diagnostics.Count("verdict-leg:graded");
        diagnostics.Count(verdict.Intent.IsUnknown ? (verdict.OffPromptIntent is null ? "verdict-leg:not-understood" : "verdict-leg:off-prompt") : "verdict-leg:taken-by-another-intent");
        if (contained.Count > 0)
        {
            diagnostics.Count("verdict-leg:contains-an-error-alone");
        }
    }

    /// <summary>
    /// A learner who types without accents writes <c>el</c> for <c>él</c> and <c>esta</c> for <c>está</c>, and both spellings are lexicon words (M26b, CR-013). When a correct sentence has
    /// an accented word whose plain spelling is also a lexicon form, and the words next to it leave no doubt which one is meant (an independent model of the rule below), the same sentence
    /// written without that accent must analyze exactly as the accented one does: the same errors at the same places. The cases the neighbours leave open are counted and not asserted,
    /// because a reader cannot tell them apart either (<c>él compra</c> and <c>el compra</c>, where <c>compra</c> is a noun as well).
    /// </summary>
    [Fact]
    public void CorrectSentences_WithAnAccentDroppedWhereTheNeighboursLeaveNoDoubt_AnalyzeAsTheAccentedSentenceDoes()
    {
        var diagnostics = new FuzzDiagnostics();
        var gen = SentenceGen.Recipe(Harness);
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);

        gen.Sample(
            (SentenceRecipe recipe) =>
            {
                var sentence = SentenceGen.Realize(Harness, recipe, diagnostics: diagnostics);
                if (sentence is null)
                {
                    return;
                }

                var tokens = sentence.Tokens.Select(static t => t.Norm).ToArray();
                var plain = tokens.ToArray();
                var changed = new List<string>();
                for (var i = 0; i < tokens.Length; i++)
                {
                    var spelling = tokens[i];
                    var folded = Harness.Lexicon.LookupWithAccentVariants(Accents.Strip(spelling));
                    if (spelling == Accents.Strip(spelling) || folded.Exact.Count == 0 || !folded.Variants.Any(v => v.Form == spelling))
                    {
                        continue;
                    }

                    if (!UnaccentedReadingIsContradicted(tokens, i, sentence.Tokens[i].Role))
                    {
                        diagnostics.Count("accent:open-" + Accents.Strip(spelling));
                        continue;
                    }

                    diagnostics.Count("accent:dropped-" + Accents.Strip(spelling));
                    plain[i] = Accents.Strip(spelling);
                    changed.Add(spelling);
                }

                if (changed.Count == 0)
                {
                    return;
                }

                var accented = Harness.Analyzer.Analyze(PipelineHarness.Normalize(string.Join(' ', tokens)), []);
                var dropped = Harness.Analyzer.Analyze(PipelineHarness.Normalize(string.Join(' ', plain)), []);
                var context = $"{sentence.Describe()} dropped=[{string.Join(", ", changed)}] unaccented=\"{string.Join(' ', plain)}\" seed={recipe.Seed}";
                var expected = accented.Errors.Where(static e => !ErrorTags.IsPronunciation(e.Tag)).Select(Describe).ToArray();
                var actual = dropped.Errors.Where(static e => !ErrorTags.IsPronunciation(e.Tag)).Select(Describe).ToArray();
                if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
                {
                    throw Determinism.Fail(
                        Oracle,
                        $"Dropping the accent changed the errors: with it [{string.Join("; ", expected)}], without it [{string.Join("; ", actual)}].",
                        context);
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Unaccented function word diagnostics:\n" + diagnostics.Report());
        if (iterations >= MinimumIterationsForCoverage)
        {
            // The accented function words must really be drawn and unaccented, or the oracle above would hold for a generator that stopped producing them.
            Assert.True(diagnostics.Sum("accent:dropped-") > 0, $"No accent was ever dropped from a function word.\n{diagnostics.Report()}");
        }
    }

    // The plain spellings of the lexicon's forms (accents stripped, ñ kept), built once: what a word typed with n for ñ must not be for the slip to be read (M70).
    private static readonly Lazy<HashSet<string>> PlainForms = new(static () => Harness.Lexicon.Entries.Select(static e => Accents.Strip(e.Form)).ToHashSet(StringComparer.Ordinal));

    // The Spanish words that differ from another only in an n where the other has ñ, as this test models them (an independent copy of the idea, not of the pack's
    // list): año/ano, caña/cana, caño/cano, cañón/canon, ceño/ceno, coño/cono, cuña/cuna, maña/mana, moña/mona, moño/mono, pañal/panal, peña/pena, piña/pina,
    // piño/pino, seña/sena, tiña/tina, uña/una.
    private static readonly HashSet<string> WordsWithAnN = new(StringComparer.Ordinal)
    {
        "ano", "anos", "cana", "canas", "cano", "canos", "canon", "canones", "ceno", "cono", "conos", "cuna", "cunas", "mana", "manas", "mona", "monas", "mono",
        "monos", "panal", "panales", "pena", "penas", "pina", "pinas", "pino", "pinos", "sena", "senas", "tina", "tinas", "una", "unas",
    };

    /// <summary>
    /// M70 (a widening; the owner's answer of 2026-11-01, D-game-22 item 2): a learner who types without the tilde writes <c>manana</c> for <c>mañana</c>. When a correct
    /// sentence has a word with <c>ñ</c> whose spelling with <c>n</c> is no lexicon form (accents ignored) and no Spanish word of this test's own list, the sentence written
    /// with that <c>n</c> must analyze as the sentence with <c>ñ</c> does: the same errors at the same places with the same expected forms (the heard text is the
    /// learner's spelling). A word whose spelling with <c>n</c> is a word (<c>una</c>, <c>pena</c>) is counted and left out: it is another word.
    /// </summary>
    [Fact]
    public void CorrectSentences_WithAnNTildeTypedAsNWhereThatSpellingIsNoWord_AnalyzeAsTheSentenceWithIt()
    {
        var diagnostics = new FuzzDiagnostics();
        var gen = SentenceGen.Recipe(Harness);
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);

        gen.Sample(
            (SentenceRecipe recipe) =>
            {
                var sentence = SentenceGen.Realize(Harness, recipe, diagnostics: diagnostics);
                if (sentence is null)
                {
                    return;
                }

                var tokens = sentence.Tokens.Select(static t => t.Norm).ToArray();
                var plain = tokens.ToArray();
                var changed = new List<string>();
                for (var i = 0; i < tokens.Length; i++)
                {
                    if (!tokens[i].Contains('ñ', StringComparison.Ordinal) || sentence.Tokens[i].Role == TokenRole.Name)
                    {
                        continue;
                    }

                    var typed = tokens[i].Replace('ñ', 'n');
                    if (PlainForms.Value.Contains(Accents.Strip(typed)) || WordsWithAnN.Contains(Accents.Strip(typed)))
                    {
                        diagnostics.Count("ntilde:a-word-" + Accents.Strip(typed));
                        continue;
                    }

                    diagnostics.Count("ntilde:typed-as-n");
                    plain[i] = typed;
                    changed.Add(tokens[i]);
                }

                if (changed.Count == 0)
                {
                    return;
                }

                var withTilde = Harness.Analyzer.Analyze(PipelineHarness.Normalize(string.Join(' ', tokens)), []);
                var withN = Harness.Analyzer.Analyze(PipelineHarness.Normalize(string.Join(' ', plain)), []);
                var context = $"{sentence.Describe()} typed=[{string.Join(", ", changed)}] withN=\"{string.Join(' ', plain)}\" seed={recipe.Seed}";
                var expected = withTilde.Errors.Where(static e => !ErrorTags.IsPronunciation(e.Tag)).Select(DescribeWithoutHeard).ToArray();
                var actual = withN.Errors.Where(static e => !ErrorTags.IsPronunciation(e.Tag)).Select(DescribeWithoutHeard).ToArray();
                if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
                {
                    throw Determinism.Fail(
                        Oracle,
                        $"Typing n for ñ changed the errors: with ñ [{string.Join("; ", expected)}], with n [{string.Join("; ", actual)}].",
                        context);
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("N for ñ diagnostics:\n" + diagnostics.Report());
        if (iterations >= MinimumIterationsForCoverage)
        {
            // Words with ñ must really be drawn and typed with n, or the oracle above would hold for a generator that stopped producing them.
            Assert.True(diagnostics.Sum("ntilde:typed-as-n") > 0, $"No ñ was ever typed as n.\n{diagnostics.Report()}");
        }
    }

    private static string DescribeWithoutHeard(ErrorTag error)
    {
        return $"{error.Tag}@{error.Span.Start}+{error.Span.Length} expected '{error.Expected}'";
    }

    // The independent model of the resolver's rule for the words the generators draw: a subject pronoun (él, tú) written without its accent is read as the pronoun when the next word
    // cannot go on a noun phrase (no noun, adjective or determiner reading); está and estás written without the accent are the verb when the words after them are not a noun phrase
    // (an article right after, or no noun at the end of the adjectives that follow). Anything else leaves the reader in doubt.
    private static bool UnaccentedReadingIsContradicted(string[] tokens, int index, TokenRole role)
    {
        var spelling = tokens[index];
        if (role == TokenRole.Pronoun && spelling is "él" or "tú")
        {
            return index + 1 < tokens.Length && !CanGoOnANounPhrase(tokens[index + 1]);
        }

        if (role == TokenRole.Verb && spelling is "está" or "estás")
        {
            var next = index + 1;
            if (next >= tokens.Length)
            {
                return true;
            }

            var readings = PartsOfSpeech(tokens[next]);
            if (readings.Count > 0 && readings.All(static pos => pos == LexiconSchema.Article))
            {
                return true;
            }

            // The adjectives, articles and numerals (digits are numerals) after the word belong to the noun phrase that a demonstrative would open.
            var head = next;
            while (head < tokens.Length && PartsOfSpeech(tokens[head]) is { Count: > 0 } found && found.All(static pos => pos is LexiconSchema.Adjective or LexiconSchema.Article or LexiconSchema.Numeral))
            {
                head++;
            }

            return head >= tokens.Length || (PartsOfSpeech(tokens[head]) is { Count: > 0 } headParts && headParts.All(static pos => pos != LexiconSchema.Noun));
        }

        return false;
    }

    private static List<string> PartsOfSpeech(string token)
    {
        return token.Length > 0 && token.All(char.IsAsciiDigit) ? [LexiconSchema.Numeral] : [.. Harness.Lexicon.Lookup(token).Select(static r => r.Pos)];
    }

    private static bool CanGoOnANounPhrase(string token)
    {
        var parts = PartsOfSpeech(token);
        return parts.Count == 0 || parts.Any(static pos => pos is LexiconSchema.Noun or LexiconSchema.Adjective or LexiconSchema.Article or LexiconSchema.Numeral);
    }

    // A word that is a noun and also a verb form (van, busca) or an adjective (adolescente) can be given to an optional [VERB] or [ADJ] token as well as to a [NOUN]
    // slot, and a verb that is also a noun to a [NOUN] token, so the matcher may legitimately bind the words in more than one way; this is the same ambiguity as two
    // optional tokens of one class, caused by the word.
    private static bool HomographCompetes(GrammarPattern pattern, GeneratedSentence sentence)
    {
        return (pattern.HasFlexibleTag("VERB") || pattern.HasFlexibleTag("NOUN") || pattern.HasFlexibleTag("ADJ"))
            && sentence.Tokens.Any(token => token.Generated
                && token.Role is TokenRole.Noun or TokenRole.Verb
                && Harness.Lexicon.Lookup(token.Norm) is var readings
                && readings.Select(static r => r.Pos).Where(static pos => pos is LexiconSchema.Noun or LexiconSchema.Verb or LexiconSchema.Adjective).Distinct().Count() > 1);
    }

    // With an ambiguous pattern the matcher may bind the same tokens to the slots in a different but equally valid way, so only the values count.
    private static bool SlotsAgree(Dictionary<string, string> actual, GeneratedSentence sentence, bool ambiguous)
    {
        var expected = sentence.ExpectedSlots;
        if (ambiguous)
        {
            // Optional tokens that compete for the same word may or may not take it, so the only claim left is that every extracted value
            // is the lemma (or the digits) of some word of the sentence.
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in sentence.Tokens)
            {
                candidates.Add(token.Norm);
                if (token.Lemma is not null)
                {
                    candidates.Add(token.Lemma);
                }

                foreach (var reading in Harness.Lexicon.Lookup(token.Norm))
                {
                    candidates.Add(reading.Lemma);
                }
            }

            return actual.Values.All(candidates.Contains);
        }

        return actual.Count == expected.Count && expected.All(pair => actual.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    private static string Describe(ErrorTag error)
    {
        return $"{error.Tag}@{error.Span.Start}+{error.Span.Length} expected '{error.Expected}' actual '{error.Actual}'";
    }
}
