namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>A cave entrance as the tools and the pictures see it (B11b): a winding natural passage from the mouth at the surface down into a cave.</summary>
/// <param name="X">The mouth's x.</param>
/// <param name="Z">The mouth's z.</param>
/// <param name="MouthY">The y of the top solid block at the mouth.</param>
/// <param name="Length">The cells along the path from the mouth to where the passage meets its cave.</param>
/// <param name="Landing">The cells after that.</param>
/// <param name="Depth">How far under the mouth the passage meets its cave.</param>
/// <param name="Cover">How far the ground lies over the passage's floor twelve cells in: about 6 on level ground, more where the passage goes into a rising hillside.</param>
/// <param name="Path">The centre cells of the path, mouth first.</param>
/// <param name="Floors">The y of the floor block at each cell of the path.</param>
public sealed record CaveEntrance(int X, int Z, int MouthY, int Length, int Landing, int Depth, int Cover, IReadOnlyList<(int X, int Z)> Path, IReadOnlyList<int> Floors)
{
    /// <summary>The centre column of the passage at an index along its path (clamped to its ends).</summary>
    /// <param name="along">The index along.</param>
    public (int X, int Z) CentreAt(int along) => Path[Math.Clamp(along, 0, Path.Count - 1)];
}
