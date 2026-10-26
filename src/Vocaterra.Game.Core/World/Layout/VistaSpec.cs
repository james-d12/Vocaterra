namespace Vocaterra.Game.Core.World.Layout;

/// <summary>
/// A sight cone (S14), for a lookout such as the player's ledge: from the site's eye (the site's height plus the player's eye) the trees inside the cone
/// (<paramref name="HalfAngleDegrees"/> either side of <paramref name="YawDegrees"/>, out to <paramref name="Radius"/> blocks) must stand lower than the
/// line that falls <paramref name="DownDegrees"/> degrees below the horizontal, so no crown hides the valley floor from the lookout.
/// </summary>
/// <param name="Site">The lookout site.</param>
/// <param name="YawDegrees">The way the cone points, as a compass bearing in degrees.</param>
/// <param name="HalfAngleDegrees">Half the cone's width in degrees.</param>
/// <param name="Radius">How far the cone reaches in blocks.</param>
/// <param name="DownDegrees">The depression of the sight line the tree tops must stay under.</param>
public sealed record VistaSpec(string Site, int YawDegrees, int HalfAngleDegrees, int Radius, int DownDegrees);

