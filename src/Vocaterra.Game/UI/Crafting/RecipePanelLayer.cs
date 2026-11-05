using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.UI.Crafting;

/// <summary>
/// The workbench's recipe panel (S15, reference C): while the player stands at the workbench, a dark rounded panel at the right of the screen shows what the
/// recipe makes at the top and, below the arrow, each ingredient as an icon with its <c>have/need</c> count (green when there is enough, red when there is not).
/// It is the glance before the craft; the full recipe book is still on C. It is hidden while any other panel is open.
/// </summary>
public sealed class RecipePanelLayer : IFrameLayer
{

    private long _tick = -1;
    private RecipePanelModel? _model;
    private bool _atFire;

    private const int Slot = 60;
    private const int Gap = 8;
    private static readonly Color Back = new(18, 20, 26, 232);
    private static readonly Color Edge = new(104, 108, 122);
    private static readonly Color SlotBack = new(34, 36, 44, 240);
    private static readonly Color SlotEdge = new(70, 74, 88);
    private static readonly Color Warm = new(255, 204, 110);
    private static readonly Color Enough = new(130, 232, 140);
    private static readonly Color Short = new(246, 118, 108);
    private static readonly Color Soft = new(168, 170, 182);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Voxel is null || view.Dialogue.IsOpen || view.Book.IsOpen || view.Inventory.IsOpen || view.Crafting.IsOpen || view.ControlsOpen || view.Check is not null)
        {
            return;
        }

        // The model is made once a tick, and only at a station (the workbench, or a lit fire, N07): away from it the layer costs one look round the player a tick.
        if (view.Voxel.Tick != _tick)
        {
            _tick = view.Voxel.Tick;
            _model = null;
            _atFire = false;
            if (view.Voxel.StationInReach(BlockId.Table) || view.Voxel.StationInReach(BlockId.FireRing))
            {
                var panels = RecipePanelModel.AtStation(CraftingScreen.Rows(view.Voxel, view.Session.Recipes), view.Voxel.StationInReach);
                _model = panels.FirstOrDefault(static p => p.CanMake) ?? (panels.Count > 0 ? panels[0] : null); // the one the player can make now, else the first
                _atFire = _model is { } m && view.Session.Recipes.Entries[m.Index].Rule.Station == BlockId.FireRing;
            }
        }

        if (_model is not { } model)
        {
            return;
        }

        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var slots = Math.Max(1, model.Ingredients.Count);
        var width = Math.Max(2 * Slot, (slots * Slot) + ((slots - 1) * Gap)) + 48;
        var height = 34 + Slot + 36 + Slot + 22 + 32;
        var placed = HudLayout.RecipePanelBox(view.ViewportWidth, view.ViewportHeight, width, height, ObjectiveLayer.BottomOf(view)); // under the checklist, above the hotbar (X08)
        var panel = new Rectangle(placed.X, placed.Y, placed.Width, placed.Height);
        DrawHelpers.RoundedPanel(spriteBatch, pixel, panel, 8, Back, Edge, 2);
        var ui = view.Session.Locale;
        DrawHelpers.Text(spriteBatch, font, ui.Ui(_atFire ? "fireTitle" : "workbenchTitle"), new Vector2(panel.X + 16, panel.Y + 8), Warm);

        // The product: its icon in a framed slot, with the number one craft makes.
        var outputBox = new Rectangle(panel.X + ((panel.Width - Slot) / 2), panel.Y + 34, Slot, Slot);
        DrawSlot(spriteBatch, view, font, outputBox, model.Output, model.OutputCount > 1 ? model.OutputCount.ToString(System.Globalization.CultureInfo.InvariantCulture) : null, model.CanMake);

        // The arrow from the ingredients to the product (it points up: the ingredients are below).
        var arrowX = panel.X + (panel.Width / 2);
        var arrowTop = outputBox.Bottom + 6;
        spriteBatch.Draw(pixel, new Rectangle(arrowX - 1, arrowTop + 5, 3, 18), Soft);
        for (var i = 0; i < 5; i++)
        {
            spriteBatch.Draw(pixel, new Rectangle(arrowX - i, arrowTop + i, (2 * i) + 1, 1), Soft);
        }

        var rowWidth = (slots * Slot) + ((slots - 1) * Gap);
        var left = panel.X + ((panel.Width - rowWidth) / 2);
        var top = arrowTop + 30;
        for (var i = 0; i < model.Ingredients.Count; i++)
        {
            var part = model.Ingredients[i];
            var box = new Rectangle(left + (i * (Slot + Gap)), top, Slot, Slot);
            DrawSlot(spriteBatch, view, font, box, part.Item, null, part.Enough);
            var text = part.Counts;
            var textWidth = DrawHelpers.Width(font, text);
            DrawHelpers.Text(spriteBatch, font, text, new Vector2(box.X + ((Slot - textWidth) / 2), box.Bottom + 4), part.Enough ? Enough : Short);
        }

        var hint = model.CanMake ? ui.Ui("workbenchReady") : ui.Ui("workbenchMissing");
        DrawHelpers.Text(spriteBatch, font, hint, new Vector2(panel.X + ((panel.Width - DrawHelpers.Width(font, hint)) / 2), panel.Bottom - 28), model.CanMake ? Enough : Soft);
    }

    private static void DrawSlot(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, Rectangle box, ItemId item, string? badge, bool ready)
    {
        DrawHelpers.RoundedPanel(spriteBatch, view.Pixel, box, 5, SlotBack, ready ? Enough : SlotEdge, ready ? 2 : 1);
        view.DrawIcon(spriteBatch, item, new Rectangle(box.X + 8, box.Y + 8, Slot - 16, Slot - 16));
        if (badge is not null)
        {
            DrawHelpers.Badge(spriteBatch, view.Pixel, font, badge, new Point(box.Right - 3, box.Bottom - 3));
        }
    }
}
