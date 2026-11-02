using System.Collections.Immutable;
using CsCheck;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Evaluation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>
/// Random stage results for verdict assembly and serialization, within the contract ranges of plan section 4: confidences in [0, 1] (with
/// exact threshold values and values a hair either side of the epsilon band, and a few NaNs the assembler documents as 0), spans inside
/// the text, tags from <see cref="ErrorTags.All"/> plus a few unknown ones, and slot lists with duplicate names in shuffled order.
/// </summary>
public static class VerdictGen
{
    private static readonly string[] SlotNames = ["item", "qty", "spell", "who", "verb"];
    private static readonly string[] UnknownTags = ["custom.tag", "zzz", "grammar.new_rule", "pron.future"];

    /// <summary>
    /// Draws a complete set of verdict inputs; the seed decides everything, so a CsCheck seed replays it. NaN confidences, which the assembler
    /// documents as 0 but which canonical JSON refuses to write, are only produced when <paramref name="allowNaN"/> is true.
    /// </summary>
    public static Gen<VerdictInputs> Inputs(PipelineHarness harness, bool allowNaN = true)
    {
        return Gen.ULong.Select(seed => Build(harness, new SeededRng(seed), allowNaN));
    }

    /// <summary>Builds verdict inputs from a random stream.</summary>
    public static VerdictInputs Build(PipelineHarness harness, SeededRng rng, bool allowNaN = true)
    {
        var config = PickConfig(harness, rng);
        var prompt = PickPrompt(harness, rng);
        var settings = config.For(prompt.Difficulty);

        var normalized = PipelineHarness.Normalize(RandomText(harness, rng));
        var length = normalized.Text.Length;
        var realAnalysis = harness.Analyzer.Analyze(normalized, harness.Bundle.Intents.Grammars);
        var errors = rng.NextDouble() < 0.4 ? realAnalysis.Errors.ToList() : RandomErrors(rng, length, withUnknownTags: true);
        var analysis = new AnalysisResult(
            realAnalysis.Tokens,
            RandomSlotValues(rng, length),
            Shuffled(errors, rng),
            realAnalysis.MatchedGrammarId,
            Confidence(rng, settings, allowNaN));

        var asr = new AsrResult(normalized.Text, [], [], Confidence(rng, settings, allowNaN), "fuzz-asr", prompt.Mode);
        var match = RandomMatch(harness, rng, prompt, settings, allowNaN);

        IntentId intentId;
        if (rng.NextDouble() < 0.35)
        {
            intentId = IntentId.Unknown;
        }
        else
        {
            intentId = harness.Bundle.Intents.Intents[rng.NextInt(harness.Bundle.Intents.Intents.Count)].Id;
        }

        var intent = new IntentResolution(
            intentId,
            RandomSlotMap(rng),
            (IntentRung)rng.NextInt(4),
            rng.NextDouble(),
            Confidence(rng, settings, allowNaN));

        var pronunciation = rng.NextDouble() < 0.4 ? null : RandomPronunciation(rng, length, settings, allowNaN);
        var required = rng.NextDouble() < 0.7
            ? [.. harness.Bundle.Intents.RequiredSlots(intentId)]
            : SlotNames.Where(_ => rng.NextDouble() < 0.3).ToArray();
        var known = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var token in analysis.Tokens.Where(static t => t.Readings.Count > 0))
        {
            known.Add(token.Token.Text);
        }

        for (var i = rng.NextInt(4); i > 0; i--)
        {
            known.Add(rng.Pick(harness.Words.AllForms));
        }

        var knownWords = rng.NextDouble() < 0.2 ? Shuffled(known.ToList(), rng) : known.ToList();
        var versions = RandomVersions(harness, rng);

        // M72, drawn last so the draws above keep their places in the stream: the answer set's canonical answers (none, the set's own, or one answer, which names
        // the word of a derived vocab.wrong), one prompt in seven a quiz, which allows no intent, and the off-prompt intent below.
        var canonical = CanonicalAnswers(harness, rng, prompt);
        if (rng.NextInt(7) == 0)
        {
            prompt = prompt with { AllowedIntentIds = [] };
        }

        // The intent stage's off-prompt intent (M72), one time in three, whatever the intent: the assembler carries it only for an unknown intent and no match.
        if (rng.NextInt(3) == 0)
        {
            intent = intent with { OffPromptIntent = harness.Bundle.Intents.Intents[rng.NextInt(harness.Bundle.Intents.Intents.Count)].Id };
        }

