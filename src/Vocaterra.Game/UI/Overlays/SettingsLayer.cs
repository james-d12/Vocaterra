using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.Overlays;

/// <summary>The settings panel of N09 (F8): the drive rate slider and the two needs toggles, in English, over the world.</summary>
public sealed class SettingsLayer : IFrameLayer
{
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Back = new(12, 16, 30, 240);
    private static readonly Color Dim = new(180, 180, 200);
    private static readonly Color Track = new(70, 76, 100);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.SettingsOpen)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var ui = view.Session.Locale;
        var layout = new SettingsLayout(view.ViewportWidth, view.ViewportHeight, SettingsPanel.RowCount);
        var box = Rect(layout.Panel);
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, DialoguePanel.EnglishColor);
        DrawHelpers.Text(spriteBatch, font, ui.Ui("settingsTitle"), new Vector2(box.X + 16, box.Y + 12), Gold);
        string[] labels = [ui.Ui("settingsRate"), ui.Ui("settingsEffects"), ui.Ui("settingsRelaxed")];
        string[] values = [SettingsPanel.RateText(view.SettingsRate), ui.Ui(view.SettingsEffectsOn ? "settingsOn" : "settingsOff"), ui.Ui(view.SettingsRelaxed ? "settingsOn" : "settingsOff")];
        for (var i = 0; i < SettingsPanel.RowCount; i++)
        {
            var selected = i == view.SettingsSelected;
            var row = Rect(layout.Row(i));
            if (selected)
            {
                DrawHelpers.Border(spriteBatch, view.Pixel, row, 1, Gold);
            }

            var label = Rect(layout.Label(i));
            var value = Rect(layout.Value(i));
            var textY = row.Y + ((row.Height - 18) / 2);
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, labels[i], label.Width), new Vector2(label.X, textY), selected ? Gold : DialoguePanel.EnglishColor);
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, values[i], value.Width), new Vector2(value.X, textY), selected ? Gold : DialoguePanel.EnglishColor);
        }

        var track = Rect(layout.Track);
        spriteBatch.Draw(view.Pixel, track, Track);
        var share = (view.SettingsRate - SaveSettings.MinNeedsRate) / (SaveSettings.MaxNeedsRate - SaveSettings.MinNeedsRate);
        var knobX = track.X + (int)Math.Round(share * (track.Width - 8));
        spriteBatch.Draw(view.Pixel, new Rectangle(knobX, track.Y - 5, 8, track.Height + 10), Gold);
        var hint = layout.Hint;
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, ui.Ui("settingsHint"), hint.Width), new Vector2(hint.X, hint.Y), Dim);
    }

    private static Rectangle Rect(PixelBox b) => new(b.X, b.Y, b.Width, b.Height);
}
