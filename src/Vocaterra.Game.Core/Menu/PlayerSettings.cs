using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Menu;

/// <summary>
/// The player's settings as the game holds them while it runs (the menu issue): the four volumes, the graphics quality, the text size, the underwater blur, the needs' view effects,
/// the mouse sensitivity and Invert Y, the field of view, the draw distance, the drive rate and the relaxed needs. The menu changes them through <see cref="Set"/>, which limits a value to its range and
/// its steps (<see cref="Spec"/>), and <see cref="Changed"/> tells the front-end to apply the one that moved (the mixer, the canvas, the camera). They are kept in the save's settings
/// (<see cref="ToSave"/>, <see cref="FromSave"/>); a launch option wins over the save for the run (<see cref="LaunchSettings"/>). Nothing here reaches the simulation but the drive rate
/// and the relaxed needs, which the session already has as its settings (N05, N09).
/// </summary>
public sealed class PlayerSettings
{
    /// <summary>The grass density of a new game, in percent (B18): a plain of grass shows a tuft on six columns in ten, which reads as a lush meadow without hiding the ground or the paths.</summary>
    public const int DefaultGrass = 60;

    private static readonly string[] OnOff = ["settingsOff", "settingsOn"];

    private static readonly IReadOnlyList<SettingSpec> Specs =
    [
        new(SettingId.AmbientVolume, SettingKind.Slider, 0, 100, 5, 60, "settingsAmbient", "%"),
        new(SettingId.WeatherVolume, SettingKind.Slider, 0, 100, 5, 60, "settingsWeather", "%"),
        new(SettingId.VoiceVolume, SettingKind.Slider, 0, 100, 5, 100, "settingsVoice", "%"),
        new(SettingId.SfxVolume, SettingKind.Slider, 0, 100, 5, 80, "settingsSfx", "%"),
        new(SettingId.UiVolume, SettingKind.Slider, 0, 100, 5, 80, "settingsUiSounds", "%"),
        new(SettingId.Quality, SettingKind.Choice, 0, 2, 1, (int)QualityLevel.High, "settingsQuality", ChoiceKeys: ["qualityLow", "qualityMedium", "qualityHigh"]),
        new(SettingId.ViewDistance, SettingKind.Slider, 32, 576, 16, 32, "settingsViewDistance", MinKey: "settingsAuto"),
        new(SettingId.TextSize, SettingKind.Choice, 0, 2, 1, 2, "settingsTextSize", ChoiceKeys: ["textSize100", "textSize125", "textSize150"]),
        new(SettingId.Blur, SettingKind.Choice, 0, 3, 1, (int)UnderwaterBlurSettings.Default, "settingsBlur", ChoiceKeys: ["settingsOff", "blurLow", "blurMedium", "blurHigh"]),
        new(SettingId.ViewEffects, SettingKind.Toggle, 0, 1, 1, 1, "settingsEffects", ChoiceKeys: OnOff),
        new(SettingId.MouseSensitivity, SettingKind.Slider, 25, 300, 5, 100, "settingsSensitivity", "%"),
        new(SettingId.InvertY, SettingKind.Toggle, 0, 1, 1, 0, "settingsInvertY", ChoiceKeys: OnOff),
        new(SettingId.FieldOfView, SettingKind.Slider, 60, 110, 5, 70, "settingsFov", "°"),
        new(SettingId.NeedsRate, SettingKind.Slider, 50, 400, 25, 100, "settingsRate", "%"),
        new(SettingId.RelaxedNeeds, SettingKind.Toggle, 0, 1, 1, 0, "settingsRelaxed", ChoiceKeys: OnOff),
        new(SettingId.PassCheat, SettingKind.Toggle, 0, 1, 1, 0, "settingsPass", ChoiceKeys: OnOff),
        new(SettingId.Grass, SettingKind.Slider, 0, 100, 10, DefaultGrass, "settingsGrass", "%"),
    ];

    private readonly int[] _values;

    /// <summary>Creates the settings of a new game (every value its default, the text at the largest size).</summary>
    public PlayerSettings()
    {
        _values = Specs.OrderBy(static spec => (int)spec.Id).Select(static spec => spec.Default).ToArray();
    }

    /// <summary>Raised after a setting changed to another value, with the setting.</summary>
    public event Action<SettingId>? Changed;

    /// <summary>The text sizes in percent, by the <see cref="SettingId.TextSize"/> index.</summary>
    public static IReadOnlyList<int> TextPercents => SaveSettings.TextScaleLevels;

    /// <summary>Every setting's spec, in the order the settings are listed.</summary>
    public static IReadOnlyList<SettingSpec> All => Specs;

    /// <summary>The spec of a setting.</summary>
    /// <param name="id">The setting.</param>
    public static SettingSpec Spec(SettingId id) => Specs.First(spec => spec.Id == id);

    /// <summary>The ambient beds' volume, 0 to 1.</summary>
    public double AmbientVolume => Get(SettingId.AmbientVolume) / 100.0;

