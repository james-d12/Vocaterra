using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>
/// The shaped windows the residency and the ring share (B39): a band made through a group's window is the band made alone, the columns cut from the window are the columns made alone, the window cache is bounded, and a ring footprint makes the same four
/// columns (and takes the same lowest layer) whether its bands come from the wide window, the small one or none.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ColumnGroupStreamingTests
{
    [Fact]
    public void Given_AColumnOfOpenLand_When_ItsBandIsMadeThroughItsGroup_Then_AllSixteenBandsAreHeldAndEqualTheOnesMadeAlone()
    {
        var world = StreamedWorlds.Composition.World;
        var cache = new ColumnBandCache(world.Bands);

        var band = cache.GetThroughGroup(61, -19);

        Assert.Equal(world.Bands.Of(61, -19), band);
        Assert.Equal(1, cache.Groups.Count);
        Assert.True(cache.Groups.TryGet(60, -20, out var group));
        Assert.Equal(16, cache.Count);
        for (var cz = -20; cz < -16; cz++)
        {
            for (var cx = 60; cx < 64; cx++)
            {
                Assert.True(cache.TryGet(cx, cz, out var held));
                Assert.Equal(world.Bands.Of(cx, cz), held);
                Assert.True(group.Contains(cx, cz));
            }
        }
    }

    [Fact]
    public void Given_AColumnWhoseBandWasMadeThroughItsGroup_When_ItsLayersAreMadeFromTheGroup_Then_TheyEqualTheLayersMadeAlone()
    {
        var world = StreamedWorlds.Composition.World;
        var cache = new ColumnBandCache(world.Bands);
        _ = cache.GetThroughGroup(200, 90);

        foreach (var (cx, cz) in new[] { (200, 90), (203, 91), (201, 88) })
        {
            var band = cache.Get(cx, cz);
            _ = cache.Groups.TryGet(cx, cz, out var group);
            var alone = world.GenerateLayers(cx, cz, band.MinLayer - 1, band.MaxLayer + 1);
            var shared = world.GenerateLayers(cx, cz, band.MinLayer - 1, band.MaxLayer + 1, group);
            Assert.Equal(alone.Select(static c => c.ComputeDigestHex()), shared.Select(static c => c.ComputeDigestHex()));
        }
    }

    [Fact]
    public void Given_AGroupOfTheValleysColumnsOnly_When_ItsBandIsMadeThroughTheGroup_Then_NoWindowIsMadeAndTheBandIsTheRegions()
    {
        var world = StreamedWorlds.Composition.World;
        var cache = new ColumnBandCache(world.Bands);

        var band = cache.GetThroughGroup(9, 9); // chunk columns 8 to 11 lie in the valley (blocks 0 to 383)

        Assert.Equal(world.Bands.Of(9, 9), band);
        Assert.Equal(0, cache.Groups.Count);
    }

    [Fact]
    public void Given_AGroupCacheOfThree_When_FiveGroupsAreAdded_Then_ItHoldsTheNewestThree()
    {
        var world = StreamedWorlds.Composition.World;
        var groups = new ColumnGroupCache(3);
        for (var i = 0; i < 5; i++)
        {
            groups.Add(i * 4, 0, world.Bands.ShapeGroup(i * 4, 0, ColumnGroupCache.Span));
        }

        Assert.Equal(3, groups.Count);
        Assert.False(groups.TryGet(0, 0, out _));
        Assert.False(groups.TryGet(4, 0, out _));
        Assert.True(groups.TryGet(8, 0, out _));
        Assert.True(groups.TryGet(19, 3, out _));
        Assert.InRange(groups.Bytes, 3 * 400_000, 3 * 1_200_000);
    }

    [Theory]
    [InlineData(60, -30)]
    [InlineData(-27, 33)]
    public void Given_ARingFootprint_When_ItsColumnsAreMadeFromOneWindow_Then_TheyAndTheLowestLayerEqualThoseMadeAlone(int sx, int sz)
    {
        var world = StreamedWorlds.Composition.World;
        using var pool = new JobPool(0);
        var plain = new ColumnBandCache(world.Bands);
        var oneWindow = new LodRing(world.Far.FarWorld(new LodStore(), 2), world, pool, new ColumnBandCache(world.Bands));
        var warm = new ColumnBandCache(world.Bands);
        for (var dz = -1; dz <= 2; dz++)
        {
            for (var dx = -1; dx <= 2; dx++)
            {
                _ = warm.Get((2 * sx) + dx, (2 * sz) + dz); // every band of the ring round the footprint is held: the footprint takes the small window
            }
        }

        var smallWindow = new LodRing(world.Far.FarWorld(new LodStore(), 2), world, pool, warm);

        var lowest = new LodRing(world.Far.FarWorld(new LodStore(), 2), world, pool, plain).LowestLayerOf(sx, sz);
        var max = world.Source.MaxChunkY - 1;
        foreach (var ring in new[] { oneWindow, smallWindow })
        {
            var (columns, first) = ring.MakeColumns(sx, sz);

            Assert.Equal(lowest, first);
            for (var k = 0; k < 4; k++)
            {
                var alone = world.GenerateLayers((2 * sx) + (k & 1), (2 * sz) + (k >> 1), first, max);
                Assert.Equal(alone.Select(static c => c.ComputeDigestHex()), columns[k].Select(static c => c.ComputeDigestHex()));
            }
        }
    }
}
