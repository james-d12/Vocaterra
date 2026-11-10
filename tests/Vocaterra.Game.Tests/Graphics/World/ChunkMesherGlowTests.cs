using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>The lanterns' glow as its own mesh (E02a): the base meshes bake none of it, the glow mesh holds the very quads of the faces a lantern reaches, and no band changes it.</summary>
public sealed class ChunkMesherGlowTests
{
    private static ChunkMeshes Build(MapBlocks blocks, LightBand band, IReadOnlyList<BlockPos>? lights, int chunkX = 0) =>
        ChunkMesher.Build(blocks, chunkX, 0, 0, 0, new MeshOptions(true, 100, lights, band));

    // Every kind of mesh in one scene, lanterns among them: a grass floor, a fence, a bush, undergrowth, water, a lantern and a torch.
    private static MapBlocks Scene() => new MapBlocks().Fill(0, 4, 0, 15, 5, 15, BlockId.Grass)
        .Set(8, 6, 8, BlockId.Fence).Set(3, 6, 3, BlockId.Bush).Set(5, 6, 5, BlockId.TallGrass).Set(10, 6, 4, BlockId.Water)
        .Set(6, 6, 6, BlockId.Lantern).Set(9, 6, 9, BlockId.PostTorch);

    private static readonly BlockPos[] SceneLights = [new BlockPos(6, 6, 6), new BlockPos(9, 6, 9)];

    private static (MeshVertex, MeshVertex, MeshVertex, MeshVertex)[] Quads(MeshBuffer mesh)
    {
        var v = mesh.Vertices.ToArray();
        return [.. Enumerable.Range(0, v.Length / 4).Select(i => (v[i * 4], v[(i * 4) + 1], v[(i * 4) + 2], v[(i * 4) + 3]))];
    }

    private static double Distance(MeshVertex v, BlockPos light) => Math.Sqrt(Math.Pow(v.X - (light.X + 0.5), 2) + Math.Pow(v.Y - (light.Y + 0.5), 2) + Math.Pow(v.Z - (light.Z + 0.5), 2));

    [Fact]
    public void Given_LanternsAndAnyBand_When_TheBaseMeshesAreBuilt_Then_TheyAreTheMeshesWithNoLantern()
    {
        foreach (var band in Enum.GetValues<LightBand>())
        {
            var without = Build(Scene(), band, null);
            var with = Build(Scene(), band, SceneLights);

            Assert.Equal(without.Opaque.Vertices.ToArray(), with.Opaque.Vertices.ToArray());
            Assert.Equal(without.Alpha.Vertices.ToArray(), with.Alpha.Vertices.ToArray());
            Assert.Equal(without.PlantMesh.Vertices.ToArray(), with.PlantMesh.Vertices.ToArray());
            Assert.Equal(without.LitMesh.Vertices.ToArray(), with.LitMesh.Vertices.ToArray());
            Assert.Equal(0, without.GlowMesh.FaceCount);
            Assert.NotEqual(0, with.GlowMesh.FaceCount);
        }
    }

    [Fact]
    public void Given_ALantern_When_TheGlowMeshIsBuilt_Then_ItHasAQuadForEveryFaceInReachAndNoneBeyondTheRadius()
    {
        // A floor of stone two chunks wide and a lantern at (8, 6, 8): the faces of the first chunk are near it, the far half of the second is beyond ChunkMesher.GlowRadius (9 blocks).
        var floor = new MapBlocks().Fill(0, 4, 0, 31, 5, 15, BlockId.Stone);
        var lights = new[] { new BlockPos(8, 6, 8) };

        foreach (var chunk in new[] { 0, 1 })
        {
            var meshes = Build(floor, LightBand.Day, lights, chunk);
            bool Within(MeshVertex v, double radius) => Distance(v, lights[0]) < radius;
            var all = Quads(meshes.Opaque).Select(static q => new[] { q.Item1, q.Item2, q.Item3, q.Item4 }).ToArray();
            var surely = all.Count(q => q.Any(v => Within(v, 0.9 * ChunkMesher.GlowRadius))); // a corner this near has a glow of at least a level of 255
            var maybe = all.Count(q => q.Any(v => Within(v, ChunkMesher.GlowRadius))); // the very edge of the radius rounds to none, so a face there may have no quad
            var beyond = all.Count(q => q.All(v => !Within(v, ChunkMesher.GlowRadius)));

            Assert.InRange(meshes.GlowMesh.FaceCount, surely, maybe);
            Assert.True(beyond > 0 || chunk == 0, "the second chunk has faces beyond the radius");
            foreach (var (a, b, c, d) in Quads(meshes.GlowMesh))
            {
                var near = new[] { a, b, c, d }.Where(v => Distance(v, lights[0]) < ChunkMesher.GlowRadius).ToArray();
                Assert.NotEmpty(near); // no quad lies wholly beyond the radius
                Assert.All(near.Where(v => Within(v, 0.9 * ChunkMesher.GlowRadius)), static v => Assert.NotEqual(LanternGlow.None, v.Color)); // and every corner well inside it has some glow
                Assert.All(new[] { a, b, c, d }.Where(v => Distance(v, lights[0]) >= ChunkMesher.GlowRadius), static v => Assert.Equal(LanternGlow.None, v.Color));
            }
        }
    }

