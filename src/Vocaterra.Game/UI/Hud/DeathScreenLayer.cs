using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.Hud;

/// <summary>What the short screen after a death or a faint says (N05).</summary>
/// <param name="Spanish">The line in Spanish (<c>Estás en tu campamento.</c>).</param>
/// <param name="English">Its English.</param>
/// <param name="Cause">The cause line (<c>You fainted from thirst and woke by your fire...</c>).</param>
public sealed record DeathScreenView(string Spanish, string English, string Cause);

/// <summary>
/// The death screen (N05, design 9.7): after the fade to black, a short screen in the middle of it, «Estás en tu campamento.» large with its English beneath and the cause
/// line (what N04 says of the death) under that. Drawn over the black of <see cref="FadeLayer"/>; the world fades back in after it.
/// </summary>
public sealed class DeathScreenLayer : IFrameLayer
{
    private const int PhraseSize = 32;
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color Muted = new(190, 190, 205);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.DeathScreen is not { } screen)
        {
            return;
        }

        var small = view.Fonts.Get(FontProvider.NativeSize);
        var large = view.Fonts.Get(PhraseSize);
        var room = view.ViewportWidth - (2 * HudLayout.Margin * 2);
        var spanishRows = TextWrap.Wrap(screen.Spanish, room, text => DrawHelpers.Width(large, text));
        var causeRows = TextWrap.Wrap(screen.Cause, room, text => DrawHelpers.Width(small, text));
        var total = (spanishRows.Count * 40) + HudLayout.RowHeight + 14 + (causeRows.Count * HudLayout.RowHeight);
        var y = Math.Max(HudLayout.Margin, (view.ViewportHeight - total) / 2);
        foreach (var row in spanishRows)
        {
            DrawHelpers.Text(spriteBatch, large, row, new Vector2((view.ViewportWidth - DrawHelpers.Width(large, row)) / 2, y), Ink);
            y += 40;
        }

        DrawHelpers.Text(spriteBatch, small, screen.English, new Vector2((view.ViewportWidth - DrawHelpers.Width(small, screen.English)) / 2, y), DialoguePanel.EnglishColor);
        y += HudLayout.RowHeight + 14;
        foreach (var row in causeRows)
        {
            DrawHelpers.Text(spriteBatch, small, row, new Vector2((view.ViewportWidth - DrawHelpers.Width(small, row)) / 2, y), Muted);
            y += HudLayout.RowHeight;
        }
    }
}
