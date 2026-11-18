using System.Security.Cryptography;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>
/// B17c: the fallback's colours are today's. With the default mesh options (light baked into the vertex colours, the bands, the glow mesh) the meshes are what B17b's base commit built: a hash of every vertex of every mesh of a few
/// scenes in every band, recorded from that commit before the light moved into the shader, must not move.
/// </summary>
public sealed class BakedLightGoldenTests
{
    private static MapBlocks Scene() => new MapBlocks().Fill(0, 4, 0, 15, 5, 15, BlockId.Grass)
        .Set(8, 6, 8, BlockId.Fence).Set(3, 6, 3, BlockId.Bush).Set(5, 6, 5, BlockId.TallGrass).Set(10, 6, 4, BlockId.Water)
        .Set(6, 6, 6, BlockId.Lantern).Set(9, 6, 9, BlockId.PostTorch).Set(12, 6, 12, BlockId.Bed).Set(12, 6, 13, BlockId.BedFoot).Set(2, 6, 12, BlockId.Door);

    private static MapBlocks Cave() => new MapBlocks().Fill(0, 0, 0, 15, 9, 15, BlockId.Stone).Fill(3, 1, 3, 12, 3, 12, BlockId.Air).Set(6, 1, 6, BlockId.Lava);

    private static readonly BlockPos[] Lights = [new BlockPos(6, 6, 6), new BlockPos(9, 6, 9)];

    private static string Hash(ChunkMeshes meshes)
    {
        using var sha = SHA256.Create();
        foreach (var mesh in new[] { meshes.Opaque, meshes.Alpha, meshes.PlantMesh, meshes.LitMesh, meshes.GlowMesh })
        {
            foreach (var v in mesh.Vertices)
            {
                sha.TransformBlock(BitConverter.GetBytes(v.X), 0, 4, null, 0);
                sha.TransformBlock(BitConverter.GetBytes(v.Y), 0, 4, null, 0);
                sha.TransformBlock(BitConverter.GetBytes(v.Z), 0, 4, null, 0);
                sha.TransformBlock(BitConverter.GetBytes(v.Color), 0, 4, null, 0);
                sha.TransformBlock(BitConverter.GetBytes(v.U), 0, 4, null, 0);
                sha.TransformBlock(BitConverter.GetBytes(v.V), 0, 4, null, 0);
            }

            sha.TransformBlock([0xFF], 0, 1, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!)[..16];
    }

    [Fact]
    public void Given_TheDefaultOptions_When_EveryBandIsBuilt_Then_TheMeshesAreTheOnesOfBeforeB17c()
    {
        var actual = new List<string>();
        foreach (var band in Enum.GetValues<LightBand>())
        {
            actual.Add($"{band}: {Hash(ChunkMesher.Build(Scene(), 0, 0, 0, 0, new MeshOptions(true, 100, Lights, band)))}");
        }

        actual.Add($"Cave: {Hash(ChunkMesher.Build(Cave(), 0, 0, 0, 0, new MeshOptions(true, 100, null, LightBand.Day, new SkySurface(0, 0, (_, _) => 9))))}");
        actual.Add($"NoAo: {Hash(ChunkMesher.Build(Scene(), 0, 0, 0, 0, new MeshOptions(false, 60, Lights, LightBand.Dusk)))}");
        Assert.Equal(Expected, actual);
    }

    // Re-recorded by I08a: the hash covers each vertex's V, which is the atlas row over the atlas height, and the gear icons grew the colour rows from 15 to 17 (the atlas from 480 to 544 texels high). V is the one input of the hash that depends on the atlas size; the positions, the colours and U do not.
    private static readonly string[] Expected =
    [
        "Day: 508C231F6D1F1A89",
        "Dusk: B281C39A13E24090",
        "Night: 742E2F318BA03E93",
        "Dawn: 4BECE701FA81C848",
        "Cave: DEFD34D52C4366A9",
        "NoAo: B281C39A13E24090",
    ];
}
