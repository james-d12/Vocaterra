using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Core.Taxonomy;
using Slg.Core.Verdicts;
using MatchType = Slg.Core.Stages.MatchType;

namespace Slg.Evaluation.Tests;

/// <summary>Builders for fake stage outputs; every call returns fresh instances so tests can mutate copies freely.</summary>
internal static class Fixtures
{
    public const string ErrorPersonText = "tengo";

    public static VersionStamp Versions()
    {
        return new VersionStamp(
            "content-abc",
            new Dictionary<string, string> { ["wav2vec2"] = "w-hash", ["vosk"] = "v-hash" });
    }

    public static PromptSpec Prompt(
        RecognitionMode mode = RecognitionMode.Open,
        Difficulty difficulty = Difficulty.Beginner,
        string answerSetId = "blacksmith.offer")
    {
        return new PromptSpec("blacksmith.offer.1", mode, difficulty, answerSetId, ["offer_item"], "blacksmith", ["offer_item.have"]);
    }

    public static AsrResult Asr(double confidence = 0.9, RecognitionMode mode = RecognitionMode.Open)
    {
        return new AsrResult("yo tiene dos espada", [new AsrHypothesis("yo tiene dos espada", 1.0)], [], confidence, "asr-hash", mode);
    }

    public static MatchResult NoMatch(double confidence = 0.0)
    {
        return new MatchResult(null, MatchType.None, 0, false, confidence);
    }

    public static MatchResult Matched(MatchType type, double confidence = 1.0)
    {
        return new MatchResult("answer-1", type, 0, false, confidence);
    }

    public static AnalyzedToken Token(string text, int start, string? pos = null)
    {
        IReadOnlyList<LexicalFeatures> readings = pos is null
            ? []
            : [new LexicalFeatures(text, pos, null, null, null, null, null)];
        return new AnalyzedToken(new Token(text, new TextSpan(start, text.Length), text), readings);
    }

    public static ErrorTag ConjPerson()
    {
        return new ErrorTag(ErrorTags.ConjPerson, new TextSpan(3, 5), ErrorPersonText, "tiene");
    }

    public static ErrorTag NounNumber()
    {
        return new ErrorTag(ErrorTags.NounNumber, new TextSpan(13, 6), "espadas", "espada");
    }

    public static AnalysisResult Analysis(
        IReadOnlyList<AnalyzedToken>? tokens = null,
        IReadOnlyList<SlotValue>? slots = null,
        IReadOnlyList<ErrorTag>? errors = null,
        double confidence = 0.95)
    {
        return new AnalysisResult(tokens ?? [], slots ?? [], errors ?? [], null, confidence);
    }

    public static IntentResolution Intent(string id = "offer_item", IntentRung rung = IntentRung.Grammar, double confidence = 1.0, SlotMap? slots = null)
    {
        return new IntentResolution(new IntentId(id), slots ?? SlotMap.Empty, rung, 0.5, confidence);
    }

    public static IntentResolution UnknownIntent()
    {
        return new IntentResolution(IntentId.Unknown, SlotMap.Empty, IntentRung.Unresolved, 0.0, 0.0);
    }

    public static PronunciationResult Pronunciation(
        IReadOnlyList<PhoneScore>? phones = null,
        IReadOnlyList<ErrorTag>? errors = null,
        double confidence = 1.0)
    {
        return new PronunciationResult(phones ?? [], errors ?? [], 0.8, confidence, "pron-hash");
    }

    /// <summary>The HLD worked example "yo tiene dos espada" at the blacksmith, with deliberately unsorted analysis lists.</summary>
    public static VerdictInputs WorkedExample()
    {
        var analysis = new AnalysisResult(
            [
                Token("yo", 0, "pron"),
                Token("tiene", 3, "verb"),
                Token("dos", 9, "num"),
                Token("espada", 13, "noun"),
            ],
            [
                new SlotValue("qty", "2", new TextSpan(9, 3)),
                new SlotValue("item", "espada", new TextSpan(13, 6)),
            ],
            [NounNumber(), ConjPerson()],
            "offer_item.have",
            0.95);

        return new VerdictInputs(
            Prompt(),
            Asr(),
            NoMatch(),
            analysis,
            Intent(),
            null,
            ["item", "qty"],
            ["yo", "tener", "tiene", "tengo", "dos", "espada", "espadas"],
            EvaluationConfig.Default,
            Versions());
    }

    public static Verdict MakeVerdict(
        bool communicated = true,
        double correctness = 1.0,
        double confidence = 1.0,
        IReadOnlyList<PhoneScore>? phones = null)
    {
        return new Verdict(
            new IntentId("offer_item"),
            SlotMap.Empty,
            communicated,
            correctness,
            [],
            phones ?? [],
            confidence,
            ResolvedBy.Grammar,
            Versions());
    }

    public static PhoneScore Phone(double score)
    {
        return new PhoneScore(new TextSpan(0, 1), "a", "a", score);
    }
}
