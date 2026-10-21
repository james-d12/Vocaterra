namespace Vocaterra.Simulation.Tiles;

/// <summary>
/// The foreground tile kinds. The numeric values are stored in chunks, hashed into the world digest and written to saves, so a value is
/// never changed or reused: new kinds are appended.
/// </summary>
public enum TileId : ushort
{
    /// <summary>Empty space.</summary>
    Air = 0,

    /// <summary>Soil, the layer under the grass.</summary>
    Dirt = 1,

    /// <summary>Dirt with air above it.</summary>
    Grass = 2,

    /// <summary>Rock, the deep layer.</summary>
    Stone = 3,

    /// <summary>The wood of a tree (not solid: the player walks in front of it).</summary>
    Trunk = 4,

    /// <summary>The canopy of a tree (not solid).</summary>
    Leaves = 5,
}
