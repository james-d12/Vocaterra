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
/// of the sampler's grids, are hashed. They were recorded from the code before the sampler shared its work between columns, so any change to what the sampler answers fails here. B37 split each place's digest in three, recorded from the
/// B36 code: the levels 0 to 2 with the grids ("near"), level 3 and levels 4 to 7 ("far"), so that the far picture's change at the coarse levels (B37: levels 4 to 7 take the lattice point's climate for the cliff test's neighbours and no longer pool rivers; their digests were re-recorded, in a commit of their own) re-records the last and the others still pin the exact levels.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarSectionDigestTests
{
    // The recorded digests: "<world> <place> <part>" to the digest of the part: "near" (the stacks of levels 0 to 2 and the sample grids), "l3" (level 3's stacks) and "far" (levels 4 to 7).
    private static readonly Dictionary<string, string> Expected = new(StringComparer.Ordinal)
    {
        ["free2026 coast far"] = "0E5A9CA121DB3766ECA6712B",
        ["free2026 coast l3"] = "1CC7953ADA7A960F53C2B28D",
        ["free2026 coast near"] = "33EDDC308544F81BAA381793",
        ["free2026 forest far"] = "A252A0CA510200398226F979",
        ["free2026 forest l3"] = "0CB51A7A064D7DE898BD149E",
        ["free2026 forest near"] = "8DC1632EEE56E0547D12F973",
        ["free2026 mountain far"] = "DE841B5886E926C477C040C7",
        ["free2026 mountain l3"] = "5D0A254DDA9AFD1C7BD6A130",
        ["free2026 mountain near"] = "3351A5FE0AFB1EFE38E635E1",
        ["free2026 river far"] = "4C5C76C5E56119294D9CED69",
        ["free2026 river l3"] = "41167A574281527619138C7F",
        ["free2026 river near"] = "276C35D7973B9AED5A61B498",
        ["free2026 sea far"] = "0E5A9CA121DB3766ECA6712B",
        ["free2026 sea l3"] = "B9CFC5857D1C855A5D7AA080",
        ["free2026 sea near"] = "86DA70AE1F49C74E8160DA42",
        ["free42 coast far"] = "231FC3069E5E9038BFAE9C13",
        ["free42 coast l3"] = "DB63E95DFCFED2C40B0CEF6B",
        ["free42 coast near"] = "A7FE3EE826B8D95C310A0D9D",
        ["free42 forest far"] = "96F5F1525219D24B5C87F655",
        ["free42 forest l3"] = "C78F01A8E7ACEA19A1143030",
        ["free42 forest near"] = "E97876ABC201D70461DC237A",
        ["free42 mountain far"] = "1D7A8025F254B22871DFD4AB",
        ["free42 mountain l3"] = "63A22D1519FB082AB5FD833F",
        ["free42 mountain near"] = "AA56A5432C48D2FE3A9A826A",
        ["free42 river far"] = "E4D70C6A1639856CD26E7874",
        ["free42 river l3"] = "4EE14D6F344E10916F5A00C1",
        ["free42 river near"] = "D9A7073CABE69A1985F1BB66",
        ["free42 sea far"] = "FCE09896828F5D19EBF3431D",
        ["free42 sea l3"] = "4388B245EA6EBDF83C868D4C",
        ["free42 sea near"] = "1E558A32788C946D28209DFC",
        ["valley42 edge far"] = "E8E412FB9FC50BE70B027F8C",
        ["valley42 edge l3"] = "244CBEF80B71401E03460E90",
        ["valley42 edge near"] = "AA0514AADD199A3EB75E8C74",
        ["valley42 ridge far"] = "6AE357D129D104EDCA3ED5FF",
        ["valley42 ridge l3"] = "A43FA9A81E12CEF6CCCAEEE4",
        ["valley42 ridge near"] = "C749EDF20617366A3C879F3B",
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
        var failures = new List<string>();
        foreach (var part in new[] { "near", "l3", "far" })
        {
            var digest = Digest(sampler, cx, cz, part);
            if (!Expected.TryGetValue($"{name} {part}", out var expected) || expected != digest)
            {
                failures.Add($"[\"{name} {part}\"] = \"{digest}\",");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
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

    private static string Digest(FarTerrainSampler sampler, int cx, int cz, string part)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var sections = new LodModelSections(sampler.Lod(4), sampler.MinY, sampler.MaxY);
        var (first, last) = part switch { "near" => (0, 2), "l3" => (3, 3), _ => (4, 7) };
        for (var level = first; level <= last; level++)
        {
            var (min, max) = LodWorld.SyRange(level, sampler.MinY, sampler.MaxY);
            var shift = LodSection.Shift + level;
            foreach (var section in sections.Stack(level, (cx << VoxelChunk.Shift) >> shift, (cz << VoxelChunk.Shift) >> shift, min, max))
            {
                hash.AppendData(section.Pack());
            }
        }

        if (part != "near")
        {
            return Convert.ToHexString(hash.GetHashAndReset())[..24];
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
