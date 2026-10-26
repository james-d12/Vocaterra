using Vocaterra.Game.Input;

namespace Vocaterra.Game.Tests.Input;

/// <summary>The key that opened a text box is swallowed once, by its first matching letter, and forgotten after two frames.</summary>
public sealed class OpenKeySwallowTests
{
    [Fact]
    public void TheArmedKeyIsSwallowedOnceInEitherCase()
    {
        var swallow = new OpenKeySwallow();
        Assert.False(swallow.Take('t'));
        swallow.Arm('t');
        Assert.False(swallow.Take('x'));
        Assert.True(swallow.Take('T'));
        Assert.False(swallow.Take('t'));
    }

    [Fact]
    public void TheArmedKeyIsForgottenAfterTwoFramesAndNullArmsNothing()
    {
        var swallow = new OpenKeySwallow();
        swallow.Arm('w');
        swallow.Tick();
        swallow.Tick();
        Assert.False(swallow.Take('w'));

        swallow.Arm('e');
        swallow.Tick();
        Assert.True(swallow.Take('e'));

        swallow.Arm(null);
        Assert.False(swallow.Take('e'));
    }
}
