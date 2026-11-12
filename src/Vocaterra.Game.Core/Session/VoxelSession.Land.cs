using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Session;

// The player's land (Y09, D-game-32 (c)): the rules the simulation asks (the village, the roads and Luna's camp refuse blocks; planks come back only on owned land) and the
// two plots: the pending one the stake marks (Y10, <see cref="UseStake"/>) and the owned one the mayor sells (Y11). The outline and the HUD line are the game's; no coins here.
public sealed partial class VoxelSession
{
    /// <summary>The hint for a block refused in the village (<c>ui.landVillage</c>).</summary>
    public const string LandVillageHint = "landVillage";

    /// <summary>The hint for a block refused at Luna's camp (<c>ui.landCamp</c>).</summary>
    public const string LandCampHint = "landCamp";

    /// <summary>The hint for a block refused on a road or the bridge (<c>ui.landRoad</c>).</summary>
    public const string LandRoadHint = "landRoad";

    /// <summary>How near the player stands to a plot, in blocks, for the HUD line to show (Y10; the outline is drawn from a little farther).</summary>
    public const int PlotHudReach = 24;

    // The ui keys of the stake's lines (content/locale/en/ui.json, Y10).
    private const string PlotVillageHint = "plotVillage";
    private const string PlotCampHint = "plotCamp";
    private const string PlotRoadHint = "plotRoad";
    private const string PlotOutsideHint = "plotOutside";
    private const string PlotWaterHint = "plotWater";
    private const string PlotTreeHint = "plotTree";
    private const string PlotNotGroundHint = "plotNotGround";
    private const string PlotSteepHint = "plotSteep";
    private const string PlotMarkedHint = "plotMarked";
    private const string PlotOwnedHereHint = "plotOwnedHere";
    private const string PlotOwnedAlreadyHint = "plotOwnedAlready";
    private const string PlotHudKey = "plotHud";
    private const string PlotHudOwnedKey = "plotHudOwned";

    /// <summary>The hint for a plank that cannot be taken back because the land is not the player's (<c>ui.landTakeBack</c>).</summary>
    public const string LandTakeBackHint = "landTakeBack";

    /// <summary>What using the stake on the ground did (Y10).</summary>
    public enum StakeResult
    {
        /// <summary>Nothing: no stake in the bag, the player is dead, or the cell is outside the world.</summary>
        NotApplied,

        /// <summary>The pending plot was marked (or moved) round the cell.</summary>
        Marked,

        /// <summary>The plot cannot lie there; the hint says why and the pending plot is as it was.</summary>
        Refused,

        /// <summary>The player owns land already: the stake marks nothing more, and the hint says so.</summary>
        AlreadyOwned,
    }

    /// <summary>The answer of <see cref="MarkPlot"/>: what happened and the land rules' check.</summary>
    /// <param name="Result">Marked, refused or already owned.</param>
    /// <param name="Check">The check of the plot round the cell (<see cref="PlotCheck.Yes"/> when marked; none is made when the land is owned).</param>
    public readonly record struct StakeUse(StakeResult Result, PlotCheck Check);

    /// <summary>The player's land: the rules, the pending plot and the owned one. The simulation's <see cref="Simulation.VoxelSimulation.BuildRules"/> is this register.</summary>
    public LandRegister Land { get; private set; } = null!;

    /// <summary>The plot the player has marked and not bought (Y10), or <see langword="null"/>.</summary>
    public Plot? PendingPlot => Land.Pending;

    /// <summary>The plot the player owns (Y11), or <see langword="null"/>.</summary>
    public Plot? OwnedPlot => Land.Owned;

    /// <summary>Whether the column lies in the plot the player owns.</summary>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public bool IsOwned(int x, int z) => Land.IsOwned(x, z);

    // Builds the rules from the scene and the bundle and hands the simulation its hook; called once, by the constructor.
    private void InitLand(Scene scene)
    {
        Land = new LandRegister(LandRules.Create(Data.Land, Data.Scene, scene));
        Simulation.BuildRules = Land;
    }

    /// <summary>
    /// Marks the plot with its west-north corner at the column as the pending plot (Y10 calls it with the corner of the plot round the ground the stake was set on) when
    /// <see cref="LandRules.CheckPlot"/> says yes; a plot that fails leaves the pending one as it was. Marking again replaces the pending plot and costs nothing.
    /// </summary>
    /// <param name="x">The west column.</param>
    /// <param name="z">The north row.</param>
    /// <returns>The check: yes, or the reason (a <see cref="PlotFault"/>, and for a protected column which protection).</returns>
    public PlotCheck SetPendingPlot(int x, int z)
    {
        var check = Land.Rules.CheckPlot(Scene.Grid, Land.Rules.PlotAt(x, z));
        if (check.Ok)
        {
            Land.Pending = Land.Rules.PlotAt(x, z);
        }

        return check;
    }

    /// <summary>
    /// Marks the pending plot round a ground column, the way the stake does (Y10): the column is the plot's middle (<see cref="LandRules.PlotAround"/>: with an even width the
    /// middle is the column east and south of the centre line), facing the world's axes. No hint, no item needed: the stake's use (<see cref="UseStake"/>) and the launch flag
    /// <c>--mark-plot</c> are the two callers. With land already owned nothing is marked.
    /// </summary>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public StakeUse MarkPlot(int x, int z)
    {
        if (Land.Owned is not null)
        {
            return new StakeUse(StakeResult.AlreadyOwned, PlotCheck.Yes);
        }

        var corner = Land.Rules.PlotAround(x, z);
        var check = SetPendingPlot(corner.X, corner.Z);
        return new StakeUse(check.Ok ? StakeResult.Marked : StakeResult.Refused, check);
    }

