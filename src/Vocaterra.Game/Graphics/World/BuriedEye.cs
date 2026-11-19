namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// Whether the eye is buried: deep under the lowest surface of the land in the chunk columns round it (<see cref="Reach"/> of them on each side), in a cave or a mine, where no way out to the far land is near. The far pass is not drawn then
/// (the owner's report of 2026-11-19: in a cave 120 blocks down the far view still drew 16 km of land behind the rock). A cave's mouth in a hillside is not buried: the land below the mouth is lower than the eye. A shaft open to the sky
/// counts at its bottom (<see cref="SkySurface.Lowest"/>), so its foot is not buried either. The eye goes in at <see cref="Below"/> blocks under and comes out at <see cref="Rise"/>, so a step at the line does not switch it back and forth.
/// </summary>
public static class BuriedEye
{
    /// <summary>The chunk columns on each side of the eye's that are looked at (4: 64 blocks round it).</summary>
    public const int Reach = 4;

    /// <summary>How many blocks under the lowest surface round it the eye is buried.</summary>
    public const int Below = 16;

    /// <summary>How many blocks under it a buried eye comes out again (fewer than <see cref="Below"/>).</summary>
    public const int Rise = 10;

    /// <summary>Whether the eye is buried now.</summary>
    /// <param name="wasBuried">Whether it was in the last frame.</param>
    /// <param name="eyeY">The block the eye is in, y.</param>
    /// <param name="lowestSurface">The lowest surface of the columns round it.</param>
    public static bool Next(bool wasBuried, int eyeY, int lowestSurface)
    {
        var depth = (long)lowestSurface - eyeY;
        return depth >= (wasBuried ? Rise : Below);
    }
}
