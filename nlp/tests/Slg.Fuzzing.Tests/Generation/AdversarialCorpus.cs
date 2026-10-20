using CsCheck;

namespace Slg.Fuzzing.Tests.Generation;

/// <summary>
/// Fixed edge-case inputs mixed into the benign generators at a 1-in-4 weight: the strings a normalizer, analyzer or template engine is
/// most likely to trip on. Stage 2 adds the audio, phone and JSON-mutation corpora next to the strings.
/// </summary>
public static class AdversarialCorpus
{
    /// <summary>Free-text edge cases: empty and blank, huge, emoji, combining marks, odd Unicode, number words, punctuation, template syntax.</summary>
    public static IReadOnlyList<string> Strings { get; } =
    [
        string.Empty,
        " ",
        "   ",
        "\t\r\n",
        "null",
        "NULL",
        "undefined",
        "0",
        "1",
        "007",
        "12345",
        "-1",
        "1.000",
        "1,5",
        "999999999999999999999999",
        "al",
        "del",
        "AL DEL",
        "¿?",
        "¡!",
        "...",
        "??",
        "¿¿¿",
        "!!!",
        ",,,;;;:::",
        "a-b-c",
        "-a",
        "a-",
        "-----",
        "a\u2010b",
        "a\u2011b",
        "😀",
        "hola 😀 amigo",
        "👩\u200d👩\u200d👧\u200d👦",
        "🇪🇸",
        "e\u0301",
        "\u0301",
        "\u0301\u0301\u0301",
        "n\u0303",
        "n\u0303o",
        "a\u0308\u0301",
        "\u200b",
        "\u200d",
        "\u00a0",
        "\u2028",
        "\ufeff",
        "a\u0000b",
        "\u0000",
        "\ud800",
        "x\udc00y",
        "\ud83d",
        "שלום",
        "مرحبا",
        "你好",
        "Σίσυφος ΟΔΥΣΣΕΥΣ",
        "İstanbul",
        "ǅ",
        "ß",
        "ẞ",
        "ﬃ",
        "ＡＢＣ",
        "ｱ",
        "ı",
        "ÁÉÍÓÚÜÑ",
        "áéíóúüñ",
        "ñandú",
        "NIÑO",
        "mil",
        "mil mil mil",
        "cien cien",
        "ciento",
        "veinte y uno",
        "treinta y un",
        "novecientos noventa y nueve mil novecientos noventa y nueve",
        "un un un",
        "uno uno",
        "una",
        "y y y",
        "{",
        "}",
        "{{",
        "}}",
        "{slot}",
        "{item:plural-if(qty)}",
        "%s %d",
        "${x}",
        "<script>alert(1)</script>",
        "\"quoted\"",
        "'single'",
        "back\\slash",
        "tab\tseparated",
        new string('a', 10_000),
        new string(' ', 10_000),
        string.Concat(Enumerable.Repeat("hola ", 2_000)),
        string.Concat(Enumerable.Repeat("¿", 5_000)),
        string.Concat(Enumerable.Repeat("e\u0301", 3_000)),
    ];

    /// <summary>A generator that draws one corpus string.</summary>
    public static Gen<string> StringGen { get; } = Gen.OneOfConst([.. Strings]);

    /// <summary>
    /// Mixes a benign generator with the corpus at the suite-wide weights (3-in-4 benign, 1-in-4 adversarial).
    /// </summary>
    public static Gen<string> Mix(Gen<string> benign)
    {
        return Gen.Frequency(
            (FuzzOptions.BenignWeight, (IGen<string>)benign),
            (FuzzOptions.AdversarialWeight, (IGen<string>)StringGen));
    }
}
