namespace Vocaterra.MonoGame.UI.Dialogue;

/// <summary>The kinds of card that stand over the dialogue panel (S22): what the conversation layout places.</summary>
public enum ConversationCardKind
{
    /// <summary>The small toast of a first pickup (<c>New word</c> and the noun).</summary>
    NewWord,

    /// <summary>A support card (<c>Puedes decir</c>: the phrase the NPC is asking for).</summary>
    Support,

    /// <summary>A key-word card of the stall (<c>el agua</c>).</summary>
    KeyWord,

    /// <summary>The slim chip of the audio-only support level.</summary>
    Chip,
}

/// <summary>A card asking for room: its full size, the narrowest it is drawn, and the width of its one-line form.</summary>
/// <param name="Kind">The kind of card.</param>
/// <param name="Width">The width of the full card.</param>
/// <param name="Height">The height of the full card.</param>
/// <param name="MinWidth">The narrowest the full card is laid out; a zone narrower than this takes the card collapsed or not at all.</param>
/// <param name="CollapsedWidth">The width of the card as one line.</param>
public readonly record struct ConversationCardRequest(ConversationCardKind Kind, int Width, int Height, int MinWidth, int CollapsedWidth);

/// <summary>How a card is shown: whole, as one line, or not at all (no room even for the line).</summary>
public enum CardPresence
{
    /// <summary>The whole card.</summary>
    Full,

    /// <summary>One line: the phrase and no more.</summary>
    Collapsed,

    /// <summary>Not drawn: there is no room.</summary>
    Hidden,
}

/// <summary>Where a card stands: its kind, how it is shown and its rectangle (meaningless when hidden).</summary>
/// <param name="Kind">The kind of card.</param>
/// <param name="State">Whether it is whole, a line or hidden.</param>
/// <param name="Box">The card's rectangle in window pixels.</param>
public readonly record struct CardPlacement(ConversationCardKind Kind, CardPresence State, PixelBox Box);

/// <summary>
/// The layout of the conversation screen (S22). Before it the support cards, the key-word cards and the new-word card stood in a row over the dialogue
/// panel, on top of the speaker and under the speech bubble (S15 logged it: at the stall the cards hid the shopkeeper). This model decides where every card
/// goes from the window, the speaker's projected head rectangle, the bubble, the dialogue panel and the cards asking for room. Plain numbers, no window and
/// no fonts, so a test proves the rule: no card overlaps the head, the bubble, the panel, another card or the window's edge.
/// <para>
/// The rule: the head and the bubble make one keep-clear box (with a gap around it). The cards stack above the panel, in a column at the side of that box
/// (the wider side, then the other), or in the strip between the box and the panel. The request list is newest first: the first card stands nearest the
/// panel, the next ones above it, small cards sharing a row. When the whole cards do not fit, every card collapses to one line, newest at the bottom; when
/// even the lines do not fit, the oldest cards (the end of the list) are hidden.
/// </para>
/// </summary>
public sealed class ConversationLayout
{
    /// <summary>The gap between cards, to the keep-clear box and to the panel.</summary>
    public const int Gap = 8;

    /// <summary>The distance kept from the window's edge.</summary>
    public const int Margin = 8;

    /// <summary>The top of a card column: below the facilitator's label and the objective strip.</summary>
    public const int ColumnTop = 64;

    /// <summary>The widest a column of cards is.</summary>
    public const int MaxColumnWidth = 440;

    /// <summary>The height of a card collapsed to one line.</summary>
    public const int CollapsedHeight = 26;

    /// <summary>The narrowest a collapsed line is laid out.</summary>
    public const int CollapsedMinWidth = 120;

    /// <summary>The height of the facilitator's label in a playtest.</summary>
    public const int FacilitatorHeight = 24;

    private ConversationLayout(IReadOnlyList<CardPlacement> cards, PixelBox? zone, PixelBox? keepClear)
    {
        Cards = cards;
        Zone = zone;
        KeepClear = keepClear;
    }

    /// <summary>The placements, in the order of the requests.</summary>
    public IReadOnlyList<CardPlacement> Cards { get; }

    /// <summary>The zone the cards stand in (a side column or the strip), or <see langword="null"/> when there are no cards or none fits.</summary>
    public PixelBox? Zone { get; }

    /// <summary>The keep-clear box: the head and the bubble together, or <see langword="null"/> when neither is on the screen.</summary>
    public PixelBox? KeepClear { get; }

    /// <summary>The top of the bubble in a playtest: below the facilitator's label.</summary>
    public static int BubbleTop => FacilitatorLabel(0).Bottom + Gap;

    /// <summary>
    /// The facilitator's label of a playtest (S22): top left, where the debug overlay would be (a playtest has none) and above <see cref="ColumnTop"/>, so it
    /// overlaps neither the dialogue footer (it did at the bottom left) nor the cards.
    /// </summary>
    /// <param name="textWidth">The width of the label's text.</param>
    public static PixelBox FacilitatorLabel(int textWidth) => new(12, 12, textWidth + 16, FacilitatorHeight);

    /// <summary>The smallest box that holds both.</summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box, or <see langword="null"/>.</param>
    public static PixelBox Union(PixelBox a, PixelBox? b)
    {
        if (b is not { } other)
        {
            return a;
        }

        var x = Math.Min(a.X, other.X);
        var y = Math.Min(a.Y, other.Y);
        return new PixelBox(x, y, Math.Max(a.Right, other.Right) - x, Math.Max(a.Bottom, other.Bottom) - y);
    }

