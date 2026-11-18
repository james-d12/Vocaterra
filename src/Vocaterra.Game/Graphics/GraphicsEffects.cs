using Vocaterra.Game.Core.Menu;

namespace Vocaterra.Game.Graphics;

/// <summary>
/// The graphics effects the settings page's Graphics group switches (B17c, D-game-34 item 43), each on by default: the sun following the hour (B22's sun per band and deeper dusk), the climate colours (B22's land tint), the water shader (the depth tint of B22 with the
/// surface's movement and the edge at grazing angles), the fog by height, and the far view's light. With one off the picture is the game's without it: the afternoon sun at every hour and the older dusk, the colour map's middle everywhere, plain water, the
/// distance fog alone, the far view drawn in the day's baked light. Plain values, made from the player's settings (<see cref="Of"/>) or a quality level (<see cref="Of(QualityPreset)"/>).
/// </summary>
/// <param name="SunFollowsHour">The faces are shaded by a sun that follows the hour, with the deeper dusk.</param>
/// <param name="ClimateColours">The grass and the oak leaves take the colour of their climate.</param>
/// <param name="WaterShader">The water's depth tint, the movement of its surface and the edge at grazing angles.</param>
/// <param name="HeightFog">The haze lies thicker in the low land.</param>
/// <param name="FarLight">The far view is drawn through the chunks' program, in the same sun, fog and haze.</param>
public sealed record GraphicsEffects(bool SunFollowsHour = true, bool ClimateColours = true, bool WaterShader = true, bool HeightFog = true, bool FarLight = true)
{
    /// <summary>Every effect on: the look of a new game at the high level.</summary>
    public static GraphicsEffects All { get; } = new();

    /// <summary>Every effect off.</summary>
    public static GraphicsEffects None { get; } = new(false, false, false, false, false);

    /// <summary>The effects the player's settings say.</summary>
    /// <param name="settings">The settings.</param>
    public static GraphicsEffects Of(PlayerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new GraphicsEffects(settings.SunFollowsHour, settings.ClimateColours, settings.WaterShader, settings.HeightFog, settings.FarLight);
    }

    /// <summary>The effects a quality level turns on.</summary>
    /// <param name="preset">The level's preset.</param>
    public static GraphicsEffects Of(QualityPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return new GraphicsEffects(preset.SunFollowsHour, preset.ClimateColours, preset.WaterShader, preset.HeightFog, preset.FarLight);
    }

    /// <summary>Sets the five effect rows of the settings from this one (what a change of quality level does: each row can be changed alone after it).</summary>
    /// <param name="settings">The settings.</param>
    public void WriteTo(PlayerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Set(SettingId.SunFollowsHour, SunFollowsHour ? 1 : 0);
        settings.Set(SettingId.ClimateColours, ClimateColours ? 1 : 0);
        settings.Set(SettingId.WaterShader, WaterShader ? 1 : 0);
        settings.Set(SettingId.HeightFog, HeightFog ? 1 : 0);
        settings.Set(SettingId.FarLight, FarLight ? 1 : 0);
    }
}
