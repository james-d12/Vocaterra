using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>The land rules' mask (protected, eligible per column) and the plot check, on the bundle's route with real coordinates (seed 42 unless a test says otherwise).</summary>
public sealed class LandRulesTests
{
    private static LandRules RulesOf(Scene scene) => LandRules.Create(TestGame.Data.Land, TestGame.Data.Scene, scene);

    private static (Scene Scene, LandRules Rules) Seed42()
    {
        var scene = TestGame.Route(LandTestScene.Seed);
        return (scene, RulesOf(scene));
    }

    [Fact]
    public void Given_TheVillage_When_TheMaskIsBuilt_Then_ItsCentreAndTheEdgeOfItsBlendPlusMarginAreProtectedAsVillage()
    {
        var (scene, rules) = Seed42();
        var village = scene.Point("village");

        Assert.Equal(Protection.Village, rules.ProtectionAt(village.X, village.Z));
        Assert.Equal(Protection.Village, rules.ProtectionAt(village.X + 34, village.Z)); // radius 24 + blend 8 + margin 2
        Assert.Equal(Protection.Village, rules.ProtectionAt(village.X, village.Z - 34));
        Assert.NotEqual(Protection.Village, rules.ProtectionAt(village.X + 35, village.Z));
        Assert.NotEqual(Protection.Village, rules.ProtectionAt(village.X - 36, village.Z - 36)); // the corner of the square is outside the disc
    }

    [Fact]
    public void Given_LunasCamp_When_TheMaskIsBuilt_Then_ItsDiscOfTwelvePlusTwoIsProtectedAsCamp()
    {
        var (scene, rules) = Seed42();
        var camp = scene.Point("camp.guide");

        Assert.Equal(Protection.Camp, rules.ProtectionAt(camp.X, camp.Z));
        Assert.Equal(Protection.Camp, rules.ProtectionAt(camp.X, camp.Z + 14));
        Assert.NotEqual(Protection.Camp, rules.ProtectionAt(camp.X, camp.Z + 15));
    }

