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
        { 42, "river", -42, -32, "fbd0f1a650d8b5d8c38a48916e8075c411532a8e9e08eb042fcf0392edfc4133" },
        { 42, "lake", 0, -64, "7fa0a149d6cab462696a50218332d074a9a9ad4ffcde4b5914cb72140b07e697" },
        { 42, "coast", -20, 28, "ee3cceaf01c8edc43393d675bf675f1c0f0cf3e04ec015c5582879594df65999" },
        { 42, "forest", -40, 4, "7d288daeed6255210a336be16b32e9c36aa582302533a31ed4099d476a7d133e" },
        { 42, "mountain", -4, -4, "1d995860e83617b707acc4edb640f90bb4d0f83077c8a359bce61b4dd93b8040" },
        { 42, "plain", 12, 20, "612f8c2218fe4561d12cb4938691ba6881591ec57042109e3886a0dd0e312caa" },
        { 7, "river", -25, 24, "54cd759c4f618d391a4c12e3cabbf3137c830838d4a37428ed6eb5e0ca7bcd54" },
        { 7, "lake", 15, 75, "bc49afedafce8c4048807c023bd78cca05ed37de741b285d36441bcdb78d0e8c" },
        { 7, "coast", 28, 20, "b242351c3089db65b6a27b29be84ea839d3de30e79893b3a4d6d757124eaea5f" },
        { 7, "forest", -24, 8, "5daa82c61062c1e4c3e8c65d7f216f4ce9c4cef56ba72f2573d2c1ff2103c23b" },
        { 7, "mountain", 132, 156, "d6f28ca56ca03734cf2235dd993fd6d0c46dae8b86d9130e4a60a242d672875e" },
        { 7, "plain", 12, -20, "2e777c60496d22bf7144c9969c9480caac7330babaa08d9db9a7717e8dfe47c4" },
        { 2026, "river", -32, -46, "3ae91a647de9b37f73403aaeac019af86f61f6e0bccf78abf5181e193ec7963c" },
        { 2026, "lake", -45, 73, "6d1bbce77f7744293a4c572798236ae63cf58e9aba2d00120b39759ec985621b" },
        { 2026, "coast", -16, 20, "3555c8215f3ed13d0e7ca733bedc78b4ab6241192b148a5974edc36754eed0e7" },
        { 2026, "forest", -140, -188, "619d0756ffd647e7fd05f0ab5c96c0eb74a5846a8b937b99bfa09a07a6b09f93" },
        { 2026, "mountain", -20, -20, "f1fa4c3ffe7f631ff34b11b3b985cb2899373d277c27b217af4e3146357ef1c5" },
        { 2026, "plain", 4, 16, "fc91a979a7f48ed7e87af86e9c9e00b3e12cf858f1957fc279e7299db074035c" },
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
