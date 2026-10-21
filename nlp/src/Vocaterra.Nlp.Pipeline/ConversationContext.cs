using System.Collections.Immutable;
using Vocaterra.Nlp.Core.Determinism;
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
public sealed record ConversationContext(
    ulong WorldSeed,
    string AgentId,
    AgentState AgentState,
    StoredTimestamp Now,
    LearnerModel Learner,
    IReadOnlyDictionary<string, int> RepeatCountsByTag,
    string HostStateHash)
{
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

    /// <summary>Builds a context whose <see cref="HostStateHash"/> is the digest of all the other values.</summary>
    public static ConversationContext Create(
        ulong worldSeed,
        string agentId,
        AgentState agentState,
        StoredTimestamp now,
        LearnerModel learner,
        IReadOnlyDictionary<string, int>? repeatCountsByTag = null)
    {
        var counts = SortCounts(repeatCountsByTag ?? ImmutableSortedDictionary<string, int>.Empty);
        var hash = CanonicalJson.Hash(new StateSnapshot(worldSeed, agentId, agentState, now, learner, counts));
        return new ConversationContext(worldSeed, agentId, agentState, now, learner, counts, hash);
    }

    /// <summary>
    /// The context the next utterance starts from, after this one was evaluated (the context passed to <c>Evaluate</c>). The agent is the one
    /// this context names, in the state the decision moved it to (or unchanged when the bundle has no state machine for it); the learner model
    /// is the one after the update; the repeat counts are <see cref="RepeatCounts.Next"/> of this context's counts (the counts before this utterance)
    /// and its verdict, which are the counts before the next utterance; the world seed is kept. The host supplies <paramref name="now"/>, and
    /// <see cref="HostStateHash"/> is recomputed from the values, as <see cref="Create"/> does.
    /// The host keeps the states of the other agents itself: this context carries only the agent it spoke to.
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
        foreach (var pair in RepeatCountsByTag)
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
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

    private sealed record StateSnapshot(
        ulong WorldSeed,
        string AgentId,
        AgentState AgentState,
        StoredTimestamp Now,
        LearnerModel Learner,
        IReadOnlyDictionary<string, int> RepeatCountsByTag);
}
