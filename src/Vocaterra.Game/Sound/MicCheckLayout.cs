using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.UI;

namespace Vocaterra.Game.Sound;

/// <summary>Where the mic-check screen's pieces are for a window, so that drawing and the mouse hit-test agree.</summary>
public readonly record struct MicCheckLayout
{
    /// <summary>The width of the screen's box.</summary>
    public const int Width = 560;

    /// <summary>The height of the screen's box.</summary>
    public const int Height = 330;

    /// <summary>Computes the layout.</summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="buttons">The buttons to lay out.</param>
    /// <param name="measure">The width of a text in pixels.</param>
    public MicCheckLayout(int viewportWidth, int viewportHeight, IReadOnlyList<MicCheckButton> buttons, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        ArgumentNullException.ThrowIfNull(measure);
        var width = Math.Min(Width, Math.Max(viewportWidth - 32, 200));
        Panel = new PixelBox((viewportWidth - width) / 2, Math.Max(8, (viewportHeight - Height) / 2), width, Height);
        Meter = new PixelBox(Panel.X + 40, Panel.Y + 150, Panel.Width - 80, 26);
        var placed = new List<(MicCheckButton Button, PixelBox Box)>();
        var x = Panel.X + 40;
        var y = Panel.Bottom - 54;
        foreach (var button in buttons)
        {
            var w = (int)Math.Ceiling(measure(MicCheckModel.LabelOf(button))) + 28;
            placed.Add((button, new PixelBox(x, y, w, 32)));
            x += w + 12;
        }

        Buttons = placed;
    }

    /// <summary>The screen's box.</summary>
    public PixelBox Panel { get; }

    /// <summary>The level meter's box.</summary>
    public PixelBox Meter { get; }

    /// <summary>The buttons with their boxes.</summary>
    public IReadOnlyList<(MicCheckButton Button, PixelBox Box)> Buttons { get; }

    /// <summary>The button under a point, or <see langword="null"/>.</summary>
    /// <param name="x">The pointer's x.</param>
    /// <param name="y">The pointer's y.</param>
    public MicCheckButton? ButtonAt(int x, int y)
    {
        foreach (var (button, box) in Buttons)
        {
            if (box.Contains(x, y))
            {
                return button;
            }
        }

        return null;
    }
}
