namespace Vocaterra.Game.Core.Water;

/// <summary>
/// The player's air, in ticks (water-plan H03). Under water (the eye below the surface) it falls by one a tick; in the open it refills in about two
/// seconds. It only counts and reports (<see cref="BreathEvents"/>): what running out costs is decided elsewhere (<see cref="DrowningDamage"/>).
/// Pure and deterministic: it reads no clock.
/// </summary>
public sealed class Breath
{
    /// <summary>The air of a full breath: 15 seconds at 60 ticks a second.</summary>
    public const int MaxAir = 900;

    /// <summary>The air gained a tick in the open (a full breath in 113 ticks, about 1.9 seconds).</summary>
    public const int RefillPerTick = 8;

    /// <summary>The ticks between two <see cref="BreathEvents.OutOfAir"/> reports while the air stays at zero (one second).</summary>
    public const int OutOfAirIntervalTicks = 60;

    /// <summary>The most bubbles the HUD shows.</summary>
    public const int MaxBubbles = 10;

    private int _ticksAtZero;

    /// <summary>The air left, in ticks, 0 to <see cref="MaxAir"/>.</summary>
    public int Air { get; private set; } = MaxAir;

    /// <summary>The HUD's bubbles, 0 to 10: one goes with each tenth of the air, so it is 10 while the air is full and 0 only at no air.</summary>
    public int Bubbles => ((Air * MaxBubbles) + MaxAir - 1) / MaxAir;

    /// <summary>Whether the air is full.</summary>
    public bool IsFull => Air == MaxAir;

    /// <summary>Whether the air has run out.</summary>
    public bool IsEmpty => Air == 0;

    /// <summary>Advances one tick.</summary>
    /// <param name="eyeInWater">Whether the eye is under the water's surface.</param>
    /// <returns>What happened in this tick.</returns>
    internal BreathEvents Step(bool eyeInWater)
    {
        if (eyeInWater)
        {
            if (Air > 0)
            {
                Air--;
                if (Air == 0)
                {
                    _ticksAtZero = 0;
                    return BreathEvents.OutOfAir;
                }

                return BreathEvents.None;
            }

            _ticksAtZero++;
            return _ticksAtZero % OutOfAirIntervalTicks == 0 ? BreathEvents.OutOfAir : BreathEvents.None;
        }

        _ticksAtZero = 0;
        if (Air == MaxAir)
        {
            return BreathEvents.None;
        }

        Air = Math.Min(MaxAir, Air + RefillPerTick);
        return Air == MaxAir ? BreathEvents.Restored : BreathEvents.None;
    }

    /// <summary>Puts back a saved amount of air (clamped to 0 to <see cref="MaxAir"/>); the out-of-air cadence starts again.</summary>
    internal void Restore(int air)
    {
        Air = Math.Clamp(air, 0, MaxAir);
        _ticksAtZero = 0;
    }
}
