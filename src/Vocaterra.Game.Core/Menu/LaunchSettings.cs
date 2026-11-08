using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Menu;

/// <summary>The settings the command line fixed for this run, each over the saved value (a flag wins for the run); <see langword="null"/> leaves the save's.</summary>
/// <param name="Scripted">Whether the run is a scripted screenshot run: its text stays at 100 percent unless a flag or a save says otherwise, so the recorded runs keep their pictures.</param>
/// <param name="SfxVolume"><c>--sfx-volume</c>, 0 to 1.</param>
/// <param name="AmbientVolume"><c>--ambient-volume</c>, 0 to 1.</param>
/// <param name="TextPercent"><c>--text-scale</c>: 100, 125 or 150.</param>
/// <param name="Quality"><c>--quality</c>.</param>
/// <param name="Blur"><c>--underwater-blur</c>.</param>
/// <param name="ViewEffects"><c>--view-effects</c>.</param>
/// <param name="NeedsRate"><c>--needs-rate</c>, 0.5 to 4.</param>
/// <param name="RelaxedNeeds">Whether <c>--relaxed-needs</c> was given (it only switches the setting on).</param>
public sealed record LaunchSettings(
    bool Scripted = false,
    double? SfxVolume = null,
    double? AmbientVolume = null,
    int? TextPercent = null,
    QualityLevel? Quality = null,
    UnderwaterBlur? Blur = null,
    bool? ViewEffects = null,
    double? NeedsRate = null,
    bool RelaxedNeeds = false);
