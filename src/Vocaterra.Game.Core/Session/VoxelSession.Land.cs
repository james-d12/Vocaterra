using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Npc;
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
    private const string PlotBeaconKey = "plotBeacon";

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
            PointAtSeller(); // Y11: a marked plot is bought from the mayor
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

    /// <summary>The ui key of the hint said when the sale did not go through at the last moment (Y11).</summary>
    public const string PlotSaleFailedHint = "plotSaleFailed";

    private const string PlotBoughtHint = "plotBought";

    /// <summary>
    /// The land's facts as a turn needs them (Y11): whether land is owned, whether the marked plot passes the land rules as the world is now (a plot a tree grew on, or a block was placed on,
    /// does not), and the price. Plain values, taken on the game thread when a line is submitted.
    /// </summary>
    protected override PlotFacts? PlotFactsNow() =>
        new(Land.Owned is not null, Land.Pending is { } pending && Land.Rules.CheckPlot(Scene.Grid, pending).Ok, Land.Rules.PlotPrice);

    /// <summary>The play session's objective that waits for the plot to be bought (the one with a step on <c>plot_bought</c>), or <see langword="null"/> when the bundle has none.</summary>
    private Objective? LandObjective => Objectives.FirstOrDefault(static o => o.Spec.Steps.Any(static s => string.Equals(s.Trigger.Event, WorldEvents.PlotBought, StringComparison.Ordinal)));

    /// <summary>
    /// The offer the NPC who sells the plot opens the talk with (Y11): the stake while the player has none and no plot, marked or owned; the plot once one is marked. Only from the play session the
    /// land's checklist is shown from (day 2: before it the mayor is the villager he was). <see langword="null"/> for any other NPC, and when there is nothing to offer.
    /// </summary>
    /// <param name="npc">The NPC the panel opens on.</param>
    protected override string? LandOfferAt(string npc)
    {
        if (!string.Equals(Data.HostEvents.NpcWith(NpcEffectKind.SellPlot), npc, StringComparison.Ordinal) || LandObjective is not { } objective || !Shown(objective.Spec) || Needs.IsDead || Land.Owned is not null)
        {
            return null;
        }

        if (Land.Pending is not null)
        {
            return NpcHostEvents.Of(npc, NpcHostEvents.OfferPlot);
        }

        return Simulation.Inventory.Count(ItemId.Stake) == 0 ? NpcHostEvents.Of(npc, NpcHostEvents.OfferStake) : null;
    }

    /// <summary>
    /// The exchange of the sale (Y11, <see cref="NpcEffectKind.SellPlot"/>): the price in coins out, the plot owned and the camp kit in, as one operation that cannot half happen. The order: first the
    /// checks of the exchange (the land is not owned, a plot is marked and passes the rules, the coins cover the price, the bag has room for the kit once the coins are out:
    /// <see cref="NpcEffect.PlotMiss"/> and <see cref="Items.Inventory.CanExchange"/>); then <see cref="OwnPlot"/>, which checks the world once more and may refuse; then the coins and the kit. A refusal at
    /// any step leaves the coins, the bag and the land as they were; the mayor answered the miss on the worker, and a refusal that only the session sees (the world moved since the turn began) says
    /// so on the hint line. A repeated «sí», a reload or a second talk finds the land owned and changes nothing (<c>already_owned</c>).
    /// </summary>
    /// <param name="npc">The seller's agent id.</param>
    /// <param name="effect">The sale (its item is the camp kit).</param>
    /// <param name="how">How it was asked for, for the log.</param>
    /// <returns>Whether the plot was bought.</returns>
    internal bool SellPlot(string npc, NpcEffect effect, string how)
    {
        var price = Land.Rules.PlotPrice;
        var bag = Simulation.Inventory;
        if (Needs.IsDead || effect.PlotMiss(bag, PlotFactsNow()) is not null || !bag.CanExchange(ItemId.Coin, price, effect.Item, effect.Count))
        {
            return false;
        }

        var plot = Land.Pending!.Value;
        if (!OwnPlot())
        {
            _hints.Add(Locale.Ui(PlotSaleFailedHint));
            return false;
        }

        Take(npc, ItemId.Coin, price, how);
        _ = Give(npc, effect.Item, effect.Count, how);
        LogWorld(WorldEvents.PlotBought, npc, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{plot.X},{plot.Z}"));
        _hints.Add(Locale.Ui(PlotBoughtHint, ("price", price.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        PointAtPlot(plot);
        return true;
    }

    // The arrow goes to the middle of the owned plot (Y11): the pointer takes any cell, so a plot needs no named place of the scene. Its word is the plot's noun (ui.plotBeacon: «terreno»).
    private void PointAtPlot(Plot plot)
    {
        var (x, z) = plot.Centre;
        var y = LandRules.GroundOf(Scene.Grid, x, z).Y + 1;
        var label = Locale.Ui(PlotBeaconKey);
        Pointer = new PointerMarker(label, new GuideCell(x, y, z));
        LogWorld(WorldEvents.BeaconSet, null, $"{label} at {x},{y},{z}");
    }

    // The arrow goes to the NPC who sells the plot (Y11), when a plot is marked and not yet bought: the mayor stands at his place of the scene, and shows his label.
    private void PointAtSeller()
    {
        if (Data.HostEvents.NpcWith(NpcEffectKind.SellPlot) is not { } npc || Data.Npcs.Find(npc) is not { } entry)
        {
            return;
        }

        var feet = Scene.Npc(npc).Feet;
        Pointer = new PointerMarker(entry.Label, new GuideCell(feet.X, feet.Y, feet.Z));
        LogWorld(WorldEvents.BeaconSet, npc, $"{entry.Label} at {feet.X},{feet.Y},{feet.Z}");
    }

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
