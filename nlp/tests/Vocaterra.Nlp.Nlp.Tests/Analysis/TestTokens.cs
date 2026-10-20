namespace Vocaterra.Nlp.Nlp.Tests.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Nlp.Analysis;
using Vocaterra.Nlp.Nlp.Lexicon;
using Vocaterra.Nlp.Nlp.Morphology;
using Vocaterra.Nlp.Nlp.SlotGrammar;

/// <summary>Builds hand-made normalized text from a space-separated string, standing in for the real normalizer.</summary>
internal static class TestTokens
{
    /// <summary>Splits on single spaces; token spans index into the text, and the original surface form equals the token text.</summary>
    public static NormalizedText From(string text)
    {
        var tokens = new List<Token>();
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf(' ', start);
            if (end < 0)
            {
                end = text.Length;
            }

            if (end > start)
            {
                tokens.Add(new Token(text[start..end], new TextSpan(start, end - start), text[start..end]));
            }

            start = end + 1;
        }

        return new NormalizedText(text, tokens);
    }
}

/// <summary>Shared lexicon, generator and analyzer instances built from the embedded seed lexicon.</summary>
internal static class Fixtures
{
    public static Lexicon Seed { get; } = Lexicon.Seed;

    public static MorphologyGenerator Morphology { get; } = new(Lexicon.Seed);

    public static MorphologyGenerator RulesOnly { get; } = new(Lexicon.Empty);

    public static LinguisticAnalyzer Analyzer { get; } = new(Lexicon.Seed);

    public static AnalysisResult Analyze(string text, params string[] grammarPatterns)
    {
        var grammars = grammarPatterns.Select(pattern => SlotGrammar.Parse(pattern)).ToList();
        return Analyzer.Analyze(TestTokens.From(text), grammars);
    }

    public static IReadOnlyList<ErrorTag> Errors(string text)
    {
        return Analyzer.Analyze(TestTokens.From(text)).Errors;
    }

    public static string Describe(IReadOnlyList<ErrorTag> errors)
    {
        return string.Join("; ", errors.Select(e => $"{e.Tag}@{e.Span.Start}+{e.Span.Length} '{e.Actual}'->'{e.Expected}'"));
    }
}
