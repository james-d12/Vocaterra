using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// What a block is made of, for tools (W02): a tool speeds up gathering only the blocks of its own material. <see cref="None"/> is what no tool works on.
/// </summary>
public enum Material : byte
{
    /// <summary>Not a material a tool works on (props, buildings of mixed parts, snow, the blocks a player cannot gather).</summary>
    None = 0,

    /// <summary>Wood: trunks, planks and the timber and wooden fittings.</summary>
    Wood = 1,

    /// <summary>Plants: leaves, bushes, the berry bush, undergrowth and wheat.</summary>
    Plant = 2,

    /// <summary>Stone and cobble.</summary>
    Stone = 3,

    /// <summary>Earth: grass, dirt, sand, the path and the farmland.</summary>
    Soil = 4,

    /// <summary>Water.</summary>
    Water = 5,
}

/// <summary>
/// What the simulation knows about one block kind: whether bodies collide with it, how it should be treated by a renderer (transparent,
/// liquid), and how a player gathers it by hand. Spanish nouns are not here: game words live in the content bundle.
/// </summary>
/// <param name="Id">The block kind.</param>
/// <param name="Name">A stable lowercase English key (words joined by a hyphen); layout files and the front-end's atlas lookup use it.</param>
/// <param name="Solid">Whether bodies collide with it. Everything outside the grid is solid as well.</param>
/// <param name="Transparent">Whether a renderer should draw the faces behind it (leaves, water, plants, a torch). Nothing in the simulation reads it.</param>
/// <param name="Liquid">Whether the block is a liquid (water). Nothing in the simulation reads it besides the renderer's alpha and the scene's river.</param>
/// <param name="GatherTicks">The ticks a player must hold <c>Interact</c> on the block by hand to gather it; 0 means the block cannot be gathered.</param>
/// <param name="Drop">The item gathering gives, or <see langword="null"/> when gathering gives nothing (then the block is not gatherable).</param>
/// <param name="DropCount">How many of <paramref name="Drop"/> one gather gives.</param>
/// <param name="BecomesWhenGathered">The block that is left in the cell after a gather: <see cref="BlockId.Air"/> for a trunk, a plain bush for a berry bush.</param>
/// <param name="Decor">Whether the block is undergrowth (S14): not solid, never gathered, and invisible to the block ray, so the crosshair looks through it at whatever stands behind it; a block may be placed into its cell.</param>
/// <param name="Material">What the block is made of (W02): the tools whose <see cref="ItemDefinition.Tool"/> names it gather it faster.</param>
/// <param name="StopsBody">Whether a body collides with the block although it is not <see cref="Solid"/> (lava, B10): it cannot be walked into or fallen into, and sight and the block ray still see through it as through a fluid.</param>
/// <param name="Glows">Whether the block is a light kind (lantern, torch, post torch, lava): the renderer lights the faces around it.</param>
/// <param name="Indestructible">Whether no edit removes or replaces the block (bedrock, B10): the grid refuses every write that would.</param>
/// <param name="Tall">Whether a body collides with the block as if it were two blocks high (a fence, issue #33): no step-up lifts a body onto it, a jump does not clear it, and nothing stands on it. The block ray and the renderer still see one block.</param>
public sealed record BlockDefinition(
    BlockId Id,
    string Name,
    bool Solid,
    bool Transparent,
    bool Liquid,
    int GatherTicks,
    ItemId? Drop,
    int DropCount,
    BlockId BecomesWhenGathered,
    bool Decor = false,
    Material Material = Material.None,
    bool Tall = false,
    bool StopsBody = false,
    bool Glows = false,
    bool Indestructible = false)
{
    /// <summary>The ticks a trunk takes to gather by hand (one second).</summary>
    public const int TrunkGatherTicks = 60;

    /// <summary>The ticks a berry bush takes to gather by hand (three quarters of a second).</summary>
    public const int BerryBushGatherTicks = 45;

    private static readonly BlockDefinition[] Table =
    [
        new(BlockId.Air, "air", false, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.Grass, "grass", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Soil),
        new(BlockId.Dirt, "dirt", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Soil),
        new(BlockId.Stone, "stone", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
        new(BlockId.Sand, "sand", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Soil),
        new(BlockId.Trunk, "trunk", true, false, false, TrunkGatherTicks, ItemId.Wood, 1, BlockId.Air, Material: Material.Wood),
        new(BlockId.Leaves, "leaves", true, true, false, 0, null, 0, BlockId.Air, Material: Material.Plant),
        new(BlockId.Bush, "bush", false, true, false, 0, null, 0, BlockId.Air, Material: Material.Plant),
        new(BlockId.BerryBush, "berry-bush", false, true, false, BerryBushGatherTicks, ItemId.Berry, 1, BlockId.Bush, Material: Material.Plant),
        new(BlockId.Water, "water", false, true, true, 0, null, 0, BlockId.Air, Material: Material.Water),
        new(BlockId.Path, "path", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Soil),
        new(BlockId.Planks, "planks", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.Door, "door", false, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.Table, "table", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.Torch, "torch", false, true, false, 0, null, 0, BlockId.Air, Glows: true),
        new(BlockId.Stall, "stall", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.Roof, "roof", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.Fence, "fence", true, true, false, 0, null, 0, BlockId.Air, Material: Material.Wood, Tall: true),
        new(BlockId.Wall, "wall", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.FireRing, "fire-ring", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
        new(BlockId.Sign, "sign", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.Bridge, "bridge", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.Snow, "snow", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.SpruceTrunk, "spruce-trunk", true, false, false, TrunkGatherTicks, ItemId.Wood, 1, BlockId.Air, Material: Material.Wood),
        new(BlockId.BirchTrunk, "birch-trunk", true, false, false, TrunkGatherTicks, ItemId.Wood, 1, BlockId.Air, Material: Material.Wood),
        new(BlockId.SpruceLeaves, "spruce-leaves", true, true, false, 0, null, 0, BlockId.Air, Material: Material.Plant),
        new(BlockId.BirchLeaves, "birch-leaves", true, true, false, 0, null, 0, BlockId.Air, Material: Material.Plant),
        new(BlockId.TallGrass, "tall-grass", false, true, false, 0, null, 0, BlockId.Air, Decor: true, Material: Material.Plant),
        new(BlockId.Fern, "fern", false, true, false, 0, null, 0, BlockId.Air, Decor: true, Material: Material.Plant),
        new(BlockId.Reeds, "reeds", false, true, false, 0, null, 0, BlockId.Air, Decor: true, Material: Material.Plant),
        new(BlockId.YellowFlower, "yellow-flower", false, true, false, 0, null, 0, BlockId.Air, Decor: true, Material: Material.Plant),
        new(BlockId.WhiteFlower, "white-flower", false, true, false, 0, null, 0, BlockId.Air, Decor: true, Material: Material.Plant),
        new(BlockId.MossyStone, "mossy-stone", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
        new(BlockId.Cobble, "cobble", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
        new(BlockId.Timber, "timber", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.Lantern, "lantern", false, true, false, 0, null, 0, BlockId.Air, Glows: true),
        new(BlockId.Crate, "crate", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.AwningRed, "awning-red", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.AwningWhite, "awning-white", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Canvas, "canvas", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Chest, "chest", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.Farmland, "farmland", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Soil),
        new(BlockId.Wheat, "wheat", false, true, false, 0, null, 0, BlockId.Air, Decor: true, Material: Material.Plant),
        new(BlockId.Loaves, "loaves", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.Apples, "apples", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.Bottles, "bottles", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.SignTiendaA, "sign-tienda-a", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.SignTiendaB, "sign-tienda-b", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.SignRio, "sign-rio", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.SignMontanaA, "sign-montana-a", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.SignMontanaB, "sign-montana-b", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood),
        new(BlockId.PostTorch, "post-torch", false, true, false, 0, null, 0, BlockId.Air, Glows: true),
        new(BlockId.Bed, "bed", false, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.Bedroll, "bedroll", false, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.BedFoot, "bed-foot", false, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.BedrollFoot, "bedroll-foot", false, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.Counter, "counter", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Wood, Tall: true),
        new(BlockId.Slate, "slate", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
        new(BlockId.Granite, "granite", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
        new(BlockId.Limestone, "limestone", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
        new(BlockId.Gravel, "gravel", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Soil),
        new(BlockId.Lava, "lava", false, false, true, 0, null, 0, BlockId.Air, StopsBody: true, Glows: true),
        new(BlockId.Bedrock, "bedrock", true, false, false, 0, null, 0, BlockId.Air, Indestructible: true),
        new(BlockId.IronOre, "iron-ore", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
        new(BlockId.TinOre, "tin-ore", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
        new(BlockId.CopperOre, "copper-ore", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
        new(BlockId.Basalt, "basalt", true, false, false, 0, null, 0, BlockId.Air, Material: Material.Stone),
    ];

    /// <summary>Every block kind, ordered by id.</summary>
    public static IReadOnlyList<BlockDefinition> All => Table;

    /// <summary>Whether the block can be gathered by hand (it has gather ticks and a drop).</summary>
    public bool Gatherable => GatherTicks > 0 && Drop is not null && DropCount > 0;

    /// <summary>The definition of <paramref name="id"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The id has no definition.</exception>
    public static BlockDefinition Get(BlockId id)
    {
        var index = (int)id;
        if ((uint)index >= (uint)Table.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "No block definition for this id.");
        }

        return Table[index];
    }

    /// <summary>Whether bodies collide with blocks of kind <paramref name="id"/>. An id without a definition counts as solid.</summary>
    public static bool IsSolid(BlockId id)
    {
        var index = (int)id;
        return (uint)index >= (uint)Table.Length || Table[index].Solid;
    }

    /// <summary>Whether a body collides with the block as if it were two blocks high (<see cref="Tall"/>). An id without a definition is not.</summary>
    public static bool IsTall(BlockId id)
    {
        var index = (int)id;
        return (uint)index < (uint)Table.Length && Table[index].Tall;
    }

    /// <summary>Whether a body collides with blocks of kind <paramref name="id"/> although they are not solid (<see cref="StopsBody"/>: lava). An id without a definition is not.</summary>
    public static bool StopsBodyOf(BlockId id)
    {
        var index = (int)id;
        return (uint)index < (uint)Table.Length && Table[index].StopsBody;
    }

    /// <summary>Whether blocks of kind <paramref name="id"/> glow (<see cref="Glows"/>). An id without a definition does not.</summary>
    public static bool IsLight(BlockId id)
    {
        var index = (int)id;
        return (uint)index < (uint)Table.Length && Table[index].Glows;
    }

    /// <summary>Whether no edit removes or replaces blocks of kind <paramref name="id"/> (<see cref="Indestructible"/>: bedrock). An id without a definition is not.</summary>
    public static bool IsIndestructible(BlockId id)
    {
        var index = (int)id;
        return (uint)index < (uint)Table.Length && Table[index].Indestructible;
    }

    /// <summary>Whether the block is a trunk of any species (oak, spruce or birch): the wood a player gathers.</summary>
    public static bool IsTrunk(BlockId id) => id is BlockId.Trunk or BlockId.SpruceTrunk or BlockId.BirchTrunk;

    /// <summary>Whether the block is the leaves of a tree of any species.</summary>
    public static bool IsLeaves(BlockId id) => id is BlockId.Leaves or BlockId.SpruceLeaves or BlockId.BirchLeaves;

    /// <summary>Whether the block is undergrowth (<see cref="Decor"/>): the block ray passes through it and a placed block replaces it. An id without a definition is not.</summary>
    public static bool IsDecor(BlockId id)
    {
        var index = (int)id;
        return (uint)index < (uint)Table.Length && Table[index].Decor;
    }

    /// <summary>Whether the block is one of the five undergrowth plants the scene's undergrowth pass sows (tall grass, fern, reeds, the two flowers); wheat and the stall's goods are decor too but are built with their props (S15).</summary>
    public static bool IsUndergrowth(BlockId id) => id is BlockId.TallGrass or BlockId.Fern or BlockId.Reeds or BlockId.YellowFlower or BlockId.WhiteFlower;

    /// <summary>The block whose <see cref="Name"/> is <paramref name="name"/> (ordinal), or <see langword="null"/> when there is none.</summary>
    public static BlockDefinition? FromName(string? name)
    {
        foreach (var definition in Table)
        {
            if (string.Equals(definition.Name, name, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    /// <summary>
    /// The block that placing one <paramref name="item"/> makes, or <see langword="null"/> when the item cannot be placed. Wood makes planks and a
    /// torch makes a torch; a berry is eaten, not placed.
    /// </summary>
    public static BlockId? PlacedBy(ItemId item) => item switch
    {
        ItemId.Wood => BlockId.Planks,
        ItemId.Torch => BlockId.Torch,
        _ => null,
    };
}
