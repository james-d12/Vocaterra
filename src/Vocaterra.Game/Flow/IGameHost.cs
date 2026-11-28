using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Vocaterra.Game.Flow;

/// <summary>
/// What a world needs of the window it is played in (Q01): the graphics device, the window, the display's settings, whether the window has the focus, and the mouse pointer. The window and its graphics device are made once
/// for the whole run, by <see cref="AppGame"/>, and the title screen and every world are played in them one after the other: a second MonoGame window made in the same process does not run (its loop never starts), so a
/// world is not a window of its own.
/// </summary>
public interface IGameHost
{
    /// <summary>The graphics device of the run's one window.</summary>
    GraphicsDevice GraphicsDevice { get; }

    /// <summary>The window.</summary>
    GameWindow Window { get; }

    /// <summary>The display's settings (the window's size, full screen).</summary>
    GraphicsDeviceManager Graphics { get; }

    /// <summary>Whether the window has the focus.</summary>
    bool IsActive { get; }

    /// <summary>Whether the pointer is drawn over the window.</summary>
    bool IsMouseVisible { get; set; }
}
