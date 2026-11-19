using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Temperature;

namespace Vocaterra.Game.Core.Needs;

/// <summary>A drive's icon on the HUD (N05): a loaf, a drop or a moon with one to four notches, one for each stage.</summary>
/// <param name="Drive">Which drive.</param>
/// <param name="Notches">The stage, 1 to 4: how many notches the icon shows.</param>
public sealed record DriveIcon(Drive Drive, int Notches);

/// <summary>A stage change on show (N05): the player's own line in Spanish with the English beneath, above the hotbar. The cold's line (I06) and the line of a tool that wore out (I07) go the same way.</summary>
/// <param name="Meter">The need that changed, or <see langword="null"/> for the line of the cold (I06), which is no need.</param>
/// <param name="Stage">Its new stage (1 to 4 for a drive, 1 or 2 for health; 0 for the cold's line).</param>
/// <param name="Spanish">The line in Spanish (<c>Tengo mucha sed.</c>).</param>
/// <param name="English">The line in the support language (<c>I'm very thirsty.</c>).</param>
/// <param name="StartTick">The tick it came on screen.</param>
public sealed record StageAnnouncement(NeedMeter? Meter, int Stage, string Spanish, string English, long StartTick);

/// <summary>The cold's icon on the HUD (I06): a snowflake with one notch for each degree of chill the band has.</summary>
/// <param name="Notches">The band's chill, 1 and up.</param>
/// <param name="OutOf">The most chill any band has, so the notches can be drawn out of it.</param>
public sealed record ColdIcon(int Notches, int OutOf);

/// <summary>One row of the inventory's list of the five needs (N05).</summary>
/// <param name="Meter">Which need.</param>
/// <param name="Name">The need's name in Spanish (<c>sed</c>).</param>
/// <param name="NameEnglish">The need's name in the support language (<c>thirst</c>).</param>
/// <param name="Stage">The stage: 0 healthy, otherwise as <see cref="StageAnnouncement.Stage"/>; always 0 for stamina, which has none.</param>
/// <param name="Spanish">The stage's line in Spanish, or <c>bien</c> when the need is healthy.</param>
/// <param name="English">The stage's line in the support language, or <c>fine</c>.</param>
public sealed record NeedsListRow(NeedMeter Meter, string Name, string NameEnglish, int Stage, string Spanish, string English);

/// <summary>
/// How strongly each effect on the view is drawn at an instant, 0 (not at all) to <see cref="MaxAlpha"/> (N05). All are gentle: they ease in and out over seconds, none is
/// brighter than the world, and none changes the whole screen faster than <see cref="MaxChangePerSecond"/>.
/// </summary>
/// <param name="DimEdges">Dark edges (thirst at stage 3 and above).</param>
/// <param name="Blink">A slow dimming of the whole view, now and then (sleep at stage 3 and above).</param>
/// <param name="RedEdges">Red edges (health below 25).</param>
public readonly record struct ViewEffectLevels(double DimEdges, double Blink, double RedEdges)
{
    /// <summary>The strongest any effect gets: never a full-brightness or fully black frame.</summary>
    public const double MaxAlpha = 0.6;

    /// <summary>The most an effect's level may change in a second: a full swing takes about half a second at the least, so nothing flashes (under 3 Hz).</summary>
    public const double MaxChangePerSecond = 1.2;
}

/// <summary>
/// What the HUD shows of the player's needs (N05, adaptation design 9.9): plain values read from the needs and the session, no drawing and no clock (the tick comes from
/// the game). The health bar is always shown, stamina's only while it is below its maximum (with a dashed end for what the drives cut), a drive's icon from stage 1 with a
/// notch for each stage, and a stage that gets worse shows its line once and asks for its sound once. With the needs off (<c>--needs off</c>) nothing is shown.
/// </summary>
/// <remarks>
/// A worse stage announces itself; a recovery only takes the icon (and the effect) away. The first look at the needs announces a stage found already below healthy once, so a
/// <c>--set-need</c> or a load is seen too. Several changes in a frame are shown one after the other, <see cref="DisplayTicks"/> each.
/// </remarks>
public sealed class NeedsHud
{
    /// <summary>How long a stage line stays on screen, in world ticks (4 seconds).</summary>
    public const int DisplayTicks = 4 * NeedsData.TicksPerSecond;

    /// <summary>The seconds between two blinks at sleep stage 3 and at stage 4.</summary>
    private static readonly double[] BlinkPeriods = [0, 0, 0, 14, 9];

    /// <summary>The seconds the eyelid takes to close, and to open.</summary>
    private const double BlinkSeconds = 0.9;

    private static readonly NeedMeter[] Told = [NeedMeter.Hunger, NeedMeter.Thirst, NeedMeter.Sleep, NeedMeter.Health];

    private readonly VoxelSession _session;
    private readonly int[] _last = new int[4];
    private readonly Queue<(NeedMeter? Meter, int Stage, string Key)> _waiting = new();
    private StageAnnouncement? _current;
    private int _sounds;

