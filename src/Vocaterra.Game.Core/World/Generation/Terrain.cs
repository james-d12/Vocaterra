using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation;

/// <summary>
/// The shape of the ground as numbers: the top block of every column, the top of the water above it, and which columns belong to the route
/// (paths, level sites, the river's banks) where trees keep away. Made by <see cref="TerrainGenerator"/>; the <see cref="SceneBuilder"/> turns it
/// into blocks.
/// </summary>
internal sealed class Terrain
{
    /// <summary>The column is part of the route: trees keep a margin from it.</summary>
    public const byte RouteFlag = 1;

    /// <summary>The column is trodden path (its top block is <see cref="BlockId.Path"/>).</summary>
    public const byte PathFlag = 2;

    /// <summary>The column is on the bed or the bank of the river, the lake or the pool.</summary>
    public const byte ShoreFlag = 4;

    /// <summary>The column is river water (inside the river's half width): the river's reaches meet each other in <see cref="WaterAtRest"/>'s rapids.</summary>
    public const byte RiverWaterFlag = 8;

    /// <summary>Creates empty terrain of a size.</summary>
    public Terrain(int sizeX, int sizeZ)
    {
        SizeX = sizeX;
        SizeZ = sizeZ;
        Height = new int[sizeX * sizeZ];
        WaterTop = new int[sizeX * sizeZ];
        Flags = new byte[sizeX * sizeZ];
    }

    /// <summary>The width in columns.</summary>
    public int SizeX { get; }

    /// <summary>The depth in columns.</summary>
    public int SizeZ { get; }

    /// <summary>The y of the top solid block of each column, indexed <c>z * SizeX + x</c>.</summary>
    public int[] Height { get; }

    /// <summary>The y of the top water block of each column, or -1 when the column holds no water above its ground.</summary>
    public int[] WaterTop { get; }

    /// <summary>The columns of falling water: the cell at <c>Bottom</c> up to the one under the top fall, the top cell is a source (see <see cref="WaterAtRest"/>).</summary>
    public List<FallingColumn> Falls { get; } = [];

    /// <summary>The <see cref="RouteFlag"/>, <see cref="PathFlag"/> and <see cref="ShoreFlag"/> bits of each column.</summary>
    public byte[] Flags { get; }

    /// <summary>Whether the column holds water above its ground.</summary>
    public bool IsWater(int x, int z) => WaterTop[(z * SizeX) + x] > Height[(z * SizeX) + x];
}


/// <summary>A column of falling water: from <paramref name="Bottom"/> (the pool's surface layer) up to <paramref name="Top"/>, whose cell is a source.</summary>
internal readonly record struct FallingColumn(int X, int Z, int Bottom, int Top);
