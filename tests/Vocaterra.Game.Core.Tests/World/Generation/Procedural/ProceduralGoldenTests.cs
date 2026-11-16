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
        { 42, "river", 31, -190, "4a4633e4cf36433df4440530f82859839b73fa04a678bf8877d6ac026f3d5494" },
        { 42, "lake", 108, -75, "5391c469fbdebff05ce1bceae89409c0d517447660d854b41e0c0aec0f2f7ab0" },
        { 42, "coast", -1, 39, "e36bc6c063645e64534c07802d5e2109a7dbc568b335dc2455941f5ea7e69f5e" },
        { 42, "forest", 3, -91, "10d83a112c24235fafdb4e10797ec4d34566c0a4ddc60a3796e0167421901f20" },
        { 42, "mountain", -29, -101, "035c24d7911d3a11aef3bb4394f110f4baa31d34e2225cffdad4bbd5b5e4ced5" },
        { 42, "plain", 70, -30, "b5bbbe19330afed39cea52f3e64e7ff727eb297d85a6e5a08b1bc89295e7a176" },
        { 7, "river", -26, 24, "a38f784049db04bffd84a321c3a96edc0fa82c148925965be0ebb49b3a9b970e" },
        { 7, "lake", -124, -192, "94a93b2a01edbecd5f9609c06a60552756b224b40665f31d47c6d58345af4287" },
        { 7, "coast", 7, 21, "30b7ff8f1cbb58490e2fd1764d8e75d2273c50229cd057a51f2684ca1ff0ded8" },
        { 7, "forest", -45, -13, "907e0496eebcea91312cee4c57c043c4ec0329ef7779b080f68241bc43eb79df" },
        { 7, "mountain", -211, -184, "a2d48d9aa3dfcf39c871b0a63eaeff39c1457a80740cd48967f02847f4ce9952" },
        { 7, "plain", 43, -76, "8dad54f5ecb9e538bbc364a6e1ef382fb52f8c17a4bc52888d5b4a0887aaf699" },
        { 2026, "river", -13, 12, "05ca244734bf57cae54e8081e1df79b2002627b9f0023837a0cbc8a7a02174fa" },
        { 2026, "lake", 90, 109, "53de0dfd8b7da846739df680c84f1b643646cbcfe4cee6dcd8fdc88313706eea" },
        { 2026, "coast", -10, 22, "e2b100aa6a6f7fd932c2c262d47cb2a34336be2a176b7dc0a0cbeea6f06322fb" },
        { 2026, "forest", 285, 13, "ebabfd3bcb66df52da9f6c61aa6783c0fcf39be3f186f15f97138e10e00cbec1" },
        { 2026, "mountain", -4, -11, "687a7d64c31ffc5526c99eb542ab35c748effc71474ead1ed678d503d46d5c7f" },
        { 2026, "plain", 7, 21, "86b86f13c705386516f4226a6cc68d0009fc8fd3e28b942c84aac4035b8d605b" },
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
