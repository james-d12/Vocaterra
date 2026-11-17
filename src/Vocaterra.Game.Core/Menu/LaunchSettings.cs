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
/// <param name="Workers"><c>--workers</c> (B07d): 0 for auto, or 1 to 8 worker threads; <see langword="null"/> for the save's.</param>
/// <param name="FarDistance"><c>--far-distance</c> in km (B07d; 0 for the quality's own), or <see langword="null"/> for the save's.</param>
/// <param name="FarDetail"><c>--far-detail</c> in percent, 50 to 200 in steps of 25 (B07d), or <see langword="null"/> for the save's.</param>
/// <param name="FarTrees"><c>--far-trees</c> as the setting counts it (0 auto, 1 none, 2 to 6 the distances; B07d), or <see langword="null"/> for the save's.</param>
/// <param name="FarTextures"><c>--far-textures</c> 0 to 3 (B07d), or <see langword="null"/> for the save's.</param>
/// <param name="FarExact"><c>--far-exact</c> as the setting counts it (0 auto, 1 none, 2 to 5 the distances; B07d), or <see langword="null"/> for the save's.</param>
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
    int? Workers = null,
    int? FarDistance = null,
    int? FarDetail = null,
    int? FarTrees = null,
    int? FarTextures = null,
    int? FarExact = null);
