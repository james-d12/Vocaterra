using System.Security.Cryptography;
using System.Text;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Tests.World.Far;

/// <summary>
/// The far model's output pinned byte for byte (B36): the packed sections of the model's stacks at levels 0 to 7 over land, a river, a coast, cliffs and open sea, for the seeds 42 and 2026 and for the valley's blended world, and the samples
/// of the sampler's grids, are hashed. They were recorded from the code before the sampler shared its work between columns, so any change to what the sampler answers fails here.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarSectionDigestTests
{
    // The recorded digests: "<world> <place>" to the digest of the stacks of levels 0 to 7 and of the sample grids.
    private static readonly Dictionary<string, string> Expected = new(StringComparer.Ordinal)
    {
        ["free2026 coast"] = "AC5FEB649D1410774F3E58DF",
        ["free2026 forest"] = "640110984EE5F06C59DF6DE2",
        ["free2026 mountain"] = "B2502FBFFB3FB7D13FD80531",
        ["free2026 river"] = "D7363F3C963577A6CC4BBC9B",
        ["free2026 sea"] = "E39F4D83E01F2AF076EE61D2",
        ["free42 coast"] = "D76B7CF591A5F37B9517BA6E",
        ["free42 forest"] = "8AE44F688246D4A8DA4F80C8",
        ["free42 mountain"] = "6F433FAA1ABF4A4A2A001B70",
        ["free42 river"] = "831DE177B9C0D3DE621DC0B8",
        ["free42 sea"] = "D882600170624BD77F003B22",
        ["valley42 edge"] = "AD70CF7CEC5ECA348D39DAE0",
        ["valley42 ridge"] = "C411CB85FCFCCCF73801E609",
    };

    public static TheoryData<string> Places()
    {
        var data = new TheoryData<string>();
        foreach (var world in new[] { "free42", "free2026", "valley42" })
        {
            foreach (var place in world == "valley42" ? new[] { "edge", "ridge" } : new[] { "river", "coast", "mountain", "forest", "sea" })
            {
                data.Add($"{world} {place}");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void Given_APlaceAndAWorld_When_TheModelsStacksAndGridsAreHashed_Then_TheyAreTheRecordedDigests(string name)
    {
        var (world, place) = (name.Split(' ')[0], name.Split(' ')[1]);
        var (sampler, cx, cz) = Locate(world, place);
        var digest = Digest(sampler, cx, cz);

        Assert.True(Expected.TryGetValue(name, out var expected), $"[\"{name}\"] = \"{digest}\",");
        Assert.Equal(expected, digest);
    }

    private static (FarTerrainSampler Sampler, int Cx, int Cz) Locate(string world, string place)
    {
        if (world == "valley42")
        {
            // Just outside the valley's footprint (0 to 383), inside its blend margin, and a mountain range away from it.
            var composed = RegionTestWorld.Composition().World.Far;
            return place == "edge" ? (composed, 26, 8) : (composed, -30, 40);
        }

        var seed = world == "free42" ? 42UL : 2026UL;
        var sampler = new FarTerrainSampler(ProceduralTestWorld.Source(seed).World, RegionCatalog.Empty);
        var (cx, cz) = place switch
        {
            "river" => ProceduralTestWorld.Find(seed, Feature.River),
            "coast" => ProceduralTestWorld.Find(seed, Feature.Coast),
            "mountain" => ProceduralTestWorld.Find(seed, Feature.Mountain),
            "forest" => ProceduralTestWorld.Find(seed, Feature.Forest),
            _ => OpenSea(sampler),
        };
        return (sampler, cx, cz);
    }

    // The nearest chunk column to the origin, in rings of 7, whose centre is open sea.
    private static (int Cx, int Cz) OpenSea(FarTerrainSampler sampler)
    {
        for (var ring = 1; ring < 300; ring++)
        {
            for (var gz = -ring; gz <= ring; gz++)
            {
                for (var gx = -ring; gx <= ring; gx++)
                {
                    if (Math.Max(Math.Abs(gx), Math.Abs(gz)) == ring)
                    {
                        var s = sampler.At(((gx * 7) << 4) + 8, ((gz * 7) << 4) + 8);
                        if (s.IsWater && s.Height < sampler.SeaLevel - 8)
                        {
                            return (gx * 7, gz * 7);
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException("no open sea");
    }

    private static string Digest(FarTerrainSampler sampler, int cx, int cz)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var sections = new LodModelSections(sampler.Lod(4), sampler.MinY, sampler.MaxY);
        for (var level = 0; level <= 7; level++)
        {
            var (min, max) = LodWorld.SyRange(level, sampler.MinY, sampler.MaxY);
            var shift = LodSection.Shift + level;
            foreach (var section in sections.Stack(level, (cx << VoxelChunk.Shift) >> shift, (cz << VoxelChunk.Shift) >> shift, min, max))
            {
                hash.AppendData(section.Pack());
            }
        }

        var (x0, z0) = (cx << VoxelChunk.Shift, cz << VoxelChunk.Shift);
        var text = new StringBuilder();
        foreach (var step in new[] { 1, 2, 5 })
        {
            var grid = new FarSample[24 * 24];
            sampler.Fill(x0, z0, step, 24, 24, grid);
            foreach (var sample in grid)
            {
                text.Append(sample).Append(';');
            }
        }

        foreach (var cell in new[] { 6, 8, 32, 128 })
        {
            var grid = new FarSample[16 * 16];
            sampler.FillCells(x0, z0, cell, 16, 16, grid);
            foreach (var sample in grid)
            {
                text.Append(sample).Append(';');
            }
        }

        hash.AppendData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(hash.GetHashAndReset())[..24];
    }
}
