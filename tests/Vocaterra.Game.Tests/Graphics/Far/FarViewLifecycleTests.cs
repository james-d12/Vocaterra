using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>
/// B49: a drawn LOD is retired only once what replaces it is drawn. The earlier far-view tests ran without workers (a tile landed in the frame it was asked for), with the ring's radius equal to the voxel zone's, and asserted "no uncovered leaf" only once a fill was complete,
/// so they could not see a stand-in thrown away while it was on screen (the LOD review's finding 1: a tile-sized hole when the ring's mips change its sections), an in-flight stand-in dropped by a re-selection (finding 2) or one evicted by Trim (finding 8). These run real workers,
/// the game's ratio (the ring's 1,000 blocks against a voxel zone of 400) and a flight out and back, and assert <see cref="FarView.Uncovered"/> is 0 in every frame once the view has been covered, not only at the end of a fill.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarViewLifecycleTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    private static readonly FarViewSettings Game = new(2048, MaxTiles: 800, TreeLevels: 1, RealRadius: 1000);
    private const int Vox = 400;
    private const int Meshing = 20;
    private const int Workers = 3;

    // One frame of the game's loop, without a card and without the clock: the frame's uploads, the ring, the far view, the uploads of what it made and the assembly after them. A frame ends when the workers have finished JobsPerFrame jobs
    // (or have none left): the speed of the machine decides how long that takes, never what the frame sees, so a slow machine cannot make a hole and a fast one cannot hide it.
    private sealed class Rig : IDisposable
    {
        private readonly Dictionary<(int, int), int> _readyAt = [];
        private readonly NearFirstGate _gate = new(NearHold.All);
        private readonly LodRing? _ring;
        private readonly List<FarTile> _released;
        public JobPool Pool { get; } = new(Workers);
        public FarView View { get; }
        public double EyeX { get; set; } = 192;
        public double EyeZ { get; set; } = 192;
        public int Frame { get; private set; }
        public int JobsPerFrame { get; set; } = 2;
        public Dictionary<FarTileKey, FarTileMesh> Meshes { get; } = [];

        public Rig(List<FarTile> released, bool ring, Func<int, int, bool>? awaits = null)
        {
            _released = released;
            View = Make(Pool, Game);
            View.HoldFine = () => _gate.HoldsFine;
            View.HoldCoarse = () => _gate.HoldsCoarse;
            if (ring)
            {
                var world = StreamedWorlds.Composition.World;
                _ring = new LodRing(View.World, world, Pool, new ColumnBandCache(world.Bands)) { Hold = () => _gate.HoldsFine };
                View.SetRingWait(_ring.Awaits);
            }
            else if (awaits is not null)
            {
                View.SetRingWait(awaits);
            }

            _ = View.UsePath(true, static _ => { });
            View.SetNearReady(Ready);
            _gate.Arm(0, EyeX, EyeZ);
        }

        private bool Ready(int cx, int cz)
        {
            var (dx, dz) = ((cx * 16) + 8 - EyeX, (cz * 16) + 8 - EyeZ);
            if ((dx * dx) + (dz * dz) > (double)Vox * Vox)
            {
                _ = _readyAt.Remove((cx, cz));
                return false;
            }

            if (!_readyAt.TryGetValue((cx, cz), out var at))
            {
                _readyAt[(cx, cz)] = at = Frame + Meshing;
            }

            return Frame >= at;
        }

        private void Upload()
        {
            foreach (var tile in View.PendingTiles.ToList())
            {
                Meshes[tile.Key] = tile.Pending!;
                tile.Adopt(tile.Pending!);
                tile.Pending = null;
                tile.OnCard = true;
            }
        }

        public void Step()
        {
            Frame++;
            var before = Pool.Completed + Pool.Skipped;
            Upload();
            _ = _gate.Observe(Frame * 16.7, () => Enumerable.Range(-8, 17).All(dz => Enumerable.Range(-8, 17).All(dx => Ready(((int)EyeX >> 4) + dx, ((int)EyeZ >> 4) + dz))), (EyeX, EyeZ));
            _ring?.Update(EyeX, EyeZ, Game.RealRadius);
            View.Update(EyeX, EyeZ, Vox, _released.Add, null);
            Upload();
            View.Reassemble();

            // The frame lasts until the workers have done a few jobs; the guard is a hang guard, not a speed check.
            var until = HangGuard.DeadlineAfter(60_000);
            while (Pool.Pending > 0 && Pool.Completed + Pool.Skipped - before < JobsPerFrame && !HangGuard.Passed(until))
            {
                Thread.Sleep(1);
            }

            Assert.Null(Pool.Failure);
        }

        public void Dispose() => Pool.Dispose();
    }

    // Frames until the view has been covered once (every leaf the chunks do not show has a tile or a stand-in), bounded by progress: a frame always sees the workers do jobs, so the bound counts frames, not time.
    private static void ColdStart(Rig rig, int maxFrames)
    {
        while (rig.View.CoveredAfterMilliseconds is null && rig.Frame < maxFrames)
        {
            rig.Step();
        }

        Assert.NotNull(rig.View.CoveredAfterMilliseconds);
    }

    [Fact]
    public void Given_AFlightOutAndBackWithTheRingAndRealWorkers_When_EveryFrameIsAssembled_Then_NoLeafIsUncoveredInAnyFrameAfterTheFirstCover()
    {
        using var rig = new Rig(_released, ring: true);
        rig.JobsPerFrame = 4;
        ColdStart(rig, 6_000);
        var covered = rig.Frame;
        var uncovered = new List<(int Frame, int Leaves)>();
        const int Out = 300;
        const double Speed = 3.0;
        for (var i = 0; i < 2 * Out; i++)
        {
            rig.EyeX += i < Out ? Speed : -Speed;
            rig.Step();
            if (rig.View.Uncovered != 0)
            {
                uncovered.Add((rig.Frame, rig.View.Uncovered));
            }
        }

        Output.WriteLine($"covered at frame {covered}; flight of {2 * Out} frames; frames with an uncovered leaf: {uncovered.Count}: {string.Join(", ", uncovered.Take(20).Select(static u => $"{u.Frame}:{u.Leaves}"))}; tiles built {rig.View.Stats.Built}, cached {rig.View.Stats.Cached}, released {_released.Count}");
        Assert.Empty(uncovered);
    }

    // B51: the stand-ins drawn inside windows in this frame: along every edge of every window the stand-in's wall must reach below the ground beside it (the neighbouring drawn tile's, or, where nothing far is drawn there because the chunks show it, the real
    // blocks' at level 0), or the sky shows through the slot between them. Returns the blocks of window edge looked at and the ones left open.
    private static (int Samples, int Open, string First, float Worst) OpenWindowEdges(Rig rig, Dictionary<FarTileKey, SeamProbe.GroundMap> grounds)
    {
        SeamProbe.GroundMap GroundOf(FarTileKey key)
        {
            if (!grounds.TryGetValue(key, out var map))
            {
                grounds[key] = map = new SeamProbe.GroundMap(rig.View.World, Game, key);
            }

            return map;
        }

        var (samples, open, first, worst) = (0, 0, "", 0f);
        var drawn = rig.View.Draw;
        foreach (var stand in drawn.Where(static t => t.Windows is { Count: > 0 }))
        {
            if (!rig.Meshes.TryGetValue(stand.Key, out var mesh) || mesh.Granules is null)
            {
                continue;
            }

            var standGround = GroundOf(stand.Key);
            foreach (var w in stand.Windows!)
            {
                var quads = SeamProbe.Quads(mesh, SeamProbe.Drawn(mesh, w));
                foreach (var (alongX, c, side, from, to) in new[] { (true, (float)w.MinX, 1, w.MinZ, w.MaxZ), (true, (float)w.MaxX, -1, w.MinZ, w.MaxZ), (false, (float)w.MinZ, 1, w.MinX, w.MaxX), (false, (float)w.MaxZ, -1, w.MinX, w.MaxX) })
                {
                    for (var p = from; p < to; p += 2)
                    {
                        // The block just outside the window on this side.
                        var (bx, bz) = alongX ? (c - (side * 0.5f), p + 0.5f) : (p + 0.5f, c - (side * 0.5f));
                        var beside = drawn.FirstOrDefault(t => t != stand && Covers(t, Game, bx, bz));
                        var besideKey = beside?.Key ?? new FarTileKey(0, (int)Math.Floor(bx / Game.SizeOf(0)), (int)Math.Floor(bz / Game.SizeOf(0)));
                        if (beside is null && Math.Sqrt(((bx - rig.EyeX) * (bx - rig.EyeX)) + ((bz - rig.EyeZ) * (bz - rig.EyeZ))) > Vox + 16)
                        {
                            continue; // nothing is drawn there at all: the leaf is not asked for yet (the coverage tests own that)
                        }

                        var top = GroundOf(besideKey).TopAt(bx, bz);
                        if (top is null)
                        {
                            continue;
                        }

                        samples++;
                        var gap = SeamProbe.OpenBeside(quads, standGround, alongX, c, side, p, top.Value);
                        if (gap > 0.01f)
                        {
                            open++;
                            worst = Math.Max(worst, gap);
                            first = gap < worst ? first : $"frame {rig.Frame}: stand-in {stand.Key} window {w} edge {(alongX ? "x" : "z")}={c} at {p}: wall stops {gap} blocks above the ground beside it ({(beside is null ? "chunks" : beside.Key.ToString())})";
                        }
                    }
                }
            }
        }

        return (samples, open, first, worst);
    }

    // Whether a drawn tile draws the ground at a block: inside its footprint and, when it stands in inside windows, inside one of them.
    private static bool Covers(FarTile tile, FarViewSettings settings, float x, float z)
    {
        var size = settings.SizeOf(tile.Key.Level);
        var (x0, z0) = (tile.Key.MinX(settings), tile.Key.MinZ(settings));
        return x >= x0 && x < x0 + size && z >= z0 && z < z0 + size && (tile.Windows is null || tile.Windows.Any(w => x >= w.MinX && x < w.MaxX && z >= w.MinZ && z < w.MaxZ));
    }

    [Fact]
    public void Given_AFlightOverAFillingView_When_EveryFrameIsAssembled_Then_NoStandInWindowEdgeLeavesASlotToTheSkyAndTheZoneEdgeIsCoveredByFarLand()
    {
        using var rig = new Rig(_released, ring: true);
        rig.JobsPerFrame = 2;
        var grounds = new Dictionary<FarTileKey, SeamProbe.GroundMap>();
        long samples = 0;
        long open = 0;
        long standInFrames = 0;
        var worstGap = 0f;
        var first = "";
        var zoneUncovered = new List<string>();
        var inner = Game.InnerRadius(Vox);
        Assert.True(inner <= Vox - 16, "the far land starts a chunk or more inside the voxel zone's edge");
        for (var i = 0; i < 700; i++)
        {
            if (i >= 60)
            {
                rig.EyeX += 3.0;
            }

            rig.Step();
            if (rig.View.CoveredAfterMilliseconds is null || i % 7 != 0)
            {
                continue;
            }

            // The ground just outside the radius the far land starts at, all round the eye: some drawn tile draws it, whether the chunks are drawn there or not (the far land is behind them).
            for (var a = 0; a < 90; a++)
            {
                var angle = a * Math.PI / 45;
                var (x, z) = (rig.EyeX + ((inner + 3) * Math.Cos(angle)), rig.EyeZ + ((inner + 3) * Math.Sin(angle)));
                if (!rig.View.Draw.Any(t => Covers(t, Game, (float)x, (float)z)) && zoneUncovered.Count < 5)
                {
                    zoneUncovered.Add($"frame {rig.Frame}: nothing far is drawn at {x:0},{z:0}, {inner + 3:0} blocks from the eye");
                }
            }

            var (s, o, f, w) = OpenWindowEdges(rig, grounds);
            samples += s;
            open += o;
            if (w > worstGap)
            {
                (worstGap, first) = (w, f);
            }

            standInFrames += rig.View.Draw.Any(static t => t.Windows is { Count: > 0 }) ? 1 : 0;
        }

        Output.WriteLine($"{standInFrames} sampled frames with stand-ins inside windows; {samples} blocks of window edge looked at, {open} open (worst {worstGap} blocks); worst: {first}; zone edge: {zoneUncovered.Count} uncovered samples");
        Assert.True(standInFrames > 0, "the flight must show stand-ins inside windows, or it checks nothing");
        Assert.True(open == 0, $"{open} of {samples} blocks of window edge are open; first: {first}");
        Assert.Empty(zoneUncovered);
    }

    [Fact]
    public void Given_AStandInOnScreenAndTheSectionsUnderItChange_When_EveryFrameIsAssembled_Then_ItKeepsDrawingUntilTheLeavesUnderItAreDrawnAndNoLeafIsUncovered()
    {
        // The ring's footprints write mips at levels 1 and up through LodStore.Changed: the level-4 tile that stands in for waiting leaves was disposed while drawn, and its leaves showed sky until each was built (finding 1).
        var open = false;
        using var rig = new Rig(_released, ring: false, (_, _) => !open);
        ColdStart(rig, 6_000);
        for (var i = 0; i < 40; i++)
        {
            rig.Step();
        }

        var stand = rig.View.Draw.Take(rig.View.FallbackCount).Where(static t => t.Windows is { Count: > 0 }).ToList();
        Assert.NotEmpty(stand);
        Assert.Equal(0, rig.View.Uncovered);
        var store = rig.View.World.Store;
        foreach (var tile in stand)
        {
            store.Changed.Enqueue(new LodSectionKey(tile.Key.Level, tile.Key.Tx, 0, tile.Key.Tz));
        }

        var uncovered = new List<(int Frame, int Leaves)>();
        for (var i = 0; i < 60; i++)
        {
            rig.Step();
            if (rig.View.Uncovered != 0)
            {
                uncovered.Add((rig.Frame, rig.View.Uncovered));
            }
        }

        Output.WriteLine($"{stand.Count} stand-in(s) with windows changed under them; frames with an uncovered leaf: {uncovered.Count}: {string.Join(", ", uncovered.Take(20).Select(static u => $"{u.Frame}:{u.Leaves}"))}");
        Assert.Empty(uncovered);

        // The ring's data is in: the leaves are built, the old stand-ins are retired and nothing stale is left.
        open = true;
        var until = HangGuard.DeadlineAfter(HangGuardMilliseconds);
        while ((rig.View.Missing > 0 || rig.View.Waiting > 0 || rig.View.Stale > 0 || rig.View.Unuploaded > 0 || rig.View.Requested.Count > 0) && !HangGuard.Passed(until))
        {
            rig.Step();
            Assert.Equal(0, rig.View.Uncovered);
        }

        for (var i = 0; i < 3; i++)
        {
            rig.Step(); // a changed tile is retired by the first update that finds it undrawn: the last frame's uploads drew the leaves
        }

        Output.WriteLine($"settled: cached {rig.View.Cache.Count}, released {_released.Count}, stale {rig.View.Stale}");
        Assert.Equal(0, rig.View.Missing);
        Assert.Equal(0, rig.View.Stale);
        // Memory stays bounded: a changed stand-in that was kept for drawing is freed once its leaves are drawn (it is not kept as a stale tile).
        Assert.All(stand, tile => Assert.Contains(tile, _released));
    }

    private static readonly FarViewSettings Small192 = new(2048, MaxTiles: 400, TreeLevels: 1, RealRadius: 192);

    [Fact]
    public void Given_StandInsBeingBuilt_When_TheEyeMovesAndTheLeavesAreChosenAgain_Then_EveryOneThatStandsInForANewLeafStaysWanted()
    {
        // Finding 2: the old re-selection reset the wanted set without them, and a worker that reached the job before the stand-ins were asked for again dropped it as stale, leaving its key "being built" for good.
        using var pool = new JobPool(0);
        var view = Make(pool, Small192);
        view.Update(192, 192, 192, Release, 0);
        var leaves = FarTileSelector.Select(Small192, 192, 192, 192, false, 1.0).ToHashSet();
        var inFlight = view.Requested.Where(k => !leaves.Contains(k) && k.Level >= 4).ToList();
        Assert.NotEmpty(inFlight);

        view.Reselect(208, 192, 192);

        var standing = new HashSet<FarTileKey>();
        foreach (var leaf in FarTileSelector.Select(Small192, 208, 192, 192, false, 1.0))
        {
            var ancestor = leaf;
            for (var step = 0; step < 4 && ancestor.Level < Small192.TopLevel; step++)
            {
                ancestor = ancestor.GetParent();
                _ = standing.Add(ancestor);
            }
        }

        var kept = inFlight.Where(standing.Contains).ToList();
        Output.WriteLine($"{inFlight.Count} stand-ins in flight, {kept.Count} stand in for a leaf of the new selection");
        Assert.NotEmpty(kept);
        Assert.All(kept, key => Assert.True(view.IsWanted(key), $"{key} was dropped from the wanted set while in flight"));
    }

    [Fact]
    public void Given_ATinyCacheAndAFlightOverWaitingLeaves_When_TheViewIsTrimmed_Then_NoTileThatCanStandInForAWaitingLeafIsEvicted()
    {
        // Finding 8: Trim kept what was drawn, wanted or pending, so a built stand-in that drew nothing at the moment (every leaf under it covered by a nearer tile) was evicted, and the next leaf to wait under it found only sky.
        var tiny = Small192 with { MaxTiles = 8 };
        using var pool = new JobPool(0);
        var view = Make(pool, tiny);
        view.SetRingWait(static (_, _) => true); // the fine leaves wait: stand-ins draw for them
        var (eyeX, eyeZ) = (192.0, 192.0);
        var selectedAt = (X: double.NaN, Z: 0.0);
        var checkedFrames = 0;
        for (var frame = 0; frame < 500; frame++)
        {
            if (frame >= 150)
            {
                eyeX += 3;
            }

            var before = view.Cache.Keys.ToHashSet();
            view.Update(eyeX, eyeZ, 192, Release, 2.0);
            if (double.IsNaN(selectedAt.X) || Math.Abs(eyeX - selectedAt.X) >= 8)
            {
                selectedAt = (eyeX, eyeZ);
            }

            foreach (var leaf in FarTileSelector.Select(tiny, selectedAt.X, selectedAt.Z, 192, false, 1.0))
            {
                if (view.Cache.TryGetValue(leaf, out var own) && own.OnCard)
                {
                    continue;
                }

                var ancestor = leaf;
                for (var step = 0; step < 4 && ancestor.Level < tiny.TopLevel; step++)
                {
                    ancestor = ancestor.GetParent();
                    if (before.Contains(ancestor))
                    {
                        Assert.True(view.Cache.ContainsKey(ancestor), $"frame {frame}: {ancestor} stands in for the waiting leaf {leaf} and was evicted");
                        checkedFrames++;
                    }
                }
            }

            foreach (var tile in view.PendingTiles.ToList())
            {
                tile.Adopt(tile.Pending!);
                tile.Pending = null;
                tile.OnCard = true;
            }

            view.Reassemble();
        }

        Output.WriteLine($"{checkedFrames} ancestor checks");
        Assert.True(checkedFrames > 0);
    }
}
