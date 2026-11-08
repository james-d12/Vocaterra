using Vocaterra.Game.Core.Physics;

namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// The splash of a fast entry into water (H10): the feet go into water from above, falling at least <see cref="SplashBlocksPerSecond"/>. A wade in (no fall),
/// a swim, and a step off a one-block ledge (about 7.9 blocks a second) make no splash. Pure: it reads the feet each tick and nothing else.
/// </summary>
/// <remarks>
/// The threshold follows the landing rule (<see cref="Footsteps.HardFallBlocks"/>, 1.5 blocks). A body falling 1.5 blocks from rest reaches sqrt(2 g h), about
/// 9.7 blocks a second with the game's gravity, so a fall that counts as a hard landing splashes, and a jump back to the water's level (about 9.4) does too.
/// </remarks>
public sealed class WaterEntry
{
    /// <summary>The fall speed that splashes, in blocks a second.</summary>
    public const double SplashBlocksPerSecond = 9.0;

    private bool _hasLast;
    private bool _wasInWater;
    private double _lastFeetY;

    /// <summary>Reads the player's feet this tick; true when they went into the water this tick at a splash's speed.</summary>
    /// <param name="feet">The footstate of the tick.</param>
    public bool Observe(FootState feet)
    {
        var splash = _hasLast && feet.InWater && !_wasInWater
            && (_lastFeetY - feet.FeetY) * VoxelPhysics.TicksPerSecond >= SplashBlocksPerSecond; // the height fell this tick: blocks a tick, times ticks a second
        _hasLast = true;
        _wasInWater = feet.InWater;
        _lastFeetY = feet.FeetY;
        return splash;
    }
}
