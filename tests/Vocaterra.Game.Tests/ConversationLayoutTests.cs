using Vocaterra.Game.Graphics.Camera;
using Vocaterra.Game.UI;
using Vocaterra.Game.UI.Dialogue;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests;

/// <summary>S22 item 1 and 2: the conversation screen's layout. No card overlaps the speaker's head, the bubble, the panel or another card, at the two window sizes, with 0 to 5 cards; the facilitator's label has a reserved place.</summary>
public sealed class ConversationLayoutTests(ITestOutputHelper output)
{
    private static readonly (int Width, int Height)[] Sizes = [(1280, 720), (800, 600)];

    // The head's x as a fraction of the window's width and its top y as a fraction of the height: centred, to the left, to the right and near the top.
    private static readonly (double X, double Y)[] HeadSpots = [(0.5, 0.25), (0.2, 0.3), (0.8, 0.3), (0.5, 0.08), (0.05, 0.4), (0.95, 0.4)];

    private static readonly ConversationCardRequest[] Pattern =
    [
        ConversationCards.NewWord,
        ConversationCards.Support,
        ConversationCards.Support,
        ConversationCards.KeyWord(false),
        ConversationCards.KeyWord(false),
    ];

    private static (PixelBox Head, PixelBox Bubble) Speaker(int width, int height, (double X, double Y) spot, int bubbleWidth)
    {
        var x = spot.X * width;
        var y = spot.Y * height;
        var head = ConversationCards.HeadRect(x, y, y + 110);
        var bubble = DialogueBubble.Place(width, height, x, y, bubbleWidth, 70)!.Value;
        return (head, new PixelBox(bubble.Box.X, bubble.Box.Y, bubble.Box.Width, bubble.Box.Height + DialogueBubble.TailHeight));
    }

    [Fact]
    public void NoCardOverlapsTheHeadTheBubbleThePanelAnotherCardOrTheWindowsEdge()
    {
        var checkedCases = 0;
        foreach (var (width, height) in Sizes)
        {
            var panel = DialogueLayout.PanelBox(width, height);
            var window = new PixelBox(0, 0, width, height);
            foreach (var spot in HeadSpots)
            {
                var (head, bubble) = Speaker(width, height, spot, 300);
                for (var count = 0; count <= Pattern.Length; count++)
                {
                    var layout = ConversationLayout.Compute(width, height, head, bubble, panel, Pattern.Take(count).ToList());
                    Assert.Equal(count, layout.Cards.Count);
                    var shown = layout.Cards.Where(c => c.State != CardPresence.Hidden).ToList();
                    if (count > 0)
                    {
                        Assert.NotEqual(CardPresence.Hidden, layout.Cards[0].State); // the newest card is always shown
                    }

                    foreach (var card in shown)
                    {
                        Assert.False(ConversationLayout.Overlaps(card.Box, head), $"{width}x{height} head {spot} {count} cards: {card.Kind} {card.Box} overlaps the head {head}");
                        Assert.False(ConversationLayout.Overlaps(card.Box, bubble), $"{width}x{height} head {spot} {count} cards: {card.Kind} {card.Box} overlaps the bubble {bubble}");
                        Assert.False(ConversationLayout.Overlaps(card.Box, panel), $"{width}x{height} head {spot} {count} cards: {card.Kind} {card.Box} overlaps the panel {panel}");
                        Assert.True(card.Box.X >= 0 && card.Box.Y >= 0 && card.Box.Right <= window.Right && card.Box.Bottom <= window.Bottom, $"{card.Box} leaves the window");
                    }

                    for (var i = 0; i < shown.Count; i++)
                    {
                        for (var j = i + 1; j < shown.Count; j++)
                        {
                            Assert.False(ConversationLayout.Overlaps(shown[i].Box, shown[j].Box), $"{width}x{height} head {spot} {count} cards: cards {i} and {j} overlap");
                        }
                    }

                    checkedCases++;
                }
            }
        }

        output.WriteLine($"{checkedCases} layouts checked");
    }

