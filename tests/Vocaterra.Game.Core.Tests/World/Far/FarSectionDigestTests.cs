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
        ["free2026 coast far"] = "86AB852E28279163FF155E5B",
        ["free2026 coast l3"] = "D029976930905EBC57A1BE87",
        ["free2026 coast near"] = "2A1621FA08153AC9444418FE",
        ["free2026 forest far"] = "B82FCD96B04584D858FF0E88",
        ["free2026 forest l3"] = "0CB51A7A064D7DE898BD149E",
        ["free2026 forest near"] = "E0349474A6CAAD279A1D9833",
        ["free2026 mountain far"] = "A629A0B216A156C17D4F1CE2",
        ["free2026 mountain l3"] = "5D0A254DDA9AFD1C7BD6A130",
        ["free2026 mountain near"] = "B87125A0A5FC621381037946",
        ["free2026 river far"] = "15C6D5AA6D1A57E242E5E69D",
        ["free2026 river l3"] = "5E2315A36E72F29BBE49663E",
        ["free2026 river near"] = "9807358AC55D18C154D0422E",
        ["free2026 sea far"] = "86AB852E28279163FF155E5B",
        ["free2026 sea l3"] = "319AC7233E75E3B8C6EA8A29",
        ["free2026 sea near"] = "92277DD3163CC5ECD71AF59F",
        ["free42 coast far"] = "F3CF20092425E27DB7CFA416",
        ["free42 coast l3"] = "6A95FB788EFA9F1CD75794FB",
        ["free42 coast near"] = "41D91AB7793BFB93F7B65D40",
        ["free42 forest far"] = "F595996A6A7C72FFE79B34B5",
        ["free42 forest l3"] = "C78F01A8E7ACEA19A1143030",
        ["free42 forest near"] = "56006104E3985E3EF079CBD5",
        ["free42 mountain far"] = "E9DBE9A482AB232BCDD38D45",
        ["free42 mountain l3"] = "63A22D1519FB082AB5FD833F",
        ["free42 mountain near"] = "BB158D4E534C4166FF37C987",
        ["free42 river far"] = "4CB538737E7BF728EA573CD0",
        ["free42 river l3"] = "4EE14D6F344E10916F5A00C1",
        ["free42 river near"] = "CDB1D5B26AFA41651BB0717C",
        ["free42 sea far"] = "F3CF20092425E27DB7CFA416",
        ["free42 sea l3"] = "2142B111ED249A4811DED44D",
        ["free42 sea near"] = "C8F5E35FA40E96E2BD7BE02E",
        ["valley42 edge far"] = "E8E412FB9FC50BE70B027F8C",
        ["valley42 edge l3"] = "244CBEF80B71401E03460E90",
        ["valley42 edge near"] = "AA0514AADD199A3EB75E8C74",
        ["valley42 ridge far"] = "C0F22AB9363D57254A2BFF43",
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
