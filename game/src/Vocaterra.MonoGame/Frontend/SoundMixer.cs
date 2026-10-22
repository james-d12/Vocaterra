using System.Globalization;
using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>One sound the mixer lets through: the cue, its take (the file index, 0 for a synthesized cue), its final gain and its pitch change in octaves.</summary>
/// <param name="Cue">The cue.</param>
/// <param name="Category">The cue's category.</param>
/// <param name="Take">The index of the take in <see cref="SoundDefinition.Files"/>.</param>
/// <param name="Gain">The final gain, 0 to 1: the cue's own level, the category's volume and the duck.</param>
/// <param name="Pitch">The pitch change in octaves (the engine's -1 to 1 pitch), a small random value from the presentation generator.</param>
public readonly record struct SoundRequest(SoundCue Cue, SoundCategory Category, int Take, double Gain, double Pitch);

/// <summary>One input of the ambient cross-fade (S25): a bed and how strongly the player's place calls for it, 0 to 1.</summary>
/// <param name="Bed">The bed's id (S25 names them: <c>forest</c>, <c>river</c>, ...).</param>
/// <param name="Weight">The target weight, 0 to 1; a bed that is not named is faded out.</param>
public readonly record struct BedInput(string Bed, double Weight);

/// <summary>One bed's level as the output plays it: the cross-fade's level times the ambient volume, the duck and the microphone gate.</summary>
/// <param name="Bed">The bed's id.</param>
/// <param name="Level">The level, 0 to 1.</param>
public readonly record struct BedLevel(string Bed, double Level);

/// <summary>
/// Where the sounds go (S24). The MonoGame implementation plays them through the engine; <see cref="NullSoundOutput"/> plays nothing and needs no device, and the
/// scripted runs and the tests use it. The mixer decides what plays; an output only plays what it is given.
/// </summary>
public interface ISoundOutput : IDisposable
{
    /// <summary>Whether sounds can be played at all (false for the null output, and after the device failed).</summary>
    bool Available { get; }

    /// <summary>Plays a one-shot sound.</summary>
    /// <param name="request">The cue, its take, its gain and its pitch.</param>
    void Play(SoundRequest request);

    /// <summary>Silences every one-shot that plays now (the microphone starts recording).</summary>
    void StopAll();

    /// <summary>Sets the levels of the ambient beds (S25); a bed that is not listed is silent.</summary>
    /// <param name="levels">The beds and their levels.</param>
    void SetBeds(IReadOnlyList<BedLevel> levels);
}

/// <summary>An output that plays nothing and needs no audio device: the output of a scripted run and of every headless test.</summary>
public sealed class NullSoundOutput : ISoundOutput
{
    /// <inheritdoc />
    public bool Available => false;

    /// <inheritdoc />
    public void Play(SoundRequest request)
    {
    }

    /// <inheritdoc />
    public void StopAll()
    {
    }

    /// <inheritdoc />
    public void SetBeds(IReadOnlyList<BedLevel> levels)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>Where the sound log's lines go (<c>--sound-log</c>): a file in the game, a list in a test.</summary>
public interface ISoundLog
{
    /// <summary>Writes one line (without the line break).</summary>
    /// <param name="line">The line.</param>
    void Write(string line);
}

/// <summary>A sound log over a text writer.</summary>
public sealed class SoundLogWriter(TextWriter writer) : ISoundLog, IDisposable
{
    /// <inheritdoc />
    public void Write(string line) => writer.WriteLine(line);

    /// <inheritdoc />
    public void Dispose() => writer.Dispose();

    /// <summary>Opens the file for <c>--sound-log</c> (its folder is made, the file is replaced and every line is flushed as it is written).</summary>
    /// <param name="path">The file.</param>
    public static SoundLogWriter Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return new SoundLogWriter(new StreamWriter(full, append: false, new System.Text.UTF8Encoding(false)) { AutoFlush = true });
    }
}

/// <summary>What the push-to-talk tells the sound side: a recording is about to start, has started, has ended (S24, the microphone rule).</summary>
public interface ITalkSounds
{
    /// <summary>The recording is about to start: plays the start beep. Returns the seconds of the recording's start to discard, so the beep never reaches the recognizer.</summary>
    double Starting();

    /// <summary>The recording started: the other sounds go silent.</summary>
    void Started();

    /// <summary>The recording ended: the sounds may play again, and the stop beep plays.</summary>
    void Stopped();
}

