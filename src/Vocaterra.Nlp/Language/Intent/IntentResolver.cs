using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Language.Intent;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// Resolves an utterance to an intent with a fixed ladder: slot grammar, then lemma keyword rules, then embedding nearest neighbour,
/// then <see cref="IntentId.Unknown"/>. Pure and deterministic for the same catalog, model, options and inputs.
/// </summary>
/// <remarks>
/// Only intents listed in <see cref="PromptSpec.AllowedIntentIds"/> are candidates (an empty list allows every intent) and they are
/// always visited in ordinal id order, whatever the order of the list. <see cref="PromptSpec.SlotGrammarIds"/> is not used here: it
/// selects the grammars the analyzer was given, while this resolver also matches the candidates' own grammars.
/// <para>
/// Rung 1 (grammar): the grammar named by <see cref="AnalysisResult.MatchedGrammarId"/> when a candidate owns it, otherwise the candidate
/// grammar that matches the analyzed tokens with the most explicitly consumed tokens, then the most tokens bound to literal words (first intent and first grammar win a further tie).
/// Margin is 1 and confidence is the analysis confidence.
/// </para>
/// <para>
/// Rung 2 (rules): a rule fires when all its lemmas occur and all its required slots can be filled; the rule with the most lemmas wins,
/// and when two different intents tie at the top the rung is ambiguous and is skipped. Slots come from the first noun or numeral that is
/// not itself a rule keyword (a noun in "por favor" is ignored). Margin is 1 and confidence is
/// <see cref="IntentResolverOptions.RulesConfidence"/>.
/// </para>
/// <para>
/// Rung 3 (embedding, only with a model): the best example score per intent is the cosine similarity of unit vectors summed in fixed
/// order; intents sort by score descending then ordinal id. The top intent is accepted only when
/// <see cref="IntentResolverOptions.EmbeddingTop1"/> accepts its score and <see cref="IntentResolverOptions.EmbeddingGap"/> accepts the
/// gap to the runner-up (1 with a single scored candidate). An Uncertain or Reject decision on either margin falls through to Unknown.
/// Margin is the gap and confidence the top score.
/// </para>
/// Unknown resolves with <see cref="IntentRung.Unresolved"/>, empty slots, margin 0 and confidence 0.
/// <para>
/// Off the prompt (M72): when the allowed intents resolve nothing at a prompt that lists allowed intents, the grammar and keyword rungs run again over every
/// intent of the catalog, in the same order and with the same rules, and the intent they find is reported as <see cref="IntentResolution.OffPromptIntent"/>
/// of the unknown resolution (the intent, slots, rung, margin and confidence stay those of Unknown). It is always an intent the prompt does not allow, and it is
/// the intent the grammar or rules rung would resolve with every intent allowed. A prompt with no allowed intent (every intent a candidate) has no second pass.
/// </para>
/// <para>
/// The catalog's reading (M74): the grammar rung reads the line over the whole catalog before the prompt narrows it. The best grammar of the intents the prompt
/// does not allow takes the line from the best allowed grammar when it beats it in the rung's order and binds more tokens to elements that name the word
/// (<see cref="SlotGrammarMatch.NamedTokenCount"/>: literal words and verbs of a named lemma), so it knows a word the allowed grammar reads only as a class of word
/// (<c>hace calor</c>: <c>calor</c>, not <c>[NOUN:item]</c>). The line is then unknown with that intent off the prompt. A line no allowed grammar matches goes
/// down the ladder as before (the allowed keyword rules first). With every intent allowed, a grammar the analyzer was not given replaces the analyzer's match
/// when it matches the analyzed tokens better, so the off-prompt intent is still the intent with every intent allowed.
/// </para>
/// <para>
/// The utterance is embedded exactly as given (<see cref="NormalizedText.Text"/>) while the catalog examples are normalized with
/// <see cref="IntentResolverOptions.ExampleNormalization"/>, so normalize the utterance with the same options (indefinite articles
/// kept) for the embedding rung to compare like with like.
/// </para>
/// </remarks>
public sealed class IntentResolver
{
    private static readonly IntentResolution Unresolved = new(IntentId.Unknown, SlotMap.Empty, IntentRung.Unresolved, 0.0, 0.0);

