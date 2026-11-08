using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Evaluation;

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
    /// <item><description><b>Communicated</b> is true when either (a) the intent is known, every required slot is filled with a non-empty value and the overall confidence is not rejected by the communicated margin,
    /// or (b) the answer set matched (<see cref="MatchType.Exact"/>, <see cref="MatchType.Alias"/> or <see cref="MatchType.Fuzzy"/>) and the match confidence is not rejected by the same margin.
    /// Rule (b) is the answer-match rule: a prompt that only asks for a word (a quiz) has no intent, yet a matched answer is a resolution in its own right (<see cref="ResolvedBy.Exact"/>, <see cref="ResolvedBy.Alias"/>
    /// or <see cref="ResolvedBy.Fuzzy"/>), and it leaves <see cref="Verdict.Intent"/> as the intent stage reported it (usually <see cref="IntentId.Unknown"/>).
    /// An <see cref="MarginDecision.Uncertain"/> confidence counts as communicated in both rules: the host rewards communication.</description></item>
    /// <item><description><b>Errors</b> merge analysis, pronunciation and the derived <c>vocab.wrong</c> error, deduplicated by (tag, span) and sorted by span start, tag order, then expected text.</description></item>
    /// <item><description><b>Correctness</b> is 1 minus the sum of the difficulty's error weights in sorted error order, clamped to [0, 1] and rounded to 9 decimals.</description></item>
    /// <item><description><b>OffPromptIntent</b> (M72) is the intent stage's <see cref="IntentResolution.OffPromptIntent"/> when the intent is unknown and the answer set did not match, and null otherwise.</description></item>
    /// <item><description><b>Confidence</b> is the minimum of the stage confidences (match confidence only counts when something matched, and an unresolved intent only counts when nothing matched); NaN counts as 0 and values are clamped to [0, 1].</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// The derived <c>vocab.wrong</c> error is raised when the answer-set match is <see cref="MatchType.None"/>, the prompt is
    /// <see cref="RecognitionMode.Constrained"/> and the line is a miss the tag can explain by naming a word (M67, M72, M75). Two misses can be explained:
    /// <list type="number">
    /// <item><description><b>A near miss of an answer</b> (M75; the owner, 2026-11-02, D-game-22: "a near miss must name its word"): the line resolved no intent, or an
    /// intent that no slot grammar gave (a keyword rule or the embedding rung), and <see cref="VerdictInputs.NearMiss"/> holds the one word by which it misses the
    /// nearest answer phrase: the two have the same number of words, exactly one differs, and the two differing words are a slip pair, close in spelling or both of
    /// one closed class (question words, articles, prepositions, pronouns; <c>AnswerNearMisses</c>). The tag is on that word, a word the lexicon knows
    /// (<see cref="VerdictInputs.KnownLexiconWords"/>; an unknown word has its <c>vocab.unknown</c>), and its <c>Expected</c> is the answer's word, so it is never
    /// empty: <c>cuál es esto</c> for <c>qué es esto</c> at <c>lesson-what</c> is <c>cuál</c>, expected <c>qué</c>. A line that adds or drops a word
    /// (<c>quiero hacer un hacha</c> at <c>smith-open</c>) or changes a content word for another far from it in spelling (<c>pasado mañana iré al río con
    /// Mar</c>, <c>rápido por favor</c>) is no near miss: M67's rule, a distance to the nearest answer within the fuzzy budget for the line's own length, tagged
    /// them, and scored correct Spanish 0.4 with an empty expected word while the agent praised it. A set with no answers has no phrase to miss, so no near miss
    /// (the PR #28 review's item 8: M67's rule read the matcher's distance 0 there as a near miss of every unresolved line).</description></item>
    /// <item><description><b>A line that resolved no intent</b> (<see cref="IntentId.IsUnknown"/>) at a prompt that asks for one word (an answer set of exactly one answer,
    /// or a quiz, which allows no intent; M72): the tag is on the first content token (the first token that is not purely an article, pronoun, preposition or
    /// conjunction; unknown words count as content) when that is a word the lexicon knows. Its <c>Expected</c> is the set's canonical answer when the set has exactly
    /// one answer, and empty at a quiz of several answers, which has no single word to name (M17c).</description></item>
    /// </list>
    /// The near miss is tried first. A line a slot grammar understood never gets the tag, nor does a correct line that is simply not among a prompt's several answers
    /// (M72: <c>la tienda está en el pueblo</c> at <c>farmer-open</c>, or <c>buscaré madera para luz</c> at <c>smith-open</c>, scored 0.4 before M67): it names no word and
    /// explains no miss, so it has no error and, with no intent, is not communicated. <c>Actual</c> is the token text. An error on a number reports the learner's word
    /// for it (M70: <c>dos</c>, not the <c>2</c> of the normalized text), whichever stage reported it.
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
            inputs.Versions,
            OffPromptIntentOf(inputs));
    }

    // M72: the intent the catalog gives a line this prompt did not take. An answer match is a line the prompt took, so the member is set only when the intent is
    // unknown and nothing matched, which is also when the line is not communicated.
    private static IntentId? OffPromptIntentOf(VerdictInputs inputs)
    {
        return inputs.Intent.Intent.IsUnknown && inputs.Match.Type == MatchType.None ? inputs.Intent.OffPromptIntent : null;
    }

    private static bool IsCommunicated(VerdictInputs inputs, SlotMap slots, double confidence, DifficultyEvaluationConfig settings)
    {
        var margin = settings.CommunicatedMargin();
        return IntentCommunicated(inputs, slots, confidence, margin) || AnswerMatchCommunicated(inputs, margin);
    }

    // Rule (a): the intent stage understood the player and every slot it needs is filled.
    private static bool IntentCommunicated(VerdictInputs inputs, SlotMap slots, double confidence, Margin margin)
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

        return margin.Evaluate(confidence) != MarginDecision.Reject;
    }

    // Rule (b): the transcript matched an answer of the prompt's answer set, which needs no intent.
    private static bool AnswerMatchCommunicated(VerdictInputs inputs, Margin margin)
    {
        return inputs.Match.Type != MatchType.None
            && margin.Evaluate(SanitizeConfidence(inputs.Match.Confidence)) != MarginDecision.Reject;
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

        var sorted = unique.Values.Select(error => WithLearnersWord(error, inputs.Analysis.Tokens)).ToArray();
        var tags = inputs.ErrorTags ?? ErrorTagSet.V1;
        Array.Sort(sorted, (left, right) => CompareErrors(left, right, tags));
        return sorted;
    }

    // M70: an error on a number reports the learner's own word, not the digits the normalizer wrote for it (dos panes es caro: heard dos, not 2). The span and the
    // expected text are unchanged; a number the learner wrote in digits keeps them.
    private static ErrorTag WithLearnersWord(ErrorTag error, IReadOnlyList<AnalyzedToken> tokens)
    {
        foreach (var analyzed in tokens)
        {
            var token = analyzed.Token;
            if (token.Span == error.Span
                && string.Equals(error.Actual, token.Text, StringComparison.Ordinal)
                && IsDigits(token.Text)
                && !IsDigits(token.Original))
            {
                return error with { Actual = token.Original.ToLowerInvariant() };
            }
        }

        return error;
    }

    private static bool IsDigits(string text)
    {
        return text.Length > 0 && text.All(char.IsAsciiDigit);
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

    private static int CompareErrors(ErrorTag left, ErrorTag right, ErrorTagSet tags)
    {
        var byStart = left.Span.Start.CompareTo(right.Span.Start);
        if (byStart != 0)
        {
            return byStart;
        }

        var byRank = tags.Rank(left.Tag).CompareTo(tags.Rank(right.Tag));
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

    private static ErrorTag? DeriveVocabWrong(VerdictInputs inputs)
    {
        if (inputs.Match.Type != MatchType.None || inputs.Prompt.Mode != RecognitionMode.Constrained)
        {
            return null;
        }

        // M75: a near miss names its word, the line's word that is one slip from the nearest answer phrase's, with the answer's word as the expected text.
        var unknown = inputs.Intent.Intent.IsUnknown;
        if ((unknown || inputs.Intent.Rung != IntentRung.Grammar)
            && inputs.NearMiss is { } nearMiss
            && ContainsOrdinal(inputs.KnownLexiconWords, nearMiss.Actual))
        {
            return new ErrorTag(ErrorTags.VocabWrong, nearMiss.Span, nearMiss.Expected, nearMiss.Actual);
        }

        if (!unknown || !NamesTheWord(inputs))
        {
            return null;
        }

        var token = FirstContentToken(inputs.Analysis.Tokens);
        if (token is null || !ContainsOrdinal(inputs.KnownLexiconWords, token.Text))
        {
            return null;
        }

        var expected = inputs.CanonicalAnswers is { Count: 1 } answers ? answers[0] : string.Empty;
        return new ErrorTag(ErrorTags.VocabWrong, token.Span, expected, token.Text);
    }

    // A prompt names the word a line missed when its answer set has exactly one answer, or when it is a quiz (it allows no intent, so it asks only for an answer). A
    // correct line that is simply not among a prompt's several answers (la tienda está en el pueblo at farmer-open) has no word to name: it gets no error (M72).
    private static bool NamesTheWord(VerdictInputs inputs)
    {
        return inputs.CanonicalAnswers is { Count: 1 } || inputs.Prompt.AllowedIntentIds.Count == 0;
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

        // An unresolved intent has confidence 0, but when an answer matched there was no intent to find (a quiz prompt), so it says nothing about the verdict.
        if (!inputs.Intent.Intent.IsUnknown || inputs.Match.Type == MatchType.None)
        {
            confidence = Math.Min(confidence, SanitizeConfidence(inputs.Intent.Confidence));
        }

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