    [Fact]
    public void WithNoHeadOnTheScreenTheCardsStillStandAbovePanelAndInsideTheWindow()
    {
        foreach (var (width, height) in Sizes)
        {
            var panel = DialogueLayout.PanelBox(width, height);
            var layout = ConversationLayout.Compute(width, height, null, null, panel, Pattern);
            Assert.All(layout.Cards.Where(c => c.State != CardPresence.Hidden), c =>
            {
                Assert.False(ConversationLayout.Overlaps(c.Box, panel));
                Assert.True(c.Box.X >= 0 && c.Box.Right <= width && c.Box.Y >= 0);
            });
            Assert.Contains(layout.Cards, c => c.State != CardPresence.Hidden);
        }
    }

    [Fact]
    public void AtTheStallTheCardsStandInAColumnBesideTheSpeakerNotOverHim()
    {
        var panel = DialogueLayout.PanelBox(1280, 720);
        var (head, bubble) = Speaker(1280, 720, (0.5, 0.25), 200);

        var layout = ConversationLayout.Compute(1280, 720, head, bubble, panel, [ConversationCards.Support]);

        var card = Assert.Single(layout.Cards);
        Assert.Equal(CardPresence.Full, card.State);
        Assert.True(card.Box.X >= head.Right || card.Box.Right <= head.X, "beside the head, not under it");
        Assert.Equal(panel.Y - ConversationLayout.Gap, card.Box.Bottom); // the newest card stands on the panel
        Assert.NotNull(layout.Zone);
    }

    [Fact]
    public void WhenThereIsNoRoomTheCardsCollapseToOneLineNewestAtTheBottomAndTheOldestAreHiddenLast()
    {
        var panel = DialogueLayout.PanelBox(800, 600);
        var (head, bubble) = Speaker(800, 600, (0.5, 0.3), 340);
        var many = Enumerable.Repeat(ConversationCards.Support, 8).ToList();

        var layout = ConversationLayout.Compute(800, 600, head, bubble, panel, many);

        var shown = layout.Cards.TakeWhile(c => c.State != CardPresence.Hidden).ToList();
        Assert.NotEmpty(shown);
        Assert.True(shown.Count < many.Count, "not all eight fit");
        Assert.All(layout.Cards.Skip(shown.Count), c => Assert.Equal(CardPresence.Hidden, c.State)); // the hidden ones are the oldest, at the end
        Assert.All(shown.Skip(1), c => Assert.Equal(CardPresence.Collapsed, c.State));
        for (var i = 1; i < shown.Count; i++)
        {
            Assert.True(shown[i].Box.Bottom <= shown[i - 1].Box.Y, "each older card stands above the newer one");
        }

        Assert.Equal(ConversationLayout.CollapsedHeight, shown[^1].Box.Height);
        output.WriteLine($"8 support cards at 800x600: {shown.Count} shown, {shown.Count(c => c.State == CardPresence.Collapsed)} collapsed");
    }

    [Fact]
    public void TheNewestCardsStayWholeWhileTheOlderOnesCollapse()
    {
        var panel = DialogueLayout.PanelBox(1280, 720);
        var (head, bubble) = Speaker(1280, 720, (0.5, 0.25), 200);

        var layout = ConversationLayout.Compute(1280, 720, head, bubble, panel, Pattern);

        var states = layout.Cards.Select(c => c.State).ToList();
        output.WriteLine(string.Join(", ", layout.Cards.Select(c => $"{c.Kind}:{c.State}")));
        Assert.DoesNotContain(CardPresence.Hidden, states);
        Assert.Equal(CardPresence.Full, states[0]); // the toast, the newest
        var firstCollapsed = states.FindIndex(s => s == CardPresence.Collapsed);
        if (firstCollapsed >= 0)
        {
            Assert.All(states.Skip(firstCollapsed), s => Assert.Equal(CardPresence.Collapsed, s)); // never a whole card above a line
        }
    }

