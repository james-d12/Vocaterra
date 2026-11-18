using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The rest of ChunkResidencyTests's tests (B28 split the slow ones into classes of their own, sharing ChunkResidencyTestsBase).</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ChunkResidencyTests : ChunkResidencyTestsBase
{
    [Fact]
    public void Given_AStreamedWorld_When_ItSettlesAtTheSpawn_Then_EveryChunkOfThePlanIsResidentAndNothingElseIs()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool);
        var (x, y, z) = Eye(scene);

        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));

        var plan = residency.Plan!;
        Assert.True(residency.IsSettled);
        Assert.All(plan.Wanted, chunk => Assert.True(scene.Grid.IsResident(chunk), $"{chunk} is wanted and not resident"));
        Assert.All(Resident(scene), chunk => Assert.True(plan.Holds(chunk), $"{chunk} is resident and the plan does not hold it"));
        Assert.Equal(plan.Wanted.Count, scene.Grid.ResidentChunkCount - Resident(scene).Count(c => !plan.Wanted.Contains(c)));
        Assert.Empty(scene.Grid.ChangedChunks()); // installing is not an edit
        Assert.True(scene.Grid.ResidentChunkCount < plan.Columns.Count * 12, "the band holds a few layers of each column, not the 24 of a whole column");
    }

    [Fact]
    public void Given_NoWorkersAndTwoWorkers_When_TheWorldSettlesAtTheSameEye_Then_TheResidentChunksAreTheSame()
    {
        var inline = StreamedWorlds.Scene();
        var threaded = StreamedWorlds.Scene();
        using var none = new JobPool(0);
        using var two = new JobPool(2);
        var (x, y, z) = Eye(inline);

        HangGuard.Settled(limit => Make(inline, none).Settle(x, y, z, limit));
        HangGuard.Settled(limit => Make(threaded, two).Settle(x, y, z, limit));

        Assert.Equal(Resident(inline).Order(Comparer<VoxelChunkCoord>.Create(Compare)).ToList(), Resident(threaded).Order(Comparer<VoxelChunkCoord>.Create(Compare)).ToList());
        Assert.Null(two.Failure);
    }

    [Fact]
    public void Given_APlayerWhoWalksOutOfTheValley_When_FramesAreSpentInsideABudget_Then_TheWorldFollowsInstallingNearestFirstWithoutHolesOnceSettled()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(2);
        var residency = Make(scene, pool);
        var (x, y, z) = Eye(scene);
        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        var before = residency.Stats;

        // Walk east out through the pass, a chunk at a time, one frame each: the frames never finish the plan, only the settle does.
        var frames = 0;
        for (var step = 0; step < 24; step++)
        {
            residency.Update(x + (step * 16), y, z, 1.5);
            frames++;
        }

        HangGuard.Settled(limit => residency.Settle(x + (23 * 16), y, z, limit));
        var plan = residency.Plan!;
        Assert.Equal(VoxelChunkCoord.FromBlock((int)MathF.Floor(x + (23 * 16)), (int)MathF.Floor(y), (int)MathF.Floor(z)), plan.Player);
        Assert.All(plan.Wanted, chunk => Assert.True(scene.Grid.IsResident(chunk)));
        Assert.All(Resident(scene), chunk => Assert.True(plan.Holds(chunk)));
        Assert.True(residency.Stats.Evicted > before.Evicted, "the valley's far side was let go");
        Assert.True(residency.Stats.Plans > before.Plans);
        Assert.Null(pool.Failure);
        Assert.Equal(24, frames);
    }

    [Fact]
    public void Given_AChangedChunk_When_ThePlayerWalksFarFromIt_Then_ItStaysResidentWithItsBlockWhileTheUnchangedOnesAreDropped()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool);
        var (x, y, z) = Eye(scene);
        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        var cell = new BlockPos(scene.Spawn.Feet.X + 2, scene.Spawn.Feet.Y + 3, scene.Spawn.Feet.Z);
        Assert.True(scene.Grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Planks));
        var changed = VoxelChunkCoord.FromBlock(cell.X, cell.Y, cell.Z);

        HangGuard.Settled(limit => residency.Settle(x + 900, y, z, limit));

        Assert.True(scene.Grid.IsResident(changed), "a changed chunk is state and is never dropped");
        Assert.Equal(BlockId.Planks, scene.Grid.PeekBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal([changed], scene.Grid.ChangedChunks().Select(static c => c.Coord).ToList());
        Assert.False(residency.Plan!.Holds(changed), "the plan let it go; the grid refused");
        Assert.True(residency.Stats.Evicted > 0);
    }

    [Fact]
    public void Given_ThePlayerUnderground_When_TheWorldSettles_Then_TheNearZoneHoldsTheStoneDownToTheFloor()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool);
        var (x, _, z) = Eye(scene);
        var floorLayer = scene.Grid.Limits.MinChunkY;

        HangGuard.Settled(limit => residency.Settle(x, 20f, z, limit)); // twenty blocks down: under the valley's ground

        Assert.True(residency.Plan!.Underground);
        Assert.True(scene.Grid.IsResident(new VoxelChunkCoord((int)MathF.Floor(x) >> 4, floorLayer, (int)MathF.Floor(z) >> 4)));
        var above = StreamedWorlds.Scene();
        HangGuard.Settled(limit => Make(above, pool).Settle(x, Eye(above).Y, z, limit));
        Assert.True(scene.Grid.ResidentChunkCount > above.Grid.ResidentChunkCount);
    }

    [Fact]
    public void Given_ThePlayerGoesUnderground_When_TheWorldSettlesThenSettlesAtTheSurface_Then_TheNearZoneWidensAndNarrows()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool); // near 2, underground near 3 columns
        var (x, y, z) = Eye(scene);
        var cx = (int)MathF.Floor(x) >> 4;
        var cz = (int)MathF.Floor(z) >> 4;
        var floorLayer = scene.Grid.Limits.MinChunkY;
        var wide = new VoxelChunkCoord(cx + 4, floorLayer, cz); // four columns away, at the bottom of the world: held by the underground near zone (3 columns, and the column beside it each way), not by the surface one (2 and one)

        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        Assert.False(residency.Plan!.Underground);
        Assert.False(scene.Grid.IsResident(wide));
        var narrow = scene.Grid.ResidentChunkCount;

        HangGuard.Settled(limit => residency.Settle(x, 20f, z, limit));
        Assert.True(residency.Plan!.Underground);
        Assert.True(scene.Grid.IsResident(wide), "underground the near zone reaches four columns");
        Assert.True(scene.Grid.ResidentChunkCount > narrow);

        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        Assert.False(residency.Plan!.Underground);
        Assert.False(residency.Plan.Holds(wide), "back at the surface the zone narrows again");
    }

    [Fact]
    public void Given_ACapOnResidentChunks_When_TheWorldSettles_Then_NothingNewIsAskedForPastItAndItStillSettles()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool, max: 300);
        var (x, y, z) = Eye(scene);

        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));

        Assert.InRange(scene.Grid.ResidentChunkCount, 200, 300 + 30);
    }

    [Fact]
    public void Given_ACapOnResidentChunks_When_ThePlayerMovesOn_Then_TheCapIsAnAdmissionLimitAndThePlansEvictionFreesTheRoomBehindThem()
    {
        // B24 (review C8): the cap stops new asks; it does not evict chunks the plan still holds (that would be asked for again at once and thrash). What is behind the player is freed by the plan's
        // own eviction, so the count stays near the cap over a walk and the land at the new eye fills nearest first.
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool, max: 300);
        var (x, y, z) = Eye(scene);
        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        var here = new VoxelChunkCoord((int)MathF.Floor(x) >> 4, (int)MathF.Floor(y) >> 4, (int)MathF.Floor(z) >> 4);
        Assert.True(scene.Grid.IsResident(here), "the eye's own chunk comes first");

        HangGuard.Settled(limit => residency.Settle(x + 700, y, z, limit));

        Assert.InRange(scene.Grid.ResidentChunkCount, 100, 300 + 30);
        Assert.False(scene.Grid.IsResident(here), "the plan's eviction freed the chunks behind the player");
        var ahead = new VoxelChunkCoord((int)MathF.Floor(x + 700) >> 4, (int)MathF.Floor(y) >> 4, (int)MathF.Floor(z) >> 4);
        Assert.True(scene.Grid.IsResident(ahead), "the nearest land to the new eye is resident");
    }

    [Fact]
    public void Given_ChunksInstalledByTheResidency_When_TheyAreCompared_Then_EachEqualsWhatTheSourceGenerates()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(2);
        var residency = Make(scene, pool);
        var (x, y, z) = Eye(scene);
        HangGuard.Settled(limit => residency.Settle(x + 700, y, z, limit));

        var compared = 0;
        foreach (var coord in Resident(scene).Where(static c => c.X % 5 == 0))
        {
            var expected = scene.Streaming!.Source.Generate(coord).ComputeDigestHex();
            Assert.True(scene.Grid.TryGetChunk(coord, out var chunk));
            Assert.Equal(expected, chunk!.ComputeDigestHex());
            compared++;
        }

        Assert.True(compared > 50);
    }

    [Fact]
    public void Given_AStartAndAJump_When_TheWorldRoundThePlayerIsAsked_Then_ItIsReadyOnlyOnceThePlanIsForThePlaceAndTheNearColumnsAreResident()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool);
        var (x, y, z) = Eye(scene);

        Assert.False(residency.IsNearReady(x, y, z), "nothing is planned yet: the simulation waits");
        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        Assert.True(residency.IsNearReady(x, y, z));
        Assert.False(residency.IsNearReady(x + 700, y, z), "a jump: the plan is for the old place");

        residency.Update(x + 700, y, z, 0.0);
        Assert.False(residency.IsNearReady(x + 700, y, z), "the plan is made but its near columns are not all made yet");
        HangGuard.Settled(limit => residency.Settle(x + 700, y, z, limit));
        Assert.True(residency.IsNearReady(x + 700, y, z));
        Assert.True(scene.Grid.IsResident(VoxelChunkCoord.FromBlock((int)(x + 700), (int)y, (int)z)) || residency.Plan!.TryGetLayers(((int)(x + 700)) >> 4, ((int)z) >> 4, out _, out _));
    }

    [Fact]
    public void Given_AJumpFarFromABusyStart_When_TheNearZoneIsMadeNow_Then_ItIsReadyBeforeTheRestOfThePlanAndTheOldPlaceAsksForNothingMore()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(2);
        var residency = Make(scene, pool, new BandStreamPolicy(14, 16, 3, 5));
        var (x, y, z) = Eye(scene);
        for (var frame = 0; frame < 30; frame++)
        {
            residency.Update(x, y, z, 1.5); // the start's work is queued and half done
        }

        residency.Update(x + 700, y, z, 1.5);
        Assert.Null(residency.Plan); // a jump: the old place's plan is let go, so it asks for nothing more at the old place

        HangGuard.Settled(limit => residency.SettleNear(x + 700, y, z, limit));

        Assert.True(residency.IsNearReady(x + 700, y, z));
        Assert.False(residency.IsSettled, "only the near zone was made: the rest of the plan comes over the next frames");
        Assert.Null(pool.Failure);
    }

    [Fact]
    public void Given_NoWorkers_When_TheNearZoneIsMadeNow_Then_ItIsReadyOnTheCallersThread()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool);
        var (x, y, z) = Eye(scene);

        HangGuard.Settled(limit => residency.SettleNear(x, y, z, limit));

        Assert.True(residency.IsNearReady(x, y, z));
    }

    [Fact]
    public void Given_TwoWorkers_When_TheWorldStreamsIn_Then_EveryInstallAndEvictionHappensOnTheCallersThreadAndNeverOnAWorker()
    {
        var scene = StreamedWorlds.Scene();
        var recorder = new ThreadRecorder();
        scene.Grid.AddResidencyListener(recorder);
        using var pool = new JobPool(2);
        var residency = Make(scene, pool);
        var (x, y, z) = Eye(scene);

        HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
        HangGuard.Settled(limit => residency.Settle(x + 500, y, z, limit));

        Assert.True(recorder.Events > 500);
        Assert.Equal([Environment.CurrentManagedThreadId], recorder.Threads);
        Assert.True(residency.Stats.Evicted > 0);
        Assert.Null(pool.Failure);
    }
}
