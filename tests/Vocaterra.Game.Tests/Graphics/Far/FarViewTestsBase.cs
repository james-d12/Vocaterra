using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>The shared parts of the test classes split out of FarViewTests (B28: xUnit runs the tests of one class one after another, so the slow ones are in classes of their own): the fields and helpers.</summary>
public abstract class FarViewTestsBase(ITestOutputHelper output)
{
    internal static readonly FarViewSettings Small = new(320, MaxTiles: 400, TreeLevels: 1);

    // A small voxel zone, so a settled view is a few tiles (a tile takes a good deal longer to build in Debug).
    internal const int Voxel = 64;

    internal readonly List<FarTile> _released = [];

    internal void Release(FarTile tile) => _released.Add(tile);

    // What the renderer does with the tiles the workers made, without a card: every pending mesh is taken as uploaded, and the view is assembled again (so Draw lists what a frame would draw).
    internal static void Land(FarView view, double x, double z)
    {
        foreach (var tile in view.PendingTiles.ToList())
        {
            tile.Adopt(tile.Pending!);
            tile.Pending = null;
            tile.OnCard = true;
        }

        view.Update(x, z, Voxel, _ => { }, null);
    }

    // No tile of the draw list lies over another's ground: levels never overlap, so no face of a coarser tile is behind or in front of a finer one's (the review's H1).
    internal static void AssertNoOverlap(FarView view)
    {
        var settings = view.Settings;
        var tiles = view.Draw.Select(static t => t.Key).ToList();
        foreach (var a in tiles)
        {
            foreach (var b in tiles)
            {
                if (a == b)
                {
                    continue;
                }

                var (sa, sb) = (settings.SizeOf(a.Level), settings.SizeOf(b.Level));
                var overlap = a.MinX(settings) < b.MinX(settings) + sb && b.MinX(settings) < a.MinX(settings) + sa
                    && a.MinZ(settings) < b.MinZ(settings) + sb && b.MinZ(settings) < a.MinZ(settings) + sa;
                Assert.False(overlap, $"{a} and {b} overlap");
            }
        }
    }

    internal static FarView Make(JobPool pool, FarViewSettings? settings = null) => new(StreamedWorlds.Composition.World.Far, settings ?? Small, pool);

    internal sealed class BrokenDisk : Vocaterra.Game.Core.World.Lod.ILodPersistence
    {
        public byte[]? Load(Vocaterra.Game.Core.World.Lod.LodSectionKey key) => throw new InvalidOperationException("a disk that fails");

        public void Save(Vocaterra.Game.Core.World.Lod.LodSectionKey key, byte[] blob)
        {
        }
    }

    // These tests wait on a condition (a job started, a job finished, a tile built), never on the clock: the guard against a hang is progress (SettleGuard: a slice of two minutes in which nothing was built fails; a slow machine
    // that keeps building does not), and the waits on a job are bounded at ten minutes.
    internal const long HangGuardMilliseconds = 600_000;

    internal sealed class GatedDisk : Vocaterra.Game.Core.World.Lod.ILodPersistence
    {
        public ManualResetEventSlim Started { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public byte[]? Load(Vocaterra.Game.Core.World.Lod.LodSectionKey key)
        {
            Started.Set();
            Release.Wait();
            return null;
        }

        public void Save(Vocaterra.Game.Core.World.Lod.LodSectionKey key, byte[] blob)
        {
        }
    }

    protected ITestOutputHelper Output => output;
}
