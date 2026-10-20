using System.Collections.Immutable;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Decision.Npc;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>
/// The game-side state one utterance is evaluated in: the world seed, the NPC being talked to and its state, the stored time,
/// the learner model and how often each error tag was repeated. Nothing in it is read from a clock; the caller supplies every value.
/// </summary>
/// <param name="WorldSeed">The save's world seed; with the NPC id and interaction counter it seeds every random draw.</param>
/// <param name="NpcId">The NPC being talked to; when the bundle has no state machine for it the decision layer skips the NPC step.</param>
/// <param name="NpcState">The NPC's persisted state; its interaction counter selects the random stream.</param>
/// <param name="Now">The stored timestamp of the interaction.</param>
/// <param name="Learner">The learner model before this utterance.</param>
/// <param name="RepeatCountsByTag">How many times each error tag (and <c>not_communicated</c>) has been seen in a row, including this utterance.</param>
/// <param name="GameStateHash">A caller-supplied digest of the rest of the game state, copied into the replay record.</param>
public sealed record GameContext(
    ulong WorldSeed,
    string NpcId,
    NpcState NpcState,
    StoredTimestamp Now,
    LearnerModel Learner,
    IReadOnlyDictionary<string, int> RepeatCountsByTag,
    string GameStateHash)
{
    /// <summary>The NPC being talked to.</summary>
    public string NpcId { get; init; } = NpcId ?? throw new ArgumentNullException(nameof(NpcId));

    /// <summary>The NPC's persisted state.</summary>
    public NpcState NpcState { get; init; } = NpcState ?? throw new ArgumentNullException(nameof(NpcState));

    /// <summary>The learner model before this utterance.</summary>
    public LearnerModel Learner { get; init; } = Learner ?? throw new ArgumentNullException(nameof(Learner));

    /// <summary>Repeat counts, held in ordinal key order so serialization never depends on insertion order.</summary>
    public IReadOnlyDictionary<string, int> RepeatCountsByTag { get; init; } = SortCounts(RepeatCountsByTag);

    /// <summary>The caller-supplied game state digest.</summary>
    public string GameStateHash { get; init; } = GameStateHash ?? throw new ArgumentNullException(nameof(GameStateHash));

    /// <summary>Builds a context whose <see cref="GameStateHash"/> is the digest of all the other values.</summary>
    public static GameContext Create(
        ulong worldSeed,
        string npcId,
        NpcState npcState,
        StoredTimestamp now,
        LearnerModel learner,
        IReadOnlyDictionary<string, int>? repeatCountsByTag = null)
    {
        var counts = SortCounts(repeatCountsByTag ?? ImmutableSortedDictionary<string, int>.Empty);
        var hash = CanonicalJson.Hash(new StateSnapshot(worldSeed, npcId, npcState, now, learner, counts));
        return new GameContext(worldSeed, npcId, npcState, now, learner, counts, hash);
    }

    /// <summary>Compares every member; the repeat counts are compared as sorted maps.</summary>
    public bool Equals(GameContext? other)
    {
        if (other is null
            || WorldSeed != other.WorldSeed
            || !string.Equals(NpcId, other.NpcId, StringComparison.Ordinal)
            || !NpcState.Equals(other.NpcState)
            || !Now.Equals(other.Now)
            || !Learner.Equals(other.Learner)
            || !string.Equals(GameStateHash, other.GameStateHash, StringComparison.Ordinal)
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

    /// <summary>Hash consistent with <see cref="Equals(GameContext)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(WorldSeed);
        hash.Add(NpcId, StringComparer.Ordinal);
        hash.Add(NpcState);
        hash.Add(Now);
        hash.Add(Learner);
        hash.Add(GameStateHash, StringComparer.Ordinal);
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
        string NpcId,
        NpcState NpcState,
        StoredTimestamp Now,
        LearnerModel Learner,
        IReadOnlyDictionary<string, int> RepeatCountsByTag);
}
