using FontStashSharp;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Menu;

namespace Vocaterra.Game.UI.Menu;

/// <summary>The controls page's wrapped lines, kept until the panel's columns change (the canvas or the text size), so the layer and the pointer's metrics wrap the table once, not every frame.</summary>
public sealed class ControlsPageCache
{
    private readonly LocaleText _locale;
    private readonly Func<SpriteFontBase> _font;
    private IReadOnlyList<ControlsPageLine>? _lines;
    private (int Keys, int Text) _key;

    /// <summary>Creates the cache.</summary>
    /// <param name="locale">The locale whose words the page uses.</param>
    /// <param name="font">The font the page is drawn in (once it exists).</param>
    public ControlsPageCache(LocaleText locale, Func<SpriteFontBase> font)
    {
        _locale = locale;
        _font = font;
    }

    /// <summary>The lines of the page for a layout.</summary>
    /// <param name="layout">The controls page's layout.</param>
    public IReadOnlyList<ControlsPageLine> For(MenuLayout layout)
    {
        var key = (layout.KeysWidth, layout.TextWidth);
        if (_lines is null || key != _key)
        {
            _key = key;
            _lines = ControlsPageLines.Build(ControlsListing.Rows(_locale.Ui, _locale.KeyName), layout, _font());
        }

        return _lines;
    }
}
