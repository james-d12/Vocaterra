using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Graphics.Far;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>Which meshes go to the card in a frame (B07c): the coarse levels first, within a byte budget, so a settled view has its horizon.</summary>
public sealed class FarUploadsTests
{
    private static FarTile Tile(int level, int x, int vertices, bool uploaded = false)
    {
        var mesh = new FarTileMesh(new FarTileKey(level, x, 0), new VertexPositionColorTexture[vertices], vertices, [0, 1, 2], null, 3, 3, 0, default(BoundingBox), 1);
        return new FarTile(mesh.Key) { Pending = mesh, Buffer = uploaded ? (VertexBuffer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(VertexBuffer)) : null };
    }

    [Fact]
    public void Given_ManyFineTilesNearestAndFewCoarseOnes_When_AFrameChoosesWhatToUpload_Then_TheCoarseLevelsComeFirstAndTheBudgetCutsTheRest()
    {
        var draw = new List<FarTile>();
        for (var i = 0; i < 40; i++)
        {
            draw.Add(Tile(0, i, 1000));
        }

        draw.Add(Tile(7, 0, 100));
        draw.Add(Tile(6, 0, 100));
        draw.Add(Tile(5, 3, 100));
        var chosen = new List<FarTile>();

        FarUploads.Choose(draw, 7, 2L * 1000 * 36, chosen);

        Assert.Equal([7, 6, 5], chosen.Take(3).Select(static t => t.Key.Level));
        Assert.True(chosen.Count is > 3 and < 10, $"{chosen.Count} chosen");
        Assert.Equal(0, chosen[3].Key.Tx); // then the nearest first of the fine ones (the list's order)
    }

    [Fact]
    public void Given_ATinyBudget_When_AFrameChoosesWhatToUpload_Then_AtLeastOneIsChosenAndNewTilesGoBeforeRemadeOnes()
    {
        var draw = new List<FarTile> { Tile(0, 0, 1000, uploaded: true), Tile(0, 1, 1000) };
        var chosen = new List<FarTile>();

        FarUploads.Choose(draw, 7, 1, chosen);

        Assert.Single(chosen);
        Assert.Equal(1, chosen[0].Key.Tx);
    }

    [Fact]
    public void Given_NothingPending_When_AFrameChoosesWhatToUpload_Then_NothingIsChosen()
    {
        var tile = Tile(0, 0, 10);
        tile.Pending = null;
        var chosen = new List<FarTile> { tile };

        FarUploads.Choose([tile], 7, 1_000_000, chosen);

        Assert.Empty(chosen);
    }
}
