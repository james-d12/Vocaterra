using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Nlp.Analysis;
using Vocaterra.Nlp.Nlp.Lexicon;
using Vocaterra.Nlp.Nlp.Morphology;
using Vocaterra.Nlp.Nlp.Normalization;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Response.Tests;

/// <summary>Shared engines, analyzers and verdict builders for the response tests.</summary>
internal static class TestSupport
{
    private static readonly NormalizationOptions KeepWords = new() { ConvertIndefiniteArticles = false };

    public static MorphologyGenerator Morphology { get; } = new(Lexicon.Seed);

    public static TemplateEngine Engine { get; } = new(Lexicon.Seed, new MorphologyGenerator(Lexicon.Seed));

    public static LinguisticAnalyzer Analyzer { get; } = new(Lexicon.Seed);

    public static SlotMap Slots(params (string Key, string Value)[] pairs)
    {
        return SlotMap.From(pairs.Select(static p => new KeyValuePair<string, string>(p.Key, p.Value)));
    }

    public static DialogueTemplate Template(string text, string lineId = "test.line")
    {
        return new DialogueTemplate(lineId, text);
    }

    public static NormalizedText Normalize(string text, bool numbersToDigits = false)
    {
        return TextNormalizer.Normalize(text, KeepWords with { NumbersToDigits = numbersToDigits });
    }

    public static Verdict MakeVerdict(string intent, bool communicated = true, double correctness = 1.0, SlotMap? slots = null)
    {
        return new Verdict(
            new IntentId(intent),
            slots ?? SlotMap.Empty,
            communicated,
            correctness,
            [],
            [],
            1.0,
            ResolvedBy.Exact,
            VersionStamp.Empty);
    }
}
