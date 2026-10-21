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
    /// <summary>Sand, a surface patch (solid).</summary>
    Sand = 6,
    /// <summary>A flower on the surface (not solid).</summary>
    Flower = 7,
    /// <summary>A mushroom on the surface (not solid).</summary>
    Mushroom = 8,
    /// <summary>Planks: wood the player placed (solid).</summary>
    Planks = 9,
    /// <summary>A placed door (solid).</summary>
    Door = 10,
    /// <summary>A placed table (solid).</summary>
    Table = 11,
    /// <summary>A placed chair (solid).</summary>
    Chair = 12,
    /// <summary>A placed bed (solid).</summary>
    Bed = 13,
    /// <summary>A placed chest (solid).</summary>
    Chest = 14,
    /// <summary>A placed furnace, a crafting station (solid).</summary>
    Furnace = 15,
    /// <summary>A placed glass block (solid).</summary>
    Glass = 16,
    /// <summary>A placed brick block (solid).</summary>
    Brick = 17,

    /// <summary>Stone with coal in it (solid; mined with the pickaxe into coal). Appended in P7.</summary>
    CoalOre = 18,

    /// <summary>Stone with copper in it (solid; mined with the pickaxe into copper).</summary>
    CopperOre = 19,

    /// <summary>Stone with iron in it (solid; mined with the pickaxe into iron).</summary>
    IronOre = 20,

    /// <summary>Stone with gold in it (solid; mined with the pickaxe into gold; the deepest ore).</summary>
    GoldOre = 21,

    /// <summary>A placed torch: light from its tile, not solid (the player walks through it).</summary>
    Torch = 22,

    /// <summary>Tall grass on the surface (not solid; drops nothing). Its noun is <c>hierba</c>, the same as the grass.</summary>
    TallGrass = 23,
}
