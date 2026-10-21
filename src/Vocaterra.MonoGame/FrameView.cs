using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation;

namespace Vocaterra.MonoGame;

/// <summary>
/// What a layer may read while drawing one frame: the simulation (read only by convention), the camera for this frame, the interpolated
/// player box and the frame statistics. The game owns and refreshes one instance per frame; layers must not keep it past the call.
/// </summary>
public sealed class FrameView
{
    /// <summary>The simulation being shown.</summary>
    public required GameSimulation Simulation { get; init; }

    /// <summary>The running game: the guide, the arrow and the conversation (read only for layers).</summary>
    public required GameSession Session { get; init; }

    /// <summary>The dialogue panel's state.</summary>
    public required DialogueModel Dialogue { get; init; }

    /// <summary>The camera, already following the player for this frame.</summary>
    public required Camera Camera { get; init; }

    /// <summary>The fonts (FontStashSharp over the committed TTF).</summary>
    public required FontProvider Fonts { get; init; }

    /// <summary>A 1 x 1 white texture: scale and tint it to draw rectangles.</summary>
    public required Texture2D Pixel { get; init; }

    /// <summary>The world seed.</summary>
    public required ulong Seed { get; init; }

    /// <summary>The player box between the previous and the latest tick (see <see cref="TickAccumulator.Alpha"/>), in simulation units.</summary>
    public Rectangle PlayerUnits { get; set; }

    /// <summary>The way the player faces: -1 left, 1 right.</summary>
    public int Facing { get; set; } = 1;

    /// <summary>Frames per second over the last half second.</summary>
    public double Fps { get; set; }

    /// <summary>The ticks dropped so far because a frame fell too far behind.</summary>
    public long DroppedTicks { get; set; }

    /// <summary>The mouse pointer in window pixels, or <see langword="null"/> when it is outside the window.</summary>
    public Point? Mouse { get; set; }
}

/// <summary>
/// A piece of the screen drawn after the world, in order: the guide, the arrow, the debug overlay and the dialogue panel (P3); the word and inventory UI (P4, P6) later.
/// A layer reads <see cref="FrameView"/> and draws with the <see cref="SpriteBatch"/> the game has already begun (point sampling, alpha blend).
/// </summary>
public interface IFrameLayer
{
    /// <summary>Draws the layer on top of what is already on screen.</summary>
    void Draw(SpriteBatch spriteBatch, FrameView view);
}
