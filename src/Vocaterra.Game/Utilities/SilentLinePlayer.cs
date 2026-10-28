using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Utilities;

/// <summary>The line player of S7: it plays nothing (the panel highlights the line instead).</summary>
public sealed class SilentLinePlayer : ILinePlayer
{
    /// <summary>The one instance.</summary>
    public static SilentLinePlayer Instance { get; } = new();

    /// <inheritdoc />
    public void Play(string npc, string line, bool slow)
    {
    }
}
