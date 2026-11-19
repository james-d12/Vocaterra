namespace Vocaterra.Game.Core.Water;

/// <summary>
/// The fog with the eye in lava, and the rule that blends the fog round it towards it by how far in the view is (the eased amount of <c>UnderwaterView</c> for lava): the colour, the start and the end each move in a straight line, as
/// <see cref="UnderwaterFog"/> does for water. Lava is an opaque cube, so the faces between it and the rock round it are never made: with the eye inside it nothing near is drawn and the view went straight through the rock to the caves and
/// the undersides of the land above (the owner's report of 2026-11-19). The fog ends within the lava block, so nothing past it shows.
/// </summary>
public static class LavaFog
{
    /// <summary>The glowing orange of the lava, red channel (0 to 1); the screen's lava tint is the same colour.</summary>
    public const float LavaRed = 0.85f;

    /// <summary>The glowing orange of the lava, green channel (0 to 1).</summary>
    public const float LavaGreen = 0.25f;

    /// <summary>The glowing orange of the lava, blue channel (0 to 1).</summary>
    public const float LavaBlue = 0.03f;

    /// <summary>Where the fog in lava starts, in blocks.</summary>
    public const float LavaStart = 0f;

    /// <summary>Where the fog in lava is complete, in blocks: less than the block the eye is in is wide, so no face beyond it shows.</summary>
    public const float LavaEnd = 0.9f;

    /// <summary>The fog of the lava itself (the view fully in).</summary>
    public static FogParameters Lava { get; } = new(LavaRed, LavaGreen, LavaBlue, LavaStart, LavaEnd);

    /// <summary>
    /// The fog for a view <paramref name="amount"/> of the way into lava: <paramref name="outside"/> at 0, <see cref="Lava"/> at 1, a straight blend between. An amount outside 0 to 1 is held to it.
    /// </summary>
    /// <param name="outside">The fog round the eye without the lava (the dry fog, or the water's).</param>
    /// <param name="amount">How far in, 0 to 1.</param>
    public static FogParameters Blend(FogParameters outside, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        if (t <= 0f)
        {
            return outside;
        }

        if (t >= 1f)
        {
            return Lava;
        }

        return new FogParameters(
            Lerp(outside.Red, LavaRed, t),
            Lerp(outside.Green, LavaGreen, t),
            Lerp(outside.Blue, LavaBlue, t),
            Lerp(outside.Start, LavaStart, t),
            Lerp(outside.End, LavaEnd, t));
    }

    private static float Lerp(float from, float to, float t) => from + ((to - from) * t);
}
