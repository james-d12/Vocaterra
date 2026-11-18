using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>
/// The cave entrances (B11b): each is a winding natural passage a body walks down from the surface into a cave on steps of one block, 2 blocks across at the narrowest and 3 high, its width, height and slope
/// changing along it, never in or near water, with no tree or plant at its mouth, and each is the same whichever chunk or thread makes it. The sample is the entrances of the sites nearest the origin of three seeds, read from the generated blocks.
/// </summary>
public sealed class EntrancePassageTests
{
    private const int SitesEachWay = 4;

    private static UndergroundData Data => TestGame.Data.Underground!;

    public static TheoryData<ulong> Seeds => new() { 42UL, 7UL, 2026UL };

    /// <summary>The passages of the sites round the origin, nearest first.</summary>
    internal static IReadOnlyList<EntranceRamp> Sample(ulong seed)
    {
        var sites = ProceduralTestWorld.Source(seed).World.Entrances!;
        var found = new List<EntranceRamp>();
        for (var sz = -SitesEachWay; sz < SitesEachWay; sz++)
        {
            for (var sx = -SitesEachWay; sx < SitesEachWay; sx++)
            {
                if (sites.Site(sx, sz) is { } ramp)
                {
                    found.Add(ramp);
                }
            }
        }

        return [.. found.OrderBy(static r => Math.Abs(r.AnchorX) + Math.Abs(r.AnchorZ))];
    }

    [Fact]
    public void Given_ThreeSeeds_When_TheSitesRoundTheOriginAreRead_Then_EachSeedHasPassagesThatWindAndChangeSlope()
    {
        var all = ProceduralTestWorld.Seeds.SelectMany(Sample).ToList();

        Assert.All(ProceduralTestWorld.Seeds, seed => Assert.NotEmpty(Sample(seed)));
        Assert.True(all.Count >= 6, $"{all.Count} passages");
        Assert.All(all, r =>
        {
            Assert.True(r.MouthY - r.FloorAt(r.Length) >= Data.Entrances.MinDrop, $"a passage that drops {r.MouthY - r.FloorAt(r.Length)}");
            Assert.True(r.Length < Data.Entrances.MaxLength);
            for (var i = 1; i <= r.Last; i++)
            {
                var (a, b) = (r.Nodes[i - 1], r.Nodes[i]);
                Assert.Equal(1, Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z)); // 4-connected
                Assert.InRange(a.Floor - b.Floor, 0, 1); // never a step of more than a block, never up
            }
        });

        // It winds: most paths leave the straight line from the mouth to their end by two blocks or more.
        var winding = all.Count(r =>
        {
            var (ex, ez) = r.CentreAt(r.Length);
            var (dx, dz) = (ex - r.AnchorX, ez - r.AnchorZ);
            var norm = Math.Max(1.0, Math.Sqrt((dx * dx) + (dz * dz)));
            return r.Nodes.Max(n => Math.Abs(((n.X - r.AnchorX) * dz) - ((n.Z - r.AnchorZ) * dx)) / norm) >= 2.0;
        });
        Assert.True(winding * 100 >= all.Count * 70, $"{winding} of {all.Count} passages bend");

        // The slope varies: stretches of 1, 2, 3 or more cells to a block of drop, and level bits, across the sample.
        var runs = new HashSet<int>();
        var levels = 0;
        foreach (var r in all)
        {
            var run = 1;
            for (var i = 1; i <= r.Length; i++)
            {
                if (r.Nodes[i].Floor == r.Nodes[i - 1].Floor)
                {
                    run++;
                }
                else
                {
                    runs.Add(Math.Min(run, 6));
                    levels += run >= 4 ? 1 : 0;
                    run = 1;
                }
            }
        }

