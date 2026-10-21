using Vocaterra.Simulation.Tiles;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game;

/// <summary>
/// A position in the world the guide talks about. In the 2D slice it is a tile (<see cref="X"/> the column, <see cref="Y"/> the row, <see cref="Z"/>
/// unused); in the 3D scene it is a block (x east, y up, z south). The guide never reads the axes itself: it passes cells between the facts
/// (<see cref="IGuideFacts"/>) and the front-end, which puts the beacon or the arrow on them.
/// </summary>
/// <param name="X">The column (2D) or the x of the block (3D).</param>
/// <param name="Y">The row (2D) or the y of the block (3D).</param>
/// <param name="Z">The z of the block (3D); zero for a tile.</param>
public readonly record struct GuideCell(int X, int Y, int Z)
{
    /// <summary>A tile of the 2D slice as a cell.</summary>
    public static implicit operator GuideCell(TilePoint tile) => new(tile.X, tile.Y, 0);

    /// <summary>A block of the 3D scene as a cell.</summary>
    public static implicit operator GuideCell(BlockPos block) => new(block.X, block.Y, block.Z);

    /// <summary>The tile this cell is, for the 2D slice (the Z is dropped).</summary>
    public TilePoint ToTile() => new(X, Y);

    /// <summary>The block this cell is, for the 3D scene.</summary>
    public BlockPos ToBlock() => new(X, Y, Z);
}

/// <summary>
/// Where the player stands, as the guide's facts need it. The 2D slice needs only <see cref="Cell"/> (the tile under the middle of the body).
/// The 3D world needs the exact centre of the body in 1/4096-block units (<see cref="CentreX"/>, <see cref="CentreZ"/>, the middle of the feet)
/// and the facing (<see cref="Yaw"/>, angle steps: 0 north, 1024 east), so the direction the guide gives is relative to where the player looks.
/// <see cref="Cell"/> is the block the centre lies in.
/// </summary>
/// <param name="Cell">The tile or block under the middle of the player's body.</param>
/// <param name="CentreX">The centre of the body on x in 1/4096 block units (3D only).</param>
/// <param name="CentreZ">The centre of the body on z in 1/4096 block units (3D only).</param>
/// <param name="Yaw">The facing in angle steps (3D only); 0 is north.</param>
public readonly record struct GuideViewpoint(GuideCell Cell, long CentreX = 0, long CentreZ = 0, int Yaw = 0)
{
    /// <summary>A tile of the 2D slice as a viewpoint.</summary>
    public static implicit operator GuideViewpoint(TilePoint tile) => new(tile);

    /// <summary>A cell as a viewpoint (2D; the 3D world also sets the centre and the facing).</summary>
    public static implicit operator GuideViewpoint(GuideCell cell) => new(cell);
}

/// <summary>
/// The nearest place of a catalogue noun from a viewpoint: where it is, how many blocks away and in which direction the player has to go, in
/// Spanish. <see cref="Blocks"/> is the distance the guide says ("a ocho bloques"); <see cref="Distance2"/> is the squared distance the nearest
/// choice compares, in the facts' own units.
/// </summary>
/// <param name="Cell">The tile or block found.</param>
/// <param name="Blocks">The distance in blocks, rounded.</param>
/// <param name="Direction">The direction as a Spanish phrase (<c>a la derecha</c>, <c>adelante, a la izquierda</c>); empty when it is here.</param>
/// <param name="Distance2">The squared distance the nearest choice used (tiles: the squared straight-line distance; blocks: the squared horizontal distance in units).</param>
public sealed record GuideNearest(GuideCell Cell, int Blocks, string Direction, long Distance2)
{
    /// <summary>The distance in blocks spelled in Spanish, for example <c>ocho</c>.</summary>
    public string BlocksInWords => WorldFacts.NumberInWords(Blocks);
}

/// <summary>
/// What the guide can say about the world: the nearest place of a noun, and the noun of the thing at a cell. The 2D slice implements it with
/// <see cref="WorldFacts"/> (tiles) and the 3D scene with <see cref="WorldFacts3D"/> (blocks), so <see cref="GuideService"/> answers the same
/// way in both until the 2D slice is retired (S4).
/// </summary>
public interface IGuideFacts
{
    /// <summary>The nearest place of a catalogue noun from a viewpoint, or <see langword="null"/> when the world has none of it.</summary>
    /// <param name="from">Where the player is.</param>
    /// <param name="lemma">The catalogue noun (<c>árbol</c>).</param>
    GuideNearest? NearestOfNoun(GuideViewpoint from, string lemma);

    /// <summary>The catalogue nouns that name the thing at a cell (the water names <c>agua</c> and <c>río</c>), in lemma order; empty for air.</summary>
    /// <param name="cell">The tile or block.</param>
    IReadOnlyList<string> NounsAt(GuideCell cell);
}
