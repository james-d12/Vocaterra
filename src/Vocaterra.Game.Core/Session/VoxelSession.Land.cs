using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Session;

// The player's land (Y09, D-game-32 (c)): the rules the simulation asks (the village, the roads and Luna's camp refuse blocks; planks come back only on owned land) and the
// two plots the later milestones fill in: the pending one the stake marks (Y10) and the owned one the mayor sells (Y11). No item, no coins and no outline here.
public sealed partial class VoxelSession
{
    /// <summary>The hint for a block refused in the village (<c>ui.landVillage</c>).</summary>
    public const string LandVillageHint = "landVillage";

    /// <summary>The hint for a block refused at Luna's camp (<c>ui.landCamp</c>).</summary>
    public const string LandCampHint = "landCamp";

    /// <summary>The hint for a block refused on a road or the bridge (<c>ui.landRoad</c>).</summary>
    public const string LandRoadHint = "landRoad";

    /// <summary>The hint for a plank that cannot be taken back because the land is not the player's (<c>ui.landTakeBack</c>).</summary>
    public const string LandTakeBackHint = "landTakeBack";

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
