using System.Security.Cryptography;
using System.Text;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The procedural goldens (B03): the digest of a block of 3 x 3 chunk columns (the 24 chunk digests of each column's layers, from y -64 to 320, hashed in order, the columns row by row) round a fixed
/// column of each of three seeds, which hold a mountain, a coast, a river, a lake, a forest and open plains, so the borders between chunks, the crowns that cross them and the shores are in it. A change to any pass, strategy, species, data row or noise moves some of them, which is what they are for: when a change is meant, read the
/// new digests from the failure and put them here. Like <see cref="SceneTests.GoldenDigest"/> they are digests of blocks, so they hold across machines.
/// </summary>
public sealed class ProceduralGoldenTests(ITestOutputHelper output)
{
    /// <summary>The golden columns: the seed, what is in the column, its chunk coordinates and its digest.</summary>
    public static TheoryData<ulong, string, int, int, string> Columns => new()
    {
        { 42, "river", -12, -8, "b5c3b7171e09fc02d74c0ad48d376bb5cf1e7df97392865359acb02ec4d0045c" },
        { 42, "lake", -100, -104, "9154ef8160d5b56250e99d00c5d9ffd2adbcbcdbebddc3e2795ab26fc816ee32" },
        { 42, "coast", 16, 24, "1f802b2c84c9fed62dc243acfa7242700795d915103219f3eae34fb054f2e630" },
        { 42, "forest", -52, -8, "9c0db7940385c17e0509dda3db25de489f96ff4f1ea0754d0f3461b2653fe5e0" },
        { 42, "mountain", -64, -64, "e39e8d573fb9a210a567902158d9c952cff67fd9e333467a37b2c2620de74627" },
        { 42, "plain", 24, 16, "9fef48527581254a7094f2de72e34055761ee88f1744bac0560546aee47c1fc3" },
        { 7, "river", -8, 8, "4cbfe49f5671ce61a0e8c070a507343f1fa71b2fb3278b84cca3f441d48e13ba" },
        { 7, "lake", -32, 20, "5a553da3a43a6854a0c41458ee55791a83bc55e75326298ee0d2eda74325abe5" },
        { 7, "coast", 4, -16, "c1b92a8c55deeb65f858e038e0f79fa05c8af3676f6b830193a17858284d88e0" },
        { 7, "forest", -20, 12, "0a3332af8842d7d15f90bbd92e5bc905dff6d1b5c1e66d7d919738d9eae8a70d" },
        { 7, "mountain", 12, 8, "1832c934631da60a181a4d05764a20a69ce32a7bc64760b146f96e6a45f6835a" },
        { 7, "plain", 12, -20, "bc12068b5430c2ca0b91c1c969fe0a7a595466e2c08cfaba9dc2fda147cc3e4d" },
        { 2026, "river", -4, -12, "3aa7aade4634e6979546f4b6de2f42ac69239998476c5e35e958a6da100ca773" },
        { 2026, "lake", 16, 20, "ead6457762d57d9e894a23b897d1e105c7fce61f4154f41a4f2b9ebe2ee3940d" },
        { 2026, "coast", 12, 0, "df6bdf0034e40c86ce41c5b216f79eb0fa1e864d4575bf57ea4130b8b74efe0a" },
        { 2026, "forest", 184, -88, "5074693b3829e18aa32f84a9642859d3f78af43160411d4a905c24fb42c780ba" },
        { 2026, "mountain", -24, 8, "07eb1d55c838c780567cb5932e8db5350e33a4f9dbe995fad2fb710f92889edc" },
        { 2026, "plain", 16, 8, "7b1749e2a145bfa0037d15e9cfe1a37c181a67cc9e33d2980ca4b38b94c6a5e6" },
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
