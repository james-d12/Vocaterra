using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Water;

/// <summary>What one of the four cells that meet at a corner of a water surface is, for the corner's height.</summary>
public enum CornerKind : byte
{
    /// <summary>A solid block: it does not count (the water meets a wall there, so the corner keeps the height of the water beside it).</summary>
    Solid,

    /// <summary>Air or anything else that does not hold water: it counts as a height of nothing, so a sheet slopes down to its open edge.</summary>
    Open,

    /// <summary>A water cell, of the level given.</summary>
    Water,
}

/// <summary>One of the four cells that meet at a corner of a water surface.</summary>
/// <param name="Kind">What the cell is.</param>
/// <param name="Level">The level of the cell (read only when it is water).</param>
/// <param name="WaterAbove">Whether the cell above it holds water (then the corner is full, so a column of water has no step in it).</param>
public readonly record struct CornerCell(CornerKind Kind, WaterLevel Level = default, bool WaterAbove = false)
{
    /// <summary>A solid block.</summary>
    public static CornerCell Solid => new(CornerKind.Solid);

    /// <summary>Air, undergrowth or any other cell without water.</summary>
    public static CornerCell Open => new(CornerKind.Open);

    /// <summary>A water cell.</summary>
    public static CornerCell Water(WaterLevel level, bool waterAbove = false) => new(CornerKind.Water, level, waterAbove);
}

/// <summary>
/// The height of one corner of a water surface (water-plan 4.8), worked out as Minecraft does: the four cells that meet at the corner are averaged, so a
/// spreading sheet slopes smoothly from the source to its thin edge. Each water cell counts with its <see cref="WaterSurface"/> height; a source or a full
/// cell weighs ten times a flowing one, so a river bank is not pulled down by one thin cell; open cells count as a height of nothing (weight one); solid cells do
/// not count. If any of the four has water above it the corner is full. Pure, in whole 256ths of a block, so it can be tested to the digit.
/// </summary>
public static class WaterCorners
{
    /// <summary>The units a block is divided into.</summary>
    public const int PerBlock = 256;

    // A cell at least this high (13/16, which a source at 14/16 and a full cell reach and a flowing cell does not) weighs <HeavyWeight>.
    private const int HeavySixteenths = 13;
    private const int HeavyWeight = 10;

    /// <summary>The height of the corner in 256ths of a block above the floor of the cells, from the four cells that meet there.</summary>
    public static int Height(CornerCell a, CornerCell b, CornerCell c, CornerCell d)
    {
        if (a.WaterAbove || b.WaterAbove || c.WaterAbove || d.WaterAbove)
        {
            return PerBlock;
        }

        var total = 0;
        var weight = 0;
        Add(a, ref total, ref weight);
        Add(b, ref total, ref weight);
        Add(c, ref total, ref weight);
        Add(d, ref total, ref weight);
        return weight == 0 ? 0 : total * (PerBlock / WaterSurface.Full) / weight;
    }

    private static void Add(CornerCell cell, ref int total, ref int weight)
    {
        switch (cell.Kind)
        {
            case CornerKind.Water:
                var sixteenths = WaterSurface.Sixteenths(cell.Level, false);
                var w = sixteenths >= HeavySixteenths ? HeavyWeight : 1;
                total += sixteenths * w;
                weight += w;
                break;
            case CornerKind.Open:
                weight++;
                break;
            default:
                break;
        }
    }
}
