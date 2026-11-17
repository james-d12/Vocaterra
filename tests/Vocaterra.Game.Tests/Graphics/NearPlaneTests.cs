using Vocaterra.Game.Graphics.Camera;

namespace Vocaterra.Game.Tests.Graphics;

/// <summary>The near pass's near plane (B07d, the rendering review's quick win 10): 0.1 block, which still clears a block against the eye and halves the depth step at the voxel view distance.</summary>
public sealed class NearPlaneTests
{
    // The depth buffer's step at distance z for a 24-bit buffer and a perspective projection: z squared over (near times 2^24).
    private static double DepthStep(double z, double near) => z * z / (near * (1 << 24));

    [Fact]
    public void Given_TheNearPass_When_ItsNearPlaneIsRead_Then_ItIsATenthOfABlockAndStillShorterThanTheBodysRadius()
    {
        Assert.Equal(0.1f, FirstPersonCamera.NearPlane);
        Assert.True(FirstPersonCamera.NearPlane < 0.3f / 2, "a wall at the body's side is never cut");
    }

    [Fact]
    public void Given_TheViewDistancesOfTheQualityLevels_When_TheDepthStepIsComputed_Then_ItIsAtMostAFifthOfABlockAndHalfWhatItWas()
    {
        foreach (var distance in new[] { 224.0, 304.0, 400.0, 576.0 })
        {
            var now = DepthStep(distance, FirstPersonCamera.NearPlane);
            Assert.True(now < 0.2, $"{now:0.000} blocks at {distance}");
            Assert.Equal(DepthStep(distance, 0.05) / 2, now, 6);
        }
    }
}
