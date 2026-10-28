using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Nlp.Es;

namespace Vocaterra.Game.UI.Book;

/// <summary>
/// The book (J, or the book item), remade as a book (S28): an open two-page spread on paper with a spine and binding, page edges and a cover, five ribbon
/// bookmarks along the top edge (Nouns, Verbs, Phrases, Lessons, What I said) and a short page-turn leaf. Everything is drawn in code from rectangles (D-game-5).
/// The left page lists (the lines <see cref="BookPages"/> builds from the learner's record, scrolled with the wheel, Page Up and Page Down, a speaker button at the
/// right of a row that has a recording, S20b); the right page shows the selected item: on the Verbs page its conjugation table (<see cref="ConjugationTable"/>),
/// elsewhere the item's own lines, written out in full. It covers the screen while it is open and takes the keyboard (the player does not walk).
/// </summary>
public sealed class BookLayer : IFrameLayer
{
    private const int IconSize = 16;
    private const int IndentWidth = 24;
    private const int Pip = 10;
    private const int WordPips = WordProgress.UsesToKnow;
    private const int PipGap = 6;
    private const int Gutter = 10;
    private const int NotchDepth = 9;

    // The narrowest a row's right-hand note may be and still be read (X03); narrower, the note is left off.
    private const int MinNoteWidth = 90;

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
    private static readonly Color InkHint = UiPalette.InkHint.ToColor();
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
        spriteBatch.Draw(pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), Dim);

        var geometry = new BookGeometry(view.ViewportWidth, view.ViewportHeight);
        var page = view.Book.Page;
        DrawCover(spriteBatch, pixel, geometry);
        DrawPaper(spriteBatch, pixel, geometry);

        // The headings: the title on the left page, the summary on the right, a rule under each.
        var left = geometry.ContentLeft;
        var right = geometry.ContentRight;
        InkText(spriteBatch, font, $"{BookTabs.Title}  -  {BookTabs.LabelOf(page)}", new Vector2(left, geometry.TitleTop), InkKnown);
        var summary = Fit(font, SummaryOf(view, page), geometry.DetailRight - geometry.DetailLeft);
        InkText(spriteBatch, font, summary, new Vector2(geometry.DetailRight - DrawHelpers.Width(font, summary), geometry.TitleTop), InkHint);
        spriteBatch.Draw(pixel, new Rectangle(left, geometry.RuleTop, right - left, 1), PageEdgeLine);
        spriteBatch.Draw(pixel, new Rectangle(geometry.DetailLeft, geometry.RuleTop, geometry.DetailRight - geometry.DetailLeft, 1), PageEdgeLine);

        // The left page: the list.
        var lines = BookPages.ListFor(view.Session, page, view.Recordings);
        var visible = geometry.LinesVisible;
        view.Book.ClampTo(lines.Count, visible);
        var offset = view.Book.Offset;
        var textWidth = right - left;
        var selected = SelectedLine(lines, view.Book);
        var y = geometry.ContentTop;
        if (lines.Count == 0)
        {
            InkText(spriteBatch, font, EmptyText(page), new Vector2(left, y), InkHint);
        }

        for (var i = offset; i < lines.Count && i < offset + visible; i++)
        {
            DrawLine(spriteBatch, view, font, lines[i], left, right, y, textWidth, i == selected);
            y += BookGeometry.LineHeight;
        }

        if (lines.Count > visible)
        {
            var position = $"lines {offset + 1} to {Math.Min(lines.Count, offset + visible)} of {lines.Count}   wheel, Page Up, Page Down";
            position = Fit(font, position, right - left);
            InkText(spriteBatch, font, position, new Vector2(left, geometry.Paper.Bottom - 22), InkHint);
        }

        // The right page: the selected item.
        if (page == BookPage.Verbs)
        {
            DrawVerbDetail(spriteBatch, view, geometry, lines, selected);
        }
        else
        {
            DrawBlockDetail(spriteBatch, view, geometry, lines, selected < 0 ? FirstOnScreen(lines, offset, visible) : selected);
        }

        if (view.Book.IsTurning)
        {
            DrawLeaf(spriteBatch, pixel, geometry, view.Book.TurnProgress, view.Book.TurnForward);
        }

        DrawRibbons(spriteBatch, pixel, font, geometry, page); // over the leaf: the ribbons hang from the top edge whatever page is turning

        var hintRoom = geometry.Panel.Width - 56;
        var hints = Fit(font, DrawHelpers.Width(font, BookTabs.KeyHints) <= hintRoom ? BookTabs.KeyHints : BookTabs.KeyHintsShort, hintRoom);
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
    private static void DrawRibbons(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, BookGeometry geometry, BookPage page)
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

            var label = BookTabs.LabelOf(tab);
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
            BookPage.Nouns => $"Known: {session.Words.Catalogue.Nouns.Count(noun => session.Words.RecordOf(noun.Lemma).Known)} of {session.Words.Catalogue.Nouns.Count}   ·   Session {session.PlaySession}",
            BookPage.Phrases => $"Without help: {PhraseWithoutHelp(view)}   ·   Session {session.PlaySession}",
            _ => $"Session {session.PlaySession}",
        };
    }

    private static string EmptyText(BookPage page) => page switch
    {
        BookPage.Verbs => "No verbs yet. Use a verb in a sentence and it comes here.",
        BookPage.Said => "Nothing said yet.",
        _ => "Nothing here yet.",
    };

    private static int PhraseWithoutHelp(FrameView view)
    {
        var rows = PhraseJournalRows.Build(view.Session.PhraseProgress, view.Session.StoredTime.UnixSeconds, view.Session.Credit);
        return rows.Count(static r => r is { HasScaffold: true, Level: SupportLevel.None });
    }

    // The line index of the selected row (S20b: a row with a speaker button; S28: a verb on the verbs page), or -1 when the page has none.
    private static int SelectedLine(IReadOnlyList<BookLine> lines, BookModel book)
    {
        var rows = BookSpeakers.Selectable(lines);
        var index = book.SelectedIndex(rows.Count);
        return index < 0 ? -1 : rows[index];
    }

    private static int FirstOnScreen(IReadOnlyList<BookLine> lines, int offset, int visible)
    {
        for (var i = Math.Max(0, offset); i < lines.Count && i < offset + visible; i++)
        {
            if (lines[i].Text.Length > 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static void DrawLine(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, BookLine line, int left, int right, int top, int textWidth, bool selected)
    {
        var pixel = view.Pixel;
        if (selected)
        {
            spriteBatch.Draw(pixel, new Rectangle(left - 4, top, right - left + 8, BookGeometry.LineHeight - 1), Select);
        }

        // The speaker button sits at the right edge; the text before it keeps clear of it.
        var rightEdge = right;
        if (line.Speak is not null)
        {
            DrawSpeaker(spriteBatch, pixel, new Rectangle(right - BookSpeakers.ButtonSize, top + 2, BookSpeakers.ButtonSize, BookSpeakers.ButtonSize), selected);
            rightEdge = right - BookSpeakers.ButtonSize - BookSpeakers.Gap;
        }
        else if (line.Practise is not null)
        {
            // The "Practise again" button of a completed lesson (S26), at the right edge of its heading.
            var box = new Rectangle(right - BookPractise.ButtonWidth, top + 1, BookPractise.ButtonWidth, BookGeometry.LineHeight - 3);
            spriteBatch.Draw(pixel, box, new Color(246, 236, 210));
            DrawHelpers.Border(spriteBatch, pixel, box, 1, InkKnown);
            var labelWidth = (int)DrawHelpers.Width(font, BookPractise.Label);
            InkText(spriteBatch, font, BookPractise.Label, new Vector2(box.X + ((box.Width - labelWidth) / 2), top + 1), InkKnown);
            rightEdge = box.X - BookSpeakers.Gap;
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

        // The right-hand note. A row with an English aside keeps its Spanish and its English whole, and the note takes what is left, shortened with "..."
        // (X03: a narrow row on an 800 x 600 page kept no note at all before). It is left off only when less than a readable width is left.
        var reserved = 0;
        if (line.Right is { } rightText)
        {
            var room = line.Aside is null
                ? textWidth / 2
                : rightEdge - x - (int)DrawHelpers.Width(font, line.Text) - (Gutter * 2) - (int)DrawHelpers.Width(font, line.Aside) - Gutter;
            if (line.Aside is null || room >= MinNoteWidth)
            {
                var fitted = FitNote(font, rightText, room);
                var width = (int)DrawHelpers.Width(font, fitted);
                InkText(spriteBatch, font, fitted, new Vector2(rightEdge - width, top + 2), line.Tone == BookTone.Gold ? InkKnown : InkHint);
                reserved = width + Gutter;
            }
        }

        var maxText = Math.Max(40, rightEdge - x - reserved - Gutter);
        var text = Fit(font, line.Text, maxText);
        InkText(spriteBatch, font, text, new Vector2(x, top + 2), ToneColour(line.Tone));
        if (line.Aside is { } aside)
        {
            var asideX = x + (int)DrawHelpers.Width(font, text) + Gutter;
            var asideText = Fit(font, aside, Math.Max(20, rightEdge - asideX - reserved - Gutter));
            InkText(spriteBatch, font, asideText, new Vector2(asideX, top + 2), InkHint);
        }
    }

    // The right page of a page that is not the verbs: the selected item's own lines (its block), written out in full on the page. The page scrolls (X03), so a
    // long block is read to its end and not cut at the bottom.
    private static void DrawBlockDetail(SpriteBatch spriteBatch, FrameView view, BookGeometry geometry, IReadOnlyList<BookLine> lines, int line)
    {
        var block = BookPages.BlockOf(lines, line);
        if (block.Count == 0)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var big = view.Fonts.Get(FontProvider.NativeSize * 2);
        var left = geometry.DetailLeft;
        var width = geometry.DetailRight - left;
        var sheet = new DetailSheet(spriteBatch);
        var heading = block[0];
        var x = heading.Lemma is null ? left : left + 40;
        var title = Fit(big, heading.Text, left + width - x);
        var headingColour = ToneColour(heading.Tone == BookTone.Unseen ? BookTone.Seen : heading.Tone);
        sheet.Add(38, y =>
        {
            if (heading.Lemma is { } lemma)
            {
                DrawNounIcon(spriteBatch, view, lemma, new Rectangle(left, y, 32, 32));
            }

            InkText(spriteBatch, big, title, new Vector2(x, y), headingColour);
        });

        if (heading.Pips is { } pips)
        {
            sheet.Add(BookGeometry.LineHeight, y =>
            {
                for (var i = 0; i < WordPips; i++)
                {
                    DrawPip(spriteBatch, view.Pixel, new Rectangle(left + (i * (Pip + PipGap)), y + 2, Pip, Pip), i < pips);
                }
            });
        }

        foreach (var text in new[] { heading.Aside, heading.Right })
        {
            if (text is not null)
            {
                AddWrapped(sheet, font, text, left, width, InkHint);
            }
        }

        sheet.Add(6, static _ => { });
        for (var i = 1; i < block.Count; i++)
        {
            var text = block[i];
            var inset = text.Tick is null ? 0 : 18;
            Action<int>? tick = text.Tick is { } ticked ? y => DrawTick(spriteBatch, view.Pixel, new Rectangle(left, y + 3, 11, 11), ticked) : null;
            AddWrapped(sheet, font, text.Text, left + inset, width - inset, ToneColour(text.Tone), tick);
            if (text.Aside is not null)
            {
                AddWrapped(sheet, font, text.Aside, left + inset, width - inset, InkHint);
            }

            if (text.Right is not null)
            {
                AddWrapped(sheet, font, text.Right, left + inset, width - inset, InkHint);
            }

            sheet.Add(4, static _ => { });
        }

        PaintDetail(spriteBatch, view, geometry, font, sheet, line);
    }

    // The right page of the verbs page: the verb and its English, the conjugation table, what the colours mean, the other forms met and the last sentence. The
    // whole page scrolls (X03).
    private static void DrawVerbDetail(SpriteBatch spriteBatch, FrameView view, BookGeometry geometry, IReadOnlyList<BookLine> lines, int selected)
    {
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var big = view.Fonts.Get(FontProvider.NativeSize * 2);
        var left = geometry.DetailLeft;
        var width = geometry.DetailRight - left;
        var sheet = new DetailSheet(spriteBatch);
        if (selected < 0 || lines[selected].Verb is not { } lemma || BookPages.VerbTable(view.Session, lemma) is not { } table)
        {
            sheet.Add(BookGeometry.LineHeight, y => InkText(spriteBatch, font, "Choose a verb on the left page.", new Vector2(left, y), InkHint));
            PaintDetail(spriteBatch, view, geometry, font, sheet, selected);
            return;
        }

        var title = Fit(big, table.Lemma, width);
        sheet.Add(34, y => InkText(spriteBatch, big, title, new Vector2(left, y), InkKnown));
        sheet.Add(BookGeometry.LineHeight + 6, y =>
        {
            if (table.Gloss is not null)
            {
                InkText(spriteBatch, font, table.Gloss, new Vector2(left, y), InkSeen);
            }
        });
        AddTable(sheet, view.Pixel, font, table, left, width);
        sheet.Add(6, static _ => { });
        AddWrapped(sheet, font, "Highlighted: said (x = times). Plain: taught. Dash: not yet.", left, width, InkHint);

        // What the learner said, as the verbs page listed it (S20): each form with its English, its count and the last sentence it was said in.
        var row = view.Session.Learner.Verbs.First(r => string.Equals(r.Lemma, lemma, StringComparison.Ordinal));
        var said = BookPages.Verbs([row], view.Session.Glosses, view.Session.Phrases, view.Session.Bundle.SpanishMorphology()).Skip(1).Where(static l => l.Text.Length > 0).ToList();
        sheet.Add(8, static _ => { });
        if (said.Count > 0)
        {
            sheet.Add(BookGeometry.LineHeight, y => InkText(spriteBatch, font, "What you said", new Vector2(left, y), InkKnown));
        }

        foreach (var form in said)
        {
            if (form.Right is { } count)
            {
                sheet.Add(BookGeometry.LineHeight, y =>
                {
                    var countWidth = (int)DrawHelpers.Width(font, count);
                    InkText(spriteBatch, font, count, new Vector2(left + width - countWidth, y), InkHint);
                    InkText(spriteBatch, font, Fit(font, form.Text, width - countWidth - Gutter), new Vector2(left, y), InkSeen);
                });
            }
            else
            {
                AddWrapped(sheet, font, form.Text, left + IndentWidth, width - IndentWidth, InkHint);
            }
        }

        PaintDetail(spriteBatch, view, geometry, font, sheet, selected);
    }

    // The table as rows of the scrolling page: a header row of tenses (row -1), then the five persons. Each row is drawn at its own top.
    private static void AddTable(DetailSheet sheet, Texture2D pixel, SpriteFontBase font, ConjugationTable table, int left, int width)
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
                sheet.Add(ConjugationLayout.RowHeight, y => DrawTableRow(sheet.Batch, pixel, font, table, left, y - ((row + 1) * ConjugationLayout.RowHeight), width, row, first, count));
            }
        }
    }

    // One row of a table: -1 is the header of tenses and the rule under it; a person's row draws its label and its cells. A used form is highlighted with its count;
    // a taught one is plain; a form not taught is a dash.
    private static void DrawTableRow(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, ConjugationTable table, int left, int top, int width, int r, int firstColumn, int columns)
    {
        if (r < 0)
        {
            for (var c = 0; c < columns; c++)
            {
                DrawCentred(spriteBatch, font, table.Columns[firstColumn + c].Title, ToRectangle(ConjugationLayout.Header(left, top, width, columns, c)), InkKnown);
            }

            spriteBatch.Draw(pixel, new Rectangle(left, top + ConjugationLayout.RowHeight - 2, width, 1), PageEdgeLine);
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
    private static void DrawCentred(SpriteBatch spriteBatch, SpriteFontBase font, string text, Rectangle area, Color colour)
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
            InkText(spriteBatch, font, line, new Vector2(area.X + ((area.Width - width) / 2), top), colour);
            top += lineHeight;
        }
    }

    // Wrapped text as rows of the right page: one row per line. The first line also draws what is given for it (a tick box beside a unit).
    private static void AddWrapped(DetailSheet sheet, SpriteFontBase font, string text, int x, int width, Color colour, Action<int>? atFirst = null)
    {
        var extra = atFirst;
        foreach (var line in Wrap(font, text, width))
        {
            var first = extra;
            extra = null;
            sheet.Add(BookGeometry.LineHeight, y =>
            {
                InkText(sheet.Batch, font, line, new Vector2(x, y), colour);
                first?.Invoke(y);
            });
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
            const string marker = "more below";
            var width = (int)DrawHelpers.Width(font, marker);
            InkText(spriteBatch, font, marker, new Vector2(geometry.DetailRight - width, geometry.Paper.Bottom - 22), InkHint);
        }
    }

    // Breaks a text at its spaces into lines no wider than a width (a word wider than the width stays whole on its own line; the caller cuts it).
    private static List<string> Wrap(SpriteFontBase font, string text, int width)
    {
        var result = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && DrawHelpers.Width(font, candidate) > width)
            {
                result.Add(current);
                current = word;
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

    // Text on paper: dark ink with no shadow (the panels' shadowed text is for dark backgrounds).
    private static void InkText(SpriteBatch spriteBatch, SpriteFontBase font, string text, Vector2 position, Color colour) =>
        spriteBatch.DrawString(font, text, position, colour);

    // The speaker button (S20b): a box with a speaker drawn in pixels; dark red-brown ink, with the selected row's button boxed in the brass of the select bar.
    private static void DrawSpeaker(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, bool selected)
    {
        var ink = selected ? InkKnown : InkSeen;
        spriteBatch.Draw(pixel, box, new Color(246, 236, 210));
        DrawHelpers.Border(spriteBatch, pixel, box, 1, selected ? InkKnown : InkHint);
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
            spriteBatch.Draw(view.Pixel, icon, PaperShade);
        }
    }

    private static void DrawTick(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, bool ticked)
    {
        DrawHelpers.Border(spriteBatch, pixel, box, 1, ticked ? InkKnown : InkHint);
        if (ticked)
        {
            // A tick: a short stroke down to the corner and a longer one up.
            spriteBatch.Draw(pixel, new Rectangle(box.X + 2, box.Y + 6, 2, 2), InkKnown);
            spriteBatch.Draw(pixel, new Rectangle(box.X + 4, box.Y + 7, 2, 2), InkKnown);
            spriteBatch.Draw(pixel, new Rectangle(box.X + 6, box.Y + 5, 2, 2), InkKnown);
            spriteBatch.Draw(pixel, new Rectangle(box.X + 8, box.Y + 3, 2, 2), InkKnown);
        }
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
            DrawHelpers.Border(spriteBatch, pixel, box, 1, InkHint);
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

    private static Color ToneColour(BookTone tone) => tone switch
    {
        BookTone.Known => InkKnown,
        BookTone.Learning => Ink,
        BookTone.Seen => InkSeen,
        BookTone.Unseen => InkUnseen,
        BookTone.Gold => InkKnown,
        _ => InkHint,
    };

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
