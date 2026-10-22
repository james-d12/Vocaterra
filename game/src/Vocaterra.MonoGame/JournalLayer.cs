using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The journal (J): every catalogue noun with its article once it has been seen (<c>el árbol</c>, <c>¿?</c> before), its icon in its current
/// colour, the counts (heard, read, said, said correctly) and three progress pips (one per correct use; the noun is known with all three).
/// It covers the screen while it is open and takes the keyboard (the player does not walk).
/// </summary>
public sealed class JournalLayer : IFrameLayer
{
    /// <summary>The title over the list.</summary>
    public const string Title = "Diario de palabras";

    /// <summary>The key hints under the list.</summary>
    public const string KeyHints = "J o Esc: cerrar el diario";

    private const int Margin = 16;
    private const int Padding = 16;
    private const int RowHeight = 36;
    private const int Icon = 24;
    private const int Pip = 10;
    private const int WordPips = Vocaterra.Game.WordProgress.UsesToKnow;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Known = new(255, 236, 160);
    private static readonly Color Learning = new(235, 235, 235);
    private static readonly Color Seen = new(190, 190, 200);
    private static readonly Color Unseen = new(110, 110, 120);
    private static readonly Color Hints = new(140, 140, 150);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.Journal.IsOpen)
        {
            return;
        }

        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var viewport = new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight);
        spriteBatch.Draw(pixel, viewport, new Color(6, 6, 14, 236));

        var width = Math.Min(760, viewport.Width - (Margin * 2));
        var panel = new Rectangle((viewport.Width - width) / 2, Margin, width, viewport.Height - (Margin * 2));
        spriteBatch.Draw(pixel, panel, new Color(18, 18, 34, 250));
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Gold);

        var words = view.Session.Words;
        var now = view.Session.StoredTime.UnixSeconds;
        var rows = JournalRows.Build(words, now);
        var known = rows.Count(static r => r.Known);
        var left = panel.Left + Padding;
        var textWidth = panel.Width - (Padding * 2);
        DrawHelpers.Text(spriteBatch, font, Title, new Vector2(left, panel.Top + 12), Gold);
        var summary = $"Conocidas: {known} de {rows.Count}";
        DrawHelpers.Text(spriteBatch, font, summary, new Vector2(panel.Right - Padding - DrawHelpers.Width(font, summary), panel.Top + 12), Known);
        spriteBatch.Draw(pixel, new Rectangle(left, panel.Top + 40, textWidth, 1), new Color(255, 214, 64, 120));

        var y = panel.Top + 50;
        var bottom = panel.Bottom - 48;
        var capacity = Math.Max(1, (bottom - y) / RowHeight);
        foreach (var row in rows.Take(capacity))
        {
            DrawRow(spriteBatch, view, font, row, left, y, textWidth);
            y += RowHeight;
        }

        if (rows.Count > capacity)
        {
            var more = $"y {rows.Count - capacity} más";
            DrawHelpers.Text(spriteBatch, font, more, new Vector2(left, y), Hints);
        }

        DrawHelpers.Text(spriteBatch, font, KeyHints, new Vector2(left, panel.Bottom - 30), Hints);
    }

    private static void DrawRow(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, JournalRow row, int left, int top, int width)
    {
        var pixel = view.Pixel;
        var noun = view.Session.Nouns.ForLemma(row.Lemma);
        var icon = new Rectangle(left, top + 4, Icon, Icon);
        if (noun is { Items.Count: > 0 })
        {
            view.VoxelIcons.DrawIcon(spriteBatch, noun.Items[0], icon, row.Saturation);
        }
        else
        {
            spriteBatch.Draw(pixel, icon, new Color(40, 40, 50));
        }

        var colour = row.Known ? Known : row.Record.CreditUnits > 0 ? Learning : row.Seen ? Seen : Unseen;
        DrawHelpers.Text(spriteBatch, font, row.Label, new Vector2(left + Icon + 10, top + 4), colour);

        var counts = $"oído {row.Record.Heard}   leído {row.Record.Read}   dicho {row.Record.Spoken}   bien {row.Record.SpokenCorrect}";
        DrawHelpers.Text(spriteBatch, font, counts, new Vector2(left + Icon + 10, top + 20), Hints);

        // Three pips at the right: filled for each use in, outlined for each still to come.
        var pipLeft = left + width - ((WordPips * Pip) + ((WordPips - 1) * 6));
        for (var i = 0; i < WordPips; i++)
        {
            var box = new Rectangle(pipLeft + (i * (Pip + 6)), top + 5, Pip, Pip);
            if (i < row.Pips)
            {
                spriteBatch.Draw(pixel, box, Gold);
            }
            else
            {
                DrawHelpers.Border(spriteBatch, pixel, box, 1, Hints);
            }
        }

        if (row.Known)
        {
            const string Tag = "conocida";
            DrawHelpers.Text(spriteBatch, font, Tag, new Vector2(pipLeft - DrawHelpers.Width(font, Tag) - 14, top + 2), Gold);
        }

        if (row.DueText is { } due)
        {
            var dueWidth = DrawHelpers.Width(font, due);
            DrawHelpers.Text(spriteBatch, font, due, new Vector2(left + width - dueWidth, top + 20), row.IsDue ? Gold : Hints);
        }
    }
}
