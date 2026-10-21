using Vocaterra.Simulation.Generation;
using Vocaterra.Simulation.Queries;
using Vocaterra.Simulation.Tiles;
using Xunit.Abstractions;

namespace Vocaterra.Simulation.Tests;

/// <summary>Goldens and properties of world generation.</summary>
public sealed class GenerationTests(ITestOutputHelper output)
{
    /// <summary>The SHA-256 of the tile arrays of seed 42 at <see cref="WorldSize.Small"/>, generator version 2. Any change to a pass changes it on purpose, with <see cref="WorldGenerator.Version"/>.</summary>
    private const string Seed42Golden = "0505865bc11189d2e8f8e67edb1bf91f80cf1d7bc05b7cfd718f647b296d58a2";

    private static readonly WorldSize MediumSize = new(512, 256);

    public static TheoryData<ulong> Seeds => new() { 1, 2, 3, 7, 42, 99, 123, 1000, 31337, 65535, ulong.MaxValue, 0 };

    [Fact]
    public void Seed42HasTheGoldenDigest()
    {
        var world = WorldGenerator.Generate(42);
        output.WriteLine($"seed 42, {world.Width} x {world.Height}, generator version {WorldGenerator.Version}: {world.ComputeDigestHex()}");
        Assert.Equal(Seed42Golden, world.ComputeDigestHex());
    }

    [Fact]
    public void TheSameSeedTwiceGivesTheSameWorldAndADifferentSeedDiffers()
    {
        var first = WorldGenerator.Generate(42);
        var second = WorldGenerator.Generate(42);
        var other = WorldGenerator.Generate(43);
        Assert.Equal(first.ComputeDigestHex(), second.ComputeDigestHex());
        Assert.Equal(first.Spawn, second.Spawn);
        Assert.Equal(first.Record.TreeBases, second.Record.TreeBases);
        Assert.NotEqual(first.ComputeDigestHex(), other.ComputeDigestHex());
    }

    [Fact]
    public void ADifferentSizeIsADifferentDigestEvenWithTheSameSeed()
    {
        Assert.NotEqual(
            WorldGenerator.Generate(42, MediumSize).ComputeDigestHex(),
            WorldGenerator.Generate(42, new WorldSize(512, 288)).ComputeDigestHex());
    }

