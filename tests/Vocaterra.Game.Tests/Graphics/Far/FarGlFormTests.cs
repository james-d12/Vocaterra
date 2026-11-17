using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Graphics.Gl;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>B17c: a far tile on the card in the game's own program's form counts as on the card as a tile in MonoGame's form does: it is a tile made again, not a new one, and it is built.</summary>
public sealed class FarGlFormTests
{
    [Fact]
    public void Given_ATileWithBuffersInTheProgramsForm_When_UploadsAreChosen_Then_ItIsMadeAgainAfterTheNewTilesAndCountsAsBuilt()
    {
        var mesh = new FarTileMesh(new FarTileKey(0, 0, 0), new VertexPositionColorTexture[4], [0, 1, 2], null, 3, 0, default(BoundingBox), 1);
        var replaced = new FarTile(mesh.Key) { Pending = mesh, GlBuffers = (GlFarBuffers)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(GlFarBuffers)), OnCard = true };
        var fresh = new FarTile(new FarTileKey(0, 1, 0)) { Pending = mesh with { Key = new FarTileKey(0, 1, 0) } };
        Assert.True(replaced.HasBuffers);
        Assert.True(replaced.IsBuilt);
        Assert.False(fresh.HasBuffers);
        var chosen = new List<FarTile>();

        FarUploads.Choose([replaced, fresh], 7, 1_000_000, chosen);

        Assert.Equal([fresh, replaced], chosen); // new tiles before tiles made again, as for the effects' form
    }
}
