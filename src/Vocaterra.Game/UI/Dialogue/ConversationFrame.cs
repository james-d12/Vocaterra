using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.UI.Dialogue;

/// <summary>
/// What the conversation screen looks like this frame (S22): the speaker's projected head, the bubble over it, the dialogue panel and where every card
/// stands, from <see cref="ConversationLayout"/>. The bubble layer, the support layer and the new-word toast each ask for it, so they agree on the places.
/// </summary>
public sealed class ConversationFrame
{
    private ConversationFrame()
    {
    }

    /// <summary>The speaker's head rectangle, or <see langword="null"/> when the head is not on the screen.</summary>
    public PixelBox? Head { get; private init; }

    /// <summary>The bubble's place, or <see langword="null"/> when there is none.</summary>
    public BubbleLayout? Bubble { get; private init; }

    /// <summary>The rows of the bubble's line.</summary>
    public IReadOnlyList<string> BubbleRows { get; private init; } = [];

    /// <summary>The beacon's edge label while a talk is open (clear of the panel, the head and the bubble; the cards keep clear of it), or <see langword="null"/> when there is none.</summary>
    public PixelBox? BeaconLabel { get; private init; }

    /// <summary>The layout of the cards.</summary>
    public required ConversationLayout Layout { get; init; }

    /// <summary>The new-word toast's placement, or <see langword="null"/> when no toast is up.</summary>
    public CardPlacement? NewWord { get; private init; }

    /// <summary>The support cards' placements, newest first.</summary>
    public IReadOnlyList<CardPlacement> Support { get; private init; } = [];

    /// <summary>The key-word cards' placements.</summary>
    public IReadOnlyList<CardPlacement> KeyWords { get; private init; } = [];

    /// <summary>The audio-only chip's placement, or <see langword="null"/>.</summary>
    public CardPlacement? Chip { get; private init; }

    /// <summary>Whether the key words stand alone (large) before the question.</summary>
    public bool KeyWordsAlone { get; private init; }

    /// <summary>Computes the frame for a view whose dialogue panel is open.</summary>
    /// <param name="view">The frame's view.</param>
    public static ConversationFrame For(FrameView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var width = view.ViewportWidth;
        var height = view.ViewportHeight;
        var panel = DialogueLayout.PanelBox(width, height);
        var dialogue = view.Dialogue;
        PixelBox? head = null;
        BubbleLayout? bubble = null;
        IReadOnlyList<string> rows = [];
        var speaker = view.Figures.FirstOrDefault(f => string.Equals(f.Name, dialogue.Npc, StringComparison.Ordinal));
        if (dialogue.IsOpen && speaker is not null)
        {
            var top = view.Camera3D.Screen.Project(speaker.Feet.X + 0.5, speaker.Feet.Y + 2.1, speaker.Feet.Z + 0.5);
            if (top.InFront)
            {
                var chest = view.Camera3D.Screen.Project(speaker.Feet.X + 0.5, speaker.Feet.Y + 1.1, speaker.Feet.Z + 0.5);
                head = ConversationCards.HeadRect(top.X, top.Y, chest.Y);
                if (DialogueBubble.LineOf(dialogue) is { } line)
                {
                    var font = view.Fonts.Get(FontProvider.NativeSize);
                    rows = DialogueBubble.Rows(line, DialogueBubble.MaxWidth - (2 * DialogueBubble.Padding), t => DrawHelpers.Width(font, t));
                    var widest = rows.Max(r => DrawHelpers.Width(font, r));
                    var bubbleWidth = (int)Math.Min(DialogueBubble.MaxWidth, Math.Max(widest, DrawHelpers.Width(font, dialogue.NpcName)) + (2 * DialogueBubble.Padding));
                    var bubbleHeight = (2 * DialogueBubble.Padding) + ((rows.Count + 1) * DialogueLayout.LineHeight);
                    var inset = view.Playtest ? ConversationLayout.BubbleTop : DialogueBubble.Margin;
                    if (view.LessonStripBox is { } stripBox)
                    {
                        inset = Math.Max(inset, stripBox.Bottom + ConversationLayout.Gap); // the bubble stands under the lesson strip, not over it (X08)
                    }

                    bubble = DialogueBubble.PlaceBetween(width, height, top.X, top.Y, bubbleWidth, bubbleHeight, inset, panel.Y - ConversationLayout.Gap);
                }
            }
        }

        var session = view.Session;
        var keyAlone = session.KeyWordsPending && session.KeyWordCards.Count > 0;
        var requests = new List<ConversationCardRequest>();
        var showToast = view.Cards.Current(view.Tick) is not null;
        if (showToast)
        {
            requests.Add(ConversationCards.NewWord);
        }

        var supportCount = session.Support.Cards.Count;
        requests.AddRange(Enumerable.Repeat(ConversationCards.Support, supportCount));
        requests.AddRange(Enumerable.Repeat(ConversationCards.KeyWord(keyAlone), session.KeyWordCards.Count));
        var chipUp = session.Support.AudioOnly.Count > 0 && supportCount == 0;
        if (chipUp)
        {
            requests.Add(ConversationCards.Chip);
        }

        PixelBox? bubbleBox = bubble is { } b ? new PixelBox(b.Box.X, b.Box.Y, b.Box.Width, b.Box.Height + DialogueBubble.TailHeight) : null;

        // The beacon's label is placed first, clear of the panel, the head and the bubble; the cards then keep clear of it (S22).
        var labelAvoid = new List<PixelBox> { panel };
        if (view.LessonStripBox is { } stripToAvoid)
        {
            labelAvoid.Add(stripToAvoid);
        }

        var labelSoft = new List<PixelBox>();
        if (head is { } headBox)
        {
            labelSoft.Add(ConversationLayout.Union(headBox, bubbleBox));
        }

        var beaconLabel = BeaconArrowLayer.LabelFor(view, labelAvoid, labelSoft)?.Box;
        var extraClear = new List<PixelBox>();
        if (beaconLabel is { } label)
        {
            extraClear.Add(label);
        }

        if (view.LessonStripBox is { } strip)
        {
            extraClear.Add(strip);
        }

        var layout = ConversationLayout.Compute(width, height, head, bubbleBox, panel, requests, extraClear);
        var placements = layout.Cards;
        var at = 0;
        CardPlacement? toast = showToast ? placements[at++] : null;
        var support = placements.Skip(at).Take(supportCount).ToList();
        at += supportCount;
        var keys = placements.Skip(at).Take(session.KeyWordCards.Count).ToList();
        at += keys.Count;
        CardPlacement? chip = chipUp ? placements[at] : null;
        return new ConversationFrame
        {
            Head = head,
            Bubble = bubble,
            BubbleRows = rows,
            Layout = layout,
            BeaconLabel = beaconLabel,
            NewWord = toast,
            Support = support,
            KeyWords = keys,
            Chip = chip,
            KeyWordsAlone = keyAlone,
        };
    }
}
