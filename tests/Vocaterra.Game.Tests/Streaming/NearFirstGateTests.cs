using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>B45: far work (the ring's footprints and the far view's tiles) waits for the near view at a start and after a jump, then goes on; the ring's copy of resident chunks is made on its worker, with the same bytes and the same refusal of edited chunks.</summary>
public sealed class NearFirstGateTests(ITestOutputHelper output)
{
    private static readonly FarViewSettings Wide = new(2048, MaxTiles: 400, TreeLevels: 1, RealRadius: 192);

    [Fact]
    public void Given_AGateNobodyArmed_When_AskedWhatItHolds_Then_ItHoldsNothing()
    {
        var gate = new NearFirstGate(NearHold.Fine);

        Assert.False(gate.IsClosed);
        Assert.False(gate.HoldsFine);
        Assert.False(gate.HoldsCoarse);
    }

    [Fact]
    public void Given_AnArmedGate_When_TheNearViewIsDone_Then_ItOpensAndSaysHowLongItHeld()
    {
        var gate = new NearFirstGate(NearHold.Fine);
        gate.Arm(1_000);

        Assert.True(gate.HoldsFine);
        Assert.False(gate.HoldsCoarse); // the default lets the coarse tiles and the stand-ins through
        Assert.False(gate.Observe(2_000, static () => false));
        Assert.True(gate.IsClosed);
        Assert.True(gate.Observe(3_500, static () => true));
        Assert.False(gate.IsClosed);
        Assert.Equal(2_500, gate.LastHeldMilliseconds);
        Assert.Equal(0, gate.Timeouts);
    }

    [Fact]
    public void Given_ANearViewThatNeverCompletes_When_TheTimeoutPasses_Then_TheGateOpensAnyway()
    {
        var gate = new NearFirstGate(NearHold.Fine);
        gate.Arm(0);
        var asked = 0;

        Assert.False(gate.Observe(NearFirstGate.TimeoutMilliseconds - 1, () => { asked++; return false; }));
        Assert.True(gate.Observe(NearFirstGate.TimeoutMilliseconds, () => { asked++; return false; }));

        Assert.False(gate.IsClosed);
        Assert.Equal(1, gate.Timeouts);
        Assert.Equal(1, asked); // after the timeout nothing is asked of the near view
    }

    [Fact]
    public void Given_AnEyeThatFliesOnFromWhereTheGateWasClosed_When_ItHasLeftTheNearView_Then_TheGateOpensAtOnceAndNotAtTheTimeout()
    {
        // B47: the near view round the eye cannot be finished by an eye that outruns the chunks, so the gate held every far tile (the stand-ins too) for the whole timeout while the eye flew over land with no far tile under it.
        var gate = new NearFirstGate(NearHold.All);
        gate.Arm(1_000, 500, 500);

        Assert.False(gate.Observe(1_100, static () => false, (500 + NearFirstGate.LeaveBlocks - 1, 500)));
        Assert.True(gate.IsClosed);
        Assert.True(gate.Observe(1_200, static () => false, (500, 500 + NearFirstGate.LeaveBlocks)));
        Assert.False(gate.IsClosed);
        Assert.Equal(1, gate.Left);
        Assert.Equal(0, gate.Timeouts);
        Assert.Equal(200, gate.LastHeldMilliseconds);
    }

    [Fact]
    public void Given_AnEyeThatWalksOnAFewBlocks_When_TheNearViewIsNotDone_Then_TheGateStaysClosed()
    {
        var gate = new NearFirstGate(NearHold.All);
        gate.Arm(0, 100, 100);

        Assert.False(gate.Observe(10_000, static () => false, (140, 130)));
        Assert.True(gate.IsClosed);
    }

    [Fact]
    public void Given_AnOpenGate_When_TheEyeJumps_Then_ItClosesAgainAndItsClockStartsOver()
    {
        var gate = new NearFirstGate(NearHold.Fine);
        gate.Arm(0);
        Assert.True(gate.Observe(100, static () => true));

        gate.Arm(50_000);

        Assert.True(gate.IsClosed);
        Assert.Equal(2, gate.Armed);
        Assert.False(gate.Observe(50_000 + NearFirstGate.TimeoutMilliseconds - 1, static () => false));
        Assert.True(gate.Observe(50_000 + NearFirstGate.TimeoutMilliseconds, static () => false));
    }

