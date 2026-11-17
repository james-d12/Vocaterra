namespace Vocaterra.Game.Core.World.Generation;

/// <summary>
/// The version of the procedural world generator (B05). The world's state digest carries it, so a change to the algorithm that makes the same seed
/// and data generate different chunks moves every digest and refuses every older save. Bump it with any such change. 3: B11b's caves and rocks; 4: the rivers and lakes are the drainage network's (B02c).
/// </summary>
public static class ProceduralGenerator
{
    /// <summary>The generator's version.</summary>
    public const int Version = 4;
}
