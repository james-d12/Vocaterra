using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.Tests.UI.Dialogue;

/// <summary>
/// U02b (QA-2026-11-06-ui-2 and ui-5): at 800x600 and 1280x720, at 100, 125 and 150 % text, the collapsed exchange keeps the learner's line and the NPC's
/// Spanish (the room the layout gives it is measured, not assumed), and the lesson heading keeps its step and kind.
/// </summary>
public sealed class PanelFitLayoutTests
{
    private static Func<string, float> MeasureAt(int scale) => text => text.Length * 7f * scale / 100f;

    [Theory]
    [InlineData(800, 600, 100)]
    [InlineData(800, 600, 125)]
    [InlineData(800, 600, 150)]
    [InlineData(1280, 720, 100)]
    [InlineData(1280, 720, 125)]
    [InlineData(1280, 720, 150)]
    public void TheCollapsedExchangeKeepsTheLearnersLineAndTheNpcsSpanishAtEverySize(int windowWidth, int windowHeight, int scale)
    {
        var canvas = UiScale.Canvas(scale, windowWidth, windowHeight);
        var layout = new DialogueLayout(canvas.Width, canvas.Height, MeasureAt(scale), [], [], false);
        var capacity = (layout.LinesBottom - layout.LinesTop) / DialogueLayout.LineHeight;
        var blocks = new List<PanelBlock>
        {
            new(4, DialogueSpeaker.Player, 1, 0),
            new(5, DialogueSpeaker.Guide, 1, 1),
            new(-1, DialogueSpeaker.Coaching, 2, 0),
        };

        var fit = PanelFitter.Fit(blocks, capacity, expanded: false, scrollBack: 0, hiddenBefore: 4);
        Assert.True(fit.Shows(0) && fit.Shows(1), $"{windowWidth}x{windowHeight} at {scale}% ({capacity} rows): the learner's line and the NPC's line must show");
    }

    [Theory]
    [InlineData(800, 600, 100)]
    [InlineData(800, 600, 125)]
    [InlineData(800, 600, 150)]
    [InlineData(1280, 720, 100)]
    [InlineData(1280, 720, 125)]
    [InlineData(1280, 720, 150)]
    public void TheLessonHeadingKeepsTheStepAndItsKindAtEverySize(int windowWidth, int windowHeight, int scale)
    {
        var canvas = UiScale.Canvas(scale, windowWidth, windowHeight);
        var panel = DialogueLayout.PanelBox(canvas.Width, canvas.Height);
        var measure = MeasureAt(scale);
        var nameRight = panel.X + DialogueLayout.Padding + measure("Tendero") + 24;
        var room = panel.Right - DialogueLayout.Padding - DialoguePanel.HeaderStatusRoom - nameRight;
        var heading = new LessonHeading("Lesson", "Welcome: first words with Luna", "step 3 of 25 · Say it");
        var fitted = heading.FitTo(room, measure);
        Assert.Contains("step 3 of 25 · Say it", fitted, StringComparison.Ordinal);
        Assert.True(measure(fitted) <= room + 0.5f, $"{windowWidth}x{windowHeight} at {scale}%: the heading is wider than the room");
    }

    [Fact]
    public void TheHiddenLinesRowReadsSingularForOneLine()
    {
        Assert.Equal("1 earlier line hidden (PageUp or O)", TestGame.Data.Locale.Plural("ui.dialogueOlder", 1));
        Assert.Equal("3 earlier lines hidden (PageUp or O)", TestGame.Data.Locale.Plural("ui.dialogueOlder", 3));
    }
}
