using System.Text;
using CsCheck;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.De;

/// <summary>
/// The German text generator of the fuzz oracles (M37): free text a learner might type or a recognizer might return, benign and adversarial. Tokens are
/// German words with umlauts and <c>ß</c>, nouns with their capitals, the preposition-article contractions, number words and digits, and the Swiss and
/// keyboard spellings (<c>ae oe ue</c>, <c>ss</c>); lines carry German quotes <c>„…“</c> and <c>»…«</c>, final marks, odd spacing, upper case, decomposed
/// umlauts, and the adversarial corpus (lone surrogates included) spliced in at a 1-in-4 weight. <see cref="Tame"/> is the same without the adversarial
/// strings, for the metamorphic oracles that compare a text with its respelled twin.
/// </summary>
public static class GermanFuzzText
{
    /// <summary>The German words the generators draw from: umlauts, the sharp s, nouns, number words, contractions and function words.</summary>
    public static IReadOnlyList<string> Words { get; } =
    [
        "ich", "du", "Sie", "wir", "bin", "habe", "möchte", "kann", "will", "Hallo", "Danke", "Bitte", "Tschüss", "Grüß", "Gott", "Grüezi",
        "Straße", "Strasse", "Fuß", "Fuss", "Käse", "Kaese", "Brötchen", "Broetchen", "Mädchen", "Maedchen", "schön", "schoen", "schon", "für", "fuer", "über", "ueber",
        "Haus", "Hund", "Katze", "Wasser", "Brot", "Apfel", "Äpfel", "Ärztin", "Öl", "Übung", "Österreich", "Zürich", "Köln",
        "ein", "eine", "einen", "einem", "einer", "der", "die", "das", "dem", "den", "und", "oder", "nicht", "kein",
        "am", "im", "zum", "zur", "beim", "vom", "ins", "ans", "aufs",
        "eins", "zwei", "drei", "zwölf", "fünf", "einundzwanzig", "hundertvierzig", "zweitausenddreihundert", "dreißig", "dreissig", "zwei hundert", "3", "21", "140",
        "wie", "viel", "kostet", "das", "Wie geht es Ihnen", "Guten Morgen", "Auf Wiedersehen", "E-Mail", "Baden-Württemberg",
    ];

    /// <summary>German free text with noise and, one time in four, an adversarial string spliced in.</summary>
    public static Gen<string> Text { get; } = Build(adversarial: true);

    /// <summary>German free text with noise and no adversarial string: every character is a letter, a digit, a mark or white space.</summary>
    public static Gen<string> Tame { get; } = Build(adversarial: false);

    /// <summary>Writes the umlauts and the sharp s as two letters (<c>ä</c> as <c>ae</c>, <c>ß</c> as <c>ss</c>) and nothing else.</summary>
    /// <param name="text">The text.</param>
    public static string SpellOut(string text)
    {
        var builder = new StringBuilder(text.Length + 4);
        foreach (var c in text)
        {
            builder.Append(c switch
            {
                'ä' => "ae",
                'ö' => "oe",
                'ü' => "ue",
                'ß' => "ss",
                'Ä' => "Ae",
                'Ö' => "Oe",
                'Ü' => "Ue",
                'ẞ' => "SS",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }

    private static Gen<string> Build(bool adversarial)
    {
        var words = Gen.OneOfConst([.. Words]);
        var line = Gen.Select(
            words.Array[0, 7],
            Gen.Int[0, 5],
            Gen.Int[0, 6],
            Gen.Int[0, 3],
            Gen.Bool,
            Gen.Int[0, 3],
            static (tokens, caseMode, quoteMode, spaceMode, nfd, spellMode) => Decorate(tokens, caseMode, quoteMode, spaceMode, nfd, spellMode));
        if (!adversarial)
        {
            return line;
        }

        return Gen.Frequency(
            (3, (IGen<string>)line),
            (1, (IGen<string>)Gen.OneOfConst([.. AdversarialCorpus.Strings])),
            (1, (IGen<string>)Gen.Select(line, Gen.OneOfConst([.. AdversarialCorpus.Strings]), static (text, junk) => text + " " + junk)));
    }

    private static string Decorate(string[] tokens, int caseMode, int quoteMode, int spaceMode, bool nfd, int spellMode)
    {
        var text = string.Join(spaceMode == 1 ? "  " : spaceMode == 2 ? " \t " : " ", tokens);
        text = spellMode switch
        {
            1 => SpellOut(text),
            2 => text.Replace("ß", "ss", StringComparison.Ordinal),
            _ => text,
        };
        text = caseMode switch
        {
            1 => text.ToUpperInvariant(),
            2 => text.ToLowerInvariant(),
            3 => string.Join(' ', text.Split(' ').Select(static w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..])),
            4 => string.Concat(text.Select(static (c, i) => i % 2 == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c))),
            _ => text,
        };
        text = quoteMode switch
        {
            1 => "„" + text + "“",
            2 => "»" + text + "«",
            3 => text + "?",
            4 => text + "!",
            5 => "„" + text + "?“",
            6 => text + ".",
            _ => text,
        };
        return nfd ? text.Normalize(NormalizationForm.FormD) : text;
    }
}
