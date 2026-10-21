namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>The closed vocabularies of the lexicon schema (part of speech and feature values).</summary>
public static class LexiconSchema
{
    /// <summary>The only schema version this library reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Part of speech: noun.</summary>
    public const string Noun = "noun";

    /// <summary>Part of speech: verb.</summary>
    public const string Verb = "verb";

    /// <summary>Part of speech: adjective.</summary>
    public const string Adjective = "adj";

    /// <summary>Part of speech: article.</summary>
    public const string Article = "art";

    /// <summary>Part of speech: pronoun.</summary>
    public const string Pronoun = "pron";

    /// <summary>Part of speech: numeral.</summary>
    public const string Numeral = "num";

    /// <summary>Part of speech: preposition.</summary>
    public const string Preposition = "prep";

    /// <summary>Part of speech: conjunction.</summary>
    public const string Conjunction = "conj";

    /// <summary>Part of speech: adverb.</summary>
    public const string Adverb = "adv";

    /// <summary>Part of speech: interjection.</summary>
    public const string Interjection = "interj";

    /// <summary>Singular number.</summary>
    public const string Singular = "sg";

    /// <summary>Plural number.</summary>
    public const string Plural = "pl";

    /// <summary>Masculine gender.</summary>
    public const string Masculine = "m";

    /// <summary>Feminine gender.</summary>
    public const string Feminine = "f";

    /// <summary>Present tense.</summary>
    public const string Present = "pres";

    /// <summary>Preterite tense.</summary>
    public const string Preterite = "pret";

    /// <summary>Imperfect tense.</summary>
    public const string Imperfect = "impf";

    /// <summary>Future tense.</summary>
    public const string Future = "fut";

    /// <summary>Conditional tense.</summary>
    public const string Conditional = "cond";

    /// <summary>Indicative mood.</summary>
    public const string Indicative = "ind";

    /// <summary>Subjunctive mood.</summary>
    public const string Subjunctive = "subj";

    /// <summary>Imperative mood.</summary>
    public const string Imperative = "imp";

    // The closed vocabularies as arrays: the lexicon reader compares each value of tens of thousands of entries against them.
    internal static readonly string[] PartsOfSpeechArray = ["noun", "verb", "adj", "art", "pron", "num", "prep", "conj", "adv", "interj"];

    internal static readonly string[] PersonsArray = ["1", "2", "3"];

    internal static readonly string[] NumbersArray = ["sg", "pl"];

    internal static readonly string[] TensesArray = ["pres", "pret", "impf", "fut", "cond"];

    internal static readonly string[] MoodsArray = ["ind", "subj", "imp"];

    internal static readonly string[] GendersArray = ["m", "f"];

    /// <summary>All part-of-speech values, in schema order.</summary>
    public static IReadOnlyList<string> PartsOfSpeech { get; } = Array.AsReadOnly(PartsOfSpeechArray);

    /// <summary>All person values.</summary>
    public static IReadOnlyList<string> Persons { get; } = Array.AsReadOnly(PersonsArray);

    /// <summary>All number values.</summary>
    public static IReadOnlyList<string> Numbers { get; } = Array.AsReadOnly(NumbersArray);

    /// <summary>All tense values.</summary>
    public static IReadOnlyList<string> Tenses { get; } = Array.AsReadOnly(TensesArray);

    /// <summary>All mood values.</summary>
    public static IReadOnlyList<string> Moods { get; } = Array.AsReadOnly(MoodsArray);

    /// <summary>All gender values.</summary>
    public static IReadOnlyList<string> Genders { get; } = Array.AsReadOnly(GendersArray);

    /// <summary>Throws <see cref="FormatException"/> when the entry violates the schema.</summary>
    public static void Validate(LexiconEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Require(!string.IsNullOrWhiteSpace(entry.Form), entry, "form must not be empty");
        Require(!string.IsNullOrWhiteSpace(entry.Lemma), entry, "lemma must not be empty");
        Require(Contains(PartsOfSpeech, entry.Pos), entry, $"unknown pos '{entry.Pos}'");

        // A missing "features" property arrives as null although the record declares it non-nullable; an entry without features writes "features": {}.
        if ((object?)entry.Features is null)
        {
            throw new FormatException($"Invalid lexicon entry '{entry.Form}' ({entry.Lemma}/{entry.Pos}): features must be present (use {{}} for none).");
        }

        RequireValue(entry, "person", entry.Features.Person, Persons);
        RequireValue(entry, "number", entry.Features.Number, Numbers);
        RequireValue(entry, "tense", entry.Features.Tense, Tenses);
        RequireValue(entry, "mood", entry.Features.Mood, Moods);
        RequireValue(entry, "gender", entry.Features.Gender, Genders);
    }

    private static void RequireValue(LexiconEntry entry, string name, string? value, IReadOnlyList<string> allowed)
    {
        Require(value is null || Contains(allowed, value), entry, $"unknown {name} '{value}'");
    }

    private static bool Contains(IReadOnlyList<string> values, string value)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void Require(bool condition, LexiconEntry entry, string message)
    {
        if (!condition)
        {
            throw new FormatException($"Invalid lexicon entry '{entry.Form}' ({entry.Lemma}/{entry.Pos}): {message}.");
        }
    }
}
