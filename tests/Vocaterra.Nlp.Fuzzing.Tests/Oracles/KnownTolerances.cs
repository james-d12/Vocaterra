using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Fuzzing.Tests.Oracles;

/// <summary>
/// The only place an exception may be waved through. Every entry names the oracle that relies on it and says why the rejection is the
/// designed outcome rather than a bug; any other exception from the stage under test is a finding. The entries are the template engine's
/// missing slot (O12), the WAV reader's rejection of corrupted or unsupported files (O13), the content loader's validation failure on a
/// mutated bundle (O15) and a grammar that reads a word of the mutated sentence in two ways (O4, O5).
/// </summary>
public static class KnownTolerances
{
    /// <summary>
    /// O12: a <see cref="TemplateException"/> is expected when the slot map handed to a template lacks a placeholder the template reads.
    /// The engine reports a missing slot with the slot name in <see cref="TemplateException.Placeholder"/> (a malformed template or an
    /// invalid filter reports the raw placeholder text including its braces instead), so the entry only covers the exception whose
    /// placeholder really is absent from the slot map. A <see cref="TemplateException"/> for a slot that is present means the slot's
    /// value did not suit its filter, which the generators avoid by construction and which therefore counts as a finding.
    /// </summary>
    public static bool IsMissingSlot(TemplateException exception, SlotMap slots)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(slots);
        return exception.Placeholder.Length > 0
            && exception.Placeholder[0] != '{'
            && !slots.ContainsKey(exception.Placeholder);
    }

    /// <summary>
    /// O4 and O5: a mutated sentence on which the analyzer's answer differs from the mutation's is excused when the grammar itself reads a word of the sentence in two
    /// ways. Two elements that compete for a word that is both a noun and a verb form (<c>[NOUN:item]</c> or <c>[VERB]</c> over <c>duchas</c>, or <c>[ADJ]</c> or
    /// <c>[PRON]</c> over <c>alguno</c>) tie as parses, the word keeps both readings, and the charitable one may hold no mistake: <c>duchas complicados</c> is a verb and
    /// an adjective as well as a noun with its adjective of the wrong gender. The excuse needs a tie that touches a word of the sentence the analyzer read
    /// in more than one class; the oracles count the excused cases and assert that they stay rare.
    /// </summary>
    public static bool IsTiedParse(AnalysisResult analysis, SlotGrammar grammar)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(grammar);
        var match = SlotGrammarMatcher.Match(analysis.Tokens, grammar, SpanishLanguagePack.Instance);
        return match is not null
            && match.AmbiguousTokens.Any(index => analysis.Tokens[index].Readings.Select(static r => r.Pos).Distinct(StringComparer.Ordinal).Count() > 1);
    }

    /// <summary>
    /// O13: <see cref="InvalidDataException"/> (the bytes are not a well-formed WAV file) and <see cref="NotSupportedException"/> (a well-formed
    /// file with an encoding or channel layout the reader does not decode) are the only exceptions <c>WavFile.Read</c> documents for bad input.
    /// A corrupted header or a truncated file is expected to produce one of them, or a shorter clip; nothing else may escape.
    /// </summary>
    public static bool IsWavRejection(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is InvalidDataException or NotSupportedException;
    }

    /// <summary>
    /// O15: the content loader reports a bundle that breaks the schema, a hash or a cross-reference as a <see cref="ContentValidationException"/>
    /// listing every problem. It is the designed outcome for a mutated bundle, but only when at least one problem starts with the bundle-relative
    /// path of the file the mutation touched (the message must tell an author where to look); any other exception type is a bug.
    /// </summary>
    public static bool NamesPath(ContentValidationException exception, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        return exception.Problems.Any(problem => problem.StartsWith(relativePath, StringComparison.Ordinal));
    }

    /// <summary>Why the loader may accept a bundle with a field removed or retyped.</summary>
    public enum ContentFieldReason
    {
        /// <summary>The field is optional by design: it is nullable, a list that may be empty, or has a documented fallback.</summary>
        Optional,

        /// <summary>
        /// The field is a number, a flag or an enumeration the loader does not require: a missing value binds to zero, false or the first enumeration member
        /// without a message. This is a gap in the loader (README, known findings), tolerated here so the oracle stays strict about everything else.
        /// </summary>
        SilentDefault,

        /// <summary>
        /// A whole dialogue pool that no agent of the bundle names (neither as a dialogue intent nor as a variant pool): the host voices it (the game's guide does,
        /// <c>guide.find.found</c> and the like), so the loader has nothing to cross-check and a removed pool is a content change, not damage. A pool an agent names
        /// is never on this list: removing it must be rejected.
        /// </summary>
        HostVoicedPool,

        /// <summary>
        /// The slot grammars of an intent that also has keyword rules: an intent may be matched by its rules alone (the game's intents have both, and the lint
        /// reports an example only a rule covers), so the loader has no reason to refuse it. An intent without a keyword rule is never on this list: with no grammar
        /// and no rule it can match nothing, and removing its grammars must be rejected.
        /// </summary>
        RuleOnlyIntent,
    }

    /// <summary>
    /// O15: whether removing <c>slotGrammars</c> from the intent at the path leaves a bundle that is valid by design, because the intent has keyword rules in the
    /// original <c>intents/intents.json</c>. Returns <see cref="ContentFieldReason.RuleOnlyIntent"/> then, otherwise null.
    /// </summary>
    public static ContentFieldReason? AcceptedRuleOnlyIntent(string file, IReadOnlyList<string> path, System.Text.Json.Nodes.JsonNode originalIntentsFile)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(originalIntentsFile);
        if (file != "intents/intents.json" || path.Count != 3 || path[0] != "intents" || path[2] != "slotGrammars" || !int.TryParse(path[1], out var index))
        {
            return null;
        }

        return originalIntentsFile["intents"]![index]!["keywordRules"] is System.Text.Json.Nodes.JsonArray { Count: > 0 } ? ContentFieldReason.RuleOnlyIntent : null;
    }

    /// <summary>
    /// O15: whether removing the dialogue pool at the path leaves a bundle that is valid by design, because no agent of the bundle names the pool. Returns
    /// <see cref="ContentFieldReason.HostVoicedPool"/> then, otherwise null.
    /// </summary>
    public static ContentFieldReason? AcceptedHostVoicedPool(string file, IReadOnlyList<string> path, IEnumerable<AgentStateMachine> agents)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(agents);
        if (file != "dialogue/dialogue.json" || path.Count != 2 || path[0] != "pools")
        {
            return null;
        }

        var named = agents.SelectMany(static agent => agent.Definition.States)
            .SelectMany(static state => state.Transitions.SelectMany(static transition => transition.Actions).Concat(state.Fallback?.Actions ?? []))
            .Any(action => action.DialogueIntent == path[1] || action.VariantPoolId == path[1]);
        return named ? null : ContentFieldReason.HostVoicedPool;
    }

    // File prefix, then a path pattern: '/' separates segments, '*' matches any one segment (array indexes included), 'name*' matches a prefix.
    private static readonly (string File, string Path, ContentFieldReason Reason)[] ContentFields =
    [
        ("answer-sets/", "answers/*/aliases", ContentFieldReason.Optional),
        ("answer-sets/", "answers/*/dialects", ContentFieldReason.Optional),
        ("answer-sets/", "answers/*/dialects/*", ContentFieldReason.Optional),
        ("dialogue/dialogue.json", "pools/answer_accepted", ContentFieldReason.Optional),
        ("dialogue/dialogue.json", "pools/feedback.*", ContentFieldReason.Optional),
        ("effects/effects.json", "effects/*/requiresCommunicated", ContentFieldReason.Optional),
        ("effects/effects.json", "effects/*/curve/*/correctness", ContentFieldReason.SilentDefault),
        ("effects/effects.json", "effects/*/curve/*/value", ContentFieldReason.SilentDefault),
        ("evaluation/evaluation-config.json", "*/errorWeights/*", ContentFieldReason.Optional),
        ("evaluation/evaluation-config.json", "*/communicatedConfidenceThreshold", ContentFieldReason.SilentDefault),
        ("evaluation/evaluation-config.json", "*/correctnessThreshold", ContentFieldReason.SilentDefault),
        ("evaluation/evaluation-config.json", "*/pronunciationThreshold", ContentFieldReason.SilentDefault),
        ("evaluation/evaluation-config.json", "*/epsilon", ContentFieldReason.SilentDefault),
        ("evaluation/evaluation-config.json", "*/unknownTagWeight", ContentFieldReason.SilentDefault),
        ("feedback/feedback-policy.json", "notCommunicatedType", ContentFieldReason.Optional),
        ("feedback/feedback-policy.json", "rules/*/difficulty", ContentFieldReason.Optional),
        ("feedback/feedback-policy.json", "rules/*/type", ContentFieldReason.SilentDefault),
        ("intents/intents.json", "intents/*/examples", ContentFieldReason.Optional),
        ("intents/intents.json", "intents/*/keywordRules", ContentFieldReason.Optional),
        ("intents/intents.json", "intents/*/requiredSlots", ContentFieldReason.Optional),
        ("intents/intents.json", "intents/*/keywordRules/*/requiresSlots", ContentFieldReason.Optional),
        ("intents/intents.json", "intents/*/keywordRules/*/slotFromNumber", ContentFieldReason.Optional),

        // The expectations and waivers of the content lint (IntentExample, IntentLintWaivers): an example given as an object may drop its prompt (the lint then picks
        // the first open prompt that allows the intent) or its expected tags (it becomes a plain example), and an intent may drop its lint block or any waiver in it.
        ("intents/intents.json", "intents/*/examples/*/prompt", ContentFieldReason.Optional),
        ("intents/intents.json", "intents/*/examples/*/errors", ContentFieldReason.Optional),
        ("intents/intents.json", "intents/*/lint", ContentFieldReason.Optional),
        ("intents/intents.json", "intents/*/lint/*", ContentFieldReason.Optional),
        ("intents/intents.json", "intents/*/lint/*/*", ContentFieldReason.Optional),
        ("lexicon/", "generated_by", ContentFieldReason.Optional),
        ("lexicon/", "entries/*/features/*", ContentFieldReason.Optional),

        // The sidecar of a generated lexicon (lexicon/*.source.json): the loader checks the schema version, the importer version, the generated file and
        // its entry and lemma counts, and every source record. The import parameters and the report are information for people; the loader reads them
        // for shape only (a missing count is 0, a missing reason row is a row less).
        ("lexicon/", "parameters/*", ContentFieldReason.Optional),
        ("lexicon/", "parameters/handAuthoredFiles/*", ContentFieldReason.Optional),
        ("lexicon/", "report/*", ContentFieldReason.SilentDefault),
        ("lexicon/", "report/*/*", ContentFieldReason.Optional),
        ("agents/", "states/*/fallback", ContentFieldReason.Optional),
        ("agents/", "states/*/fallback/actions/*/variantPoolId", ContentFieldReason.Optional),
        ("agents/", "states/*/transitions/*/actions/*/hostEvent", ContentFieldReason.Optional),
        ("agents/", "states/*/transitions/*/actions/*/variantPoolId", ContentFieldReason.Optional),
        ("agents/", "states/*/transitions/*/on/communicated", ContentFieldReason.Optional),
        ("agents/", "states/*/transitions/*/on/intent", ContentFieldReason.Optional),
        ("prompts/prompts.json", "*/mode", ContentFieldReason.SilentDefault),
        ("prompts/prompts.json", "*/difficulty", ContentFieldReason.SilentDefault),
        ("prompts/prompts.json", "*/agentId", ContentFieldReason.Optional),
        ("prompts/prompts.json", "*/text", ContentFieldReason.Optional),
        ("provenance.json", "items/*/approvedBy", ContentFieldReason.Optional),
        ("provenance.json", "items/*/approvedOn", ContentFieldReason.Optional),

        // The model fields (M29g): required for a model-output item (the validator refuses a missing model there, and refuses them elsewhere), so
        // dropping one from an item of another kind leaves a loadable bundle.
        ("provenance.json", "items/*/model", ContentFieldReason.Optional),
        ("provenance.json", "items/*/voice", ContentFieldReason.Optional),
        ("provenance.json", "items/*/speaker", ContentFieldReason.Optional),
        ("provenance.json", "items/*/source", ContentFieldReason.Optional),
    ];

    /// <summary>
    /// O15: dropping or retyping a field the loader does not require may leave a loadable bundle. Returns why, or null when the place is not on the
    /// documented list, in which case an accepted mutation there is a finding (a field that should have been validated, or a new optional field that
    /// should be added to the list with its reason).
    /// </summary>
    public static ContentFieldReason? AcceptedContentField(string file, IReadOnlyList<string> path)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);
        ArgumentNullException.ThrowIfNull(path);
        foreach (var (prefix, pattern, reason) in ContentFields)
        {
            if (file.StartsWith(prefix, StringComparison.Ordinal) && PathMatches(pattern.Split('/'), path))
            {
                return reason;
            }
        }

        return null;
    }

    private static bool PathMatches(string[] pattern, IReadOnlyList<string> path)
    {
        if (pattern.Length != path.Count)
        {
            return false;
        }

        for (var i = 0; i < pattern.Length; i++)
        {
            var segment = pattern[i];
            var matches = segment == "*"
                || segment == path[i]
                || (segment.EndsWith('*') && path[i].StartsWith(segment[..^1], StringComparison.Ordinal));
            if (!matches)
            {
                return false;
            }
        }

        return true;
    }
}
