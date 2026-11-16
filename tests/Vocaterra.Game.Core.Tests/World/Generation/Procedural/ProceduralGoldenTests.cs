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
        { 42, "river", -42, -32, "9d6e15c20f72be24c6031d9d43efb769f1e9060ddc42b0ed27017f6d9375ec20" },
        { 42, "lake", 0, -64, "36efe305082e2ee4985597c27d208d36211a10a13378c6289a2c1eeb3abadd0e" },
        { 42, "coast", -20, 28, "9fea60e233fb12a0682a19663e634ddb72168ea9fa3904d431c78b05e717e3cc" },
        { 42, "forest", -40, 4, "9dc495849888e2505584cb88ffa524200bfc35718d45d280ccc9eb6eb1955c56" },
        { 42, "mountain", -4, -4, "55d4ea8221afa770b04d68084b9b422561d7e0df1736d9d5a6f86865ebc406be" },
        { 42, "plain", 12, 20, "a11d8ba92df48eb99756a4dc6aae90befb698cdfd997535d40ffb2d70143799e" },
        { 7, "river", -25, 24, "290f0e2f3dd2db8502f52f0b02c5a49e1ff9bc5eebc1d63e0933ca58d9aab50e" },
        { 7, "lake", 15, 75, "ebb89c0e41164e94e9d99b8c3b89654ee223041f75526478514cd0b2b2b9b0e6" },
        { 7, "coast", 28, 20, "9a0be2c8aa7043e5efd92ebd73f4f492a1be3a49b2260a4f54b040377458d29f" },
        { 7, "forest", -24, 8, "d60a456f7dff6f2c80dd6e9e77e651858f533d691fa956af3ce5af201396a8b9" },
        { 7, "mountain", 132, 156, "2fbc577c23ec7f71f5375e89007aa888b27ecca52e0b9a1078f7dd418a17e9c1" },
        { 7, "plain", 12, -20, "d306b12f7e60db4563ec0daa48fedc1c7881b32708c5187b93a5c3dc720180d1" },
        { 2026, "river", -32, -46, "e294e764a0d487355dfe2354d2a44ab0d415fb9ad1fd62273be6965b42a954c8" },
        { 2026, "lake", -45, 73, "dc8a693fc36170dcb1a26ad4747da38ab258f7b74e9343b5b3639be9caabd699" },
        { 2026, "coast", -16, 20, "c83916e0c4434b3d6c1ef85c70ca70c9e1e73b191dcd0a6ff9f026adea43fd01" },
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
        var digest = ColumnDigest(seed, chunkX, chunkZ);

        output.WriteLine($"{{ {seed}, \"{what}\", {chunkX}, {chunkZ}, \"{digest}\" }},");
        Assert.Equal(expected, digest);
    }
}
