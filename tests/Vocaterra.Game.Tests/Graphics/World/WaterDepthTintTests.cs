using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Water;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>B22, G6: the water's surface is tinted by depth per vertex, light and clear over the shallows and the shore, darker and bluer in deep water.</summary>
public sealed class WaterDepthTintTests
{
    [Fact]
    public void Given_ACornerAtTheShore_When_ItsTintIsRead_Then_ItIsWhiteAndAtTheDeepEndItIsTheDeepMultiplier()
    {
        Assert.Equal((1f, 1f, 1f), WaterDepthTint.At(0f));
        Assert.Equal((1f, 1f, 1f), WaterDepthTint.At(WaterDepthTint.ShoreDepth));
        Assert.Equal(WaterDepthTint.Deep, WaterDepthTint.At(WaterDepthTint.DeepDepth));
        Assert.Equal(WaterDepthTint.Deep, WaterDepthTint.At(100f));
    }

    [Fact]
    public void Given_RisingDepths_When_TheTintIsRead_Then_EveryChannelFallsAndBlueKeepsMost()
    {
        var last = WaterDepthTint.At(0f);
        for (var depth = 0.5f; depth <= 6f; depth += 0.5f)
        {
            var now = WaterDepthTint.At(depth);
            Assert.True(now.R <= last.R && now.G <= last.G && now.B <= last.B, $"depth {depth}");
            last = now;
        }

        var deep = WaterDepthTint.Deep;
        Assert.True(deep.R < deep.G && deep.G < deep.B, "deep water is bluer: red falls most");
    }

    [Fact]
    public void Given_FourCells_When_TheCornerDepthIsMade_Then_ItIsTheirMeanWithLandCountingZeroAndTheColumnCapped()
    {
        Assert.Equal(1f, WaterDepthTint.CornerDepth(1, 1, 1, 1));
        Assert.Equal(0.5f, WaterDepthTint.CornerDepth(1, 1, 0, 0));
        Assert.Equal(WaterDepthTint.CountDepth, WaterDepthTint.CornerDepth(50, 50, 50, 50));
    }

    // The red of the up-facing surface vertices at an x, z of the pool.
    private static double Red(ChunkMeshes meshes, float x, float z) => meshes.Alpha.Vertices.ToArray().Where(v => v.X == x && v.Z == z && v.Y > 4.5f).Average(static v => (double)(v.Color & 0xFF));

    [Fact]
    public void Given_APoolDeepInTheMiddleAndShallowAtTheRim_When_Meshed_Then_TheSurfaceIsDarkerOverTheDeepEnd()
    {
        // A basin: stone, with a column of water 6 deep at 7..9 and 1 deep elsewhere in 3..13.
        var blocks = new MapBlocks().Fill(0, 0, 0, 15, 0, 15, BlockId.Stone);
        blocks.Fill(0, 1, 0, 15, 9, 15, BlockId.Stone); // the rim stands as high as the water, so no side of water shows
        blocks.Fill(3, 9, 3, 13, 9, 13, BlockId.Water);
        blocks.Fill(7, 4, 7, 9, 8, 9, BlockId.Water);
        var meshes = ChunkMesher.Build(blocks, 0, 0, 0, 0, new MeshOptions(false, 100, null, LightBand.Day));

        var deep = Red(meshes, 8f, 8f);
        var shore = Red(meshes, 3f, 3f);
        var shallow = Red(meshes, 6f, 6f);

        Assert.True(deep < shallow, $"deep {deep} below the shallows {shallow}");
        Assert.True(shallow <= shore + 1, $"and the shore ({shore}) is at least as light as the shallows");
    }
}
