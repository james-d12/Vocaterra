using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Screens;
using Vocaterra.Game.Utilities;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Tests.Screens;

/// <summary>The end of a check battery: a pre-check hands over to the world, a post-check ends the run after its thank-you frames, and a playtest's waits for Esc.</summary>
public sealed class CheckBatteryTests : IDisposable
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-checks-" + Path.GetRandomFileName());
    private readonly List<string> _lines = [];
    private readonly List<CheckScreen> _playtestEnds = [];
    private SessionLog? _log;
    private int _exits;

    public void Dispose()
    {
        _log?.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void APreCheckHandsOverToTheWorldWhenItIsDone()
    {
        var checks = Make(playtest: false);
        checks.Begin(CheckKind.Pre, scripted: false, scriptIndex: 0);
        Assert.NotNull(checks.Screen);
        Assert.Contains(_lines, static l => l.StartsWith("check: pre, ", StringComparison.Ordinal));

        checks.Screen!.SkipRest();
        checks.Drive();
        Assert.Null(checks.Screen);
        Assert.True(checks.Done);
        Assert.False(checks.EndsRun);
        Assert.Contains(_lines, static l => l.StartsWith("check pre: 0 answered, ", StringComparison.Ordinal));
        Assert.Equal(0, _exits);
    }

    [Fact]
    public void APostCheckOutsideAPlaytestEndsTheRunAfterItsThankYouFrames()
    {
        var checks = Make(playtest: false);
        checks.Begin(CheckKind.Post, scripted: false, scriptIndex: 0);
        checks.Screen!.SkipRest();
        checks.Drive();
        Assert.True(checks.Done);
        Assert.True(checks.EndsRun);
        Assert.True(checks.RestsForShot);
        for (var frame = 1; frame < CheckBattery.EndFrames; frame++)
        {
            checks.Drive();
        }

        Assert.Equal(0, _exits);
        checks.Drive();
        Assert.Equal(1, _exits);
        Assert.Empty(_playtestEnds);
    }

    [Fact]
    public void APlaytestsPostCheckTellsTheGameAndWaitsForEsc()
    {
        var checks = Make(playtest: true);
        checks.Begin(CheckKind.Post, scripted: false, scriptIndex: 0);
        var screen = checks.Screen!;
        screen.SkipRest();
        for (var frame = 0; frame < CheckBattery.EndFrames * 2; frame++)
        {
            checks.Drive();
        }

        Assert.Same(screen, Assert.Single(_playtestEnds));
        Assert.Equal(0, _exits);
        Assert.True(checks.EndsRun);
    }

    private CheckBattery Make(bool playtest)
    {
        Directory.CreateDirectory(_folder);
        _log = SessionLog.Open(_folder, static () => new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        var sound = new SoundFrontend(new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(1)), new SoundEventMapper());
        var voice = new NpcVoice(_folder, new SpeechIndex([]), new SilentOutput(), muted: true, _ => { });
        var hooks = new CheckBatteryHooks(_ => { }, static () => false, static () => false, _ => { }, () => _exits++, _playtestEnds.Add);
        return new CheckBattery(session, () => _log, MonotonicClock.Milliseconds, sound, voice, new ListenSession(new PushToTalkSpeechCapture(null)), new OpenKeySwallow(), [], scriptedRun: false, playtest, hooks, _lines.Add);
    }

    private sealed class SilentOutput : IAudioOutput
    {
        public bool Available => false;

        public bool IsPlaying => false;

        public bool Start(AudioBuffer clip) => false;

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