    [Fact]
    public void TheFacilitatorsLabelHasItsOwnPlaceClearOfThePanelTheCardsAndTheBubble()
    {
        const int labelText = 230; // about the width of "F10: end the run (facilitator)"
        foreach (var (width, height) in Sizes)
        {
            var panel = DialogueLayout.PanelBox(width, height);
            var label = ConversationLayout.FacilitatorLabel(labelText);
            Assert.False(ConversationLayout.Overlaps(label, panel), "not over the dialogue panel or its footer");
            Assert.True(label.X >= 0 && label.Y >= 0 && label.Right <= width && label.Bottom <= height);
            Assert.True(label.Bottom <= ConversationLayout.ColumnTop, "above the card column");

            foreach (var spot in HeadSpots)
            {
                var x = spot.X * width;
                var y = spot.Y * height;
                var bubble = DialogueBubble.Place(width, height, x, y, 300, 70, ConversationLayout.BubbleTop)!.Value;
                Assert.False(ConversationLayout.Overlaps(label, bubble.Box), $"{width}x{height} head {spot}: the bubble keeps below the label");
            }

            var head = ConversationCards.HeadRect(width / 2.0, height * 0.25, (height * 0.25) + 110);
            var layout = ConversationLayout.Compute(width, height, head, null, panel, Pattern);
            Assert.All(layout.Cards.Where(c => c.State != CardPresence.Hidden), c => Assert.False(ConversationLayout.Overlaps(label, c.Box)));
        }
    }

    [Fact]
    public void TheBeaconLabelStaysInsideTheWindowClearOfThePanelAndTheSpeakerAndTheCardsKeepClearOfIt()
    {
        // (right, up) of an off-screen target in the camera's frame: right, left, up, down, and the corners.
        var directions = new (double Right, double Up)[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1), (1, 0.2), (-1, -0.2) };
        foreach (var (width, height) in Sizes)
        {
            var panel = DialogueLayout.PanelBox(width, height);
            var (head, bubble) = Speaker(width, height, (0.5, 0.25), 300);
            var speaker = ConversationLayout.Union(head, bubble);
            foreach (var (right, up) in directions)
            {
                var arrow = EdgeArrow.For(new ProjectedPoint(0, 0, -1, right, up), width, height, 56);
                Assert.False(arrow.OnScreen);
                foreach (var textWidth in new[] { 120.0, 190.0 }) // "campamento 111 blocks" is about 190 px
                {
                    var label = BeaconLabel.Place(width, height, arrow.X, arrow.Y, textWidth, [panel, speaker]);
                    var where = $"{width}x{height} target ({right}, {up}), arrow ({arrow.X:0}, {arrow.Y:0})";
                    Assert.True(label.X >= 0 && label.Y >= 0 && label.Right <= width && label.Bottom <= height, $"{where}: label {label} leaves the window");
                    Assert.False(ConversationLayout.Overlaps(label, panel), $"{where}: label {label} overlaps the panel");
                    Assert.False(ConversationLayout.Overlaps(label, speaker), $"{where}: label {label} overlaps the speaker");

                    var layout = ConversationLayout.Compute(width, height, head, bubble, panel, Pattern, [label]);
                    Assert.Contains(layout.Cards, c => c.State != CardPresence.Hidden);
                    Assert.All(layout.Cards.Where(c => c.State != CardPresence.Hidden), c => Assert.False(ConversationLayout.Overlaps(c.Box, label), $"{where}: a card overlaps the label"));
                }
            }
        }
    }

    [Fact]
    public void TheHeadRectangleIsCentredOnTheHeadAndNeverTiny()
    {
        var head = ConversationCards.HeadRect(640, 200, 300);
        Assert.Equal(200, head.Y);
        Assert.Equal(100, head.Height);
        Assert.Equal(640, head.X + (head.Width / 2));
        var far = ConversationCards.HeadRect(640, 200, 205);
        Assert.True(far.Width >= 48 && far.Height >= 48);
    }
}
