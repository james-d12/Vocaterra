using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M62b, found by the gate of the game's V15 (O3, CsCheck seed <c>aeG5w1c3Hvh2</c>; older than M62: the analyzer of <c>76f52f8</c> reads the sentence the same way): the
/// random pattern <c>[ART]? [ADJ:item] ([WORD:qty]|[NOUN]|[ADJ] [VERB])?</c> gave <c>un japón Dejo</c>, with a drawn name that is also the lexicon noun <c>dejo</c>. The
/// alternative ties <c>[WORD:qty]</c> with <c>[NOUN]</c>, so the analyzer keeps the noun's readings (a tied parse keeps every reading), and after the adjective, which goes
/// after its noun, the noun <c>dejo</c> makes <c>japón dejo</c> a slip of order. The realiser took the name for a name. It now re-rolls a drawn name that is a lexicon noun
/// right after a determiner or an adjective that belongs to no noun (<c>skip:name-read-as-noun</c>).
/// </summary>
public sealed class NameReadAsNounTests
{
    private const string Pattern = "[ART]? [ADJ:item] ([WORD:qty]|[NOUN]|[ADJ] [VERB])?";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    // The recipe of the failing case: realised now, it is a sentence the analyzer finds correct (O3's comparison), and not the one it built.
    [Fact]
    public void TheCaseOfSeedAeG5w1c3Hvh2_IsNoLongerASentenceTheAnalyzerFlags()
    {
        var recipe = new SentenceRecipe("random", GrammarPattern.Parse(Pattern), 129792773031646320UL);
        var sentence = SentenceGen.Realize(Harness, recipe);
        if (sentence is null)
        {
            return;
        }

        Assert.NotEqual("un japón dejo", sentence.NormalizedText);
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize(sentence.Text), [SlotGrammarType.Parse(Pattern)]);
        Assert.Empty(analysis.Errors);
    }

    // Why the realiser may not build it: under the tied alternative the analyzer reads the name as the noun, and only a grammar that binds it as a name alone leaves it
    // a name.
    [Theory]
    [InlineData(Pattern, "word_order")]
    [InlineData("[ART]? [ADJ:item] [WORD:qty]", "")]
    public void UnderATiedAlternativeTheNameIsTheNoun(string pattern, string tags)
    {
        var analysis = Harness.Analyzer.Analyze(PipelineHarness.Normalize("un japón Dejo"), [SlotGrammarType.Parse(pattern)]);
        Assert.Equal(tags, string.Join(';', analysis.Errors.Select(static e => e.Tag)));
    }
}
