using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Input;
using Vocaterra.Game.Screens;

namespace Vocaterra.Game.Tests.Screens;

/// <summary>The screens over the world: the first open one takes the keys, the world has them when none is open, and the report names the open one in its own order.</summary>
public sealed class ScreenStackTests
{
    [Fact]
    public void TheFirstOpenScreenTakesTheKeysAndNoneLeavesThemToTheWorld()
    {
        var (mic, check, book) = (new FakeScreen("mic check", freesMouse: true), new FakeScreen("check (pre)", freesMouse: false), new FakeScreen("book", freesMouse: true));
        var stack = new ScreenStack([mic, check, book], [check, mic, book]);
        var input = new InputSnapshot(new KeyboardState(Keys.Escape), default, new KeyboardState(), false, false, 0);

        Assert.False(stack.HandleInput(input));
        Assert.Null(stack.Top);
        Assert.Equal("none (the world)", stack.OpenScreenName());

        check.IsOpen = true;
        book.IsOpen = true;
        Assert.True(stack.HandleInput(input));
        Assert.Equal(1, check.Inputs);
        Assert.Equal(0, book.Inputs);

        mic.IsOpen = true;
        Assert.Same(mic, stack.Top);
        Assert.True(stack.HandleInput(input));
        Assert.Equal(1, mic.Inputs);
        Assert.Equal("check (pre)", stack.OpenScreenName()); // the report's own order
    }

    [Fact]
    public void TheMouseIsFreeOnlyWhileAnOpenScreenFreesIt()
    {
        var (check, book) = (new FakeScreen("check (post)", freesMouse: false), new FakeScreen("book", freesMouse: true));
        var stack = new ScreenStack([check, book], [check, book]);
        Assert.False(stack.FreesMouse);
        check.IsOpen = true;
        Assert.False(stack.FreesMouse);
        book.IsOpen = true;
        Assert.True(stack.FreesMouse);
    }

    private sealed class FakeScreen(string name, bool freesMouse) : IScreen
    {
        public string Name => name;

        public bool IsOpen { get; set; }

        public bool FreesMouse => freesMouse;

        public int Inputs { get; private set; }

        public void HandleInput(IInputFrame input) => Inputs++;
    }
}