    [Fact]
    public void Given_TheModes_When_TheGateIsClosed_Then_OffHoldsNothingFineHoldsFineAndAllHoldsEverything()
    {
        var off = new NearFirstGate(NearHold.Off);
        var all = new NearFirstGate(NearHold.All);
        off.Arm(0);
        all.Arm(0);

        Assert.False(off.IsClosed);
        Assert.True(all.HoldsFine);
        Assert.True(all.HoldsCoarse);
        all.Open();
        Assert.False(all.HoldsFine);
    }

    // A ring over a pool with no worker: its jobs wait in the pool until the test runs them.
    private static (LodRing Ring, JobPool Pool) MakeRing(Func<bool> hold)
    {
        var world = StreamedWorlds.Composition.World;
        var pool = new JobPool(0);
        var ring = new LodRing(world.Far.FarWorld(new LodStore(), 2), world, pool, new ColumnBandCache(world.Bands)) { Hold = hold };
        return (ring, pool);
    }

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void Given_AGateThatIsClosed_When_TheRingUpdates_Then_NoFootprintIsPlannedOrSubmittedUntilItOpens()
    {
        var held = true;
        var (ring, made) = MakeRing(() => held);
        using var pool = made;
        var scene = StreamedWorlds.Scene();
        var (x, z) = (scene.Spawn.Feet.X, scene.Spawn.Feet.Z);

        for (var frame = 0; frame < 50; frame++)
        {
            ring.Update(x, z, 48);
            _ = pool.RunInline(null);
        }

        Assert.Equal(0, pool.Pending);
        Assert.Equal(0, ring.Done);
        Assert.False(ring.HasPlan);

        held = false;
        var until = HangGuard.DeadlineAfter(3_600_000);
        do
        {
            ring.Update(x, z, 48);
            _ = pool.RunInline(null);
        }
        while ((!ring.HasPlan || !ring.IsIdle) && !HangGuard.Passed(until));

        Assert.True(ring.Done > 0, $"{ring.Done} footprints made after the gate opened");
    }

    [Fact]
    public void Given_AGateThatHoldsTheFineTiles_When_TheViewUpdates_Then_OnlyTheStandInsAreBuiltUntilItOpensAndThenTheViewFills()
    {
        using var pool = new JobPool(0);
        var view = new FarView(StreamedWorlds.Composition.World.Far, Wide, pool);
        var held = true;
        view.HoldFine = () => held;
        var leaves = FarTileSelector.Select(Wide, 192, 192, 64).ToHashSet();

        for (var frame = 0; frame < 60; frame++)
        {
            view.Update(192, 192, 64, static _ => { }, 0);
        }

        var fineAsked = view.Requested.Concat(view.Cache.Keys).Count(k => leaves.Contains(k) && k.Level < 4 && k.Level < Wide.TopLevel);
        Assert.Equal(0, fineAsked);
        Assert.NotEmpty(view.Cache); // the stand-ins cover the ground meanwhile

        held = false;
        for (var frame = 0; frame < 4000 && view.Missing > 0; frame++)
        {
            view.Update(192, 192, 64, static _ => { }, 0);
        }

        Assert.Equal(0, view.Missing);
        output.WriteLine($"{view.Cache.Count} tiles after the gate opened");
    }

    [Fact]
    public void Given_AGateThatHoldsEverything_When_TheViewUpdates_Then_NoTileIsAskedForUntilItOpens()
    {
        using var pool = new JobPool(0);
        var view = new FarView(StreamedWorlds.Composition.World.Far, Wide, pool);
        var held = true;
        view.HoldFine = () => held;
        view.HoldCoarse = () => held;

        for (var frame = 0; frame < 60; frame++)
        {
            view.Update(192, 192, 64, static _ => { }, 0);
        }

        Assert.Empty(view.Requested);
        Assert.Empty(view.Cache);
        Assert.Equal(0, pool.Pending);

        held = false;
        for (var frame = 0; frame < 4000 && view.Missing > 0; frame++)
        {
            view.Update(192, 192, 64, static _ => { }, 0);
        }

        Assert.Equal(0, view.Missing);
    }

    // The zone as the residency leaves it round a footprint, every layer of every column of the footprint.
    private static void FillZone(VoxelGrid grid, int sx, int sz)
    {
        var world = StreamedWorlds.Composition.World;
        for (var k = 0; k < 4; k++)
        {
            var (cx, cz) = ((2 * sx) + (k & 1), (2 * sz) + (k >> 1));
            foreach (var chunk in world.GenerateLayers(cx, cz, world.Source.MinChunkY, world.Source.MaxChunkY - 1))
            {
                _ = GridResidency.Install(grid, chunk);
            }
        }
    }