    /// <summary>A HUD for a session.</summary>
    /// <param name="session">The session whose needs are shown.</param>
    public NeedsHud(VoxelSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <summary>Whether the effects on the view are on (the option, F7; <c>--view-effects off</c>). Off leaves the icons and the lines.</summary>
    public bool EffectsOn { get; set; } = true;

    /// <summary>Whether the needs HUD is drawn at all: the needs run (<see cref="VoxelSession.NeedsEnabled"/>).</summary>
    public bool Visible => _session.NeedsEnabled;

    /// <summary>Health, 0 to 1 (the bar is always shown while <see cref="Visible"/>).</summary>
    public double Health => _session.Needs.Value(NeedMeter.Health) / PlayerNeeds.Full;

    /// <summary>Health's warning stage: 0 healthy, 1 below 50 (the bar pulses), 2 below 25 (the edges redden).</summary>
    public int HealthStage => _session.Needs.HealthStage;

    /// <summary>Stamina is shown only while it is below its current maximum.</summary>
    public bool StaminaVisible => _session.Needs.Value(NeedMeter.Stamina) < _session.Needs.Effects().StaminaMaximum - 1e-6;

    /// <summary>Stamina, 0 to 1 of the full bar (100).</summary>
    public double Stamina => _session.Needs.Value(NeedMeter.Stamina) / StaminaScale;

    /// <summary>Stamina's current maximum, 0 to 1 of the full bar: the bar from here to its end is dashed, for what the drives cut.</summary>
    public double StaminaMaximum => _session.Needs.Effects().StaminaMaximum / StaminaScale;

    // The bar's full length in meter units: 100, or the ceiling when gear raises it above 100 (I05a), so the bar always shows the whole ceiling.
    private double StaminaScale => Math.Max(PlayerNeeds.Full, _session.Needs.Effects().StaminaMaximum);

    /// <summary>The icons of the drives that are at stage 1 or worse, in the order hunger, thirst, sleep; none when all are healthy.</summary>
    public IReadOnlyList<DriveIcon> Icons
    {
        get
        {
            var icons = new List<DriveIcon>();
            foreach (var drive in new[] { Drive.Hunger, Drive.Thirst, Drive.Sleep })
            {
                var stage = _session.Needs.StageOf(drive);
                if (stage >= 1)
                {
                    icons.Add(new DriveIcon(drive, stage));
                }
            }

            return icons;
        }
    }

    /// <summary>
    /// The cold's icon (I06) while the feeling is in a band with a chill (cold or very cold, by the data), or <see langword="null"/>: a snowflake after the drive icons, with a notch
    /// for each degree of chill. Never shown with the needs off.
    /// </summary>
    public ColdIcon? ColdIcon => _session.NeedsEnabled && _session.Temperature.Current is { Band.Chill: > 0 } reading
        ? new ColdIcon(reading.Band.Chill, _session.Temperature.Data.MaxChill)
        : null;

    /// <summary>How the temperature stands at the last tick (I06), for the debug overlay; <see langword="null"/> before the first tick. Shown whether or not the needs run.</summary>
    public TemperatureReading? Temperature => _session.Temperature.Current;

    /// <summary>The stage line on show now, or <see langword="null"/>.</summary>
    public StageAnnouncement? Announcement => _current;

    /// <summary>Takes the number of sounds asked for since the last call (one for each stage line that came on screen), and clears it.</summary>
    public int TakeSounds()
    {
        var count = _sounds;
        _sounds = 0;
        return count;
    }

    /// <summary>Reads the needs for a tick: queues the lines of the stages that got worse, and moves the line on show along.</summary>
    /// <param name="tick">The world tick now.</param>
    public void Update(long tick)
    {
        for (var i = 0; i < Told.Length; i++)
        {
            var now = StageOf(Told[i]);
            if (_session.NeedsEnabled && now > _last[i] && now >= 1)
            {
                _waiting.Enqueue((Told[i], now, LineKey(Told[i], now)));
            }

            _last[i] = now;
        }

        if (_session.Temperature.TakeLine() is { } coldLine && _session.NeedsEnabled)
        {
            _waiting.Enqueue((null, 0, coldLine)); // I06: «Tengo frío», said once on the way into the cold, by the path the needs' lines take
        }

        while (_session.TakeWearLine() is { } wearLine)
        {
            if (_session.NeedsEnabled)
            {
                _waiting.Enqueue((null, 0, wearLine)); // I07: «Necesito arreglar la herramienta», said once when a tool wears out, by the path the cold's line takes
            }
        }

        if (_current is { } shown && tick - shown.StartTick >= DisplayTicks)
        {
            _current = null;
        }

        if (_current is null && _waiting.Count > 0)
        {
            var (meter, stage, key) = _waiting.Dequeue();
            _current = new StageAnnouncement(meter, stage, _session.Data.Words.HudLine(key) ?? key, _session.Locale.Text(key), tick);
            _sounds++;
        }
    }

    /// <summary>The effects on the view at a tick, each 0 to <see cref="ViewEffectLevels.MaxAlpha"/>; all 0 when the option is off or the needs are not shown.</summary>
    /// <param name="tick">The world tick.</param>
    public ViewEffectLevels Effects(long tick)
    {
        if (!EffectsOn || !Visible)
        {
            return default;
        }

        var needs = _session.Needs;
        var dim = needs.StageOf(Drive.Thirst) switch { 3 => 0.3, 4 => 0.45, _ => 0.0 };
        var red = needs.HealthStage == 2 ? RedLevel(tick) : 0.0;
        return new ViewEffectLevels(dim, BlinkLevel(needs.StageOf(Drive.Sleep), tick), red);
    }

    /// <summary>
    /// The eyelid at a tick: every <c>period</c> seconds the view dims to <see cref="ViewEffectLevels.MaxAlpha"/> and back, closing and opening in 0.9 s each (a raised cosine, so
    /// it eases at both ends). Sleep stages below 3 do not blink.
    /// </summary>
    /// <param name="sleepStage">The sleep drive's stage.</param>
    /// <param name="tick">The world tick.</param>
    public static double BlinkLevel(int sleepStage, long tick)
    {
        if (sleepStage < 3)
        {
            return 0;
        }

        var period = BlinkPeriods[Math.Min(sleepStage, 4)];
        var t = (tick / (double)NeedsData.TicksPerSecond) % period;
        var phase = t / BlinkSeconds;
        if (phase >= 2)
        {
            return 0;
        }

        var shape = phase < 1 ? phase : 2 - phase; // 0 to 1 and back
        return ViewEffectLevels.MaxAlpha * 0.5 * (1 - Math.Cos(Math.PI * shape));
    }

    // The red edges breathe a little (a slow swell, about one every four seconds) around 0.35, so they are alive and never flash.
    private static double RedLevel(long tick) => 0.35 + (0.1 * Math.Sin(2 * Math.PI * tick / (4.0 * NeedsData.TicksPerSecond)));

    /// <summary>The health bar's heart pulse at a tick, 0 to 1 (at health stage 1 and above the bar swells about once a second); 0 when healthy.</summary>
    /// <param name="tick">The world tick.</param>
    public double HeartPulse(long tick) =>
        HealthStage >= 1 ? 0.5 * (1 + Math.Sin(2 * Math.PI * tick / (double)NeedsData.TicksPerSecond)) : 0;

    /// <summary>The five needs for the inventory's list, each with its stage line in Spanish and in English (or <c>bien</c> and <c>fine</c> when healthy).</summary>
    public IReadOnlyList<NeedsListRow> ListRows()
    {
        var rows = new List<NeedsListRow>();
        foreach (var meter in new[] { NeedMeter.Hunger, NeedMeter.Thirst, NeedMeter.Sleep, NeedMeter.Health, NeedMeter.Stamina })
        {
            var stage = meter == NeedMeter.Stamina ? 0 : StageOf(meter);
            var key = NameKey(meter);
            rows.Add(new NeedsListRow(
                meter,
                _session.Data.Words.HudLine(key) ?? key,
                _session.Locale.Text(key),
                stage,
                stage == 0 ? _session.Data.Words.HudLine("hud.fine") ?? "hud.fine" : SpanishLine(meter, stage),
                stage == 0 ? _session.Locale.Text("hud.fine") : _session.Locale.Text(LineKey(meter, stage))));
        }

        return rows;
    }

    private int StageOf(NeedMeter meter) => meter switch
    {
        NeedMeter.Hunger => _session.Needs.StageOf(Drive.Hunger),
        NeedMeter.Thirst => _session.Needs.StageOf(Drive.Thirst),
        NeedMeter.Sleep => _session.Needs.StageOf(Drive.Sleep),
        NeedMeter.Health => _session.Needs.HealthStage,
        _ => 0,
    };

    // The key of a stage's line: the data's own (game/needs.json) for the meter's stage.
    private string LineKey(NeedMeter meter, int stage) => meter switch
    {
        NeedMeter.Hunger => _session.Data.Needs.Of(Drive.Hunger).Stages[stage - 1].Line,
        NeedMeter.Thirst => _session.Data.Needs.Of(Drive.Thirst).Stages[stage - 1].Line,
        NeedMeter.Sleep => _session.Data.Needs.Of(Drive.Sleep).Stages[stage - 1].Line,
        _ => _session.Data.Needs.Health.Warnings[stage - 1].Line,
    };

    private string SpanishLine(NeedMeter meter, int stage)
    {
        var key = LineKey(meter, stage);
        return _session.Data.Words.HudLine(key) ?? key;
    }

    private static string NameKey(NeedMeter meter) => "hud.name." + meter switch
    {
        NeedMeter.Hunger => "hunger",
        NeedMeter.Thirst => "thirst",
        NeedMeter.Sleep => "sleep",
        NeedMeter.Health => "health",
        _ => "stamina",
    };
}
