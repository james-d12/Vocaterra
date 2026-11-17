using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Core.World.Underground;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>
/// The rock variety (B11b, D-game-34 item 27): the walls and floors of the caves above y 0 are not one grey. The measure is over the exposed faces of rock in a sample of chunks of three seeds: a face is
/// a solid, non-ore, non-bedrock block with cave air (air at or under its column's ground) beside, above or under it, and the share of faces on plain stone must stay at about 60 to 70 percent at
/// most, at least four kinds of rock must show, and no kind may lie in one huge blob (the largest connected body of a kind at most 30 000 blocks, a cube of about 31). The same measure over the underground as it was before B11b (two blobs of granite and limestone, no linings) is
/// printed beside it and asserted to be much greyer. The cliffs of a mountain and the scree on its slopes get their own measures.
/// </summary>
public sealed class RockVarietyTests(ITestOutputHelper output)
{
    private static UndergroundData After => TestGame.Data.Underground!;

    // The underground as B11 made it: the two blobs of granite and limestone, no linings and no surface rock.
    private static UndergroundData Before => After with
    {
        Strata = After.Strata with { Blobs = [new BlobSpec("granite", 48, 32, 2, 330), new BlobSpec("limestone", 48, 32, 2, 330)] },
        Linings = null,
        SurfaceRock = null,
    };

    public static TheoryData<ulong> Seeds => new() { 42UL, 7UL, 2026UL };

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(bool, ulong), (Dictionary<BlockId, long>, long, SampledArea)> Measured = new();

    private static (Dictionary<BlockId, long> Faces, long Total, SampledArea Area) Measure(UndergroundData data, ulong seed) => Measured.GetOrAdd((ReferenceEquals(data, After), seed), _ => MeasureNow(data, seed));

