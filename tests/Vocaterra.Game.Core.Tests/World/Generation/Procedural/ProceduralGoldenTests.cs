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
        { 42, "river", -12, -8, "a220df49dc3269b2ee40e96e56151618b7daa6159815e88055bd528705db074e" },
        { 42, "lake", -100, -104, "0628299b1c2adb1015e755e2deac840d45d05f6afb43868673b7bfa7bff7ecd8" },
        { 42, "coast", 16, 24, "a72c09e0c8aec51960615470065abdad63dfdf5e7dc6fec90527b5cd511bc12a" },
        { 42, "forest", -52, -8, "c8dbdbd941267c07c2f127788c30906e98b4415c8d2ca2d0589eb7fba30458ff" },
        { 42, "mountain", -64, -64, "1971c49c0cbdd734617c1680d2199fbc92cf93b0426be11e9ce3ca1273b7078a" },
        { 42, "plain", 24, 16, "99c99777a31e57f4c1250381fe89270628ad476863558f8242320fba0cd4ef0d" },
        { 7, "river", -8, 8, "3fdf1983e60035002705fea06d0b1d4b05df568d0363ca5b7d3c9da7a1621ec2" },
        { 7, "lake", -32, 20, "1b385e7fbfd745ab3ca54b3a77ee5cf23ef4e54800ca4e3343c96492ae3e1561" },
        { 7, "coast", 4, -16, "bc182191b754b61067bd95fce8f86c771be65e8bd7be3b5f61ae7f85e6adf765" },
        { 7, "forest", -20, 12, "75ac755c500df3046293b3f0d2cfed063704a662ba36077213f20da9d65396a1" },
        { 7, "mountain", 12, 8, "1971d41dcc72afec7412fefe5ccee951410aa45386957f14f9d00e0e119699a2" },
        { 7, "plain", 12, -20, "de549f0b01a4dfe4032634001737d9796b4886cc67ff2e588f7323c9007072d1" },
        { 2026, "river", -4, -12, "d9921883f98f8b482a81f9cf4c53870c348559cd5e97148756f236675ac323ee" },
        { 2026, "lake", 16, 20, "e0749d6524e0d4dce57482820a8f4c3064e6e644264d5e5617bcc6494e4f3971" },
        { 2026, "coast", 12, 0, "a8958f400705d86018a87da3338c34b786b11cf7dd1120198398342c621ac164" },
        { 2026, "forest", 184, -88, "127020cd774516be662ae51a6ff560106a3315bfc8684ee0dc601f6aa7feabd2" },
        { 2026, "mountain", -24, 8, "26e6b5086c31c831fba079322cc4018751fd70a21f1fd9f842c4f26afba587e6" },
        { 2026, "plain", 16, 8, "936c589d49fa6689ef720370cf50c33ef57a281233585e5b238a57ecfd199d6c" },
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
