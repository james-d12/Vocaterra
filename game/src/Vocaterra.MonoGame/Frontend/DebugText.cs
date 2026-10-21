using System.Globalization;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>The words of the debug overlay, built without any engine type so they can be tested.</summary>
public static class DebugText
{
    /// <summary>A Spanish test string that needs every accent the font must cover; also shows the font works at run time.</summary>
    public const string SpanishTestLine = "¿Dónde está el árbol? ¡Mañana!";

    /// <summary>The overlay's lines, top to bottom.</summary>
    /// <param name="fps">Frames per second.</param>
    /// <param name="tick">The simulation tick.</param>
    /// <param name="playerTile">The tile under the player's feet.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="mouseTile">The tile under the mouse pointer, if the pointer is in the window.</param>
    public static IReadOnlyList<string> Lines(double fps, long tick, TilePoint playerTile, ulong seed, TilePoint? mouseTile)
    {
        var inv = CultureInfo.InvariantCulture;
        var mouse = mouseTile is { } m ? string.Create(inv, $"({m.X}, {m.Y})") : "-";
        return
        [
            string.Create(inv, $"FPS {fps:0}   tick {tick}   seed {seed}"),
            string.Create(inv, $"player tile ({playerTile.X}, {playerTile.Y})   mouse tile {mouse}"),
            SpanishTestLine,
        ];
    }
}
