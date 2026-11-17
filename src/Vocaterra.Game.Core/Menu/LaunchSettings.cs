using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Menu;

/// <summary>The settings the command line fixed for this run, each over the saved value (a flag wins for the run); <see langword="null"/> leaves the save's.</summary>
/// <param name="Scripted">Whether the run is a scripted screenshot run: its text stays at 100 percent unless a flag or a save says otherwise, so the recorded runs keep their pictures.</param>
/// <param name="SfxVolume"><c>--sfx-volume</c>, 0 to 1.</param>
/// <param name="AmbientVolume"><c>--ambient-volume</c>, 0 to 1.</param>
/// <param name="VoiceVolume"><c>--voice-volume</c>, 0 to 1.</param>
/// <param name="UiVolume"><c>--ui-volume</c>, 0 to 1.</param>
/// <param name="WeatherVolume"><c>--weather-volume</c>, 0 to 1.</param>
/// <param name="TextPercent"><c>--text-scale</c>: 100, 125 or 150.</param>
/// <param name="Quality"><c>--quality</c>.</param>
/// <param name="Blur"><c>--underwater-blur</c>.</param>
/// <param name="ViewEffects"><c>--view-effects</c>.</param>
/// <param name="NeedsRate"><c>--needs-rate</c>, 0.5 to 4.</param>
/// <param name="RelaxedNeeds">Whether <c>--relaxed-needs</c> was given (it only switches the setting on).</param>
/// <param name="ViewDistance"><c>--view-distance</c> in blocks (kept to the menu's steps of 16), or <see langword="null"/> for the save's, else Auto.</param>
/// <param name="FieldOfView"><c>--fov</c> in degrees, 60 to 110 in steps of 5.</param>
/// <param name="MouseSensitivity"><c>--mouse-sensitivity</c> in percent, 25 to 300 in steps of 5.</param>
/// <param name="InvertY"><c>--invert-y</c>.</param>
/// <param name="Grass"><c>--grass</c> in percent, 0 to 100 in steps of 10 (B18), or <see langword="null"/> for the save's.</param>
/// <param name="Shaders"><c>--gl-shaders on|off</c> (B17b): the game's own GLSL shaders for the chunks, or <see langword="null"/> for the save's (on for a new game).</param>
/// <param name="PassOpen">Whether <c>--pass open</c> was given (it only switches the pass cheat on).</param>
public sealed record LaunchSettings(
    bool Scripted = false,
    double? SfxVolume = null,
    double? AmbientVolume = null,
    int? TextPercent = null,
    QualityLevel? Quality = null,
    UnderwaterBlur? Blur = null,
    bool? ViewEffects = null,
    double? NeedsRate = null,
    bool RelaxedNeeds = false,
    int? ViewDistance = null,
    int? FieldOfView = null,
    int? MouseSensitivity = null,
    bool? InvertY = null,
    double? VoiceVolume = null,
    double? UiVolume = null,
    double? WeatherVolume = null,
    bool PassOpen = false,
    int? Grass = null,
    bool? Shaders = null);
