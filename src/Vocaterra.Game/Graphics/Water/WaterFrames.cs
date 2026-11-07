namespace Vocaterra.Game.Graphics.Water;

/// <summary>
/// Which of the water's animation frames shows at a simulation tick (water-plan 4.8): the 16 frames loop, each held for a few ticks. A pure function of the
/// tick, so a scripted run and a live one show the same frame at the same tick and nothing reads the clock.
/// </summary>
public static class WaterFrames
{
    /// <summary>The frames in the loop.</summary>
    public const int Count = 16;

    /// <summary>The simulation ticks a frame is held (60 ticks a second, so 12 frames a second and a loop of about 1.3 seconds).</summary>
    public const int TicksPerFrame = 5;

    /// <summary>The frame, 0 to <see cref="Count"/> - 1, at the tick. A tick before the start wraps round the loop the same way.</summary>
    public static int IndexAt(long tick)
    {
        var held = tick >= 0 ? tick / TicksPerFrame : -((-tick + TicksPerFrame - 1) / TicksPerFrame); // rounded down, also before the start
        var frame = held % Count;
        return (int)(frame < 0 ? frame + Count : frame);
    }
}
