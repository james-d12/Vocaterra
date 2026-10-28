using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Input;

namespace Vocaterra.Game.Screens;

/// <summary>The controls help (F1, S16): the list of keys; when it does not all fit the canvas (a large text size), Up, Down, Page Up, Page Down and the wheel scroll it (X08). Esc or F1 closes it.</summary>
public sealed class ControlsHelp : IScreen
{
    private readonly Func<int> _maxScroll;

    /// <summary>Creates the help.</summary>
    /// <param name="maxScroll">How far the list can scroll on the canvas as it is now, in lines.</param>
    public ControlsHelp(Func<int> maxScroll) => _maxScroll = maxScroll;

    /// <summary>Whether the help is open.</summary>
    public bool IsOpen { get; private set; }

    /// <inheritdoc />
    public string Name => "controls (F1)";

    /// <inheritdoc />
    public bool FreesMouse => true;

    /// <summary>How far the list is scrolled, in lines.</summary>
    public int Scroll { get; private set; }

    /// <summary>Opens the help.</summary>
    public void Open() => IsOpen = true;

    /// <summary>The help's keys and wheel while it is open.</summary>
    public void HandleInput(IInputFrame input)
    {
        if (input.Pressed(GameKey.Escape) || input.Pressed(GameKey.F1))
        {
            IsOpen = false;
            Scroll = 0;
            return;
        }

        var step = (input.Pressed(GameKey.Down) ? 1 : 0) - (input.Pressed(GameKey.Up) ? 1 : 0) + (input.Pressed(GameKey.PageDown) ? 5 : 0) - (input.Pressed(GameKey.PageUp) ? 5 : 0);
        var wheelNotches = -(int)Math.Round(input.Wheel / 120.0);
        Scroll = Math.Clamp(Scroll + step + wheelNotches, 0, _maxScroll());
    }
}
