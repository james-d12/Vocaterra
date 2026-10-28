using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Input;
using Vocaterra.Game.Screens;
using Vocaterra.Game.Sound;
using Vocaterra.Game.UI;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests.Screens;

/// <summary>The mic check's screen: Esc skips it, F8 during a recording counts as nothing heard, and the meter falls back while nothing is recorded.</summary>
public sealed class MicCheckScreenTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    [Fact]
    public void EscSkipsTheCheck()
    {
        var screen = Make();
        Assert.True(screen.Active);
        screen.HandleInput(Pressed(Keys.Escape));
        Assert.False(screen.Active);
    }

    [Fact]
    public void AnF8DuringARecordingCountsAsNothingHeard()
    {
        var screen = Make();
        screen.Model!.BeginListening();
        Assert.Equal(MicCheckPhase.Listening, screen.Model.Phase);
        screen.InterruptForReport();
        Assert.Equal(MicCheckPhase.Failed, screen.Model.Phase);

        screen.InterruptForReport(); // not recording: nothing changes
        Assert.Equal(MicCheckPhase.Failed, screen.Model.Phase);
    }

    [Fact]
    public void TheMeterFallsBackWhileNothingIsRecorded()
    {
        var screen = Make();
        screen.Model!.Feed(0.8, 0);
        screen.FeedWhileIdle(0.1);
        Assert.InRange(screen.Model.Level, 0, 0.8 - 1e-9);
    }

    [Fact]
    public void ARunWithoutTheCheckHasNoScreen()
    {
        var screen = new MicCheckScreen(null, RouteScene.NewSession(Bundle.Value, Nouns.Value), new ListenSession(new PushToTalkSpeechCapture(null)), Sound(), new UiCanvas(100, static () => (1280, 720)), static () => throw new InvalidOperationException("no font in this test"), false, static () => false, _ => { });
        Assert.False(screen.Active);
        screen.FeedWhileIdle(0.1);
        screen.InterruptForReport();
        screen.DriveScripted();
    }

    private static MicCheckScreen Make()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        var model = new MicCheckModel(session.LogMicCheck);
        return new MicCheckScreen(model, session, new ListenSession(new PushToTalkSpeechCapture(null)), Sound(), new UiCanvas(100, static () => (1280, 720)), static () => throw new InvalidOperationException("no font in this test"), false, static () => false, _ => { });
    }

    private static SoundFrontend Sound() => new(new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(1)), new SoundEventMapper());

    private static InputSnapshot Pressed(params Keys[] keys) => new(new KeyboardState(keys), default, new KeyboardState(), false, false, 0);
}
