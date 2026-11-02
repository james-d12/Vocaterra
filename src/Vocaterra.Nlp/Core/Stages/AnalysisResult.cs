using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Core.Stages;

/// <summary>Morphological reading of one word form; optional features are null when not applicable.</summary>
/// <param name="Lemma">The dictionary form.</param>
/// <param name="Pos">The part of speech.</param>
/// <param name="Person">The grammatical person (<c>1</c>, <c>2</c>, <c>3</c>) or null.</param>
/// <param name="Number">The number or null.</param>
/// <param name="Tense">The tense or null.</param>
/// <param name="Mood">The mood or null.</param>
/// <param name="Gender">The gender or null.</param>
/// <param name="Case">The grammatical case of a language that has cases (M34, additive), as the pack's profile allows; null for Spanish.</param>
/// <param name="VerbForm">The verb form class of a language that needs one (M34, additive); null for Spanish.</param>
/// <param name="Declension">The declension or inflection class (M34, additive); null for Spanish.</param>
/// <param name="Particle">The particle class of a word, such as a separable verb prefix (M34, additive); null for Spanish.</param>
/// <param name="Register">The register of the form, for example <c>formal</c> (M34, additive); null for Spanish.</param>
/// <remarks>
/// The five trailing members are new in M34 and null for every Spanish reading. They are omitted from JSON while null, so a Spanish reading
/// serializes exactly as before, and record equality is unchanged while they are null.
/// </remarks>
public sealed record LexicalFeatures(
    string Lemma,
    string Pos,
    string? Person,
    string? Number,
    string? Tense,
    string? Mood,
    string? Gender,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Case = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? VerbForm = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Declension = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Particle = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Register = null);

/// <summary>A token with its candidate readings; an empty reading list means the word is unknown.</summary>
public sealed record AnalyzedToken(Token Token, IReadOnlyList<LexicalFeatures> Readings)
{
    /// <summary>Structural equality including reading list contents.</summary>
    public bool Equals(AnalyzedToken? other)
    {
        return other is not null
            && EqualityComparer<Token>.Default.Equals(Token, other.Token)
            && ValueEquality.ListEquals(Readings, other.Readings);
    }

    /// <summary>Hash consistent with <see cref="Equals(AnalyzedToken)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Token);
        ValueEquality.AddList(ref hash, Readings);
        return hash.ToHashCode();
    }
}

/// <summary>A named slot value extracted from the text, with its span.</summary>
public sealed record SlotValue(string Name, string Value, TextSpan Span);

/// <summary>
/// Output of linguistic analysis: readings, extracted slots, grammar errors, the matched grammar if any, and the resolved words (one per token, in token
/// order, with the reading the resolver chose).
/// </summary>
/// <param name="Tokens">Every token with every lexicon reading (the intent stage matches lemmas on these).</param>
/// <param name="Slots">The slot values of the best matching grammar.</param>
/// <param name="Errors">The grammar and vocabulary errors, ordered by span.</param>
/// <param name="MatchedGrammarId">The id of the best matching grammar, or null.</param>
/// <param name="Confidence">The analysis confidence.</param>
/// <param name="Words">The resolved words, one per token in token order; null when the analysis was built by hand without the resolver.</param>
/// <param name="Notes">
/// The verbs written without the accent that marks their tense or person and read as the accented form, in token order (M70, <see cref="AccentNote"/>); null when
/// there is none. Omitted from JSON while null, so an analysis without a note serializes as before.
/// </param>
public sealed record AnalysisResult(
    IReadOnlyList<AnalyzedToken> Tokens,
    IReadOnlyList<SlotValue> Slots,
    IReadOnlyList<ErrorTag> Errors,
    string? MatchedGrammarId,
    double Confidence,
    IReadOnlyList<ResolvedWord>? Words = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AccentNote>? Notes = null)
{
    /// <summary>Structural equality including list contents.</summary>
    public bool Equals(AnalysisResult? other)
    {
        return other is not null
            && ValueEquality.ListEquals(Tokens, other.Tokens)
            && ValueEquality.ListEquals(Slots, other.Slots)
            && ValueEquality.ListEquals(Errors, other.Errors)
            && string.Equals(MatchedGrammarId, other.MatchedGrammarId, StringComparison.Ordinal)
            && Confidence.Equals(other.Confidence)
            && ValueEquality.ListEquals(Words ?? Array.Empty<ResolvedWord>(), other.Words ?? Array.Empty<ResolvedWord>())
            && ValueEquality.ListEquals(Notes ?? Array.Empty<AccentNote>(), other.Notes ?? Array.Empty<AccentNote>());
    }

    /// <summary>Hash consistent with <see cref="Equals(AnalysisResult)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        ValueEquality.AddList(ref hash, Tokens);
        ValueEquality.AddList(ref hash, Slots);
        ValueEquality.AddList(ref hash, Errors);
        hash.Add(MatchedGrammarId);
        hash.Add(Confidence);
        ValueEquality.AddList(ref hash, Words ?? Array.Empty<ResolvedWord>());
        ValueEquality.AddList(ref hash, Notes ?? Array.Empty<AccentNote>());
        return hash.ToHashCode();
    }
}
