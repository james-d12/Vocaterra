using System.Security.Cryptography;
using System.Text;
using CsCheck;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// Proof that the Spanish generators do not change unnoticed, and that the Spanish record generates the cases the suite generated before the per-language harness (M35).
/// CsCheck seeds only its first iteration (the later ones draw from its own state), so a stream is compared one case per seed: the first case at each of
/// <see cref="Seeds"/> fixed seeds, one line each, hashed. Two claims:
/// <list type="number">
/// <item>On the frozen inputs (<see cref="FrozenGeneratorInputs"/>: a copy of the bundle's lexicon and grammars that is never edited, M57), the old generator expressions
/// (<c>SpanishTextGen.Text</c>, <c>SentenceGen.Recipe</c> with <c>Realize</c>) and the record's generators hash to the recorded digests. A changed generator, a changed draw
/// order or a changed realisation fails here, not as a silent change of what the oracles test. A content edit of <c>content/game-v1</c> cannot: the inputs are frozen.</item>
/// <item>On the bundle (<see cref="PipelineHarness.Shared"/>), <see cref="FuzzLanguage.Spanish"/>, the record the oracles run on, gives the same streams as the old
/// expressions. The two are compared with each other, not with a recorded digest, so a content edit moves both alike and needs no new record.</item>
/// </list>
/// </summary>
public sealed class FuzzLanguageStreamTests
{
    private const int Seeds = 400;

    // Digests of the first case at each fixed seed on the frozen inputs. Re-record them only when the generators change (a generator, its draw order, the realiser, a
    // mirrored table of LexiconCatalog or PipelineHarness.FixtureGrammars), in the commit that changes them, and say there what changed. Never re-record them for a content
    // edit: none can move them. History: from M35 to M59 they were recorded on content/game-v1 itself, and every content milestone that added a word or a grammar
    // (V10 to V16) re-recorded them with the generators unchanged; M57 (c) moved them to the frozen inputs (copied at 0041eff), the generators unchanged. The sentence
    // digest again at M57 (a), whose realiser finds the time markers of more than one word (TimeMarkerModel) and whose fixture grammars gained two patterns with them;
    // the text digest did not move.
    private const string TextDigest = "24826CDAB4703637C13D991FE7D2FF0010F467C05BCD9D5C6C2F1AF9065D8D25";
    private const string SentenceDigest = "2915B6C25E9A378059C23B9ECD7D001A42130850817872973FCD5395DD0EABE3";

    [Fact]
    public void TheSpanishRecordUsesTheSpanishPackAndTheWholeMutationSet()
    {
        Assert.Same(SpanishLanguagePack.Instance, FuzzLanguage.Spanish.Pack);
        Assert.Equal(Enum.GetValues<MutationKind>().Length, FuzzLanguage.Spanish.Mutations.Count);
    }

    [Fact]
    public void TheTextStreamOnTheFrozenInputsIsTheRecordedOne()
    {
        var frozen = FrozenGeneratorInputs.Instance;
        Pinned(TextDigest, FirstCases(SpanishTextGen.Text(frozen.Words), static text => text, "text"));
        Pinned(TextDigest, FirstCases(FuzzLanguage.SpanishFreeText(frozen), static text => text, "text"));
    }

    [Fact]
    public void TheSentenceStreamOnTheFrozenInputsIsTheRecordedOne()
    {
        var frozen = FrozenGeneratorInputs.Instance;
        var old = SentenceGen.Recipe(frozen).Select(recipe => SentenceGen.Realize(frozen, recipe));
        Pinned(SentenceDigest, FirstCases(old, static s => Render(s?.Text, s?.PatternSource, s?.PatternText), "sentence"));
        Pinned(SentenceDigest, FirstCases(FuzzLanguage.SpanishSentences(frozen), static s => Render(s?.Text, s?.PatternSource, s?.PatternText), "sentence"));
    }

    [Fact]
    public void TheSpanishRecordGivesTheOldStreamsOnTheBundle()
    {
        var harness = PipelineHarness.Shared;
        Assert.Equal(
            FirstCases(SpanishTextGen.Text(harness.Words), static text => text, "text"),
            FirstCases(FuzzLanguage.Spanish.FreeText, static text => text, "text"));
        var old = SentenceGen.Recipe(harness).Select(recipe => SentenceGen.Realize(harness, recipe));
        Assert.Equal(
            FirstCases(old, static s => Render(s?.Text, s?.PatternSource, s?.PatternText), "sentence"),
            FirstCases(FuzzLanguage.Spanish.Sentences, static s => Render(s?.Text, s?.PatternSource, s?.PatternText), "sentence"));
    }

    [Fact]
    public void TheFrozenInputsAreACopyThatTheDigestsExercise()
    {
        // The fixture is what its files say (the bundle's hand-authored lexicon files and its 92 grammars at 0041eff), it is not the bundle, and most first cases
        // are realised sentences: a digest of 400 skipped recipes would guard nothing.
        var frozen = FrozenGeneratorInputs.Instance;
        Assert.Equal(1299, frozen.Lexicon.Entries.Count);
        Assert.Equal(92, frozen.GrammarCount);
        Assert.NotSame(PipelineHarness.Shared.Lexicon, frozen.Lexicon);
        Assert.DoesNotContain(Path.Combine("content", "game-v1"), frozen.Directory, StringComparison.Ordinal);
        var realised = 0;
        for (var i = 0; i < Seeds; i++)
        {
            FuzzLanguage.SpanishSentences(frozen).Sample(sentence => realised += sentence is null ? 0 : 1, iter: 1, threads: 1, seed: Seed("sentence", i));
        }

        Assert.True(realised * 2 >= Seeds, $"Only {realised} of the {Seeds} first cases on the frozen inputs are realised sentences.");
    }

    private static string Render(string? text, string? source, string? pattern) => text is null ? "<null>" : text + "|" + source + "|" + pattern;

    // A pinned digest, compared in full (xUnit truncates long strings in its messages, and the digest is the evidence).
    private static void Pinned(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            Assert.Fail($"The stream digest is {actual}, not the recorded {expected}.");
        }
    }

    // The first case of the generator at each fixed seed, as one line each, hashed.
    private static string FirstCases<T>(Gen<T> gen, Func<T, string> render, string stream)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < Seeds; i++)
        {
            gen.Sample(value => builder.Append(render(value)).Append('\n'), iter: 1, threads: 1, seed: Seed(stream, i));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    // The fixed seed of the i-th first case of a stream (the seeds of M35).
    private static string Seed(string stream, int i)
    {
        return "M35" + stream[..1].ToUpperInvariant() + i.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
    }
}
