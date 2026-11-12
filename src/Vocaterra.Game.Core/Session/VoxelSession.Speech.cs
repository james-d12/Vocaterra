using Vocaterra.Game.Core.Language.Speech;

namespace Vocaterra.Game.Core.Session;

// A line the world has an NPC say on its own (Y07): the guide sending the player to the inn when the camp checklist is done. The session queues it with its hint; the front-end plays it.
public sealed partial class VoxelSession
{
    /// <summary>The dialogue pool of the guide's line that sends the player to the inn when the camp checklist is done (Y07).</summary>
    public const string InnLinePool = "guide.camp.inn";

    private readonly List<SpokenLine> _spoken = [];

    /// <summary>The lines the world had an NPC say since the last call, in order (the front-end plays each in its speaker's voice and prints it), and clears them.</summary>
    public IReadOnlyList<SpokenLine> TakeSpoken()
    {
        var lines = _spoken.ToArray();
        _spoken.Clear();
        return lines;
    }

    // The guide says the first line of a pool: it is queued for the voice and shown as a hint with its English beside it.
    private void GuideSays(string poolId)
    {
        if (!Bundle.Dialogue.TryGetPoolById(poolId, out var pool) || pool.Variants.Count == 0)
        {
            return;
        }

        var variant = pool.Variants[0];
        var english = Locale.Line(variant.LineId);
        _spoken.Add(new SpokenLine(SpeechSpeakers.OfPool(poolId, Data.Npcs), variant.Text, english));
        _hints.Add(english is null ? $"“{variant.Text}”" : $"“{variant.Text}” ({english})");
    }
}

/// <summary>A line an NPC says on the world's own account (Y07), not in a conversation.</summary>
/// <param name="Speaker">The NPC's agent id (the voice).</param>
/// <param name="Spanish">The Spanish text.</param>
/// <param name="English">The English gloss, or <see langword="null"/> when the locale has none.</param>
public sealed record SpokenLine(string Speaker, string Spanish, string? English);