    [Fact]
    public void Given_APathBlockOfTheRoad_When_TheMaskIsBuilt_Then_EveryColumnWithinTwoOfItIsProtected()
    {
        var (scene, rules) = Seed42();
        var path = scene.Grid.BlocksOfKind(BlockId.Path).First(b => rules.ProtectionAt(b.X, b.Z) == Protection.Road);

        for (var dz = -2; dz <= 2; dz++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                Assert.True(rules.IsProtected(path.X + dx, path.Z + dz), $"({path.X + dx}, {path.Z + dz}) beside the path block {path}");
            }
        }
    }

    [Fact]
    public void Given_EveryPathAndBridgeBlock_When_TheMaskIsBuilt_Then_AllOfThemAreProtected()
    {
        var (scene, rules) = Seed42();

        var bridge = scene.Grid.BlocksOfKind(BlockId.Bridge);
        Assert.NotEmpty(bridge);
        Assert.All(bridge, b => Assert.True(rules.IsProtected(b.X, b.Z), $"the bridge block {b}"));
        Assert.All(scene.Grid.BlocksOfKind(BlockId.Path), b => Assert.True(rules.IsProtected(b.X, b.Z), $"the path block {b}"));
        Assert.Equal(Protection.Road, rules.ProtectionAt(bridge[0].X, bridge[0].Z));
    }

    [Fact]
    public void Given_TheMeadows_When_TheMaskIsBuilt_Then_TheirCentresAreEligibleAndNotProtected()
    {
        var (scene, rules) = Seed42();

        foreach (var name in new[] { "meadow.north", "meadow.south" })
        {
            var centre = scene.Point(name);
            Assert.True(rules.IsEligible(centre.X, centre.Z), name);
            Assert.False(rules.IsProtected(centre.X, centre.Z), name);
        }

        var north = scene.Point("meadow.north");
        Assert.True(rules.IsEligible(north.X + 22, north.Z));
        Assert.False(rules.IsEligible(north.X + 23, north.Z));
    }

    [Fact]
    public void Given_AColumnFarInTheWild_When_TheMaskIsAsked_Then_ItIsNeitherProtectedNorEligible()
    {
        var (_, rules) = Seed42();

        Assert.False(rules.IsProtected(150, 250));
        Assert.False(rules.IsEligible(150, 250));
        Assert.False(rules.IsProtected(-5, 10)); // outside the world: not protected, not eligible
        Assert.False(rules.IsEligible(-5, 10));
    }

    [Fact]
    public void Given_TheRiverBetweenTheWaterfallAndTheLake_When_TheMaskIsAsked_Then_NoPointOfItsCourseIsProtectedBesideTheBridge()
    {
        var (_, rules) = Seed42();
        var course = TestGame.Data.Scene.River!.Path.Where(p => p.X != 232 || p.Z != 205).ToList();

        Assert.Equal(11, course.Count);
        Assert.All(course, p => Assert.False(rules.IsProtected(p.X, p.Z), $"the river at ({p.X}, {p.Z})"));
    }

    [Fact]
    public void Given_ThePlotFitsOfTheMeadows_When_TheyAreCountedOnTheFourSeeds_Then_AtLeastThreePlotsFitInEach()
    {
        foreach (var seed in new ulong[] { 42, 7, 1234, 2025 })
        {
            var scene = TestGame.Route(seed);
            var rules = RulesOf(scene);
            foreach (var name in new[] { "meadow.north", "meadow.south" })
            {
                var fit = PlotFit.Best(rules, scene.Grid, scene.Point(name).X, scene.Point(name).Z, 22);

                Assert.True(fit >= 3, $"seed {seed}, {name}: {fit} plots fit");
            }
        }
    }

    [Fact]
    public void Given_AValidSpotOfTheNorthMeadow_When_ThePlotIsChecked_Then_TheAnswerIsYes()
    {
        var (scene, rules) = Seed42();
        var (x, z) = LandTestScene.NorthPlot;

        var check = rules.CheckPlot(scene.Grid, rules.PlotAt(x, z));

        Assert.True(check.Ok);
        Assert.Equal(PlotFault.None, check.Fault);
        Assert.Equal(new Plot(x, z, 10, 10), rules.PlotAt(x, z));
        Assert.Equal(new Plot(x, z, 10, 10), rules.PlotAround(x + 5, z + 5));
    }

    [Theory]
    [InlineData(285, 210, PlotFault.Protected, Protection.Village)]
    [InlineData(170, 180, PlotFault.Protected, Protection.Camp)]
    [InlineData(150, 250, PlotFault.OutsideMeadows, Protection.None)]
    [InlineData(-3, 10, PlotFault.OutsideMeadows, Protection.None)]
    [InlineData(264, 159, PlotFault.Tree, Protection.None)]
    [InlineData(255, 164, PlotFault.NotGround, Protection.None)]
    [InlineData(255, 165, PlotFault.TooSteep, Protection.None)]
    [InlineData(247, 245, PlotFault.Water, Protection.None)]
    public void Given_APlotOnSeed42_When_ItIsChecked_Then_TheAnswerIsTheReason(int x, int z, PlotFault fault, Protection protection)
    {
        var (scene, rules) = Seed42();

        var check = rules.CheckPlot(scene.Grid, rules.PlotAt(x, z));

        Assert.Equal(new PlotCheck(fault, protection), check);
        Assert.False(check.Ok);
    }

    [Fact]
    public void Given_APlotOnTheRoad_When_ItIsChecked_Then_TheReasonIsProtectedAsRoad()
    {
        var (scene, rules) = Seed42();
        var path = scene.Grid.BlocksOfKind(BlockId.Path).First(b => rules.ProtectionAt(b.X, b.Z) == Protection.Road);

        Assert.Equal(new PlotCheck(PlotFault.Protected, Protection.Road), rules.CheckPlot(scene.Grid, rules.PlotAround(path.X, path.Z)));
    }

    [Fact]
    public void Given_APlotWithAPlankPlacedOnIt_When_ItIsChecked_Then_TheTopBlockIsNoGround()
    {
        var (scene, rules) = Seed42();
        var (x, z) = LandTestScene.NorthPlot;
        var plot = rules.PlotAt(x, z);
        var top = LandRules.GroundOf(scene.Grid, x + 3, z + 3).Y;
        Assert.True(rules.CheckPlot(scene.Grid, plot).Ok);

        _ = scene.Grid.SetBlock(x + 3, top + 1, z + 3, BlockId.Planks);

        Assert.Equal(PlotFault.NotGround, rules.CheckPlot(scene.Grid, plot).Fault);
    }

    [Fact]
    public void Given_ACrownOfLeavesOverAColumn_When_ThePlotIsChecked_Then_TheReasonIsATree()
    {
        var (scene, rules) = Seed42();
        var (x, z) = LandTestScene.NorthPlot;
        var plot = rules.PlotAt(x, z);
        var top = LandRules.GroundOf(scene.Grid, x + 4, z + 4).Y;

        _ = scene.Grid.SetBlock(x + 4, top + 10, z + 4, BlockId.Leaves);

        Assert.Equal(PlotFault.Tree, rules.CheckPlot(scene.Grid, plot).Fault);
        _ = scene.Grid.SetBlock(x + 4, top + 10, z + 4, BlockId.Air);
        _ = scene.Grid.SetBlock(x + 4, top + 30, z + 4, BlockId.Leaves); // far above the probe's 24 blocks
        Assert.True(rules.CheckPlot(scene.Grid, plot).Ok);
    }

    [Fact]
    public void Given_TheRulesAndTheScene_When_TwoMasksAreBuilt_Then_EveryColumnAgrees()
    {
        var (scene, first) = Seed42();
        var second = RulesOf(scene);

        for (var z = 0; z < scene.Grid.SizeZ; z += 3)
        {
            for (var x = 0; x < scene.Grid.SizeX; x += 3)
            {
                Assert.Equal((first.ProtectionAt(x, z), first.IsEligible(x, z)), (second.ProtectionAt(x, z), second.IsEligible(x, z)));
            }
        }
    }
}
