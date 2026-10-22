using System.Text.Json;
using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Content;

/// <summary>The allowed values of a provenance item's <c>generatedBy</c>.</summary>
public static class GeneratedByKinds
{
    /// <summary>Written by a person.</summary>
    public const string HandAuthored = "hand-authored";

    /// <summary>Drafted by a language model; it must be approved by a person before it can ship.</summary>
    public const string LlmDraft = "llm-draft";

    /// <summary>Imported from Wiktionary data (CC BY-SA, see the attribution notice).</summary>
    public const string WiktionaryImport = "wiktionary-import";

    /// <summary>
    /// Produced by a model from text a person has already reviewed, such as pre-rendered speech. Only host-owned files may carry it (never the
    /// language data the loader parses), the item must name its <c>model</c>, and it needs no approval in the bundle: reviewing the output is the
    /// host's process.
    /// </summary>
    public const string ModelOutput = "model-output";

    /// <summary>All kinds, in declaration order.</summary>
    public static IReadOnlyList<string> All { get; } = [HandAuthored, LlmDraft, WiktionaryImport, ModelOutput];
}

/// <summary>Where one bundle file came from and who approved it.</summary>
/// <param name="Path">Bundle-relative path of the file.</param>
/// <param name="GeneratedBy">One of <see cref="GeneratedByKinds"/>.</param>
/// <param name="ApprovedBy">Name of the reviewer, or null when nobody has approved the file.</param>
/// <param name="ApprovedOn">Approval date as <c>YYYY-MM-DD</c>, or null; required exactly when <paramref name="ApprovedBy"/> is set.</param>
/// <param name="Model">The model id a <see cref="GeneratedByKinds.ModelOutput"/> file was produced with: required for that kind, refused for the others.</param>
/// <param name="Voice">Optional voice name the output was rendered in: allowed only for <see cref="GeneratedByKinds.ModelOutput"/>.</param>
/// <param name="Speaker">Optional speaker id within the model, as text: allowed only for <see cref="GeneratedByKinds.ModelOutput"/>.</param>
/// <param name="Source">Optional text or line id the output renders: allowed only for <see cref="GeneratedByKinds.ModelOutput"/>.</param>
public sealed record ProvenanceItem(
    string Path,
    string GeneratedBy,
    string? ApprovedBy,
    string? ApprovedOn,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Model = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Voice = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Speaker = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Source = null);

/// <summary>The provenance file: one item per bundle file.</summary>
public sealed record ContentProvenance(IReadOnlyList<ProvenanceItem> Items)
{
    /// <summary>Parses provenance JSON; throws <see cref="JsonException"/> when it is malformed.</summary>
    public static ContentProvenance FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var provenance = CanonicalJson.Deserialize<ContentProvenance>(json);
        if ((object?)provenance.Items is null)
        {
            throw new JsonException("The provenance file needs an items array.");
        }

        foreach (var item in provenance.Items)
        {
            if ((object?)item is null || (object?)item.Path is null || (object?)item.GeneratedBy is null)
            {
                throw new JsonException("Every provenance item needs a path and generatedBy.");
            }
        }

        return provenance;
    }

    /// <summary>Serializes as canonical JSON.</summary>
    public string ToJson()
    {
        return CanonicalJson.Serialize(this);
    }

    /// <summary>Structural equality including the item list.</summary>
    public bool Equals(ContentProvenance? other)
    {
        return other is not null && Items.SequenceEqual(other.Items);
    }

    /// <summary>Hash consistent with <see cref="Equals(ContentProvenance)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
