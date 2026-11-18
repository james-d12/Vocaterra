using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The guide's window onto a streamed world (B06): the region inside a region, a box of 128 blocks round and 64 above and below the player outside one, and facts that read only the window.</summary>
public sealed class WorldWindowTests
{
    [Fact]
    public void Given_TheBoundedValley_When_TheWindowIsAsked_Then_ItIsTheWholeGridWhereverThePlayerStands()
    {
        var scene = RouteScene.Build();

        Assert.Equal(new WorldWindow(0, 0, 0, 384, 128, 384), WorldWindow.For(scene, 10, 20, 30));
        Assert.Equal(new WorldWindow(0, 0, 0, 384, 128, 384), WorldWindow.For(scene, 900, 20, 900));
    }

    [Fact]
    public void Given_APlayerInsideTheValleyOfAStreamedWorld_When_TheWindowIsAsked_Then_ItIsTheRegionWhateverTheHeight()
    {
        var scene = RegionTestWorld.StreamedScene();
        var valley = WorldWindow.Of(RegionTestWorld.Bounds);

        Assert.Equal(new WorldWindow(0, 0, 0, 384, 128, 384), valley);
        Assert.Equal(valley, WorldWindow.For(scene, 200, 60, 200));
        Assert.Equal(valley, WorldWindow.For(scene, 383, 90, 0));
        Assert.Equal(valley, WorldWindow.For(scene, 5, 200, 380));
    }

    [Fact]
    public void Given_APlayerOutsideEveryRegion_When_TheWindowIsAsked_Then_ItIsTheBoxOfRadiusSixtyFourBelowAndAboveInWholeChunks()
    {
        var scene = RegionTestWorld.StreamedScene();

        var window = WorldWindow.For(scene, 1000, 90, -500);

        Assert.Equal(new WorldWindow(864, 16, -640, 1136, 160, -368), window); // x 872..1128, y 26..154, z -628..-372, grown to whole chunks
        Assert.All(new[] { window.MinX, window.MinY, window.MinZ, window.EndX, window.EndY, window.EndZ }, v => Assert.Equal(0, v & 15));
        Assert.True(window.Contains(1000 - WorldWindow.Radius, 90 - WorldWindow.Below, -500 + WorldWindow.Radius));
        Assert.True(window.Contains(1000 + WorldWindow.Radius, 90 + WorldWindow.Above, -500 - WorldWindow.Radius));
        Assert.False(window.Contains(1000, 90, 100));
        Assert.Equal((128, 64, 64), (WorldWindow.Radius, WorldWindow.Below, WorldWindow.Above));
    }

    [Fact]
    public void Given_APlayerNearTheLimits_When_TheWindowIsAsked_Then_ItIsCutToTheWorld()
    {
        var scene = RegionTestWorld.StreamedScene();
        var limits = scene.Grid.Limits;

        var top = WorldWindow.For(scene, 29_990, 315, -29_990);
        var bottom = WorldWindow.For(scene, -29_990, -60, 29_990);

        Assert.Equal(limits.EndX, top.EndX);
        Assert.Equal(limits.EndY, top.EndY);
        Assert.Equal(limits.MinZ, top.MinZ);
        Assert.Equal(limits.MinX, bottom.MinX);
        Assert.Equal(limits.MinY, bottom.MinY);
        Assert.Equal(limits.EndZ, bottom.EndZ);
    }

    [Fact]
    public void Given_AWindowOutsideTheValley_When_TheNearestStoneIsAsked_Then_ItIsFoundInsideTheWindowAndTheLiveAndSnapshotFactsAgree()
    {
        var scene = RegionTestWorld.StreamedScene();
        var stone = TestGame.Nouns.Nouns.First(static n => n.Blocks.Contains(BlockId.Stone));
        var (x, y, z) = (1000, 40, 1000);
        var window = WorldWindow.For(scene, x, y, z);
        var from = new GuideViewpoint(new GuideCell(x, y, z), ((long)x * WorldFacts.UnitsPerBlock) + WorldFacts.HalfBlock, ((long)z * WorldFacts.UnitsPerBlock) + WorldFacts.HalfBlock, 0);

        var snapshot = WorldFacts.Snapshot(scene.Grid, TestGame.Nouns, TestGame.Data.Words.Directions, window).NearestOfNoun(from, stone.Lemma);
        var live = WorldFacts.Live(scene.Grid, TestGame.Nouns, TestGame.Data.Words.Directions, v => WorldWindow.For(scene, v.Cell.X, v.Cell.Y, v.Cell.Z)).NearestOfNoun(from, stone.Lemma);

        Assert.NotNull(snapshot);
        Assert.True(window.Contains(snapshot.Cell.X, snapshot.Cell.Y, snapshot.Cell.Z));
        Assert.Equal(snapshot, live);
        Assert.Equal(BlockId.Stone, scene.Grid.GetBlock(snapshot.Cell.X, snapshot.Cell.Y, snapshot.Cell.Z));
    }

    [Fact]
    public void Given_AStreamedGridAndNoWindow_When_FactsAreAsked_Then_TheWorldIsRefusedAsTooLargeToSearch()
    {
        var scene = RegionTestWorld.StreamedScene();

        Assert.Throws<NotSupportedException>(() => WorldFacts.Live(scene.Grid, TestGame.Nouns, TestGame.Data.Words.Directions));
        Assert.Throws<NotSupportedException>(() => WorldFacts.Snapshot(scene.Grid, TestGame.Nouns, TestGame.Data.Words.Directions));
    }
}
