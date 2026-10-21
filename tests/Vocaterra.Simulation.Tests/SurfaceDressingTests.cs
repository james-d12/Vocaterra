using System.Security.Cryptography;
using System.Text;
using Vocaterra.Simulation.Generation;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Tests;

/// <summary>The P6 generation passes (sand, flowers, mushrooms) and the promise that terrain, trees and the spawn did not move.</summary>
public sealed class SurfaceDressingTests
{
    /// <summary>The SHA-256 of the seed-42 tree bases (generator version 2, before P6), written as "x,y" pairs joined by ';'.</summary>
    private const string Seed42TreeBasesHash = "2bd06abe53176b3217b57d3625e47e48ad7e2ebc64d5af62f8e5abf98215e774";

    private static readonly WorldSize MediumSize = new(512, 256);

    private static string TreeBasesHash(World world)
    {
        var text = string.Join(";", world.Record.TreeBases.Select(t => $"{t.X},{t.Y}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(text))).ToLowerInvariant();
    }

    [Fact]
    public void SeedFortyTwoHasSandFlowersAndMushroomsOnItsSurface()
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

        Assert.True(counts.GetValueOrDefault(TileId.Sand) > 100, $"sand tiles: {counts.GetValueOrDefault(TileId.Sand)}");
        Assert.True(counts.GetValueOrDefault(TileId.Flower) > 10, $"flowers: {counts.GetValueOrDefault(TileId.Flower)}");
        Assert.True(counts.GetValueOrDefault(TileId.Mushroom) > 5, $"mushrooms: {counts.GetValueOrDefault(TileId.Mushroom)}");
    }

    [Fact]
    public void TheTreesAndTheSpawnOfSeedFortyTwoAreWhereTheyWereBeforeP6()
    {
        var world = WorldGenerator.Generate(42);
        Assert.Equal(65, world.Record.TreeBases.Count);
        Assert.Equal(Seed42TreeBasesHash, TreeBasesHash(world));
        Assert.Equal(new TilePoint(512, 182), world.Spawn);
        Assert.Equal(TileId.Grass, world.GetTile(world.Spawn.X, world.Spawn.Y + 1));
    }

    [Fact]
    public void FlowersAndMushroomsStandOnGrassWithAirAboveThemAndSandReplacesGrassOnTheSurface()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var world = WorldGenerator.Generate(seed, MediumSize);
            for (var y = 1; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    var tile = world.GetTile(x, y);
                    if (tile is TileId.Flower or TileId.Mushroom)
                    {
                        Assert.Equal(TileId.Grass, world.GetTile(x, y + 1));
                    }
                }
            }

            foreach (var treeBase in world.Record.TreeBases)
            {
                for (var dx = -2; dx <= 2; dx++)
                {
                    Assert.NotEqual(TileId.Sand, world.GetTile(treeBase.X + dx, treeBase.Y));
                    Assert.NotEqual(TileId.Flower, world.GetTile(treeBase.X + dx, treeBase.Y - 1));
                    Assert.NotEqual(TileId.Mushroom, world.GetTile(treeBase.X + dx, treeBase.Y - 1));
                }
            }

            Assert.Equal(TileId.Grass, world.GetTile(world.Spawn.X, world.Spawn.Y + 1));
            for (var dx = -4; dx <= 4; dx++)
            {
                var x = world.Spawn.X + dx;
                Assert.NotEqual(TileId.Sand, world.GetTile(x, world.Record.SurfaceHeights[x]));
            }

            Assert.Equal(TileId.Air, world.GetTile(world.Spawn.X, world.Spawn.Y));
        }
    }

    [Fact]
    public void TheSurfacePassesRunAfterTheSpawnAndNeverMoveTheTerrain()
    {
        var passes = WorldGenerator.DefaultPasses.Select(p => p.Name).ToList();
        Assert.Equal(["terrain", "layers", "grass", "forest", "spawn", "spawn-tree", "sand", "flora", "caves", "ores"], passes);

        var withoutDressing = WorldGenerator.DefaultPasses.Where(p => p.Name is not ("sand" or "flora")).ToList();
        var plain = WorldGenerator.Generate(42, MediumSize, withoutDressing);
        var dressed = WorldGenerator.Generate(42, MediumSize);
        Assert.Equal(plain.Record.SurfaceHeights, dressed.Record.SurfaceHeights);
        Assert.Equal(plain.Record.TreeBases, dressed.Record.TreeBases);
        Assert.Equal(plain.Spawn, dressed.Spawn);
        for (var y = 0; y < plain.Height; y++)
        {
            for (var x = 0; x < plain.Width; x++)
            {
                var before = plain.GetTile(x, y);
                var after = dressed.GetTile(x, y);
                if (before != after)
                {
                    Assert.Contains(after, new[] { TileId.Sand, TileId.Flower, TileId.Mushroom, TileId.TallGrass });
                }
            }
        }
    }

    [Fact]
    public void TheSurfaceDressingIsADeterministicFunctionOfTheSeed()
    {
        var first = WorldGenerator.Generate(7, MediumSize);
        var second = WorldGenerator.Generate(7, MediumSize);
        Assert.Equal(first.ComputeDigestHex(), second.ComputeDigestHex());
    }
}
