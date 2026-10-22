namespace Vocaterra.Simulation.Voxel;

/// <summary>
/// The block kinds of the 3D voxel world. The numeric values are stored in chunks, hashed into the world digest and (later) written to saves,
/// so a value is never changed or reused: new kinds are appended, as with the 2D <c>TileId</c>.
/// </summary>
public enum BlockId : ushort
{
    /// <summary>Empty space.</summary>
    Air = 0,

    /// <summary>Grass: the top layer of the ground (solid).</summary>
    Grass = 1,

    /// <summary>Dirt: the layers under the top of the ground (solid).</summary>
    Dirt = 2,

    /// <summary>Stone: the deep layer (solid).</summary>
    Stone = 3,

    /// <summary>Sand: a riverbank or beach (solid).</summary>
    Sand = 4,

    /// <summary>The wood of a tree (solid; gathered by hand for <c>wood</c>).</summary>
    Trunk = 5,

    /// <summary>The canopy of a tree (solid, transparent for the renderer).</summary>
    Leaves = 6,

    /// <summary>A plain bush (not solid: the player walks through it).</summary>
    Bush = 7,

    /// <summary>A bush with berries (not solid; gathered by hand for a berry, then it becomes a plain <see cref="Bush"/>).</summary>
    BerryBush = 8,

    /// <summary>Water (not solid, liquid, transparent).</summary>
    Water = 9,

    /// <summary>A trodden path or the village square (solid).</summary>
    Path = 10,

    /// <summary>Planks: house floors, or wood the player placed (solid).</summary>
    Planks = 11,

    /// <summary>A doorway with an open door (not solid: the player walks through).</summary>
    Door = 12,

    /// <summary>The workbench (<c>mesa</c>), the crafting station (solid).</summary>
    Table = 13,

    /// <summary>A torch (not solid).</summary>
    Torch = 14,

    /// <summary>The stall's counter (solid).</summary>
    Stall = 15,

    /// <summary>A roof or an awning (solid).</summary>
    Roof = 16,

    /// <summary>A fence post or rail (solid).</summary>
    Fence = 17,

    /// <summary>A house wall (solid).</summary>
    Wall = 18,

    /// <summary>A stone of the camp's fire ring (solid). Appended in S1: the route needs an unlit fire ring.</summary>
    FireRing = 19,

    /// <summary>The board of a signpost (solid). Appended in S1.</summary>
    Sign = 20,

    /// <summary>A plank of the bridge deck (solid). Appended in S1.</summary>
    Bridge = 21,

    /// <summary>Snow: the cap of the high peaks (solid). Appended in S13.</summary>
    Snow = 22,

    /// <summary>The wood of a spruce (solid; gathered by hand for <c>wood</c>, like <see cref="Trunk"/>). Appended in S14.</summary>
    SpruceTrunk = 23,

    /// <summary>The wood of a birch (solid; gathered by hand for <c>wood</c>). Appended in S14.</summary>
    BirchTrunk = 24,

    /// <summary>The needles of a spruce (solid, transparent for the renderer). Appended in S14.</summary>
    SpruceLeaves = 25,

    /// <summary>The leaves of a birch (solid, transparent for the renderer). Appended in S14.</summary>
    BirchLeaves = 26,

    /// <summary>Tall grass: undergrowth, not solid, never targeted by the block ray. Appended in S14.</summary>
    TallGrass = 27,

    /// <summary>A fern: undergrowth, not solid, never targeted. Appended in S14.</summary>
    Fern = 28,

    /// <summary>Reeds by the water: undergrowth, not solid, never targeted. Appended in S14.</summary>
    Reeds = 29,

    /// <summary>A yellow flower: undergrowth, not solid, never targeted. Appended in S14.</summary>
    YellowFlower = 30,

    /// <summary>A white flower: undergrowth, not solid, never targeted. Appended in S14.</summary>
    WhiteFlower = 31,

    /// <summary>Mossy stone: low rock faces in the forest (solid). Appended in S14.</summary>
    MossyStone = 32,

    /// <summary>Cobblestone: paving and stonework (solid). Appended in S14.</summary>
    Cobble = 33,
}