    [Fact]
    public void Given_ALantern_When_TheGlowMeshIsBuilt_Then_EveryQuadIsAQuadOfTheOpaqueMeshWithTheSamePositionsOrderAndPicture()
    {
        // The same positions in the same order are what keeps the glow pass at the very depth of the face it lights (no z-fighting): only the colour differs.
        var meshes = Build(Scene(), LightBand.Dusk, SceneLights);
        var opaque = Quads(meshes.Opaque).Select(static q => (Key(q.Item1), Key(q.Item2), Key(q.Item3), Key(q.Item4))).ToHashSet();
        static (float, float, float, float, float) Key(MeshVertex v) => (v.X, v.Y, v.Z, v.U, v.V);

        Assert.NotEqual(0, meshes.GlowMesh.FaceCount);
        foreach (var (a, b, c, d) in Quads(meshes.GlowMesh))
        {
            Assert.Contains((Key(a), Key(b), Key(c), Key(d)), opaque);
        }
    }

    [Fact]
    public void Given_TheBands_When_TheGlowMeshIsBuilt_Then_ItIsTheSameInEveryBand()
    {
        var day = Build(Scene(), LightBand.Day, SceneLights).GlowMesh.Vertices.ToArray();

        Assert.NotEmpty(day);
        Assert.Equal(day, Build(Scene(), LightBand.Dusk, SceneLights).GlowMesh.Vertices.ToArray());
        Assert.Equal(day, Build(Scene(), LightBand.Night, SceneLights).GlowMesh.Vertices.ToArray());
    }

    [Fact]
    public void Given_ALeafBlockBesideALantern_When_TheGlowMeshIsBuilt_Then_ItsQuadsTakeTheLeafPictureSoTheCutOutPixelsGlowNot()
    {
        // The glow quad carries the face's texture coordinates, and the glow pass uses the opaque pass's alpha test: where the leaf's picture is clear, nothing is drawn or lit.
        var leaves = new MapBlocks().Set(5, 5, 5, BlockId.Leaves);
        var plain = Build(leaves, LightBand.Day, null);
        var lit = Build(leaves, LightBand.Day, [new BlockPos(5, 6, 5)]);

        Assert.Equal(6, lit.GlowMesh.FaceCount);
        Assert.Equal(plain.Opaque.Vertices.ToArray().Select(static v => (v.U, v.V)).OrderBy(static t => t.U).ThenBy(static t => t.V), lit.GlowMesh.Vertices.ToArray().Select(static v => (v.U, v.V)).OrderBy(static t => t.U).ThenBy(static t => t.V));
    }

    [Fact]
    public void Given_ALanternBesideAFace_When_TheGlowIsRead_Then_ItIsBrighterNextToTheLanternAndOnFacesThatLookAtIt()
    {
        var floor = new MapBlocks().Fill(0, 4, 0, 15, 5, 15, BlockId.Stone);
        var glow = Build(floor, LightBand.Night, [new BlockPos(8, 6, 8)]).GlowMesh.Vertices.ToArray();
        var level = static (MeshVertex v) => (int)(v.Color & 0xFF);
        var under = glow.Where(static v => v.Y == 6f && v.X is >= 8f and <= 9f && v.Z is >= 8f and <= 9f).Max(level);
        var away = glow.Where(static v => v.Y == 6f && (v.X >= 15f || v.Z >= 15f)).Max(level);

        Assert.True(under > away, $"under the lantern {under}, away {away}");
        Assert.InRange(under, 200, 255); // next to the lantern the glow is nearly at full reach
    }
}
