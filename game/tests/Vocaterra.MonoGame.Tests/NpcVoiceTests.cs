using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.MonoGame.Tests;

/// <summary>
/// The NPC voices (S9) with a fake output: which clip plays for a shown line, the order of a turn's lines, the slow file, mute, no device,
/// missing files. The committed speech folder is read through the core's <see cref="StaticLineStore"/>; no sound device is needed. The voice logs
/// each clip it starts as <c>voice: &lt;lineId&gt;</c>, and the tests read the played ids from that log.
/// </summary>
public sealed class NpcVoiceTests
{
    private static readonly Lazy<(string Speech, IReadOnlyList<SpeechLine> Lines)> Committed = new(() =>
    {
        var bundle = GameBundle.Load();
        var lines = SpeechLines.Enumerate(bundle, NounCatalog.Load(bundle), PhraseSheet.Load(bundle));
        return (Path.Combine(GameBundle.ResolveDirectory(), SpeechLines.FolderName), lines);
    });

    [Fact]
    public void ATurnsLinesPlayOneAfterTheOtherInTheOrderShown()
    {
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: false);

        voice.Say(SpeechSpeakers.Villager, ["Hola.", "¡Hola! ¿Qué buscas?"]);
        Assert.Equal(["villager_hello.1"], Played(logs));

        output.Finish();
        voice.Update();
        Assert.Equal(["villager_hello.1", "villager_greet.1"], Played(logs));

        output.Finish();
        voice.Update();
        Assert.Equal(2, voice.Played);
        Assert.False(voice.Busy);
    }

    [Fact]
    public void RepetirPlaysTheNormalFileAndMasDespaciosTheSlowFile()
    {
        var logs = new List<string>();
        var voice = Voice(new FakeOutput(), logs, muted: false);

        voice.Play(SpeechSpeakers.Guide, "¿Quieres agua?", slow: false);
        voice.Play(SpeechSpeakers.Guide, "¿Quieres agua?", slow: true);

        Assert.Equal(["guide_offer.1", "guide_offer.1.slow"], Played(logs));
    }

    [Fact]
    public void ANewUtteranceReplacesTheClipThatPlays()
    {
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: false);

        voice.Say(SpeechSpeakers.Shopkeeper, ["¿Qué necesitas?"]);
        voice.Say(SpeechSpeakers.Shopkeeper, ["¿Quieres pan?"]);

        Assert.Equal(["shop_need.1", "shop_offer.1"], Played(logs));
        Assert.Equal(1, output.Stops);
    }

    [Fact]
    public void WordCardsSayTheirNounInTheGuidesVoice()
    {
        var logs = new List<string>();
        var voice = Voice(new FakeOutput(), logs, muted: false);

        voice.Say(SpeechSpeakers.Guide, ["la madera"]);

        Assert.Equal(["card.madera"], Played(logs));
    }

    [Fact]
    public void MutedPlaysNothingAndLogsNothing()
    {
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: true);

        voice.Say(SpeechSpeakers.Guide, ["¿Quieres agua?"]);
        voice.Play(SpeechSpeakers.Guide, "¿Quieres agua?", slow: true);

        Assert.Empty(output.Clips);
        Assert.Equal(0, voice.Played);
        Assert.Empty(logs);
    }

    [Fact]
    public void WithoutAnAudioDeviceTheVoiceLogsOnceAndStaysSilent()
    {
        var output = new FakeOutput { Device = false };
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: false);

        voice.Say(SpeechSpeakers.Guide, ["¿Quieres agua?"]);
        voice.Say(SpeechSpeakers.Guide, ["¿Quieres agua?"]);
        voice.Play(SpeechSpeakers.Guide, "¿Quieres agua?", slow: false);

        Assert.False(output.Available);
        Assert.Equal(0, voice.Played);
        Assert.Single(logs, static line => line.Contains("no audio device", StringComparison.Ordinal));
    }

    [Fact]
    public void ALineWithoutAFileCountsAsMissingAndIsSkipped()
    {
        var empty = Path.Combine(Path.GetTempPath(), "vocaterra-s9-voice-" + Guid.NewGuid().ToString("N"));
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = new NpcVoice(empty, new SpeechIndex(Committed.Value.Lines), output, muted: false, logs.Add);

        voice.Say(SpeechSpeakers.Guide, ["¿Quieres agua?"]);

        Assert.Equal(1, voice.Missing);
        Assert.Empty(output.Clips);
        Assert.Contains(logs, static line => line.StartsWith("voice: missing guide_offer.1.wav", StringComparison.Ordinal));
    }

    [Fact]
    public void AShownLineWithNoSpeakableIdIsTextOnlyNotMissing()
    {
        var output = new FakeOutput();
        var voice = Voice(output, [], muted: false);

        voice.Say(SpeechSpeakers.Guide, ["El río está allí, a la izquierda, a sesenta bloques."]);

        Assert.Equal(1, voice.TextOnly);
        Assert.Equal(0, voice.Missing);
        Assert.Empty(output.Clips);
    }

    [Fact]
    public void TheCommittedFolderGivesAPlayableClipForAShownLine()
    {
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: false);

        voice.Say(SpeechSpeakers.Villager, ["Hola."]);

        Assert.Equal(["villager_hello.1"], Played(logs));
        Assert.Equal(0, voice.Missing);
        Assert.Single(output.Clips);
        Assert.True(output.Clips[0].Samples.Length > 0);
    }

    /// <summary>The ids the voice started, read from its log (the missing and no-device lines are not clips).</summary>
    private static string[] Played(List<string> logs) =>
        logs.Where(static line => line.StartsWith("voice: ", StringComparison.Ordinal) && !line.StartsWith("voice: missing", StringComparison.Ordinal)
            && !line.StartsWith("voice: no audio device", StringComparison.Ordinal))
            .Select(static line => line["voice: ".Length..])
            .ToArray();

    private static NpcVoice Voice(FakeOutput output, List<string> logs, bool muted) =>
        new(Committed.Value.Speech, new SpeechIndex(Committed.Value.Lines), output, muted, logs.Add);

    /// <summary>A sound output that records the clips it is given; the test says when a clip has finished.</summary>
    private sealed class FakeOutput : IAudioOutput
    {
        private bool _playing;

        /// <summary>False: there is no sound device, so the first start fails.</summary>
        public bool Device { get; init; } = true;

        /// <inheritdoc />
        public bool Available { get; private set; } = true;

        /// <inheritdoc />
        public bool IsPlaying => _playing;

        /// <summary>The clips started, in order.</summary>
        public List<AudioBuffer> Clips { get; } = [];

        /// <summary>How many times a clip was stopped.</summary>
        public int Stops { get; private set; }

        /// <inheritdoc />
        public bool Start(AudioBuffer clip)
        {
            if (!Device)
            {
                Available = false;
                return false;
            }

            Clips.Add(clip);
            _playing = true;
            return true;
        }

        /// <inheritdoc />
        public void Stop()
        {
            Stops++;
            _playing = false;
        }

        /// <summary>Marks the clip that plays as finished, as the sound device does at the end of a clip.</summary>
        public void Finish() => _playing = false;

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