/// <summary>
/// The mixer's policy (S24, D-game-18): which sounds play, how loud and with which variation. It is engine free: it decides, an <see cref="ISoundOutput"/> plays.
/// </summary>
/// <remarks>
/// <para>
/// <b>Categories.</b> Voice, sfx, ui and ambient each have a volume (<see cref="SoundVolumes"/>, kept in the save's settings). The NPCs' voices are played by
/// <c>NpcVoice</c>; the mixer is told when one is on (<see cref="SetVoice"/>).
/// </para>
/// <para>
/// <b>Ducking.</b> While a voice line plays, sfx and ambient dip to <see cref="DuckLevel"/> of their volume, eased in and out (<see cref="Advance"/>), so the ear stays on the
/// Spanish (D-game-13). The interface and progress sounds are short and are not ducked.
/// </para>
/// <para>
/// <b>The microphone rule.</b> From <see cref="Started"/> to <see cref="Stopped"/> sfx, ambient and ui are all silent: nothing is played and what plays is stopped.
/// The start beep plays just before the recording (<see cref="Starting"/>) and the stop beep just after it, so no game sound reaches the recognizer.
/// </para>
/// <para>
/// <b>Variation.</b> The take of a cue and its small pitch change come from a <see cref="PresentationRng"/> seeded from the world seed. A cue never plays the same take
/// twice in a row when it has others. Nothing here reaches the simulation, a turn, a state hash or the replay.
/// </para>
/// <para>
/// <b>Beds.</b> S25's ambient beds plug in through <see cref="SetBeds"/>: the position-weighted target weights, cross-faded over <see cref="BedFadeSeconds"/> with no hard cut.
/// </para>
/// </remarks>
public sealed class SoundMixer : ITalkSounds
{
    /// <summary>The level of sfx and ambient under a voice line, as a fraction of their volume.</summary>
    public const double DuckLevel = 0.35;

    /// <summary>How long the duck takes to close in, in seconds.</summary>
    public const double DuckAttackSeconds = 0.08;

    /// <summary>How long the sounds take to come back after the voice, in seconds.</summary>
    public const double DuckReleaseSeconds = 0.6;

    /// <summary>How long a bed takes to fade fully in or out, in seconds.</summary>
    public const double BedFadeSeconds = 1.5;

    private const double LeadInMargin = 0.05;

    private readonly ISoundOutput _output;
    private readonly PresentationRng _rng;
    private readonly ISoundLog? _log;
    private readonly Dictionary<SoundCue, int> _lastTake = [];
    private readonly Dictionary<SoundCue, int> _played = [];
    private readonly Dictionary<string, int> _dropped = [];
    private readonly Dictionary<string, double> _targets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _levels = new(StringComparer.Ordinal);
    private long _tick;
    private bool _bedsPushed;
    private bool _voiceActive;
    private double _duck = 1.0;

