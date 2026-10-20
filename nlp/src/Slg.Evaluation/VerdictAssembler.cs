using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Core.Taxonomy;
using Slg.Core.Verdicts;
using MatchType = Slg.Core.Stages.MatchType;

namespace Slg.Evaluation;

/// <summary>Combines the outputs of every earlier stage into one <see cref="Verdict"/>; a pure, deterministic function.</summary>
public static class VerdictAssembler
{
    private const int CorrectnessDecimals = 9;

    // Parts of speech that never count as "the answer word" when looking for a wrong-word error.
    private static readonly HashSet<string> FunctionPartsOfSpeech = new(StringComparer.OrdinalIgnoreCase)
    {
        "art",
        "article",
        "conj",
        "conjunction",
        "det",
        "determiner",
        "prep",
        "preposition",
        "pron",
        "pronoun",
    };

    /// <summary>
    /// Assembles the verdict.
    /// <list type="bullet">
    /// <item><description><b>Intent</b> comes from the intent stage; <b>Slots</b> are the analysis slots overlaid by the intent slots (the intent wins on a name clash).</description></item>
    /// <item><description><b>Communicated</b> needs a known intent, every required slot filled with a non-empty value, and a confidence that the communicated margin does not reject.
    /// An <see cref="MarginDecision.Uncertain"/> confidence counts as communicated: the game rewards communication.</description></item>
    /// <item><description><b>Errors</b> merge analysis, pronunciation and the derived <c>vocab.wrong</c> error, deduplicated by (tag, span) and sorted by span start, tag order, then expected text.</description></item>
    /// <item><description><b>Correctness</b> is 1 minus the sum of the difficulty's error weights in sorted error order, clamped to [0, 1] and rounded to 9 decimals.</description></item>
    /// <item><description><b>Confidence</b> is the minimum of the stage confidences (match confidence only counts when something matched); NaN counts as 0 and values are clamped to [0, 1].</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// The derived <c>vocab.wrong</c> error is raised when the answer-set match is <see cref="MatchType.None"/>, the prompt is
    /// <see cref="RecognitionMode.Constrained"/> and the first content token (the first token that is not purely an article,
    /// pronoun, preposition or conjunction; unknown words count as content) is in <see cref="VerdictInputs.KnownLexiconWords"/>.
    /// The canonical answer is not available to this stage, so <c>Expected</c> carries <see cref="PromptSpec.AnswerSetId"/> and
    /// <c>Actual</c> carries the token text; later stages that know the answer set may replace the expected text.
    /// </remarks>
    public static Verdict Assemble(VerdictInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var settings = inputs.Config.For(inputs.Prompt.Difficulty);

        var slots = MergeSlots(inputs.Analysis.Slots, inputs.Intent.Slots);
        var errors = CollectErrors(inputs);
        var confidence = MinimumConfidence(inputs);
        var communicated = IsCommunicated(inputs, slots, confidence, settings);

        return new Verdict(
            inputs.Intent.Intent,
            slots,
            communicated,
            ComputeCorrectness(errors, settings),
            errors,
            inputs.Pronunciation?.Phones.ToArray() ?? [],
            confidence,
            ResolveBy(inputs),
            inputs.Versions);
    }

    private static bool IsCommunicated(VerdictInputs inputs, SlotMap slots, double confidence, DifficultyEvaluationConfig settings)
    {
        if (inputs.Intent.Intent.IsUnknown)
        {
            return false;
        }

        foreach (var required in inputs.RequiredSlots)
        {
            if (!slots.TryGetValue(required, out var value) || string.IsNullOrEmpty(value))
            {
                return false;
            }
        }

        return settings.CommunicatedMargin().Evaluate(confidence) != MarginDecision.Reject;
    }

    private static SlotMap MergeSlots(IReadOnlyList<SlotValue> analysisSlots, SlotMap intentSlots)
    {
        // Duplicate analysis slot names resolve to the earliest span so input order cannot matter.
        var ordered = analysisSlots.ToArray();
        Array.Sort(ordered, CompareSlotValues);

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slot in ordered)
        {
            merged.TryAdd(slot.Name, slot.Value);
        }

        foreach (var pair in intentSlots)
        {
            merged[pair.Key] = pair.Value;
        }

