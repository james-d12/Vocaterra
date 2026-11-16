using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>
/// The cave entrances (B11b): each is a passage a body walks down from the surface into a cave on steps of one block, wide enough to walk, never in or near water, with a rim of rock round its mouth and
/// no tree or plant on it, and each is the same whichever chunk or thread makes it. The sample is the entrances of the sites nearest the origin of three seeds, read from the generated blocks.
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
    public void Given_ThreeSeeds_When_TheSitesRoundTheOriginAreRead_Then_EachSeedHasPassagesAndTheyAreNotAllInOneDirection()
    {
        var all = ProceduralTestWorld.Seeds.SelectMany(Sample).ToList();

        Assert.All(ProceduralTestWorld.Seeds, seed => Assert.NotEmpty(Sample(seed)));
        Assert.True(all.Count >= 6, $"{all.Count} passages");
        Assert.True(all.Select(static r => (r.DirX, r.DirZ)).Distinct().Count() >= 2);
        Assert.All(all, r =>
        {
            Assert.Equal(Data.Entrances.Width, r.Width);
            Assert.True(r.Length - 0 >= Data.Entrances.MinDrop * Data.Entrances.Run, $"a passage of {r.Length} blocks drops {r.Length / r.Run}");
            Assert.True(r.MouthY - r.LowestFloor >= Data.Entrances.MinDrop);
            Assert.True(r.Length <= Data.Entrances.MaxLength);
        });
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_EachPassage_When_ABodyWalksFromItsMouthOnStepsOfOneBlock_Then_ItReachesACaveAtLeastTwentyBlocksBelowTheMouth(ulong seed)
    {
        var source = ProceduralTestWorld.Source(seed);
        var caves = source.World.Underground!.Caves;
        var checkedOne = 0;
        foreach (var ramp in Sample(seed).Take(4))
        {
            var blocks = new PassageBlocks(source, ramp);
            // The walk starts on the ground just outside the mouth, where a body comes from, and must pass the mouth.
            var (bx, bz) = Cell(ramp, -1, 0);
            var start = (X: bx, Y: blocks.Top(bx, bz) + 1, Z: bz);
            Assert.True(blocks.Standable(start.X, start.Y, start.Z), $"the ground before the mouth at {start} is not a place to stand");
            Assert.True(Math.Abs(start.Y - (ramp.MouthY + 1)) <= 1, $"the ground before the mouth is {start.Y - ramp.MouthY - 1} blocks off the mouth's floor");

            var found = blocks.Walk(start, cell => ramp.MouthY + 1 - cell.Y >= 20 && caves.KindAt(cell.X, cell.Y, cell.Z) != CaveKind.None && blocks.Block(cell.X, cell.Y, cell.Z) == BlockId.Air, out var reached, out var longestStep);

            Assert.True(found, $"seed {seed}: no walk from the mouth {start} reaches a cave 20 blocks down");
            Assert.True(ramp.MouthY + 1 - reached.Y >= 20);
            Assert.True(longestStep <= 1, $"a step of {longestStep} blocks");
            checkedOne++;
        }

        Assert.True(checkedOne >= 1);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_EachPassage_When_ItsCrossSectionsAreRead_Then_EveryOneIsAsWideAsTheDataAndHasAFloorAndHeadroom(ulong seed)
    {
        var source = ProceduralTestWorld.Source(seed);
        foreach (var ramp in Sample(seed).Take(4))
        {
            var blocks = new PassageBlocks(source, ramp);
            for (var along = 0; along <= ramp.Last; along++)
            {
                var standable = 0;
                for (var lateral = ramp.LateralMin; lateral <= ramp.LateralMax; lateral++)
                {
                    var (x, z) = Cell(ramp, along, lateral);
                    standable += blocks.Standable(x, ramp.FloorAt(along) + 1, z) ? 1 : 0;
                    for (var h = 1; h <= ramp.Headroom; h++)
                    {
                        Assert.False(BlockDefinition.IsSolid(blocks.Block(x, ramp.FloorAt(along) + h, z)), $"a solid block {h} over the floor at {x},{z}");
                    }
                }

                Assert.True(standable >= Math.Max(2, ramp.Width), $"seed {seed}: {standable} places to stand across the passage at {along} (anchor {ramp.AnchorX},{ramp.AnchorZ})");
            }
        }
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
            for (var along = 0; along <= ramp.Last; along += 2)
            {
                for (var lateral = ramp.LateralMin; lateral <= ramp.LateralMax; lateral++)
                {
                    var (x, z) = Cell(ramp, along, lateral);
                    var floor = ramp.FloorAt(along);
                    for (var y = floor + 1; y <= floor + ramp.Headroom; y += 2)
                    {
                        for (var dz = -margin; dz <= margin; dz++)
                        {
                            for (var dx = -margin; dx <= margin; dx++)
                            {
                                for (var dy = -margin; dy <= margin; dy++)
                                {
                                    Assert.NotEqual(BlockId.Water, blocks.Block(x + dx, y + dy, z + dz));
                                }
                            }
                        }
                    }

                    for (var y = floor + 1; y <= floor + Data.Caves.BedClearance; y++)
                    {
                        Assert.NotEqual(BlockId.Water, blocks.Block(x, y, z));
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_EachMouth_When_TheRimIsRead_Then_ItsGroundIsRockAndNoTreeOrPlantStandsOnIt(ulong seed)
    {
        var source = ProceduralTestWorld.Source(seed);
        var rim = new HashSet<BlockId>(Data.Entrances.RimBlocks.Select(static b => BlockDefinition.FromName(b.Block)!.Id));
        foreach (var ramp in Sample(seed).Take(4))
        {
            var blocks = new PassageBlocks(source, ramp);
            var rock = 0;
            var total = 0;
            for (var along = -ramp.RimWidth; along <= ramp.RimLength + ramp.RimWidth; along++)
            {
                for (var lateral = ramp.LateralMin - ramp.RimWidth; lateral <= ramp.LateralMax + ramp.RimWidth; lateral++)
                {
                    var (x, z) = Cell(ramp, along, lateral);
                    if (ramp.InPassage(along, lateral) && ramp.FloorAt(along) < ramp.MouthY)
                    {
                        continue; // the passage's own cells hold its air
                    }

                    var top = blocks.Top(x, z);
                    total++;
                    rock += rim.Contains(blocks.Block(x, top, z)) ? 1 : 0;
                    Assert.False(blocks.Block(x, top + 1, z) is BlockId.Trunk or BlockId.Leaves or BlockId.SpruceTrunk or BlockId.BirchTrunk or BlockId.SpruceLeaves or BlockId.BirchLeaves or BlockId.TallGrass or BlockId.Fern or BlockId.YellowFlower or BlockId.WhiteFlower, $"a plant over the rim at {x},{z}");
                }
            }

            Assert.True(rock * 100 >= total * 90, $"seed {seed}: {rock} of {total} rim columns have a rock top");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ASiteAndAFreshSource_When_ItsPassageIsResolvedAgainAndOnManyThreads_Then_ItIsTheSame(ulong seed)
    {
        var first = Sample(seed).Take(3).ToList();
        var fresh = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, Data).World.Entrances!;

        foreach (var ramp in first)
        {
            var (sx, sz) = (Vocaterra.Game.Core.Mathematics.IntMath.DivDown(ramp.AnchorX, Data.Entrances.Cell), Vocaterra.Game.Core.Mathematics.IntMath.DivDown(ramp.AnchorZ, Data.Entrances.Cell));
            var again = fresh.Site(sx, sz)!;
            Assert.Equal((ramp.AnchorX, ramp.AnchorZ, ramp.DirX, ramp.DirZ, ramp.MouthY, ramp.Length), (again.AnchorX, again.AnchorZ, again.DirX, again.DirZ, again.MouthY, again.Length));
        }

        var threaded = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, Data).World.Entrances!;
        var results = first.AsParallel().WithDegreeOfParallelism(8).Select(r => threaded.Site(Vocaterra.Game.Core.Mathematics.IntMath.DivDown(r.AnchorX, Data.Entrances.Cell), Vocaterra.Game.Core.Mathematics.IntMath.DivDown(r.AnchorZ, Data.Entrances.Cell))!.Length).ToList();
        Assert.Equal(first.Select(static r => r.Length), results);
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

    private static (int X, int Z) Cell(EntranceRamp ramp, int along, int lateral) =>
        (ramp.AnchorX + (ramp.DirX * along) - (ramp.DirZ * lateral), ramp.AnchorZ + (ramp.DirZ * along) + (ramp.DirX * lateral));

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
            _minLayer = (ramp.LowestFloor - 24) >> 4;
            _maxLayer = (ramp.MouthY + 40) >> 4;
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
        public bool Walk((int X, int Y, int Z) start, Func<(int X, int Y, int Z), bool> goal, out (int X, int Y, int Z) reached, out int longestStep)
        {
            var seen = new HashSet<(int, int, int)> { start };
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
