using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.Streaming;
using Vocaterra.Game.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>Tests split out of FarViewTests (B28), so they run beside the others: SectionChangedWhile, RealChunksComeIntoTheStore, FillerTile, NewerMeshWaits, NoColumnDrawnYet.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarViewChangedSectionTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    [Fact]
    public void Given_ASettledView_When_RealChunksComeIntoTheStore_Then_TheTileOverThemIsMadeAgainAndTheRestAreKept()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));
        Land(view, 192, 192);
        var nearest = view.Draw.First(static t => t.Key.Level == 0);
        var built = view.Stats.Built;
        Assert.True(nearest.Triangles > 0);

        // The real chunks over the tile's footprint are all air (a quarried-away hill, say): the tile must follow.
        view.World.IngestStack(nearest.Key.Tx, nearest.Key.Tz, (_, _, _) => null);
        view.Update(192, 192, Voxel, Release, null);

        Assert.True(view.Stale > 0 || view.Stats.Built > built);
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));
        Land(view, 192, 192);
        Assert.Equal(0, view.Stale);
        Assert.Equal(0, view.Failed);
        Assert.Equal(0, nearest.Triangles);
        Assert.InRange(view.Stats.Built - built, 1, 6); // that tile, and the coarser tiles that came to hold the real section above it
        Assert.Empty(_released);
    }

    [Fact]
    public void Given_ASectionChangedWhileItsTileIsBuilding_When_TheViewSettles_Then_TheTileIsMadeAgain()
    {
        using var pool = new JobPool(0);
        var store = new Vocaterra.Game.Core.World.Lod.LodStore();
        var view = new FarView(StreamedWorlds.Composition.World.Far, Small, pool, store);
        view.Update(192, 192, Voxel, Release, 0); // everything is asked for and (the budget being nothing) at most one is built
        var key = FarTileSelector.Select(Small, 192, 192, Voxel)[^1];
        Assert.True(view.Stats.Building > 0);

        store.Changed.Enqueue(new Vocaterra.Game.Core.World.Lod.LodSectionKey(key.Level, key.Tx, 0, key.Tz));
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));

        Assert.Equal(0, view.Missing);
        Assert.True(view.Stats.Built > FarTileSelector.Select(Small, 192, 192, Voxel).Count - 1);
    }

    [Fact]
    public void Given_ATileWhoseNewerMeshWaitsAndWhoseOldBufferIsOnTheCard_When_TheEyeLeavesAndComesBack_Then_ItIsMadeAgainAndNotShownFromTheOldBuffer()
    {
        // B07f, C6: the newer mesh of a tile nobody wants was dropped and the tile kept its old buffer, as if it were current.
        using var pool = new JobPool(0);
        var view = Make(pool);
        Assert.True(SettleGuard.Settle(limit => view.Settle(192, 192, Voxel, Release, limit), () => view.Stats.Built), "the view settles (no slice without progress)");
        var tile = view.PendingTiles.First(static t => t.Key.Level == 0); // built, not on the card (no renderer here): made to look like a tile on the card with a newer mesh waiting
        tile.OnCard = true;
        tile.Buffer = (Microsoft.Xna.Framework.Graphics.VertexBuffer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Microsoft.Xna.Framework.Graphics.VertexBuffer));
        Assert.NotNull(tile.Pending);

        for (var frame = 0; frame < 20; frame++)
        {
            view.Update(9000, 9000, Voxel, Release, 0);
        }

        Assert.Contains(tile, _released);
        Assert.True(SettleGuard.Settle(limit => view.Settle(192, 192, Voxel, Release, limit), () => view.Stats.Built), "the view settles (no slice without progress)");
        var again = view.PendingTiles.First(t => t.Key == tile.Key);
        Assert.NotSame(tile, again);
        Assert.Null(again.Buffer);
        Assert.NotNull(again.Pending);
        Assert.Equal(0, view.Stale);
    }

    [Fact]
    public void Given_NoColumnDrawnYet_When_TheNearZoneIsToldAndTheEyeStaysAMoment_Then_FarTilesFillTheZoneAndLeaveItAgainWhenItsColumnsAreDrawn()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        var ready = false;
        view.SetNearReady((_, _) => ready);
        view.Update(192, 192, Voxel, Release, null);
        Assert.Equal(0, view.Missing); // the filler inside the zone never holds a settle up
        Thread.Sleep(800);
        view.Update(192, 192, Voxel, Release, null);
        Land(view, 192, 192);
        var inner = Small.InnerFraction * Voxel;
        var inside = view.Draw.Where(t => t.Key.FarthestDistance(Small, 192, 192) < inner).ToList();
        Assert.NotEmpty(inside);
        AssertNoOverlap(view);

        ready = true; // the chunks are drawn: the far land inside the zone is not listed any more
        view.Update(192, 192, Voxel, Release, null);
        Assert.DoesNotContain(view.Draw, t => t.Key.FarthestDistance(Small, 192, 192) < inner);
    }

    [Fact]
    public void Given_AFillerTileInsideTheNearZoneThatIsBuilt_When_ItsColumnsAreDrawnAndItsSectionChanges_Then_ItIsDroppedAndNeverHoldsASettleUp()
    {
        // B07f: an edit under the player's feet marked the filler tile over it dirty, nothing remade it (its columns are drawn by the chunks), and Stale stayed above 0, so every settle of a scripted run waited out its bound (300 s a frame).
        using var pool = new JobPool(0);
        var store = new Vocaterra.Game.Core.World.Lod.LodStore();
        var view = new FarView(StreamedWorlds.Composition.World.Far, Small, pool, store);
        view.SetNearReady(static (_, _) => false);
        FarTile? inner = null;
        var until = Environment.TickCount64 + HangGuardMilliseconds;
        while (inner is null && Environment.TickCount64 < until)
        {
            view.Update(192, 192, Voxel, Release, null);
            inner = view.PendingTiles.FirstOrDefault(static t => t.Key.FarthestDistance(Small, 192, 192) < Small.InnerFraction * Voxel);
            Thread.Sleep(20);
        }

        Assert.NotNull(inner);
        view.SetNearReady(static (_, _) => true);
        store.Changed.Enqueue(new Vocaterra.Game.Core.World.Lod.LodSectionKey(inner.Key.Level, inner.Key.Tx, 0, inner.Key.Tz));
        view.Update(192, 192, Voxel, Release, null);

        Assert.Equal(0, view.Stale);
        Assert.Contains(inner, _released);
    }
}
