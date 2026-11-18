using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.Blocks;

/// <summary>The cells of the block atlas: one 16 x 16 picture each, in this order, row by row (eight to a row).</summary>
public enum BlockCell
{
    /// <summary>The top of a grass block.</summary>
    GrassTop = 0,

    /// <summary>The side of a grass block (green fringe over dirt).</summary>
    GrassSide,

    /// <summary>Dirt.</summary>
    Dirt,

    /// <summary>Stone.</summary>
    Stone,

    /// <summary>Sand.</summary>
    Sand,

    /// <summary>The bark of a trunk.</summary>
    TrunkSide,

    /// <summary>The cut end of a trunk (rings).</summary>
    TrunkEnd,

    /// <summary>Leaves.</summary>
    Leaves,

    /// <summary>A plain bush.</summary>
    Bush,

    /// <summary>A bush with red berries.</summary>
    BerryBush,

    /// <summary>Water (translucent).</summary>
    Water,

    /// <summary>The top of a trodden path.</summary>
    PathTop,

    /// <summary>Planks.</summary>
    Planks,

    /// <summary>A door leaf.</summary>
    DoorLeaf,

    /// <summary>The top of the workbench.</summary>
    TableTop,

    /// <summary>The side of the workbench.</summary>
    TableSide,

    /// <summary>The stick of a torch.</summary>
    TorchStick,

    /// <summary>The flame of a torch.</summary>
    TorchFlame,

    /// <summary>The awning of the stall (red and white stripes).</summary>
    StallTop,

    /// <summary>The counter of the stall.</summary>
    StallSide,

    /// <summary>The top of a roof (terracotta tiles).</summary>
    RoofTop,

    /// <summary>The side of a roof.</summary>
    RoofSide,

    /// <summary>A fence.</summary>
    Fence,

    /// <summary>A house wall (plaster in a timber frame).</summary>
    Wall,

    /// <summary>A stone of the fire ring.</summary>
    FireRing,

    /// <summary>The board of the signpost.</summary>
    SignBoard,

    /// <summary>The deck of the bridge.</summary>
    BridgeTop,

    /// <summary>The side of the bridge.</summary>
    BridgeSide,

    /// <summary>Plain white: the figures, the beacon and other things that take their colour from the vertex.</summary>
    White,

    /// <summary>The icon of a piece of wood (the item).</summary>
    IconWood,

    /// <summary>The icon of a berry (the item).</summary>
    IconBerry,

    /// <summary>The icon of an empty glass bottle (the item, S6).</summary>
    IconBottle,

    /// <summary>The icon of a bottle of water (the item, S6).</summary>
    IconWaterBottle,


    /// <summary>The icon of a loaf of bread (the item, S6).</summary>
    IconBread,

    /// <summary>Snow: the top of the high peaks.</summary>
    Snow,

    /// <summary>The side of a snow block (white cap over stone, S14).</summary>
    SnowSide,

    /// <summary>Mossy stone: low rock in the forest (S14).</summary>
    MossyStone,

    /// <summary>Cobblestone (S14).</summary>
    Cobble,

    /// <summary>The bark of a spruce (S14).</summary>
    SpruceBark,

    /// <summary>The cut end of a spruce (S14).</summary>
    SpruceEnd,

    /// <summary>The bark of a birch (S14).</summary>
    BirchBark,

    /// <summary>The cut end of a birch (S14).</summary>
    BirchEnd,

    /// <summary>Spruce needles, with cut-out holes (S14).</summary>
    SpruceLeaves,

    /// <summary>Birch leaves, with cut-out holes (S14).</summary>
    BirchLeaves,

    /// <summary>Tall grass, a crossed-quad plant (S14).</summary>
    TallGrass,

    /// <summary>A fern, a crossed-quad plant (S14).</summary>
    Fern,

    /// <summary>Reeds with cattails, a crossed-quad plant (S14).</summary>
    Reeds,

    /// <summary>A yellow flower, a crossed-quad plant (S14).</summary>
    YellowFlower,

    /// <summary>A white flower, a crossed-quad plant (S14).</summary>
    WhiteFlower,

    /// <summary>Falling water: the face of the waterfall (S14).</summary>
    Waterfall,

    /// <summary>Shallow water, lighter than <see cref="Water"/> (S14).</summary>
    WaterShallow,

    /// <summary>The side of a timber post or beam (S15).</summary>
    TimberSide,

    /// <summary>The end of a timber post or beam.</summary>
    TimberEnd,

    /// <summary>The glass of a lantern: bright, drawn without shading.</summary>
    Lantern,

    /// <summary>The side of a crate.</summary>
    CrateSide,

