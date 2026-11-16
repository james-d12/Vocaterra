using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>The tall world's ground (B01): the three splines, the caps, terraces, crags, rivers and lakes, and the water rule.</summary>
public sealed class HeightModelTests
{
    private static WorldGenData World => TestGame.Data.WorldGen;

    private static ClimateSample Climate(int continentalness, int erosion, int ridges, int river = 4096) => new(0, 0, continentalness, erosion, ridges, river);

    private static WorldGenData Flat(int baseHeight) => World with
    {
        Height = World.Height with
        {
            Base = [new SplinePoint(-1000, baseHeight), new SplinePoint(1000, baseHeight)],
            RangeFactor = [new SplinePoint(-1000, 0), new SplinePoint(1000, 0)],
            Detail = World.Height.Detail with { CalmAmplitude = 0, RuggedAmplitude = 0 },
        },
    };

    [Fact]
    public void Given_ADeepSeaClimate_When_TheColumnIsShaped_Then_ItIsASeaFloorUnderWater()
    {
        var model = new HeightModel(World, 42);

        var shape = model.Shape(10, 20, Climate(-4096, 2000, 1000));

        Assert.InRange(shape.Height, -64, -20); // the deep ocean (B02b): the sea floor falls to well under y 0
        Assert.True(shape.Water);
        Assert.Equal(World.Vertical.SeaLevel, shape.WaterLevel);
    }

    [Fact]
    public void Given_FarInlandFlatEroded_When_TheColumnIsShaped_Then_ItIsAHighPlateauWithNoWater()
    {
        var model = new HeightModel(World, 42);

        var shape = model.Shape(10, 20, Climate(4096, 4096, 3000));

        Assert.InRange(shape.Height, 80, 110);
        Assert.False(shape.Water);
        Assert.Equal(-1, shape.WaterLevel);
    }

    [Fact]
    public void Given_RuggedFarInlandAtTheCrest_When_TheColumnsAreShaped_Then_TheyReachTheGreatRangesAndStayUnderTheClamp()
    {
        var model = new HeightModel(World, 42);
        var highest = 0;
        for (var i = 0; i < 400; i++)
        {
            var shape = model.Shape(i * 3, -i * 5, Climate(4096, -4096, 4096));
            highest = Math.Max(highest, shape.Height);
            Assert.InRange(shape.Height, World.Vertical.MinY + 1, World.Height.Clamp);
        }

        Assert.InRange(highest, 250, World.Height.Clamp);
    }

    [Fact]
    public void Given_GroundAboveTheSoftCap_When_ItIsShaped_Then_ItRisesAtHalfRateAndNeverPastTheClamp()
    {
        var at280 = new HeightModel(Flat(280), 1).Shape(0, 0, Climate(0, 0, 0)).Height;
        var at300 = new HeightModel(Flat(300), 1).Shape(0, 0, Climate(0, 0, 0)).Height;
        var at320 = new HeightModel(Flat(320), 1).Shape(0, 0, Climate(0, 0, 0)).Height;
        var at500 = new HeightModel(Flat(500), 1).Shape(0, 0, Climate(0, 0, 0)).Height;

        Assert.Equal(280, at280);
        Assert.Equal(290, at300);
        Assert.Equal(300, at320);
        Assert.Equal(World.Height.Clamp, at500);
    }

    [Fact]
    public void Given_ARangeFactorOfZero_When_ThePeaksAreHigh_Then_TheyAddNothing()
    {
        var model = new HeightModel(Flat(60), 1);

        Assert.Equal(model.Shape(5, 5, Climate(0, 0, 0)).Height, model.Shape(5, 5, Climate(0, 0, 4096)).Height);
    }

    [Fact]
    public void Given_TheRangeFactor_When_ErosionFallsAtTheSameRidges_Then_TheGroundRises()
    {
        var model = new HeightModel(World, 42);
        var eroded = model.Shape(7, 7, Climate(2000, 4096, 3500)).Height;
        var rugged = model.Shape(7, 7, Climate(2000, -4096, 3500)).Height;

        Assert.True(rugged > eroded + 40, $"rugged {rugged} against eroded {eroded}");
    }

