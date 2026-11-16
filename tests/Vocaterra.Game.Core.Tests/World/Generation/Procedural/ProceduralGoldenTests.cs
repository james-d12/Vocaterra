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
        { 42, "river", 39, -15, "b6964be06428c7ac68ed1f837d224336f77b72177e1b9b9467f3302f1a277fd0" },
        { 42, "lake", 9, -60, "fe7f12d0eb4dc83841651fc7b33c3063846c85539aa0c027164978d890e9edbc" },
        { 42, "coast", -20, 28, "ee3cceaf01c8edc43393d675bf675f1c0f0cf3e04ec015c5582879594df65999" },
        { 42, "forest", -40, 4, "7d288daeed6255210a336be16b32e9c36aa582302533a31ed4099d476a7d133e" },
        { 42, "mountain", -4, -4, "7ff31d7ae291b9e2b46b6368fc6758accfe71185384c88160081d7c4fb92232b" },
        { 42, "plain", 12, 20, "612f8c2218fe4561d12cb4938691ba6881591ec57042109e3886a0dd0e312caa" },
        { 7, "river", -20, -28, "9d886e562e1dbbe03cfd5803f467d8079967ef257cc098b1caea26851447c61c" },
        { 7, "lake", 12, 75, "adbe124e1427b3d3675f38731b6d3fb5524722d2e00500b6e19023bbbad4565b" },
        { 7, "coast", 28, 20, "7bf5ac5ac709c60d6155711c15fdf0b4e351698045ede72363ad57fc1980397f" },
        { 7, "forest", -24, 8, "5daa82c61062c1e4c3e8c65d7f216f4ce9c4cef56ba72f2573d2c1ff2103c23b" },
        { 7, "mountain", 132, 156, "d6f28ca56ca03734cf2235dd993fd6d0c46dae8b86d9130e4a60a242d672875e" },
        { 7, "plain", 12, -20, "2e777c60496d22bf7144c9969c9480caac7330babaa08d9db9a7717e8dfe47c4" },
        { 2026, "river", -41, 45, "14cdbb3facc712e7fab4779d15ad66d6a39aab268f74ad784571114e9b6ceeb7" },
        { 2026, "lake", -38, 67, "2e0c045b0c82937811542f528d4725dbb5c0d017cac2ad1d363bc0790bde64d5" },
        { 2026, "coast", -16, 20, "3aee326b1205f8da8f48d1817e533a7cf0118148fd41c69ae4657a982f0ae84a" },
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
