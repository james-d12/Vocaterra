using Vocaterra.Game.Graphics.Camera;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.Tests.UI.Hud;

/// <summary>
/// Where an NPC's name plate stands (<see cref="NpcLabelLayer.Place"/>). village-plan V09's scripted runs found the farmer's plate, <c>Granjero</c>, pinned to the
/// window's left edge in r03, while the player stood at the villager facing south-south-east and the farmer was 70 degrees to the left: in front of the camera
/// but out of the picture, and the clamp of X08 moved his plate inside the window. A plate wholly outside the canvas is now left out.
/// </summary>
public sealed class NpcLabelPlacementTests
{
    private const int W = 1280;
    private const int H = 720;
    private const double StepsPerDegree = 4096 / 360.0;
    private const double TextWidth = 64;

    // r03's last frame: the player at the villager, after the talk, yaw 152 and pitch -9 (FirstPersonCamera's 70 degrees of vertical field of view).
    private static ScreenProjection AtTheVillager() => new(292.2, 38 + 1.62, 219.1, 152 * StepsPerDegree, -9 * StepsPerDegree, FirstPersonCamera.FieldOfViewDegrees, W, H);

    [Fact]
    public void TheFarmerBesideThePlayerButOutOfThePictureHasNoPlate()
    {
        var farmer = AtTheVillager().Project(300.5, 38 + 2.25, 218.5); // npc.farmer (300, 218), the label point above the head
        Assert.True(farmer.InFront);
        Assert.True(farmer.X < 0, $"the farmer projects at x {farmer.X}, inside the window");
        Assert.Null(NpcLabelLayer.Place(farmer.X, farmer.Y, TextWidth, W, H));
    }

    [Fact]
    public void TheVillagerInFrontKeepsHisPlateOverTheHead()
    {
        var villager = AtTheVillager().Project(293.5, 38 + 2.25, 221.5); // the villager (293, 221)
        var plate = NpcLabelLayer.Place(villager.X, villager.Y, TextWidth, W, H);
        Assert.NotNull(plate);
        Assert.InRange(plate.Value.X + (plate.Value.Width / 2.0), villager.X - 1, villager.X + 1);
    }

    [Theory]
    [InlineData(-20.0, 300.0)] // the plate half over the left edge
    [InlineData(W + 20.0, 300.0)] // half over the right edge
    [InlineData(640.0, 2.0)] // over the top edge
    public void APlatePartlyOverAnEdgeIsMovedInsideTheWindow(double x, double y)
    {
        var plate = NpcLabelLayer.Place(x, y, TextWidth, W, H);
        Assert.NotNull(plate);
        Assert.InRange(plate.Value.X, 0, W - plate.Value.Width);
        Assert.InRange(plate.Value.Y, 0, H - plate.Value.Height);
    }

    [Theory]
    [InlineData(-200.0, 300.0)]
    [InlineData(W + 200.0, 300.0)]
    [InlineData(640.0, -100.0)]
    [InlineData(640.0, H + 100.0)]
    public void APlateWhollyOutsideTheWindowIsLeftOut(double x, double y) => Assert.Null(NpcLabelLayer.Place(x, y, TextWidth, W, H));
}
