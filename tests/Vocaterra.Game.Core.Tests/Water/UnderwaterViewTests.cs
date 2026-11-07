using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>The easing of the view under water.</summary>
public sealed class UnderwaterViewTests
{
    [Fact]
    public void Given_ANewView_When_Read_Then_ItIsAboveWater()
    {
        var view = new UnderwaterView();

        Assert.Equal(0, view.Amount);
        Assert.False(view.IsUnder);
        Assert.Equal(0f, view.TintOpacity);
    }

    [Fact]
    public void Given_TheEyeGoesUnder_When_TimePasses_Then_TheAmountRisesToOneInAboutAFifteenthOfASecond()
    {
        var view = new UnderwaterView();

        view.Advance(true, UnderwaterView.EaseInSeconds / 2);
        Assert.Equal(0.5, view.Amount, 6);
        Assert.True(view.IsUnder);

        view.Advance(true, UnderwaterView.EaseInSeconds / 2);
        Assert.Equal(1, view.Amount, 6);
    }

    [Fact]
    public void Given_TheViewIsUnder_When_TheEyeSurfaces_Then_TheAmountFallsToZeroInAboutThreeTenthsOfASecond()
    {
        var view = new UnderwaterView();
        view.Advance(true, 1);

        view.Advance(false, UnderwaterView.EaseOutSeconds / 2);
        Assert.Equal(0.5, view.Amount, 6);

        view.Advance(false, UnderwaterView.EaseOutSeconds / 2);
        Assert.Equal(0, view.Amount, 6);
        Assert.False(view.IsUnder);
    }

    [Fact]
    public void Given_ALongFrame_When_Advanced_Then_TheAmountStopsAtTheEnds()
    {
        var view = new UnderwaterView();

        view.Advance(true, 30);
        Assert.Equal(1, view.Amount);

        view.Advance(false, 30);
        Assert.Equal(0, view.Amount);
    }

    [Fact]
    public void Given_ANegativeOrZeroTime_When_Advanced_Then_NothingMoves()
    {
        var view = new UnderwaterView();
        view.Advance(true, 0.05);
        var before = view.Amount;

        view.Advance(true, -1);
        view.Advance(false, 0);

        Assert.Equal(before, view.Amount);
    }

    [Fact]
    public void Given_ThePartlyUnderView_When_TheEyeGoesBackUnder_Then_ItTurnsAroundFromWhereItWas()
    {
        var view = new UnderwaterView();
        view.Advance(true, UnderwaterView.EaseInSeconds / 2);
        view.Advance(false, UnderwaterView.EaseOutSeconds / 4); // 0.5 - 0.25
        Assert.Equal(0.25, view.Amount, 6);

        view.Advance(true, UnderwaterView.EaseInSeconds / 4); // + 0.25
        Assert.Equal(0.5, view.Amount, 6);
    }

    [Fact]
    public void Given_TheViewIsFullyUnder_When_TheTintIsRead_Then_ItIsAQuarterAndScalesWithTheAmount()
    {
        var view = new UnderwaterView();
        view.Advance(true, UnderwaterView.EaseInSeconds / 2);
        Assert.Equal(0.125f, view.TintOpacity, 5);

        view.Advance(true, 1);
        Assert.Equal(0.25f, view.TintOpacity, 5);
    }

    [Fact]
    public void Given_TheWaterFog_When_Read_Then_ItIsABlueGreenEndingNearFourteenBlocks()
    {
        var fog = UnderwaterView.WaterFog;

        Assert.True(fog.Blue > fog.Green && fog.Green > fog.Red);
        Assert.Equal(0f, fog.Start);
        Assert.Equal(14f, fog.End);
    }
}
