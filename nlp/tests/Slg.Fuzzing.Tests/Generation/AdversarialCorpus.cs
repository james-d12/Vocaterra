using CsCheck;
using Slg.Phonology;

namespace Slg.Fuzzing.Tests.Generation;

/// <summary>
/// Fixed edge-case inputs mixed into the benign generators at a 1-in-4 weight: the strings a normalizer, analyzer or template engine is
/// most likely to trip on, the audio shapes a front-end is most likely to trip on, the phone sequences a scorer is most likely to trip on,
/// and the JSON values a content loader is most likely to trip on.
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

    /// <summary>
    /// Audio edge shapes: empty and one-sample clips, clips of exactly one voice-activity frame and one sample either side of it, every kind of
    /// signal at full scale, a clipped square wave, out-of-phase stereo that downmixes to silence, a DC offset, a long clip at the highest rate (mono, to keep the cost down; the generator itself reaches 5 s).
    /// </summary>
    public static IReadOnlyList<AudioSpec> AudioShapes { get; } =
    [
        new(16000, 1, WavEncoding.Pcm16, WavLayout.Canonical, 0, SignalKind.Silence, 440.0, 0.0, 0, false),
        new(8000, 1, WavEncoding.Pcm16, WavLayout.Canonical, 0, SignalKind.Sine, 440.0, 0.5, 0, false),
        new(96000, 2, WavEncoding.Float32, WavLayout.Canonical, 0, SignalKind.WhiteNoise, 440.0, 1.0, 1, false),
        new(8000, 1, WavEncoding.Pcm16, WavLayout.Canonical, 1, SignalKind.Impulse, 440.0, 1.0, 0, false),
        new(11025, 1, WavEncoding.Pcm16, WavLayout.Canonical, 1, SignalKind.DcOffset, 440.0, 1.0, 0, true),
        new(16000, 1, WavEncoding.Pcm16, WavLayout.Canonical, 19, SignalKind.Sine, 1000.0, 0.8, 0, false),
        new(16000, 1, WavEncoding.Pcm16, WavLayout.Canonical, 20, SignalKind.Sine, 1000.0, 0.8, 0, false),
        new(16000, 1, WavEncoding.Pcm16, WavLayout.Canonical, 21, SignalKind.Sine, 1000.0, 0.8, 0, false),
        new(44100, 1, WavEncoding.Pcm16, WavLayout.ExtraChunks, 20, SignalKind.TwoTone, 440.0, 0.9, 0, false),
        new(48000, 2, WavEncoding.Pcm16, WavLayout.Extensible, 250, SignalKind.Sine, 220.0, 1.0, 1, false),
        new(22050, 2, WavEncoding.Float32, WavLayout.Extensible, 250, SignalKind.Sine, 220.0, 1.0, 1, false),
        new(48000, 1, WavEncoding.Pcm16, WavLayout.Canonical, 250, SignalKind.ClippedSquare, 100.0, 1.0, 0, false),
        new(44100, 2, WavEncoding.Pcm16, WavLayout.Canonical, 250, SignalKind.DcOffset, 0.0, 1.0, 2, true),
        new(16000, 1, WavEncoding.Pcm16, WavLayout.Canonical, 250, SignalKind.Silence, 0.0, 0.0, 0, false),
        new(16000, 1, WavEncoding.Pcm16, WavLayout.Canonical, 250, SignalKind.Sine, 500.0, 0.0005, 0, false),
        new(8000, 1, WavEncoding.Float32, WavLayout.Canonical, 300, SignalKind.WhiteNoise, 440.0, 1.0, 0, false),
        new(96000, 1, WavEncoding.Pcm16, WavLayout.Canonical, 2000, SignalKind.TwoTone, 3000.0, 1.0, 0, false),
    ];

    /// <summary>
    /// Phone edge sequences (as token lists): one phone, one phone repeated up to the longest length (repeats need a blank frame between to survive
    /// CTC collapse), every vowel, the whole inventory, the learner phones alone, a tap next to a trill, an affricate beside its parts.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> PhoneSequences { get; } =
    [
        [PhoneInventory.A],
        [PhoneInventory.Tap],
        [PhoneInventory.H],
        [.. Enumerable.Repeat(PhoneInventory.A, 20)],
        [.. Enumerable.Repeat(PhoneInventory.Tap, 20)],
        [PhoneInventory.A, PhoneInventory.E, PhoneInventory.I, PhoneInventory.O, PhoneInventory.U],
        [.. PhoneInventory.All.Take(20)],
        [.. PhoneInventory.All.Skip(20).Take(20)],
        [PhoneInventory.EnglishR, PhoneInventory.Schwa, PhoneInventory.EnglishEi, PhoneInventory.EnglishOu],
        [PhoneInventory.Tap, PhoneInventory.Trill, PhoneInventory.Tap],
        [PhoneInventory.T, PhoneInventory.Sh, PhoneInventory.Ch],
        [PhoneInventory.J, PhoneInventory.W, PhoneInventory.J],
    ];

    /// <summary>
    /// JSON values a field is switched to when the mutation changes its kind: null, empty and huge strings, numbers at the edges of every integer
    /// and floating-point range, booleans, empty and nested containers.
    /// </summary>
    public static IReadOnlyList<string> JsonKinds { get; } =
    [
        "null",
        "\"\"",
        "\" \"",
        "\"0\"",
        "\"true\"",
        "0",
        "-1",
        "1.5",
        "2147483648",
        "-9223372036854775809",
        "1e308",
        "1e999",
        "true",
        "false",
        "[]",
        "{}",
        "[null]",
        "[[]]",
        "{\"a\":{}}",
        "\"" + new string('x', 5_000) + "\"",
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
