using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The book (J, or the book item): an open book over the scene in the panel style (S15), with five pages turned with Tab, Left or Right or a click
/// (S20): Nouns, Verbs, Phrases, Lessons and What I said. Its lines are built by <see cref="BookPages"/> from the learner's record; this layer only draws
/// them: the tone, the icon of a noun, the pips, the lamps, the tick boxes and the right-hand text, with the scroll (the wheel, Page Up and Page Down).
/// A row with a recording has a speaker button at its right (S20b), and the selected row is marked; the keys and the clicks are read by the game.
/// It covers the screen while it is open and takes the keyboard (the player does not walk).
/// </summary>
public sealed class BookLayer : IFrameLayer
{
    private const int IconSize = 16;
    private const int IndentWidth = 24;
    private const int Pip = 10;
    private const int WordPips = Vocaterra.Game.WordProgress.UsesToKnow;
    private const int PipGap = 6;
    private const int Gutter = 10;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Known = new(255, 236, 160);
    private static readonly Color Learning = new(235, 235, 235);
    private static readonly Color Seen = new(190, 190, 200);
    private static readonly Color Unseen = new(110, 110, 120);
    private static readonly Color Hints = new(140, 140, 150);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.Book.IsOpen)
        {
            return;
        }

        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var viewport = new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight);
        spriteBatch.Draw(pixel, viewport, new Color(6, 6, 14, 236));

        var geometry = new BookGeometry(view.ViewportWidth, view.ViewportHeight);
        var page = view.Book.Page;
        var panel = ToRectangle(geometry.Panel);
        spriteBatch.Draw(pixel, panel, new Color(18, 18, 34, 250));
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Gold);

        var left = panel.Left + BookGeometry.Padding;
        var right = panel.Right - BookGeometry.Padding;
        DrawHelpers.Text(spriteBatch, font, BookTabs.Title, new Vector2(left, geometry.TitleTop), Gold);
        var summary = SummaryOf(view, page);
        DrawHelpers.Text(spriteBatch, font, summary, new Vector2(right - DrawHelpers.Width(font, summary), geometry.TitleTop), Known);
        DrawTabs(spriteBatch, pixel, font, geometry, page);
        spriteBatch.Draw(pixel, new Rectangle(left, geometry.RuleTop, right - left, 1), new Color(255, 214, 64, 120));

        var lines = BookPages.ForPage(view.Session, page, view.Recordings);
        var visible = geometry.LinesVisible;
        view.Book.ClampTo(lines.Count, visible);
        var offset = view.Book.Offset;
        var textWidth = right - left;
        var selected = SelectedLine(view, lines);
        var y = geometry.ContentTop;
        for (var i = offset; i < lines.Count && i < offset + visible; i++)
        {
            DrawLine(spriteBatch, view, font, lines[i], left, right, y, textWidth, i == selected);
            y += BookGeometry.LineHeight;
        }

        if (lines.Count > visible)
        {
            var first = offset + 1;
            var last = Math.Min(lines.Count, offset + visible);
            var position = $"lines {first} to {last} of {lines.Count}   wheel, Page Up, Page Down";
            DrawHelpers.Text(spriteBatch, font, position, new Vector2(right - DrawHelpers.Width(font, position), panel.Bottom - 50), Hints);
        }

        DrawHelpers.Text(spriteBatch, font, BookTabs.KeyHints, new Vector2(left, panel.Bottom - 30), Hints);
    }

    // The tab row: the page on screen is bright, the others dim.
    private static void DrawTabs(SpriteBatch spriteBatch, Texture2D pixel, FontStashSharp.SpriteFontBase font, BookGeometry geometry, BookPage page)
    {
        foreach (var tab in Enum.GetValues<BookPage>())
        {
            var label = BookTabs.LabelOf(tab);
            var rect = geometry.TabRect(tab);
            DrawHelpers.Text(spriteBatch, font, label, new Vector2(rect.X + 6, geometry.TabsTop), tab == page ? Known : Hints);
            if (tab == page)
            {
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 6, geometry.TabsTop + 18, (int)DrawHelpers.Width(font, label), 2), Gold);
            }
        }
    }

    private static string SummaryOf(FrameView view, BookPage page)
    {
        var session = view.Session;
        return page switch
        {
            BookPage.Nouns => $"Known: {session.Words.Catalogue.Nouns.Count(noun => session.Words.RecordOf(noun.Lemma).Known)} of {session.Words.Catalogue.Nouns.Count}   ·   Session {session.PlaySession}",
            BookPage.Phrases => $"Without help: {PhraseWithoutHelp(view)}   ·   Session {session.PlaySession}",
            _ => $"Session {session.PlaySession}",
        };
    }

    private static int PhraseWithoutHelp(FrameView view)
    {
        var rows = PhraseJournalRows.Build(view.Session.PhraseProgress, view.Session.StoredTime.UnixSeconds, view.Session.Credit);
        return rows.Count(static r => r is { HasScaffold: true, Level: SupportLevel.None });
    }

    // The line index of the selected speakable row (S20b), or -1 when the page has no row with a recording.
    private static int SelectedLine(FrameView view, IReadOnlyList<BookLine> lines)
    {
        var rows = BookSpeakers.Speakable(lines);
        var index = view.Book.SelectedIndex(rows.Count);
        return index < 0 ? -1 : rows[index];
    }

    private static void DrawLine(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, BookLine line, int left, int right, int top, int textWidth, bool selected)
    {
        var pixel = view.Pixel;
        if (selected)
        {
            spriteBatch.Draw(pixel, new Rectangle(left, top, right - left, BookGeometry.LineHeight - 1), new Color(70, 56, 12));
        }

        // The speaker button sits at the right edge; the text before it keeps clear of it.
        var rightEdge = right;
        if (line.Speak is not null)
        {
            DrawSpeaker(spriteBatch, pixel, new Rectangle(right - BookSpeakers.ButtonSize, top + 2, BookSpeakers.ButtonSize, BookSpeakers.ButtonSize), selected);
            rightEdge = right - BookSpeakers.ButtonSize - BookSpeakers.Gap;
        }

        var x = left + (line.Indent ? IndentWidth : 0);
        if (line.Tick is { } ticked)
        {
            DrawTick(spriteBatch, pixel, new Rectangle(x, top + 4, 11, 11), ticked);
            x += 18;
        }

        if (line.Lemma is { } lemma)
        {
            DrawNounIcon(spriteBatch, view, lemma, new Rectangle(x, top + 2, IconSize, IconSize));
            x += IconSize + 6;
        }

        // The right-hand part: the pips or the lamps, then the text before them.
        if (line.Pips is { } pips)
        {
            var pipLeft = rightEdge - ((WordPips * Pip) + ((WordPips - 1) * PipGap));
            for (var i = 0; i < WordPips; i++)
            {
                var box = new Rectangle(pipLeft + (i * (Pip + PipGap)), top + 5, Pip, Pip);
                if (i < pips)
                {
                    spriteBatch.Draw(pixel, box, Gold);
                }
                else
                {
                    DrawHelpers.Border(spriteBatch, pixel, box, 1, Hints);
                }
            }

            rightEdge = pipLeft - Gutter;
        }
        else if (line.Lamps is { } lamps)
        {
            var lampsLeft = rightEdge - ((lamps.Count * Pip) + ((lamps.Count - 1) * 4));
            for (var i = 0; i < lamps.Count; i++)
            {
                DrawLamp(spriteBatch, pixel, new Rectangle(lampsLeft + (i * (Pip + 4)), top + 5, Pip, Pip), lamps[i]);
            }

            rightEdge = lampsLeft - Gutter;
        }

        var reserved = 0;
        if (line.Right is { } rightText)
        {
            var fitted = Fit(font, rightText, textWidth / 2);
            var width = (int)DrawHelpers.Width(font, fitted);
            DrawHelpers.Text(spriteBatch, font, fitted, new Vector2(rightEdge - width, top + 2), line.Tone == BookTone.Gold ? Gold : Hints);
            reserved = width + Gutter;
        }

        var maxText = Math.Max(40, rightEdge - x - reserved - Gutter);
        var text = Fit(font, line.Text, maxText);
        DrawHelpers.Text(spriteBatch, font, text, new Vector2(x, top + 2), ToneColour(line.Tone));
        if (line.Aside is { } aside)
        {
            var asideX = x + (int)DrawHelpers.Width(font, text) + Gutter;
            var asideText = Fit(font, aside, Math.Max(20, rightEdge - asideX - reserved - Gutter));
            DrawHelpers.Text(spriteBatch, font, asideText, new Vector2(asideX, top + 2), Hints);
        }
    }

    // The speaker button (S20b): a box with a speaker drawn in pixels; gold when the row is selected.
    private static void DrawSpeaker(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, bool selected)
    {
        var ink = selected ? Gold : Seen;
        DrawHelpers.Border(spriteBatch, pixel, box, 1, selected ? Gold : Hints);
        var x = box.X;
        var y = box.Y;
        spriteBatch.Draw(pixel, new Rectangle(x + 3, y + 6, 3, 4), ink); // the body
        spriteBatch.Draw(pixel, new Rectangle(x + 6, y + 4, 2, 8), ink); // the cone
        spriteBatch.Draw(pixel, new Rectangle(x + 8, y + 2, 2, 12), ink);
        spriteBatch.Draw(pixel, new Rectangle(x + 11, y + 5, 1, 6), ink); // the sound
        spriteBatch.Draw(pixel, new Rectangle(x + 13, y + 3, 1, 10), ink);
    }

    private static void DrawNounIcon(SpriteBatch spriteBatch, FrameView view, string lemma, Rectangle icon)
    {
        var noun = view.Session.Nouns.ForLemma(lemma);

        // The colour rule of --greyscale (S22): a noun that is not known yet has a grey icon, a known one is in colour; with the switch off every icon is in colour.
        var saturation = NounIcons.Saturation(view.Greyscale, view.Session.Words.RecordOf(lemma).Known);
        if (noun is { Items.Count: > 0 })
        {
            view.VoxelIcons.DrawIcon(spriteBatch, noun.Items[0], icon, saturation);
        }
        else if (noun is { Blocks.Count: > 0 })
        {
            // A noun that names a block and no item (the tree, the river) shows the block's side, grey or in colour by the same rule (it was a dark square).
            view.VoxelIcons.DrawBlock(spriteBatch, noun.Blocks[0], icon, saturation);
        }
        else
        {
            spriteBatch.Draw(view.Pixel, icon, new Color(40, 40, 50));
        }
    }

    private static void DrawTick(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, bool ticked)
    {
        DrawHelpers.Border(spriteBatch, pixel, box, 1, ticked ? Gold : Hints);
        if (ticked)
        {
            // A tick: a short stroke down to the corner and a longer one up.
            spriteBatch.Draw(pixel, new Rectangle(box.X + 2, box.Y + 6, 2, 2), Gold);
            spriteBatch.Draw(pixel, new Rectangle(box.X + 4, box.Y + 7, 2, 2), Gold);
            spriteBatch.Draw(pixel, new Rectangle(box.X + 6, box.Y + 5, 2, 2), Gold);
            spriteBatch.Draw(pixel, new Rectangle(box.X + 8, box.Y + 3, 2, 2), Gold);
        }
    }

    private static void DrawLamp(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, bool lit)
    {
        if (lit)
        {
            spriteBatch.Draw(pixel, box, Gold);
        }
        else
        {
            DrawHelpers.Border(spriteBatch, pixel, box, 1, Hints);
        }
    }

    // Shortens a text to fit a width with "..." at its end (a line never runs over its neighbour).
    private static string Fit(FontStashSharp.SpriteFontBase font, string text, int maxWidth)
    {
        if (DrawHelpers.Width(font, text) <= maxWidth)
        {
            return text;
        }

        var cut = text.Length;
        while (cut > 0 && DrawHelpers.Width(font, text[..cut] + "...") > maxWidth)
        {
            cut--;
        }

        return text[..cut] + "...";
    }

    private static Color ToneColour(BookTone tone) => tone switch
    {
        BookTone.Known => Known,
        BookTone.Learning => Learning,
        BookTone.Seen => Seen,
        BookTone.Unseen => Unseen,
        BookTone.Gold => Gold,
        _ => Hints,
    };

    private static Rectangle ToRectangle(BookRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
