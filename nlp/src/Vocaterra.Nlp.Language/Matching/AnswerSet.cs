using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Nlp.Language.Matching;

/// <summary>A prompt's set of acceptable answers; the unit that <see cref="AnswerMatcher"/> matches against.</summary>
/// <param name="AnswerSetId">Stable identifier referenced from prompts.</param>
/// <param name="Answers">The answers, in authoring order (matching does not depend on the order).</param>
public sealed record AnswerSet(string AnswerSetId, IReadOnlyList<Answer> Answers)
{
    private static readonly NormalizationOptions GrammarOptions = new(ExpandContractions: false, NumbersToDigits: false);

    /// <summary>Structural equality including the answers.</summary>
    public bool Equals(AnswerSet? other)
    {
        return other is not null
            && string.Equals(AnswerSetId, other.AnswerSetId, StringComparison.Ordinal)
            && ListEquality.Equal(Answers, other.Answers);
    }

    /// <summary>Hash consistent with <see cref="Equals(AnswerSet)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(AnswerSetId);
        ListEquality.Add(ref hash, Answers);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Parses canonical JSON (<c>answerSetId</c>, <c>answers[]</c> with <c>answerId</c>, <c>canonical</c> and optional <c>aliases</c>
    /// and <c>dialects</c>). Throws <see cref="JsonException"/> for malformed or inconsistent data.
    /// </summary>
    public static AnswerSet FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var dto = CanonicalJson.Deserialize<AnswerSetDto>(json);
        if (string.IsNullOrWhiteSpace(dto.AnswerSetId))
        {
            throw new JsonException("answerSetId must not be empty.");
        }

        // Missing members arrive as null even though the DTO is declared non-nullable, so check explicitly.
        if ((object?)dto.Answers is null)
        {
            throw new JsonException("answers is required.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var answers = new List<Answer>(dto.Answers.Count);
        foreach (var item in dto.Answers)
        {
            if ((object?)item is null)
            {
                throw new JsonException("answers must not contain null.");
            }

            if (string.IsNullOrWhiteSpace(item.AnswerId))
            {
                throw new JsonException("answerId must not be empty.");
            }

            if (!seen.Add(item.AnswerId))
            {
                throw new JsonException($"Duplicate answerId '{item.AnswerId}'.");
            }

            if (string.IsNullOrWhiteSpace(item.Canonical))
            {
                throw new JsonException($"Answer '{item.AnswerId}' has an empty canonical phrase.");
            }

            var aliases = (item.Aliases ?? []).ToArray();
            if (aliases.Any(string.IsNullOrWhiteSpace))
            {
                throw new JsonException($"Answer '{item.AnswerId}' has an empty alias.");
            }

            answers.Add(new Answer(item.AnswerId, item.Canonical, aliases, (item.Dialects ?? []).ToArray()));
        }

        return new AnswerSet(dto.AnswerSetId, answers);
    }

    /// <summary>Serializes the set as canonical JSON.</summary>
    public string ToJson()
    {
        return CanonicalJson.Serialize(this);
    }

    /// <summary>Reads a set from a UTF-8 JSON file.</summary>
    public static AnswerSet Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return FromJson(File.ReadAllText(path, Encoding.UTF8));
    }

    /// <summary>Writes the set to a UTF-8 file (no byte order mark) as canonical JSON.</summary>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
    }

    /// <summary>
    /// Returns every canonical and alias phrase, normalized, de-duplicated and ordinal-sorted, ready to be used as a constrained
    /// recognizer grammar. Numbers are written as words (a recognizer emits <c>dos</c>, not <c>2</c>) and contractions are left intact.
    /// </summary>
    public IReadOnlyList<string> GrammarPhrases()
    {
        var phrases = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var answer in Answers)
        {
            AddPhrase(phrases, answer.Canonical);
            foreach (var alias in answer.Aliases)
            {
                AddPhrase(phrases, alias);
            }
        }

        return phrases.ToArray();
    }

    private static void AddPhrase(SortedSet<string> phrases, string raw)
    {
        var text = SpanishNumbers.DigitsToWords(TextNormalizer.Normalize(raw, GrammarOptions).Text);
        if (text.Length > 0)
        {
            phrases.Add(text);
        }
    }

    private sealed record AnswerSetDto(string AnswerSetId, IReadOnlyList<AnswerDto> Answers);

    private sealed record AnswerDto(
        string AnswerId,
        string Canonical,
        IReadOnlyList<string>? Aliases,
        IReadOnlyList<string>? Dialects);
}
