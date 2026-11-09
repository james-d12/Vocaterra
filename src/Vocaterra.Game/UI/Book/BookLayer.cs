using System.Globalization;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Book;

/// <summary>
/// The book (J, or the book item), remade as a book (S28): an open two-page spread on paper with a spine and binding, page edges and a cover, five ribbon
/// bookmarks along the top edge (Nouns, Verbs, Phrases, Lessons, What I said) and a short page-turn leaf. Everything is drawn in code from rectangles (D-game-5).
/// The left page lists (the lines <see cref="BookPages"/> builds from the learner's record, scrolled with the wheel, Page Up and Page Down, a speaker button at the
/// right of a row that has a recording, S20b); the right page shows the selected item: on the Verbs page its conjugation table (<see cref="ConjugationTable"/>),
/// elsewhere the item's own lines, written out in full. It covers the screen while it is open and takes the keyboard (the player does not walk).
/// The hierarchy (U07, D-game-28): only two sizes, 16 and 32 px. The left page's title (32 px, the ribbon's colour, a 2 px rule under it) names the page; the list draws
/// by line kind (<see cref="BookLineKind"/>): section bands in the page's accent, entry rows in the ink of their tone, a row of grey tiles for the nouns not met, and the
/// selection with an accent edge. The right page writes the selected entry's block: its title at 32 px, labels with a rule, numbers at 32 px over their captions. The "strong"
/// weight (the text drawn twice, one pixel apart) is for known entries, the right page's labels and the next lesson's line only, never running text.
/// </summary>
public sealed class BookLayer : IFrameLayer
{
    private const int IconSize = 16;
    /// <summary>How far a note under a form is indented on the right page, in canvas pixels.</summary>
    public const int IndentWidth = 24;
    private const int Pip = 10;
    private const int WordPips = WordProgress.UsesToKnow;
    private const int PipGap = 6;
    /// <summary>The gap between a row's parts, and at its right end, in canvas pixels.</summary>
    public const int Gutter = 10;
    private const int NotchDepth = 9;

    // The narrowest a row's right-hand note may be and still be read (X03); narrower, the note is left off.
    private const int MinNoteWidth = 90;

    // The narrowest an entry's aside may be and still be drawn (U07); narrower, it is left off.
    private const int MinAsideWidth = 45;

    // The cover, the paper and the ink (all drawn in code).
    private static readonly Color CoverDark = new(70, 38, 28);
    private static readonly Color Cover = new(104, 58, 40);
    private static readonly Color CoverLight = new(132, 80, 54);
    private static readonly Color Brass = new(206, 164, 72);
    private static readonly Color Paper = new(238, 227, 200);
    private static readonly Color PaperShade = new(214, 200, 168);
    private static readonly Color PageEdge = new(247, 240, 222);
    private static readonly Color PageEdgeLine = new(196, 182, 150);
    private static readonly Color Ink = UiPalette.Ink.ToColor();
    private static readonly Color InkKnown = UiPalette.InkKnown.ToColor();
    private static readonly Color InkSeen = UiPalette.InkSeen.ToColor();
    private static readonly Color InkUnseen = UiPalette.InkUnseen.ToColor();
    private static readonly Color InkMeta = UiPalette.InkMeta.ToColor();
    private static readonly Color BandMuted = UiPalette.BandMuted.ToColor();
    private static readonly Color MarkGood = UiPalette.MarkGood.ToColor();
    private static readonly Color MarkSlip = UiPalette.MarkSlip.ToColor();
    private static readonly Color MarkMiss = UiPalette.MarkMiss.ToColor();
    private static readonly Color Select = new(246, 218, 128);
    private static readonly Color Highlight = new(250, 214, 96);
    private static readonly Color Cream = new(238, 220, 176);
    private static readonly Color Dim = new(6, 6, 14, 236);

