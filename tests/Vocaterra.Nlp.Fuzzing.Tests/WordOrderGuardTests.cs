using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M58 (c), by the owner's decision of 2026-10-31 ("go with option 1"): the guard of the <c>word_order</c> mutation reads the words' readings. Found at M57 (O5, CsCheck
/// seed <c>ff2aNwHuJBk8</c>): the case below built <c>estacionamientos suelta plantar satisfecha felicitación</c> from the pattern
/// <c>[NOUN:item] ([NOUN:qty]|[VERB:soltar:spell]|[ART])? [VERB:plantar]? [NOUN:thing] [ADJ]</c> and expected <c>word_order</c>, which the analyzer rightly does not
/// give: <c>plantar</c> can be an adjective and <c>suelta</c> before it a noun, so <c>suelta plantar satisfecha</c> reads as one phrase. The guard now passes over a word
/// that can be a descriptive adjective, as it passed over the adjectives before, and makes no move when a word that can be a noun stands behind them.
/// </summary>
public sealed class WordOrderGuardTests
{
    // The mutated sentence as M57 found it, and as the same case built it on the bundle of M58 (before the fix) once the bundle had grown.
    private static readonly string[] FailingSentences = ["estacionamientos suelta plantar satisfecha felicitación", "circos suelta plantar satisfecha columna"];

    private const string Pattern = "[NOUN:item] ([NOUN:qty]|[VERB:soltar:spell]|[ART])? [VERB:plantar]? [NOUN:thing] [ADJ]";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    // The case is skipped as it was found, or the mutation it now builds is one the analyzer flags as named (O4's comparison), and its recast is the sentence before
    // the mutation (O5's).
    [Fact]
    public void TheCaseOfSeedFf2aNwHuJBk8_NoLongerExpectsAWordOrderTheAnalyzerRightlyDoesNotGive()
    {
        var mutation = MutationGen.Build(Harness, new MutationCase(MutationKind.WordOrder, 64247025744));
        if (mutation is null)
        {
            return;
        }

        Assert.DoesNotContain(mutation.Mutated.Text.ToLowerInvariant(), FailingSentences);
        var grammar = SlotGrammarType.Parse(mutation.Source.PatternText);
        var normalized = PipelineHarness.Normalize(mutation.Mutated.Text);
        var analysis = Harness.Analyzer.Analyze(normalized, [grammar]);
        var names = UnboundNames.Expect(mutation.Mutated.Tokens, normalized, Harness.Words, analysis.MatchedGrammarId is not null);
        Assert.NotNull(names);
        var difference = MutationTagFuzzTests.Compare([.. mutation.Errors, .. names], analysis.Errors);
        Assert.True(difference is null, $"{difference} {mutation.Describe()}");
    }

    // The sentences it built: with the readings the analyzer sees, no word_order, which is why the mutation may not build them.
    [Theory]
    [InlineData("estacionamientos suelta plantar satisfecha felicitación")]
    [InlineData("circos suelta plantar satisfecha columna")]
    public void TheSentencesItBuiltHaveNoWordOrderMistake_ForTheAnalyzer(string sentence)
    {
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence), [SlotGrammarType.Parse(Pattern)]);
        Assert.DoesNotContain(analysis.Errors, static e => e.Tag == "word_order");
    }
}
