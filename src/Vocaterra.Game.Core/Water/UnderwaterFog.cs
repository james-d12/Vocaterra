namespace Vocaterra.Game.Core.Water;

/// <summary>
/// The fog under water, and the rule that blends the fog above water towards it by how far under the view is (<see cref="UnderwaterView.Amount"/>):
/// the colour, the start and the end each move in a straight line, so at 0 the numbers are exactly the dry ones and at 1 exactly the water's.
/// </summary>
public static class UnderwaterFog
{
    /// <summary>The murky blue-green of the water, red channel (0 to 1).</summary>
    public const float WaterRed = 0.09f;

    /// <summary>The murky blue-green of the water, green channel (0 to 1).</summary>
    public const float WaterGreen = 0.33f;

    /// <summary>The murky blue-green of the water, blue channel (0 to 1).</summary>
    public const float WaterBlue = 0.40f;

    /// <summary>Where the fog under water is complete, in blocks: the far lake bed fades into murk.</summary>
    public const float WaterEnd = 14f;

    /// <summary>Where the fog under water starts, in blocks.</summary>
    public const float WaterStart = 0f;

    /// <summary>The fog of the water itself (the view fully under).</summary>
    public static FogParameters Water { get; } = new(WaterRed, WaterGreen, WaterBlue, WaterStart, WaterEnd);

    /// <summary>
    /// The fog for a view <paramref name="amount"/> of the way under water: <paramref name="dry"/> at 0, <see cref="Water"/> at 1, a straight blend between.
    /// An amount outside 0 to 1 is held to it.
    /// </summary>
    /// <param name="dry">The fog above water (its colour, start and end).</param>
    /// <param name="amount">How far under, 0 to 1.</param>
    public static FogParameters Blend(FogParameters dry, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        if (t <= 0f)
        {
            return dry;
        }

        var wet = Water;
        if (t >= 1f)
        {
            return wet;
        }

        return new FogParameters(
            Lerp(dry.Red, wet.Red, t),
            Lerp(dry.Green, wet.Green, t),
            Lerp(dry.Blue, wet.Blue, t),
            Lerp(dry.Start, wet.Start, t),
            Lerp(dry.End, wet.End, t));
    }

    private static float Lerp(float from, float to, float t) => from + ((to - from) * t);
}
