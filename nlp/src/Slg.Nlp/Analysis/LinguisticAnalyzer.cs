namespace Slg.Nlp.Analysis;

using Slg.Core.Stages;
using Slg.Core.Taxonomy;
using Slg.Core.Verdicts;
using Slg.Nlp.Lexicon;
using Slg.Nlp.Morphology;
using Slg.Nlp.SlotGrammar;

/// <summary>
/// Looks up every token, resolves each word to one part of speech (see <see cref="ReadingResolver"/>), checks agreement and word order on that reading, and
/// extracts slots with slot grammars; pure and deterministic.
/// </summary>
public sealed class LinguisticAnalyzer
{
    // More than one error, so that a tense mistake is not explained away by reading the verb form as a noun; see Cost.
    private const int PastMarkerWithoutVerbCost = 2;

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
    /// <remarks>
    /// The grammar is matched first, on every reading of every word, because the words it binds tell the resolver which part of speech each ambiguous word is.
    /// The checks then run on the resolved readings only; the returned tokens still list every reading of the lexicon, which the intent stage matches lemmas on.
    /// </remarks>
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

        var match = BestMatch(analyzed, grammars);
        var resolved = ReadingResolver.Resolve(analyzed, match, Cost);
        new AgreementChecker(resolved, _morphology, errors).Run();
        return new AnalysisResult(
            analyzed,
            match?.Slots ?? [],
            OrderErrors(errors),
            match?.Grammar.Id,
            allKnown ? 1.0 : 0.5);
    }

    // What the resolver minimizes: the number of distinct errors the checks find in one resolved reading of the sentence. A sentence that has a past-time adverb and
    // no finite verb at all costs two more, which is more than one tense mistake: ayer canto is not a noun and an adverb, it is a present verb after ayer. Without a
    // past-time adverb no such charge is made, because a sentence of nouns and adjectives (a list, a name, a greeting) has no verb and is fine.
    private int Cost(IReadOnlyList<AnalyzedToken> resolved)
    {
        var found = new List<ErrorTag>();
        var checker = new AgreementChecker(resolved, _morphology, found);
        checker.Run();
        var cost = found.Distinct().Count();
        return cost + (checker.HasPastTimeMarker() && !resolved.Any(static t => t.Readings.Any(ReadingClasses.IsFiniteVerb)) ? PastMarkerWithoutVerbCost : 0);
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
