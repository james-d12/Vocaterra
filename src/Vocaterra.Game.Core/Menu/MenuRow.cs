namespace Vocaterra.Game.Core.Menu;

/// <summary>
/// One row of the settings page: a heading, a note, or a control with its label, its value and what changing it does. Rows are built by <see cref="MenuRows"/> over the settings
/// and the testing hooks; the page draws them and <see cref="MenuScreen"/> changes them, so no row knows about keys or the screen.
/// </summary>
public sealed class MenuRow
{
    private readonly Func<int>? _get;
    private readonly Action<int>? _set;
    private readonly Action? _run;

    private MenuRow(string id, MenuRowKind kind, string labelKey, int min, int max, int step, string suffix, IReadOnlyList<string>? choiceKeys, Func<int>? get, Action<int>? set, Action? run)
    {
        Id = id;
        Kind = kind;
        LabelKey = labelKey;
        Min = min;
        Max = max;
        Step = step;
        Suffix = suffix;
        ChoiceKeys = choiceKeys ?? [];
        _get = get;
        _set = set;
        _run = run;
    }

    /// <summary>The row's name (the setting's, or a testing row's).</summary>
    public string Id { get; }

    /// <summary>What the row is.</summary>
    public MenuRowKind Kind { get; }

    /// <summary>The locale's <c>ui.&lt;key&gt;</c> of the row's text (its label, a heading, a note, a button's name).</summary>
    public string LabelKey { get; }

    /// <summary>The lowest value of a slider or a choice.</summary>
    public int Min { get; }

    /// <summary>The highest value.</summary>
    public int Max { get; }

    /// <summary>How far one press of Left or Right moves a slider.</summary>
    public int Step { get; }

    /// <summary>What follows a slider's number.</summary>
    public string Suffix { get; }

    /// <summary>The locale's keys of a choice's values (and of a toggle's off and on).</summary>
    public IReadOnlyList<string> ChoiceKeys { get; }

    /// <summary>Whether the cursor can stop on the row.</summary>
    public bool Selectable => Kind is not (MenuRowKind.Heading or MenuRowKind.Note);

    /// <summary>The value now (0 for a row that has none).</summary>
    public int Value => _get?.Invoke() ?? 0;

    /// <summary>A heading.</summary>
    /// <param name="labelKey">The locale's key of its text.</param>
    public static MenuRow Heading(string labelKey) => new("heading." + labelKey, MenuRowKind.Heading, labelKey, 0, 0, 0, string.Empty, null, null, null, null);

    /// <summary>A line of text.</summary>
    /// <param name="labelKey">The locale's key of its text.</param>
    public static MenuRow Note(string labelKey) => new("note." + labelKey, MenuRowKind.Note, labelKey, 0, 0, 0, string.Empty, null, null, null, null);

    /// <summary>The row of a setting, changing it in <paramref name="settings"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="id">The setting.</param>
    public static MenuRow Of(PlayerSettings settings, SettingId id)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var spec = PlayerSettings.Spec(id);
        var kind = spec.Kind switch { SettingKind.Slider => MenuRowKind.Slider, SettingKind.Choice => MenuRowKind.Choice, _ => MenuRowKind.Toggle };
        return new(id.ToString(), kind, spec.LabelKey, spec.Min, spec.Max, spec.Step, spec.Suffix, spec.ChoiceKeys, () => settings.Get(id), value => settings.Set(id, value), null);
    }

    /// <summary>A toggle over something the front-end holds.</summary>
    /// <param name="id">The row's name.</param>
    /// <param name="labelKey">The locale's key of its label.</param>
    /// <param name="get">Whether it is on.</param>
    /// <param name="set">Sets it.</param>
    public static MenuRow Switch(string id, string labelKey, Func<bool> get, Action<bool> set)
    {
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        return new(id, MenuRowKind.Toggle, labelKey, 0, 1, 1, string.Empty, ["settingsOff", "settingsOn"], () => get() ? 1 : 0, value => set(value == 1), null);
    }

    /// <summary>A button.</summary>
    /// <param name="id">The row's name.</param>
    /// <param name="labelKey">The locale's key of its name.</param>
    /// <param name="run">What it does.</param>
    public static MenuRow Button(string id, string labelKey, Action run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new(id, MenuRowKind.Action, labelKey, 0, 0, 0, string.Empty, null, null, null, run);
    }

    /// <summary>Left or Right: a slider moves by a step and stops at its ends, a choice cycles round, a toggle flips.</summary>
    /// <param name="direction">1 for Right, -1 for Left.</param>
    public void Change(int direction)
    {
        if (_get is null || _set is null || direction == 0)
        {
            return;
        }

        var sign = Math.Sign(direction);
        switch (Kind)
        {
            case MenuRowKind.Slider:
                _set(Math.Clamp(_get() + (sign * Step), Min, Max));
                break;
            case MenuRowKind.Choice:
                var count = Max - Min + 1;
                _set(Min + ((_get() - Min + sign + count) % count));
                break;
            case MenuRowKind.Toggle:
                _set(1 - _get());
                break;
        }
    }

    /// <summary>Enter or Space or a click: a toggle flips, a choice goes to the next value, a button does its work; a slider does nothing.</summary>
    public void Activate()
    {
        if (Kind == MenuRowKind.Action)
        {
            _run!();
        }
        else if (Kind is MenuRowKind.Choice or MenuRowKind.Toggle)
        {
            Change(1);
        }
    }

    /// <summary>Puts a slider at a place along its track (the mouse): 0 is the lowest value, 1 the highest, snapped to the steps.</summary>
    /// <param name="fraction">How far along the track, 0 to 1.</param>
    public void SetFraction(double fraction)
    {
        if (Kind != MenuRowKind.Slider || _set is null)
        {
            return;
        }

        var clamped = double.IsNaN(fraction) ? 0 : Math.Clamp(fraction, 0, 1);
        _set(Min + (int)Math.Round(clamped * (Max - Min)));
    }

    /// <summary>How far along its track a slider's value is, 0 to 1.</summary>
    public double Fraction => Max == Min ? 0 : (Value - Min) / (double)(Max - Min);

    /// <summary>The text of the value: a slider's number with its suffix, a choice's or toggle's name in the locale; empty for a button, a heading or a note.</summary>
    /// <param name="ui">The locale's interface words by key.</param>
    public string ValueText(Func<string, string> ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        return Kind switch
        {
            MenuRowKind.Slider => Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + Suffix,
            MenuRowKind.Choice or MenuRowKind.Toggle => ui(ChoiceKeys[Math.Clamp(Value - Min, 0, ChoiceKeys.Count - 1)]),
            _ => string.Empty,
        };
    }
}
