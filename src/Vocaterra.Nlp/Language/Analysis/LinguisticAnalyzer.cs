namespace Vocaterra.Nlp.Language.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

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
    private readonly ILanguagePack _language;

    /// <summary>Creates an analyzer over a lexicon with a morphology generator built from the same lexicon, for the language of the pack.</summary>
    public LinguisticAnalyzer(Lexicon lexicon, ILanguagePack language)
        : this(lexicon, new MorphologyGenerator(lexicon), language)
    {
    }

    /// <summary>Creates an analyzer with an explicit morphology generator, for the language of the pack (the bundle's <c>Language</c>).</summary>
    public LinguisticAnalyzer(Lexicon lexicon, MorphologyGenerator morphology, ILanguagePack language)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(morphology);
        ArgumentNullException.ThrowIfNull(language);
        _lexicon = lexicon;
        _morphology = morphology;
        _language = language;
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
    /// A word that the best grammar binds to a <c>[WORD]</c> element is a name: it is not reported as <c>vocab.unknown</c>, it does not lower the confidence, and it takes
    /// part in no agreement check (see <see cref="ReadingResolver"/>).
    /// The grammar is matched first, on every reading of every word, because the words it binds tell the resolver which part of speech each ambiguous word is.
    /// The checks then run on the resolved readings only; the returned tokens still list every reading of the lexicon, which the intent stage matches lemmas on.
    /// </remarks>
    public AnalysisResult Analyze(NormalizedText text, IReadOnlyList<SlotGrammar> grammars)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(grammars);
        var errors = new List<ErrorTag>();
        var analyzed = new List<AnalyzedToken>(text.Tokens.Count);
        var irregular = new ErrorTag?[text.Tokens.Count];
        var variants = new IReadOnlyList<LexicalFeatures>[text.Tokens.Count];
        for (var i = 0; i < text.Tokens.Count; i++)
        {
            analyzed.Add(new AnalyzedToken(text.Tokens[i], ReadingsFor(text.Tokens[i], out irregular[i], out variants[i])));
        }

        // The grammar decides first which words are names ([WORD] elements). A name is not an unknown word, and it is no irregular verb form either, even when
        // its spelling is one. It carries no reading at all, so no rule, check or recast treats it as a word (a lexicon verb form would be conjugated by a recast).
        var match = BestMatch(analyzed, grammars);
        var names = match?.WordTokens ?? new HashSet<int>();
        var allKnown = true;
        for (var i = 0; i < analyzed.Count; i++)
        {
            if (names.Contains(i))
            {
                analyzed[i] = new AnalyzedToken(analyzed[i].Token, []);
                continue;
            }

            if (irregular[i] is { } error)
            {
                errors.Add(error);
            }

            if (analyzed[i].Readings.Count == 0)
            {
                allKnown = false;
                errors.Add(new ErrorTag(ErrorTags.VocabUnknown, analyzed[i].Token.Span, string.Empty, analyzed[i].Token.Text));
            }
        }

        var resolved = ReadingResolver.Resolve(analyzed, variants, match, Cost);
        new AgreementChecker(resolved, _morphology, errors).Run();
        return new AnalysisResult(
            analyzed,
            match?.Slots ?? [],
            OrderErrors(errors),
            match?.Grammar.Id,
            allKnown ? 1.0 : 0.5,
            ResolvedWords(text, analyzed, resolved, names));
    }

    // One resolved word per token: a name keeps no reading, an unknown word has none, and a word gets the reading the resolver left first.
    private List<ResolvedWord> ResolvedWords(NormalizedText text, IReadOnlyList<AnalyzedToken> analyzed, IReadOnlyList<AnalyzedToken> resolved, IReadOnlySet<int> names)
    {
        var words = new List<ResolvedWord>(text.Tokens.Count);
        var reported = ReportedReadings.Choose(text.Tokens, analyzed, resolved, names, _lexicon);
        for (var i = 0; i < text.Tokens.Count; i++)
        {
            if (names.Contains(i))
            {
                words.Add(new ResolvedWord(text.Tokens[i], WordRole.Name, null));
            }
            else if (analyzed[i].Readings.Count == 0)
            {
                words.Add(new ResolvedWord(text.Tokens[i], WordRole.Unknown, null));
            }
            else
            {
                words.Add(new ResolvedWord(text.Tokens[i], WordRole.Word, reported[i]));
            }
        }

        return words;
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

    private SlotGrammarMatch? BestMatch(IReadOnlyList<AnalyzedToken> tokens, IReadOnlyList<SlotGrammar> grammars)
    {
        SlotGrammarMatch? best = null;
        foreach (var grammar in grammars)
        {
            var match = SlotGrammarMatcher.Match(tokens, grammar, _language);
            if (match is not null && (best is null || SlotGrammarMatch.Beats(match, best)))
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

    // The readings of one token; an irregular verb form also yields the conj.irregular error it stands for, which the caller drops for a name. The accent
    // variants are the readings of the accented spellings of a plain lexicon form (esta, está); only the resolver reads them, and it keeps them only where the
    // neighbouring words call for them, so the returned tokens, the grammar matching and the intent stage see the readings of the spelling as typed.
    private IReadOnlyList<LexicalFeatures> ReadingsFor(Token token, out ErrorTag? irregular, out IReadOnlyList<LexicalFeatures> accentVariants)
    {
        irregular = null;
        accentVariants = [];
        if (_morphology.TryGetRegularization(token.Text, out var regularized))
        {
            irregular = new ErrorTag(ErrorTags.ConjIrregular, token.Span, regularized.Expected, token.Text);
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

        var found = _lexicon.LookupWithAccentVariants(token.Text);
        if (found.Exact.Count > 0)
        {
            accentVariants = [.. found.Variants.Select(static v => v.Reading)];
            return [.. found.Exact];
        }

        if (IsDigits(token.Text))
        {
            var number = string.Equals(token.Text, "1", StringComparison.Ordinal) ? LexiconSchema.Singular : LexiconSchema.Plural;
            return [new LexicalFeatures(token.Text, LexiconSchema.Numeral, null, number, null, null, null)];
        }

        return [];
    }
}
