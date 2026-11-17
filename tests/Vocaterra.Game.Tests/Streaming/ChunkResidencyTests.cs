using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>
/// The residency of the streamed world (B07, biomes plan section 3.7): the surface band of the columns round the player, padded by a chunk, made nearest first on worker threads or inline and installed by
/// the game loop; the unchanged chunks the plan lets go are dropped and the changed ones never. Residency changes performance and never a result.
/// </summary>
public sealed class ChunkResidencyTests
{
    private static readonly BandStreamPolicy Small = new(6, 8, 2, 3);

    private static (float X, float Y, float Z) Eye(Scene scene) => (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);

    private static ChunkResidency Make(Scene scene, JobPool pool, BandStreamPolicy? policy = null, int max = int.MaxValue) =>
        new(scene.Grid, scene.Streaming!, policy ?? Small, pool, scene.Places, max);

    private static HashSet<VoxelChunkCoord> Resident(Scene scene) => [.. GridResidency.Resident(scene.Grid)];

    [Fact]
    public void Given_AStreamedWorld_When_ItSettlesAtTheSpawn_Then_EveryChunkOfThePlanIsResidentAndNothingElseIs()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool);
        var (x, y, z) = Eye(scene);

        Assert.True(residency.Settle(x, y, z));

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

        Assert.True(Make(inline, none).Settle(x, y, z));
        Assert.True(Make(threaded, two).Settle(x, y, z));

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
        Assert.True(residency.Settle(x, y, z));
        var before = residency.Stats;

        // Walk east out through the pass, a chunk at a time, one frame each: the frames never finish the plan, only the settle does.
        var frames = 0;
        for (var step = 0; step < 24; step++)
        {
            residency.Update(x + (step * 16), y, z, 1.5);
            frames++;
        }

        Assert.True(residency.Settle(x + (23 * 16), y, z));
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
        Assert.True(residency.Settle(x, y, z));
        var cell = new BlockPos(scene.Spawn.Feet.X + 2, scene.Spawn.Feet.Y + 3, scene.Spawn.Feet.Z);
        Assert.True(scene.Grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Planks));
        var changed = VoxelChunkCoord.FromBlock(cell.X, cell.Y, cell.Z);

        Assert.True(residency.Settle(x + 900, y, z));

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

        Assert.True(residency.Settle(x, 20f, z)); // twenty blocks down: under the valley's ground

        Assert.True(residency.Plan!.Underground);
        Assert.True(scene.Grid.IsResident(new VoxelChunkCoord((int)MathF.Floor(x) >> 4, floorLayer, (int)MathF.Floor(z) >> 4)));
        var above = StreamedWorlds.Scene();
        Assert.True(Make(above, pool).Settle(x, Eye(above).Y, z));
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

        Assert.True(residency.Settle(x, y, z));
        Assert.False(residency.Plan!.Underground);
        Assert.False(scene.Grid.IsResident(wide));
        var narrow = scene.Grid.ResidentChunkCount;

        Assert.True(residency.Settle(x, 20f, z));
        Assert.True(residency.Plan!.Underground);
        Assert.True(scene.Grid.IsResident(wide), "underground the near zone reaches four columns");
        Assert.True(scene.Grid.ResidentChunkCount > narrow);

        Assert.True(residency.Settle(x, y, z));
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

        Assert.True(residency.Settle(x, y, z));

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
        Assert.True(residency.Settle(x, y, z));
        var here = new VoxelChunkCoord((int)MathF.Floor(x) >> 4, (int)MathF.Floor(y) >> 4, (int)MathF.Floor(z) >> 4);
        Assert.True(scene.Grid.IsResident(here), "the eye's own chunk comes first");

        Assert.True(residency.Settle(x + 700, y, z));

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
        Assert.True(residency.Settle(x + 700, y, z));

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
    public void Given_AFreshWorldFarFromTheValley_When_FramesAreSpentInsideABudget_Then_ThePlanStartsFromTheNearBandsAndGrowsAsTheRestArrive()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool, new BandStreamPolicy(14, 16, 2, 3));
        var frames = 0;

        while (residency.Plan is null && frames++ < 4000)
        {
            residency.Update(2000, 90, 2000, 2.0);
        }

        Assert.NotNull(residency.Plan);
        Assert.False(residency.IsSettled, "the far bands are not all made: the plan is a partial one");
        var first = residency.Plan!.Columns.Count;
        Assert.InRange(first, 20, 700);
        Assert.DoesNotContain(GridResidency.Resident(scene.Grid), c => c.X < 100); // nothing of the valley or anything else was dropped or made by a partial plan

        Assert.True(residency.Settle(2000, 90, 2000));
        Assert.True(residency.Plan!.Columns.Count > first);
        Assert.All(residency.Plan.Wanted, chunk => Assert.True(scene.Grid.IsResident(chunk)));
    }

    [Fact]
    public void Given_AStartAndAJump_When_TheWorldRoundThePlayerIsAsked_Then_ItIsReadyOnlyOnceThePlanIsForThePlaceAndTheNearColumnsAreResident()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(0);
        var residency = Make(scene, pool);
        var (x, y, z) = Eye(scene);

        Assert.False(residency.IsNearReady(x, y, z), "nothing is planned yet: the simulation waits");
        Assert.True(residency.Settle(x, y, z));
        Assert.True(residency.IsNearReady(x, y, z));
        Assert.False(residency.IsNearReady(x + 700, y, z), "a jump: the plan is for the old place");

        residency.Update(x + 700, y, z, 0.0);
        Assert.False(residency.IsNearReady(x + 700, y, z), "the plan is made but its near columns are not all made yet");
        Assert.True(residency.Settle(x + 700, y, z));
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

        Assert.True(residency.SettleNear(x + 700, y, z, 20_000));

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

        Assert.True(residency.SettleNear(x, y, z, 60_000));

        Assert.True(residency.IsNearReady(x, y, z));
    }

    private sealed class ThreadRecorder : IChunkResidencyListener
    {
        public HashSet<int> Threads { get; } = [];

        public int Events { get; private set; }

        public void OnChunkInstalled(VoxelChunkCoord coord) => Note();

        public void OnChunkEvicted(VoxelChunkCoord coord) => Note();

        private void Note()
        {
            _ = Threads.Add(Environment.CurrentManagedThreadId);
            Events++;
        }
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

        Assert.True(residency.Settle(x, y, z));
        Assert.True(residency.Settle(x + 500, y, z));

        Assert.True(recorder.Events > 500);
        Assert.Equal([Environment.CurrentManagedThreadId], recorder.Threads);
        Assert.True(residency.Stats.Evicted > 0);
        Assert.Null(pool.Failure);
    }

    // ------------------------------------------------------------------ residency never changes a result

    // The player's walk, out of the valley through the pass and round, as eye positions.
    private static readonly (int X, int Y, int Z)[] Walk =
    [
        (200, 70, 200), (320, 70, 215), (380, 60, 208), (420, 60, 208), (470, 70, 208), (600, 80, 260), (700, 90, 300), (520, 80, 90), (100, 60, -100), (-300, 70, 50), (200, 60, -250), (200, 70, 200),
    ];

    // The edits: a step of the walk and a cell; the last but one puts back what an earlier one changed.
    private static readonly (int Step, int X, int Y, int Z, BlockId? Block)[] Edits =
    [
        (0, 210, 62, 200, BlockId.Planks),
        (2, 386, 60, 210, BlockId.Planks),
        (3, 440, 70, 208, BlockId.Planks),
        (4, 475, 72, 215, BlockId.Stone),
        (5, 610, 85, 255, BlockId.Planks),
        (6, 440, 70, 208, null),
        (7, 515, 30, 95, BlockId.Planks),
        (8, 95, -10, -95, BlockId.Planks),
        (9, -300, 40, 50, BlockId.Stone),
        (11, 205, 61, 201, BlockId.Planks),
    ];

    private static (string Digest, IReadOnlyList<string> Save, int Resident) Run(BandStreamPolicy policy, int workers, int framesPerStep, bool settle)
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(workers);
        var residency = new ChunkResidency(scene.Grid, scene.Streaming!, policy, pool, scene.Places);
        var header = WorldStateHeader.For(TestGame.Bundle, 42, scene.TemplateDigest);
        var original = new Dictionary<(int, int, int), BlockId>();
        for (var step = 0; step < Walk.Length; step++)
        {
            var (x, y, z) = Walk[step];
            if (settle)
            {
                Assert.True(residency.Settle(x, y, z));
            }
            else
            {
                for (var frame = 0; frame < framesPerStep; frame++)
                {
                    residency.Update(x, y, z, 2.0);
                    if (workers > 0)
                    {
                        Thread.Sleep(1);
                    }
                }
            }

            foreach (var (_, ex, ey, ez, block) in Edits.Where(e => e.Step == step))
            {
                if (!original.ContainsKey((ex, ey, ez)))
                {
                    original[(ex, ey, ez)] = scene.Grid.GetBlock(ex, ey, ez);
                }

                _ = scene.Grid.SetBlock(ex, ey, ez, block ?? original[(ex, ey, ez)]);
            }
        }

        var save = VoxelGridDiff.Capture(scene.Grid)
            .Select(static e => $"{e.X},{e.Y},{e.Z}:{string.Join(';', e.Runs)}|{(e.Water is null ? string.Empty : string.Join(';', e.Water))}")
            .ToList();
        return (WorldStateDigest.ComputeHex(scene.Grid, header), save, scene.Grid.ResidentChunkCount);
    }

    [Fact]
    public void Given_TheSameWalkAndEdits_When_ItRunsUnderTwoPoliciesWithAndWithoutWorkers_Then_TheStateDigestsAndTheSavesAreIdentical()
    {
        var tight = Run(new BandStreamPolicy(3, 4, 1, 2), workers: 0, framesPerStep: 6, settle: false);
        var wide = Run(new BandStreamPolicy(7, 10, 3, 5), workers: 2, framesPerStep: 40, settle: false);
        var settled = Run(new BandStreamPolicy(5, 6, 2, 3), workers: 2, framesPerStep: 0, settle: true);
        var inline = Run(new BandStreamPolicy(5, 6, 2, 3), workers: 0, framesPerStep: 0, settle: true);

        Assert.NotEqual(tight.Resident, wide.Resident); // the policies really kept different chunks
        Assert.Equal(tight.Digest, wide.Digest);
        Assert.Equal(tight.Digest, settled.Digest);
        Assert.Equal(tight.Digest, inline.Digest);
        Assert.Equal(tight.Save, wide.Save);
        Assert.Equal(tight.Save, settled.Save);
        Assert.Equal(tight.Save, inline.Save);
        Assert.InRange(tight.Save.Count, 4, 8); // the edited chunks: one edit was put back, some share a chunk, and where the ground already holds the block an edit is none
    }

    private static int Compare(VoxelChunkCoord a, VoxelChunkCoord b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X);
}