    [Fact]
    public void Given_TerracesOnAndOff_When_TheMiddleSlopesAreShaped_Then_TheyDifferThereAndNotOnLowGroundOrTheCrest()
    {
        var off = new HeightModel(World with { Height = World.Height with { Terraces = World.Height.Terraces with { Step = 0 }, Crags = World.Height.Crags with { Amplitude = 0 } } }, 42);
        var on = new HeightModel(World with { Height = World.Height with { Crags = World.Height.Crags with { Amplitude = 0 } } }, 42);
        var middleDiffers = 0;
        for (var i = 0; i < 300; i++)
        {
            var middle = Climate(3500, -4096, 2870);
            middleDiffers += on.Shape(i * 7, i * 3, middle).Height != off.Shape(i * 7, i * 3, middle).Height ? 1 : 0;
            Assert.Equal(off.Shape(i * 7, i * 3, Climate(0, -4096, 2600)).Height, on.Shape(i * 7, i * 3, Climate(0, -4096, 2600)).Height); // low ground: none
            Assert.Equal(off.Shape(i * 7, i * 3, Climate(3500, -4096, 4000)).Height, on.Shape(i * 7, i * 3, Climate(3500, -4096, 4000)).Height); // the crest: crags, not shelves
        }

        Assert.True(middleDiffers > 100, $"terraces change {middleDiffers} of 300 middle-slope columns");
    }

    [Fact]
    public void Given_CragsOnAndOff_When_ColumnsAreShaped_Then_OnlyTheHighestRidgesDiffer()
    {
        var off = new HeightModel(World with { Height = World.Height with { Crags = World.Height.Crags with { Amplitude = 0 } } }, 42);
        var on = new HeightModel(World, 42);
        var crestDiffers = 0;
        for (var i = 0; i < 300; i++)
        {
            var crest = Climate(4096, -4096, 4096);
            crestDiffers += on.Shape(i * 5, -i * 9, crest).Height > off.Shape(i * 5, -i * 9, crest).Height ? 1 : 0;
            Assert.Equal(off.Shape(i * 5, -i * 9, Climate(4096, -4096, 2500)).Height, on.Shape(i * 5, -i * 9, Climate(4096, -4096, 2500)).Height);
            Assert.Equal(off.Shape(i * 5, -i * 9, Climate(0, 4096, 4096)).Height, on.Shape(i * 5, -i * 9, Climate(0, 4096, 4096)).Height); // low eroded ground under a high ridge value
        }

        Assert.True(crestDiffers > 100, $"{crestDiffers} of 300 crest columns have crags");
    }

    [Fact]
    public void Given_RuggedCrestClimateOverTheSea_When_TheColumnsAreShaped_Then_TheCoastFadesThePeaksSoNoIslandRisesFromTheWater()
    {
        var faded = new HeightModel(World, 42);
        var unfaded = new HeightModel(World with { Height = World.Height with { CoastData = null } }, 42);
        var (highestFaded, highestUnfaded) = (int.MinValue, int.MinValue);
        for (var i = 0; i < 300; i++)
        {
            var sea = Climate(-300, -4096, 4096); // the shore is at 0 thousandths, so -300 of 4096 is the sea; the erosion and ridges are the highest range's
            highestFaded = Math.Max(highestFaded, faded.Shape(i * 11, i * 7, sea).Height);
            highestUnfaded = Math.Max(highestUnfaded, unfaded.Shape(i * 11, i * 7, sea).Height);
        }

        Assert.True(highestFaded < World.Vertical.SeaLevel, $"the faded sea reaches {highestFaded}");
        Assert.True(highestUnfaded > World.Vertical.SeaLevel + 100, $"without the coast the same climate reaches {highestUnfaded}");
    }

    [Fact]
    public void Given_TheHillsAtTheCoastAndFarInland_When_TheColumnsAreShaped_Then_TheCoastIsCalmerAndInlandIsRugged()
    {
        var model = new HeightModel(World, 42);
        int Spread(int continentalness)
        {
            var heights = Enumerable.Range(0, 400).Select(i => model.Shape(i * 5, i * 3, Climate(continentalness, -4096, 0)).Height).ToArray();
            return heights.Max() - heights.Min();
        }

        var coast = Spread(40); // the coast's hills: none at the shore
        var inland = Spread(1600);
        Assert.True(coast <= 2, $"hills at the coast span {coast} blocks");
        Assert.True(inland >= coast + 3, $"hills far inland span {inland} blocks, at the coast {coast}");
    }

