using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Menu;

/// <summary>The menu (Esc): the main page, the settings and the controls, in English, over the dimmed world. The state is <see cref="Core.Menu.MenuScreen"/>; <see cref="MenuPainter"/> draws it.</summary>
public sealed class MenuLayer : IFrameLayer
{
    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Menu is not { IsOpen: true } menu)
        {
            return;
        }

        MenuPainter.Draw(spriteBatch, view.Pixel, view.Fonts.Get(FontProvider.NativeSize), view.Session.Locale, menu, view.ViewportWidth, view.ViewportHeight, view.ControlLines, dimBackground: true);
    }
}
