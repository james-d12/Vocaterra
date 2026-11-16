namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>A cave entrance as the tools and the pictures see it (B11b): a straight stair from the mouth at the surface down into a cave.</summary>
/// <param name="X">The mouth's x.</param>
/// <param name="Z">The mouth's z.</param>
/// <param name="DirX">The direction's x (-1, 0 or 1).</param>
/// <param name="DirZ">The direction's z (-1, 0 or 1).</param>
/// <param name="MouthY">The y of the top solid block at the mouth.</param>
/// <param name="Length">The blocks along from the mouth to where the passage meets its cave.</param>
/// <param name="Landing">The level blocks after that.</param>
/// <param name="Width">The passage's width.</param>
/// <param name="Run">The blocks along for each block down.</param>
/// <param name="Cover">How far the ground lies over the passage's floor twelve blocks in: about 6 on level ground, more where the passage goes into a rising hillside.</param>
public sealed record CaveEntrance(int X, int Z, int DirX, int DirZ, int MouthY, int Length, int Landing, int Width, int Run, int Cover)
{
    /// <summary>The centre column of the passage at an index along (0 is the mouth).</summary>
    /// <param name="along">The index along.</param>
    public (int X, int Z) CentreAt(int along) => (X + (DirX * along), Z + (DirZ * along));

    /// <summary>The floor's y (the top solid block) at an index along: one lower every <see cref="Run"/> blocks, level on the landing.</summary>
    /// <param name="along">The index along.</param>
    public int FloorAt(int along) => MouthY - (Math.Clamp(along, 0, Length) / Run);
}