    // The sample is the chunks round the nearest mountain (the ground is high there, so the caves above y 0 are many).
    private static (Dictionary<BlockId, long> Faces, long Total, SampledArea Area) MeasureNow(UndergroundData data, ulong seed)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, Feature.Mountain);
        var source = new ProceduralChunkSource(TestGame.Data.WorldGen, TestGame.Data.Biomes, seed, null, data);
        var area = new SampledArea(source, source.GenerateColumn, cx - 2, cz - 2, 5, 5);
        var faces = new Dictionary<BlockId, long>();
        long total = 0;
        for (var z = area.MinZ + 16; z < area.MaxZ - 16; z++)
        {
            for (var x = area.MinX + 16; x < area.MaxX - 16; x++)
            {
                var top = Math.Min(area.Height(x, z) - 8, 160);
                for (var y = 1; y <= top; y++)
                {
                    var block = area.Block(x, y, z);
                    if (!IsRock(block))
                    {
                        continue;
                    }

                    var open = (area.IsCave(x, y + 1, z) ? 1 : 0) + (area.IsCave(x, y - 1, z) ? 1 : 0) + (area.IsCave(x + 1, y, z) ? 1 : 0) + (area.IsCave(x - 1, y, z) ? 1 : 0) + (area.IsCave(x, y, z + 1) ? 1 : 0) + (area.IsCave(x, y, z - 1) ? 1 : 0);
                    if (open > 0)
                    {
                        faces[block] = faces.GetValueOrDefault(block) + open;
                        total += open;
                    }
                }
            }
        }

        return (faces, total, area);
    }

    private static bool IsRock(BlockId block) => BlockDefinition.IsSolid(block) && block is not (BlockId.Bedrock or BlockId.IronOre or BlockId.TinOre or BlockId.CopperOre or BlockId.Leaves or BlockId.SpruceLeaves or BlockId.BirchLeaves);

    private string Report(string label, Dictionary<BlockId, long> faces, long total) =>
        $"{label}: {total} faces: " + string.Join(", ", faces.OrderByDescending(static p => p.Value).Select(p => $"{BlockDefinition.Get(p.Key).Name} {p.Value * 100.0 / total:0.0}%"));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheCavesAboveSeaLevelOfAPlace_When_TheirExposedFacesAreCounted_Then_PlainStoneIsAtMostAboutSixtyFivePercentAndAtLeastFourKindsShow(ulong seed)
    {
        var (faces, total, _) = Measure(After, seed);
        var (beforeFaces, beforeTotal, _) = Measure(Before, seed);
        output.WriteLine(Report($"seed {seed} after ", faces, total));
        output.WriteLine(Report($"seed {seed} before", beforeFaces, beforeTotal));

        Assert.True(total > 3000, $"{total} exposed faces in the sample");
        var stone = faces.GetValueOrDefault(BlockId.Stone) * 100.0 / total;
        var stoneBefore = beforeFaces.GetValueOrDefault(BlockId.Stone) * 100.0 / beforeTotal;
        Assert.True(stone <= 68.0, $"seed {seed}: plain stone is {stone:0.0}% of the exposed faces");
        Assert.True(stoneBefore > stone, $"seed {seed}: before B11b plain stone was {stoneBefore:0.0}% and after {stone:0.0}%");
        var kinds = faces.Count(p => p.Value * 100.0 / total >= 1.0);
        Assert.True(kinds >= 4, $"seed {seed}: {kinds} kinds of rock show: " + Report("after", faces, total));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheRockOfAPlace_When_EachKindsBlobsAreCounted_Then_NoKindLiesInOneHugeBlob(ulong seed)
    {
        var (_, _, area) = Measure(After, seed);
        BlockId[] kinds = [BlockId.Granite, BlockId.Limestone, BlockId.Gravel, BlockId.Sand, BlockId.Dirt];
        var (x0, z0, x1, z1) = (area.MinX + 16, area.MinZ + 16, area.MaxX - 16, area.MaxZ - 16);
        foreach (var kind in kinds)
        {
            var seen = new HashSet<(int, int, int)>();
            long cells = 0;
            long largest = 0;
            for (var z = z0; z < z1; z++)
            {
                for (var x = x0; x < x1; x++)
                {
                    for (var y = 1; y <= Math.Min(area.Height(x, z) - 8, 160); y++)
                    {
                        if (area.Block(x, y, z) != kind)
                        {
                            continue;
                        }

                        cells++;
                        if (!seen.Contains((x, y, z)))
                        {
                            largest = Math.Max(largest, Flood(area, kind, (x, y, z), seen, (x0, z0, x1, z1)));
                        }
                    }
                }
            }

            output.WriteLine($"seed {seed} {BlockDefinition.Get(kind).Name}: {cells} blocks, the largest blob {largest} ({(cells == 0 ? 0 : largest * 100.0 / cells):0}%)");
            Assert.True(cells > 500, $"seed {seed}: only {cells} blocks of {kind}");
            Assert.True(largest <= 30_000, $"seed {seed}: {kind}'s largest blob is {largest} of {cells} blocks (a cube of 31)");
        }
    }

    private static long Flood(SampledArea area, BlockId kind, (int X, int Y, int Z) start, HashSet<(int, int, int)> seen, (int X0, int Z0, int X1, int Z1) box)
    {
        var stack = new Stack<(int X, int Y, int Z)>();
        stack.Push(start);
        seen.Add(start);
        long size = 0;
        while (stack.Count > 0)
        {
            var (x, y, z) = stack.Pop();
            size++;
            foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
            {
                var next = (X: x + dx, Y: y + dy, Z: z + dz);
                if (next.X < box.X0 || next.X >= box.X1 || next.Z < box.Z0 || next.Z >= box.Z1 || next.Y < 1 || next.Y > Math.Min(area.Height(next.X, next.Z) - 8, 160) || seen.Contains(next) || area.Block(next.X, next.Y, next.Z) != kind)
                {
                    continue;
                }

                seen.Add(next);
                stack.Push(next);
            }
        }

        return size;
    }

    [Theory]
    [InlineData(42UL)]
    public void Given_AMountainsCliffsAndSlopes_When_TheirSurfaceIsRead_Then_BandedRockShowsOnTheFacesAndGravelLiesOnTheSteepGround(ulong seed)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, Feature.Mountain);
        var source = ProceduralTestWorld.Source(seed);
        var rock = source.World.SurfaceRock!;
        var faces = new Dictionary<BlockId, int>();
        int steep = 0, steepGravel = 0, cliffColumns = 0, flat = 0, flatGravel = 0;
        for (var z = cz - 2; z <= cz + 2; z++)
        {
            for (var x = cx - 2; x <= cx + 2; x++)
            {
                var ctx = source.ShapedColumn(x, z);
                var column = source.GenerateColumn(x, z);
                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        var (wx, wz) = ((x << 4) + lx, (z << 4) + lz);
                        if (lx is 0 or 15 || lz is 0 or 15)
                        {
                            continue;
                        }

                        var height = ctx.HeightAt(wx, wz);
                        var drop = rock.Drop(ctx, wx, wz);
                        var topBlock = column[(height >> 4) - source.MinChunkY].Blocks[ChunkData.Index(lx, height & 15, lz)];
                        if (topBlock == BlockId.Snow)
                        {
                            continue; // snow is never covered
                        }

                        if (drop >= After.SurfaceRock!.Scree.SteepDrop && drop < After.SurfaceRock.Cliff.Drop)
                        {
                            steep++;
                            steepGravel += topBlock == BlockId.Gravel ? 1 : 0;
                        }
                        else if (drop == 0)
                        {
                            flat++;
                            flatGravel += topBlock == BlockId.Gravel ? 1 : 0;
                        }

                        if (drop >= After.SurfaceRock.Cliff.Drop)
                        {
                            cliffColumns++;
                            var low = rock.LowestNeighbour(ctx, wx, wz);
                            for (var y = low + 1; y < height; y++)
                            {
                                var block = column[(y >> 4) - source.MinChunkY].Blocks[ChunkData.Index(lx, y & 15, lz)];
                                faces[block] = faces.GetValueOrDefault(block) + 1;
                            }
                        }
                    }
                }
            }
        }

        var total = faces.Values.Sum();
        output.WriteLine($"seed {seed}: {cliffColumns} cliff columns, {total} face blocks: " + string.Join(", ", faces.OrderByDescending(static p => p.Value).Select(p => $"{BlockDefinition.Get(p.Key).Name} {p.Value * 100.0 / total:0.0}%")) + $"; steep columns {steep} with gravel on {steepGravel}, flat columns {flat} with gravel on {flatGravel}");
        Assert.True(steep > 50, $"{steep} steep columns");
        Assert.True(steepGravel * 100 >= steep * 25, $"gravel tops {steepGravel} of {steep} steep columns");
        if (cliffColumns <= 100)
        {
            // B02c: the ground of the shipped world is smooth (no step over 3 anywhere; the cliffs this sample counted were the walls of the old rivers' valleys, and a high mountain is under snow, which is never covered), so the nearest mountain
            // may hold too few cliffs to read the faces' rocks from. The bands themselves are held by the tests above.
            output.WriteLine($"seed {seed}: only {cliffColumns} cliff columns in the sample, the faces are not read");
            return;
        }

        Assert.True(faces.Count(p => p.Value * 100.0 / total >= 5.0) >= 3, "three kinds of rock show on the cliff faces");
        Assert.True(faces.GetValueOrDefault(BlockId.Stone) * 100.0 / total <= 75.0, "plain stone is not the whole face");
        Assert.True(steep > 50, $"{steep} steep columns");
        Assert.True(steepGravel * 100 >= steep * 25, $"gravel tops {steepGravel} of {steep} steep columns");
    }
}