    /// <summary>The top of a crate.</summary>
    CrateTop,

    /// <summary>A red stripe of the stall's awning.</summary>
    AwningRed,

    /// <summary>A white stripe of the stall's awning.</summary>
    AwningWhite,

    /// <summary>The canvas of the tent.</summary>
    Canvas,

    /// <summary>The side of a chest, with its iron bands and latch.</summary>
    ChestSide,

    /// <summary>The lid of a chest.</summary>
    ChestTop,

    /// <summary>Tilled soil in furrows.</summary>
    FarmlandTop,

    /// <summary>Wheat, a crossed-quad plant.</summary>
    Wheat,

    /// <summary>Loaves of bread on a cloth, the pile on the counter.</summary>
    Loaves,

    /// <summary>A pile of red apples.</summary>
    Apples,

    /// <summary>A group of blue glass bottles.</summary>
    Bottles,

    /// <summary>The top of a bed's head half, seen from above with the head at the top of the picture: a wooden frame at the head end, the cream of the pillow, the sheet turned down and the red blanket (Y03, Y08).</summary>
    Bed,

    /// <summary>The top of a bedroll's head half: a striped mat of cloth with the darker patch the rolled pillow lies on (Y03, Y08).</summary>
    Bedroll,

    /// <summary>The shop's board, first half: an arrow and TIE.</summary>
    SignTiendaA,

    /// <summary>The shop's board, second half: NDA.</summary>
    SignTiendaB,

    /// <summary>The river's board: an arrow and RÍO.</summary>
    SignRio,

    /// <summary>The mountain's board, first half: an arrow and MON.</summary>
    SignMontanaA,

    /// <summary>The mountain's board, second half: TAÑA.</summary>
    SignMontanaB,

    /// <summary>The front of the guide's head (S15).</summary>
    FaceGuide,

    /// <summary>The front of the shopkeeper's head.</summary>
    FaceShopkeeper,

    /// <summary>The front of the villager's head.</summary>
    FaceVillager,

    /// <summary>The front of the player's head.</summary>
    FacePlayer,

    /// <summary>Woven cloth, nearly white: the clothes of the figures take their colour from the vertex.</summary>
    Cloth,

    /// <summary>Skin: the hands, arms, necks and the sides of the heads.</summary>
    Skin,

    /// <summary>Hair: streaked, nearly white, tinted by the vertex.</summary>
    Hair,

    /// <summary>The back of a backpack: a leather flap and a buckle.</summary>
    Backpack,

    /// <summary>The icon of a torch (the item).</summary>
    IconTorch,

    /// <summary>A signpost board with no word: the sides of a board that are not meant to be read (S15).</summary>
    SignBlank,

    /// <summary>The icon of the learner's book (the item, S20): a closed book with a brown cover and cream pages.</summary>
    IconBook,

    /// <summary>The front of the fisher's head (village-plan V05): a grey beard.</summary>
    FaceFisher,

    /// <summary>The front of the baker's head: a woman with auburn hair down the sides.</summary>
    FaceBaker,

    /// <summary>The front of the smith's head: black hair and a moustache.</summary>
    FaceSmith,

    /// <summary>The front of the farmer's head: sandy hair and freckles.</summary>
    FaceFarmer,

    /// <summary>The icon of a coin (the item, V05): a gold piece with a raised rim.</summary>
    IconCoin,

    /// <summary>The icon of an apple (the item): a red apple with a stalk and a leaf.</summary>
    IconApple,

    /// <summary>The icon of the axe (the item): a steel head on a wooden haft.</summary>
    IconAxe,

    /// <summary>The icon of a roast apple (the item, N07): the apple, browned and cooked, with a golden highlight.</summary>
    IconRoastApple,

    /// <summary>The top of a bed's foot half (Y08): the red blanket going on from the head half, with a fold across it and the frame at the foot end.</summary>
    BedFoot,

    /// <summary>The side of a bed's mattress (Y08): the blanket hanging over the edge, with a darker hem. Placed before the interface-only cells like the other world pictures.</summary>
    BedSide,

    /// <summary>The top and sides of a bedroll's foot half (Y08): the same striped mat as <see cref="Bedroll"/>, without the pillow's patch.</summary>
    BedrollFoot,

    /// <summary>Slate: dark blue-grey rock in horizontal bands (B10).</summary>
    Slate,

    /// <summary>Granite: pink-grey rock speckled with dark and light grains (B10).</summary>
    Granite,

    /// <summary>Limestone: pale cream rock with fine grain and faint seams (B10).</summary>
    Limestone,

