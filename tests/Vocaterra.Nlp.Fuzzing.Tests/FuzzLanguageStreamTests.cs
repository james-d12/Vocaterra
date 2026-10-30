using System.Security.Cryptography;
using System.Text;
using CsCheck;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// Proof that the Spanish suite generates the cases it generated before the per-language harness (M35). CsCheck seeds only its first iteration (the
/// later ones draw from its own state), so a stream is compared one case per seed: for each of <see cref="Seeds"/> fixed seeds, the first case of the
/// old generator expression and the first case of <see cref="FuzzLanguage.Spanish"/> must be the same, and both must hash to the digest recorded
/// from the unchanged generators (<c>SpanishTextGen.Text</c>, <c>SentenceGen.Recipe</c> with <c>Realize</c>). A changed generator, a changed draw order
/// or a changed realisation fails here, not as a silent change of what the oracles test.
/// </summary>
public sealed class FuzzLanguageStreamTests
{
    private const int Seeds = 400;

    // Digests of the first case at each fixed seed, from the generator expressions the Spanish classes used before M35. The generators draw their words and grammars
    // from content/game-v1, so a content edit moves them like a golden: recorded again at the game's V10 (VILLAGE_PLAN), whose lexicon gained panadería, pescador and
    // letrero and whose grammars gained ask_far, ask_availability and the comprar and buscar rows of state_action; the generators did not change.
    private const string OldTextDigest = "60D70E0AE81AEA72BB24C3218A8D3B77BD3340FBCDF4D060172A772896AC73A1";
    private const string OldSentenceDigest = "24324671E57E97C39E283FBBC936C0765F0B43258BE0641E63AA0A7F32F58C75";

    [Fact]
    public void TheSpanishRecordUsesTheSpanishPackAndTheWholeMutationSet()
    {
        Assert.Same(SpanishLanguagePack.Instance, FuzzLanguage.Spanish.Pack);
        Assert.Equal(Enum.GetValues<MutationKind>().Length, FuzzLanguage.Spanish.Mutations.Count);
    }

    [Fact]
    public void TheTextStreamOfTheSpanishRecordIsTheOldOne()
    {
        var words = PipelineHarness.Shared.Words;
        Pinned(OldTextDigest, FirstCases(SpanishTextGen.Text(words), static text => text, "text"));
        Pinned(OldTextDigest, FirstCases(FuzzLanguage.Spanish.FreeText, static text => text, "text"));
    }

    [Fact]
    public void TheSentenceStreamOfTheSpanishRecordIsTheOldOne()
    {
        var harness = PipelineHarness.Shared;
        var old = SentenceGen.Recipe(harness).Select(recipe => SentenceGen.Realize(harness, recipe));
        Pinned(OldSentenceDigest, FirstCases(old, static s => Render(s?.Text, s?.PatternSource, s?.PatternText), "sentence"));
        Pinned(OldSentenceDigest, FirstCases(FuzzLanguage.Spanish.Sentences, static s => Render(s?.Text, s?.PatternSource, s?.PatternText), "sentence"));
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
            var seed = "M35" + stream[..1].ToUpperInvariant() + i.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
            gen.Sample(value => builder.Append(render(value)).Append('\n'), iter: 1, threads: 1, seed: seed);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
