using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>
/// The far pass is not drawn with the eye buried in a cave (the owner's report of 2026-11-19: 120 blocks down, the far view still drew 16 km of land behind the rock). Buried means deep under the lowest surface of the land round the eye, so
/// a cave's mouth in a hillside (the valley below it is lower than the eye) and the foot of a shaft open to the sky (counted at its bottom) are not.
/// </summary>
public sealed class BuriedEyeTests
{
    [Fact]
    public void Given_AnEyeOnTheSurfaceOrInTheAir_When_Judged_Then_ItIsNotBuried()
    {
        Assert.False(BuriedEye.Next(false, 64, 63));
        Assert.False(BuriedEye.Next(false, 200, 63));
        Assert.False(BuriedEye.Next(true, 64, 63)); // a buried eye that comes up is out at once
    }

    [Fact]
    public void Given_AnEyeDeepUnderTheLowestSurfaceRoundIt_When_Judged_Then_ItIsBuried()
    {
        Assert.True(BuriedEye.Next(false, -60, 60));
        Assert.True(BuriedEye.Next(false, 60 - BuriedEye.Below, 60));
        Assert.False(BuriedEye.Next(false, 60 - BuriedEye.Below + 1, 60));
    }

    [Fact]
    public void Given_AnEyeAtTheLine_When_ItStepsUpAndDown_Then_ItGoesInDeeperThanItComesOut()
    {
        Assert.True(BuriedEye.Rise < BuriedEye.Below);
        var buried = false;
        var switches = 0;
        foreach (var y in new[] { 45, 44, 45, 44, 45, 44 }) // 15 and 16 under a surface at 60: in at 16, and stepping back to 15 does not take it out
        {
            var now = BuriedEye.Next(buried, y, 60);
            switches += now != buried ? 1 : 0;
            buried = now;
        }

        Assert.True(buried);
        Assert.Equal(1, switches);
        Assert.False(BuriedEye.Next(true, 60 - BuriedEye.Rise + 1, 60));
    }

    [Fact]
    public void Given_ASkySurfaceWithAShaft_When_ItsLowestIsRead_Then_ItIsTheShaftsBottom()
    {
        // A flat land at 64 with a shaft open to the sky down to a cave floor at 20 in one column: the foot of the shaft is not buried, as the sky shows there.
        var surface = new SkySurface(0, 0, static (x, z) => x == 5 && z == 7 ? 20 : 64);

        Assert.Equal(64, surface.Highest);
        Assert.Equal(20, surface.Lowest);
        Assert.False(BuriedEye.Next(false, 20, surface.Lowest));
        Assert.True(BuriedEye.Next(false, 20, new SkySurface(0, 0, static (_, _) => 64).Lowest));
    }
}
