namespace Vocaterra.Game.Core.Land;

/// <summary>
/// A plot of land: a rectangle of columns along the world's axes (Y09). It has no height: the columns are the plot, whatever stands on them. The pending plot (marked by a stake,
/// Y10) and the owned one (bought from the mayor, Y11) are both a <see cref="Plot"/>.
/// </summary>
/// <param name="X">The west column (the smallest x).</param>
/// <param name="Z">The north row (the smallest z).</param>
/// <param name="Width">The east-west size in blocks.</param>
/// <param name="Depth">The north-south size in blocks.</param>
public readonly record struct Plot(int X, int Z, int Width, int Depth)
{
    /// <summary>Whether the column lies in the plot.</summary>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public bool Contains(int x, int z) => x >= X && x < X + Width && z >= Z && z < Z + Depth;

    /// <summary>The plot's middle column (the one at half its width and depth from its corner).</summary>
    public (int X, int Z) Centre => (X + (Width / 2), Z + (Depth / 2));
}

/// <summary>Why a plot cannot be marked or bought where it is (Y09), in the order <see cref="LandRules.CheckPlot"/> finds them.</summary>
public enum PlotFault
{
    /// <summary>No fault: the plot may lie there.</summary>
    None = 0,

    /// <summary>A column is protected (the village, the roads, Luna's camp); <see cref="PlotCheck.Protection"/> says which.</summary>
    Protected = 1,

    /// <summary>A column is outside the eligible regions (the meadows), or outside the world.</summary>
    OutsideMeadows = 2,

    /// <summary>A column has water on top.</summary>
    Water = 3,

    /// <summary>A tree stands on a column (a trunk or leaves above it).</summary>
    Tree = 4,

    /// <summary>A column's top block is not grass or dirt (stone, sand, a path, a block someone placed).</summary>
    NotGround = 5,

    /// <summary>The highest and lowest ground of the plot differ by more than the slope allows.</summary>
    TooSteep = 6,
}

/// <summary>The answer of <see cref="LandRules.CheckPlot"/>: yes, or the reason.</summary>
/// <param name="Fault">The first fault found, or <see cref="PlotFault.None"/>.</param>
/// <param name="Protection">For <see cref="PlotFault.Protected"/>, which protection the first protected column has; otherwise <see cref="Land.Protection.None"/>.</param>
public readonly record struct PlotCheck(PlotFault Fault, Protection Protection = Protection.None)
{
    /// <summary>Whether the plot may lie there.</summary>
    public bool Ok => Fault == PlotFault.None;

    /// <summary>The answer yes.</summary>
    public static PlotCheck Yes => new(PlotFault.None);
}
