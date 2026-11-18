namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// A crown's layers from the top down (B03): the first layer lies <see cref="TopOffset"/> blocks above the top of the trunk, and every layer has leaves in the columns whose squared distance from the
/// trunk is at most its limit. The valley's profiles, ported.
/// </summary>
/// <param name="TopOffset">How far above the trunk's top block the first layer lies.</param>
/// <param name="Limits">The squared distance, in columns, inside which each layer has leaves.</param>
public readonly record struct CrownProfile(int TopOffset, IReadOnlyList<int> Limits);

/// <summary>
/// The shape of a tree's crown, by name (B03): the code that decides where the leaves go, named by a biome row's tree entry. Implementations are immutable and safe to call from any thread.
/// </summary>
public interface ICrownShape
{
    /// <summary>The shape's name, as a biome row writes it.</summary>
    string Name { get; }

    /// <summary>The farthest, in columns, a leaf stands from the trunk (the pipeline reads the largest of these to know how far a chunk's neighbours reach into it).</summary>
    int Reach { get; }

    /// <summary>The highest a leaf stands over the trunk's top block.</summary>
    int Rise { get; }

    /// <summary>The layers of a crown on a trunk of the given length.</summary>
    /// <param name="trunkLength">The trunk's length in blocks.</param>
    /// <param name="shape">The tree's own random bits (a crown may vary with them).</param>
    CrownProfile Profile(int trunkLength, ulong shape);
}
