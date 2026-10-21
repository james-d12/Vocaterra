using Vocaterra.Simulation.Generation;
using Vocaterra.Simulation.Tiles;
using Xunit.Abstractions;

namespace Vocaterra.Simulation.Tests;

/// <summary>Caves, ore veins and the surface vegetation of generator version 4 (P7): counts and bands for seed 42, and properties over seeds.</summary>
public sealed class CavesAndOresTests(ITestOutputHelper output)
{
    private static readonly WorldSize MediumSize = new(512, 256);

    /// <summary>The ore kinds with their depth bands as the ore pass writes them (tiles below the surface column, inclusive).</summary>
    public static TheoryData<TileId, int, int> OreBands => new()
    {
        { TileId.CoalOre, 8, 60 },
        { TileId.CopperOre, 20, 90 },
        { TileId.IronOre, 40, 150 },
        { TileId.GoldOre, 110, 260 },
    };

    public static TheoryData<ulong> Seeds => new() { 1, 2, 3, 7, 42, 99, 123, 1000 };

    [Fact]
    public void SeedFortyTwoHasOreInEveryBandAndMoreAirUndergroundThanNone()
    {
        var world = WorldGenerator.Generate(42);
        var counts = new Dictionary<TileId, int>();
        var underground = 0;
        for (var x = 0; x < world.Width; x++)
        {
            var surface = world.Record.SurfaceHeights[x];
            for (var y = 0; y < world.Height; y++)
            {
                var tile = world.GetTile(x, y);
                counts[tile] = counts.GetValueOrDefault(tile);
                counts[tile]++;
                if (tile == TileId.Air && y - surface >= CavePass.MinDepth)
                {
                    underground++;
                }
            }
        }

        output.WriteLine($"seed 42 ore tiles: coal {counts.GetValueOrDefault(TileId.CoalOre)}, copper {counts.GetValueOrDefault(TileId.CopperOre)}, iron {counts.GetValueOrDefault(TileId.IronOre)}, gold {counts.GetValueOrDefault(TileId.GoldOre)}; underground air {underground}");
        Assert.True(counts.GetValueOrDefault(TileId.CoalOre) >= 40, "Seed 42 has too little coal.");
        Assert.True(counts.GetValueOrDefault(TileId.CopperOre) >= 60, "Seed 42 has too little copper.");
        Assert.True(counts.GetValueOrDefault(TileId.IronOre) >= 60, "Seed 42 has too little iron.");
        Assert.True(counts.GetValueOrDefault(TileId.GoldOre) >= 30, "Seed 42 has too little gold.");
        Assert.True(underground >= 10_000, "Seed 42 has too few caves.");
    }

    [Theory]
    [MemberData(nameof(OreBands))]
    public void OreOfEachKindLiesWithinItsDepthBand(TileId ore, int minDepth, int maxDepth)
    {
        foreach (var seed in new ulong[] { 1, 2, 42, 99 })
        {
            var world = WorldGenerator.Generate(seed, MediumSize);
            var found = 0;
            for (var x = 0; x < world.Width; x++)
            {
                var surface = world.Record.SurfaceHeights[x];
                for (var y = 0; y < world.Height; y++)
                {
                    if (world.GetTile(x, y) != ore)
                    {
                        continue;
                    }

                    found++;
                    var depth = y - surface;
                    Assert.InRange(depth, minDepth, maxDepth);
                }
            }

            Assert.True(found > 0, $"{ore} never appears for seed {seed}.");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoCaveOpensWithinSixTilesOfTheSurface(ulong seed)
    {
        var world = WorldGenerator.Generate(seed, MediumSize);
        for (var x = 0; x < world.Width; x++)
        {
            var surface = world.Record.SurfaceHeights[x];
            for (var dy = 0; dy <= 6; dy++)
            {
                Assert.NotEqual(TileId.Air, world.GetTile(x, surface + dy));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheSpawnAreaStaysIntact(ulong seed)
    {
        var world = WorldGenerator.Generate(seed, MediumSize);
        var spawn = world.Spawn;
        for (var x = spawn.X - 20; x <= spawn.X + 20; x++)
        {
            var surface = world.Record.SurfaceHeights[x];
            for (var dy = 0; dy <= 6; dy++)
            {
                Assert.NotEqual(TileId.Air, world.GetTile(x, surface + dy));
            }
        }

        // The spawn column itself: the grass under the spawn, and no cave or ore beside the feet.
        Assert.Equal(TileId.Grass, world.GetTile(spawn.X, spawn.Y + 1));
        Assert.Equal(TileId.Air, world.GetTile(spawn.X, spawn.Y));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EverySeedHasSomeCaveUnderground(ulong seed)
    {
        var world = WorldGenerator.Generate(seed, MediumSize);
        var underground = 0;
        for (var x = 0; x < world.Width; x++)
        {
            var surface = world.Record.SurfaceHeights[x];
            for (var y = surface + CavePass.MinDepth; y < world.Height; y++)
            {
                if (world.GetTile(x, y) == TileId.Air)
                {
                    underground++;
                }
            }
        }

        Assert.True(underground > 0, $"Seed {seed} has no cave at all.");
    }

    [Fact]
    public void GenerationIsTheSameForTheSameSeedWithCavesAndOres()
    {
        var first = WorldGenerator.Generate(42, MediumSize);
        var second = WorldGenerator.Generate(42, MediumSize);
        Assert.Equal(first.ComputeDigestHex(), second.ComputeDigestHex());
        Assert.Equal(WorldGenerator.Version, first.Info.GeneratorVersion);
    }

}