        Assert.True(runs.Count >= 3, $"{runs.Count} different runs between steps");
        Assert.True(levels >= 3, $"{levels} level stretches");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_EachPassage_When_ABodyWalksFromItsMouthOnStepsOfOneBlock_Then_ItReachesACaveAtLeastTwentyBlocksBelowTheMouth(ulong seed)
    {
        var source = ProceduralTestWorld.Source(seed);
        var caves = source.World.Underground!.Caves;
        foreach (var ramp in Sample(seed).Take(4))
        {
            var blocks = new PassageBlocks(source, ramp);

            // The walk starts on the ground just outside the mouth, where a body comes from, and must pass the mouth.
            var start = (X: ramp.ApproachX, Y: blocks.Top(ramp.ApproachX, ramp.ApproachZ) + 1, Z: ramp.ApproachZ);
            Assert.True(blocks.Standable(start.X, start.Y, start.Z), $"the ground before the mouth at {start} is not a place to stand");
            Assert.True(Math.Abs(start.Y - (ramp.MouthY + 1)) <= 1, $"the ground before the mouth is {start.Y - ramp.MouthY - 1} blocks off the mouth's floor");

            var found = blocks.Walk(start, cell => ramp.MouthY + 1 - cell.Y >= 20 && caves.KindAt(cell.X, cell.Y, cell.Z) != CaveKind.None && blocks.Block(cell.X, cell.Y, cell.Z) == BlockId.Air, out var reached, out var longestStep, out var visited);

            Assert.True(found, $"seed {seed}: no walk from the mouth {start} reaches a cave 20 blocks down; {Stuck(ramp, blocks, visited)}");
            Assert.True(ramp.MouthY + 1 - reached.Y >= 20);
            Assert.True(longestStep <= 1, $"a step of {longestStep} blocks");
        }
    }

    private static int GroundAt(ulong seed, int x, int z)
    {
        var world = ProceduralTestWorld.Source(seed).World;
        return world.Height.Shape(x, z, world.Climate.At(x, z)).Height;
    }

    // B11d: the ground climbs four blocks or more within the passage's first six cells.
    private static bool GroundRisesAhead(ulong seed, EntranceRamp r) => GroundAt(seed, r.Nodes[6].X, r.Nodes[6].Z) - r.MouthY >= 4;

    // B11d: a mouth opens sideways into a slope when the ground rises ahead of it and the passage runs in without a plunge (its floor falls at most a block over the first three cells), from an approach outside the slope.
    private static bool OpensIntoSlope(ulong seed, EntranceRamp r) =>
        GroundRisesAhead(seed, r) && r.MouthY - r.Nodes[3].Floor <= 1 && GroundAt(seed, r.ApproachX, r.ApproachZ) <= r.MouthY + 1;

