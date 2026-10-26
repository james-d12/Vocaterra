using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.Core.Voxel;

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
    bool Decor = false)
{
    /// <summary>The ticks a trunk takes to gather by hand (one second).</summary>
    public const int TrunkGatherTicks = 60;

    /// <summary>The ticks a berry bush takes to gather by hand (three quarters of a second).</summary>
    public const int BerryBushGatherTicks = 45;

    private static readonly BlockDefinition[] Table =
    [
        new(BlockId.Air, "air", false, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.Grass, "grass", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Dirt, "dirt", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Stone, "stone", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Sand, "sand", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Trunk, "trunk", true, false, false, TrunkGatherTicks, ItemId.Wood, 1, BlockId.Air),
        new(BlockId.Leaves, "leaves", true, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.Bush, "bush", false, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.BerryBush, "berry-bush", false, true, false, BerryBushGatherTicks, ItemId.Berry, 1, BlockId.Bush),
        new(BlockId.Water, "water", false, true, true, 0, null, 0, BlockId.Air),
        new(BlockId.Path, "path", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Planks, "planks", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Door, "door", false, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Table, "table", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Torch, "torch", false, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.Stall, "stall", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Roof, "roof", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Fence, "fence", true, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.Wall, "wall", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.FireRing, "fire-ring", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Sign, "sign", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Bridge, "bridge", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Snow, "snow", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.SpruceTrunk, "spruce-trunk", true, false, false, TrunkGatherTicks, ItemId.Wood, 1, BlockId.Air),
        new(BlockId.BirchTrunk, "birch-trunk", true, false, false, TrunkGatherTicks, ItemId.Wood, 1, BlockId.Air),
        new(BlockId.SpruceLeaves, "spruce-leaves", true, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.BirchLeaves, "birch-leaves", true, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.TallGrass, "tall-grass", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.Fern, "fern", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.Reeds, "reeds", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.YellowFlower, "yellow-flower", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.WhiteFlower, "white-flower", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.MossyStone, "mossy-stone", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Cobble, "cobble", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Timber, "timber", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Lantern, "lantern", false, true, false, 0, null, 0, BlockId.Air),
        new(BlockId.Crate, "crate", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.AwningRed, "awning-red", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.AwningWhite, "awning-white", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Canvas, "canvas", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Chest, "chest", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Farmland, "farmland", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.Wheat, "wheat", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.Loaves, "loaves", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.Apples, "apples", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.Bottles, "bottles", false, true, false, 0, null, 0, BlockId.Air, Decor: true),
        new(BlockId.SignTiendaA, "sign-tienda-a", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.SignTiendaB, "sign-tienda-b", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.SignRio, "sign-rio", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.SignMontanaA, "sign-montana-a", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.SignMontanaB, "sign-montana-b", true, false, false, 0, null, 0, BlockId.Air),
        new(BlockId.PostTorch, "post-torch", false, true, false, 0, null, 0, BlockId.Air),
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
