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

        Assert.Equal(0, rig.View.Missing);
        Assert.Equal(0, rig.View.Stale);
        Assert.True(rig.View.Cache.Count <= Game.MaxTiles);
    }
}