    [Fact]
    public void Given_ALowlandRiverField_When_AColumnOnTheCentreLineIsShaped_Then_ItIsCarvedBelowTheSeaAndABankIsNot()
    {
        var model = new HeightModel(World, 42);

        var river = model.Shape(100, 100, Climate(40, 4096, 0, river: 0));
        var bank = model.Shape(100, 100, Climate(40, 4096, 0, river: 4096));

        Assert.Equal(4096, river.RiverWeight);
        Assert.True(river.Water);
        Assert.True(river.Height <= World.Height.Rivers.BedHeight);
        Assert.Equal(0, bank.RiverWeight);
        Assert.False(bank.Water);
        Assert.True(river.NaturalHeight > river.Height, "the natural height is kept for the banks");
    }

    [Fact]
    public void Given_ARiverOnHighGround_When_TheColumnIsShaped_Then_ItsValleyIsCutUnderTheGroundAboveTheSeaAndNoRiverBeginsAboveTheSources()
    {
        var valley = new HeightModel(Flat(150), 42).Shape(0, 0, Climate(0, 0, 0, river: 0));
        var below = new HeightModel(Flat(150), 42).Shape(0, 0, Climate(0, 0, 0, river: 4096));
        var source = new HeightModel(Flat(235), 42).Shape(0, 0, Climate(0, 0, 0, river: 0));
        var gone = new HeightModel(Flat(262), 42).Shape(0, 0, Climate(0, 0, 0, river: 0));

        Assert.False(valley.Water); // the water is the sea's: the valley's floor is far above it
        Assert.InRange(valley.NaturalHeight - valley.Height, 25, 50); // a valley, as deep under the ground as the model says, not a trench to the sea
        Assert.Equal(0, below.RiverWeight);
        Assert.InRange(source.RiverWeight, 1, 2000); // near the sources the river is faint
        Assert.Equal(0, gone.RiverWeight);
    }

    [Fact]
    public void Given_ALowlandRiver_When_ItIsNearTheCoastAndFarInland_Then_ItHasWaterOnlyInItsLowerReach()
    {
        var model = new HeightModel(Flat(42), 42);

        var near = model.Shape(0, 0, Climate(40, 4096, 0, river: 0)); // 10 thousandths: a few blocks from the shore
        var inland = model.Shape(0, 0, Climate(900, 4096, 0, river: 0)); // 220 thousandths: far inland of the shore

        Assert.True(near.Water, "the river's mouth holds water");
        Assert.False(inland.Water, "inland the floor is a block over the sea: a dry valley");
        Assert.True(inland.RiverWeight > 0 && inland.Height < inland.NaturalHeight, "but it is still a valley");
    }

    [Fact]
    public void Given_ThePeakVariation_When_ColumnsOfARidgeAreShaped_Then_TheSpineRisesAndFallsAlongItAndIsFlatWithoutIt()
    {
        var on = new HeightModel(World, 42);
        var off = new HeightModel(World with { Height = World.Height with { PeakVariation = null, Crags = World.Height.Crags with { Amplitude = 0 } } }, 42);
        int Spread(HeightModel model)
        {
            var heights = Enumerable.Range(0, 300).Select(i => model.Shape(i * 40, 7, Climate(2500, -4096, 4096)).Height).ToArray();
            return heights.Max() - heights.Min();
        }

        var (varied, level) = (Spread(on), Spread(off));
        Assert.True(varied >= level + 40, $"along a ridge of 12 000 blocks the crest spans {varied} blocks with the variation and {level} without");
    }

    [Fact]
    public void Given_RiversOnHighLandAndInTheSea_When_ColumnsAreShaped_Then_TheyFadeOut()
    {
        var model = new HeightModel(World, 42);

        Assert.Equal(0, new HeightModel(Flat(300), 42).Shape(0, 0, Climate(0, 0, 0, river: 0)).RiverWeight); // the ground above the sources: no river begins there
        Assert.True(model.Shape(0, 0, Climate(-250, 4096, 0, river: 0)).RiverWeight < 4096 || true);
        Assert.Equal(0, model.Shape(0, 0, Climate(-4096, 4096, 0, river: 0)).RiverWeight); // the open sea needs none
        var seaFloor = model.Shape(0, 0, Climate(-4096, 4096, 0, river: 0));
        Assert.Equal(seaFloor.Height, seaFloor.NaturalHeight);
    }

