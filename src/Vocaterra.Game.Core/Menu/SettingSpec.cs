namespace Vocaterra.Game.Core.Menu;

/// <summary>What a setting is: how the menu changes it, its range and the words that label it. The one table of ranges, steps and defaults (<see cref="PlayerSettings"/> clamps by it).</summary>
/// <param name="Id">The setting.</param>
/// <param name="Kind">How it is changed.</param>
/// <param name="Min">The lowest value.</param>
/// <param name="Max">The highest value.</param>
/// <param name="Step">The distance one press of Left or Right moves it.</param>
/// <param name="Default">The value of a new game.</param>
/// <param name="LabelKey">The locale's <c>ui.&lt;key&gt;</c> that names the row.</param>
/// <param name="Suffix">What follows a slider's number (<c>%</c>), or empty.</param>
/// <param name="ChoiceKeys">For a choice, the locale's <c>ui.&lt;key&gt;</c> of each value from <see cref="Min"/> up (a toggle uses on and off).</param>
/// <param name="MinKey">For a slider whose lowest value stands for "automatic" (the draw distance), the locale's <c>ui.&lt;key&gt;</c> shown in place of that number; otherwise <see langword="null"/>.</param>
public sealed record SettingSpec(SettingId Id, SettingKind Kind, int Min, int Max, int Step, int Default, string LabelKey, string Suffix = "", IReadOnlyList<string>? ChoiceKeys = null, string? MinKey = null)
{
    /// <summary>The value nearest to <paramref name="value"/> on this setting's steps, within its range.</summary>
    /// <param name="value">The value asked for.</param>
    public int Snap(int value)
    {
        var clamped = Math.Clamp(value, Min, Max);
        if (Kind != SettingKind.Slider)
        {
            return clamped;
        }

        var snapped = Min + ((int)Math.Round((clamped - Min) / (double)Step, MidpointRounding.AwayFromZero) * Step);
        return Math.Min(snapped, Max);
    }
}
