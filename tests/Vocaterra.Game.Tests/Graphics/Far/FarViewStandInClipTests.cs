using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>
/// B42: a stand-in used to hide every leaf under it that was already on the card, so a coarse tile of 512 blocks popped over finished fine land whenever a new leaf appeared under it (the owner's "heavy flickering of the low LODs in chunks that
/// are fully rendered"). A stand-in is now drawn only where leaves wait, never over a leaf that is on the card nor over what the chunks show, and what a ground cell shows never goes coarse and fine again.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarViewStandInClipTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    private static readonly FarViewSettings Wide = new(2048, MaxTiles: 600, TreeLevels: 1, RealRadius: 192);
    private const int Vox = 192;
    private const int Cell = 32; // the finest tile's side: every footprint of every level is a whole number of cells
    private const int Meshing = 20; // frames a chunk column takes to be drawn once it is in the voxel zone

    // B47: the windows are not only what the view hands the renderer: both paths draw the runs of a stand-in's indices that RunsOf gives, so what is checked here is the geometry those runs draw. Every quad drawn lies inside a window, and
    // every flat top whose middle is inside a window is drawn (nothing shows sky there); a stand-in that has windows has its granules (without them it would be drawn whole).
    private static void AssertDrawsOnlyItsWindows(FarTile tile, FarTileMesh mesh, HashSet<string> checkedBefore)
    {
        var windows = tile.Windows!;
        var signature = $"{tile.Key}:{string.Join(';', windows)}";
        if (!checkedBefore.Add(signature))
        {
            return;
        }

        Assert.NotNull(tile.Granules);
        var runs = new List<(int First, int Count)>();
        var drawn = new HashSet<int>();
        foreach (var bucket in new[] { FarBucket.Opaque, FarBucket.Lit, FarBucket.Water })
        {
            tile.RunsOf(bucket, runs);
            var (first, end) = tile.Granules!.Bucket(bucket);
            foreach (var (at, count) in runs)
            {
                Assert.InRange(at, first, end);
                Assert.InRange(at + count, first, end);
                for (var i = at; i < at + count; i += 6)
                {
                    var quad = mesh.IndexAt(i) / 4;
                    Assert.True(drawn.Add(quad), $"{tile.Key}: a quad is drawn twice");
                    var corners = Enumerable.Range(0, 4).Select(k => mesh.VertexArray[(quad * 4) + k].Position).ToArray();
                    var (minX, maxX, minZ, maxZ) = (corners.Min(static c => c.X), corners.Max(static c => c.X), corners.Min(static c => c.Z), corners.Max(static c => c.Z));
                    Assert.True(windows.Any(w => minX >= w.MinX && maxX <= w.MaxX && minZ >= w.MinZ && maxZ <= w.MaxZ), $"{tile.Key}: a quad at x {minX}..{maxX}, z {minZ}..{maxZ} is drawn outside every window");
                }
            }
        }

        var opaque = mesh.OpaqueIndices / 6;
        for (var q = 0; q < opaque; q++)
        {
            var corners = Enumerable.Range(0, 4).Select(k => mesh.VertexArray[(q * 4) + k].Position).ToArray();
            if (corners.Max(static c => c.Y) != corners.Min(static c => c.Y) || corners.Max(static c => c.X) == corners.Min(static c => c.X))
            {
                continue; // a top or a bottom only: a flat quad with an area
            }

            var (mx, mz) = ((corners.Min(static c => c.X) + corners.Max(static c => c.X)) / 2, (corners.Min(static c => c.Z) + corners.Max(static c => c.Z)) / 2);
            if (windows.Any(w => mx > w.MinX && mx < w.MaxX && mz > w.MinZ && mz < w.MaxZ))
            {
                Assert.True(drawn.Contains(q), $"{tile.Key}: a flat quad at {mx},{mz} is inside a window and not drawn");
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_AFreshFillAndAWalkOfTheEye_When_EveryFrameIsAssembled_Then_NoGroundIsDrawnTwiceNoFineLeafLosesItsGroundAndNoCellGoesFineCoarseFine(bool gl)
    {
        using var pool = new JobPool(0);
        var view = Make(pool, Wide);
        var (eyeX, eyeZ) = (192.0, 192.0);
        var readyAt = new Dictionary<(int, int), int>();
        var frame = 0;
        bool Ready(int cx, int cz)
        {
            var (dx, dz) = ((cx * 16) + 8 - eyeX, (cz * 16) + 8 - eyeZ);
            if ((dx * dx) + (dz * dz) > (double)Vox * Vox)
            {
                _ = readyAt.Remove((cx, cz));
                return false;
            }

            if (!readyAt.TryGetValue((cx, cz), out var at))
            {
                readyAt[(cx, cz)] = at = frame + Meshing;
            }

            return frame >= at;
        }

        _ = view.UsePath(gl, static _ => { }); // the game's own program or MonoGame's effects (B47): stand-ins are drawn inside their windows in both
        var meshes = new Dictionary<FarTileKey, FarTileMesh>();
        var verified = new HashSet<string>();
        view.SetNearReady(Ready);
        var shown = new Dictionary<(int, int), List<int>>();
        var windowed = 0;
        var whole = 0;
        var until = HangGuard.DeadlineAfter(HangGuardMilliseconds);
        for (frame = 0; frame < 1100 && !HangGuard.Passed(until); frame++)
        {
            foreach (var tile in view.PendingTiles.ToList())
            {
                meshes[tile.Key] = tile.Pending!;
                tile.Adopt(tile.Pending!);
                tile.Pending = null;
                tile.OnCard = true;
            }

            if (frame >= 700)
            {
                eyeX += 0.5;
            }

            view.Update(eyeX, eyeZ, Vox, static _ => { }, 0.0);

            // Who draws each cell of ground this frame: a tile's whole footprint, or a stand-in's windows. A cell with two owners is ground drawn twice.
            var owner = new Dictionary<(int, int), FarTileKey>();
            for (var i = 0; i < view.Draw.Count; i++)
            {
                var tile = view.Draw[i];
                var standIn = i < view.FallbackCount;
                if (standIn)
                {
                    if (tile.Windows is null)
                    {
                        whole++;
                    }
                    else
                    {
                        windowed++;
                        AssertDrawsOnlyItsWindows(tile, meshes[tile.Key], verified);
                    }
                }

                var rects = standIn && tile.Windows is { } windows ? windows.Select(static w => (w.MinX, w.MinZ, w.MaxX, w.MaxZ)).ToList() : [(tile.Key.MinX(Wide), tile.Key.MinZ(Wide), tile.Key.MinX(Wide) + Wide.SizeOf(tile.Key.Level), tile.Key.MinZ(Wide) + Wide.SizeOf(tile.Key.Level))];
                foreach (var (x0, z0, x1, z1) in rects)
                {
                    for (var gz = z0 / Cell; gz < z1 / Cell; gz++)
                    {
                        for (var gx = x0 / Cell; gx < x1 / Cell; gx++)
                        {
                            Assert.True(owner.TryAdd((gx, gz), tile.Key), $"frame {frame}: the cell {gx},{gz} is drawn by {owner.GetValueOrDefault((gx, gz))} and by {tile.Key}");
                        }
                    }
                }
            }

            // B35's purpose kept: every fine leaf the chunks do not show, whose level-4 stand-in is on the card, has its ground drawn by its own tile or a stand-in (nothing shows sky there).
            foreach (var leaf in FarTileSelector.Select(Wide, eyeX, eyeZ, Vox).Where(static l => l.Level < 4))
            {
                var stand = leaf;
                while (stand.Level < 4)
                {
                    stand = stand.GetParent();
                }

                var (x0, z0, size) = (leaf.MinX(Wide), leaf.MinZ(Wide), Wide.SizeOf(leaf.Level));
                if (view.Cache.TryGetValue(stand, out var standTile) && standTile.OnCard && !ChunksShow(x0, z0, x0 + size, z0 + size, eyeX, eyeZ, Ready))
                {
                    Assert.True(owner.ContainsKey((x0 / Cell, z0 / Cell)), $"frame {frame}: the fine leaf {leaf} is neither drawn nor stood in for (sky shows there)");
                }
            }

            if (frame < 700)
            {
                continue;
            }

            foreach (var (cellKey, key) in owner)
            {
                var (cx, cz) = cellKey;
                var (cdx, cdz) = (((cx * Cell) + 16) - eyeX, ((cz * Cell) + 16) - eyeZ);
                if (Math.Abs(cdx) < 500 && Math.Abs(cdz) < 500 && (cdx * cdx) + (cdz * cdz) > 1.2 * Vox * 1.2 * Vox) // outside the chunks' zone: under them a stand-in's backdrop comes and goes unseen
                {
                    if (!shown.TryGetValue(cellKey, out var levels))
                    {
                        shown[cellKey] = levels = [];
                    }

                    if (levels.Count == 0 || levels[^1] != key.Level)
                    {
                        levels.Add(key.Level);
                    }
                }
            }
        }

        var flicker = shown.Where(static kv => HasRevisit(kv.Value)).Select(static kv => $"{kv.Key}: {string.Join(">", kv.Value)}").ToList();
        Output.WriteLine($"{shown.Count} cells watched over the walk; stand-in draws windowed {windowed}, whole {whole}; cells that went to a level and back: {flicker.Count}");
        foreach (var line in flicker.Take(8))
        {
            Output.WriteLine(line);
        }

        Assert.Empty(flicker.Take(5));
        Assert.True(windowed > 0, "the walk made stand-ins to clip");
    }

    // Whether the chunks show a rectangle of ground, as the far view decides it for a leaf (IsNeeded): it lies wholly inside the zone (the farthest corner under the inner radius) and every chunk column of it is drawn.
    private static bool ChunksShow(int x0, int z0, int x1, int z1, double eyeX, double eyeZ, Func<int, int, bool> ready)
    {
        var (dx, dz) = (Math.Max(Math.Abs(eyeX - x0), Math.Abs(eyeX - x1)), Math.Max(Math.Abs(eyeZ - z0), Math.Abs(eyeZ - z1)));
        var far = Math.Sqrt((dx * dx) + (dz * dz));
        if (far >= Wide.InnerFraction * Vox)
        {
            return false;
        }

        for (var cz = z0 >> 4; cz <= (z1 - 1) >> 4; cz++)
        {
            for (var cx = x0 >> 4; cx <= (x1 - 1) >> 4; cx++)
            {
                if (!ready(cx, cz))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasRevisit(List<int> levels)
    {
        for (var i = 2; i < levels.Count; i++)
        {
            if (levels[i] == levels[i - 2])
            {
                return true;
            }
        }

        return false;
    }
}
