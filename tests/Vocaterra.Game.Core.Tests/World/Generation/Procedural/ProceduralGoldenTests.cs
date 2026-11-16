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
        { 42, "river", 31, -190, "96089177810595cb0a289ccc584ce15cc16804924897db39bff293acabc1ddbc" },
        { 42, "lake", 108, -75, "19f2f848054b8ba490cf676af77f8ac0d84d74dd09d76d2db09733f4da14de50" },
        { 42, "coast", -1, 39, "0151842a965a6593a03a81b2799496cb32ae1707cd57ada7b34d68f6f32c32df" },
        { 42, "forest", 3, -91, "2fa40abdacd85dd7ed636a21525cac89c59fff45e7f2f35bb49d5875a0ac75fd" },
        { 42, "mountain", -29, -101, "9a41e3cfb3a57999b53b9f4c7554f3201a84a7df85460051d29d4ab3d769bbf7" },
        { 42, "plain", 70, -30, "dac8d6d87a2d3d89963f1ee987e6f7801401a32cc4254257193873cc461b695a" },
        { 7, "river", -26, 24, "e0e2e574c08c415f706d06a0172c0de0c4ee9680c62d6e46ac5626b8f3d935c7" },
        { 7, "lake", -124, -192, "2112c412e20c53725843cb7ae3df863db8146a0e4d7b1bf1f66e531440a79443" },
        { 7, "coast", 7, 21, "78b1d6afad6e5c24b3af050c3ef439b79f9535280f9c553da357892db237d150" },
        { 7, "forest", -45, -13, "17e77db9c586afa37afa4736e2b1ae769bdabc3d9f6ea07949b6cbe71627b000" },
        { 7, "mountain", -211, -184, "880adb8be8e848c7432f9ab604f46b78d0e3173e3899d95f412dc95613f498b0" },
        { 7, "plain", 43, -76, "e48ba299d0edcc8c6861267d13f3356ad11ff5c9b4ffe405ea952a4ce434d0da" },
        { 2026, "river", -13, 12, "1de2047d4df3c15b9ee51b1fb9dd35308ec7297fc272241b629698194831d6ba" },
        { 2026, "lake", 90, 109, "a53e193c218bbb3f010015ebb12390653625486c7cf825da363c2c13312e621c" },
        { 2026, "coast", -10, 22, "f6580fd173abdb420aea84e9e6c401f815603da2b3bcf12832080cbebe625f37" },
        { 2026, "forest", 285, 13, "2a1fc35515136d3254e3ca919c01df96f3a632648c375b57971fbf123f59aa75" },
        { 2026, "mountain", -4, -11, "0eb91c60ff3d096870c1171158814114a4847362f47b37d2cd6c1e63209342c0" },
        { 2026, "plain", 7, 21, "8eeecac0f6f558c2a476b83f369d234e3a75b88cc0954814a3e8f4a5f6363df1" },
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
