namespace Slg.Nlp.Analysis;

using Slg.Core.Stages;
using Slg.Core.Taxonomy;
using Slg.Core.Verdicts;
using Slg.Nlp.Lexicon;
using Slg.Nlp.Morphology;
using Slg.Nlp.SlotGrammar;

/// <summary>Looks up every token, checks agreement and word order, and extracts slots with slot grammars; pure and deterministic.</summary>
public sealed class LinguisticAnalyzer
{
    private readonly Lexicon _lexicon;
    private readonly MorphologyGenerator _morphology;

    /// <summary>Creates an analyzer over a lexicon with a morphology generator built from the same lexicon.</summary>
    public LinguisticAnalyzer(Lexicon lexicon)
        : this(lexicon, new MorphologyGenerator(lexicon))
    {
    }

    /// <summary>Creates an analyzer with an explicit morphology generator.</summary>
    public LinguisticAnalyzer(Lexicon lexicon, MorphologyGenerator morphology)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(morphology);
        _lexicon = lexicon;
        _morphology = morphology;
    }

    /// <summary>Analyzes the text without any slot grammar.</summary>
    public AnalysisResult Analyze(NormalizedText text)
    {
        return Analyze(text, []);
    }

    /// <summary>
    /// Analyzes the text: unknown words, regularized irregular verbs, agreement, tense, ser/estar and word-order errors,
    /// plus the slots of the best matching grammar. Errors are ordered by span start and then by taxonomy order.
    /// </summary>
    public AnalysisResult Analyze(NormalizedText text, IReadOnlyList<SlotGrammar> grammars)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(grammars);
        var errors = new List<ErrorTag>();
        var analyzed = new List<AnalyzedToken>(text.Tokens.Count);
        var allKnown = true;
        foreach (var token in text.Tokens)
        {
            var readings = ReadingsFor(token, errors);
            if (readings.Count == 0)
            {
                allKnown = false;
                errors.Add(new ErrorTag(ErrorTags.VocabUnknown, token.Span, string.Empty, token.Text));
            }

            analyzed.Add(new AnalyzedToken(token, readings));
        }

        new AgreementChecker(analyzed, _morphology, errors).Run();
        var match = BestMatch(analyzed, grammars);
        return new AnalysisResult(
            analyzed,
            match?.Slots ?? [],
            OrderErrors(errors),
            match?.Grammar.Id,
            allKnown ? 1.0 : 0.5);
    }

    private static SlotGrammarMatch? BestMatch(IReadOnlyList<AnalyzedToken> tokens, IReadOnlyList<SlotGrammar> grammars)
    {
        SlotGrammarMatch? best = null;
        foreach (var grammar in grammars)
        {
            var match = SlotGrammarMatcher.Match(tokens, grammar);
            if (match is not null && (best is null || match.ExplicitTokenCount > best.ExplicitTokenCount))
            {
                best = match;
            }
        }

        return best;
    }

    private static List<ErrorTag> OrderErrors(List<ErrorTag> errors)
    {
        var ordered = errors.Distinct().ToList();
        ordered.Sort(CompareErrors);
        return ordered;
    }

    private static int CompareErrors(ErrorTag left, ErrorTag right)
    {
        var cmp = left.Span.Start.CompareTo(right.Span.Start);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = TagRank(left.Tag).CompareTo(TagRank(right.Tag));
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = left.Span.Length.CompareTo(right.Span.Length);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = string.CompareOrdinal(left.Tag, right.Tag);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = string.CompareOrdinal(left.Expected, right.Expected);
        return cmp != 0 ? cmp : string.CompareOrdinal(left.Actual, right.Actual);
    }

    private static int TagRank(string tag)
    {
        for (var i = 0; i < ErrorTags.All.Count; i++)
        {
            if (string.Equals(ErrorTags.All[i], tag, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static bool IsDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private IReadOnlyList<LexicalFeatures> ReadingsFor(Token token, List<ErrorTag> errors)
    {
        if (_morphology.TryGetRegularization(token.Text, out var regularized))
        {
            errors.Add(new ErrorTag(ErrorTags.ConjIrregular, token.Span, regularized.Expected, token.Text));
            return
            [
                new LexicalFeatures(
                    regularized.Lemma,
                    LexiconSchema.Verb,
                    regularized.Person,
                    regularized.Number,
                    regularized.Tense,
                    LexiconSchema.Indicative,
                    null),
            ];
        }

        var found = _lexicon.Lookup(token.Text);
        if (found.Count > 0)
        {
            return [.. found];
        }

        if (IsDigits(token.Text))
        {
            var number = string.Equals(token.Text, "1", StringComparison.Ordinal) ? LexiconSchema.Singular : LexiconSchema.Plural;
            return [new LexicalFeatures(token.Text, LexiconSchema.Numeral, null, number, null, null, null)];
        }

        return [];
    }
}