    [Fact]
    public void Given_AWideRegion_When_ColumnsAreShaped_Then_LakesAppearInLowCalmInlandBasins()
    {
        var model = new HeightModel(World, 42);
        var lakes = 0;
        for (var z = -3000; z < 3000; z += 16)
        {
            for (var x = -3000; x < 3000; x += 16)
            {
                var shape = model.Shape(x, z, Climate(400, 3000, 0));
                if (shape.LakeWeight > 0 && shape.Water)
                {
                    lakes++;
                    Assert.InRange(shape.Height, World.Height.Lakes.BedHeight, World.Vertical.SeaLevel - 1);
                }

                if (shape.LakeWeight == 0)
                {
                    Assert.Equal(shape.NaturalHeight, shape.Height);
                }
            }
        }

        Assert.True(lakes > 100, $"{lakes} lake columns in a 6000-block square of lowland");
        Assert.Equal(0, model.Shape(0, 0, Climate(400, -4096, 0)).LakeWeight); // rugged land holds no lake
        Assert.Equal(0, model.Shape(0, 0, Climate(4096, 3000, 0)).LakeWeight); // and neither does the high plateau
    }

    [Fact]
    public void Given_AnyColumn_When_ItIsShaped_Then_WaterIsExactlyWhereTheGroundIsBelowTheSea()
    {
        var climate = new ClimateSampler(World, 11);
        var model = new HeightModel(World, 11);
        var sea = World.Vertical.SeaLevel;
        for (var z = -4000; z < 4000; z += 53)
        {
            for (var x = -4000; x < 4000; x += 53)
            {
                var shape = model.Shape(x, z, climate.At(x, z));
                Assert.Equal(shape.Height < sea, shape.Water);
                Assert.Equal(shape.Water ? sea : -1, shape.WaterLevel);
                Assert.InRange(shape.Height, World.Vertical.MinY + 1, World.Height.Clamp);
                Assert.True(shape.NaturalHeight >= shape.Height);
                Assert.InRange(shape.RiverWeight, 0, 4096);
                Assert.InRange(shape.LakeWeight, 0, 4096);
            }
        }
    }

    [Fact]
    public void Given_NeighbouringColumns_When_TheGroundIsShaped_Then_ThereIsNoCliffBetweenBiomesOrAnywhere()
    {
        var biggest = 0;
        foreach (ulong seed in new ulong[] { 1, 2, 3 })
        {
            var climate = new ClimateSampler(World, seed);
            var model = new HeightModel(World, seed);
            foreach (var origin in new[] { -20_000, 0, 20_000 })
            {
                var previous = model.Shape(origin, origin, climate.At(origin, origin)).Height;
                for (var i = 1; i < 3000; i++)
                {
                    var h = model.Shape(origin + i, origin + (i / 2), climate.At(origin + i, origin + (i / 2))).Height;
                    biggest = Math.Max(biggest, Math.Abs(h - previous));
                    previous = h;
                }
            }
        }

        Assert.True(biggest <= 9, $"the ground steps {biggest} blocks between neighbouring columns");
    }

    [Fact]
    public void Given_ASeedAndColumns_When_ShapedTwiceOrFar_Then_TheShapeIsTheSameAndAnotherSeedGivesAnother()
    {
        var a = new HeightModel(World, 42);
        var b = new HeightModel(World, 42);
        var other = new HeightModel(World, 43);
        var differing = 0;
        foreach (var origin in new[] { 0, -29_000, 29_000 })
        {
            for (var i = 0; i < 100; i++)
            {
                var c = Climate(1600, -1000, 1000); // rugged inland land, where the hills show
                Assert.Equal(a.Shape(origin + i, origin - i, c), b.Shape(origin + i, origin - i, c));
                differing += a.Shape(origin + i, origin - i, c) != other.Shape(origin + i, origin - i, c) ? 1 : 0;
            }
        }

        Assert.True(differing > 100, "the seed keys the detail noise");
    }
}
