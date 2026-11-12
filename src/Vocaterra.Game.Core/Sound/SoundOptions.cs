namespace Vocaterra.Game.Core.Sound;

/// <summary>The part of the command line the sound side's start-up decisions read (<see cref="SoundSetup"/>).</summary>
/// <param name="IsScreenshotRun">Whether the run is scripted (<c>--screenshot</c>).</param>
/// <param name="NoSounds">Whether <c>--no-sounds</c> was given.</param>
/// <param name="SfxVolume">The <c>--sfx-volume</c>, or <see langword="null"/>.</param>
/// <param name="AmbientVolume">The <c>--ambient-volume</c>, or <see langword="null"/>.</param>
/// <param name="VoiceVolume">The <c>--voice-volume</c>, or <see langword="null"/>.</param>
/// <param name="UiVolume">The <c>--ui-volume</c>, or <see langword="null"/>.</param>
/// <param name="WeatherVolume">The <c>--weather-volume</c>, or <see langword="null"/>.</param>
public sealed record SoundOptions(bool IsScreenshotRun, bool NoSounds, double? SfxVolume, double? AmbientVolume, double? VoiceVolume = null, double? UiVolume = null, double? WeatherVolume = null);
