using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Graphics.Far;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>
/// B51: the joins between pieces of far land leave no opening to the sky. A coarse stand-in is drawn only inside the windows of the leaves that wait under it (B47), so the edge of a window is a cut through the middle of the tile: next to it a finer tile
/// draws its own, lower or higher, ground. The cut edge has to hang a wall down below the coarse ground, as a tile's outer edge does, or the finer ground's top can lie under the coarse tile's last wall face and the sky shows through the slot between them.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarSeamTests(ITestOutputHelper output)
{
    private static readonly FarViewSettings Settings = new(16384);

    private static LodWorld WorldOf(ulong seed) => WorldFactory.Compose(TestGame.Data, seed, RegionCatalog.Empty).World.Far.FarWorld(new LodStore(), 4);

    // The places that are land far from the valley, in blocks (the tiles of each level lie on the one lattice).
    private static readonly (int X, int Z)[] Places = [(3000, 1200), (2200, 900), (-1800, 2600)];

    [Fact]
    public void Given_ACoarseStandInDrawnInsideAWindowInTheMiddleOfItsTile_When_FinerTilesAreBesideIt_Then_NoCutEdgeHasAnOpeningToTheSky()
    {
        long samples = 0;
        long open = 0;
        long openStandInHigher = 0;
        long openLeafHigher = 0;
        long openLeafHigherNear = 0;
        var worst = new Dictionary<int, SeamProbe.Seam>();
        var table = new SortedDictionary<(int Level, int Finer), (int Samples, int Open, float Gap)>();
        var examplesStandIn = new List<string>();
        var examplesNear = new List<string>();
        foreach (var seed in new ulong[] { 42, 7900, 20261119 })
        {
            var world = WorldOf(seed);
            foreach (var level in new[] { 1, 2, 3, 4, 5, 6 })
            {
                foreach (var finer in Enumerable.Range(Math.Max(0, level - 4), level - Math.Max(0, level - 4)).Where(f => FarGranules.Fits(Settings, level, f)))
                {
                    foreach (var (px, pz) in Places)
                    {
                        var key = new FarTileKey(level, (int)Math.Floor(px / (double)Settings.SizeOf(level)), (int)Math.Floor(pz / (double)Settings.SizeOf(level)));
                        var stand = FarTileMesher.Build(world, Settings, key, cut: true);
                        var leaf = Settings.SizeOf(finer);
                        var n = Settings.SizeOf(level) / leaf;
                        var at = n >= 4 ? 1 : 0;
                        var (x0, z0) = (key.MinX(Settings) + (at * leaf), key.MinZ(Settings) + (at * leaf));
                        var window = new FarWindow(x0, z0, x0 + leaf, z0 + leaf);
                        var coarse = SeamProbe.Quads(stand, SeamProbe.Drawn(stand, window));
                        var coarseGround = new SeamProbe.GroundMap(world, Settings, key);

                        // The four neighbours of the window, a finer leaf each, where they lie inside the stand-in's tile.
                        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            var (ix, iz) = (at + dx, at + dz);
                            if (ix < 0 || iz < 0 || ix >= n || iz >= n)
                            {
                                continue;
                            }

                            var neighbour = new FarTileKey(finer, (key.MinX(Settings) / leaf) + ix, (key.MinZ(Settings) / leaf) + iz);
                            var fine = SeamProbe.Quads(FarTileMesher.Build(world, Settings, neighbour, cut: true));
                            var fineGround = new SeamProbe.GroundMap(world, Settings, neighbour);
                            var seam = dx != 0
                                ? SeamProbe.Measure($"seed {seed} stand-in level {level} at {x0},{z0} over leaf {finer} {(dx > 0 ? "east" : "west")}", dx > 0 ? fine : coarse, dx > 0 ? fineGround : coarseGround, dx > 0 ? coarse : fine, dx > 0 ? coarseGround : fineGround, true, dx > 0 ? x0 + leaf : x0, z0, z0 + leaf)
                                : SeamProbe.Measure($"seed {seed} stand-in level {level} at {x0},{z0} over leaf {finer} {(dz > 0 ? "south" : "north")}", dz > 0 ? fine : coarse, dz > 0 ? fineGround : coarseGround, dz > 0 ? coarse : fine, dz > 0 ? coarseGround : fineGround, false, dz > 0 ? z0 + leaf : z0, x0, x0 + leaf);
                            var was0 = table.GetValueOrDefault((level, finer));
                            table[(level, finer)] = (was0.Samples + seam.Samples, was0.Open + seam.Open, Math.Max(was0.Gap, seam.WorstGap));
                            samples += seam.Samples;
                            open += seam.Open;

                            // The positive side is the finer leaf for an east or south neighbour (dx or dz above 0), else the stand-in.
                            var coarseIsPositive = dx < 0 || dz < 0;
                            openStandInHigher += coarseIsPositive ? seam.OpenUnderNegative : seam.OpenUnderPositive;
                            var leafHigher = coarseIsPositive ? seam.OpenUnderPositive : seam.OpenUnderNegative;
                            openLeafHigher += leafHigher;
                            openLeafHigherNear += level - finer <= 2 ? leafHigher : 0;
                            if (seam.Open > 0)
                            {
                                if (!worst.TryGetValue(level, out var was) || was.WorstGap < seam.WorstGap)
                                {
                                    worst[level] = seam;
                                }

                                var text = $"{seam.Where}: {seam.Open} of {seam.Samples} blocks open, worst {seam.WorstGap} ({seam.Worst})";
                                if ((coarseIsPositive ? seam.OpenUnderNegative : seam.OpenUnderPositive) > 0)
                                {
                                    examplesStandIn.Add(text);
                                }
                                else if (level - finer <= 2)
                                {
                                    examplesNear.Add(text);
                                }
                            }
                        }
                    }
                }
            }
        }

        foreach (var (level, seam) in worst.OrderBy(static w => w.Key))
        {
            output.WriteLine($"level {level}: worst gap {seam.WorstGap} blocks: {seam.Worst}");
        }

        foreach (var ((level, finer), row) in table)
        {
            output.WriteLine($"stand-in level {level} beside leaves of level {finer}: {row.Open} of {row.Samples} blocks open, worst gap {row.Gap} blocks");
        }

        output.WriteLine($"{open} of {samples} blocks of cut edge are open: {openStandInHigher} where the stand-in stands higher than the leaf beside it, {openLeafHigher} where the leaf stands higher ({openLeafHigherNear} of them under a stand-in at most two levels coarser)");

        // The stand-in's own wall: it hangs below the neighbouring ground, so a finer leaf that stands lower leaves no slot (the five-block opening of the LOD review).
        Assert.True(openStandInHigher == 0, $"{openStandInHigher} of {samples} blocks of cut edge are open under the stand-in's wall; first: {string.Join(" | ", examplesStandIn.Take(4))}");

        // The finer leaf's own skirt closes a step up to its depth to a stand-in at most two levels coarser; further down the step can be deeper than the skirt (listed above, a known limit).
        Assert.True(openLeafHigherNear == 0, $"{openLeafHigherNear} blocks of cut edge are open under the finer leaf's skirt; first: {string.Join(" | ", examplesNear.Take(4))}");
    }
}
