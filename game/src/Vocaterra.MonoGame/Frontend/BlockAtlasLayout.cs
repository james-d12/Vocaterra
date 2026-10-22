using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Frontend;

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
    public const int ColourRows = 5;

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
            BlockId.Sand => BlockCell.Sand,
            BlockId.Trunk => up || down ? BlockCell.TrunkEnd : BlockCell.TrunkSide,
            BlockId.Leaves => BlockCell.Leaves,
            BlockId.Bush => BlockCell.Bush,
            BlockId.BerryBush => BlockCell.BerryBush,
            BlockId.Water => BlockCell.Water,
            BlockId.Path => up ? BlockCell.PathTop : BlockCell.Dirt,
            BlockId.Planks => BlockCell.Planks,
            BlockId.Door => BlockCell.DoorLeaf,
            BlockId.Table => up ? BlockCell.TableTop : down ? BlockCell.Planks : BlockCell.TableSide,
            BlockId.Torch => up ? BlockCell.TorchFlame : BlockCell.TorchStick,
            BlockId.Stall => up ? BlockCell.StallTop : BlockCell.StallSide,
            BlockId.Roof => up ? BlockCell.RoofTop : BlockCell.RoofSide,
            BlockId.Fence => BlockCell.Fence,
            BlockId.Wall => BlockCell.Wall,
            BlockId.FireRing => BlockCell.FireRing,
            BlockId.Sign => up || down ? BlockCell.Planks : BlockCell.SignBoard,
            BlockId.Bridge => up ? BlockCell.BridgeTop : BlockCell.BridgeSide,
            _ => BlockCell.White,
        };
    }

    /// <summary>The cell that draws an item as an icon (the hotbar and the held item), or <see langword="null"/> when the item has no 3D picture.</summary>
    public static BlockCell? IconFor(ItemId item) => item switch
    {
        ItemId.Wood => BlockCell.IconWood,
        ItemId.Berry => BlockCell.IconBerry,
        ItemId.Torch => BlockCell.TorchFlame,
        ItemId.Bottle => BlockCell.IconBottle,
        ItemId.WaterBottle => BlockCell.IconWaterBottle,
        ItemId.Bread => BlockCell.IconBread,
        _ => null,
    };

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