    /// <summary>Whether two boxes share any pixel.</summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box.</param>
    public static bool Overlaps(PixelBox a, PixelBox b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    /// <summary>Lays the cards out.</summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="head">The speaker's projected head rectangle, or <see langword="null"/> when it is off the screen.</param>
    /// <param name="bubble">The speech bubble's box (with its pointer), or <see langword="null"/> when there is none.</param>
    /// <param name="panel">The dialogue panel's box.</param>
    /// <param name="cards">The cards, newest first.</param>
    /// <param name="extraClear">Further rectangles the cards keep clear of (the beacon's label, S22), or <see langword="null"/>.</param>
    public static ConversationLayout Compute(int viewportWidth, int viewportHeight, PixelBox? head, PixelBox? bubble, PixelBox panel, IReadOnlyList<ConversationCardRequest> cards, IReadOnlyList<PixelBox>? extraClear = null)
    {
        ArgumentNullException.ThrowIfNull(cards);
        PixelBox? clear = head is { } h ? Union(h, bubble) : bubble;
        var hidden = cards.Select(c => new CardPlacement(c.Kind, CardPresence.Hidden, default)).ToList();
        if (cards.Count == 0)
        {
            return new ConversationLayout(hidden, null, clear);
        }

        var floor = panel.Y - Gap;
        var zones = ZonesFor(viewportWidth, floor, clear);
        (List<CardPlacement> Placed, int Shown, PixelBox Zone)? best = null;
        for (var fullCount = cards.Count; fullCount >= 0; fullCount--)
        {
            foreach (var zone in zones)
            {
                var placed = Pack(zone, cards, fullCount, extraClear ?? []);
                var shown = placed.Count(p => p.State != CardPresence.Hidden);
                if (shown == cards.Count)
                {
                    return new ConversationLayout(placed, zone, clear);
                }

                if (fullCount == 0 && (best is null || shown > best.Value.Shown))
                {
                    best = (placed, shown, zone);
                }
            }
        }

        return best is { Shown: > 0 } b ? new ConversationLayout(b.Placed, b.Zone, clear) : new ConversationLayout(hidden, null, clear);
    }

    private static List<PixelBox> ZonesFor(int viewportWidth, int floor, PixelBox? clear)
    {
        var zones = new List<PixelBox>();
        var height = Math.Max(0, floor - ColumnTop);
        if (clear is not { } box)
        {
            var width = Math.Min(MaxColumnWidth, viewportWidth - (2 * Margin));
            zones.Add(new PixelBox(viewportWidth - Margin - width, ColumnTop, width, height));
            return zones;
        }

        var leftRoom = box.X - Gap - Margin;
        var rightRoom = viewportWidth - Margin - (box.Right + Gap);
        var left = new PixelBox(Margin, ColumnTop, Math.Min(MaxColumnWidth, Math.Max(0, leftRoom)), height);
        var rightWidth = Math.Min(MaxColumnWidth, Math.Max(0, rightRoom));
        var right = new PixelBox(viewportWidth - Margin - rightWidth, ColumnTop, rightWidth, height);
        zones.AddRange(rightRoom >= leftRoom ? [right, left] : [left, right]);
        var stripTop = box.Bottom + Gap;
        zones.Add(new PixelBox(Margin, stripTop, viewportWidth - (2 * Margin), Math.Max(0, floor - stripTop)));
        return zones;
    }

    // The first fullCount cards (the newest) stand whole, the others as one line each.
    private static List<CardPlacement> Pack(PixelBox zone, IReadOnlyList<ConversationCardRequest> cards, int fullCount, IReadOnlyList<PixelBox> extraClear)
    {
        var placed = new List<CardPlacement>();
        var bottom = zone.Bottom;
        var index = 0;
        var blocked = false;
        while (index < cards.Count && !blocked)
        {
            var row = new List<(ConversationCardRequest Card, int Width, int Height, bool Collapsed)>();
            var used = 0;
            while (index < cards.Count)
            {
                var card = cards[index];
                var collapsed = index >= fullCount;
                var width = Math.Min(collapsed ? card.CollapsedWidth : card.Width, zone.Width);
                var minimum = collapsed ? Math.Min(CollapsedMinWidth, card.CollapsedWidth) : card.MinWidth;
                var height = collapsed && card.Kind != ConversationCardKind.Chip ? CollapsedHeight : card.Height;
                if (width < minimum)
                {
                    blocked = true;
                    break;
                }

                // A collapsed line stands alone in its row; a whole card shares a row while it fits.
                var fitsRow = row.Count == 0 || (!collapsed && used + width <= zone.Width);
                if (!fitsRow)
                {
                    break;
                }

                row.Add((card, width, height, collapsed));
                used += width + Gap;
                index++;
            }

            if (row.Count == 0)
            {
                break;
            }

            var rowHeight = row.Max(c => c.Height);
            var rowBox = new PixelBox(zone.X, bottom - rowHeight, row.Sum(c => c.Width + Gap) - Gap, rowHeight);
            if (rowBox.Y < zone.Y || extraClear.Any(c => Overlaps(rowBox, c)))
            {
                index -= row.Count;
                break;
            }

            var x = zone.X;
            foreach (var (card, width, height, collapsed) in row)
            {
                placed.Add(new CardPlacement(card.Kind, collapsed ? CardPresence.Collapsed : CardPresence.Full, new PixelBox(x, bottom - height, width, height)));
                x += width + Gap;
            }

            bottom -= rowHeight + Gap;
        }

        for (var i = placed.Count; i < cards.Count; i++)
        {
            placed.Add(new CardPlacement(cards[i].Kind, CardPresence.Hidden, default));
        }

        return placed;
    }
}
