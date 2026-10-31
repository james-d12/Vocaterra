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
    private readonly IMorphology _morphology;
    private readonly IAnalysisRules _rules;
    private readonly ILanguagePack _language;

    /// <summary>Creates an analyzer over a lexicon with the pack's morphology and analysis rules built over the same lexicon, for the language of the pack.</summary>
    public LinguisticAnalyzer(Lexicon lexicon, ILanguagePack language)
        : this(lexicon, ValidPack(language).CreateMorphology(lexicon), language)
    {
    }

    /// <summary>Creates an analyzer with an explicit morphology, for the language of the pack (the bundle's <c>Language</c>); the analysis rules are the pack's over the same lexicon and morphology.</summary>
    public LinguisticAnalyzer(Lexicon lexicon, IMorphology morphology, ILanguagePack language)
        : this(lexicon, morphology, language, ValidPack(language).CreateAnalysisRules(lexicon, morphology))
    {
    }

    /// <summary>Creates an analyzer with explicit morphology and analysis rules (M34, additive).</summary>
    /// <param name="lexicon">The lexicon.</param>
    /// <param name="morphology">The morphology of the language.</param>
    /// <param name="language">The language pack.</param>
    /// <param name="rules">The analysis rules of the language (the bundle's <c>AnalysisRules</c>).</param>
    public LinguisticAnalyzer(Lexicon lexicon, IMorphology morphology, ILanguagePack language, IAnalysisRules rules)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(morphology);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(rules);
        _lexicon = lexicon;
        _morphology = morphology;
        _language = language;
        _rules = rules;
    }

    private static ILanguagePack ValidPack(ILanguagePack language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return language;
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
    /// The analysis is exactly <see cref="Read"/>, <see cref="BestMatch(TextReadings, IReadOnlyList{SlotGrammar})"/> and <see cref="Complete"/>.
    /// </remarks>
    public AnalysisResult Analyze(NormalizedText text, IReadOnlyList<SlotGrammar> grammars)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(grammars);
        var readings = Read(text);
        return Complete(readings, BestMatch(readings, grammars));
    }

    /// <summary>The first half of an analysis: every lexicon reading of every token, which depends on the text alone.</summary>
    internal TextReadings Read(NormalizedText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var analyzed = new AnalyzedToken[text.Tokens.Count];
        var irregular = new ErrorTag?[text.Tokens.Count];
        var variants = new IReadOnlyList<LexicalFeatures>[text.Tokens.Count];
        for (var i = 0; i < text.Tokens.Count; i++)
        {
            analyzed[i] = new AnalyzedToken(text.Tokens[i], ReadingsFor(text.Tokens[i], out irregular[i], out variants[i]));
        }

        return new TextReadings(text, analyzed, irregular, variants);
    }

    /// <summary>The match an analysis completes with: the best of the grammars over every reading of every word, or null when none matches. Only this step reads the grammars.</summary>
    internal SlotGrammarMatch? BestMatch(TextReadings readings, IReadOnlyList<SlotGrammar> grammars)
    {
        ArgumentNullException.ThrowIfNull(readings);
        return BestMatch(grammars, grammar => Match(readings, grammar));
    }

    /// <summary>The match of one grammar over every reading of every word, or null; a function of the readings and the grammar alone.</summary>
    internal SlotGrammarMatch? Match(TextReadings readings, SlotGrammar grammar)
    {
        ArgumentNullException.ThrowIfNull(readings);
        return SlotGrammarMatcher.Match(readings.Tokens, grammar, _language, _rules);
    }

    /// <summary>
    /// The best of the grammars' matches, each given by <paramref name="match"/>: in list order, a match replaces the best so far only when it beats it, so the earlier
    /// grammar wins a tie. A caller that has matched a grammar over the same readings before can pass that match in (the content lint does).
    /// </summary>
    internal static SlotGrammarMatch? BestMatch(IReadOnlyList<SlotGrammar> grammars, Func<SlotGrammar, SlotGrammarMatch?> match)
    {
        ArgumentNullException.ThrowIfNull(grammars);
        ArgumentNullException.ThrowIfNull(match);
        SlotGrammarMatch? best = null;
        foreach (var grammar in grammars)
        {
            var found = match(grammar);
            if (found is not null && (best is null || SlotGrammarMatch.Beats(found, best)))
            {
                best = found;
            }
        }

        return best;
    }

    /// <summary>
    /// The second half of an analysis: names, errors, the resolved readings, the checks and the resolved words, given the readings and the grammar match over them. It is a
    /// function of the two alone, and since a match is a function of the readings and its grammar, a caller that analyzes one text against many lists of grammars can
    /// complete it once per winning grammar (the content lint does). The readings are not changed: each result gets its own list of tokens.
    /// </summary>
    internal AnalysisResult Complete(TextReadings readings, SlotGrammarMatch? match)
    {
        ArgumentNullException.ThrowIfNull(readings);
        var text = readings.Text;
        var errors = new List<ErrorTag>();
        var analyzed = new List<AnalyzedToken>(readings.Tokens);

        // The grammar decides first which words are names ([WORD] elements). A name is not an unknown word, and it is no irregular verb form either, even when
        // its spelling is one. It carries no reading at all, so no rule, check or recast treats it as a word (a lexicon verb form would be conjugated by a recast).
        var names = match?.WordTokens ?? new HashSet<int>();
        var allKnown = true;
        for (var i = 0; i < analyzed.Count; i++)
        {
            if (names.Contains(i))
            {
                analyzed[i] = new AnalyzedToken(analyzed[i].Token, []);
                continue;
            }

            if (readings.Irregular[i] is { } error)
            {
                errors.Add(error);
            }

            if (analyzed[i].Readings.Count == 0)
            {
                allKnown = false;
                errors.Add(new ErrorTag(ErrorTags.VocabUnknown, analyzed[i].Token.Span, string.Empty, analyzed[i].Token.Text));
            }
        }

        var resolved = ReadingResolver.Resolve(analyzed, readings.AccentVariants, match, Cost, _rules, _language.Folding);
        _rules.CreateAgreementChecker(resolved, errors).Run();
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
        var reported = new ReportedReadings(_rules).Choose(text.Tokens, analyzed, resolved, names, _lexicon);
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
        var checker = _rules.CreateAgreementChecker(resolved, found);
        checker.Run();
        var cost = found.Distinct().Count();
        return cost + (checker.HasPastTimeMarker() && !resolved.Any(static t => t.Readings.Any(ReadingClasses.IsFiniteVerb)) ? PastMarkerWithoutVerbCost : 0);
    }

    private List<ErrorTag> OrderErrors(List<ErrorTag> errors)
    {
        var ordered = errors.Distinct().ToList();
        ordered.Sort(CompareErrors);
        return ordered;
    }

    private int CompareErrors(ErrorTag left, ErrorTag right)
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

    // The rank of the tag in the language's error tag set; a tag the set does not list sorts after all that it does.
    private int TagRank(string tag)
    {
        return _language.ErrorTags.Rank(tag);
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
