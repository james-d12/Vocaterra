using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Lesson;

namespace Vocaterra.Game.Tests.UI.Dialogue;

/// <summary>U02: the lesson heading in the panel's header and the strip standing flush on the panel, at every canvas.</summary>
public sealed class ConversationSurfaceLayoutTests
{
    [Theory]
    [InlineData(1280, 720)]
    [InlineData(800, 600)]
    [InlineData(1280, 540)]
    public void TheLessonHeadingFitsTheHeaderBetweenTheNameAndTheStatusAtEveryScale(int windowWidth, int windowHeight)
    {
        var heading = "Lesson · First words · step 1 of 7 · Say it";
        foreach (var scale in new[] { 100, 125, 150 })
        {
            var canvas = UiScale.Canvas(scale, windowWidth, windowHeight);
            var panel = DialogueLayout.PanelBox(canvas.Width, canvas.Height);
            float Measure(string t) => t.Length * 7f;
            var nameRight = panel.X + DialogueLayout.Padding + Measure("Aldeano") + 24;
            var room = panel.Right - DialogueLayout.Padding - DialoguePanel.HeaderStatusRoom - nameRight;
            var fitted = TextWrap.Ellipsize(heading, room, Measure);
            Assert.True(Measure(fitted) <= Math.Max(room, 0), $"{windowWidth}x{windowHeight} at {scale}%: the heading is wider than the header's room");
            Assert.True(fitted.Length > 0 || room < 40, $"{windowWidth}x{windowHeight} at {scale}%: the heading vanished with {room} px of room");
        }
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(800, 600)]
    public void TheLessonStripStandsFlushOnThePanelWithNoHeadingRowOfItsOwnAtEveryScale(int windowWidth, int windowHeight)
    {
        Assert.Equal(0, LessonStripLayout.HeadingRow);
        foreach (var scale in new[] { 100, 125, 150 })
        {
            var canvas = UiScale.Canvas(scale, windowWidth, windowHeight);
            var panel = DialogueLayout.PanelBox(canvas.Width, canvas.Height);
            var offer = new LessonOffer("first-words", "First words", "Learn the first words.", false);
            var strip = LessonStripLayout.For(TestGame.Data.Locale, null, offer, canvas.Width, canvas.Height, panel.Y, t => t.Length * 7f, t => t.Length * 14f);
            Assert.NotNull(strip);
            Assert.True(strip!.Box.Bottom <= panel.Y, $"{windowWidth}x{windowHeight} at {scale}%: the strip reaches into the panel");
            Assert.False(ConversationLayout.Overlaps(strip.Box, panel));
        }
    }
}
