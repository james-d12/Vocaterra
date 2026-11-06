using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Station;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.UI.Station;

/// <summary>
/// The station overlay's layout (W07) on the six canvases of the two window sizes and the three interface scales (X08): every piece inside the panel and the panel inside the canvas,
/// no piece over another, the strip's pieces apart, the text wrapped to its box with a stand-in font wider than the real one, and a click on a piece hits it. The layouts are plain numbers.
/// </summary>
public sealed class StationLayoutTests(ITestOutputHelper output)
{
    private static readonly (int Width, int Height)[] Windows = [(1280, 720), (800, 600)];
    private static readonly int[] Scales = [100, 125, 150];

    /// <summary>Every window size and scale.</summary>
    public static TheoryData<int, int, int> Cases
    {
        get
        {
            var data = new TheoryData<int, int, int>();
            foreach (var (width, height) in Windows)
            {
                foreach (var scale in Scales)
                {
                    data.Add(width, height, scale);
                }
            }

            return data;
        }
    }

    private static float Small(string text) => text.Length * 7f;

    private static float Large(string text) => text.Length * 14f;

    private static bool Inside(PixelBox box, PixelBox outer) => box.X >= outer.X && box.Y >= outer.Y && box.Right <= outer.Right && box.Bottom <= outer.Bottom;

    private static void NoneOverlap(string what, params (string Name, PixelBox Box)[] pieces)
    {
        for (var i = 0; i < pieces.Length; i++)
        {
            for (var j = i + 1; j < pieces.Length; j++)
            {
                Assert.False(ConversationLayout.Overlaps(pieces[i].Box, pieces[j].Box), $"{what}: {pieces[i].Name} {pieces[i].Box} overlaps {pieces[j].Name} {pieces[j].Box}");
            }
        }
    }

