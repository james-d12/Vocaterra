using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>Optional morphological features of one lexicon entry; absent features are null.</summary>
public sealed record EntryFeatures(string? Person, string? Number, string? Tense, string? Mood, string? Gender)
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
        return new LexicalFeatures(Lemma, Pos, Features.Person, Features.Number, Features.Tense, Features.Mood, Features.Gender);
    }
}

/// <summary>The lexicon JSON document: schema version, provenance and the entries.</summary>
public sealed record LexiconFile(
    int SchemaVersion,
    [property: JsonPropertyName("generated_by")] string? GeneratedBy,
    IReadOnlyList<LexiconEntry> Entries);