    /// <summary>Gravel: loose grey pebbles (B10; the path's pebbles are brown).</summary>
    Gravel,

    /// <summary>Lava: bright orange with darker veins (B10); its glow comes from the light index.</summary>
    Lava,

    /// <summary>Bedrock: near-black, rough rock (B10).</summary>
    Bedrock,

    /// <summary>Iron ore: stone with tan flecks (B10).</summary>
    IronOre,

    /// <summary>Tin ore: stone with pale grey flecks (B10).</summary>
    TinOre,

    /// <summary>Copper ore: stone with orange and green flecks (B10).</summary>
    CopperOre,

    /// <summary>Basalt: dark charcoal rock in narrow columns (B11b).</summary>
    Basalt,

    /// <summary>The icon of the bowl (a station fixture, W07): a round earthenware bowl with a pale rim.</summary>
    IconBowl,

    /// <summary>The icon of the spoon (a station fixture, W07): a wooden spoon lying along the diagonal.</summary>
    IconSpoon,

    /// <summary>The icon of the oven (a station fixture, W07): a cobble vault with a fire in its arched mouth and a chimney.</summary>
    IconOven,

    /// <summary>The plain tile any icon key without art of its own is drawn as (W07): a grey rounded square.</summary>
    IconPlain,

    /// <summary>The icon of flour (the item, W10): a cloth sack tied at the neck. Placed after the interface-only cells so the world's mip levels do not move.</summary>
    IconFlour,

    /// <summary>The front of the innkeeper's head (Y06): silver hair pinned up, rosy cheeks. Placed last, so no other cell moves.</summary>
    FaceInnkeeper,

    /// <summary>The icon of the stake (the item, Y10): a pale wooden stake with a pointed foot and a red cloth flag. Placed last, so no other cell moves.</summary>
    IconStake,

    /// <summary>The icon of the camp kit (the item, Y11): a small green tent with a dark door and a rolled bedroll in front. Placed last, so no other cell moves.</summary>
    IconCampKit,
}

/// <summary>
/// Where each block face's picture lives in the block atlas, and its texture rectangle. The atlas is <see cref="Columns"/> x <see cref="Rows"/>
/// cells of <see cref="CellSize"/> pixels: the colour cells in the top <see cref="ColourRows"/> rows, and below them a grey copy of each cell in
/// the same place (<see cref="GreyPixelRectangle"/>; the greyscale A/B of D-game-6). <see cref="BlockArt"/> draws it, the renderer uploads it.
/// </summary>
public static class BlockAtlasLayout
{
    /// <summary>The size of one cell in pixels.</summary>
    public const int CellSize = 16;

    /// <summary>The cells in a row of the atlas.</summary>
    public const int Columns = 8;

    /// <summary>The rows of colour cells, at the top of the atlas.</summary>
    public const int ColourRows = 15; // B11b: 15 rows, so basalt fits (113 cells); B10 had 14 rows for the nine underground blocks; Y03 had 13 rows for the bed and the bedroll (97 cells)

    /// <summary>The rows of the atlas: the colour cells, then their grey copies.</summary>
    public const int Rows = 2 * ColourRows;

    /// <summary>The atlas width in pixels.</summary>
    public const int Width = Columns * CellSize;

    /// <summary>The atlas height in pixels.</summary>
    public const int Height = Rows * CellSize;

    /// <summary>The number of cells in use.</summary>
    public static int CellCount => Enum.GetValues<BlockCell>().Length;

    /// <summary>
    /// The inset, in texels, that keeps the texture rectangle off the cell border, so that point sampling never picks a neighbour's texel
    /// along an edge.
    /// </summary>
    public const float Inset = 0.02f;

