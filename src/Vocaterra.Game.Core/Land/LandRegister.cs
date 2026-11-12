using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Land;

/// <summary>
/// The player's land in a session (Y09): the rules, the pending plot (marked by a stake, Y10) and the owned plot (bought from the mayor, Y11), and the answers the simulation's rules
/// hook needs. One plot at most of each kind. The state is saved as <c>systems.land</c>; the rules are rebuilt from the scene and the bundle.
/// </summary>
public sealed class LandRegister : IBuildRules
{
    /// <summary>Starts a register with no pending and no owned plot.</summary>
    /// <param name="rules">The rules of the scene.</param>
    public LandRegister(LandRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = rules;
    }

    /// <summary>The rules: the mask, the plot's numbers and <see cref="LandRules.CheckPlot"/>.</summary>
    public LandRules Rules { get; }

    /// <summary>The plot the player has marked and not bought, or <see langword="null"/>.</summary>
    public Plot? Pending { get; internal set; }

    /// <summary>The plot the player owns, or <see langword="null"/>.</summary>
    public Plot? Owned { get; internal set; }

    /// <summary>The camp the player set up on the owned plot (Y12), or <see langword="null"/>: the session sets it.</summary>
    public CampPlan? Camp { get; internal set; }

    /// <summary>Whether the column lies in the owned plot.</summary>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public bool IsOwned(int x, int z) => Owned is { } plot && plot.Contains(x, z);

    /// <inheritdoc />
    public Protection ProtectionAt(int x, int z) => Rules.ProtectionAt(x, z);

    /// <inheritdoc />
    public bool MayTakeBack(BlockPos cell) => IsOwned(cell.X, cell.Z);

    /// <inheritdoc />
    public bool IsCampBlock(BlockPos cell) => Camp?.IsBlock(cell) == true;

    /// <summary>The entry a save keeps, or <see langword="null"/> when there is no plot to keep.</summary>
    internal LandSave? Capture() => Pending is null && Owned is null ? null : new LandSave(Pending is { } p ? PlotSave.Of(p) : null, Owned is { } o ? PlotSave.Of(o) : null);

    /// <summary>Puts the entry back as saved (no check: a plot bought stays bought). No entry is no plot.</summary>
    /// <param name="save">The entry, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">A plot has no size or lies outside the world.</exception>
    internal void Restore(LandSave? save, VoxelGrid grid)
    {
        Pending = save?.Pending is { } pending ? pending.ToPlot(grid, "pending") : null;
        Owned = save?.Owned is { } owned ? owned.ToPlot(grid, "owned") : null;
    }
}

/// <summary>
/// What the land keeps in a save (<c>systems.land</c>, Y09): the pending plot and the owned one, each written only when it exists. The entry is left out when neither exists.
/// </summary>
/// <param name="Pending">The plot the player has marked with a stake and not bought (Y10), or <see langword="null"/>.</param>
/// <param name="Owned">The plot the player owns (Y11), or <see langword="null"/>.</param>
public sealed record LandSave(
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] PlotSave? Pending = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] PlotSave? Owned = null);

/// <summary>A plot in a save: its west-north corner and its size, in blocks.</summary>
/// <param name="X">The west column.</param>
/// <param name="Z">The north row.</param>
/// <param name="Width">The east-west size.</param>
/// <param name="Depth">The north-south size.</param>
public sealed record PlotSave(int X, int Z, int Width, int Depth)
{
    internal static PlotSave Of(Plot plot) => new(plot.X, plot.Z, plot.Width, plot.Depth);

    internal Plot ToPlot(VoxelGrid grid, string which)
    {
        if (Width < 1 || Depth < 1 || !grid.InBounds(X, 0, Z) || !grid.InBounds(X + Width - 1, 0, Z + Depth - 1))
        {
            throw new ArgumentException($"The {which} plot ({X}, {Z}, {Width} by {Depth}) is empty or outside the world.");
        }

        return new Plot(X, Z, Width, Depth);
    }
}
