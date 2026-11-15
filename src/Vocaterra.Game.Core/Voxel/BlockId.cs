namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The block kinds of the voxel world. The numeric values are stored in chunks, hashed into the world digest and written to saves, so a
/// value is never changed or reused: new kinds are appended.
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

    /// <summary>A timber post or beam: the frame of the houses, the stall's posts and the pergola (solid). Appended in S15.</summary>
    Timber = 34,

    /// <summary>A lantern on a post or hanging from a beam: not solid, and it glows (the mesher lights the faces near it). Appended in S15.</summary>
    Lantern = 35,

    /// <summary>A wooden crate (solid). Appended in S15.</summary>
    Crate = 36,

    /// <summary>A red stripe of the stall's awning (solid). Appended in S15.</summary>
    AwningRed = 37,

    /// <summary>A white stripe of the stall's awning (solid). Appended in S15.</summary>
    AwningWhite = 38,

    /// <summary>Canvas: the player's tent (solid). Appended in S15.</summary>
    Canvas = 39,

    /// <summary>A chest (solid). Appended in S15.</summary>
    Chest = 40,

    /// <summary>Tilled soil of the crop field (solid). Appended in S15.</summary>
    Farmland = 41,

    /// <summary>Wheat in the crop field: undergrowth, not solid, never targeted. Appended in S15.</summary>
    Wheat = 42,

    /// <summary>A pile of loaves on the stall's counter or in a crate: a small decoration, not solid, never targeted. Appended in S15.</summary>
    Loaves = 43,

    /// <summary>A pile of apples (a decoration like <see cref="Loaves"/>). Appended in S15.</summary>
    Apples = 44,

    /// <summary>A group of bottles (a decoration like <see cref="Loaves"/>). Appended in S15.</summary>
    Bottles = 45,

    /// <summary>The first half of the signpost's board that points to the shop: an arrow and "TIE" (solid). Appended in S15.</summary>
    SignTiendaA = 46,

    /// <summary>The second half of the shop's board: "NDA" (solid). Appended in S15.</summary>
    SignTiendaB = 47,

    /// <summary>The board that points to the river: an arrow and "RÍO" (solid). Appended in S15.</summary>
    SignRio = 48,

    /// <summary>The first half of the mountain's board: an arrow and "MON" (solid). Appended in S15.</summary>
    SignMontanaA = 49,

    /// <summary>The second half of the mountain's board: "TAÑA" (solid). Appended in S15.</summary>
    SignMontanaB = 50,

    /// <summary>A torch fixed to a post as scenery (the guide's pergola): drawn like <see cref="Torch"/> and lights its surroundings like a lantern, but it is not the item's torch (not gathered, not a noun, never found by "where is the torch"). Appended in S15.</summary>
    PostTorch = 51,

    /// <summary>The head cell of a bed of the inn (Y03, Y08): the half with the headboard and the pillow; the bed is two cells long, this and <see cref="BedFoot"/> beside it. Not solid (a body walks through it), not decor (the crosshair stops on it, and nothing is placed into its cell). Appended in Y03.</summary>
    Bed = 52,

    /// <summary>The head cell of a camp's bedroll (Y03, Y08): a low mat with a rolled pillow; the bedroll is two cells long, this and <see cref="BedrollFoot"/> beside it, with the bed's rules. Appended in Y03.</summary>
    Bedroll = 53,

    /// <summary>The foot cell of a bed (Y08): the half with the footboard, beside its <see cref="Bed"/> head; the bed's rules. Appended in Y08.</summary>
    BedFoot = 54,

    /// <summary>The foot cell of a bedroll (Y08): the half of the mat beside its <see cref="Bedroll"/> head; the bed's rules. Appended in Y08.</summary>
    BedrollFoot = 55,

    /// <summary>The inn's counter (Y08): a block of wood one block high that a body collides with as if it were two (<see cref="BlockDefinition.Tall"/>, like a fence), so nobody steps or jumps onto it and into the keeper's cell. Not a table, so no crafting panel opens beside it. Appended in Y08.</summary>
    Counter = 56,

    /// <summary>Slate: dark, banded underground rock (solid, not gatherable; nothing places it yet). Appended in B10.</summary>
    Slate = 57,

    /// <summary>Granite: speckled pink-grey underground rock (solid, not gatherable). Appended in B10.</summary>
    Granite = 58,

    /// <summary>Limestone: pale underground rock (solid, not gatherable). Appended in B10.</summary>
    Limestone = 59,

    /// <summary>Gravel: loose grey pebbles (solid, not gatherable). Appended in B10.</summary>
    Gravel = 60,

    /// <summary>Lava: a glowing fluid that stops a body (<see cref="BlockDefinition.StopsBody"/>: it cannot be walked into or fallen into), is not solid for sight and does no harm; a light kind like <see cref="Lantern"/>. Appended in B10.</summary>
    Lava = 61,

    /// <summary>Bedrock: near-black rough rock at the bottom of the world (solid, not gatherable, and never removed or replaced by any edit: <see cref="BlockDefinition.Indestructible"/>). Appended in B10.</summary>
    Bedrock = 62,

    /// <summary>Iron ore: stone with tan flecks (solid, not gatherable; no item and no noun in this series). Appended in B10.</summary>
    IronOre = 63,

    /// <summary>Tin ore: stone with pale grey flecks (solid, not gatherable; no item and no noun in this series). Appended in B10.</summary>
    TinOre = 64,

    /// <summary>Copper ore: stone with orange and green flecks (solid, not gatherable; no item and no noun in this series). Appended in B10.</summary>
    CopperOre = 65,
}
