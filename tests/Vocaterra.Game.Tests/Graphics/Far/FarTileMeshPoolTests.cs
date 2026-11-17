using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Graphics.Far;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>A tile's arrays are rented from the shared pools (B07d, the rendering review's M3): the counts say how much of them is the tile's, the views show exactly that, and releasing them twice is harmless.</summary>
public sealed class FarTileMeshPoolTests
{
    private sealed class Flat : ILodSectionSource
    {
        public LodStack Stack(int level, int sx, int sz)
        {
            var section = LodSection.Air();
            for (var z = 0; z < 32; z++)
            {
                for (var x = 0; x < 32; x++)
                {
                    for (var y = 0; y < 10 + ((x + z) % 3); y++)
                    {
                        section.SetBlock(LodSection.Index(x, y, z), BlockId.Stone);
                    }
                }
            }

            return new LodStack(0, [section]);
        }
    }

    [Fact]
    public void Given_ABuiltTile_When_ItsViewsAreRead_Then_TheyShowExactlyTheCountsAndTheArraysMayBeLonger()
    {
        var mesh = FarTileMesher.Build(new Flat(), new FarViewSettings(4096), new FarTileKey(0, 1, 1));

        Assert.True(mesh.VertexCount > 100);
        Assert.Equal(mesh.VertexCount, mesh.Vertices.Count);
        Assert.True(mesh.VertexArray.Length >= mesh.VertexCount);
        Assert.NotNull(mesh.Indices16);
        Assert.Null(mesh.Indices32);
        Assert.Equal(mesh.IndexCount, mesh.Indices16.Value.Count);
        Assert.Equal(0, mesh.IndexCount % 6);
        Assert.Equal(mesh.IndexCount / 3, mesh.Triangles);
        Assert.Equal((mesh.VertexCount * 24L) + (mesh.IndexCount * 2L), mesh.Bytes);
        Assert.All(mesh.Vertices, v => Assert.NotEqual(Microsoft.Xna.Framework.ContainmentType.Disjoint, mesh.Box.Contains(v.Position)));
        for (var i = 0; i < mesh.IndexCount; i += 97)
        {
            Assert.InRange(mesh.IndexAt(i), 0, mesh.VertexCount - 1);
        }

        mesh.Release();
        mesh.Release(); // once, whatever the number of calls
    }

    [Fact]
    public void Given_TheSameTileBuiltAgainAfterTheFirstWasReleased_When_TheyAreCompared_Then_TheSecondIsTheSameAsTheFirstWas()
    {
        var first = FarTileMesher.Build(new Flat(), new FarViewSettings(4096), new FarTileKey(1, 0, 0));
        var positions = first.Vertices.Select(static v => v.Position).ToArray();
        var triangles = first.Triangles;
        first.Release();

        var again = FarTileMesher.Build(new Flat(), new FarViewSettings(4096), new FarTileKey(1, 0, 0)); // may be built in the very arrays the first gave back

        Assert.Equal(triangles, again.Triangles);
        Assert.Equal(positions, again.Vertices.Select(static v => v.Position).ToArray());
        again.Release();
    }
}
