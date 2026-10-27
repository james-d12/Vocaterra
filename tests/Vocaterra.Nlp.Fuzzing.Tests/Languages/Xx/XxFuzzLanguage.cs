using CsCheck;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.Xx;

/// <summary>
/// The toy language's <see cref="FuzzLanguage"/> (M35): the toy pack, the three-word bundle, a free-text generator over its words and some
/// adversarial strings (lone surrogates included), and a sentence realiser that builds sentences from the toy grammars. No mutation set is given:
/// the mutation oracles O4 and O5 are not run on the toy language.
/// </summary>
public static class XxFuzzLanguage
{
    private static readonly Lazy<FuzzLanguage> Lazy = new(Build);

    /// <summary>The toy record, built once: the bundle is loaded from a temporary directory on first use.</summary>
    public static FuzzLanguage Instance => Lazy.Value;

    /// <summary>The toy words, in the order the lexicon lists them.</summary>
    public static IReadOnlyList<string> Words { get; } = ["ka", "mo", "ti"];

    private static FuzzLanguage Build()
    {
        var bundle = XxBundle.LoadInTemp();

        // A token is a toy word (in any case, with a mark or a number that the toy normalizer keeps apart), a made-up word that is not in the lexicon, or a
        // short run of random characters, which includes lone surrogates.
        var word = Gen.OneOf(
            Gen.Const("ka"),
            Gen.Const("mo"),
            Gen.Const("ti"),
            Gen.Const("KA"),
            Gen.Const("Mo!"),
            Gen.Const("ti?"),
            Gen.Const("nu"),
            Gen.Const("zed"),
            Gen.Const("7"));
        var random = Gen.Char.Array[1, 4].Select(static chars => new string(chars));
        var loneSurrogate = Gen.OneOf(
            Gen.Const("ka\uD800"),
            Gen.Const("\uDC00mo"),
            Gen.Const("ti\uD83Dx"));
        var token = Gen.OneOf(word, random, loneSurrogate);
        var freeText = Gen.Select(
            token.Array[0, 6],
            Gen.Bool,
            static (tokens, tight) => string.Join(tight ? " " : "  ", tokens));

        // The realised toy sentences: the words of one grammar in order, each marked as built from the catalog.
        var grammarIds = bundle.Intents.Grammars.Select(static g => g.Id).ToHashSet(StringComparer.Ordinal);
        var built = new[]
        {
            new FuzzSentence("mo ka", "catalog", "mo [NOUN]"),
            new FuzzSentence("mo ti", "catalog", "mo [NOUN]"),
            new FuzzSentence("ka", "catalog", "ka"),
            new FuzzSentence("ti", "catalog", "ti"),
        };
        foreach (var sentence in built)
        {
            if (!grammarIds.Contains(sentence.PatternText))
            {
                throw new InvalidOperationException($"The toy bundle has no grammar '{sentence.PatternText}'.");
            }
        }

        var choices = built.Select(static s => (IGen<FuzzSentence?>)Gen.Const<FuzzSentence?>(s)).Append((IGen<FuzzSentence?>)Gen.Const((FuzzSentence?)null)).ToArray();
        var sentences = Gen.OneOf(choices);

        return new FuzzLanguage(XxLanguagePack.Instance, bundle, freeText, sentences, []);
    }
}
