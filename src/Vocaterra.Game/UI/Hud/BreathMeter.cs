using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.UI.Hud;

/// <summary>What the bubble meter reads each frame: the air left and whether the eye is under water.</summary>
/// <param name="Air">The air left, in ticks (0 to <see cref="Breath.MaxAir"/>).</param>
/// <param name="Submerged">Whether the eye is under the water's surface.</param>
public readonly record struct BreathReading(int Air, bool Submerged)
{
    /// <summary>A full breath, on land.</summary>
    public static BreathReading Full => new(Breath.MaxAir, false);

    /// <summary>The bubbles shown: one goes with each tenth of the air, so 10 while the air is full and 0 only at no air (the count of <see cref="Breath.Bubbles"/>).</summary>
    public int Bubbles => ((Air * Breath.MaxBubbles) + Breath.MaxAir - 1) / Breath.MaxAir;

    /// <summary>Reads the simulation's breath and whether its eye is under water.</summary>
    /// <param name="sim">The simulation being shown.</param>
    public static BreathReading Of(VoxelSimulation sim)
    {
        ArgumentNullException.ThrowIfNull(sim);
        return new BreathReading(sim.Breath.Air, Immersion.Of(sim.Grid, sim.Player).Eye);
    }
}

/// <summary>One frame of the meter: the whole meter's opacity and each bubble's size (1 full, between 0 and 1 while it pops, 0 when it is empty).</summary>
/// <param name="Alpha">The opacity of the meter, 0 (hidden) to 1.</param>
/// <param name="Sizes">Each bubble's size, left to right.</param>
public readonly record struct BreathMeterFrame(float Alpha, IReadOnlyList<float> Sizes);

/// <summary>
/// The bubble meter's state (H04), pure: which bubbles are full, which are popping, and whether the meter shows. It shows while the eye is under water or the
/// air is below full, and it stays one second (<see cref="FadeDelayTicks"/>) after the air refills, then fades out over <see cref="FadeTicks"/>. A bubble
/// that goes pops over <see cref="PopFrames"/> frames of drawing, shrinking. Pops count frames of drawing, the fade counts simulation ticks, so a paused
/// game holds the fade. The layer only draws what this returns.
/// </summary>
public sealed class BreathMeter
{
    /// <summary>The frames a popping bubble takes to shrink away.</summary>
    public const int PopFrames = 3;

    /// <summary>The ticks the meter stays fully shown after the air refills (one second).</summary>
    public const int FadeDelayTicks = VoxelSimulation.TicksPerSecond;

    /// <summary>The ticks the meter takes to fade out once the delay has passed.</summary>
    public const int FadeTicks = 20;

    private readonly int[] _popAge = new int[Breath.MaxBubbles];
    private int? _bubbles;
    private bool _wasBelowFull;
    private long? _refilledAt;

    /// <summary>Creates a meter with every bubble full and nothing popping.</summary>
    public BreathMeter()
    {
        Array.Fill(_popAge, -1);
    }

    /// <summary>Advances the meter one frame.</summary>
    /// <param name="reading">The breath this frame.</param>
    /// <param name="tick">The simulation's tick this frame (counts the fade).</param>
    public BreathMeterFrame Update(BreathReading reading, long tick)
    {
        var bubbles = reading.Bubbles;
        var below = reading.Air < Breath.MaxAir;

        // The first reading is taken as it is (a loaded game does not pop its bubbles); after it, each bubble lost pops and each bubble back is full.
        if (_bubbles is { } previous)
        {
            for (var i = bubbles; i < previous; i++)
            {
                _popAge[i] = 0;
            }

            for (var i = previous; i < bubbles; i++)
            {
                _popAge[i] = -1;
            }
        }

        _bubbles = bubbles;

        if (below)
        {
            _refilledAt = null;
        }
        else if (_wasBelowFull)
        {
            _refilledAt = tick;
        }

        _wasBelowFull = below;

        var sizes = new float[Breath.MaxBubbles];
        for (var i = 0; i < sizes.Length; i++)
        {
            if (i < bubbles)
            {
                sizes[i] = 1f;
            }
            else if (_popAge[i] >= 0)
            {
                sizes[i] = 1f - ((float)_popAge[i] / PopFrames);
            }
        }

        for (var i = bubbles; i < _popAge.Length; i++)
        {
            if (_popAge[i] >= 0 && ++_popAge[i] >= PopFrames)
            {
                _popAge[i] = -1;
            }
        }

        return new BreathMeterFrame(Opacity(reading, below, tick), sizes);
    }

    private float Opacity(BreathReading reading, bool below, long tick)
    {
        if (reading.Submerged || below)
        {
            return 1f;
        }

        if (_refilledAt is not { } refilled)
        {
            return 0f;
        }

        var since = tick - refilled;
        if (since <= FadeDelayTicks)
        {
            return 1f;
        }

        return Math.Clamp(1f - ((since - FadeDelayTicks) / (float)FadeTicks), 0f, 1f);
    }
}
