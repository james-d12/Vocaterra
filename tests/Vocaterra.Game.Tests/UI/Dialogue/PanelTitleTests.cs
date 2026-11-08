using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.Tests.UI.Dialogue;

/// <summary>U05 (GitHub issue #6): the NPC's name is the panel's title, so the header band is taller and the lines start under it; the NPC's lines carry no name prefix.</summary>
public sealed class PanelTitleTests
{
    private static readonly Func<string, float> Measure = text => text.Length * 7f;

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(800, 600)]
    public void ANormalPanelHasTheTallHeaderAndTheLinesStartUnderIt(int width, int height)
    {
        var layout = new DialogueLayout(width, height, Measure, [], [], false);
        Assert.Equal(DialogueLayout.HeaderHeight, layout.Header);
        Assert.False(layout.Compact);
        Assert.Equal(layout.Panel.Y + DialogueLayout.HeaderHeight, layout.LinesTop);
        Assert.Equal(34 + 12, DialogueLayout.HeaderHeight); // the old 8 + 20 + 6, and the name's second 16 px less four
        Assert.Equal(32, DialoguePanel.NameSize(layout));
    }

    [Theory]
    [InlineData(800, 400)]
    [InlineData(533, 400)]
    [InlineData(1024, 539)]
    public void ACompactPanelKeepsTheSmallNameAndTheOldHeader(int width, int height)
    {
        var layout = new DialogueLayout(width, height, Measure, [], [], false);
        Assert.Equal(DialogueLayout.CompactHeaderHeight, layout.Header);
        Assert.True(layout.Compact);
        Assert.Equal(34, layout.LinesTop - layout.Panel.Y);
        Assert.Equal(16, DialoguePanel.NameSize(layout));
    }

    [Fact]
    public void TheHeaderDoesNotMoveTheButtons()
    {
        var buttons = SpeechButtons.For(RouteScene.Shipped.Words.Buttons);
        var layout = new DialogueLayout(1280, 720, Measure, [], buttons, false);
        Assert.Equal(buttons.Count, layout.Buttons.Count);
        foreach (var (action, box) in layout.Buttons)
        {
            Assert.Equal(action, layout.ActionAt(box.X + 2, box.Y + 2));
            Assert.True(box.Y >= layout.LinesBottom, "a button must sit under the conversation");
            Assert.True(box.Bottom <= layout.Panel.Bottom);
        }
    }

    [Fact]
    public void TheNpcsLinesCarryNoNamePrefixAndTheLearnersLinesKeepTheirs()
    {
        var ui = RouteScene.Shipped.Locale;
        Assert.Equal(string.Empty, DialoguePanel.LinePrefix(DialogueSpeaker.Guide, ui));
        Assert.Equal(string.Empty, DialoguePanel.LinePrefix(DialogueSpeaker.World, ui));
        Assert.Equal(string.Empty, DialoguePanel.LinePrefix(DialogueSpeaker.Correction, ui));
        Assert.Equal(ui.Ui("dialogueYou"), DialoguePanel.LinePrefix(DialogueSpeaker.Player, ui));
        Assert.Equal(DialoguePanel.SpokenPrefix(ui), DialoguePanel.LinePrefix(DialogueSpeaker.PlayerSpoken, ui));
        Assert.Equal(DialoguePanel.SupportPrefix(ui), DialoguePanel.LinePrefix(DialogueSpeaker.English, ui));
        Assert.NotEqual(string.Empty, DialoguePanel.LinePrefix(DialogueSpeaker.Player, ui));
    }
}