        return new VerdictInputs(prompt, asr, match, analysis, intent, pronunciation, required, knownWords, config, versions, canonical);
    }

    private static IReadOnlyList<string>? CanonicalAnswers(PipelineHarness harness, SeededRng rng, PromptSpec prompt)
    {
        var answers = harness.Bundle.AnswerSets.TryGetValue(prompt.AnswerSetId, out var set) ? set.Answers.Select(static a => a.Canonical).ToArray() : [];
        return rng.NextInt(3) switch
        {
            0 => null,
            1 => answers,
            _ => [answers.Length > 0 ? rng.Pick(answers) : rng.Pick(harness.Words.AllForms)],
        };
    }

    /// <summary>
    /// Returns the same inputs with every unordered collection shuffled (analysis and pronunciation errors, analysis slots, required slots,
    /// known words); a correct assembler gives the identical verdict for both.
    /// </summary>
    public static VerdictInputs Permuted(VerdictInputs inputs, SeededRng rng)
    {
        var analysis = inputs.Analysis with
        {
            Errors = Shuffled(inputs.Analysis.Errors.ToList(), rng),
            Slots = Shuffled(inputs.Analysis.Slots.ToList(), rng),
        };
        var pronunciation = inputs.Pronunciation is null ? null : inputs.Pronunciation with { Errors = Shuffled(inputs.Pronunciation.Errors.ToList(), rng) };
        return inputs with
        {
            Analysis = analysis,
            Pronunciation = pronunciation,
            RequiredSlots = Shuffled(inputs.RequiredSlots.ToList(), rng),
            KnownLexiconWords = Shuffled(inputs.KnownLexiconWords.ToList(), rng),
        };
    }

    /// <summary>
    /// Draws a confidence for a stage: mostly uniform, often exactly on or a hair either side of the edges of the communicated margin
    /// (<c>t</c>, <c>t ± ε</c> and the doubles next to them), sometimes 0 or 1, rarely NaN.
    /// </summary>
    public static double Confidence(SeededRng rng, DifficultyEvaluationConfig settings, bool allowNaN = true)
    {
        var roll = rng.NextDouble();
        if (roll < 0.45)
        {
            return rng.NextDouble();
        }

        if (roll < 0.8)
        {
            var edge = rng.Pick(new[] { 0.0, settings.Epsilon, -settings.Epsilon });
            var value = settings.CommunicatedConfidenceThreshold + edge;
            value = rng.NextInt(3) switch
            {
                0 => value,
                1 => Math.BitIncrement(value),
                _ => Math.BitDecrement(value),
            };
            return Math.Clamp(value, 0.0, 1.0);
        }

        if (roll < 0.9)
        {
            return rng.NextInt(2) == 0 ? 0.0 : 1.0;
        }

        if (roll < 0.97)
        {
            return rng.NextInt(2) == 0 ? 1e-12 : 1.0 - 1e-12;
        }

        return allowNaN ? double.NaN : 0.5;
    }

    /// <summary>Draws error tags with spans inside a text of the given length, sometimes duplicating a tag and span with different texts.</summary>
    public static List<ErrorTag> RandomErrors(SeededRng rng, int textLength, bool withUnknownTags)
    {
        var errors = new List<ErrorTag>();
        if (textLength == 0)
        {
            return errors;
        }

        var count = rng.NextInt(7);
        for (var i = 0; i < count; i++)
        {
            var tag = withUnknownTags && rng.NextDouble() < 0.15 ? rng.Pick(UnknownTags) : rng.Pick(ErrorTags.All);
            var start = rng.NextInt(textLength);
            var span = new TextSpan(start, 1 + rng.NextInt(textLength - start));
            errors.Add(new ErrorTag(tag, span, RandomWord(rng), RandomWord(rng)));
            if (rng.NextDouble() < 0.25)
            {
                errors.Add(new ErrorTag(tag, span, RandomWord(rng), RandomWord(rng)));
            }
        }

        return errors;
    }

    /// <summary>A slot map with up to four slots.</summary>
    public static SlotMap RandomSlotMap(SeededRng rng)
    {
        var map = SlotMap.Empty;
        for (var i = rng.NextInt(5); i > 0; i--)
        {
            map = map.With(rng.Pick(SlotNames), rng.NextInt(6) == 0 ? string.Empty : RandomWord(rng));
        }

        return map;
    }

    private static string RandomWord(SeededRng rng)
    {
        return rng.Pick(PipelineHarness.Shared.Words.AllForms);
    }

    private static List<T> Shuffled<T>(List<T> list, SeededRng rng)
    {
        rng.Shuffle(list);
        return list;
    }

    private static string RandomText(PipelineHarness harness, SeededRng rng)
    {
        var count = rng.NextInt(7);
        return string.Join(' ', Enumerable.Range(0, count).Select(_ => rng.Pick(harness.Words.AllForms)));
    }

    private static List<SlotValue> RandomSlotValues(SeededRng rng, int textLength)
    {
        var slots = new List<SlotValue>();
        if (textLength == 0)
        {
            return slots;
        }

        for (var i = rng.NextInt(6); i > 0; i--)
        {
            var start = rng.NextInt(textLength);
            slots.Add(new SlotValue(rng.Pick(SlotNames), RandomWord(rng), new TextSpan(start, 1 + rng.NextInt(textLength - start))));
        }

        return slots;
    }

    private static EvaluationConfig PickConfig(PipelineHarness harness, SeededRng rng)
    {
        var roll = rng.NextDouble();
        if (roll < 0.4)
        {
            return harness.Bundle.EvaluationConfig;
        }

        if (roll < 0.7)
        {
            return EvaluationConfig.Default;
        }

        DifficultyEvaluationConfig One()
        {
            var weights = ErrorTags.All.Concat(UnknownTags).Where(_ => rng.NextDouble() < 0.7).ToDictionary(static t => t, _ => Math.Round(rng.NextDouble() * 1.2, 3), StringComparer.Ordinal);
            return new DifficultyEvaluationConfig(
                weights.ToImmutableSortedDictionary(StringComparer.Ordinal),
                Math.Round(rng.NextDouble(), 3),
                Math.Round(rng.NextDouble(), 3),
                Math.Round(rng.NextDouble(), 3),
                rng.Pick(new[] { 0.0, 0.01, 0.02, 0.1 }),
                Math.Round(rng.NextDouble() * 0.5, 3));
        }

        return new EvaluationConfig(One(), One(), One());
    }

    private static PromptSpec PickPrompt(PipelineHarness harness, SeededRng rng)
    {
        var prompts = harness.Bundle.Prompts.Values.ToArray();
        var prompt = rng.Pick(prompts);
        if (rng.NextDouble() < 0.3)
        {
            prompt = prompt with
            {
                Difficulty = (Difficulty)rng.NextInt(3),
                Mode = rng.NextInt(2) == 0 ? RecognitionMode.Constrained : RecognitionMode.Open,
            };
        }

        return prompt;
    }

    private static MatchResult RandomMatch(PipelineHarness harness, SeededRng rng, PromptSpec prompt, DifficultyEvaluationConfig settings, bool allowNaN)
    {
        var type = (MatchType)rng.NextInt(4);
        if (type == MatchType.None)
        {
            return new MatchResult(null, MatchType.None, rng.NextInt(6), false, 0.0);
        }

        var answers = harness.Bundle.AnswerSets.TryGetValue(prompt.AnswerSetId, out var set) ? set.Answers : [];
        var id = answers.Count > 0 ? rng.Pick(answers).AnswerId : "unknown-answer";
        return new MatchResult(id, type, type == MatchType.Fuzzy ? rng.NextInt(4) : 0, rng.NextInt(4) == 0, Confidence(rng, settings, allowNaN));
    }

    private static PronunciationResult RandomPronunciation(SeededRng rng, int textLength, DifficultyEvaluationConfig settings, bool allowNaN)
    {
        var phones = new List<PhoneScore>();
        var errors = new List<ErrorTag>();
        if (textLength > 0)
        {
            for (var i = rng.NextInt(9); i > 0; i--)
            {
                var start = rng.NextInt(textLength);
                phones.Add(new PhoneScore(new TextSpan(start, 1 + rng.NextInt(textLength - start)), rng.Pick(["a", "e", "ɾ", "rr", "θ"]), rng.Pick(["a", "ə", "ɹ", "h"]), Math.Round(rng.NextDouble(), 6)));
            }

            for (var i = rng.NextInt(4); i > 0; i--)
            {
                var start = rng.NextInt(textLength);
                var tag = rng.NextDouble() < 0.8 ? rng.Pick(new[] { ErrorTags.PronVowel, ErrorTags.PronR, ErrorTags.PronSilentH, ErrorTags.PronStress }) : rng.Pick(ErrorTags.All);
                errors.Add(new ErrorTag(tag, new TextSpan(start, 1 + rng.NextInt(textLength - start)), rng.Pick(["ɾ", "a", "h"]), rng.Pick(["ɹ", "ə", string.Empty])));
            }
        }

        return new PronunciationResult(phones, errors, Math.Round(rng.NextDouble(), 6), Confidence(rng, settings, allowNaN), "fuzz-phoneme");
    }

    private static VersionStamp RandomVersions(PipelineHarness harness, SeededRng rng)
    {
        var models = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in new[] { "asr", "phoneme", "embedding", "tts" })
        {
            if (rng.NextInt(2) == 0)
            {
                models[key] = DeterministicHash.OfText(key + rng.NextUInt64());
            }
        }

        return new VersionStamp(harness.Bundle.ContentHash, models);
    }
}