    [Fact]
    public void Given_ThePassagesOfThreeSeeds_When_TheGroundAroundTheirMouthsIsRead_Then_AtLeastAQuarterOpenIntoASlope()
    {
        int slopes = 0, all = 0, plunges = 0;
        foreach (var seed in ProceduralTestWorld.Seeds)
        {
            var sample = Sample(seed);
            all += sample.Count;
            slopes += sample.Count(r => OpensIntoSlope(seed, r));
            plunges += sample.Count(r => GroundRisesAhead(seed, r) && !OpensIntoSlope(seed, r));
        }

        Assert.True(slopes * 100 >= all * 25, $"{slopes} of {all} passages open into a slope ({plunges} more plunge on rising ground)");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheMouthsInASlope_When_ABodyWalksInFromTheGroundBesideThem_Then_TheyFallGentlyAndReachACaveTwentyBlocksDown(ulong seed)
    {
        var source = ProceduralTestWorld.Source(seed);
        var caves = source.World.Underground!.Caves;
        var slopes = Sample(seed).Where(r => OpensIntoSlope(seed, r)).Take(4).ToList();
        Assert.NotEmpty(slopes);
        foreach (var ramp in slopes)
        {
            // The approach is outside the slope, at the mouth's height or within a block of it; the first cells fall at most a block each (in fact at most one over the first three).
            Assert.InRange(GroundAt(seed, ramp.ApproachX, ramp.ApproachZ) - ramp.MouthY, -1, 1);
            for (var i = 1; i <= 6; i++)
            {
                Assert.InRange(ramp.Nodes[i - 1].Floor - ramp.Nodes[i].Floor, 0, 1);
            }

            var blocks = new PassageBlocks(source, ramp);
            var start = (X: ramp.ApproachX, Y: blocks.Top(ramp.ApproachX, ramp.ApproachZ) + 1, Z: ramp.ApproachZ);
            Assert.True(blocks.Standable(start.X, start.Y, start.Z), $"the ground before the face mouth at {start} is not a place to stand");
            var found = blocks.Walk(start, cell => ramp.MouthY + 1 - cell.Y >= 20 && caves.KindAt(cell.X, cell.Y, cell.Z) != CaveKind.None && blocks.Block(cell.X, cell.Y, cell.Z) == BlockId.Air, out _, out var longestStep, out var visited);
            Assert.True(found, $"seed {seed}: no walk from the face mouth {start} reaches a cave; {Stuck(ramp, blocks, visited)}");
            Assert.True(longestStep <= 1);
        }
    }

    [Fact]
    public void Given_ThePassagesOfThreeSeeds_When_TheirCrossSectionsAreRead_Then_EveryOneIsWalkableAndTheWidthAndHeightKeepChanging()
    {
        var widths = new HashSet<int>();
        var heights = new HashSet<int>();
        foreach (var seed in ProceduralTestWorld.Seeds)
        {
            var source = ProceduralTestWorld.Source(seed);
            foreach (var ramp in Sample(seed).Take(3))
            {
                var blocks = new PassageBlocks(source, ramp);
                for (var i = 0; i <= ramp.Last; i++)
                {
                    var node = ramp.Nodes[i];
                    var (a, b) = (ramp.Nodes[Math.Max(0, i - 1)], ramp.Nodes[Math.Min(ramp.Last, i + 1)]);
                    var alongX = Math.Abs(b.X - a.X) >= Math.Abs(b.Z - a.Z);

                    // The row of cells across the path at this cell: how many in a run from the centre have a place to stand on a floor within a block of the cell's.
                    int Open(int dir)
                    {
                        var n = 0;
                        for (var l = 1; l <= 8; l++)
                        {
                            var (x, z) = alongX ? (node.X, node.Z + (dir * l)) : (node.X + (dir * l), node.Z);
                            if (!Enumerable.Range(node.Floor, 3).Any(floor => blocks.Standable(x, floor + 1, z)))
                            {
                                break;
                            }

                            n++;
                        }

                        return n;
                    }

                    var width = 1 + Open(1) + Open(-1);
                    var height = 0;
                    while (height < 14 && blocks.Passable(node.X, node.Floor + 1 + height, node.Z))
                    {
                        height++;
                    }

                    Assert.True(blocks.Standable(node.X, node.Floor + 1, node.Z), $"no place to stand at path cell {i} of the passage at {ramp.AnchorX},{ramp.AnchorZ}");
                    Assert.True(width >= 2, $"seed {seed}: {width} blocks across at cell {i} of the passage at {ramp.AnchorX},{ramp.AnchorZ}");
                    Assert.True(height >= 3, $"seed {seed}: {height} blocks of headroom at cell {i}");
                    widths.Add(Math.Min(width, 7));
                    heights.Add(Math.Min(height, 8));
                }
            }
        }

        Assert.True(widths.Count >= 3, $"widths {string.Join(",", widths.Order())}");
        Assert.True(heights.Count >= 3, $"heights {string.Join(",", heights.Order())}");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_EachPassage_When_WaterIsSearchedForRoundIt_Then_NoWaterLiesWithinTheCavesMarginOfAnyOfItsAirNorOverItWithinTheBedClearance(ulong seed)
    {
        var source = ProceduralTestWorld.Source(seed);
        var margin = Data.Caves.WaterMargin;
        foreach (var ramp in Sample(seed).Take(4))
        {
            var blocks = new PassageBlocks(source, ramp);
            for (var i = 0; i <= ramp.Last; i += 2)
            {
                var node = ramp.Nodes[i];
                for (var y = node.Floor + 1; y <= node.Floor + node.Headroom; y += 2)
                {
                    for (var dz = -margin; dz <= margin; dz++)
                    {
                        for (var dx = -margin; dx <= margin; dx++)
                        {
                            for (var dy = -margin; dy <= margin; dy++)
                            {
                                Assert.NotEqual(BlockId.Water, blocks.Block(node.X + dx, y + dy, node.Z + dz));
                            }
                        }
                    }
                }

                for (var y = node.Floor + 1; y <= node.Floor + Data.Caves.BedClearance; y++)
                {
                    Assert.NotEqual(BlockId.Water, blocks.Block(node.X, y, node.Z));
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_EachMouth_When_ItsSurroundingsAreRead_Then_NoTreeOrPlantStandsOnIt(ulong seed)
    {
        var source = ProceduralTestWorld.Source(seed);
        foreach (var ramp in Sample(seed).Take(4))
        {
            var blocks = new PassageBlocks(source, ramp);
            var (mx, mz) = (ramp.AnchorX, ramp.AnchorZ);
            for (var z = mz - 8; z <= mz + 8; z++)
            {
                for (var x = mx - 8; x <= mx + 8; x++)
                {
                    if (!ramp.Clears(x, z))
                    {
                        continue;
                    }

                    var top = blocks.Top(x, z);
                    Assert.False(blocks.Block(x, top + 1, z) is BlockId.Trunk or BlockId.SpruceTrunk or BlockId.BirchTrunk or BlockId.TallGrass or BlockId.Fern or BlockId.YellowFlower or BlockId.WhiteFlower, $"a trunk or plant at the mouth at {x},{z}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASiteAndAFreshSource_When_ItsPassageIsResolvedAgainAndOnManyThreads_Then_ItIsTheSame(ulong seed)
    {
        var first = Sample(seed).Take(3).ToList();
        var fresh = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, Data).World.Entrances!;

        static (int, int) SiteOf(EntranceRamp r) => (Vocaterra.Game.Core.Mathematics.IntMath.DivDown(r.AnchorX, Data.Entrances.Cell), Vocaterra.Game.Core.Mathematics.IntMath.DivDown(r.AnchorZ, Data.Entrances.Cell));
        foreach (var ramp in first)
        {
            var (sx, sz) = SiteOf(ramp);
            var again = fresh.Site(sx, sz)!;
            Assert.Equal(ramp.Nodes, again.Nodes);
            Assert.Equal(ramp.Length, again.Length);
        }

        var threaded = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, Data).World.Entrances!;
        var results = first.AsParallel().WithDegreeOfParallelism(8).Select(r => { var (sx, sz) = SiteOf(r); return threaded.Site(sx, sz)!.Nodes.Length; }).ToList();
        Assert.Equal(first.Select(static r => r.Nodes.Length), results);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheMouthsOfAChunkColumn_When_TheyAreListed_Then_TheyAreThePassagesAnchorsInIt(ulong seed)
    {
        var source = ProceduralTestWorld.Source(seed);
        var ramp = Sample(seed)[0];

        var listed = source.EntranceColumns(ramp.AnchorX >> 4, ramp.AnchorZ >> 4);

        Assert.Contains((ramp.AnchorX, ramp.AnchorZ), listed);
        Assert.All(listed, c => Assert.Equal((ramp.AnchorX >> 4, ramp.AnchorZ >> 4), (c.X >> 4, c.Z >> 4)));
    }

    // Where a walk that did not arrive stopped: the last path cell it stood on and what the next one holds.
    private static string Stuck(EntranceRamp ramp, PassageBlocks blocks, HashSet<(int, int, int)> visited)
    {
        var deepest = -1;
        for (var i = 0; i <= ramp.Last; i++)
        {
            if (visited.Contains((ramp.Nodes[i].X, ramp.Nodes[i].Floor + 1, ramp.Nodes[i].Z)))
            {
                deepest = i;
            }
        }

        if (deepest < 0 || deepest >= ramp.Last)
        {
            return $"the walk reached path cell {deepest} of {ramp.Last} (length {ramp.Length})";
        }

        var (a, b) = (ramp.Nodes[deepest], ramp.Nodes[deepest + 1]);
        string Column(PathNode n) => string.Join(" ", Enumerable.Range(n.Floor - 1, 6).Select(y => $"{y}:{blocks.Block(n.X, y, n.Z)}"));
        return $"reached path cell {deepest} of {ramp.Last} (length {ramp.Length}) at {a.X},{a.Floor},{a.Z} [{Column(a)}]; the next {b.X},{b.Floor},{b.Z} [{Column(b)}]";
    }

    /// <summary>The generated blocks round a passage, read from the chunk columns it crosses (every layer that holds it), so the checks see the world as a player would.</summary>
    internal sealed class PassageBlocks
    {
        private readonly Dictionary<(int X, int Z), IReadOnlyList<ChunkData>> _columns = [];
        private readonly ProceduralChunkSource _source;
        private readonly int _minLayer;
        private readonly int _maxLayer;

        public PassageBlocks(ProceduralChunkSource source, EntranceRamp ramp)
        {
            _source = source;
            _minLayer = Math.Max((ramp.LowestFloor - 24) >> 4, source.MinChunkY);
            _maxLayer = Math.Min((ramp.MouthY + 40) >> 4, source.MaxChunkY - 1);
            var b = ramp.Bounds(8);
            for (var cz = b.MinZ >> 4; cz <= b.MaxZ >> 4; cz++)
            {
                for (var cx = b.MinX >> 4; cx <= b.MaxX >> 4; cx++)
                {
                    _columns[(cx, cz)] = source.GenerateLayers(cx, cz, _minLayer, _maxLayer);
                }
            }
        }

        public BlockId Block(int x, int y, int z)
        {
            if (!_columns.TryGetValue((x >> 4, z >> 4), out var column) || (y >> 4) < _minLayer || (y >> 4) > _maxLayer)
            {
                return BlockId.Stone; // outside what was read: solid, so a walk never leaves the sample
            }

            var chunk = column[(y >> 4) - _minLayer];
            return chunk.IsEmpty ? BlockId.Air : chunk.Blocks[ChunkData.Index(x & 15, y & 15, z & 15)];
        }

        /// <summary>The y of the highest solid block of a column (a tree's leaves and plants not counted).</summary>
        public int Top(int x, int z)
        {
            for (var y = ((_maxLayer + 1) << 4) - 1; y > _minLayer << 4; y--)
            {
                var block = Block(x, y, z);
                if (BlockDefinition.IsSolid(block) && block is not (BlockId.Leaves or BlockId.SpruceLeaves or BlockId.BirchLeaves or BlockId.Trunk or BlockId.SpruceTrunk or BlockId.BirchTrunk))
                {
                    return y;
                }
            }

            return _minLayer << 4;
        }

        public bool Passable(int x, int y, int z)
        {
            var block = Block(x, y, z);
            return !BlockDefinition.IsSolid(block) && block is not (BlockId.Water or BlockId.Lava);
        }

        /// <summary>A place to stand: the cell and the one above are open and the one below is solid and not lava.</summary>
        public bool Standable(int x, int y, int z) => Passable(x, y, z) && Passable(x, y + 1, z) && BlockDefinition.IsSolid(Block(x, y - 1, z));

        /// <summary>
        /// A flood of standable places from <paramref name="start"/> by steps to a side of at most one block up or down (the cells beside and above the step open), until one passes <paramref name="goal"/>.
        /// </summary>
        public bool Walk((int X, int Y, int Z) start, Func<(int X, int Y, int Z), bool> goal, out (int X, int Y, int Z) reached, out int longestStep, out HashSet<(int, int, int)> visited)
        {
            var seen = new HashSet<(int, int, int)> { start };
            visited = seen;
            var queue = new Queue<(int X, int Y, int Z)>();
            queue.Enqueue(start);
            reached = start;
            longestStep = 0;
            (int, int)[] sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];
            while (queue.Count > 0 && seen.Count < 400_000)
            {
                var here = queue.Dequeue();
                if (goal(here))
                {
                    reached = here;
                    return true;
                }

                foreach (var (dx, dz) in sides)
                {
                    for (var dy = 1; dy >= -1; dy--)
                    {
                        var next = (X: here.X + dx, Y: here.Y + dy, Z: here.Z + dz);
                        if (seen.Contains(next) || !Standable(next.X, next.Y, next.Z) || !StepClear(here, next))
                        {
                            continue;
                        }

                        seen.Add(next);
                        longestStep = Math.Max(longestStep, Math.Abs(dy));
                        queue.Enqueue(next);
                    }
                }
            }

            return false;
        }

        // The cells a body passes through between two neighbouring places to stand: the head of the lower and the space of the higher.
        private bool StepClear((int X, int Y, int Z) from, (int X, int Y, int Z) to)
        {
            var top = Math.Max(from.Y, to.Y) + 1;
            return Passable(from.X, top, from.Z) && Passable(to.X, Math.Min(from.Y, to.Y) + 1, to.Z) && Passable(to.X, top, to.Z);
        }
    }
}