    /// <summary>The weather's own sounds' volume (the rain), 0 to 1.</summary>
    public double WeatherVolume => Get(SettingId.WeatherVolume) / 100.0;

    /// <summary>The volumes of the sound categories.</summary>
    public SoundVolumes Volumes => new(
        Get(SettingId.VoiceVolume) / 100.0,
        Get(SettingId.SfxVolume) / 100.0,
        Get(SettingId.UiVolume) / 100.0,
        Get(SettingId.AmbientVolume) / 100.0,
        Get(SettingId.WeatherVolume) / 100.0);

    /// <summary>The graphics quality.</summary>
    public QualityLevel Quality => (QualityLevel)Get(SettingId.Quality);

    /// <summary>How far the world is drawn in blocks, or <see langword="null"/> for Auto (the graphics quality's own distance).</summary>
    public int? ViewDistance => Get(SettingId.ViewDistance) <= Spec(SettingId.ViewDistance).Min ? null : Get(SettingId.ViewDistance);

    /// <summary>The interface text size in percent.</summary>
    public int TextPercent => TextPercents[Get(SettingId.TextSize)];

    /// <summary>The underwater blur level the player chose.</summary>
    public UnderwaterBlur Blur => (UnderwaterBlur)Get(SettingId.Blur);

    /// <summary>Whether the needs' effects on the view are on.</summary>
    public bool ViewEffects => Get(SettingId.ViewEffects) == 1;

    /// <summary>How much one pixel of mouse movement turns the view, as a multiplier of the normal turn (1 is the normal turn).</summary>
    public double MouseSensitivity => Get(SettingId.MouseSensitivity) / 100.0;

    /// <summary>Whether moving the mouse up looks down.</summary>
    public bool InvertY => Get(SettingId.InvertY) == 1;

    /// <summary>The vertical field of view in degrees.</summary>
    public double FieldOfView => Get(SettingId.FieldOfView);

    /// <summary>The drive rate multiplier (1 is the normal rate), 0.5 to 4 in steps of 0.25.</summary>
    public double NeedsRate => Get(SettingId.NeedsRate) / 100.0;

    /// <summary>Whether the needs are relaxed.</summary>
    public bool RelaxedNeeds => Get(SettingId.RelaxedNeeds) == 1;

    /// <summary>Whether the pass cheat is on: the pass through the east ring is open at once (B08b).</summary>
    public bool PassCheat => Get(SettingId.PassCheat) == 1;

    /// <summary>The decorative grass's density, 0 (none) to 1 (every grass column in the lushest biome).</summary>
    public double Grass => Get(SettingId.Grass) / 100.0;

    /// <summary>The value of a setting, as the menu counts it (see <see cref="SettingId"/>).</summary>
    /// <param name="id">The setting.</param>
    public int Get(SettingId id) => _values[(int)id];

    /// <summary>Sets a setting to the value nearest to <paramref name="value"/> in its range and steps, and tells <see cref="Changed"/> when it is another value than before.</summary>
    /// <param name="id">The setting.</param>
    /// <param name="value">The value asked for.</param>
    public void Set(SettingId id, int value)
    {
        var snapped = Spec(id).Snap(value);
        if (_values[(int)id] == snapped)
        {
            return;
        }

        _values[(int)id] = snapped;
        Changed?.Invoke(id);
    }