        return SlotMap.From(merged);
    }

    private static int CompareSlotValues(SlotValue left, SlotValue right)
    {
        var byName = string.CompareOrdinal(left.Name, right.Name);
        if (byName != 0)
        {
            return byName;
        }

        var byStart = left.Span.Start.CompareTo(right.Span.Start);
        if (byStart != 0)
        {
            return byStart;
        }

        var byLength = left.Span.Length.CompareTo(right.Span.Length);
        return byLength != 0 ? byLength : string.CompareOrdinal(left.Value, right.Value);
    }

    private static ErrorTag[] CollectErrors(VerdictInputs inputs)
    {
        var unique = new Dictionary<(string Tag, int Start, int Length), ErrorTag>();
        foreach (var error in inputs.Analysis.Errors)
        {
            AddUnique(unique, error);
        }

        if (inputs.Pronunciation is not null)
        {
            foreach (var error in inputs.Pronunciation.Errors)
            {
                AddUnique(unique, error);
            }
        }

        // The derived error is a fallback: a stage-reported error with the same tag and span is more informative and wins.
        var derived = DeriveVocabWrong(inputs);
        if (derived is not null)
        {
            unique.TryAdd((derived.Tag, derived.Span.Start, derived.Span.Length), derived);
        }

        var sorted = unique.Values.ToArray();
        Array.Sort(sorted, CompareErrors);
        return sorted;
    }

    private static void AddUnique(Dictionary<(string Tag, int Start, int Length), ErrorTag> unique, ErrorTag error)
    {
        var key = (error.Tag, error.Span.Start, error.Span.Length);
        if (!unique.TryGetValue(key, out var existing) || CompareDuplicates(error, existing) < 0)
        {
            unique[key] = error;
        }
    }

    // Among errors sharing (tag, span) the smallest (expected, actual) wins so the choice is independent of input order.
    private static int CompareDuplicates(ErrorTag left, ErrorTag right)
    {
        var byExpected = string.CompareOrdinal(left.Expected, right.Expected);
        return byExpected != 0 ? byExpected : string.CompareOrdinal(left.Actual, right.Actual);
    }

    private static int CompareErrors(ErrorTag left, ErrorTag right)
    {
        var byStart = left.Span.Start.CompareTo(right.Span.Start);
        if (byStart != 0)
        {
            return byStart;
        }

        var byRank = TagRank(left.Tag).CompareTo(TagRank(right.Tag));
        if (byRank != 0)
        {
            return byRank;
        }

        var byExpected = string.CompareOrdinal(left.Expected, right.Expected);
        if (byExpected != 0)
        {
            return byExpected;
        }

        // Remaining keys only break ties between distinct (tag, span) pairs, which makes the order total.
        var byLength = left.Span.Length.CompareTo(right.Span.Length);
        if (byLength != 0)
        {
            return byLength;
        }

        var byTag = string.CompareOrdinal(left.Tag, right.Tag);
        return byTag != 0 ? byTag : string.CompareOrdinal(left.Actual, right.Actual);
    }

    // Known tags rank by their position in ErrorTags.All; unknown tags sort after all known ones.
    private static int TagRank(string tag)
    {
        var all = ErrorTags.All;
        for (var i = 0; i < all.Count; i++)
        {
            if (string.Equals(all[i], tag, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return all.Count;
    }

    private static ErrorTag? DeriveVocabWrong(VerdictInputs inputs)
    {
        if (inputs.Match.Type != MatchType.None || inputs.Prompt.Mode != RecognitionMode.Constrained)
        {
            return null;
        }

        var token = FirstContentToken(inputs.Analysis.Tokens);
        if (token is null || !ContainsOrdinal(inputs.KnownLexiconWords, token.Text))
        {
            return null;
        }

        return new ErrorTag(ErrorTags.VocabWrong, token.Span, inputs.Prompt.AnswerSetId, token.Text);
    }

    private static Token? FirstContentToken(IReadOnlyList<AnalyzedToken> tokens)
    {
        Token? best = null;
        foreach (var analyzed in tokens)
        {
            if (IsFunctionWord(analyzed) || (best is not null && CompareTokens(analyzed.Token, best) >= 0))
            {
                continue;
            }

            best = analyzed.Token;
        }

        return best;
    }

    private static bool IsFunctionWord(AnalyzedToken analyzed)
    {
        if (analyzed.Readings.Count == 0)
        {
            return false;
        }

        foreach (var reading in analyzed.Readings)
        {
            if (!FunctionPartsOfSpeech.Contains(reading.Pos))
            {
                return false;
            }
        }

        return true;
    }

    private static int CompareTokens(Token left, Token right)
    {
        var byStart = left.Span.Start.CompareTo(right.Span.Start);
        if (byStart != 0)
        {
            return byStart;
        }

        var byLength = left.Span.Length.CompareTo(right.Span.Length);
        return byLength != 0 ? byLength : string.CompareOrdinal(left.Text, right.Text);
    }

    private static bool ContainsOrdinal(IReadOnlyList<string> words, string text)
    {
        foreach (var word in words)
        {
            if (string.Equals(word, text, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static double ComputeCorrectness(IReadOnlyList<ErrorTag> sortedErrors, DifficultyEvaluationConfig settings)
    {
        var penalty = 0.0;
        foreach (var error in sortedErrors)
        {
            penalty += settings.WeightFor(error.Tag);
        }

        var correctness = Math.Clamp(1.0 - penalty, 0.0, 1.0);
        return Math.Round(correctness, CorrectnessDecimals, MidpointRounding.ToEven);
    }

    private static double MinimumConfidence(VerdictInputs inputs)
    {
        var confidence = SanitizeConfidence(inputs.Asr.Confidence);
        if (inputs.Match.Type != MatchType.None)
        {
            confidence = Math.Min(confidence, SanitizeConfidence(inputs.Match.Confidence));
        }

        confidence = Math.Min(confidence, SanitizeConfidence(inputs.Analysis.Confidence));
        confidence = Math.Min(confidence, SanitizeConfidence(inputs.Intent.Confidence));
        if (inputs.Pronunciation is not null)
        {
            confidence = Math.Min(confidence, SanitizeConfidence(inputs.Pronunciation.Confidence));
        }

        return confidence;
    }

    private static double SanitizeConfidence(double value)
    {
        return double.IsNaN(value) ? 0.0 : Math.Clamp(value, 0.0, 1.0);
    }

    private static ResolvedBy ResolveBy(VerdictInputs inputs)
    {
        switch (inputs.Match.Type)
        {
            case MatchType.Exact:
                return ResolvedBy.Exact;
            case MatchType.Alias:
                return ResolvedBy.Alias;
            case MatchType.Fuzzy:
                return ResolvedBy.Fuzzy;
        }

        if (inputs.Intent.Intent.IsUnknown)
        {
            return ResolvedBy.Unresolved;
        }

        return inputs.Intent.Rung switch
        {
            IntentRung.Grammar => ResolvedBy.Grammar,
            IntentRung.Rules => ResolvedBy.Rules,
            IntentRung.Embedding => ResolvedBy.Embedding,
            _ => ResolvedBy.Unresolved,
        };
    }
}