    /// <summary>The picture a face of a block shows. Air has none (the white cell is returned).</summary>
    public static BlockCell CellFor(BlockId block, BlockFace face)
    {
        var up = face == BlockFace.Up;
        var down = face == BlockFace.Down;
        return block switch
        {
            BlockId.Grass => up ? BlockCell.GrassTop : down ? BlockCell.Dirt : BlockCell.GrassSide,
            BlockId.Dirt => BlockCell.Dirt,
            BlockId.Stone => BlockCell.Stone,
            BlockId.MossyStone => BlockCell.MossyStone,
            BlockId.Cobble => BlockCell.Cobble,
            BlockId.Sand => BlockCell.Sand,
            BlockId.Trunk => up || down ? BlockCell.TrunkEnd : BlockCell.TrunkSide,
            BlockId.SpruceTrunk => up || down ? BlockCell.SpruceEnd : BlockCell.SpruceBark,
            BlockId.BirchTrunk => up || down ? BlockCell.BirchEnd : BlockCell.BirchBark,
            BlockId.Leaves => BlockCell.Leaves,
            BlockId.SpruceLeaves => BlockCell.SpruceLeaves,
            BlockId.BirchLeaves => BlockCell.BirchLeaves,
            BlockId.TallGrass => BlockCell.TallGrass,
            BlockId.Fern => BlockCell.Fern,
            BlockId.Reeds => BlockCell.Reeds,
            BlockId.YellowFlower => BlockCell.YellowFlower,
            BlockId.WhiteFlower => BlockCell.WhiteFlower,
            BlockId.Bush => BlockCell.Bush,
            BlockId.BerryBush => BlockCell.BerryBush,
            BlockId.Water => BlockCell.Water,
            BlockId.Path => up ? BlockCell.PathTop : BlockCell.Dirt,
            BlockId.Planks => BlockCell.Planks,
            BlockId.Door => BlockCell.DoorLeaf,
            BlockId.Table => up ? BlockCell.TableTop : down ? BlockCell.Planks : BlockCell.TableSide,
            BlockId.Torch or BlockId.PostTorch => up ? BlockCell.TorchFlame : BlockCell.TorchStick,
            BlockId.Stall => up ? BlockCell.StallTop : BlockCell.StallSide,
            BlockId.Roof => up ? BlockCell.RoofTop : BlockCell.RoofSide,
            BlockId.Fence => BlockCell.Fence,
            BlockId.Wall => BlockCell.Wall,
            BlockId.FireRing => BlockCell.FireRing,
            BlockId.Sign => BlockCell.SignBoard,
            BlockId.Bridge => up ? BlockCell.BridgeTop : BlockCell.BridgeSide,
            BlockId.Snow => up ? BlockCell.Snow : down ? BlockCell.Stone : BlockCell.SnowSide,
            BlockId.Timber => up || down ? BlockCell.TimberEnd : BlockCell.TimberSide,
            BlockId.Lantern => BlockCell.Lantern,
            BlockId.Crate => up || down ? BlockCell.CrateTop : BlockCell.CrateSide,
            BlockId.Counter => up ? BlockCell.Planks : BlockCell.CrateSide,
            BlockId.Slate => BlockCell.Slate,
            BlockId.Granite => BlockCell.Granite,
            BlockId.Limestone => BlockCell.Limestone,
            BlockId.Gravel => BlockCell.Gravel,
            BlockId.Lava => BlockCell.Lava,
            BlockId.Bedrock => BlockCell.Bedrock,
            BlockId.IronOre => BlockCell.IronOre,
            BlockId.TinOre => BlockCell.TinOre,
            BlockId.CopperOre => BlockCell.CopperOre,
            BlockId.Basalt => BlockCell.Basalt,
            BlockId.AwningRed => BlockCell.AwningRed,
            BlockId.AwningWhite => BlockCell.AwningWhite,
            BlockId.Canvas => BlockCell.Canvas,
            BlockId.Chest => up || down ? BlockCell.ChestTop : BlockCell.ChestSide,
            BlockId.Farmland => up ? BlockCell.FarmlandTop : BlockCell.Dirt,
            BlockId.Wheat => BlockCell.Wheat,
            BlockId.Loaves => BlockCell.Loaves,
            BlockId.Apples => BlockCell.Apples,
            BlockId.Bottles => BlockCell.Bottles,
            BlockId.Bed => up ? BlockCell.Bed : BlockCell.BedSide,
            BlockId.BedFoot => up ? BlockCell.BedFoot : BlockCell.BedSide,
            BlockId.Bedroll => up ? BlockCell.Bedroll : BlockCell.BedrollFoot,
            BlockId.BedrollFoot => BlockCell.BedrollFoot,
            BlockId.SignTiendaA => Reads(face, BlockCell.SignTiendaA),
            BlockId.SignTiendaB => Reads(face, BlockCell.SignTiendaB),
            BlockId.SignRio => Reads(face, BlockCell.SignRio),
            BlockId.SignMontanaA => Reads(face, BlockCell.SignMontanaA),
            BlockId.SignMontanaB => Reads(face, BlockCell.SignMontanaB),
            _ => BlockCell.White,
        };
    }

    /// <summary>
    /// The picture of a signboard's face: the word on the south and west faces, which read left to right for someone standing on that side (the boards of the layout
    /// extend to the reader's right, and the textures run along the face that way), and a blank plank on the other four, where the word would read backwards.
    /// </summary>
    public static BlockCell Reads(BlockFace face, BlockCell word) => face is BlockFace.South or BlockFace.West ? word : BlockCell.SignBlank;

