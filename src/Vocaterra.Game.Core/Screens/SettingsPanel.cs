using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Save;

namespace Vocaterra.Game.Core.Screens;

/// <summary>
/// The settings panel (N09, F8): the drive rate as a slider, and the two needs settings that are toggles (the view effects, the relaxed needs), each applied at once. Up and Down
/// choose a row; Left and Right (or - and +) move the slider by one step, Enter or Space flips a toggle; Esc or F8 closes. It pauses the needs while open (a menu pause).
/// </summary>
public sealed class SettingsPanel : IScreen
{
    /// <summary>The number of rows: the rate, the view effects, the relaxed needs.</summary>
    public const int RowCount = 3;

    private readonly Func<double> _rate;
    private readonly Action<double> _setRate;
    private readonly Func<bool> _effects;
    private readonly Action<bool> _setEffects;
    private readonly Func<bool> _relaxed;
    private readonly Action<bool> _setRelaxed;

    /// <summary>Creates the panel over the settings it changes.</summary>
    /// <param name="rate">Reads the drive rate.</param>
    /// <param name="setRate">Sets the drive rate (already snapped to a step).</param>
    /// <param name="effects">Reads whether the view effects are on.</param>
    /// <param name="setEffects">Sets the view effects.</param>
    /// <param name="relaxed">Reads whether the needs are relaxed.</param>
    /// <param name="setRelaxed">Sets the relaxed needs.</param>
    public SettingsPanel(Func<double> rate, Action<double> setRate, Func<bool> effects, Action<bool> setEffects, Func<bool> relaxed, Action<bool> setRelaxed)
    {
        _rate = rate;
        _setRate = setRate;
        _effects = effects;
        _setEffects = setEffects;
        _relaxed = relaxed;
        _setRelaxed = setRelaxed;
    }

    /// <summary>Whether the panel is open.</summary>
    public bool IsOpen { get; private set; }

    /// <inheritdoc />
    public string Name => "settings (F8)";

    /// <inheritdoc />
    public bool FreesMouse => true;

    /// <summary>The chosen row: 0 the rate, 1 the view effects, 2 the relaxed needs.</summary>
    public int Selected { get; private set; }

    /// <summary>The drive rate now.</summary>
    public double Rate => _rate();

    /// <summary>Whether the view effects are on.</summary>
    public bool EffectsOn => _effects();

    /// <summary>Whether the needs are relaxed.</summary>
    public bool Relaxed => _relaxed();

    /// <summary>Opens the panel on its first row.</summary>
    public void Open()
    {
        IsOpen = true;
        Selected = 0;
    }

    /// <summary>The slider's text: the rate as a multiplier, "×1.25".</summary>
    /// <param name="rate">The rate.</param>
    public static string RateText(double rate) => "×" + rate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public void HandleInput(IInputFrame input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Pressed(GameKey.Escape) || input.Pressed(GameKey.F8))
        {
            IsOpen = false;
            return;
        }

        if (input.Pressed(GameKey.Down))
        {
            Selected = (Selected + 1) % RowCount;
        }
        else if (input.Pressed(GameKey.Up))
        {
            Selected = (Selected + RowCount - 1) % RowCount;
        }

        var step = (input.Pressed(GameKey.Right) || input.Pressed(GameKey.OemPlus) ? 1 : 0) - (input.Pressed(GameKey.Left) || input.Pressed(GameKey.OemMinus) ? 1 : 0);
        var flip = input.Pressed(GameKey.Enter) || input.Pressed(GameKey.Space);
        switch (Selected)
        {
            case 0 when step != 0:
                _setRate(SaveSettings.SnapNeedsRate(_rate() + (step * SaveSettings.NeedsRateStep)));
                break;
            case 1 when flip || step != 0:
                _setEffects(!_effects());
                break;
            case 2 when flip || step != 0:
                _setRelaxed(!_relaxed());
                break;
        }
    }
}
