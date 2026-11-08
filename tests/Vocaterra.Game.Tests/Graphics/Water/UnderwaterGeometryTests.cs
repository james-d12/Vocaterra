using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Graphics.Water;

namespace Vocaterra.Game.Tests.Graphics.Water;

/// <summary>The device-free sums of the underwater pass.</summary>
public sealed class UnderwaterGeometryTests
{
    private const int U = VoxelPhysics.UnitsPerBlock;

    [Fact]
    public void Given_AnEyeInBlocks_When_Converted_Then_ItIsInFixedPointUnitsRoundedDown()
    {
        var (x, y, z) = UnderwaterGeometry.EyeUnits(new Vector3(32.5f, 11.25f, 7f));

        Assert.Equal((32 * U) + (U / 2), x);
        Assert.Equal((11 * U) + (U / 4), y);
        Assert.Equal(7 * U, z);
    }

    [Fact]
    public void Given_AnEyeJustBelowACellEdge_When_Converted_Then_ItStaysInThatCell()
    {
        var (x, _, _) = UnderwaterGeometry.EyeUnits(new Vector3(-0.0001f, 0f, 0f));

        Assert.True(x < 0);
    }

    [Theory]
    [InlineData(1280, 720, 2, 640, 360)]
    [InlineData(1280, 720, 4, 320, 180)]
    [InlineData(801, 601, 2, 401, 301)]
    [InlineData(801, 601, 4, 201, 151)]
    [InlineData(1280, 720, 1, 1280, 720)]
    [InlineData(2, 2, 4, 1, 1)]
    [InlineData(1280, 720, 0, 1280, 720)]
    public void Given_APictureSize_When_TheCopyIsSized_Then_EachSideIsDividedAndRoundedUp(int width, int height, int downscale, int expectedWidth, int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), UnderwaterGeometry.CopySize(width, height, downscale));
    }
}
