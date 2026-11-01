using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Figures;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.Tests.UI.Hud;

/// <summary>Which NPCs show their name plate (<see cref="NamePlates.Select"/>, V22): the one the player faces within talking range and the beacon's, no other.</summary>
public sealed class NamePlatesTests
{
    private static readonly NpcFigure Guide = new("guide", "Luna", new BlockPos(179, 42, 187), 0);
    private static readonly NpcFigure Baker = new("baker", "Panadera", new BlockPos(281, 38, 208), 0);
    private static readonly NpcFigure Villager = new("villager", "Aldeano", new BlockPos(293, 37, 221), 0);
    private static readonly IReadOnlyList<NpcFigure> All = [Guide, Baker, Villager];

    [Fact]
    public void NothingFacedAndNoBeaconShowsNoPlate() => Assert.Empty(NamePlates.Select(All, null, null, null));

    [Fact]
    public void AnNpcInRangeButNotFacedShowsNoPlate() => Assert.Empty(NamePlates.Select(All, null, new BlockPos(0, 0, 0), null)); // Aimed is null when the crosshair is not on anyone, whoever stands near

    [Fact]
    public void TheFacedNpcShowsOnePlate() => Assert.Equal([Baker], NamePlates.Select(All, Baker, null, null));

    [Fact]
    public void TheBeaconsNpcShowsItsPlateFromFarAway() => Assert.Equal([Villager], NamePlates.Select(All, null, Villager.Feet, null));

    [Fact]
    public void TheFacedNpcThatIsTheBeaconsShowsOnePlate() => Assert.Equal([Baker], NamePlates.Select(All, Baker, Baker.Feet, null));

    [Fact]
    public void AFacedNpcAndAnotherBeaconNpcShowTwoPlatesFacedFirst() => Assert.Equal([Baker, Villager], NamePlates.Select(All, Baker, Villager.Feet, null));

    [Fact]
    public void ABeaconOnNoNpcShowsNoPlate() => Assert.Empty(NamePlates.Select(All, null, new BlockPos(176, 42, 186), null));

    [Fact]
    public void TheSpeakerHasNoPlateBecauseTheBubbleHasHisName() => Assert.Equal([Villager], NamePlates.Select(All, Baker, Villager.Feet, "baker"));
}