    /// <summary>The settings a run starts with: the save's (or the defaults of a new game), then the command line's over them. A scripted run without a saved text size keeps 100 percent.</summary>
    /// <param name="saved">The settings a loaded save holds, or <see langword="null"/> for a new game.</param>
    /// <param name="launch">What the command line fixed.</param>
    public static PlayerSettings Resolve(SaveSettings? saved, LaunchSettings? launch = null)
    {
        launch ??= new LaunchSettings();
        var settings = new PlayerSettings();
        if (launch.Scripted)
        {
            settings._values[(int)SettingId.TextSize] = 0;
        }

        if (saved is not null)
        {
            var clean = saved.Clamped();
            settings._values[(int)SettingId.AmbientVolume] = Spec(SettingId.AmbientVolume).Snap((int)Math.Round(clean.Sound.Ambient * 100));
            settings._values[(int)SettingId.WeatherVolume] = Spec(SettingId.WeatherVolume).Snap((int)Math.Round(clean.Sound.Weather * 100));
            settings._values[(int)SettingId.VoiceVolume] = Spec(SettingId.VoiceVolume).Snap((int)Math.Round(clean.Sound.Voice * 100));
            settings._values[(int)SettingId.SfxVolume] = Spec(SettingId.SfxVolume).Snap((int)Math.Round(clean.Sound.Sfx * 100));
            settings._values[(int)SettingId.UiVolume] = Spec(SettingId.UiVolume).Snap((int)Math.Round(clean.Sound.Ui * 100));
            settings._values[(int)SettingId.Quality] = (int)clean.EffectiveQuality;
            settings._values[(int)SettingId.ViewDistance] = clean.EffectiveViewDistance == 0 ? Spec(SettingId.ViewDistance).Min : clean.EffectiveViewDistance;
            settings._values[(int)SettingId.TextSize] = TextPercents.ToList().IndexOf(clean.TextScale);
            settings._values[(int)SettingId.Blur] = (int)clean.EffectiveUnderwaterBlur;
            settings._values[(int)SettingId.ViewEffects] = clean.ViewEffectsOff ? 0 : 1;
            settings._values[(int)SettingId.MouseSensitivity] = clean.EffectiveMouseSensitivity;
            settings._values[(int)SettingId.InvertY] = clean.InvertY ? 1 : 0;
            settings._values[(int)SettingId.FieldOfView] = clean.EffectiveFieldOfView;
            settings._values[(int)SettingId.NeedsRate] = (int)Math.Round(clean.EffectiveNeedsRate * 100);
            settings._values[(int)SettingId.RelaxedNeeds] = clean.RelaxedNeeds ? 1 : 0;
            settings._values[(int)SettingId.PassCheat] = clean.PassCheat ? 1 : 0;
            settings._values[(int)SettingId.Grass] = clean.EffectiveGrass;
        }

        if (launch.SfxVolume is { } sfx)
        {
            settings._values[(int)SettingId.SfxVolume] = Spec(SettingId.SfxVolume).Snap(PercentOf(sfx));
        }

        if (launch.AmbientVolume is { } ambient)
        {
            settings._values[(int)SettingId.AmbientVolume] = Spec(SettingId.AmbientVolume).Snap(PercentOf(ambient));
        }

        if (launch.WeatherVolume is { } weather)
        {
            settings._values[(int)SettingId.WeatherVolume] = Spec(SettingId.WeatherVolume).Snap(PercentOf(weather));
        }

        if (launch.VoiceVolume is { } voice)
        {
            settings._values[(int)SettingId.VoiceVolume] = Spec(SettingId.VoiceVolume).Snap(PercentOf(voice));
        }

        if (launch.UiVolume is { } ui)
        {
            settings._values[(int)SettingId.UiVolume] = Spec(SettingId.UiVolume).Snap(PercentOf(ui));
        }

        if (launch.TextPercent is { } text)
        {
            settings._values[(int)SettingId.TextSize] = TextPercents.ToList().IndexOf(SaveSettings.SnapTextScale(text));
        }

        if (launch.Quality is { } quality)
        {
            settings._values[(int)SettingId.Quality] = (int)quality;
        }

        if (launch.ViewDistance is { } distance)
        {
            settings._values[(int)SettingId.ViewDistance] = Spec(SettingId.ViewDistance).Snap(distance);
        }

        if (launch.FieldOfView is { } fov)
        {
            settings._values[(int)SettingId.FieldOfView] = Spec(SettingId.FieldOfView).Snap(fov);
        }

        if (launch.MouseSensitivity is { } sensitivity)
        {
            settings._values[(int)SettingId.MouseSensitivity] = Spec(SettingId.MouseSensitivity).Snap(sensitivity);
        }

        if (launch.InvertY is { } invert)
        {
            settings._values[(int)SettingId.InvertY] = invert ? 1 : 0;
        }

        if (launch.Blur is { } blur && Enum.IsDefined(blur))
        {
            settings._values[(int)SettingId.Blur] = (int)blur;
        }

        if (launch.ViewEffects is { } effects)
        {
            settings._values[(int)SettingId.ViewEffects] = effects ? 1 : 0;
        }

        if (launch.NeedsRate is { } rate)
        {
            settings._values[(int)SettingId.NeedsRate] = Spec(SettingId.NeedsRate).Snap((int)Math.Round(SaveSettings.SnapNeedsRate(rate) * 100));
        }

        if (launch.RelaxedNeeds)
        {
            settings._values[(int)SettingId.RelaxedNeeds] = 1;
        }

        if (launch.Grass is { } grass)
        {
            settings._values[(int)SettingId.Grass] = Spec(SettingId.Grass).Snap(grass);
        }

        if (launch.PassOpen)
        {
            settings._values[(int)SettingId.PassCheat] = 1;
        }

        return settings;
    }

    /// <summary>The settings as the save keeps them.</summary>
    public SaveSettings ToSave() => new SaveSettings(
        Volumes,
        TextPercent,
        ViewEffectsOff: !ViewEffects,
        RelaxedNeeds: RelaxedNeeds,
        NeedsRate: NeedsRate,
        UnderwaterBlur: Blur,
        Quality: Quality,
        MouseSensitivity: Get(SettingId.MouseSensitivity),
        InvertY: InvertY,
        FieldOfView: Get(SettingId.FieldOfView),
        ViewDistance: ViewDistance ?? 0,
        PassCheat: PassCheat,
        Grass: Get(SettingId.Grass)).Clamped();

    /// <summary>The drive rate as the menu and the console write it (<c>×1.25</c>).</summary>
    /// <param name="rate">The rate.</param>
    public static string RateText(double rate) => "×" + rate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static int PercentOf(double volume) => (int)Math.Round(Math.Clamp(double.IsNaN(volume) ? 0 : volume, 0, 1) * 100);
}