    [Fact]
    public void GenerationLeavesNoChunkDirtyAndRecordsThePasses()
    {
        var world = WorldGenerator.Generate(42, MediumSize);
        Assert.All(world.Tiles.Chunks, chunk => Assert.False(chunk.Dirty));
        Assert.Equal(["terrain", "layers", "grass", "forest", "spawn", "spawn-tree"], world.Record.Passes);
        Assert.Equal(16 * 8, world.Tiles.Chunks.Count);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void SpawnIsOnTheSurfaceWithClearAirAbove(ulong seed)
    {
        var world = WorldGenerator.Generate(seed, MediumSize);
        var spawn = world.Spawn;
        Assert.Equal(TileId.Air, world.GetTile(spawn.X, spawn.Y));
        Assert.Equal(TileId.Grass, world.GetTile(spawn.X, spawn.Y + 1));
        Assert.Equal(world.Record.SurfaceHeights[spawn.X], spawn.Y + 1);
        for (var dy = 1; dy <= 6; dy++)
        {
            Assert.Equal(TileId.Air, world.GetTile(spawn.X, spawn.Y - dy));
        }

        Assert.InRange(Math.Abs(spawn.X - (world.Width / 2)), 0, 40);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ATreeStandsWithinTwentyTilesOfTheSpawn(ulong seed)
    {
        var world = WorldGenerator.Generate(seed, MediumSize);
        var near = world.Record.TreeBases.Where(b => Math.Abs(b.X - world.Spawn.X) <= 20).ToList();
        Assert.NotEmpty(near);
        foreach (var treeBase in near)
        {
            Assert.Equal(TileId.Grass, world.GetTile(treeBase.X, treeBase.Y));
            Assert.Equal(TileId.Trunk, world.GetTile(treeBase.X, treeBase.Y - 1));
        }
    }

    [Fact]
    public void TheSpawnTreePassFillsInWhereTheForestAloneLeavesNoTreeNearTheSpawn()
    {
        var withoutPass = WorldGenerator.DefaultPasses.Where(pass => pass.Name != "spawn-tree").ToList();
        var needed = 0;
        for (ulong seed = 1; seed <= 150; seed++)
        {
            var plain = WorldGenerator.Generate(seed, MediumSize, withoutPass);
            var full = WorldGenerator.Generate(seed, MediumSize);
            Assert.Contains(full.Record.TreeBases, b => Math.Abs(b.X - full.Spawn.X) <= 20);
            if (!plain.Record.TreeBases.Any(b => Math.Abs(b.X - plain.Spawn.X) <= 20))
            {
                needed++;
                var added = Assert.Single(full.Record.TreeBases.Except(plain.Record.TreeBases));
                Assert.InRange(Math.Abs(added.X - full.Spawn.X), 10, 20);
            }
            else
            {
                Assert.Equal(plain.ComputeDigestHex(), full.ComputeDigestHex());
            }
        }

        output.WriteLine($"{needed} of 150 seeds had no tree within 20 tiles of the spawn before the spawn-tree pass");
        Assert.True(needed > 0, "The test no longer exercises the pass: pick a range of seeds that needs it.");
    }

    [Fact]
    public void ThePlantedTreeKeepsTheSpawnHeadroomAndDoesNotCrowdOtherTrunks()
    {
        for (ulong seed = 1; seed <= 150; seed++)
        {
            var world = WorldGenerator.Generate(seed, MediumSize);
            var spawn = world.Spawn;
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = 1; dy <= 4; dy++)
                {
                    Assert.Equal(TileId.Air, world.GetTile(spawn.X + dx, spawn.Y + 1 - dy));
                }
            }

            var xs = world.Record.TreeBases.Select(b => b.X).Order().ToList();
            for (var i = 1; i < xs.Count; i++)
            {
                Assert.True(xs[i] - xs[i - 1] >= 3, $"Trunks at {xs[i - 1]} and {xs[i]} are closer than 3 columns (seed {seed}).");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoTreeFloats(ulong seed)
    {
        var world = WorldGenerator.Generate(seed, MediumSize);
        var trunks = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetTile(x, y) == TileId.Trunk)
                {
                    trunks++;
                    Assert.True(
                        world.GetTile(x, y + 1) is TileId.Grass or TileId.Trunk,
                        $"Trunk at ({x}, {y}) stands on {world.GetTile(x, y + 1)} (seed {seed}).");
                }

                if (world.GetTile(x, y) == TileId.Leaves)
                {
                    Assert.True(HasTrunkNearby(world, x, y), $"Leaves at ({x}, {y}) have no trunk within 3 tiles (seed {seed}).");
                }
            }
        }

        Assert.Equal(trunks > 0, world.Record.TreeBases.Count > 0);
        foreach (var treeBase in world.Record.TreeBases)
        {
            Assert.Equal(TileId.Grass, world.GetTile(treeBase.X, treeBase.Y));
            Assert.Equal(TileId.Trunk, world.GetTile(treeBase.X, treeBase.Y - 1));
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ForestHasTreesAndTerrainStaysInBounds(ulong seed)
    {
        var world = WorldGenerator.Generate(seed, MediumSize);
        Assert.InRange(world.Record.TreeBases.Count, 20, 200);
        var min = WorldGenerator.MinSurfaceY(world.Height);
        var max = WorldGenerator.MaxSurfaceY(world.Height);
        for (var x = 0; x < world.Width; x++)
        {
            var surface = world.Record.SurfaceHeights[x];
            Assert.InRange(surface, min, max);

            // Ground: grass on the surface, the first solid tile from the top is that tile, and the bottom row is solid.
            Assert.Equal(TileId.Grass, world.GetTile(x, surface));
            Assert.Equal(TileId.Dirt, world.GetTile(x, surface + 1));
            Assert.True(world.IsSolid(x, world.Height - 1));
            for (var y = 0; y < surface; y++)
            {
                Assert.False(world.IsSolid(x, y), $"Solid tile above the surface at ({x}, {y}).");
            }
        }
    }

    [Fact]
    public void Seed42BothLayersAreThere()
    {
        var world = WorldGenerator.Generate(42);
        var counts = new Dictionary<TileId, int>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var tile = world.GetTile(x, y);
                counts[tile] = counts.GetValueOrDefault(tile) + 1;
            }
        }

        foreach (var (tile, count) in counts.OrderBy(pair => pair.Key))
        {
            output.WriteLine($"{tile}: {count}");
        }

        foreach (var tile in new[] { TileId.Air, TileId.Dirt, TileId.Grass, TileId.Stone, TileId.Trunk, TileId.Leaves })
        {
            Assert.True(counts.GetValueOrDefault(tile) > 0, $"{tile} never appears.");
        }

        Assert.Equal(world.Width, counts[TileId.Grass]);
    }

