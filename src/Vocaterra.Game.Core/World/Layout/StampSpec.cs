using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Layout;

/// <summary>
/// A small block stamp: layers of rows of characters, bottom layer first, north row first, west column first; a space leaves the cell alone. The stamp is drawn facing north; the
/// scene's build and the camp the player places (Y12) both turn it with <see cref="Turn"/> and read its cells with <see cref="Cells"/>, so they put the blocks down the same way.
/// </summary>
/// <param name="Name">The stamp's name.</param>
/// <param name="Legend">The block of each character.</param>
/// <param name="Layers">The layers from the ground up; each layer is a list of rows.</param>
/// <param name="Anchors">Named cells of the first layer as drawn (column, row), which turn with the stamp (Y12: the camp's <c>fire</c> middle, its <c>bed</c> and the cell to <c>wake</c> on); empty for most stamps.</param>
public sealed record StampSpec(string Name, IReadOnlyDictionary<char, BlockId> Legend, IReadOnlyList<IReadOnlyList<string>> Layers, IReadOnlyDictionary<string, (int Column, int Row)>? Anchors = null)
{
    /// <summary>The stamp's width as drawn: the longest row of any layer.</summary>
    public int DrawnWidth => Layers.Max(static layer => layer.Count == 0 ? 0 : layer.Max(static row => row.Length));

    /// <summary>The stamp's depth as drawn: the most rows of any layer.</summary>
    public int DrawnDepth => Layers.Max(static layer => layer.Count);

    /// <summary>The east-west size of the stamp once turned to face <paramref name="facing"/> (the sides swap for east and west).</summary>
    /// <param name="facing">The side the stamp faces.</param>
    public int WidthFacing(Direction facing) => facing is Direction.East or Direction.West ? DrawnDepth : DrawnWidth;

    /// <summary>The north-south size of the stamp once turned to face <paramref name="facing"/>.</summary>
    /// <param name="facing">The side the stamp faces.</param>
    public int DepthFacing(Direction facing) => facing is Direction.East or Direction.West ? DrawnWidth : DrawnDepth;

    /// <summary>
    /// Where a drawn cell lies once the stamp is turned to face <paramref name="facing"/>, as an offset from the west-north corner of the turned footprint. North is the drawing as it is;
    /// east, south and west turn it a quarter, a half and three quarters clockwise.
    /// </summary>
    /// <param name="facing">The side the stamp faces.</param>
    /// <param name="column">The drawn column (west first).</param>
    /// <param name="row">The drawn row (north first).</param>
    public (int Dx, int Dz) Turn(Direction facing, int column, int row) => facing switch
    {
        Direction.North => (column, row),
        Direction.East => (DrawnDepth - 1 - row, column),
        Direction.South => (DrawnWidth - 1 - column, DrawnDepth - 1 - row),
        _ => (row, DrawnWidth - 1 - column),
    };

    /// <summary>The named anchor's offset from the corner of the turned footprint, or <see langword="null"/> when the stamp has no such anchor.</summary>
    /// <param name="name">The anchor's name.</param>
    /// <param name="facing">The side the stamp faces.</param>
    public (int Dx, int Dz)? AnchorOffset(string name, Direction facing) =>
        Anchors is not null && Anchors.TryGetValue(name, out var cell) ? Turn(facing, cell.Column, cell.Row) : null;

    /// <summary>
    /// Every drawn cell of the stamp (a space leaves the cell alone and is not listed), turned to face <paramref name="facing"/>, in the order the scene builds them (layer by layer,
    /// row by row, column by column), with its offset from the corner of the turned footprint, its layer and its block.
    /// </summary>
    /// <param name="facing">The side the stamp faces.</param>
    public IEnumerable<(int Dx, int Dz, int Layer, BlockId Block)> Cells(Direction facing)
    {
        for (var layer = 0; layer < Layers.Count; layer++)
        {
            var rows = Layers[layer];
            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < rows[r].Length; c++)
                {
                    if (rows[r][c] == ' ')
                    {
                        continue;
                    }

                    var (dx, dz) = Turn(facing, c, r);
                    yield return (dx, dz, layer, Legend[rows[r][c]]);
                }
            }
        }
    }
}
