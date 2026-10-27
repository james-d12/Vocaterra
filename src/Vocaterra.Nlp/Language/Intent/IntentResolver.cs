using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Language.Intent;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Language.Normalization;
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
        if (candidates.Count == 0)
        {
            return Unresolved;
        }

        return TryGrammar(analysis, candidates)
            ?? TryRules(analysis, candidates)
            ?? TryEmbedding(text, analysis, candidates)
            ?? Unresolved;
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
                : AnalysisSlotFinder.FirstNoun(analysis, null, _language);
            if (value is not null)
            {
                map = map.With(declared.Name, value);
            }
        }

        return map;
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

    private IntentResolution? TryGrammar(AnalysisResult analysis, List<IntentDefinition> candidates)
    {
        if (analysis.MatchedGrammarId is { } matchedId)
        {
            foreach (var candidate in candidates)
            {
                foreach (var grammar in candidate.Grammars)
                {
                    if (string.Equals(grammar.Id, matchedId, StringComparison.Ordinal))
                    {
                        return new IntentResolution(candidate.Id, SlotsOf(analysis.Slots), IntentRung.Grammar, 1.0, analysis.Confidence);
                    }
                }
            }
        }

        IntentDefinition? bestIntent = null;
        SlotGrammarMatch? best = null;
        foreach (var candidate in candidates)
        {
            foreach (var grammar in candidate.Grammars)
            {
                var match = SlotGrammarMatcher.Match(analysis.Tokens, grammar, _language, _rules);
                if (match is not null && (best is null || SlotGrammarMatch.Beats(match, best)))
                {
                    best = match;
                    bestIntent = candidate;
                }
            }
        }

        return bestIntent is null || best is null
            ? null
            : new IntentResolution(bestIntent.Id, best.ToSlotMap(), IntentRung.Grammar, 1.0, analysis.Confidence);
    }

    private IntentResolution? TryRules(AnalysisResult analysis, List<IntentDefinition> candidates)
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
        if (rule.SlotFromNoun is { } nounSlot && AnalysisSlotFinder.FirstNoun(analysis, keywordIndexes, _language) is { } noun)
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