    /// <summary>
    /// The stake's use on the block the crosshair is on (Y10): marks the pending plot round that block's column (marking again moves the pending plot and costs nothing) and puts the
    /// answer on the hint line, the reason when the plot cannot lie there. The stake stays in the bag.
    /// </summary>
    /// <param name="ground">The block aimed at.</param>
    public StakeResult UseStake(BlockPos ground)
    {
        if (Needs.IsDead || !Scene.Grid.InBounds(ground.X, ground.Y, ground.Z) || Simulation.Inventory.Count(ItemId.Stake) < 1)
        {
            return StakeResult.NotApplied;
        }

        return MarkPlotWithHint(ground.X, ground.Z).Result;
    }

    /// <summary>
    /// <see cref="MarkPlot"/> with the answer said on the hint line, as the stake's use does (Y10); it needs no stake in the bag, so the launch flag <c>--mark-plot</c> calls it.
    /// </summary>
    /// <param name="x">The column's x.</param>
    /// <param name="z">The column's z.</param>
    public StakeUse MarkPlotWithHint(int x, int z)
    {
        var use = MarkPlot(x, z);
        _hints.Add(use.Result switch
        {
            StakeResult.AlreadyOwned => Locale.Ui(IsOwned(x, z) ? PlotOwnedHereHint : PlotOwnedAlreadyHint),
            StakeResult.Marked => Locale.Ui(PlotMarkedHint, ("width", Land.Pending!.Value.Width.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("depth", Land.Pending.Value.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("price", Land.Rules.PlotPrice.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            _ => PlotRefusalLine(use.Check),
        });
        return use;
    }

    /// <summary>
    /// The HUD line of the plot (Y10): <c>Plot 10 × 10 · price 10 coins</c> for the pending plot, <c>Your land</c> for the owned one, while the player stands within <see cref="PlotHudReach"/>
    /// blocks of the plot or holds the stake (the stake in hand shows the pending plot from anywhere); <see langword="null"/> when there is nothing to say.
    /// </summary>
    public string? PlotHudLine()
    {
        var feet = Simulation.Player.FeetBlock;
        var holdsStake = !Simulation.SelectedStack.IsEmpty && Simulation.SelectedStack.Item == ItemId.Stake;
        if (Land.Owned is { } owned && (holdsStake || Near(owned, feet.X, feet.Z)))
        {
            return Locale.Ui(PlotHudOwnedKey);
        }

        if (Land.Pending is { } pending && (holdsStake || Near(pending, feet.X, feet.Z)))
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return Locale.Ui(PlotHudKey, ("width", pending.Width.ToString(inv)), ("depth", pending.Depth.ToString(inv)), ("price", Land.Rules.PlotPrice.ToString(inv)));
        }

        return null;
    }

    private static bool Near(Plot plot, int x, int z)
    {
        var dx = Math.Max(Math.Max(plot.X - x, x - (plot.X + plot.Width - 1)), 0);
        var dz = Math.Max(Math.Max(plot.Z - z, z - (plot.Z + plot.Depth - 1)), 0);
        return (dx * dx) + (dz * dz) <= PlotHudReach * PlotHudReach;
    }

    /// <summary>The English line that says why a plot cannot be marked (Y10): a protection's own words (the village, the camp, the road), or the fault's (the meadows, water, a tree, the ground, the slope).</summary>
    /// <param name="check">A refused check.</param>
    public string PlotRefusalLine(PlotCheck check) => Locale.Ui(PlotRefusalHint(check));

    private static string PlotRefusalHint(PlotCheck check) => check.Fault switch
    {
        PlotFault.Protected => check.Protection switch
        {
            Protection.Village => PlotVillageHint,
            Protection.Camp => PlotCampHint,
            _ => PlotRoadHint,
        },
        PlotFault.OutsideMeadows => PlotOutsideHint,
        PlotFault.Water => PlotWaterHint,
        PlotFault.Tree => PlotTreeHint,
        PlotFault.NotGround => PlotNotGroundHint,
        _ => PlotSteepHint,
    };

    /// <summary>Takes the mark away (the pending plot is no more).</summary>
    public void ClearPendingPlot() => Land.Pending = null;

    /// <summary>
    /// Makes the pending plot the owned one (Y11 calls it in the exchange, after the coins are taken: this method moves no coins and gives no item). The plot is checked again against the world as
    /// it is now. Nothing changes, and false is returned, when there is no pending plot, a plot is owned already, or the check now fails.
    /// </summary>
    /// <returns>Whether the plot is owned now.</returns>
    public bool OwnPlot()
    {
        if (Land.Pending is not { } plot || Land.Owned is not null || !Land.Rules.CheckPlot(Scene.Grid, plot).Ok)
        {
            return false;
        }

        Land.Owned = plot;
        Land.Pending = null;
        return true;
    }

    // The hint a refused action says (a protection's own words), or null for none.
    private static string? LandHintFor(Protection protection) => protection switch
    {
        Protection.Village => LandVillageHint,
        Protection.Camp => LandCampHint,
        Protection.Road => LandRoadHint,
        _ => null,
    };

    /// <summary>The land entry of a save (<c>systems.land</c>), or <see langword="null"/> when there is no plot, pending or owned.</summary>
    internal LandSave? CaptureLand() => Land.Capture();

    /// <summary>Puts the land entry back (Y09); no entry is no plot.</summary>
    /// <param name="save">The entry, or <see langword="null"/>.</param>
    internal void RestoreLand(LandSave? save) => Land.Restore(save, Scene.Grid);
}
