using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>
/// The host-side state one utterance is evaluated in: the world seed, the agent being talked to and its state, the stored time,
/// the learner model and how often each error tag was repeated. Nothing in it is read from a clock; the caller supplies every value.
/// </summary>
/// <param name="WorldSeed">The save's world seed; with the agent id and interaction counter it seeds every random draw.</param>
/// <param name="AgentId">The agent being talked to; when the bundle has no state machine for it the decision layer skips the agent step.</param>
/// <param name="AgentState">The agent's persisted state; its interaction counter selects the random stream.</param>
/// <param name="Now">The stored timestamp of the interaction.</param>
/// <param name="Learner">The learner model before this utterance.</param>
/// <param name="RepeatCountsByTag">How many times in a row each error tag (and <c>not_communicated</c>) was seen before this utterance; empty at the start. The pipeline adds this utterance's tags itself (<see cref="RepeatCounts.Next"/>).</param>
/// <param name="HostStateHash">A caller-supplied digest of the rest of the host state, copied into the replay record.</param>
/// <remarks>
/// Two further inputs are properties rather than constructor parameters, so existing positional constructions keep compiling:
/// <see cref="HostSlots"/> (host facts for response templates) and <see cref="ExtraSkills"/> (host-named skills for the learner update).
/// Both belong to one utterance: <see cref="Next"/> does not carry them over. Both are written into the replay envelope (omitted when empty),
/// and <see cref="Create"/> folds them into <see cref="HostStateHash"/> only when present, so a context without them hashes as it always did.
/// </remarks>
public sealed record ConversationContext(
    ulong WorldSeed,
    string AgentId,
    AgentState AgentState,
    StoredTimestamp Now,
    LearnerModel Learner,
    IReadOnlyDictionary<string, int> RepeatCountsByTag,
    string HostStateHash)
{
    private readonly SlotMap? _hostSlots;
    private readonly IReadOnlyList<string>? _extraSkills;

    /// <summary>The agent being talked to.</summary>
    public string AgentId { get; init; } = AgentId ?? throw new ArgumentNullException(nameof(AgentId));

    /// <summary>The agent's persisted state.</summary>
    public AgentState AgentState { get; init; } = AgentState ?? throw new ArgumentNullException(nameof(AgentState));

    /// <summary>The learner model before this utterance.</summary>
    public LearnerModel Learner { get; init; } = Learner ?? throw new ArgumentNullException(nameof(Learner));

    /// <summary>
    /// Repeat counts before this utterance, held in ordinal key order so serialization never depends on insertion order. The pipeline adds
    /// this utterance's tags itself.
    /// </summary>
    public IReadOnlyDictionary<string, int> RepeatCountsByTag { get; init; } = SortCounts(RepeatCountsByTag);

    /// <summary>The caller-supplied host state digest.</summary>
    public string HostStateHash { get; init; } = HostStateHash ?? throw new ArgumentNullException(nameof(HostStateHash));

    /// <summary>
    /// Host facts for this utterance's reply and feedback templates, or null when there are none (an empty map is stored as null). Strings only,
    /// like verdict slots, and rendered with the same formatters: <c>{target:article(def)}</c> with <c>target = árbol</c> gives <c>el árbol</c>.
    /// Where a name is also a verdict slot the verdict's value wins; the reserved feedback names <c>tag</c>, <c>expected</c>, <c>actual</c> and
    /// <c>recast</c> win over both. A template that names a host slot the host did not supply fails like one naming a missing verdict slot
    /// (the feedback falls back to the generic pool). Keys are held in ordinal order.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SlotMap? HostSlots
    {
        get => _hostSlots;
        init => _hostSlots = value is { Count: 0 } ? null : value;
    }

    /// <summary>
    /// Extra exercised skill ids for this utterance, ordinal-sorted and distinct, or null when there are none. The pipeline merges them with its
    /// own <c>intent.&lt;id&gt;</c> skill before its single <see cref="LearnerModel.Update"/> call, so error families are stepped once.
    /// Each id must be non-empty and contain no whitespace (<see cref="LearnerModel.ValidateSkillId"/>); setting an invalid id throws
    /// <see cref="ArgumentException"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? ExtraSkills
    {
        get => _extraSkills;
        init => _extraSkills = NormalizeSkills(value);
    }

    /// <summary>
    /// Builds a context whose <see cref="HostStateHash"/> is the digest of all the other values, including <paramref name="hostSlots"/> and
    /// <paramref name="extraSkills"/> when they are not empty.
    /// </summary>
    public static ConversationContext Create(
        ulong worldSeed,
        string agentId,
        AgentState agentState,
        StoredTimestamp now,
        LearnerModel learner,
        IReadOnlyDictionary<string, int>? repeatCountsByTag = null,
        SlotMap? hostSlots = null,
        IReadOnlyList<string>? extraSkills = null)
    {
        var counts = SortCounts(repeatCountsByTag ?? ImmutableSortedDictionary<string, int>.Empty);
        var slots = hostSlots is { Count: 0 } ? null : hostSlots;
        var skills = NormalizeSkills(extraSkills);
        var hash = slots is null && skills is null
            ? CanonicalJson.Hash(new StateSnapshot(worldSeed, agentId, agentState, now, learner, counts))
            : CanonicalJson.Hash(new HostStateSnapshot(worldSeed, agentId, agentState, now, learner, counts, slots ?? SlotMap.Empty, skills ?? []));
        return new ConversationContext(worldSeed, agentId, agentState, now, learner, counts, hash)
        {
            HostSlots = slots,
            ExtraSkills = skills,
        };
    }

    /// <summary>
    /// The context the next utterance starts from, after this one was evaluated (the context passed to <c>Evaluate</c>). The agent is the one
    /// this context names, in the state the decision moved it to (or unchanged when the bundle has no state machine for it); the learner model
    /// is the one after the update; the repeat counts are <see cref="RepeatCounts.Next"/> of this context's counts (the counts before this utterance)
    /// and its verdict, which are the counts before the next utterance; the world seed is kept. The host supplies <paramref name="now"/>, and
    /// <see cref="HostStateHash"/> is recomputed from the values, as <see cref="Create"/> does.
    /// The host keeps the states of the other agents itself: this context carries only the agent it spoke to.
    /// <see cref="HostSlots"/> and <see cref="ExtraSkills"/> are per-utterance facts and are not carried over: the next context has none, and the
    /// host sets them again (from <see cref="Create"/>) when the next utterance needs them.
    /// </summary>
    /// <param name="result">The result of evaluating one utterance in this context.</param>
    /// <param name="now">The stored timestamp of the next utterance (the host's clock, passed in).</param>
    public ConversationContext Next(PipelineResult result, StoredTimestamp now)
    {
        ArgumentNullException.ThrowIfNull(result);
        var agentState = result.Decision.Agent?.Next ?? AgentState;
        return Create(
            WorldSeed,
            AgentId,
            agentState,
            now,
            result.Decision.Learner,
            RepeatCounts.Next(RepeatCountsByTag, result.Verdict));
    }

    /// <summary>Compares every member; the repeat counts are compared as sorted maps.</summary>
    public bool Equals(ConversationContext? other)
    {
        if (other is null
            || WorldSeed != other.WorldSeed
            || !string.Equals(AgentId, other.AgentId, StringComparison.Ordinal)
            || !AgentState.Equals(other.AgentState)
            || !Now.Equals(other.Now)
            || !Learner.Equals(other.Learner)
            || !string.Equals(HostStateHash, other.HostStateHash, StringComparison.Ordinal)
            || !Equals(HostSlots, other.HostSlots)
            || !SkillsEqual(ExtraSkills, other.ExtraSkills)
            || RepeatCountsByTag.Count != other.RepeatCountsByTag.Count)
        {
            return false;
        }

        foreach (var pair in RepeatCountsByTag)
        {
            if (!other.RepeatCountsByTag.TryGetValue(pair.Key, out var count) || count != pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Hash consistent with <see cref="Equals(ConversationContext)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(WorldSeed);
        hash.Add(AgentId, StringComparer.Ordinal);
        hash.Add(AgentState);
        hash.Add(Now);
        hash.Add(Learner);
        hash.Add(HostStateHash, StringComparer.Ordinal);
        hash.Add(HostSlots);
        foreach (var skill in ExtraSkills ?? [])
        {
            hash.Add(skill, StringComparer.Ordinal);
        }

        foreach (var pair in RepeatCountsByTag)
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }

    private static bool SkillsEqual(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        return (left ?? []).SequenceEqual(right ?? [], StringComparer.Ordinal);
    }

    private static string[]? NormalizeSkills(IReadOnlyList<string>? skills)
    {
        if (skills is null)
        {
            return null;
        }

        var sorted = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var skill in skills)
        {
            sorted.Add(LearnerModel.ValidateSkillId(skill, nameof(ExtraSkills)));
        }

        return sorted.Count == 0 ? null : sorted.ToArray();
    }

    private static ImmutableSortedDictionary<string, int> SortCounts(IReadOnlyDictionary<string, int> source)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(RepeatCountsByTag));
        if (source is ImmutableSortedDictionary<string, int> sorted && ReferenceEquals(sorted.KeyComparer, StringComparer.Ordinal))
        {
            return sorted;
        }

        return ImmutableSortedDictionary.CreateRange(StringComparer.Ordinal, source);
    }

    private sealed record HostStateSnapshot(
        ulong WorldSeed,
        string AgentId,
        AgentState AgentState,
        StoredTimestamp Now,
        LearnerModel Learner,
        IReadOnlyDictionary<string, int> RepeatCountsByTag,
        SlotMap HostSlots,
        IReadOnlyList<string> ExtraSkills);

    private sealed record StateSnapshot(
        ulong WorldSeed,
        string AgentId,
        AgentState AgentState,
        StoredTimestamp Now,
        LearnerModel Learner,
        IReadOnlyDictionary<string, int> RepeatCountsByTag);
}