    private static readonly Color[] Ribbons = UiPalette.Ribbons.Select(static c => c.ToColor()).ToArray(); // nouns, verbs, phrases, lessons, what I said

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.Book.IsOpen)
        {
            return;
        }

        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var big = view.Fonts.Get(FontProvider.NativeSize * 2);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), Dim);

        var geometry = new BookGeometry(view.ViewportWidth, view.ViewportHeight);
        var page = view.Book.Page;
        var accent = Ribbons[(int)page];
        DrawCover(spriteBatch, pixel, geometry);
        DrawPaper(spriteBatch, pixel, geometry);

        // The headings: the page's name on the left page (32 px, in the ribbon's colour, a 2 px rule of the same colour under it), the summary on the right (meta ink, one rule).
        var left = geometry.ContentLeft;
        var right = geometry.ContentRight;
        InkText(spriteBatch, big, Fit(big, BookTabs.LabelOf(view.Session.Locale, page), right - left), new Vector2(left, geometry.TitleTop), accent);
        var summary = Fit(font, SummaryOf(view, page), geometry.DetailRight - geometry.DetailLeft);
        InkText(spriteBatch, font, summary, new Vector2(geometry.DetailRight - DrawHelpers.Width(font, summary), geometry.TitleTop + 16), InkMeta);
        spriteBatch.Draw(pixel, new Rectangle(left, geometry.RuleTop, right - left, 2), accent);
        spriteBatch.Draw(pixel, new Rectangle(geometry.DetailLeft, geometry.RuleTop, geometry.DetailRight - geometry.DetailLeft, 1), PageEdgeLine);

        // The left page: the list.
        var all = BookPages.ForPage(view.Session, page, view.Recordings);
        var lines = BookPages.InListOnly(all);
        var visible = geometry.LinesVisible;
        view.Book.OpenAtNext(lines, visible);
        view.Book.ClampTo(lines.Count, visible);
        var offset = view.Book.Offset;
        var selected = SelectedLine(lines, view.Book);
        var y = geometry.ContentTop;
        if (lines.Count == 0)
        {
            InkText(spriteBatch, font, EmptyText(view.Session.Locale, page), new Vector2(left, y), InkMeta);
        }

        for (var i = offset; i < lines.Count && i < offset + visible; i++)
        {
            DrawLine(spriteBatch, view, font, lines[i], left, right, y, i == selected, accent);
            y += BookGeometry.LineHeight;
        }

        if (lines.Count > visible)
        {
            var position = view.Session.Locale.Ui("bookLines", ("from", (offset + 1).ToString(CultureInfo.InvariantCulture)), ("to", Math.Min(lines.Count, offset + visible).ToString(CultureInfo.InvariantCulture)), ("count", lines.Count.ToString(CultureInfo.InvariantCulture)));
            position = Fit(font, position, right - left);
            InkText(spriteBatch, font, position, new Vector2(left, geometry.Paper.Bottom - 22), InkMeta);
        }

        // The right page: the selected item.
        var buttons = new List<BookRightButton>();
        var detailRow = selected < 0 ? FirstOnScreen(lines, offset, visible) : selected;
        if (page == BookPage.Verbs)
        {
            DrawVerbDetail(spriteBatch, view, geometry, all, lines, detailRow, accent);
        }
        else
        {
            DrawBlockDetail(spriteBatch, view, geometry, all, detailRow, accent, buttons);
        }

        view.Book.SetRightButtons(view.Book.IsTurning ? [] : buttons); // a button under the turning leaf cannot be pressed
        if (view.Book.IsTurning)
        {
            DrawLeaf(spriteBatch, pixel, geometry, view.Book.TurnProgress, view.Book.TurnForward);
        }

        DrawRibbons(spriteBatch, pixel, font, geometry, page, view.Session.Locale); // over the leaf: the ribbons hang from the top edge whatever page is turning

        var hintRoom = geometry.Panel.Width - 56;
        var hints = Fit(font, DrawHelpers.Width(font, BookTabs.KeyHints(view.Session.Locale)) <= hintRoom ? BookTabs.KeyHints(view.Session.Locale) : BookTabs.KeyHintsShort(view.Session.Locale), hintRoom);
        DrawHelpers.Text(spriteBatch, font, hints, new Vector2(geometry.Panel.X + 28, geometry.Panel.Bottom - 24), Cream);
    }

    // The cover: leather with a lighter rim, brass corners and the binding bands across the spine (top and bottom of the cover).
    private static void DrawCover(SpriteBatch spriteBatch, Texture2D pixel, BookGeometry geometry)
    {
        var panel = ToRectangle(geometry.Panel);
        DrawHelpers.RoundedFill(spriteBatch, pixel, panel, 6, CoverDark);
        DrawHelpers.RoundedFill(spriteBatch, pixel, new Rectangle(panel.X + 2, panel.Y + 2, panel.Width - 4, panel.Height - 4), 5, Cover);
        DrawHelpers.Border(spriteBatch, pixel, new Rectangle(panel.X + 4, panel.Y + 4, panel.Width - 8, panel.Height - 8), 1, CoverLight);
        var spine = ToRectangle(geometry.Spine);
        foreach (var (cx, cy) in new[] { (panel.X + 8, panel.Y + 8), (panel.Right - 16, panel.Y + 8), (panel.X + 8, panel.Bottom - 16), (panel.Right - 16, panel.Bottom - 16) })
        {
            spriteBatch.Draw(pixel, new Rectangle(cx, cy, 8, 8), Brass);
            spriteBatch.Draw(pixel, new Rectangle(cx + 2, cy + 2, 4, 4), CoverDark);
        }
    }

    // The paper: the stack of page edges under each page (stripes at the outer side and the bottom), the two pages, the shading toward the spine and the spine itself.
    private static void DrawPaper(SpriteBatch spriteBatch, Texture2D pixel, BookGeometry geometry)
    {
        var paper = ToRectangle(geometry.Paper);
        for (var i = 3; i >= 1; i--)
        {
            var rim = new Rectangle(paper.X - i, paper.Y + i, paper.Width + (2 * i), paper.Height + i);
            spriteBatch.Draw(pixel, rim, i % 2 == 0 ? PageEdgeLine : PageEdge);
        }

        var leftPage = ToRectangle(geometry.LeftPage);
        var rightPage = ToRectangle(geometry.RightPage);
        spriteBatch.Draw(pixel, leftPage, Paper);
        spriteBatch.Draw(pixel, rightPage, Paper);
        var spine = ToRectangle(geometry.Spine);
        spriteBatch.Draw(pixel, spine, PaperShade);
        spriteBatch.Draw(pixel, new Rectangle(spine.X + (spine.Width / 2) - 1, spine.Y, 2, spine.Height), PageEdgeLine);

        // The binding: the thread through the fold, stitch after stitch down the spine.
        for (var y = spine.Y + 14; y < spine.Bottom - 14; y += 34)
        {
            spriteBatch.Draw(pixel, new Rectangle(spine.X + 3, y, spine.Width - 6, 2), CoverDark);
        }

        // The pages curve into the spine: columns of paper getting darker toward it.
        for (var i = 0; i < 14; i++)
        {
            var shade = PaperShade * (float)(0.6 * Math.Pow((14 - i) / 14.0, 2)); // premultiplied: a colour times its alpha
            spriteBatch.Draw(pixel, new Rectangle(leftPage.Right - 1 - i, leftPage.Y, 1, leftPage.Height), shade);
            spriteBatch.Draw(pixel, new Rectangle(rightPage.X + i, rightPage.Y, 1, rightPage.Height), shade);
        }
    }

    // The ribbons hang from above the top edge of the paper over the cover, a swallow-tail at the end; the ribbon of the page on screen is lit and hangs lower.
    private static void DrawRibbons(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, BookGeometry geometry, BookPage page, LocaleText ui)
    {
        foreach (var tab in Enum.GetValues<BookPage>())
        {
            var rect = ToRectangle(geometry.TabRect(tab));
            var active = tab == page;
            var colour = Ribbons[(int)tab];
            var shade = active ? Lighten(colour, UiPalette.ActiveRibbonLighten) : colour;
            var bottom = rect.Bottom + (active ? 6 : 0);
            var body = new Rectangle(rect.X, rect.Y, rect.Width, bottom - rect.Y);
            spriteBatch.Draw(pixel, new Rectangle(body.X - 1, body.Y, body.Width + 2, body.Height), Darken(colour, 0.45));
            spriteBatch.Draw(pixel, body, shade);
            spriteBatch.Draw(pixel, new Rectangle(body.X + 3, body.Y, 1, body.Height), Lighten(shade, 0.18));

            // The swallow-tail: a notch cut out of the bottom edge, in paper.
            var middle = body.X + (body.Width / 2);
            for (var r = 0; r < NotchDepth; r++)
            {
                var half = NotchDepth - r;
                spriteBatch.Draw(pixel, new Rectangle(middle - half, body.Bottom - 1 - r, half * 2, 1), Paper);
            }

            var label = BookTabs.LabelOf(ui, tab);
            var x = rect.X + ((rect.Width - (int)DrawHelpers.Width(font, label)) / 2);
            DrawHelpers.Text(spriteBatch, font, label, new Vector2(x, geometry.TabsTop), UiPalette.RibbonLabel.ToColor()); // white on every ribbon, lit or not (X08: the cream label was 3.1 to 1 on the last)
        }
    }

    // The leaf of the page turn: a sheet of paper hinged at the spine, narrowing and widening on the other side, with a shadow at its free edge.
    private static void DrawLeaf(SpriteBatch spriteBatch, Texture2D pixel, BookGeometry geometry, double progress, bool forward)
    {
        var leaf = ToRectangle(geometry.Leaf(progress, forward));
        if (leaf.Width <= 0)
        {
            return;
        }

        spriteBatch.Draw(pixel, leaf, PageEdge);
        var onRight = leaf.X >= geometry.Spine.Right;
        var freeEdge = onRight ? leaf.Right - 2 : leaf.X;
        spriteBatch.Draw(pixel, new Rectangle(freeEdge, leaf.Y, 2, leaf.Height), PageEdgeLine);
        var shadowWidth = Math.Min(16, geometry.PageWidth - leaf.Width);
        if (shadowWidth > 0)
        {
            var shadowX = onRight ? leaf.Right : leaf.X - shadowWidth;
            spriteBatch.Draw(pixel, new Rectangle(shadowX, leaf.Y, shadowWidth, leaf.Height), new Color(60, 40, 20) * 0.2f);
        }
    }

    private static string SummaryOf(FrameView view, BookPage page)
    {
        var session = view.Session;
        return page switch
        {
            BookPage.Nouns => session.Locale.Ui("bookKnownNouns", ("known", session.Words.Catalogue.Nouns.Count(noun => session.Words.RecordOf(noun.Lemma).Known).ToString(CultureInfo.InvariantCulture)), ("count", session.Words.Catalogue.Nouns.Count.ToString(CultureInfo.InvariantCulture)), ("session", session.Day.ToString(CultureInfo.InvariantCulture))),
            BookPage.Phrases => session.Locale.Ui("bookWithoutHelp", ("count", PhraseWithoutHelp(view).ToString(CultureInfo.InvariantCulture)), ("session", session.Day.ToString(CultureInfo.InvariantCulture))),
            _ => session.Locale.Ui("bookSession", ("session", session.Day.ToString(CultureInfo.InvariantCulture))),
        };
    }

    private static string EmptyText(LocaleText ui, BookPage page) => page switch
    {
        BookPage.Verbs => ui.Ui("bookNoVerbs"),
        BookPage.Said => ui.Ui("bookNothingSaid"),
        _ => ui.Ui("bookNothing"),
    };

    // The phrases met that have no help left to fade: the count of the Phrases page's "Without help" section.
    private static int PhraseWithoutHelp(FrameView view)
    {
        var rows = PhraseJournalRows.Build(view.Session.PhraseProgress, view.Session.StoredTime.UnixSeconds, view.Session.Credit, view.Session.Locale);
        return rows.Count(static r => r.Record.Seen && !(r.HasScaffold && r.Level != SupportLevel.None));
    }

    // The line index of the selected row (the entries of the list are selectable, S28 and U07), or -1 when the page has none.
    private static int SelectedLine(IReadOnlyList<BookLine> lines, BookModel book)
    {
        var rows = BookSpeakers.Selectable(lines);
        var index = book.SelectedIndex(rows.Count);
        return index < 0 ? -1 : rows[index];
    }

    // The first entry on screen: what the right page shows when no row is selected.
    private static int FirstOnScreen(IReadOnlyList<BookLine> lines, int offset, int visible)
    {
        for (var i = Math.Max(0, offset); i < lines.Count && i < offset + visible; i++)
        {
            if (lines[i].Kind == BookLineKind.Entry)
            {
                return i;
            }
        }

        return -1;
    }

    // One row of the list, drawn by its kind: a gap is nothing, a section a band, a note meta ink, tiles a row of grey icons, an entry its lead, text, aside, pips and buttons.
    private static void DrawLine(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, BookLine line, int left, int right, int top, bool selected, Color accent)
    {
        switch (line.Kind)
        {
            case BookLineKind.Section:
                DrawBand(spriteBatch, view.Pixel, font, line, left, right, top, accent);
                break;
            case BookLineKind.Meta:
                InkText(spriteBatch, font, Fit(font, line.Text, right - left), new Vector2(left, top + 2), line.Tone == BookTone.Gold ? InkKnown : InkMeta);
                break;
            case BookLineKind.Tiles:
                DrawTiles(spriteBatch, view, line, left, top);
                break;
            case BookLineKind.Entry:
                DrawEntry(spriteBatch, view, font, line, left, right, top, selected, accent);
                break;
        }
    }

    // A section's band: the page's accent (a muted taupe for what is not met yet), the label in white at the left, the count at the right; the band runs 4 px past the text each side.
    private static void DrawBand(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, BookLine line, int left, int right, int top, Color accent)
    {
        var band = new Rectangle(left - 4, top + 1, right - left + 8, 17);
        spriteBatch.Draw(pixel, band, line.Tone == BookTone.Unseen ? BandMuted : accent);
        var white = UiPalette.RibbonLabel.ToColor();
        var count = line.Right ?? string.Empty;
        var countWidth = (int)DrawHelpers.Width(font, count);
        InkText(spriteBatch, font, count, new Vector2(band.Right - 8 - countWidth, top + 2), white);
        InkText(spriteBatch, font, Fit(font, line.Text, band.Width - 24 - countWidth), new Vector2(band.X + 8, top + 2), white);
    }

    // The nouns not met yet: up to twelve icons, always in grey (whatever --greyscale says), 24 px apart (16 px and an 8 px gap).
    private static void DrawTiles(SpriteBatch spriteBatch, FrameView view, BookLine line, int left, int top)
    {
        var x = left;
        foreach (var lemma in line.Lemmas ?? [])
        {
            DrawNounIcon(spriteBatch, view, lemma, new Rectangle(x, top + 2, IconSize, IconSize), grey: true);
            x += IconSize + 8;
        }
    }

    // The ink of an entry by its tone (U07): known the darkest, learning and seen a notch lighter, not met fainter, gold the brown of a mark, a quiet line the meta grey.
    private static Color ToneColour(BookTone tone) => tone switch
    {
        BookTone.Known => Ink,
        BookTone.Learning or BookTone.Seen => InkSeen,
        BookTone.Unseen => InkUnseen,
        BookTone.Gold => InkKnown,
        _ => InkMeta,
    };

    // Whether the entry's text is drawn in the strong weight (D-game-28): known entries (not a line the learner said), the next lesson and the line that offers the review.
    private static bool IsStrong(BookLine line) =>
        (line.Tone == BookTone.Known && line.Mark is null or BookMark.Done) || line.Next || (line.Tone == BookTone.Gold && string.Equals(line.Practise, BookPractise.ReviewNow, StringComparison.Ordinal));

    // One entry: the selection under it, then from the left its mark or icon, text and aside; from the right its button, pips or lamps and right-hand text.
    private static void DrawEntry(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, BookLine line, int left, int right, int top, bool selected, Color accent)
    {
        var pixel = view.Pixel;
        if (selected)
        {
            spriteBatch.Draw(pixel, new Rectangle(left - 4, top, right - left + 8, BookGeometry.LineHeight - 1), Select);
            spriteBatch.Draw(pixel, new Rectangle(left - 4, top, 3, BookGeometry.LineHeight - 1), accent);
        }

        // The button sits at the right edge; the text before it keeps clear of it.
        var rightEdge = right;
        if (line.Speak is not null)
        {
            DrawSpeaker(spriteBatch, pixel, new Rectangle(right - BookSpeakers.ButtonSize, top + 2, BookSpeakers.ButtonSize, BookSpeakers.ButtonSize), selected);
            rightEdge = right - BookSpeakers.ButtonSize - BookSpeakers.Gap;
        }
        else if (line.Practise is not null)
        {
            // The "Practise again" button of a completed lesson (S26), or the Phrases page's "Review now" (X06), at the right edge of its line.
            var box = new Rectangle(right - BookPractise.ButtonWidth, top + 1, BookPractise.ButtonWidth, BookGeometry.LineHeight - 3);
            spriteBatch.Draw(pixel, box, new Color(246, 236, 210));
            DrawHelpers.Border(spriteBatch, pixel, box, 1, InkKnown);
            var practise = BookPractise.Label(view.Session.Locale, line.Practise, line.Ask);
            var labelWidth = (int)DrawHelpers.Width(font, practise);
            InkText(spriteBatch, font, practise, new Vector2(box.X + ((box.Width - labelWidth) / 2), top + 1), InkKnown);
            rightEdge = box.X - BookSpeakers.Gap;
        }

        var x = left;
        if (line.Mark is { } mark)
        {
            DrawMark(spriteBatch, pixel, new Rectangle(x, top + 4, 11, 11), mark, accent);
            x += 11 + Gutter;
        }

        if (line.Lemma is { } lemma)
        {
            DrawNounIcon(spriteBatch, view, lemma, new Rectangle(x, top + 2, IconSize, IconSize));
            x += IconSize + Gutter;
        }

        // The right-hand part: the pips or the lamps, then the text before them.
        if (line.Pips is { } pips)
        {
            var pipLeft = rightEdge - ((WordPips * Pip) + ((WordPips - 1) * PipGap));
            for (var i = 0; i < WordPips; i++)
            {
                DrawPip(spriteBatch, pixel, new Rectangle(pipLeft + (i * (Pip + PipGap)), top + 5, Pip, Pip), i < pips);
            }

            rightEdge = pipLeft - Gutter;
        }
        else if (line.Lamps is { } lamps)
        {
            var lampsLeft = rightEdge - ((lamps.Count * Pip) + ((lamps.Count - 1) * 4));
            for (var i = 0; i < lamps.Count; i++)
            {
                DrawPip(spriteBatch, pixel, new Rectangle(lampsLeft + (i * (Pip + 4)), top + 5, Pip, Pip), lamps[i]);
            }

            rightEdge = lampsLeft - Gutter;
        }

        // The right-hand text (a verb's uses, where a line was said). A row with an English aside keeps its Spanish and its English whole, and the text takes what is left,
        // shortened with "..." (X03). It is left off only when less than a readable width is left.
        var metaInk = line.Tone == BookTone.Unseen ? InkUnseen : line.Tone == BookTone.Gold ? InkKnown : InkMeta;
        var reserved = 0;
        if (line.Right is { } rightText)
        {
            var room = line.Aside is null
                ? (right - left) / 2
                : rightEdge - x - (int)DrawHelpers.Width(font, line.Text) - (Gutter * 2) - (int)DrawHelpers.Width(font, line.Aside) - Gutter;
            if (line.Aside is null || room >= MinNoteWidth)
            {
                var fitted = FitNote(font, rightText, room);
                var width = (int)DrawHelpers.Width(font, fitted);
                InkText(spriteBatch, font, fitted, new Vector2(rightEdge - width, top + 2), metaInk);
                reserved = width + Gutter;
            }
        }

        var maxText = Math.Max(40, rightEdge - x - reserved - Gutter);
        var text = Fit(font, line.Text, maxText);
        InkText(spriteBatch, font, text, new Vector2(x, top + 2), ToneColour(line.Tone), IsStrong(line));
        if (line.Aside is { } aside)
        {
            // The aside takes what the text and the buttons leave; under a readable room (about five letters) it is left off rather than drawn as an ellipsis alone.
            var asideX = x + (int)DrawHelpers.Width(font, text) + Gutter;
            var asideRoom = rightEdge - asideX - reserved - Gutter;
            if (asideRoom >= MinAsideWidth)
            {
                InkText(spriteBatch, font, Fit(font, aside, asideRoom), new Vector2(asideX, top + 2), metaInk);
            }
        }
    }

    // ---- The right page

    // The right page of a page that is not the verbs: the selected entry's block, written out in full as rows of a sheet that scrolls (X03): the title at 32 px (an icon before it
    // for a noun, two lines for a long lesson title), then the block's details by role.
    private static void DrawBlockDetail(SpriteBatch spriteBatch, FrameView view, BookGeometry geometry, IReadOnlyList<BookLine> all, int row, Color accent, List<BookRightButton> buttons)
    {
        var block = BookPages.BlockOfRow(all, row);
        if (block.Count == 0)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var big = view.Fonts.Get(FontProvider.NativeSize * 2);
        var left = geometry.DetailLeft;
        var width = geometry.DetailRight - left;
        var sheet = new DetailSheet(spriteBatch);
        var entry = block[0];
        AddTitle(sheet, view, big, block.FirstOrDefault(static l => l.Role == BookDetailRole.Title)?.Text ?? entry.Text, entry.Lemma, left, width);
        foreach (var detail in block.Skip(1))
        {
            AddDetail(sheet, view, font, big, detail, left, width, accent, buttons);
        }

        PaintDetail(spriteBatch, view, geometry, font, sheet, row);
    }

    // The title of the item at 32 px in the darkest ink: its icon at the left when it is a noun, a title too long for the page wraps to a second line (and is cut after that).
    private static void AddTitle(DetailSheet sheet, FrameView view, SpriteFontBase big, string title, string? lemma, int left, int width)
    {
        var x = lemma is null ? left : left + 32 + Gutter;
        var lines = Wrap(big, title, left + width - x).Take(2).ToList();
        if (lines.Count == 0)
        {
            lines.Add(string.Empty);
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var first = i == 0;
            var text = Fit(big, lines[i], left + width - x);
            sheet.Add(38, y =>
            {
                if (first && lemma is not null)
                {
                    DrawNounIcon(sheet.Batch, view, lemma, new Rectangle(left, y + 3, 32, 32));
                }

                InkText(sheet.Batch, big, text, new Vector2(x, y + 3), Ink);
            });
        }
    }

    // One detail line of a block by its role. Spaces: 10 before a label, 6 after it and after a strip of numbers.
    private static void AddDetail(DetailSheet sheet, FrameView view, SpriteFontBase font, SpriteFontBase big, BookLine detail, int left, int width, Color accent, List<BookRightButton> buttons)
    {
        switch (detail.Role)
        {
            case BookDetailRole.Title:
                break;
            case BookDetailRole.Sub:
                sheet.Add(BookGeometry.LineHeight + 4, y => DrawSub(sheet.Batch, view, font, detail, left, y, width, accent));
                break;
            case BookDetailRole.Label:
                sheet.Add(10, static _ => { });
                sheet.Add(BookGeometry.LineHeight, y => DrawLabel(sheet.Batch, view.Pixel, font, detail, left, y, width, accent));
                sheet.Add(6, static _ => { });
                break;
            case BookDetailRole.Stats:
                AddStats(sheet, font, big, detail.Stats ?? [], left, width);
                break;
            case BookDetailRole.Units:
                AddUnits(sheet, view.Pixel, font, detail.Marks ?? [], left, width, accent);
                break;
            default:
                if (detail.Speak is not null || detail.Lamps is not null)
                {
                    sheet.Add(BookGeometry.LineHeight, y => DrawDetailRow(sheet.Batch, view.Pixel, font, detail, left, y, width, buttons));
                }
                else
                {
                    AddWrapped(sheet, font, detail.Text, left, width, ToneColour(detail.Tone));
                }

                break;
        }
    }

    // The line under the title: pips or a mark, then a word or two (the strong weight for a known item and for the lesson the objective sends the learner to).
    private static void DrawSub(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, BookLine line, int left, int top, int width, Color accent)
    {
        var x = left;
        if (line.Pips is { } pips)
        {
            for (var i = 0; i < WordPips; i++)
            {
                DrawPip(spriteBatch, view.Pixel, new Rectangle(x + (i * (Pip + PipGap)), top + 4, Pip, Pip), i < pips);
            }

            x += (WordPips * Pip) + ((WordPips - 1) * PipGap) + Gutter;
        }

        if (line.Mark is { } mark)
        {
            DrawMark(spriteBatch, view.Pixel, new Rectangle(x, top + 3, 11, 11), mark, accent);
            x += 11 + 8;
        }

        var strong = line.Tone is BookTone.Known or BookTone.Gold;
        InkText(spriteBatch, font, Fit(font, line.Text, left + width - x), new Vector2(x, top + 1), ToneColour(line.Tone), strong);
    }

    // A section label of the right page: the accent, strong, with a one pixel rule in the accent under it and the count at the right.
    private static void DrawLabel(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, BookLine line, int left, int top, int width, Color accent)
    {
        var count = line.Right ?? string.Empty;
        var countWidth = (int)DrawHelpers.Width(font, count);
        InkText(spriteBatch, font, count, new Vector2(left + width - countWidth, top), InkMeta);
        InkText(spriteBatch, font, Fit(font, line.Text, width - countWidth - Gutter), new Vector2(left, top), accent, strong: true);
        spriteBatch.Draw(pixel, new Rectangle(left, top + BookGeometry.LineHeight - 1, width, 1), accent);
    }

    // A strip of numbers: each 32 px over its caption in meta ink, 26 px apart, wrapping to a second row when the page is narrow.
    private static void AddStats(DetailSheet sheet, SpriteFontBase font, SpriteFontBase big, IReadOnlyList<BookStat> stats, int left, int width)
    {
        const int Gap = 26;
        var rows = new List<List<(BookStat Stat, int Width)>> { new() };
        var used = 0;
        foreach (var stat in stats)
        {
            var w = (int)Math.Max(DrawHelpers.Width(big, stat.Number), DrawHelpers.Width(font, stat.Caption));
            if (used > 0 && used + Gap + w > width)
            {
                rows.Add([]);
                used = 0;
            }

            rows[^1].Add((stat, w));
            used += (used > 0 ? Gap : 0) + w;
        }

        foreach (var row in rows)
        {
            var items = row;
            sheet.Add(34 + BookGeometry.LineHeight + 6, y =>
            {
                var x = left;
                foreach (var (stat, w) in items)
                {
                    InkText(sheet.Batch, big, stat.Number, new Vector2(x, y), Ink);
                    InkText(sheet.Batch, font, stat.Caption, new Vector2(x, y + 34), InkMeta);
                    x += w + Gap;
                }
            });
        }
    }

    // A lesson's units: a mark and the unit's number each, 16 px apart, wrapping when the page is narrow.
    private static void AddUnits(DetailSheet sheet, Texture2D pixel, SpriteFontBase font, IReadOnlyList<BookMark> marks, int left, int width, Color accent)
    {
        var rows = new List<List<(BookMark Mark, int Number, int Width)>> { new() };
        var used = 0;
        for (var i = 0; i < marks.Count; i++)
        {
            var w = 11 + 6 + (int)DrawHelpers.Width(font, (i + 1).ToString(CultureInfo.InvariantCulture));
            if (used > 0 && used + 16 + w > width)
            {
                rows.Add([]);
                used = 0;
            }

            rows[^1].Add((marks[i], i + 1, w));
            used += (used > 0 ? 16 : 0) + w;
        }

        foreach (var row in rows)
        {
            var items = row;
            sheet.Add(BookGeometry.LineHeight + 4, y =>
            {
                var x = left;
                foreach (var (mark, number, w) in items)
                {
                    DrawMark(sheet.Batch, pixel, new Rectangle(x, y + 4, 11, 11), mark, accent);
                    InkText(sheet.Batch, font, number.ToString(CultureInfo.InvariantCulture), new Vector2(x + 17, y + 1), InkSeen);
                    x += w + 16;
                }
            });
        }
    }

    // A detail row with a lamp or a speaker button: the lamps (the card and the audio of a phrase's help) then the text and its aside; the speaker at the right edge, which the mouse hits.
    private static void DrawDetailRow(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, BookLine line, int left, int top, int width, List<BookRightButton> buttons)
    {
        var x = left;
        var rightEdge = left + width;
        if (line.Lamps is { } lamps)
        {
            for (var i = 0; i < lamps.Count; i++)
            {
                DrawPip(spriteBatch, pixel, new Rectangle(x + (i * (Pip + 4)), top + 5, Pip, Pip), lamps[i]);
            }

            x += (lamps.Count * Pip) + ((lamps.Count - 1) * 4) + 6;
        }

        if (line.Speak is { } speak)
        {
            var box = new Rectangle(rightEdge - BookSpeakers.ButtonSize, top + 2, BookSpeakers.ButtonSize, BookSpeakers.ButtonSize);
            DrawSpeaker(spriteBatch, pixel, box, selected: false);
            buttons.Add(new BookRightButton(new BookRect(box.X, box.Y, box.Width, box.Height), speak));
            rightEdge = box.X - BookSpeakers.Gap;
        }

        var text = Fit(font, line.Text, Math.Max(40, rightEdge - x));
        InkText(spriteBatch, font, text, new Vector2(x, top + 2), ToneColour(line.Tone));
        if (line.Aside is { } aside)
        {
            var asideX = x + (int)DrawHelpers.Width(font, text) + Gutter;
            InkText(spriteBatch, font, Fit(font, aside, Math.Max(20, rightEdge - asideX)), new Vector2(asideX, top + 2), InkMeta);
        }
    }

    // ---- The right page of the verbs page

    // The right page of the verbs page: the verb at 32 px and its English, the conjugation table, what the colours mean as three swatches, and what the learner said (each form
    // with its English, its count and the last sentence it was said in). The whole page scrolls (X03).
    private static void DrawVerbDetail(SpriteBatch spriteBatch, FrameView view, BookGeometry geometry, IReadOnlyList<BookLine> all, IReadOnlyList<BookLine> list, int selected, Color accent)
    {
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var big = view.Fonts.Get(FontProvider.NativeSize * 2);
        var left = geometry.DetailLeft;
        var width = geometry.DetailRight - left;
        var sheet = new DetailSheet(spriteBatch);
        if (selected < 0 || list[selected].Verb is not { } lemma || BookPages.VerbTable(view.Session, lemma) is not { } table)
        {
            sheet.Add(BookGeometry.LineHeight, y => InkText(spriteBatch, font, view.Session.Locale.Ui("bookChooseVerb"), new Vector2(left, y), InkMeta));
            PaintDetail(spriteBatch, view, geometry, font, sheet, selected);
            return;
        }

        AddTitle(sheet, view, big, table.Lemma, null, left, width);
        sheet.Add(BookGeometry.LineHeight + 10, y =>
        {
            if (table.Gloss is not null)
            {
                InkText(spriteBatch, font, Fit(font, table.Gloss, width), new Vector2(left, y), InkSeen);
            }
        });
        AddTable(sheet, view.Pixel, font, table, left, width, view.Session.Locale, accent);
        sheet.Add(6, static _ => { });
        AddVerbLegend(sheet, view.Pixel, font, view.Session.Locale, left, width);

        // What the learner said, as the verbs page listed it (S20): each form with its English, its count and the last sentence it was said in.
        var block = BookPages.BlockOfRow(all, selected).Skip(1).ToList();
        if (block.Count > 0)
        {
            AddDetail(sheet, view, font, big, new BookLine(BookTone.Hint, view.Session.Locale.Ui("bookWhatYouSaid"), Kind: BookLineKind.Detail, Role: BookDetailRole.Label), left, width, accent, []);
        }

        foreach (var form in block)
        {
            if (form.Right is { } count)
            {
                sheet.Add(BookGeometry.LineHeight, y =>
                {
                    var countWidth = (int)DrawHelpers.Width(font, count);
                    InkText(spriteBatch, font, count, new Vector2(left + width - countWidth, y), InkMeta);
                    InkText(spriteBatch, font, Fit(font, form.Text, width - countWidth - Gutter), new Vector2(left, y), InkSeen);
                });
            }
            else
            {
                AddWrapped(sheet, font, form.Text, left + IndentWidth, width - IndentWidth, InkMeta);
            }
        }

        PaintDetail(spriteBatch, view, geometry, font, sheet, selected);
    }

    // The legend of the table: three swatches that look like the cells they explain (said, taught, not yet), each with its words, wrapping when the page is narrow.
    private static void AddVerbLegend(DetailSheet sheet, Texture2D pixel, SpriteFontBase font, LocaleText ui, int left, int width)
    {
        var items = new[] { (Kind: 0, Text: ui.Ui("bookLegendSaid")), (Kind: 1, Text: ui.Ui("bookLegendTaught")), (Kind: 2, Text: ui.Ui("bookLegendNot")) };
        var rows = new List<List<(int Kind, string Text, int Width)>> { new() };
        var used = 0;
        foreach (var (kind, text) in items)
        {
            var w = 19 + 6 + (int)DrawHelpers.Width(font, text);
            if (used > 0 && used + Gutter + w > width)
            {
                rows.Add([]);
                used = 0;
            }

            rows[^1].Add((kind, text, w));
            used += (used > 0 ? Gutter : 0) + w;
        }

        foreach (var row in rows)
        {
            var members = row;
            sheet.Add(BookGeometry.LineHeight + 4, y =>
            {
                var x = left;
                foreach (var (kind, text, w) in members)
                {
                    var swatch = new Rectangle(x, y + 3, 19, 13);
                    if (kind == 0)
                    {
                        sheet.Batch.Draw(pixel, swatch, Highlight);
                        DrawHelpers.Border(sheet.Batch, pixel, swatch, 1, InkKnown);
                    }
                    else if (kind == 1)
                    {
                        DrawHelpers.Border(sheet.Batch, pixel, swatch, 1, PageEdgeLine);
                    }
                    else
                    {
                        DrawCentred(sheet.Batch, font, "-", swatch, InkUnseen);
                    }

                    InkText(sheet.Batch, font, text, new Vector2(x + 25, y + 1), InkMeta);
                    x += w + Gutter;
                }
            });
        }

    }

    // The table as rows of the scrolling page: a header row of tenses (row -1), then the five persons. Each row is drawn at its own top.
    private static void AddTable(DetailSheet sheet, Texture2D pixel, SpriteFontBase font, ConjugationTable table, int left, int width, LocaleText ui, Color accent)
    {
        var perBlock = ConjugationLayout.ColumnsPerBlock(width, table.Columns.Count);
        for (var start = 0; start < table.Columns.Count; start += perBlock)
        {
            var first = start;
            var count = Math.Min(perBlock, table.Columns.Count - start);
            if (start > 0)
            {
                sheet.Add(10, static _ => { }); // a narrow page stacks the tenses: a gap between the blocks (X08)
            }

            for (var r = -1; r < table.Rows.Count; r++)
            {
                var row = r;

                // The table's top is the row's top less the rows above it (the header is above the first person's row).
                sheet.Add(ConjugationLayout.RowHeight, y => DrawTableRow(sheet.Batch, pixel, font, table, left, y - ((row + 1) * ConjugationLayout.RowHeight), width, row, first, count, ui, accent));
            }
        }
    }

    // One row of a table: -1 is the header of tenses, in the page's accent, strong, and the rule under it; a person's row draws its label and its cells. A used form is highlighted with
    // its count; a taught one is plain; a form not taught is a dash.
    private static void DrawTableRow(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, ConjugationTable table, int left, int top, int width, int r, int firstColumn, int columns, LocaleText ui, Color accent)
    {
        if (r < 0)
        {
            for (var c = 0; c < columns; c++)
            {
                DrawCentred(spriteBatch, font, ui.Ui(table.Columns[firstColumn + c].TitleId), ToRectangle(ConjugationLayout.Header(left, top, width, columns, c)), accent, strong: true);
            }

            spriteBatch.Draw(pixel, new Rectangle(left, top + ConjugationLayout.RowHeight - 2, width, 1), accent);
            return;
        }

        var row = table.Rows[r];
        var label = ToRectangle(ConjugationLayout.Label(left, top, r));
        InkText(spriteBatch, font, Fit(font, row.Label, label.Width), new Vector2(label.X, label.Y + ((label.Height - 16) / 2)), InkSeen);
        for (var c = 0; c < columns; c++)
        {
            var cell = row.Cells[firstColumn + c];
            var box = ToRectangle(ConjugationLayout.Cell(left, top, width, columns, r, c));
            var inner = new Rectangle(box.X + 2, box.Y + 2, box.Width - 4, box.Height - 4);
            switch (cell.State)
            {
                case CellState.Used:
                    spriteBatch.Draw(pixel, inner, Highlight);
                    DrawHelpers.Border(spriteBatch, pixel, inner, 1, InkKnown);
                    DrawCentred(spriteBatch, font, cell.Text ?? string.Empty, new Rectangle(inner.X, inner.Y, inner.Width, inner.Height - 8), Ink);
                    DrawCount(spriteBatch, font, cell.Uses, inner);
                    break;
                case CellState.Taught:
                    DrawHelpers.Border(spriteBatch, pixel, inner, 1, PageEdgeLine);
                    DrawCentred(spriteBatch, font, cell.Text ?? string.Empty, inner, Ink);
                    break;
                default:
                    DrawCentred(spriteBatch, font, "-", inner, InkUnseen);
                    break;
            }
        }
    }

    // The count of a used cell, at its bottom right, in small ink ("x3").
    private static void DrawCount(SpriteBatch spriteBatch, SpriteFontBase font, int uses, Rectangle cell)
    {
        var text = $"x{uses}";
        var width = (int)DrawHelpers.Width(font, text);
        InkText(spriteBatch, font, text, new Vector2(cell.Right - width - 3, cell.Bottom - 17), InkKnown);
    }

    // Text centred in a rectangle; one that is too wide breaks at its spaces onto two lines (voy a / comer) and is cut short only as a last resort.
    private static void DrawCentred(SpriteBatch spriteBatch, SpriteFontBase font, string text, Rectangle area, Color colour, bool strong = false)
    {
        if (DrawHelpers.Width(font, text) > area.Width - 4)
        {
            text = text.Replace(" (", "\n(", StringComparison.Ordinal); // "future (ir a)" breaks before its bracket, not inside it
        }

        var lines = text.Split('\n').SelectMany(part => Wrap(font, part, area.Width - 4)).Take(2).Select(l => Fit(font, l, area.Width - 4)).ToList();
        var lineHeight = 17;
        var top = area.Y + ((area.Height - (lines.Count * lineHeight)) / 2);
        foreach (var line in lines)
        {
            var width = (int)DrawHelpers.Width(font, line);
            InkText(spriteBatch, font, line, new Vector2(area.X + ((area.Width - width) / 2), top), colour, strong);
            top += lineHeight;
        }
    }

    // Wrapped text as rows of the right page: one row per line.
    private static void AddWrapped(DetailSheet sheet, SpriteFontBase font, string text, int x, int width, Color colour)
    {
        foreach (var line in Wrap(font, text, width))
        {
            sheet.Add(BookGeometry.LineHeight, y => InkText(sheet.Batch, font, line, new Vector2(x, y), colour));
        }
    }

    // Paints a right page's detail at its scroll, and the "more below" marker when writing lies under the page. The detail is of a line (the scroll resets when
    // that line changes; see BookModel.KeepDetail).
    private static void PaintDetail(SpriteBatch spriteBatch, FrameView view, BookGeometry geometry, SpriteFontBase font, DetailSheet sheet, int line)
    {
        var book = view.Book;
        var height = geometry.DetailHeight;
        book.KeepDetail(line);
        book.ClampDetail(sheet.Height, height);
        sheet.PaintFrom(geometry.ContentTop, geometry.ContentBottom, book.DetailOffset);
        if (DetailScroll.MoreBelow(book.DetailOffset, sheet.Height, height))
        {
            var marker = view.Session.Locale.Ui("bookMoreBelow");
            var width = (int)DrawHelpers.Width(font, marker);
            InkText(spriteBatch, font, marker, new Vector2(geometry.DetailRight - width, geometry.Paper.Bottom - 22), InkMeta);
        }
    }

    // Breaks a text at its spaces into lines no wider than a width (a word wider than the width stays whole on its own line; the caller cuts it). A run of spaces inside a line
    // stays (the columns of "forms" and of the units' steps are three spaces apart); at a break it is dropped.
    private static List<string> Wrap(SpriteFontBase font, string text, int width)
    {
        var result = new List<string>();
        var current = string.Empty;
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, @" *\S+"))
        {
            var candidate = current.Length == 0 ? match.Value.TrimStart(' ') : current + match.Value;
            if (current.Length > 0 && DrawHelpers.Width(font, candidate) > width)
            {
                result.Add(current);
                current = match.Value.TrimStart(' ');
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
        {
            result.Add(current);
        }

        return result;
    }

    // Text on paper: dark ink with no shadow (the panels' shadowed text is for dark backgrounds). The strong weight is the text drawn twice, one pixel apart: the pixel font's
    // only bolder weight (D-game-28: for known entries, the right page's labels and the next lesson only).
    private static void InkText(SpriteBatch spriteBatch, SpriteFontBase font, string text, Vector2 position, Color colour, bool strong = false)
    {
        spriteBatch.DrawString(font, text, position, colour);
        if (strong)
        {
            spriteBatch.DrawString(font, text, position + new Vector2(1, 0), colour);
        }
    }

    // The speaker button (S20b): a box with a speaker drawn in pixels; dark ink, with the selected row's button boxed in the brown of a mark.
    private static void DrawSpeaker(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, bool selected)
    {
        var ink = selected ? InkKnown : InkSeen;
        spriteBatch.Draw(pixel, box, new Color(246, 236, 210));
        DrawHelpers.Border(spriteBatch, pixel, box, 1, selected ? InkKnown : InkMeta);
        var x = box.X;
        var y = box.Y;
        spriteBatch.Draw(pixel, new Rectangle(x + 3, y + 6, 3, 4), ink); // the body
        spriteBatch.Draw(pixel, new Rectangle(x + 6, y + 4, 2, 8), ink); // the cone
        spriteBatch.Draw(pixel, new Rectangle(x + 8, y + 2, 2, 12), ink);
        spriteBatch.Draw(pixel, new Rectangle(x + 11, y + 5, 1, 6), ink); // the sound
        spriteBatch.Draw(pixel, new Rectangle(x + 13, y + 3, 1, 10), ink);
    }

    private static void DrawNounIcon(SpriteBatch spriteBatch, FrameView view, string lemma, Rectangle icon, bool grey = false)
    {
        var noun = view.Session.Nouns.ForLemma(lemma);

        // The colour rule of --greyscale (S22): a noun that is not known yet has a grey icon, a known one is in colour; with the switch off every icon is in colour.
        var saturation = grey ? 0.0 : NounIcons.Saturation(view.Greyscale, view.Session.Words.RecordOf(lemma).Known); // the tiles of the nouns not met are always grey
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
            spriteBatch.Draw(view.Pixel, icon, PaperShade);
        }
    }

    // The 11 px marks (U07): a lesson's or a unit's state, and how a line the learner said was taken. A box of one pixel with its sign inside, as the mockup draws them.
    private static void DrawMark(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, BookMark mark, Color accent)
    {
        void Px(int dx, int dy, int w, int h, Color colour) => spriteBatch.Draw(pixel, new Rectangle(box.X + dx, box.Y + dy, w, h), colour);
        switch (mark)
        {
            case BookMark.Done:
                DrawHelpers.Border(spriteBatch, pixel, box, 1, InkKnown);
                DrawTickSign(Px, InkKnown);
                break;
            case BookMark.Understood:
                DrawHelpers.Border(spriteBatch, pixel, box, 1, MarkGood);
                DrawTickSign(Px, MarkGood);
                break;
            case BookMark.Half:
                DrawHelpers.Border(spriteBatch, pixel, box, 1, InkKnown);
                Px(1, 1, 4, 9, InkKnown);
                break;
            case BookMark.Empty:
                DrawHelpers.Border(spriteBatch, pixel, box, 1, InkUnseen);
                break;
            case BookMark.Wait:
                // The box with a dashed line: two pixels drawn, one left out, round the edge.
                var ring = new List<(int X, int Y)>();
                for (var i = 0; i < 11; i++)
                {
                    ring.Add((i, 0));
                }

                for (var i = 1; i < 11; i++)
                {
                    ring.Add((10, i));
                }

                for (var i = 9; i >= 0; i--)
                {
                    ring.Add((i, 10));
                }

                for (var i = 9; i >= 1; i--)
                {
                    ring.Add((0, i));
                }

                for (var i = 0; i < ring.Count; i++)
                {
                    if (i % 3 != 2)
                    {
                        Px(ring[i].X, ring[i].Y, 1, 1, InkUnseen);
                    }
                }

                break;
            case BookMark.Next:
                Px(2, 1, 2, 9, accent);
                Px(4, 2, 2, 7, accent);
                Px(6, 3, 2, 5, accent);
                Px(8, 4, 2, 3, accent);
                Px(9, 5, 1, 1, accent);
                break;
            case BookMark.Slip:
                DrawHelpers.Border(spriteBatch, pixel, box, 1, MarkSlip);
                Px(4, 2, 3, 4, MarkSlip);
                Px(4, 7, 3, 2, MarkSlip);
                break;
            case BookMark.Missed:
                DrawHelpers.Border(spriteBatch, pixel, box, 1, MarkMiss);
                for (var i = 2; i <= 8; i++)
                {
                    Px(i, i, 1, 1, MarkMiss);
                    Px(10 - i, i, 1, 1, MarkMiss);
                }

                break;
            default:
                Px(4, 4, 3, 3, InkMeta);
                break;
        }
    }

    // The tick inside a box: a short stroke down to the corner and a longer one up.
    private static void DrawTickSign(Action<int, int, int, int, Color> px, Color colour)
    {
        px(2, 6, 2, 2, colour);
        px(4, 7, 2, 2, colour);
        px(6, 5, 2, 2, colour);
        px(8, 3, 2, 2, colour);
    }

    // A pip or a lamp: filled when lit, an outline when not.
    private static void DrawPip(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, bool lit)
    {
        if (lit)
        {
            spriteBatch.Draw(pixel, box, InkKnown);
        }
        else
        {
            DrawHelpers.Border(spriteBatch, pixel, box, 1, InkMeta);
        }
    }

    // Shortens a text to fit a width with "..." at its end (a line never runs over its neighbour).
    private static string Fit(SpriteFontBase font, string text, int maxWidth) => DrawHelpers.Fit(font, text, maxWidth);

    // A row's note on the right is columns three spaces apart ("used 1   correct 1"): on a narrow page it gives up its last columns before it is cut mid-word (X08).
    private static string FitNote(SpriteFontBase font, string text, int maxWidth)
    {
        var columns = text.Split("   ", StringSplitOptions.RemoveEmptyEntries);
        for (var count = columns.Length; count >= 1; count--)
        {
            var candidate = string.Join("   ", columns.Take(count));
            if (DrawHelpers.Width(font, candidate) <= maxWidth)
            {
                return candidate;
            }
        }

        return Fit(font, columns.Length == 0 ? text : columns[0], maxWidth);
    }

    private static Color Lighten(Color colour, double amount) =>
        new((int)(colour.R + ((255 - colour.R) * amount)), (int)(colour.G + ((255 - colour.G) * amount)), (int)(colour.B + ((255 - colour.B) * amount)));

    private static Color Darken(Color colour, double amount) => new((int)(colour.R * (1 - amount)), (int)(colour.G * (1 - amount)), (int)(colour.B * (1 - amount)));

    // The writing of a right page as rows, each drawn at its top when it lies wholly on the page. The page scrolls by pixels: a row that an edge cuts waits
    // until the scroll shows it whole.
    private sealed class DetailSheet(SpriteBatch batch)
    {
        private readonly List<(int Height, Action<int> Paint)> _rows = [];

        public SpriteBatch Batch { get; } = batch;

        public int Height => _rows.Sum(static row => row.Height);

        public void Add(int height, Action<int> paint) => _rows.Add((height, paint));

        public void PaintFrom(int top, int bottom, int offset)
        {
            var y = top - offset;
            foreach (var (height, paint) in _rows)
            {
                if (y >= top && y + height <= bottom)
                {
                    paint(y);
                }

                y += height;
            }
        }
    }

    private static Rectangle ToRectangle(BookRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
