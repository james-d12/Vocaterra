using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>Optional morphological features of one lexicon entry; absent features are null.</summary>
/// <param name="Person">The person or null.</param>
/// <param name="Number">The number or null.</param>
/// <param name="Tense">The tense or null.</param>
/// <param name="Mood">The mood or null.</param>
/// <param name="Gender">The gender or null.</param>
/// <param name="Case">The grammatical case, only where the language's <see cref="LexiconProfile"/> lists cases (M34, additive); null by default.</param>
/// <param name="VerbForm">The verb form class, only where the profile lists them (M34, additive); null by default.</param>
/// <param name="Declension">The declension class, only where the profile lists them (M34, additive); null by default.</param>
/// <param name="Particle">The particle class, only where the profile lists them (M34, additive); null by default.</param>
/// <param name="Register">The register, only where the profile lists them (M34, additive); null by default.</param>
public sealed record EntryFeatures(
    string? Person,
    string? Number,
    string? Tense,
    string? Mood,
    string? Gender,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Case = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? VerbForm = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Declension = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Particle = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Register = null)
{
    /// <summary>The feature set with no features at all (for example an infinitive or a preposition).</summary>
    public static EntryFeatures None { get; } = new(null, null, null, null, null);
}

/// <summary>One lexicon row: a surface form, its lemma, part of speech and features.</summary>
public sealed record LexiconEntry(string Form, string Lemma, string Pos, EntryFeatures Features)
{
    /// <summary>Converts this row to the cross-stage <see cref="LexicalFeatures"/> reading.</summary>
    public LexicalFeatures ToFeatures()
    {
        return new LexicalFeatures(
            Lemma,
            Pos,
            Features.Person,
            Features.Number,
            Features.Tense,
            Features.Mood,
            Features.Gender,
            Features.Case,
            Features.VerbForm,
            Features.Declension,
            Features.Particle,
            Features.Register);
    }
}

/// <summary>The lexicon JSON document: schema version, provenance and the entries.</summary>
public sealed record LexiconFile(
    int SchemaVersion,
    [property: JsonPropertyName("generated_by")] string? GeneratedBy,
    IReadOnlyList<LexiconEntry> Entries);
