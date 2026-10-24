using Vocaterra.Game;
using Vocaterra.Game.Language.Checks;
using Vocaterra.Game.Session;
using Vocaterra.MonoGame.UI;
using Vocaterra.Nlp.Content;
using Xunit.Abstractions;

namespace Vocaterra.MonoGame.Tests;

/// <summary>
/// S22: the check card's text fits the card. Every item of <c>checks.json</c> (the question or the situation, the hints and every option label) is laid out by
/// <see cref="CheckCardLayout"/> at 1280 x 720 and 800 x 600, and no row is wider than the card's inner width and nothing reaches the footer. The text is measured with a
/// deliberately wide stand-in for the game's font (Pixel Operator measures about 12.4 px a character at 32 and 6.1 at 16; the stand-in takes 14 and 7), so a pass is not a
/// guess about the font's own metrics: the real font is narrower.
/// </summary>
public sealed class CheckCardLayoutTests(ITestOutputHelper output)
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());

    private static readonly (int Width, int Height)[] Sizes = [(1280, 720), (800, 600)];

    private static float Large(string text) => text.Length * 14f;

    private static float Small(string text) => text.Length * 7f;

    [Fact]
    public void EveryItemOfTheCheckBankFitsTheCardAtBothWindowSizes()
    {
        var bank = CheckBank.Load(Bundle.Value);
        Assert.True(bank.Count >= 20);
        var rowsSeen = 0;
        foreach (var (width, height) in Sizes)
        {
            var panel = CheckCardLayout.PanelFor(width, height);
            Assert.True(panel.X >= 0 && panel.Y >= 0 && panel.Right <= width && panel.Bottom <= height);
            var inner = panel.Width - (2 * CheckCardLayout.Side);
            foreach (var item in bank)
            {
                var layout = item.Kind == CheckItemKind.Comprehension
                    ? CheckCardLayout.ForComprehension(panel, item.Question, CheckLayer.ComprehensionHint, item.Options.Select(static o => (o.Label, o.Icon is not null)).ToList(), Large, Small)
                    : CheckCardLayout.ForProduction(panel, CheckLayer.ProductionPreface, item.Situation, CheckLayer.ProductionHint, Large, Small);
                var where = $"{width}x{height} {item.Id}";
                Assert.All(layout.Preface, row => Assert.True(Small(row) <= inner, $"{where}: preface row '{row}' is wider than the card"));
                Assert.All(layout.Heading, row => Assert.True(Large(row) <= inner, $"{where}: heading row '{row}' is wider than the card"));
                Assert.All(layout.Hint, row => Assert.True(Small(row) <= inner, $"{where}: hint row '{row}' is wider than the card"));
                Assert.True(layout.Heading.Count <= 2, $"{where}: the heading takes {layout.Heading.Count} rows");
                foreach (var option in layout.Options)
                {
                    var room = option.Box.Right - 12 - option.TextX;
                    Assert.All(option.Rows, row => Assert.True((option.Large ? Large(row) : Small(row)) <= room, $"{where}: option row '{row}' is wider than its box"));
                    Assert.True(option.Large || option.Rows.Count * CheckCardLayout.SmallRow <= CheckCardLayout.OptionHeight - 8, $"{where}: an option takes {option.Rows.Count} small rows");
                    Assert.True(option.Box.X >= panel.X && option.Box.Right <= panel.Right);
                    rowsSeen += option.Rows.Count;
                }

                if (layout.Input is { } input)
                {
                    Assert.True(input.X >= panel.X && input.Right <= panel.Right);
                }

                Assert.True(layout.Bottom <= CheckCardLayout.ContentLimit(panel), $"{where}: the content reaches y {layout.Bottom}, past {CheckCardLayout.ContentLimit(panel)} where the footer starts");
                rowsSeen += layout.Heading.Count + layout.Hint.Count;
            }
        }

        output.WriteLine($"{bank.Count} items x {Sizes.Length} sizes laid out, {rowsSeen} rows measured");
    }

    [Fact]
    public void ALongLineWrapsToTwoRowsInsteadOfRunningOffTheCard()
    {
        var panel = CheckCardLayout.PanelFor(1280, 720);
        var inner = panel.Width - (2 * CheckCardLayout.Side);
        const string Long = "The shopkeeper offers you bread. You don't want it. Say no politely.";
        Assert.True(Large(Long) > inner, "the stand-in font makes this line too long for one row");

        var layout = CheckCardLayout.ForProduction(panel, CheckLayer.ProductionPreface, Long, CheckLayer.ProductionHint, Large, Small);

        Assert.Equal(2, layout.Heading.Count);
        Assert.Equal(Long, string.Join(" ", layout.Heading)); // wrapped, nothing cut
        Assert.True(layout.Hint.Count >= 1);
        Assert.True(layout.Input!.Value.Y >= layout.HintY + (layout.Hint.Count * CheckCardLayout.SmallRow), "the text box moved down under the extra row");
    }
}