    private static (int Sx, int Sz) AnyFootprint()
    {
        var scene = StreamedWorlds.Scene();
        return (((int)scene.Spawn.Feet.X >> 5) + 20, ((int)scene.Spawn.Feet.Z >> 5) - 20);
    }

    private static LodRing RingOver(VoxelGrid grid, JobPool pool)
    {
        var world = StreamedWorlds.Composition.World;
        return new LodRing(world.Far.FarWorld(new LodStore(), 2), world, pool, new ColumnBandCache(world.Bands), grid);
    }

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public async Task Given_ChunksCapturedOnTheGameThread_When_AWorkerCopiesThem_Then_TheCopiesAreByteIdenticalToTheGeneratedChunks()
    {
        var world = StreamedWorlds.Composition.World;
        var (sx, sz) = AnyFootprint();
        var scene = StreamedWorlds.Scene();
        FillZone(scene.Grid, sx, sz);
        using var pool = new JobPool(0);
        var ring = RingOver(scene.Grid, pool);

        var captured = ring.CaptureResident(sx, sz)!;
        var copied = (await Task.Run(() => ring.CopyResident(captured)))!;

        var taken = 0;
        for (var k = 0; k < 4; k++)
        {
            var (cx, cz) = ((2 * sx) + (k & 1), (2 * sz) + (k >> 1));
            foreach (var made in world.GenerateLayers(cx, cz, world.Source.MinChunkY, world.Source.MaxChunkY - 1))
            {
                var layer = made.Coord.Y - world.Source.MinChunkY;
                var copy = copied[k]![layer];
                Assert.NotNull(copy);
                Assert.True(made.Blocks.SequenceEqual(copy!.Blocks), $"chunk {made.Coord} differs");
                taken++;
            }
        }

        Assert.True(taken >= 8, $"{taken} chunks compared");
    }

    [Fact]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public async Task Given_ChunksCapturedAndThenEdited_When_AWorkerCopiesThem_Then_AnEditedChunkIsNeverKeptAndTheKeptCopiesAreUnedited()
    {
        var world = StreamedWorlds.Composition.World;
        var (sx, sz) = AnyFootprint();
        var scene = StreamedWorlds.Scene();
        FillZone(scene.Grid, sx, sz);
        using var pool = new JobPool(0);
        var ring = RingOver(scene.Grid, pool);
        var original = ring.TakeResident(sx, sz)!;
        var (min, max) = (world.Source.MinChunkY, world.Source.MaxChunkY - 1);

        // The footprint is captured, then a worker copies it in a loop while the game thread edits one block of every chunk in turn.
        var captured = ring.CaptureResident(sx, sz)!;
        var copying = true;
        var copies = new List<ChunkData?[]?[]>();
        var worker = Task.Run(() =>
        {
            while (Volatile.Read(ref copying))
            {
                if (ring.CopyResident(captured) is { } copy)
                {
                    lock (copies)
                    {
                        copies.Add(copy);
                    }
                }
            }
        });

        var edited = new HashSet<(int K, int Layer)>();
        for (var k = 0; k < 4; k++)
        {
            var (cx, cz) = ((2 * sx) + (k & 1), (2 * sz) + (k >> 1));
            for (var cy = min; cy <= max; cy++)
            {
                var (bx, by, bz) = (cx * 16, cy * 16, cz * 16);
                for (var dy = 0; dy < 16; dy++)
                {
                    var was = scene.Grid.GetBlock(bx, by + dy, bz);
                    if (was != BlockId.Air && was != BlockId.Bedrock && scene.Grid.SetBlock(bx, by + dy, bz, BlockId.Air))
                    {
                        _ = edited.Add((k, cy - min));
                        break;
                    }
                }
            }
        }

        Volatile.Write(ref copying, false);
        await worker;
        var after = ring.CopyResident(captured);

        Assert.True(edited.Count > 8, $"{edited.Count} chunks edited");
        var kept = 0;
        foreach (var copy in copies.Append(after ?? new ChunkData?[4][]))
        {
            for (var k = 0; k < 4; k++)
            {
                for (var layer = 0; layer < (copy[k]?.Length ?? 0); layer++)
                {
                    if (copy[k]![layer] is { } chunk)
                    {
                        kept++;
                        Assert.True(original[k]![layer]!.Blocks.SequenceEqual(chunk.Blocks), $"a copy of column {k} layer {layer} carries an edit");
                    }
                }
            }
        }

        foreach (var (k, layer) in edited)
        {
            Assert.Null(after?[k]?[layer]); // once written, a chunk is not copied at all
        }

        output.WriteLine($"{copies.Count} copies taken while editing, {kept} chunk copies kept, all unedited");
    }
}
