using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>The playtest profile: its save goes to the session folder and F6 writes it; a run that is not a playtest has none of it.</summary>
public sealed class PlaytestFlowTests
{
    [Fact]
    public void APlaytestsSaveIsInTheSessionFolderAndF6WritesIt()
    {
        var (flow, lines) = Make(enabled: true);
        Assert.Null(flow.SavePath);
        flow.Prepare("/tmp/session-x");
        Assert.Equal(PlaytestEnd.SaveIn("/tmp/session-x"), flow.SavePath);
        Assert.Equal(flow.SavePath, flow.ManualSaveFile);
        Assert.StartsWith("playtest: the pre-check, the run, then the post-check", Assert.Single(lines));
        Assert.False(flow.ShowsThankYou);
    }

    [Fact]
    public void ARunThatIsNotAPlaytestHasNoneOfIt()
    {
        var (flow, lines) = Make(enabled: false);
        flow.Prepare("/tmp/session-y");
        flow.StartIfDue(atStepBoundary: true, worldRunning: true, dialogueOpen: false);
        flow.EndKey(worldRunning: true);
        flow.SaveAtQuit();
        Assert.Null(flow.SavePath);
        Assert.Null(flow.ManualSaveFile);
        Assert.Empty(lines);
    }

    [Fact]
    public void TheRecallNamesTheSessionTheSaveWasMadeIn()
    {
        // V30: a load keeps Day as saved (V25), so the save of day 3 is loaded at day 3, not 4.
        var (flow, lines) = Make(enabled: true, recall: true, day: 3);
        flow.Prepare("/tmp/session-z");
        Assert.Equal("playtest: the next-day recall of the save from session 3; the run ends after the check", Assert.Single(lines));
    }

    private static (PlaytestFlow Flow, List<string> Lines) Make(bool enabled, bool recall = false, int day = 1)
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        session.Day = day;
        var lines = new List<string>();
        var sound = new SoundFrontend(new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(1)), new SoundEventMapper());
        var voice = new NpcVoice(Path.GetTempPath(), new SpeechIndex([]), new NoOutput(), muted: true, _ => { });
        var checks = new CheckBattery(session, static () => null, static () => 0, sound, voice, new ListenSession(new PushToTalkSpeechCapture(null)), new OpenKeySwallow(), [], false, enabled,
            new CheckBatteryHooks(_ => { }, static () => false, static () => false, _ => { }, () => { }, _ => { }), _ => { });
        var saves = new SaveService(session, () => RouteScene.Build(), () => new SaveSettings(new SoundVolumes()), lines.Add);
        var flow = new PlaytestFlow(enabled, recall, scriptedRun: false, scriptLength: 0, session, checks, saves, new PlaytestHooks(static () => 0, _ => { }, () => { }, () => { }), lines.Add);
        return (flow, lines);
    }

    private sealed class NoOutput : IAudioOutput
    {
        public bool Available => false;

        public bool IsPlaying => false;

        public bool Start(Vocaterra.Nlp.Core.Audio.AudioBuffer clip) => false;

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
