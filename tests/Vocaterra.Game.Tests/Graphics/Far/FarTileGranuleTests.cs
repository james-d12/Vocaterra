using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Tests.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>
/// B47: a tile built cut into granules (<see cref="FarGranules"/>) can be drawn inside any set of whole leaves' footprints with nothing clipped on the card: every quad lies in one granule, the runs of a window are exactly the quads inside it, and the surface is the
/// surface of the tile built whole (only merged less).
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarTileGranuleTests(ITestOutputHelper output)
{
    private static readonly FarViewSettings Settings = new(16384);

    private sealed record Quad(Vector3 Min, Vector3 Max, Vector3 Normal, float Area, int Id);

    private static List<Quad> Quads(FarTileMesh mesh)
    {
        var quads = new List<Quad>();
        for (var q = 0; q < mesh.VertexCount / 4; q++)
        {
            var v = mesh.Vertices.Skip(q * 4).Take(4).Select(static x => x.Position).ToArray();
            var min = v.Aggregate(Vector3.Min);
            var max = v.Aggregate(Vector3.Max);
            quads.Add(new Quad(min, max, Vector3.Normalize(Vector3.Cross(v[1] - v[0], v[2] - v[0])), Vector3.Cross(v[1] - v[0], v[3] - v[0]).Length(), q));
        }

        return quads;
    }

    // The ids of the quads a list of runs of one bucket draws.
    private static List<int> Drawn(FarTileMesh mesh, List<(int First, int Count)> runs)
    {
        var drawn = new List<int>();
        foreach (var (first, count) in runs)
        {
            for (var at = first; at < first + count; at += 6)
            {
                drawn.Add(mesh.IndexAt(at) / 4);
            }
        }

        return drawn;
    }

    [Fact]
    public void Given_TilesOfEveryLevelOverRealLand_When_TheyAreBuiltCut_Then_EveryQuadLiesInOneGranuleAndAWindowDrawsExactlyTheQuadsInsideIt()
    {
        var world = StreamedWorlds.Composition.World.Far.FarWorld(new LodStore(), 4);
        long cutQuads = 0;
        long wholeQuads = 0;
        foreach (var level in new[] { 0, 1, 2, 3, 4, 5, 6, 7 })
        {
            foreach (var (tx, tz) in new[] { (3000, 1200), (2200, 900) })
            {
                var key = new FarTileKey(level, tx >> (5 + level), tz >> (5 + level));
                var whole = FarTileMesher.Build(world, Settings, key);
                var cut = FarTileMesher.Build(world, Settings, key, cut: true);
                var granules = cut.Granules!;
                var (side, blocks) = (granules.Side, granules.Blocks);
                Assert.Equal(FarGranules.SideOf(Settings, level), side);
                Assert.Equal(Settings.SizeOf(level), side * blocks);
                Assert.Null(whole.Granules);
                cutQuads += cut.VertexCount / 4;
                wholeQuads += whole.VertexCount / 4;
                output.WriteLine($"level {level}: {whole.VertexCount / 4} quads whole, {cut.VertexCount / 4} cut ({side} x {side} granules of {blocks} blocks)");

                // The surface is the same: the area of each facing and bucket (a cut quad is a piece of a whole one).
                foreach (var bucket in new[] { FarBucket.Opaque, FarBucket.Lit, FarBucket.Water })
                {
                    var (a0, a1) = granules.Bucket(bucket);
                    Assert.Equal(bucket == FarBucket.Opaque ? cut.OpaqueIndices : bucket == FarBucket.Lit ? cut.LitIndices : cut.WaterIndices, a1 - a0);
                }

                var cutQuadList = Quads(cut);
                var wholeQuadList = Quads(whole);
                foreach (var facing in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ })
                {
                    var areaCut = cutQuadList.Where(q => Vector3.Dot(q.Normal, facing) > 0.99f).Sum(static q => (double)q.Area);
                    var areaWhole = wholeQuadList.Where(q => Vector3.Dot(q.Normal, facing) > 0.99f).Sum(static q => (double)q.Area);
                    Assert.Equal(areaWhole, areaCut, Math.Max(1.0, areaWhole * 1e-6));
                }

                // Each granule's run holds quads that lie inside its square (a wall on the edge belongs to the granule of its voxel and may lie on either edge).
                var seen = new HashSet<int>();
                foreach (var bucket in new[] { FarBucket.Opaque, FarBucket.Lit, FarBucket.Water })
                {
                    var runs = new List<(int First, int Count)>();
                    for (var gz = 0; gz < side; gz++)
                    {
                        for (var gx = 0; gx < side; gx++)
                        {
                            var (x0, z0) = (granules.OriginX + (gx * blocks), granules.OriginZ + (gz * blocks));
                            granules.Runs(bucket, [new FarWindow(x0, z0, x0 + blocks, z0 + blocks)], runs);
                            foreach (var id in Drawn(cut, runs))
                            {
                                Assert.True(seen.Add(id), $"level {level}: quad {id} is in two granules");
                                var q = cutQuadList[id];
                                Assert.True(q.Min.X >= x0 && q.Max.X <= x0 + blocks && q.Min.Z >= z0 && q.Max.Z <= z0 + blocks, $"level {level}: quad {id} ({q.Min}..{q.Max}) leaves its granule {gx},{gz} ({x0},{z0})");
                            }
                        }
                    }
                }

                Assert.Equal(cut.VertexCount / 4, seen.Count);

                // A set of windows of whole leaves of the finest sizes this level can have under it: the runs draw the quads inside them, and every flat top whose middle is inside one.
                var windows = new List<FarWindow>();
                var origin = (granules.OriginX, granules.OriginZ);
                var size = Settings.SizeOf(level);
                for (var finer = Math.Max(0, level - 4); finer < level; finer++)
                {
                    var leaf = Settings.SizeOf(finer);
                    var spots = size / leaf;
                    for (var k = 0; k < spots; k += 3)
                    {
                        var (ix, iz) = (k % spots, (k * 7 / 3) % spots);
                        var w = new FarWindow(origin.OriginX + (ix * leaf), origin.OriginZ + (iz * leaf), origin.OriginX + ((ix + 1) * leaf), origin.OriginZ + ((iz + 1) * leaf));
                        if (!windows.Any(o => o.MinX < w.MaxX && w.MinX < o.MaxX && o.MinZ < w.MaxZ && w.MinZ < o.MaxZ))
                        {
                            windows.Add(w);
                        }
                    }
                }

                var drawnTops = new HashSet<int>();
                var runsOf = new List<(int First, int Count)>();
                granules.Runs(FarBucket.Opaque, windows, runsOf);
                var drawnIds = Drawn(cut, runsOf);
                Assert.Equal(drawnIds.Count, drawnIds.Distinct().Count());
                foreach (var id in drawnIds)
                {
                    var q = cutQuadList[id];
                    Assert.Contains(windows, w => q.Min.X >= w.MinX && q.Max.X <= w.MaxX && q.Min.Z >= w.MinZ && q.Max.Z <= w.MaxZ);
                    _ = drawnTops.Add(id);
                }

                var opaqueQuads = cut.OpaqueIndices / 6;
                var inside = 0;
                for (var id = 0; id < opaqueQuads; id++)
                {
                    var q = cutQuadList[id];
                    if (q.Normal.Y < 0.99f)
                    {
                        continue;
                    }

                    var (mx, mz) = ((q.Min.X + q.Max.X) / 2, (q.Min.Z + q.Max.Z) / 2);
                    if (windows.Any(w => mx > w.MinX && mx < w.MaxX && mz > w.MinZ && mz < w.MaxZ))
                    {
                        inside++;
                        Assert.Contains(id, drawnTops);
                    }
                }

                output.WriteLine($"  {windows.Count} windows: {drawnIds.Count} of {opaqueQuads} opaque quads drawn ({inside} tops inside)");
            }
        }

        output.WriteLine($"all levels: {wholeQuads} quads whole, {cutQuads} cut ({(double)cutQuads / Math.Max(1, wholeQuads):0.00}x)");
    }

    [Fact]
    public void Given_ATileWithNoWindows_When_ItsRunsAreAsked_Then_ItIsDrawnWholeInEveryBucket()
    {
        var world = StreamedWorlds.Composition.World.Far.FarWorld(new LodStore(), 4);
        var mesh = FarTileMesher.Build(world, Settings, new FarTileKey(4, 3000 >> 9, 1200 >> 9), cut: true);
        var tile = new FarTile(new FarTileKey(4, 3000 >> 9, 1200 >> 9));
        tile.Adopt(mesh);
        var runs = new List<(int First, int Count)>();
        tile.RunsOf(FarBucket.Opaque, runs);
        Assert.Equal([(0, mesh.OpaqueIndices)], runs);
        tile.RunsOf(FarBucket.Water, runs);
        Assert.Equal(mesh.WaterIndices > 0 ? [(mesh.OpaqueIndices + mesh.LitIndices, mesh.WaterIndices)] : [], runs);
        tile.Windows = [];
        tile.RunsOf(FarBucket.Opaque, runs);
        Assert.Empty(runs);
    }
}
