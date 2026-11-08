using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Overlays;

/// <summary>
/// The settings panel on a canvas (N09): a title, three rows (the drive rate slider, the two toggles) and a hint, in a panel centred on the canvas and kept inside it at
/// every window size and text scale. Plain numbers; the rows never overlap.
/// </summary>
public readonly record struct SettingsLayout
{
    /// <summary>The space above the first row (the title).</summary>
    public const int HeaderHeight = 44;

    /// <summary>The height of a row.</summary>
    public const int RowHeight = 36;

    /// <summary>The space under the last row (the hint).</summary>
    public const int FooterHeight = 34;

    /// <summary>The inner padding of the panel.</summary>
    public const int Pad = 16;

    /// <summary>The width of the labels' column.</summary>
    public const int LabelWidth = 130;

    /// <summary>The width of the values' column at the right ("×1.25", "off").</summary>
    public const int ValueWidth = 64;

    /// <summary>Computes the layout.</summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="rows">The number of rows.</param>
    public SettingsLayout(int viewportWidth, int viewportHeight, int rows)
    {
        var width = Math.Min(460, viewportWidth - 32);
        var height = Math.Min(HeaderHeight + (rows * RowHeight) + FooterHeight, viewportHeight - 16);
        Panel = new PixelBox((viewportWidth - width) / 2, Math.Max(8, (viewportHeight - height) / 2), width, height);
        Rows = rows;
    }

    /// <summary>The panel.</summary>
    public PixelBox Panel { get; }

    /// <summary>The number of rows.</summary>
    public int Rows { get; }

    /// <summary>The box of a row, the width of the panel inside its padding.</summary>
    /// <param name="index">The row.</param>
    public PixelBox Row(int index) => new(Panel.X + Pad / 2, Panel.Y + HeaderHeight + (index * RowHeight), Panel.Width - Pad, RowHeight - 4);

    /// <summary>The box of a row's label.</summary>
    /// <param name="index">The row.</param>
    public PixelBox Label(int index) => new(Panel.X + Pad, Row(index).Y, LabelWidth, Row(index).Height);

    /// <summary>The box of a row's value at the right.</summary>
    /// <param name="index">The row.</param>
    public PixelBox Value(int index) => new(Panel.Right - Pad - ValueWidth, Row(index).Y, ValueWidth, Row(index).Height);

    /// <summary>The slider's track on the first row, between the label and the value.</summary>
    public PixelBox Track
    {
        get
        {
            var x = Panel.X + Pad + LabelWidth + 8;
            return new PixelBox(x, Row(0).Y + (Row(0).Height / 2) - 3, Math.Max(20, Panel.Right - Pad - ValueWidth - 8 - x), 6);
        }
    }

    /// <summary>The hint's box under the rows.</summary>
    public PixelBox Hint => new(Panel.X + Pad, Panel.Bottom - FooterHeight + 6, Panel.Width - (2 * Pad), 22);
}
