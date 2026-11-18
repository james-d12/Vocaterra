using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>The shared parts of the test classes split out of ChunkResidencyTests (B28: xUnit runs the tests of one class one after another, so the slow ones are in classes of their own): the fields and helpers.</summary>
public abstract class ChunkResidencyTestsBase
{
    internal static readonly BandStreamPolicy Small = new(6, 8, 2, 3);

    internal static (float X, float Y, float Z) Eye(Scene scene) => (scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);

    internal static ChunkResidency Make(Scene scene, JobPool pool, BandStreamPolicy? policy = null, int max = int.MaxValue) =>
        new(scene.Grid, scene.Streaming!, policy ?? Small, pool, scene.Places, max);

    internal static HashSet<VoxelChunkCoord> Resident(Scene scene) => [.. GridResidency.Resident(scene.Grid)];

    internal sealed class ThreadRecorder : IChunkResidencyListener
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

    // ------------------------------------------------------------------ residency never changes a result

    // The player's walk, out of the valley through the pass and round, as eye positions.
    internal static readonly (int X, int Y, int Z)[] Walk =
    [
        (200, 70, 200), (320, 70, 215), (380, 60, 208), (420, 60, 208), (470, 70, 208), (600, 80, 260), (700, 90, 300), (520, 80, 90), (100, 60, -100), (-300, 70, 50), (200, 60, -250), (200, 70, 200),
    ];

    // The edits: a step of the walk and a cell; the last but one puts back what an earlier one changed.
    internal static readonly (int Step, int X, int Y, int Z, BlockId? Block)[] Edits =
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

    internal static (string Digest, IReadOnlyList<string> Save, int Resident) Run(BandStreamPolicy policy, int workers, int framesPerStep, bool settle)
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
                HangGuard.Settled(limit => residency.Settle(x, y, z, limit));
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

    internal static int Compare(VoxelChunkCoord a, VoxelChunkCoord b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X);
}
