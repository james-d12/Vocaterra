using Vocaterra.Game.Core.Title;

namespace Vocaterra.Game.UI.Title;

/// <summary>What the title screen's state machine asks of the drawn pages (<see cref="ITitleMetrics"/>), answered from <see cref="TitleLayout"/> on the canvas as it is now, so a click lands on what is drawn at any text size.</summary>
public sealed class TitleMetrics : ITitleMetrics
{
    private readonly Func<(int Width, int Height)> _canvas;

    /// <summary>Creates the metrics.</summary>
    /// <param name="canvas">The canvas's size now.</param>
    public TitleMetrics(Func<(int Width, int Height)> canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        _canvas = canvas;
    }

    /// <inheritdoc />
    public int LoadCapacity => LayoutOf(TitlePage.Load).Capacity;

    /// <inheritdoc />
    public int? SlotAt(TitlePage page, int loadScroll, int worldCount, int x, int y)
    {
        var layout = LayoutOf(page);
        switch (page)
        {
            case TitlePage.Main:
                for (var i = 0; i < TitleSlot.MainCount; i++)
                {
                    if (layout.Button(i).Contains(x, y))
                    {
                        return i;
                    }
                }

                return null;
            case TitlePage.New:
                if (layout.NewBack.Contains(x, y))
                {
                    return TitleSlot.Back;
                }

                if (layout.Create.Contains(x, y))
                {
                    return TitleSlot.Create;
                }

                if (layout.NameField.Contains(x, y))
                {
                    return TitleSlot.NameField;
                }

                if (layout.SeedField.Contains(x, y))
                {
                    return TitleSlot.SeedField;
                }

                if (layout.RandomButton.Contains(x, y))
                {
                    return TitleSlot.RandomSeed;
                }

                return layout.TypeRow.Contains(x, y) ? TitleSlot.WorldType : null;
            case TitlePage.Load:
                if (layout.LoadBack.Contains(x, y))
                {
                    return TitleSlot.Back;
                }

                if (layout.LoadButton.Contains(x, y))
                {
                    return TitleSlot.LoadAction;
                }

                if (layout.DeleteButton.Contains(x, y))
                {
                    return TitleSlot.DeleteAction;
                }

                for (var shown = 0; shown < layout.Capacity && loadScroll + shown < worldCount; shown++)
                {
                    if (layout.WorldRow(shown).Contains(x, y))
                    {
                        return loadScroll + shown;
                    }
                }

                return null;
            case TitlePage.ConfirmDelete:
                if (layout.ConfirmDelete.Contains(x, y))
                {
                    return TitleSlot.ConfirmDelete;
                }

                return layout.ConfirmKeep.Contains(x, y) ? TitleSlot.ConfirmKeep : null;
            default:
                return null; // the settings page is the menu's own: its metrics answer
        }
    }

    private TitleLayout LayoutOf(TitlePage page)
    {
        var (width, height) = _canvas();
        return new TitleLayout(width, height, page);
    }
}
