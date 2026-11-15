using System.Security.Cryptography;
using System.Text;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The procedural goldens (B03): the digest of a block of 3 x 3 chunk columns (the 24 chunk digests of each column's layers, from y -64 to 320, hashed in order, the columns row by row) round a fixed
/// column of each of three seeds, which hold a mountain, a coast, a river, a lake, a forest and open plains (B02b moved the columns to where the tuned world has each, found by a search from the old ones), so the borders between chunks, the crowns that cross them and the shores are in it. A change to any pass, strategy, species, data row or noise moves some of them, which is what they are for: when a change is meant, read the
/// new digests from the failure and put them here. Like <see cref="SceneTests.GoldenDigest"/> they are digests of blocks, so they hold across machines.
/// </summary>
public sealed class ProceduralGoldenTests(ITestOutputHelper output)
{
    /// <summary>The golden columns: the seed, what is in the column, its chunk coordinates and its digest.</summary>
    public static TheoryData<ulong, string, int, int, string> Columns => new()
    {
        { 42, "river", 14, -25, "a6dd61912e501590e5461d0edc209efb5fca11aed4fea9bd62e57339a8f5bd97" },
        { 42, "lake", 31, -98, "7e78f16c6b5c2d1ac8f1ad7aa28f36932f6a6e8f816e3562d45660de1850c79e" },
        { 42, "coast", 31, 8, "852f9952cce857bef3f1eabc4cda54082e3b28b569bce77ef6a214b6843816ae" },
        { 42, "forest", -12, -76, "3579b301fe3ec3b2e3aeb66bceaa48f62198aec4ea93837c515e4b7bdf68672c" },
        { 42, "mountain", -28, -100, "6c85f7d39240f60a7df9b54a0d68db728bad07ba5898f2cf2d84dc390e1aa254" },
        { 42, "plain", 70, -30, "973640c8aada3c68c4bbeeb91c9f281f3cca8c957900a598491a8f11ccdad200" },
        { 7, "river", -8, 8, "efbe7e5e601d2b3959f060eef7a743090f312bfa3ee5e26f667fbdf9785111e1" },
        { 7, "lake", -126, -54, "1e3e93174b65695591fc735f76b38ad58f147428d9bacda497f91af9bd380100" },
        { 7, "coast", -11, -1, "eb5237ab22fe3c5532f3a72ec5f5c4ded5de070b6e3fd93ad431414f0703f803" },
        { 7, "forest", -45, -13, "91960c9b91150f914e3c05a6e3ef000c2f7cc4a90f385da38ec3cc33f0ad7e36" },
        { 7, "mountain", -184, -157, "b421a97aae36ab81144cb0083e551d08cde205167b32e7b15eedbf1ab418566f" },
        { 7, "plain", 43, -76, "a9ae0a22265eb14472653e3764314b2fc3e6ec39465af3e6ebe18c63e2577d23" },
        { 2026, "river", -7, 3, "410d1ab0dd7e109c7b3a47571188aaba6eb2fccb78ddf35c5bdfa4d6829b41af" },
        { 2026, "lake", 26, 44, "e7071b759bf94b8fd70df75f5c343cf9026dbe63c8a3693a88bf902da903309f" },
        { 2026, "coast", 3, 9, "8371c569ab51033bc78f2badb6fbd36abb321d68189187435bac3957d5bdcbef" },
        { 2026, "forest", 184, -88, "d2054bcdd0a56d3b4a61613ea1b69ee9d45711081423d0bc651e05018f9fdd79" },
        { 2026, "mountain", -21, 6, "56930ed4c4fa2febe8ddbc447aca0203a50c256f90a0bfb4a233fae05e34dd47" },
        { 2026, "plain", 7, 21, "879c895735d94fab38b4c2c7339e390413cd5398237ead89b6c43679463bf40e" },
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
        var digest = ColumnDigest(seed, chunkX, chunkZ);

        output.WriteLine($"{{ {seed}, \"{what}\", {chunkX}, {chunkZ}, \"{digest}\" }},");
        Assert.Equal(expected, digest);
    }
}
