using Vocaterra.Game;
using Vocaterra.Game.Language.Speech;
using Vocaterra.Game.Language.Words;
using Vocaterra.Game.Npc;
using Vocaterra.MonoGame.Input;
using Vocaterra.Nlp.Content;
using Xunit.Abstractions;

namespace Vocaterra.MonoGame.Tests;

/// <summary>
/// S22 item 4: speech recognition end to end in the game's tests. The bundle's recorded <c>Hola.</c> (<c>speech/phrase.hola.wav</c>, the Kokoro line) goes through
/// the turn worker twice, as the game sends it: as a WAV file (the <c>--wav</c> step), and as what the push-to-talk hands over (a recording from a capture device,
/// converted and trimmed). Vosk hears it, the guide evaluates it. Skipped, with the reason, when the Vosk model is not in <c>VOCATERRA_MODELS_DIR</c>.
/// </summary>
[Collection("speech models")]
public sealed class SpokenRouteTests(ITestOutputHelper output)
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    private sealed class WavCapture(short[] samples, int sampleRate) : ISpeechCapture
    {
        private bool _given;

        public int SampleRate => sampleRate;

        public void Start() => _given = false;

        public short[] ReadAvailable()
        {
            if (_given)
            {
                return [];
            }

            _given = true;
            return samples;
        }

        public void Stop()
        {
        }
    }

    private static string HolaWav => System.IO.Path.Combine(GameBundle.ResolveDirectory(), "speech", "phrase.hola.wav");

    private static VoxelSession AtTheGuide()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        Assert.True(session.StartWorker().WaitUntilReady(60000));
        session.Simulation.TeleportTo(NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc("guide").Feet, session.Simulation.Player.FeetBlock)!.Value);
        _ = session.OpenTalk("guide");
        return session;
    }

    private static AppliedTurn Drain(VoxelSession session)
    {
        var deadline = Environment.TickCount64 + 120000;
        while (Environment.TickCount64 < deadline)
        {
            var applied = session.Collect();
            if (applied.Count > 0)
            {
                return applied.Single();
            }

            Thread.Sleep(5);
        }

        throw new TimeoutException("The turn did not come back from the worker.");
    }

    [ModelFact(needsKokoro: false)]
    public void TheRecordedHolaIsHeardAsHolaFromAWavFileThroughTheWorker()
    {
        using var session = AtTheGuide();

        _ = session.SubmitWav(HolaWav);
        var applied = Drain(session);

        var turn = applied.Turn!;
        output.WriteLine($"--wav {HolaWav}: Vosk heard '{turn.Line}' (confidence {turn.Confidence:0.00}), intent {turn.Intent}, input {turn.Input}");
        Assert.Equal(InputKind.Spoken, turn.Input);
        Assert.Equal("hola", turn.Line.ToLowerInvariant().Trim(' ', '.', '¡', '!'));
        Assert.Equal("greet", turn.Intent);
        Assert.True(turn.Communicated);
    }

    [ModelFact(needsKokoro: false)]
    public void TheRecordedHolaIsHeardAsHolaFromThePushToTalkPathThroughTheWorker()
    {
        var (samples, rate) = AudioClips.ReadWav(File.ReadAllBytes(HolaWav));
        var talk = new PushToTalk(new WavCapture(samples, rate));
        Assert.True(talk.Begin());
        talk.Pump();
        var clip = talk.End();
        Assert.NotNull(clip);
        using var session = AtTheGuide();

        _ = session.SubmitClip(clip);
        var applied = Drain(session);

        var turn = applied.Turn!;
        output.WriteLine($"push-to-talk, {rate} Hz, {clip.DurationSeconds:0.00} s: Vosk heard '{turn.Line}' (confidence {turn.Confidence:0.00}), intent {turn.Intent}");
        Assert.Equal(InputKind.Spoken, turn.Input);
        Assert.Equal("hola", turn.Line.ToLowerInvariant().Trim(' ', '.', '¡', '!'));
        Assert.Equal("greet", turn.Intent);
    }
}
