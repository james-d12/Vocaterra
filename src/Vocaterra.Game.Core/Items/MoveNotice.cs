namespace Vocaterra.Game.Core.Items;

/// <summary>The last refusal the inventory panel has to show (I04): the key of its line (a HUD line of <c>words.json</c> and a string of the locale, <see cref="Key"/>) and the tick it happened in. The panel shows it for a few seconds of ticks, so no clock is read.</summary>
/// <param name="Key">The key from <see cref="Containers.MoveRefusalText"/>.</param>
/// <param name="Tick">The simulation's tick when the move was refused.</param>
public readonly record struct MoveNotice(string Key, long Tick)
{
    /// <summary>How long a refusal stays on the panel, in ticks (three seconds).</summary>
    public const int ShownTicks = 180;

    /// <summary>Whether the notice is still to be shown at <paramref name="tick"/>.</summary>
    /// <param name="tick">The simulation's tick now.</param>
    public bool IsShown(long tick) => tick >= Tick && tick - Tick < ShownTicks;
}