    [Fact]
    public void AsciiMapOfSeed42AroundTheSpawn()
    {
        var sim = GameSimulation.Create(42);
        var spawn = sim.World.Spawn;
        var map = AsciiMap.Render(sim.World, spawn.X, spawn.Y + 12, 120, 60, sim.Player);
        output.WriteLine($"seed 42, spawn tile ({spawn.X}, {spawn.Y}), window 120 x 60 centred on ({spawn.X}, {spawn.Y + 12}); @ is the player");
        output.WriteLine(map);
        var lines = map.TrimEnd('\n').Split('\n');
        Assert.Equal(60, lines.Length);
        Assert.All(lines, line => Assert.Equal(120, line.Length));
        Assert.Contains('@', map);
        Assert.Contains('|', map);
    }

    [Fact]
    public void ChunkAddressingMatchesTileAddressing()
    {
        var world = WorldGenerator.Generate(42);
        Assert.Equal(32, world.Tiles.ChunksX);
        Assert.Equal(16, world.Tiles.ChunksY);
        var coord = ChunkCoord.FromTile(100, 70);
        Assert.Equal(new ChunkCoord(3, 2), coord);
        Assert.Equal(new TilePoint(96, 64), coord.Origin);
        var chunk = world.Tiles.GetChunk(coord);
        Assert.Equal(world.GetTile(100, 70), chunk[100 - 96, 70 - 64]);
        Assert.Equal(world.GetTile(100, 70), chunk.Tiles[((70 - 64) * 32) + (100 - 96)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Tiles.GetChunk(new ChunkCoord(32, 0)));
        Assert.Equal(TileId.Air, world.GetTile(-1, 0));
        Assert.True(world.IsSolid(-1, 0));
        Assert.True(world.IsSolid(0, world.Height));
    }

    [Fact]
    public void ChangingATileMarksOnlyItsChunkDirtyAndChangesTheDigest()
    {
        var world = WorldGenerator.Generate(42, MediumSize);
        var before = world.ComputeDigestHex();
        world.Tiles.SetTile(100, 70, world.GetTile(100, 70) == TileId.Air ? TileId.Dirt : TileId.Air);
        Assert.NotEqual(before, world.ComputeDigestHex());
        Assert.Equal([new ChunkCoord(3, 2)], world.Tiles.Chunks.Where(c => c.Dirty).Select(c => c.Coord));
        world.Tiles.ClearDirty();
        Assert.DoesNotContain(world.Tiles.Chunks, c => c.Dirty);
    }

    [Fact]
    public void InvalidSizesAreRefused()
    {
        Assert.Throws<ArgumentException>(() => new WorldSize(100, 64));
        Assert.Throws<ArgumentException>(() => new WorldSize(0, 64));
        Assert.Throws<ArgumentException>(() => WorldGenerator.Generate(1, new WorldSize(64, 64)));
    }

    private static bool HasTrunkNearby(World world, int x, int y)
    {
        for (var dy = -3; dy <= 3; dy++)
        {
            for (var dx = -3; dx <= 3; dx++)
            {
                if (world.GetTile(x + dx, y + dy) == TileId.Trunk)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