    /// <summary>Creates the mixer.</summary>
    /// <param name="output">Where the sounds go (<see cref="NullSoundOutput"/> in a scripted run).</param>
    /// <param name="volumes">The volume of each category.</param>
    /// <param name="rng">The presentation generator (seeded from the world seed).</param>
    /// <param name="log">The sound log, or <see langword="null"/>.</param>
    /// <param name="soundsOff">Whether <c>--no-sounds</c> was given: every sound but the voices is off.</param>
    public SoundMixer(ISoundOutput output, SoundVolumes volumes, PresentationRng rng, ISoundLog? log = null, bool soundsOff = false)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        Volumes = (volumes ?? throw new ArgumentNullException(nameof(volumes))).Clamped();
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _log = log;
        SoundsOff = soundsOff;
    }

    /// <summary>The volume of each category.</summary>
    public SoundVolumes Volumes { get; private set; }

    /// <summary>Whether every sound but the voices is off (<c>--no-sounds</c>).</summary>
    public bool SoundsOff { get; }

    /// <summary>Whether a recording is on (push-to-talk or the mic check): sfx, ambient and ui are silent.</summary>
    public bool Recording { get; private set; }

    /// <summary>Whether an NPC voice line is on (sfx and ambient duck).</summary>
    public bool VoiceActive => _voiceActive;

    /// <summary>The duck now: 1 with no voice, <see cref="DuckLevel"/> under one, eased between.</summary>
    public double Duck => _duck;

    /// <summary>How many times the cue was let through.</summary>
    /// <param name="cue">The cue.</param>
    public int PlayedCount(SoundCue cue) => _played.TryGetValue(cue, out var count) ? count : 0;

    /// <summary>How many cues were let through in all.</summary>
    public int PlayedTotal => _played.Values.Sum();

    /// <summary>How many cues were held back for a reason (<c>recording</c>, <c>volume</c>, <c>off</c>).</summary>
    /// <param name="reason">The reason.</param>
    public int DroppedCount(string reason) => _dropped.TryGetValue(reason, out var count) ? count : 0;

    /// <summary>The ambient beds' levels now (the cross-fade, before the ambient volume, the duck and the microphone gate), strongest first.</summary>
    public IReadOnlyList<BedLevel> BedLevels => _levels.Select(static pair => new BedLevel(pair.Key, pair.Value)).OrderByDescending(static b => b.Level).ThenBy(static b => b.Bed, StringComparer.Ordinal).ToList();

    /// <summary>Sets the simulation tick the log's lines are stamped with (the game's tick, never a clock).</summary>
    /// <param name="tick">The tick.</param>
    public void SetTick(long tick) => _tick = tick;

    /// <summary>Changes the volumes (clamped to 0 to 1).</summary>
    /// <param name="volumes">The new volumes.</param>
    public void SetVolumes(SoundVolumes volumes) => Volumes = (volumes ?? throw new ArgumentNullException(nameof(volumes))).Clamped();

    /// <summary>Tells the mixer whether an NPC voice line is on. Logs the change.</summary>
    /// <param name="active">Whether a voice is on.</param>
    public void SetVoice(bool active)
    {
        if (active == _voiceActive)
        {
            return;
        }

        _voiceActive = active;
        Mark(active ? "voice-begin" : "voice-end");
    }

    /// <summary>Moves the duck and the beds' cross-fade on by a frame, and sends the beds' levels to the output.</summary>
    /// <param name="seconds">The frame's seconds.</param>
    public void Advance(double seconds)
    {
        if (seconds > 0)
        {
            var target = _voiceActive ? DuckLevel : 1.0;
            var rate = (1.0 - DuckLevel) / (_voiceActive ? DuckAttackSeconds : DuckReleaseSeconds);
            _duck = target < _duck ? Math.Max(target, _duck - (rate * seconds)) : Math.Min(target, _duck + (rate * seconds));
            AdvanceBeds(seconds);
        }

        PushBeds();
    }

    /// <summary>The gain a category has now: its volume, times the duck for sfx and ambient, and zero while a recording is on (the voices are not the mixer's to gate).</summary>
    /// <param name="category">The category.</param>
    public double Gain(SoundCategory category)
    {
        if (category == SoundCategory.Voice)
        {
            return Volumes.Voice;
        }

        if (SoundsOff || Recording)
        {
            return 0;
        }

        return category switch
        {
            SoundCategory.Sfx => Volumes.Sfx * _duck,
            SoundCategory.Ui => Volumes.Ui,
            SoundCategory.Ambient => Volumes.Ambient * _duck,
            _ => 0,
        };
    }

    /// <summary>
    /// Asks for a cue to play. Returns what was let through, or <see langword="null"/> when it was held back (sounds off, a recording on, or a category at volume 0).
    /// The take and the pitch are drawn either way, so the variation of the cues that follow does not depend on what was held back.
    /// </summary>
    /// <param name="cue">The cue.</param>
    public SoundRequest? Play(SoundCue cue)
    {
        var definition = SoundCatalog.Of(cue);
        var category = SoundCues.CategoryOf(cue);
        var take = PickTake(definition);
        var pitch = definition.PitchJitter > 0 ? _rng.NextBetween(-definition.PitchJitter, definition.PitchJitter) : 0;
        if (SoundsOff)
        {
            return Drop("off", cue);
        }

        if (Recording)
        {
            return Drop("recording", cue); // the microphone rule: sfx, ambient and ui are all silent; the beeps play outside the window
        }

        var gain = definition.Gain * Gain(category);
        if (gain <= 0)
        {
            return Drop("volume", cue);
        }

        var request = new SoundRequest(cue, category, take, gain, pitch);
        _played[cue] = PlayedCount(cue) + 1;
        _log?.Write(FormatLine(_tick, request));
        _output.Play(request);
        return request;
    }

    /// <inheritdoc />
    public double Starting()
    {
        if (SoundsOff || Volumes.Ui <= 0)
        {
            return 0;
        }

        _ = Play(SoundCue.PushToTalkStart);
        return SoundSynth.StartBeepSeconds + LeadInMargin;
    }

    /// <inheritdoc />
    public void Started()
    {
        if (Recording)
        {
            return;
        }

        Recording = true;
        Mark("recording-begin");
        _output.StopAll();
    }

    /// <inheritdoc />
    public void Stopped()
    {
        if (!Recording)
        {
            return;
        }

        Recording = false;
        Mark("recording-end");
        _ = Play(SoundCue.PushToTalkStop);
    }

    /// <summary>
    /// Sets the ambient beds' targets (S25): the weights the player's position calls for. The levels move toward them over <see cref="BedFadeSeconds"/>; a bed that is not named
    /// fades out. S24 plays no bed; this is the input S25 feeds.
    /// </summary>
    /// <param name="inputs">The beds and their weights.</param>
    public void SetBeds(IReadOnlyList<BedInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        _targets.Clear();
        foreach (var input in inputs)
        {
            _targets[input.Bed] = double.IsNaN(input.Weight) ? 0 : Math.Clamp(input.Weight, 0, 1);
            _levels.TryAdd(input.Bed, 0);
        }
    }

    /// <summary>Writes a mark line to the log (a recording or a voice began or ended).</summary>
    /// <param name="text">The mark.</param>
    public void Mark(string text) => _log?.Write(string.Create(CultureInfo.InvariantCulture, $"{_tick:D6} mark    {text}"));

    /// <summary>Writes the summary lines of the log (the counts of every cue played, and of what was held back).</summary>
    public void WriteSummary()
    {
        if (_log is null)
        {
            return;
        }

        var counts = _played.OrderBy(static pair => SoundCues.NameOf(pair.Key), StringComparer.Ordinal).Select(static pair => $"{SoundCues.NameOf(pair.Key)}={pair.Value}");
        _log.Write($"# summary: {PlayedTotal} played ({string.Join(' ', counts)})");
        _log.Write($"# held back: recording={DroppedCount("recording")} volume={DroppedCount("volume")} off={DroppedCount("off")}");
    }

    /// <summary>The sound log's line for a cue that played: tick, category, cue, take, gain and pitch.</summary>
    /// <param name="tick">The tick.</param>
    /// <param name="request">What played.</param>
    public static string FormatLine(long tick, SoundRequest request) =>
        string.Create(CultureInfo.InvariantCulture, $"{tick:D6} {SoundCues.NameOf(request.Category),-7} {SoundCues.NameOf(request.Cue),-17} take={request.Take} gain={request.Gain:0.00} pitch={request.Pitch:+0.00;-0.00;+0.00}");

    private SoundRequest? Drop(string reason, SoundCue cue)
    {
        _dropped[reason] = DroppedCount(reason) + 1;
        if (reason != "off")
        {
            // A comment line (it starts with #): what the rule held back, so a log shows the microphone rule at work. It is not a cue that played.
            _log?.Write(string.Create(CultureInfo.InvariantCulture, $"# {_tick:D6} held back ({reason}): {SoundCues.NameOf(SoundCues.CategoryOf(cue))} {SoundCues.NameOf(cue)}"));
        }

        return null;
    }

    private int PickTake(SoundDefinition definition)
    {
        if (definition.Takes == 1)
        {
            return 0;
        }

        var take = _rng.NextInt(definition.Takes);
        if (_lastTake.TryGetValue(definition.Cue, out var last) && last == take)
        {
            take = (take + 1 + _rng.NextInt(definition.Takes - 1)) % definition.Takes; // never the same take twice in a row
        }

        _lastTake[definition.Cue] = take;
        return take;
    }

    private void AdvanceBeds(double seconds)
    {
        var step = seconds / BedFadeSeconds;
        foreach (var bed in _levels.Keys.ToList())
        {
            var target = _targets.TryGetValue(bed, out var weight) ? weight : 0;
            var level = _levels[bed];
            level = target > level ? Math.Min(target, level + step) : Math.Max(target, level - step);
            if (level <= 0 && target <= 0)
            {
                _ = _levels.Remove(bed);
            }
            else
            {
                _levels[bed] = level;
            }
        }
    }

    private void PushBeds()
    {
        if (_levels.Count == 0 && !_bedsPushed)
        {
            return; // S24 has no beds: nothing to tell the output every frame
        }

        _bedsPushed = _levels.Count > 0;
        var gain = Gain(SoundCategory.Ambient);
        _output.SetBeds(_levels.Select(pair => new BedLevel(pair.Key, pair.Value * gain)).ToList());
    }
}
