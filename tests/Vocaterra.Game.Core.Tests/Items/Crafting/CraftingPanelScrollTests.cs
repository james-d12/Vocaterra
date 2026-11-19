using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Crafting;

/// <summary>I10b (QA-2026-11-19-ui-1): the crafting panel's scroll, moved by the wheel and the keys and held inside the range the layout gives.</summary>
public sealed class CraftingPanelScrollTests
{
    [Fact]
    public void GivenARangeOfTenRows_WhenTheWheelAndTheKeysMoveIt_ThenItStopsAtBothEnds()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var screen = new CraftingScreen();
        var sound = new SoundFrontend(new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(1)), new SoundEventMapper());
        var panel = new CraftingPanel(screen, session, sound, static (_, _) => null, static () => false, static () => (10, 4));
        screen.Open(session.Words, []);

        panel.HandleInput(new TestInputFrame { Wheel = -120 });
        Assert.Equal(2, screen.Scroll); // a notch down: two rows
        panel.HandleInput(TestInputFrame.Pressing(GameKey.PageDown));
        Assert.Equal(6, screen.Scroll);
        panel.HandleInput(new TestInputFrame { Wheel = -1200 });
        Assert.Equal(10, screen.Scroll);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Up));
        Assert.Equal(9, screen.Scroll);
        panel.HandleInput(new TestInputFrame { Wheel = 2400 });
        Assert.Equal(0, screen.Scroll);
        panel.HandleInput(TestInputFrame.Pressing(GameKey.Escape));
        Assert.False(screen.IsOpen);
    }

    [Fact]
    public void GivenAScrolledPanel_WhenItIsOpenedAgain_ThenItStartsAtTheTop()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var screen = new CraftingScreen();
        screen.Open(session.Words, []);
        screen.ScrollBy(5, 8);
        screen.Close();

        screen.Open(session.Words, []);

        Assert.Equal(0, screen.Scroll);
    }
}