    private readonly IntentCatalog _catalog;
    private readonly ILanguagePack _language;
    private readonly IAnalysisRules _rules;
    private readonly TypedMatching _typed;
    private readonly IEmbeddingModel? _embeddings;
    private readonly IntentResolverOptions _options;
    private readonly Lazy<Dictionary<string, double[][]>> _exampleVectors;

    // The catalog's grammars that can match the tokens of one analysis (TokenFacts.CanMatch), in catalog order, worked out once per analysis: a grammar that cannot match
    // never takes a line, so the grammar rung visits only these. The entries go with their facts.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TokenFacts, (IntentDefinition Intent, SlotGrammar Grammar)[]> _plausible = new();

    /// <summary>
    /// Creates a resolver for the language of the pack (the bundle's <c>Language</c>): examples are normalized and the slot grammars matched with it. Without
    /// an embedding model the third rung is skipped. Example vectors are computed on first use.
    /// </summary>
    /// <param name="catalog">The intent catalog.</param>
    /// <param name="language">The language pack.</param>
    /// <param name="embeddings">The embedding model, or null to skip the third rung.</param>
    /// <param name="options">The thresholds, or null for the defaults.</param>
    /// <param name="rules">The analysis rules of the language (the bundle's <c>AnalysisRules</c>), or null for the pack's rules over an empty lexicon (M34, additive).</param>
    public IntentResolver(IntentCatalog catalog, ILanguagePack language, IEmbeddingModel? embeddings = null, IntentResolverOptions? options = null, IAnalysisRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(language);
        _catalog = catalog;
        _language = language;
        _rules = rules ?? AnalysisRules.For(language);
        _typed = new TypedMatching(language.Folding, _rules);
        _embeddings = embeddings;
        _options = options ?? IntentResolverOptions.Default;
        _exampleVectors = new Lazy<Dictionary<string, double[][]>>(BuildExampleVectors, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Resolves the intent and its slots; never returns null and throws only for invalid arguments or a misbehaving embedding model.</summary>
    public IntentResolution Resolve(NormalizedText text, AnalysisResult analysis, PromptSpec prompt)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(prompt);
        var candidates = Candidates(prompt);
        var facts = TokenFacts.Of(analysis.Tokens, _language, _rules);
        var grammar = candidates.Count == 0 ? null : AllowedGrammar(analysis, facts, candidates, everyIntent: prompt.AllowedIntentIds.Count == 0);

        // M74: the grammar rung reads the line over the whole catalog before the prompt narrows it. A grammar of an intent the prompt does not allow takes the line
        // from the best allowed grammar when it beats it in the rung's own order and names more of the line's words: it knows a word the allowed grammar reads only
        // as a class of word (hace calor is the weather, not the [NOUN:item] of [VERB:hacer] [NOUN:item]). The line is then off the prompt.
        if (prompt.AllowedIntentIds.Count > 0 && grammar is not null && OutsideGrammar(analysis, facts, candidates, grammar) is { } outside)
        {
            return Unresolved with { OffPromptIntent = outside.Intent.Id };
        }

        var resolution = grammar?.Resolution(analysis)
            ?? (candidates.Count == 0 ? null : TryRules(analysis, candidates) ?? TryEmbedding(text, analysis, candidates));
        if (resolution is not null)
        {
            return resolution;
        }

        // M72: a line the prompt's intents do not take may still be one the catalog understands (me gusta el río at a prompt without state_like). With every intent
        // allowed already (an empty list) there is nothing more to find.
        return prompt.AllowedIntentIds.Count > 0 ? OffPrompt(analysis, facts, candidates) : Unresolved;
    }

    // The second pass over the whole catalog, with the grammar and keyword rungs only: the embedding rung is a guess, and a guess is no ground for saying what
    // a line the prompt does not take means. The analysis is the one the prompt's grammars gave; the grammar rung matches every intent's own grammars itself.
    private IntentResolution OffPrompt(AnalysisResult analysis, TokenFacts facts, List<IntentDefinition> candidates)
    {
        // No allowed grammar matches here, so the best grammar of the catalog is one of the intents the prompt does not allow.
        var found = OutsideGrammar(analysis, facts, candidates, null)?.Intent.Id ?? TryRules(analysis, _catalog.Intents)?.Intent;
        return found is null ? Unresolved : Unresolved with { OffPromptIntent = found };
    }

    private static SlotMap SlotsOf(IReadOnlyList<SlotValue> slots)
    {
        var map = SlotMap.Empty;
        foreach (var slot in slots)
        {
            if (!map.ContainsKey(slot.Name))
            {
                map = map.With(slot.Name, slot.Value);
            }
        }

        return map;
    }

    private SlotMap DeclaredSlotsOf(IntentDefinition intent, AnalysisResult analysis)
    {
        var map = SlotMap.Empty;
        foreach (var declared in intent.DeclaredSlots)
        {
            var value = declared.Kind == SlotKind.Number
                ? AnalysisSlotFinder.FirstNumber(analysis, null)
                : AnalysisSlotFinder.FirstNoun(analysis, null, _language, _rules);
            if (value is not null)
            {
                map = map.With(declared.Name, value);
            }
        }

        return map;
    }

    private (IntentDefinition Intent, SlotGrammar Grammar)[] PlausibleGrammars(TokenFacts facts)
    {
        if (!_plausible.TryGetValue(facts, out var found))
        {
            var list = new List<(IntentDefinition, SlotGrammar)>();
            foreach (var intent in _catalog.Intents)
            {
                foreach (var grammar in intent.Grammars)
                {
                    if (facts.CanMatch(grammar))
                    {
                        list.Add((intent, grammar));
                    }
                }
            }

            found = [.. list];
            _plausible.AddOrUpdate(facts, found);
        }

        return found;
    }

    private List<IntentDefinition> Candidates(PromptSpec prompt)
    {
        var allowed = prompt.AllowedIntentIds;
        HashSet<string>? allowedSet = null;
        if (allowed is { Count: > 0 })
        {
            allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        }

        var result = new List<IntentDefinition>(_catalog.Intents.Count);
        foreach (var intent in _catalog.Intents)
        {
            if (allowedSet is null || allowedSet.Contains(intent.Id.Value))
            {
                result.Add(intent);
            }
        }

        return result;
    }

    // The grammar rung over the allowed intents: the grammar the analyzer matched when one of them owns it, otherwise the one that matches best (the most tokens consumed
    // by explicit elements, then the most bound to literal words; the first intent and the first grammar win a further tie). The analyzer chose the best of the
    // grammars it was given over every reading of every word, which the pipeline gives exactly the allowed intents' grammars. With every intent allowed (M74), the
    // line is read over the whole catalog: a grammar the analyzer was not given replaces its match when it matches the analyzed tokens better.
    private GrammarReading? AllowedGrammar(AnalysisResult analysis, TokenFacts facts, List<IntentDefinition> candidates, bool everyIntent)
    {
        GrammarReading? best = null;
        if (analysis.MatchedGrammarId is { } matchedId)
        {
            foreach (var intent in candidates)
            {
                foreach (var grammar in intent.Grammars)
                {
                    if (best is null && string.Equals(grammar.Id, matchedId, StringComparison.Ordinal))
                    {
                        // The analyzer may have matched over the readings of a learner's slip that the analyzed tokens no longer hold (M58); such a match is
                        // compared with nothing.
                        best = GrammarReading.Of(intent, SlotGrammarMatcher.Match(facts, grammar), fromAnalyzer: true);
                    }
                }
            }

            if (best is not null && (!everyIntent || best.Match is null))
            {
                return best;
            }
        }

        foreach (var (intent, grammar) in PlausibleGrammars(facts))
        {
            if (candidates.Contains(intent))
            {
                best = Better(best, intent, grammar, facts) ?? best;
            }
        }

        return best;
    }

    // The grammar rung over the intents the prompt does not allow (M74), given the best allowed reading (null when no allowed grammar matches, for the off-prompt
    // pass): the best of their grammars when no allowed grammar matches, or the best of those that beat the allowed reading when it names more of the words than
    // that reading; otherwise null. A grammar that cannot beat the best so far (by the most tokens it can consume and bind to literal words) is not matched, and none is when none of them
    // could name more of the words than the allowed reading, since then nothing takes the line from it whichever grammar is best. A match over a slip's readings
    // is never taken from.
    private GrammarReading? OutsideGrammar(AnalysisResult analysis, TokenFacts facts, List<IntentDefinition> candidates, GrammarReading? allowed)
    {
        if (allowed is { Match: null })
        {
            return null;
        }

        var tokens = analysis.Tokens.Count;
        if (allowed is not null)
        {
            // The trailing polite phrase every grammar allows binds to literal words too (M83), which the grammar's own bounds do not count.
            var polite = _language.GrammarDefaults.TrailingPoliteWords.Count;
            var any = false;
            // A grammar that cannot match the tokens can never replace the allowed reading, so only those that can are looked at.
            foreach (var (intent, grammar) in PlausibleGrammars(facts))
            {
                if (!candidates.Contains(intent))
                {
                    any |= allowed.Score.CanBeBeatenBy(grammar, tokens, polite) && Math.Min(grammar.MaxNamedTokens + polite, tokens) > allowed.Score.Named;
                }
            }

            if (!any)
            {
                return null;
            }
        }

        var best = allowed;
        foreach (var (intent, grammar) in PlausibleGrammars(facts))
        {
            if (!candidates.Contains(intent))
            {
                best = Better(best, intent, grammar, facts) ?? best;
            }
        }

        return ReferenceEquals(best, allowed) || (allowed is not null && best!.Score.Named <= allowed.Score.Named) ? null : best;
    }

    // The grammar's reading when it beats the best so far (or there is none), otherwise null; the grammar is not matched when it cannot beat it.
    private static GrammarReading? Better(GrammarReading? best, IntentDefinition intent, SlotGrammar grammar, TokenFacts facts)
    {
        if (best is not null && (string.Equals(grammar.Id, best.Match!.Grammar.Id, StringComparison.Ordinal) || !best.Score.CanBeBeatenBy(grammar, facts.Tokens.Count, facts.Language.GrammarDefaults.TrailingPoliteWords.Count)))
        {
            return null;
        }

        var match = SlotGrammarMatcher.Match(facts, grammar);
        return match is not null && (best is null || SlotGrammarMatch.Beats(match, best.Match!)) ? GrammarReading.Of(intent, match, fromAnalyzer: false) : null;
    }

    // What the rung's order and M74's rule compare of a match: the tokens consumed by explicit elements, those bound to literal words, and those whose word is named.
    private readonly record struct MatchScore(int Explicit, int Literal, int Named)
    {
        public static MatchScore Of(SlotGrammarMatch match) => new(match.ExplicitTokenCount, match.LiteralTokenCount, match.NamedTokenCount);

        // False when no match of the grammar over that many tokens can beat a match of this score: it cannot consume more tokens with explicit elements, nor as
        // many with more of them bound to literal words. The grammar's bound on literal words does not count the trailing polite phrase (M83: the matcher binds its
        // words to literals too, so an optional (por favor)? that matches the phrase before it leaves the phrase to the line's end), hence the polite words.
        public bool CanBeBeatenBy(SlotGrammar grammar, int tokens, int politeWords)
        {
            var explicitTokens = Math.Min(grammar.MaxTokens, tokens);
            return explicitTokens > Explicit || (explicitTokens == Explicit && Math.Min(grammar.MaxLiteralTokens + politeWords, tokens) > Literal);
        }
    }

    // The intent a grammar reads the line as, its match over the analyzed tokens (null only for the analyzer's match over a slip's readings) and its score, and
    // whether it is the analyzer's own grammar, whose slots are the analysis's.
    private sealed record GrammarReading(IntentDefinition Intent, SlotGrammarMatch? Match, MatchScore Score, bool FromAnalyzer)
    {
        public static GrammarReading Of(IntentDefinition intent, SlotGrammarMatch? match, bool fromAnalyzer) =>
            new(intent, match, match is null ? default : MatchScore.Of(match), fromAnalyzer);

        public IntentResolution Resolution(AnalysisResult analysis) => new(
            Intent.Id,
            FromAnalyzer || Match is null ? SlotsOf(analysis.Slots) : Match.ToSlotMap(),
            IntentRung.Grammar,
            1.0,
            analysis.Confidence);
    }

    private IntentResolution? TryRules(AnalysisResult analysis, IReadOnlyList<IntentDefinition> candidates)
    {
        IntentDefinition? winner = null;
        SlotMap winnerSlots = SlotMap.Empty;
        var bestSpecificity = 0;
        var ambiguous = false;
        foreach (var candidate in candidates)
        {
            foreach (var rule in candidate.KeywordRules)
            {
                if (!TryApply(rule, analysis, out var slots))
                {
                    continue;
                }

                var specificity = rule.Lemmas.Count;
                if (specificity > bestSpecificity)
                {
                    bestSpecificity = specificity;
                    winner = candidate;
                    winnerSlots = slots;
                    ambiguous = false;
                }
                else if (specificity == bestSpecificity && !ReferenceEquals(winner, candidate))
                {
                    ambiguous = true;
                }
            }
        }

        return winner is null || ambiguous
            ? null
            : new IntentResolution(winner.Id, winnerSlots, IntentRung.Rules, 1.0, _options.RulesConfidence);
    }

    private bool TryApply(KeywordRule rule, AnalysisResult analysis, out SlotMap slots)
    {
        slots = SlotMap.Empty;
        var keywordIndexes = new HashSet<int>();
        foreach (var lemma in rule.Lemmas)
        {
            var index = IndexOfLemma(analysis, lemma);
            if (index < 0)
            {
                return false;
            }

            keywordIndexes.Add(index);
        }

        var map = SlotMap.Empty;
        if (rule.SlotFromNoun is { } nounSlot && AnalysisSlotFinder.FirstNoun(analysis, keywordIndexes, _language, _rules) is { } noun)
        {
            map = map.With(nounSlot, noun);
        }

        if (rule.SlotFromNumber is { } numberSlot && AnalysisSlotFinder.FirstNumber(analysis, keywordIndexes) is { } number)
        {
            map = map.With(numberSlot, number);
        }

        foreach (var required in rule.RequiresSlots)
        {
            if (!map.ContainsKey(required))
            {
                return false;
            }
        }

        slots = map;
        return true;
    }

    private int IndexOfLemma(AnalysisResult analysis, string lemma)
    {
        for (var i = 0; i < analysis.Tokens.Count; i++)
        {
            if (_typed.MatchesLiteral(analysis.Tokens[i], lemma))
            {
                return i;
            }
        }

        return -1;
    }

    private IntentResolution? TryEmbedding(NormalizedText text, AnalysisResult analysis, List<IntentDefinition> candidates)
    {
        if (_embeddings is null || text.Text.Length == 0)
        {
            return null;
        }

        var examples = _exampleVectors.Value;
        var query = Unit(_embeddings.Embed(text.Text));
        var scored = new List<(IntentDefinition Intent, double Score)>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (!examples.TryGetValue(candidate.Id.Value, out var vectors) || vectors.Length == 0)
            {
                continue;
            }

            var best = double.NegativeInfinity;
            foreach (var vector in vectors)
            {
                var score = EmbeddingMath.Cosine(query, vector);
                if (score > best)
                {
                    best = score;
                }
            }

            scored.Add((candidate, best));
        }

        if (scored.Count == 0)
        {
            return null;
        }

        scored.Sort(static (a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : string.CompareOrdinal(a.Intent.Id.Value, b.Intent.Id.Value);
        });

        var top1 = scored[0].Score;
        // Cosines run from -1 to 1, so the raw gap can exceed 1; the margin is reported in [0, 1]. The clamp changes no decision, because every gap threshold is below 1.
        var gap = scored.Count > 1 ? Math.Min(1.0, top1 - scored[1].Score) : 1.0;
        if (_options.EmbeddingTop1.Evaluate(top1) != MarginDecision.Accept || _options.EmbeddingGap.Evaluate(gap) != MarginDecision.Accept)
        {
            return null;
        }

        var intent = scored[0].Intent;
        return new IntentResolution(intent.Id, DeclaredSlotsOf(intent, analysis), IntentRung.Embedding, gap, top1);
    }

    private double[] Unit(float[] vector)
    {
        if (vector is null || vector.Length != _embeddings!.Dimension)
        {
            throw new InvalidOperationException(
                $"Embedding model returned {(vector is null ? "null" : vector.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))} values; expected {_embeddings!.Dimension}.");
        }

        return EmbeddingMath.ToUnit(vector);
    }

    private Dictionary<string, double[][]> BuildExampleVectors()
    {
        var result = new Dictionary<string, double[][]>(StringComparer.Ordinal);
        foreach (var intent in _catalog.Intents)
        {
            var vectors = new List<double[]>(intent.Examples.Count);
            foreach (var example in intent.Examples)
            {
                var normalized = _language.Normalizer.Normalize(example, _options.ExampleNormalization).Text;
                if (normalized.Length > 0)
                {
                    vectors.Add(Unit(_embeddings!.Embed(normalized)));
                }
            }

            result.Add(intent.Id.Value, [.. vectors]);
        }

        return result;
    }
}
