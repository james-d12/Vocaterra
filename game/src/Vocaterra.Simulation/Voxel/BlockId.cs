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
}