    private static StationLayout LayoutOf(int windowWidth, int windowHeight, int scale, int fixtures = 3, int targets = 3, int steps = 6, int verbs = 0)
    {
        var canvas = UiScale.Canvas(scale, windowWidth, windowHeight);
        return new StationLayout(canvas.Width, canvas.Height, fixtures, targets, steps, verbs);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryPieceIsInsideThePanelAndThePanelInsideTheCanvasAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = UiScale.Canvas(scale, windowWidth, windowHeight);
        var layout = LayoutOf(windowWidth, windowHeight, scale);
        Assert.True(Inside(layout.Panel, new PixelBox(0, 0, canvas.Width, canvas.Height)), $"{layout.Panel} is outside {canvas}");
        var pieces = Pieces(layout);
        foreach (var (name, box) in pieces)
        {
            Assert.True(Inside(box, layout.Panel), $"{name} {box} is outside the panel {layout.Panel}");
            Assert.True(box.Width > 0 && box.Height > 0, $"{name} has no room");
        }

        output.WriteLine($"{canvas.Width}x{canvas.Height}: panel {layout.Panel.Width}x{layout.Panel.Height}, compact {layout.Compact}, middle {layout.Middle.Height} high, held cells {layout.Held(0).Width}x{layout.Held(0).Height}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void NoPieceOverlapsAnotherAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        foreach (var targets in new[] { 1, 2, 3, 4 })
        {
            var layout = LayoutOf(windowWidth, windowHeight, scale, targets: targets);
            NoneOverlap($"{windowWidth}x{windowHeight}@{scale} with {targets} targets", Pieces(layout));
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheStripsPiecesAreApartAndInsideItsBoxAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        foreach (var steps in new[] { 1, 3, 6, 12 })
        {
            var strip = LayoutOf(windowWidth, windowHeight, scale, steps: steps).Strip;
            var pieces = new[] { ("text", strip.Text), ("dots", strip.Dots), ("repeat", strip.Repeat), ("slower", strip.Slower), ("help", strip.Help) };
            foreach (var (name, box) in pieces)
            {
                Assert.True(Inside(box, strip.Box), $"{steps} steps: {name} {box} is outside the strip {strip.Box}");
            }

            NoneOverlap($"{steps} steps", pieces);
            Assert.True(strip.DotStep(steps) * steps <= strip.Dots.Width, $"{steps} dots do not fit {strip.Dots.Width} px");
            Assert.True(strip.MaxTextLines >= 3, $"{strip.MaxTextLines} lines of text");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheCaptionHoldsItsTextAndThePageControlsApartAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var layout = LayoutOf(windowWidth, windowHeight, scale);
        var pieces = new[] { ("caption text", layout.CaptionText), ("previous", layout.PagePrevious), ("label", layout.PageLabel), ("next", layout.PageNext) };
        foreach (var (name, box) in pieces)
        {
            Assert.True(Inside(box, layout.HeldCaption), $"{name} {box} is outside the caption {layout.HeldCaption}");
        }

        NoneOverlap("caption", pieces);
        Assert.True(layout.CaptionText.Width >= 100, "the caption's text has room");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheFixturesAndTheTargetsStayInsideTheirAreasAndApartAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var layout = LayoutOf(windowWidth, windowHeight, scale, fixtures: 4, targets: 4);
        Assert.Equal(Math.Min(4, layout.FixtureCapacity), layout.FixtureCount); // a column that cannot hold them all shows the first ones
        Assert.True(layout.FixtureCount >= 3, "the three fixtures of a station always show");
        var fixtures = Enumerable.Range(0, layout.FixtureCount).Select(i => ($"fixture {i}", layout.Fixture(i))).ToArray();
        var targets = Enumerable.Range(0, layout.TargetCount).Select(i => ($"target {i}", layout.Target(i))).ToArray();
        Assert.All(fixtures, f => Assert.True(Inside(f.Item2, layout.FixturesColumn), $"{f.Item1} is outside its column"));
        Assert.All(targets, t => Assert.True(Inside(t.Item2, layout.TargetsArea), $"{t.Item1} is outside its area"));
        NoneOverlap("fixtures", fixtures);
        NoneOverlap("targets", targets);
        Assert.All(fixtures, static f => Assert.True(f.Item2.Height >= 20));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheVerbChoiceFloatsInsideThePanelWithItsRowsInsideIt(int windowWidth, int windowHeight, int scale)
    {
        var layout = LayoutOf(windowWidth, windowHeight, scale, verbs: 3);
        Assert.True(Inside(layout.VerbBox, layout.Panel), $"{layout.VerbBox} is outside {layout.Panel}");
        var rows = Enumerable.Range(0, layout.VerbCount).Select(i => ($"verb {i}", layout.Verb(i))).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, r => Assert.True(Inside(r.Item2, layout.VerbBox), $"{r.Item1} is outside the choice"));
        NoneOverlap("verbs", rows);
        Assert.Equal(0, LayoutOf(windowWidth, windowHeight, scale).VerbBox.Width);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheStripsTextWrapsToItsBoxAtEverySizeOfFont(int windowWidth, int windowHeight, int scale)
    {
        var strip = LayoutOf(windowWidth, windowHeight, scale).Strip;
        var long1 = "Saca el pan caliente del horno con la mano, despacio, y ponlo en el mostrador de la panadería de Luz.";
        var long2 = "Take the hot bread out of the oven by hand, slowly, and put it on the counter of Luz's bakery in the village.";
        var model = new InstructionStrip("saca-el-pan", UnitKind.Step, 1, long1, long2, false, [], []);
        foreach (var measure in new Func<string, float>[] { Small, Large })
        {
            var (spanish, english) = InstructionStripLayout.TextLines(model, strip.Text.Width, strip.MaxTextLines, measure);
            Assert.True(spanish.Count + english.Count <= strip.MaxTextLines, $"{spanish.Count + english.Count} lines in {strip.MaxTextLines}");
            Assert.NotEmpty(spanish);
            Assert.NotEmpty(english); // the English keeps a line
            Assert.All(spanish.Concat(english), line => Assert.True(measure(line) <= strip.Text.Width, $"'{line}' is wider than {strip.Text.Width}"));
        }
    }

    [Fact]
    public void AClickHitsTheCellTheButtonTheControlOrTheTargetUnderIt()
    {
        var layout = new StationLayout(1280, 720, 3, 2, 3);
        StationHit? At(PixelBox box, int held = 9, int pages = 2) => layout.HitAt(box.X + (box.Width / 2), box.Y + (box.Height / 2), held, pages);
        Assert.Equal(new StationHit(StationRegion.Held, 3), At(layout.Held(3)));
        Assert.Null(At(layout.Held(6), held: 5)); // past the page's cells
        Assert.Equal(new StationHit(StationRegion.Hand, 0), At(layout.Hand));
        Assert.Equal(new StationHit(StationRegion.Target, 1), At(layout.Target(1)));
        Assert.Equal(new StationHit(StationRegion.Repeat, 0), At(layout.Strip.Repeat));
        Assert.Equal(new StationHit(StationRegion.Slower, 0), At(layout.Strip.Slower));
        Assert.Equal(new StationHit(StationRegion.Help, 0), At(layout.Strip.Help));
        Assert.Equal(new StationHit(StationRegion.PageNext, 0), At(layout.PageNext));
        Assert.Equal(new StationHit(StationRegion.PagePrevious, 0), At(layout.PagePrevious));
        Assert.Null(At(layout.PageNext, pages: 1)); // one page: no controls
        Assert.Null(layout.HitAt(2, 2, 9, 2)); // the corner of the canvas
    }

    [Fact]
    public void WhileTheVerbChoiceIsOpenOnlyItsRowsAreHit()
    {
        var layout = new StationLayout(1280, 720, 3, 2, 3, verbCount: 2);
        var row = layout.Verb(1);
        Assert.Equal(new StationHit(StationRegion.Verb, 1), layout.HitAt(row.X + 4, row.Y + 4, 9, 1));
        Assert.Null(layout.HitAt(layout.Hand.X + 2, layout.Hand.Y + 2, 9, 1)); // the rest waits
    }

    private static (string Name, PixelBox Box)[] Pieces(StationLayout layout) =>
    [
        ("title", layout.Title),
        ("strip", layout.Strip.Box),
        ("fixtures", layout.FixturesColumn),
        ("targets", layout.TargetsArea),
        ("notice", layout.Notice),
        ("held caption", layout.HeldCaption),
        ("hand", layout.Hand),
        .. Enumerable.Range(0, StationLayout.HeldColumns).Select(i => ($"held {i}", layout.Held(i))),
        ("selection", layout.Selection),
        ("footer", layout.Footer),
    ];
}
