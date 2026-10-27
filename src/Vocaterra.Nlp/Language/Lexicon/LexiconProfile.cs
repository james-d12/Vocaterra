namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// The closed vocabularies of one language's lexicon files: which values a feature may take, and which of the optional features
/// (<c>case</c>, <c>verbForm</c>, <c>declension</c>, <c>particle</c>, <c>register</c>) the language uses at all. The lexicon validates every entry
/// against the profile of the bundle's language pack (<see cref="Packs.ILanguagePack.LexiconProfile"/>). A feature with an empty list is not allowed:
/// an entry that sets it is refused. M34; additive.
/// </summary>
/// <remarks>
/// The parts of speech are shared by every language (they are the engine's word classes); a pack may list fewer, never other ones. The profile of
/// <see cref="V1"/> is the schema v1 of the Spanish lexicon: <c>sg</c>/<c>pl</c>, <c>m</c>/<c>f</c>, persons 1 to 3, five tenses, three moods, and none of the
/// optional features. A Spanish lexicon file with gender <c>n</c> is still refused.
/// </remarks>
public sealed record LexiconProfile
{
    /// <summary>Creates a profile; every list not given is empty (the feature is not allowed).</summary>
    public LexiconProfile(
        IReadOnlyList<string> genders,
        IReadOnlyList<string> numbers,
        IReadOnlyList<string> persons,
        IReadOnlyList<string> tenses,
        IReadOnlyList<string> moods,
        IReadOnlyList<string>? cases = null,
        IReadOnlyList<string>? verbForms = null,
        IReadOnlyList<string>? declensions = null,
        IReadOnlyList<string>? particles = null,
        IReadOnlyList<string>? registers = null)
    {
        ArgumentNullException.ThrowIfNull(genders);
        ArgumentNullException.ThrowIfNull(numbers);
        ArgumentNullException.ThrowIfNull(persons);
        ArgumentNullException.ThrowIfNull(tenses);
        ArgumentNullException.ThrowIfNull(moods);
        Genders = genders;
        Numbers = numbers;
        Persons = persons;
        Tenses = tenses;
        Moods = moods;
        Cases = cases ?? [];
        VerbForms = verbForms ?? [];
        Declensions = declensions ?? [];
        Particles = particles ?? [];
        Registers = registers ?? [];
    }

    /// <summary>The schema v1 profile: the closed vocabularies of <see cref="LexiconSchema"/>, which are the Spanish ones, and none of the optional features.</summary>
    public static LexiconProfile V1 { get; } = new(
        LexiconSchema.Genders,
        LexiconSchema.Numbers,
        LexiconSchema.Persons,
        LexiconSchema.Tenses,
        LexiconSchema.Moods);

    /// <summary>The allowed genders.</summary>
    public IReadOnlyList<string> Genders { get; }

    /// <summary>The allowed numbers.</summary>
    public IReadOnlyList<string> Numbers { get; }

    /// <summary>The allowed persons.</summary>
    public IReadOnlyList<string> Persons { get; }

    /// <summary>The allowed tenses.</summary>
    public IReadOnlyList<string> Tenses { get; }

    /// <summary>The allowed moods.</summary>
    public IReadOnlyList<string> Moods { get; }

    /// <summary>The allowed cases; empty when the language has none.</summary>
    public IReadOnlyList<string> Cases { get; }

    /// <summary>The allowed verb forms; empty when the language does not use the feature.</summary>
    public IReadOnlyList<string> VerbForms { get; }

    /// <summary>The allowed declension classes; empty when the language does not use the feature.</summary>
    public IReadOnlyList<string> Declensions { get; }

    /// <summary>The allowed particle classes; empty when the language does not use the feature.</summary>
    public IReadOnlyList<string> Particles { get; }

    /// <summary>The allowed registers; empty when the language does not use the feature.</summary>
    public IReadOnlyList<string> Registers { get; }

    /// <summary>Throws <see cref="FormatException"/> when the entry violates the schema or this profile.</summary>
    /// <param name="entry">The entry.</param>
    public void Validate(LexiconEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Require(!string.IsNullOrWhiteSpace(entry.Form), entry, "form must not be empty");
        Require(!string.IsNullOrWhiteSpace(entry.Lemma), entry, "lemma must not be empty");
        Require(Contains(LexiconSchema.PartsOfSpeech, entry.Pos), entry, $"unknown pos '{entry.Pos}'");

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
        RequireValue(entry, "case", entry.Features.Case, Cases);
        RequireValue(entry, "verbForm", entry.Features.VerbForm, VerbForms);
        RequireValue(entry, "declension", entry.Features.Declension, Declensions);
        RequireValue(entry, "particle", entry.Features.Particle, Particles);
        RequireValue(entry, "register", entry.Features.Register, Registers);
    }

    private static void RequireValue(LexiconEntry entry, string name, string? value, IReadOnlyList<string> allowed)
    {
        Require(value is null || Contains(allowed, value), entry, allowed.Count == 0 ? $"feature '{name}' is not used by this language" : $"unknown {name} '{value}'");
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