    /// <summary>
    /// Whether the cell at a column and a row of the colour block is one that is only ever drawn on the interface (W07: the station's icons from <see cref="BlockCell.IconBowl"/> on, with
    /// the plain tile). It is left out of the mip levels (<see cref="BlockArt.MipLevel"/>): the world never samples it, and the top levels average the whole atlas, so painting it would
    /// move the far horizon of every picture the game draws.
    /// </summary>
    /// <param name="column">The cell's column.</param>
    /// <param name="row">The cell's row within the colour block (a grey copy's row minus <see cref="ColourRows"/>).</param>
    public static bool IsUiOnly(int column, int row) => (row * Columns) + column >= (int)BlockCell.IconBowl;

    // The art by icon key (I01): the keys of game/items.json and of the station fixtures (W07). An item's `icon` names one; a key with no art draws the fallback tile.
    private static readonly Dictionary<string, BlockCell> IconCells = new(StringComparer.Ordinal)
    {
        ["wood"] = BlockCell.IconWood,
        ["berry"] = BlockCell.IconBerry,
        ["torch"] = BlockCell.IconTorch, // S15: a stick with its flame, not the flame's square
        ["bottle"] = BlockCell.IconBottle,
        ["water-bottle"] = BlockCell.IconWaterBottle,
        ["bread"] = BlockCell.IconBread,
        ["book"] = BlockCell.IconBook,
        ["coin"] = BlockCell.IconCoin,
        ["apple"] = BlockCell.IconApple,
        ["axe"] = BlockCell.IconAxe,
        ["roast-apple"] = BlockCell.IconRoastApple,
        ["flour"] = BlockCell.IconFlour,
        ["stake"] = BlockCell.IconStake,
        ["camp-kit"] = BlockCell.IconCampKit,
        ["bowl"] = BlockCell.IconBowl,
        ["spoon"] = BlockCell.IconSpoon,
        ["oven"] = BlockCell.IconOven,
    };

    /// <summary>The icon keys the atlas has art for: what an item's <c>icon</c> in <c>game/items.json</c> may name (the content check reports any other).</summary>
    public static IReadOnlySet<string> IconKeys { get; } = IconCells.Keys.ToHashSet(StringComparer.Ordinal);

    /// <summary>The cell that draws an item as an icon (the hotbar and the held item): the one its <see cref="ItemDefinition.Icon"/> key names, or the fallback tile (<see cref="BlockCell.IconPlain"/>) when the atlas has no art for the key.</summary>
    /// <param name="item">The item's definition.</param>
    public static BlockCell IconFor(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IconForKey(item.Icon);
    }

    /// <summary>
    /// The cell that draws an icon by its key (W07: a station fixture's <c>icon</c> in <c>stations.json</c>, an item's <c>icon</c> in <c>items.json</c>, or a made thing's noun). A key with no art of its own draws the plain tile, so a
    /// fixture or an item added to the content never shows as a hole.
    /// </summary>
    /// <param name="key">The icon key (<c>bowl</c>, <c>spoon</c>, <c>oven</c>, an item's key, or any other).</param>
    public static BlockCell IconForKey(string key) => IconCells.TryGetValue(key, out var cell) ? cell : BlockCell.IconPlain;

    /// <summary>The cell's rectangle in pixels of the atlas texture (x, y, width, height).</summary>
    public static (int X, int Y, int Width, int Height) PixelRectangle(BlockCell cell)
    {
        var index = (int)cell;
        return ((index % Columns) * CellSize, (index / Columns) * CellSize, CellSize, CellSize);
    }

    /// <summary>
    /// The cell's rectangle in pixels of its grey copy: the same column, one block of <see cref="ColourRows"/> rows further down.
    /// </summary>
    public static (int X, int Y, int Width, int Height) GreyPixelRectangle(BlockCell cell)
    {
        var (x, y, w, h) = PixelRectangle(cell);
        return (x, y + (ColourRows * CellSize), w, h);
    }

    /// <summary>
    /// The texture rectangle of a cell, inset by <see cref="Inset"/> texels: (u0, v0, u1, v1), v growing downward. With <paramref name="grey"/>
    /// it is the rectangle of the cell's grey copy.
    /// </summary>
    public static (float U0, float V0, float U1, float V1) Uv(BlockCell cell, bool grey = false)
    {
        var (x, y, w, h) = grey ? GreyPixelRectangle(cell) : PixelRectangle(cell);
        return ((x + Inset) / Width, (y + Inset) / Height, (x + w - Inset) / Width, (y + h - Inset) / Height);
    }
}
