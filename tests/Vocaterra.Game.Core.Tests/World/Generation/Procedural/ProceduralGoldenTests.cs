using System.Security.Cryptography;
using System.Text;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The procedural goldens (B03): the digest of a block of 3 x 3 chunk columns (the 24 chunk digests of each column's layers, from y -64 to 320, hashed in order, the columns row by row) round a fixed
/// column of each of three seeds, which hold a mountain, a coast, a river, a lake, a forest and open plains (B02b moved the columns to where the tuned world has each, found by a search from the old ones; B02c moved the river and lake columns to where the drainage network has them, `ProceduralTestWorld.Find`'s first), so the borders between chunks, the crowns that cross them and the shores are in it. A change to any pass, strategy, species, data row or noise moves some of them, which is what they are for: when a change is meant, read the
/// new digests from the failure and put them here. Like <see cref="SceneTests.GoldenDigest"/> they are digests of blocks, so they hold across machines.
/// </summary>
public sealed class ProceduralGoldenTests(ITestOutputHelper output)
{
    /// <summary>The golden columns: the seed, what is in the column, its chunk coordinates and its digest.</summary>
    public static TheoryData<ulong, string, int, int, string> Columns => new()
    {
        { 42, "river", 39, -15, "717e9bfd759fa71a686e301ee962851816171cf7b58bb7e4709c28c17a514b79" },
        { 42, "lake", 9, -60, "ea3bca99f0867739e8ebb25b06e476219a054cc2f60451b438e29401005665ea" },
        { 42, "coast", -20, 28, "9fea60e233fb12a0682a19663e634ddb72168ea9fa3904d431c78b05e717e3cc" },
        { 42, "forest", -40, 4, "fa0905c17e1a118c2e4de367b6777148e0e8de7a676e594f3cdeb4e6d758eebc" },
        { 42, "mountain", -4, -4, "a33acb45df9d70b016c10923101b5869cbca6d1aeb7c4910e1bd10f283286fe5" },
        { 42, "plain", 12, 20, "a11d8ba92df48eb99756a4dc6aae90befb698cdfd997535d40ffb2d70143799e" },
        { 7, "river", -20, -28, "9e371ea880d2914ad74a66688db36294aee0637cbdd32e955badce5147d542d3" },
        { 7, "lake", 12, 75, "98a5d067cc94a7ccc495953efb6e3e1869311f98941e9f45ea257daf6313d5c1" },
        { 7, "coast", 28, 20, "95f36f9b6c6b43d4304d349b79fe3c02a6dba8cb133264db7a3974f189b7aef3" },
        { 7, "forest", -24, 8, "d60a456f7dff6f2c80dd6e9e77e651858f533d691fa956af3ce5af201396a8b9" },
        { 7, "mountain", 132, 156, "2fbc577c23ec7f71f5375e89007aa888b27ecca52e0b9a1078f7dd418a17e9c1" },
        { 7, "plain", 12, -20, "d306b12f7e60db4563ec0daa48fedc1c7881b32708c5187b93a5c3dc720180d1" },
        { 2026, "river", -41, 45, "dcb3e8585571070fc5656774879d2386b2668ca28e24e22a9eafb554c77c8982" },
        { 2026, "lake", -38, 67, "f9172514cbb2819a0072b5cb1d3b0394929ad32cf88b80c8c44d8dc5b15f2202" },
        { 2026, "coast", -16, 20, "8511ccd5d3f695c524fa4ec55475873c922763af4764187e5a9ed85935cb429b" },
        { 2026, "forest", -140, -188, "3839391fa6fe36b65ebdc16594420db3d79cf904d5472d70a1ab0110d3ad0c42" },
        { 2026, "mountain", -20, -20, "d6cfc3b2b19861c918bd388fbb3c0ddb244b817ed1d9f2139ae8daddf8574968" },
        { 2026, "plain", 4, 16, "4e1b9d2cd7f919ec7d2fe572ee9d446e4f06c68dc7aaf2adc844b0f3808466ee" },
    };

    /// <summary>The digest of the 3 x 3 columns round a column: the chunk digests of their layers, bottom to top, row by row, hashed together.</summary>
    internal static string ColumnDigest(ulong seed, int chunkX, int chunkZ)
    {
        var builder = new StringBuilder();
        for (var z = chunkZ - 1; z <= chunkZ + 1; z++)
        {
            for (var x = chunkX - 1; x <= chunkX + 1; x++)
            {
                foreach (var chunk in ProceduralTestWorld.Source(seed).GenerateColumn(x, z))
                {
                    builder.Append(chunk.ComputeDigestHex());
                }
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    [Theory]
    [MemberData(nameof(Columns))]
    public void Given_AGoldenColumn_When_ItIsGenerated_Then_ItsDigestIsTheRecordedOne(ulong seed, string what, int chunkX, int chunkZ, string expected)
    {
        if (what is "river" or "lake")
        {
            // B02c: the column holds what its name says: a river's or a lake's carve is in the middle chunk column.
            var climate = new ClimateSampler(TestGame.Data.WorldGen, seed);
            var model = new HeightModel(TestGame.Data.WorldGen, seed);
            var carved = false;
            for (var z = chunkZ * 16; z < (chunkZ * 16) + 16 && !carved; z++)
            {
                for (var x = chunkX * 16; x < (chunkX * 16) + 16 && !carved; x++)
                {
                    var shape = model.Shape(x, z, climate.At(x, z));
                    carved = shape.Water && (what == "river" ? shape.RiverWeight : shape.LakeWeight) > 2000;
                }
            }

            Assert.True(carved, $"the {what} column ({chunkX}, {chunkZ}) of seed {seed} holds no {what}");
        }

        var digest = ColumnDigest(seed, chunkX, chunkZ);

        output.WriteLine($"{{ {seed}, \"{what}\", {chunkX}, {chunkZ}, \"{digest}\" }},");
        Assert.Equal(expected, digest);
    }
}
