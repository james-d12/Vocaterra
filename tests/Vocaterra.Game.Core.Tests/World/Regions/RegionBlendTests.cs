using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Noise;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>
/// The blend of the procedural land into the valley's edge (B06): no step across the border and the margin beyond the bound the terrain tuning holds the land to, the ground free beyond the margin, the land
/// bias that keeps the sea away, the rivers and lakes fading out, the trees keeping off the border and thinning across the margin, and the exit corridor.
/// </summary>
public sealed class RegionBlendTests(ITestOutputHelper output)
{
    /// <summary>The largest step between two neighbouring columns the terrain is held to (B01's cliff bound, B03's border bound).</summary>
    private const int StepBound = 9;

    private static RegionBlendData Blend => TestGame.Data.WorldGen.Blend;

    private static int Height(WorldComposition world, int x, int z) => RegionTestWorld.Shape(world, x, z).Height;

    // The block columns of the ring at distance d from the footprint (d 0 is the edge column itself), along each of the four sides, every stride columns.
    private static IEnumerable<(int X, int Z, int NormalX, int NormalZ)> Ring(RegionBounds b, int d, int stride)
    {
        for (var t = 0; t <= b.MaxX - b.MinX; t += stride)
        {
            yield return (b.MinX + t, b.MinZ - d, 0, -1);
            yield return (b.MinX + t, b.MaxZ + d, 0, 1);
        }

        for (var t = 0; t <= b.MaxZ - b.MinZ; t += stride)
        {
            yield return (b.MinX - d, b.MinZ + t, -1, 0);
            yield return (b.MaxX + d, b.MinZ + t, 1, 0);
        }
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2026UL)]
    public void Given_TheValleysBorderAndMargin_When_TheGroundIsSampledAcrossAndAlongIt_Then_NoStepPassesTheBound(ulong seed)
    {
        var world = RegionTestWorld.Composition(seed);
        var b = RegionTestWorld.Valley(seed).Bounds;
        var margin = Blend.Margin;
        var maxStep = 0;
        var where = string.Empty;
        void Note(int step, string place)
        {
            if (step > maxStep)
            {
                (maxStep, where) = (step, place);
            }
        }

        for (var d = 0; d <= margin + 8; d++)
        {
            foreach (var (x, z, nx, nz) in Ring(b, d, 3))
            {
                var h = Height(world, x, z);
                Note(Math.Abs(h - Height(world, x + nx, z + nz)), $"across at {x},{z} d {d}");
                Note(Math.Abs(h - Height(world, x + Math.Abs(nz), z + Math.Abs(nx))), $"along at {x},{z} d {d}");
            }
        }

        // The four corners: a square of columns from the corner outwards, every neighbour pair.
        foreach (var (cx, cz, sx, sz) in new[] { (b.MinX, b.MinZ, -1, -1), (b.MaxX, b.MinZ, 1, -1), (b.MinX, b.MaxZ, -1, 1), (b.MaxX, b.MaxZ, 1, 1) })
        {
            var size = margin + 8;
            var h = new int[size + 1, size + 1];
            for (var i = 0; i <= size; i++)
            {
                for (var j = 0; j <= size; j++)
                {
                    h[i, j] = Height(world, cx + (sx * i), cz + (sz * j));
                }
            }

            for (var i = 0; i < size; i++)
            {
                for (var j = 0; j < size; j++)
                {
                    Note(Math.Abs(h[i, j] - h[i + 1, j]), $"corner {cx},{cz} at {i},{j}");
                    Note(Math.Abs(h[i, j] - h[i, j + 1]), $"corner {cx},{cz} at {i},{j}");
                }
            }
        }

        output.WriteLine($"seed {seed}: the largest step across the border, the margin and the corners is {maxStep} ({where})");
        Assert.True(maxStep <= StepBound, $"a step of {maxStep} at {where}");
    }

    [Fact]
    public void Given_TheValleysRoughEdge_When_TheGroundAlongTheBorderIsComparedAtGrowingDistances_Then_ItsRoughnessIsEasedAwaySoNoRidgesComb()
    {
        var world = RegionTestWorld.Composition();
        var region = RegionTestWorld.Valley();
        var b = region.Bounds;
        var ease = Blend.Smoothing.Ease;

        // The smoothed profile along the east edge is much calmer than the edge itself.
        double Roughness(Func<int, int> at) => Enumerable.Range(b.MinZ, b.MaxZ - b.MinZ).Sum(z => Math.Abs(at(z + 1) - at(z)));
        var raw = Roughness(z => region.EdgeHeight(b.MaxX, z));
        var smooth = Roughness(z => region.SmoothedEdgeHeight(b.MaxX, z));
        output.WriteLine($"along the east edge the heights change by {raw} in all and the smoothed ones by {smooth}");
        Assert.True(smooth * 2 < raw, $"{smooth} against {raw}");

        // On the ground: beside the border the roughness is the edge's own, a few blocks out it is gone. Outside the pass's bank, along the north and south sides.
        double Tangential(int d) => Enumerable.Range(b.MinX, b.MaxX - b.MinX).Sum(x => Math.Abs(Height(world, x + 1, b.MinZ - d) - Height(world, x, b.MinZ - d)) + Math.Abs(Height(world, x + 1, b.MaxZ + d) - Height(world, x, b.MaxZ + d)));
        var (near, eased, far) = (Tangential(1), Tangential(ease), Tangential(2 * ease));
        output.WriteLine($"the change along the north and south borders: {near} at d 1, {eased} at d {ease}, {far} at d {2 * ease}");
        Assert.True(eased * 2 < near, $"{eased} at d {ease} against {near} at d 1");
        Assert.True(far < near, $"{far} at d {2 * ease} against {near} at d 1");
    }

    [Fact]
    public void Given_TheFirstColumnOutside_When_ItsGroundIsRead_Then_ItIsTheEdgesHeightAndBeyondTheMarginTheModelIsFree()
    {
        var world = RegionTestWorld.Composition();
        var region = RegionTestWorld.Valley();
        var b = region.Bounds;
        var inner = new HeightModel(TestGame.Data.WorldGen, RegionTestWorld.Seed);
        var margin = Blend.Margin;

        var corridor = Blend.ExitCorridors[0];
        foreach (var (x, z, _, _) in Ring(b, 1, 24))
        {
            if (Math.Abs(z - corridor.FromZ) <= corridor.HalfWidth + corridor.Shoulder + 1 && x > b.MaxX)
            {
                continue; // the exit corridor's road and bank take the ground there
            }

            Assert.InRange(Height(world, x, z) - region.EdgeHeight(x, z), -1, 1); // the weight just under one: the edge's height to within a block
        }

        foreach (var (x, z, _, _) in Ring(b, margin, 24).Concat(Ring(b, margin + 20, 24)))
        {
            var c = world.Procedural.World.Climate.At(x, z);
            Assert.Equal(inner.Shape(x, z, c), RegionTestWorld.Shape(world, x, z)); // the free land: the height model's own, on the climate with the land bias
        }
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2026UL)]
    public void Given_TheLandBias_When_TheGroundWithinItsRadiusIsSampled_Then_NoSeaOpensAndTheContinentalnessKeepsItsFloor(ulong seed)
    {
        var world = RegionTestWorld.Composition(seed);
        var b = RegionTestWorld.Valley(seed).Bounds;
        var bias = Blend.LandBias;
        var plain = new ClimateSampler(TestGame.Data.WorldGen, seed);
        var plainModel = new HeightModel(TestGame.Data.WorldGen, seed);
        var (sea, unbiasedSea, columns) = (0, 0, 0);
        for (var z = b.MinZ - bias.Radius; z <= b.MaxZ + bias.Radius; z += 8)
        {
            for (var x = b.MinX - bias.Radius; x <= b.MaxX + bias.Radius; x += 8)
            {
                var d = b.DistanceTo(x, z);
                if (d == 0 || d > bias.Radius)
                {
                    continue; // the valley's own river and lake are the valley's; the sea is what the bias keeps away
                }

                columns++;
                var shape = RegionTestWorld.Shape(world, x, z);
                sea += shape.Water && shape.RiverWeight == 0 && shape.LakeWeight == 0 ? 1 : 0;
                var floor = FieldScale.FromThousandths(bias.Floor) - (int)((long)(FieldScale.FromThousandths(bias.Floor) - FieldScale.FromThousandths(bias.ShoreFloor)) * d / bias.Radius);
                Assert.True(world.Places.ClimateAt(x, z).Continentalness >= floor, $"continentalness below the floor at {x},{z}");
                var raw = plainModel.Shape(x, z, plain.At(x, z));
                unbasedSeaCount(raw);
            }
        }

        void unbasedSeaCount(ColumnShape raw) => unbiasedSea += raw.Water && raw.RiverWeight == 0 && raw.LakeWeight == 0 ? 1 : 0;

        output.WriteLine($"seed {seed}: {columns} columns within {bias.Radius} blocks, sea columns {sea} (without the bias {unbiasedSea})");
        Assert.Equal(0, sea);
    }

    [Fact]
    public void Given_ThreeSeeds_When_TheLandBiasIsLeftOut_Then_AtLeastOneWouldHaveSeaWithinTheRadius()
    {
        // The land-bias test is not vacuous: without the pull, some seed opens sea at the valley's feet.
        var any = false;
        foreach (var seed in new[] { 42UL, 7UL, 2026UL })
        {
            var b = RegionTestWorld.Valley(seed).Bounds;
            var plain = new ClimateSampler(TestGame.Data.WorldGen, seed);
            var model = new HeightModel(TestGame.Data.WorldGen, seed);
            var radius = Blend.LandBias.Radius;
            for (var z = b.MinZ - radius; z <= b.MaxZ + radius && !any; z += 8)
            {
                for (var x = b.MinX - radius; x <= b.MaxX + radius && !any; x += 8)
                {
                    var shape = model.Shape(x, z, plain.At(x, z));
                    any = b.DistanceTo(x, z) is > 0 && b.DistanceTo(x, z) <= radius && shape.Water && shape.RiverWeight == 0 && shape.LakeWeight == 0;
                }
            }
        }

        Assert.True(any);
    }

    [Fact]
    public void Given_TheMarginsFirstColumns_When_TheirWaterIsRead_Then_RiversAndLakesHaveFadedOut()
    {
        var world = RegionTestWorld.Composition();
        var b = RegionTestWorld.Bounds;
        var heaviest = 0;
        for (var d = 1; d <= 8; d++)
        {
            foreach (var (x, z, _, _) in Ring(b, d, 2))
            {
                var shape = RegionTestWorld.Shape(world, x, z);
                heaviest = Math.Max(heaviest, Math.Max(shape.RiverWeight, shape.LakeWeight));
            }
        }

        Assert.True(heaviest <= IntMath.One / 20, $"a river or lake still weighs {heaviest} of {IntMath.One} within 8 columns of the border");
    }

    [Fact]
    public void Given_TheExitCorridor_When_ItsCentreLineIsWalked_Then_ItStartsAtTheEdgeHeightAndNoStepIsOverOneBlock()
    {
        var world = RegionTestWorld.Composition();
        var region = RegionTestWorld.Valley();
        var corridor = Assert.Single(Blend.ExitCorridors);
        Assert.Equal((383, 208), (corridor.FromX, corridor.FromZ));

        var profile = world.Procedural.World.Blended!.RoadProfile(0);
        Assert.Equal(region.EdgeHeight(corridor.FromX, corridor.FromZ), profile[0]);
        for (var i = 1; i < profile.Count; i++)
        {
            Assert.InRange(profile[i] - profile[i - 1], -1, 1);
            Assert.True(profile[i] > TestGame.Data.WorldGen.Vertical.SeaLevel);
        }

        // The ground itself along the line (and across the road's width) is the profile.
        var steps = new List<int>();
        for (var x = corridor.FromX + 1; x <= corridor.ToX - corridor.Fade; x++)
        {
            var h = Height(world, x, corridor.FromZ);
            Assert.Equal(profile[x - corridor.FromX], h);
            for (var dz = -corridor.HalfWidth; dz <= corridor.HalfWidth; dz++)
            {
                Assert.Equal(h, Height(world, x, corridor.FromZ + dz));
            }

            steps.Add(h);
        }

        output.WriteLine($"the exit road runs from y {profile[0]} to y {profile[^1]} over {profile.Count - 1} blocks");
        Assert.All(steps.Zip(steps.Skip(1)), pair => Assert.InRange(pair.Second - pair.First, -1, 1));
    }

    [Fact]
    public void Given_TheExitCorridor_When_ItsBlocksAreGenerated_Then_TwoBlocksOverTheRoadAreFreeOfTrunksAndCrownsAndTheRoadIsNotWater()
    {
        var world = RegionTestWorld.Composition();
        var corridor = Blend.ExitCorridors[0];
        var blocked = new List<string>();
        for (var cz = (corridor.FromZ - corridor.HalfWidth) >> 4; cz <= (corridor.FromZ + corridor.HalfWidth) >> 4; cz++)
        {
            for (var cx = corridor.FromX >> 4; cx <= corridor.ToX >> 4; cx++)
            {
                var column = world.Source.GenerateColumn(cx, cz);
                BlockId At(int x, int y, int z)
                {
                    var chunk = column[(y >> 4) - world.Source.MinChunkY];
                    return chunk[x & 15, y & 15, z & 15];
                }

                for (var z = Math.Max(cz << 4, corridor.FromZ - corridor.HalfWidth); z <= Math.Min((cz << 4) + 15, corridor.FromZ + corridor.HalfWidth); z++)
                {
                    for (var x = Math.Max(cx << 4, corridor.FromX + 1); x <= Math.Min((cx << 4) + 15, corridor.ToX); x++)
                    {
                        var ground = Height(world, x, z);
                        if (At(x, ground, z) is BlockId.Water or BlockId.Air || !BlockDefinition.IsSolid(At(x, ground, z)))
                        {
                            blocked.Add($"{x},{z}: ground {At(x, ground, z)}");
                        }

                        foreach (var up in new[] { 1, 2 })
                        {
                            if (BlockDefinition.IsSolid(At(x, ground + up, z)))
                            {
                                blocked.Add($"{x},{z}: {At(x, ground + up, z)} at +{up}");
                            }
                        }
                    }
                }
            }
        }

        Assert.Empty(blocked);
    }
}
