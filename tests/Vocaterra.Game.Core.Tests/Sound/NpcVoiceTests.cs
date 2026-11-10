using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Game.Core.Tests.Sound;

/// <summary>
/// The NPC voices (S9) with a fake output: which clip plays for a shown line, the order of a turn's lines, the slow file, mute, no device,
/// missing files. The committed speech folder is read through the core's <see cref="StaticLineStore"/>; no sound device is needed. The voice logs
/// each clip it starts as <c>voice: &lt;lineId&gt;</c>, and the tests read the played ids from that log.
/// </summary>
public sealed class NpcVoiceTests
{
    private static readonly Lazy<(string Speech, IReadOnlyList<SpeechLine> Lines)> Committed = new(() =>
    {
        var lines = SpeechLines.Enumerate(TestGame.Bundle, TestGame.Nouns, TestGame.Phrases, TestGame.Data.Npcs);
        return (Path.Combine(GameBundle.ResolveDirectory(), SpeechLines.FolderName), lines);
    });

    [Fact]
    public void ATurnsLinesPlayOneAfterTheOtherInTheOrderShown()
    {
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: false);

        voice.Say(NpcService.VillagerAgent, ["Hola.", "¡Hola! ¿Qué buscas?"]);
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

        Assert.Equal(["guide.review.phrase.no_gracias.1", "guide.review.phrase.no_gracias.1.slow"], Played(logs)); // the first line with that text (S11 added two)
    }

    [Fact]
    public void ANewUtteranceReplacesTheClipThatPlays()
    {
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: false);

        voice.Say(NpcService.ShopkeeperAgent, ["¿Qué necesitas?"]);
        voice.Say(NpcService.ShopkeeperAgent, ["¿Quieres pan?"]);

        Assert.Equal(["shop_need.1", "shop_offer.1"], Played(logs));
        Assert.Equal(1, output.Stops);
        Assert.Contains("voice: cut off shop_need.1", logs);
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
        Assert.Contains(logs, static line => line.StartsWith("voice: missing guide.review.phrase.no_gracias.1.wav", StringComparison.Ordinal));
    }

    [Fact]
    public void AShownLineWithNoSpeakableIdIsTextOnlyNotMissing()
    {
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: false);

        voice.Say(SpeechSpeakers.Guide, ["El río está allí, a la izquierda, a sesenta bloques."]);

        Assert.Equal(1, voice.TextOnly);
        Assert.Equal(0, voice.Missing);
        Assert.Empty(output.Clips);
        Assert.Equal(["voice: text only (guide) El río está allí, a la izquierda, a sesenta bloques."], logs);
    }

    [Fact]
    public void TheCommittedFolderGivesAPlayableClipForAShownLine()
    {
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: false);

        voice.Say(NpcService.VillagerAgent, ["Hola."]);

        Assert.Equal(["villager_hello.1"], Played(logs));
        Assert.Equal(0, voice.Missing);
        Assert.Single(output.Clips);
        Assert.True(output.Clips[0].Samples.Length > 0);
    }

    [Fact]
    public void AnNpcCopyWithNoFilePlaysTheGuidesFileOfTheSameLine()
    {
        // V30: «Hola.» as the baker's copy of villager_hello.1, whose file is not in the folder: the villager's (the shared line's) file plays, and the voice counts the fall back.
        var copy = new SpeechLine("villager_hello.1.baker", "baker", "Hola.", SpeechKind.NpcCopy);
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = new NpcVoice(Committed.Value.Speech, new SpeechIndex([.. Committed.Value.Lines, copy]), output, muted: false, logs.Add);

        voice.Say("baker", ["Hola."]);

        Assert.Equal(["villager_hello.1"], Played(logs));
        Assert.Equal((1, 1, 0), (voice.FellBack, voice.Played, voice.Missing));
        Assert.Single(output.Clips);
    }

    [Fact]
    public void AnNpcThatWasNotSureWhatItHeardSaysSoInItsOwnVoice()
    {
        // #22: the baker's «No te entendí bien. ¿Me lo dices otra vez?» (the host's guide.uncertain pool) played Luna's recording; it is the baker's own now.
        var logs = new List<string>();
        var voice = Voice(new FakeOutput(), logs, muted: false);

        voice.Say("baker", ["No te entendí bien. ¿Me lo dices otra vez?"]);
        voice.Say(NpcService.ShopkeeperAgent, ["No te entendí bien. ¿Lo dices otra vez?"]);
        voice.Say(SpeechSpeakers.Guide, ["No te entendí bien. ¿Lo dices otra vez?"]);

        Assert.Equal(["guide.uncertain.2.baker", "guide.uncertain.1.shopkeeper", "guide.uncertain.1"], Played(logs));
        Assert.Equal((0, 0), (voice.FellBack, voice.Missing));
    }

    [Fact]
    public void TheShopkeepersKeyWordsPlayInHisVoiceOneAfterTheOtherAndHisQuestionFollows()
    {
        // #36: the key-word cards («el agua», «el pan») are queued as he opens the talk and his question after them (VoxelGame appends the opening that waited for the
        // cards): each clip starts when the one before has ended, in his voice, none dropped.
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: false);

        voice.Append(NpcService.ShopkeeperAgent, "el agua", slow: false);
        voice.Append(NpcService.ShopkeeperAgent, "el pan", slow: false);
        voice.Append(NpcService.ShopkeeperAgent, "¿Qué necesitas?", slow: false);
        voice.Update();
        Assert.Equal(["card.agua.shopkeeper"], Played(logs)); // the first still plays: nothing replaces it

        for (var i = 0; i < 3; i++)
        {
            output.Finish();
            voice.Update();
        }

        Assert.Equal(["card.agua.shopkeeper", "card.pan.shopkeeper", "shop_need.1"], Played(logs));
        Assert.Equal(0, output.Stops);
        Assert.False(voice.Busy);
    }

    [Fact]
    public void TheCardOfAnItemAnNpcHandsOverWaitsForHisWholeTurn()
    {
        // #53: «Quiero agua» -> «Toma la botella. El agua está en el río.» and «¿Quieres pan?», and the bottle's first card in the same frame. The card replaced
        // the queue, so the first line was cut off and the question never played; it now follows the turn.
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, NpcService.ShopkeeperAgent);
        _ = session.OpenTalk(NpcService.ShopkeeperAgent, teachKeyWords: true);
        session.FinishKeyWords();
        var output = new FakeOutput();
        var logs = new List<string>();
        var voice = Voice(output, logs, muted: false);
        var sound = new SoundFrontend(new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(1)), new SoundEventMapper());

        var turn = session.Say("quiero agua, por favor");
        voice.Say(turn.Npc, turn.GuideLines);
        new WordCardPresenter(session, new WordCardQueue(), sound, voice).AfterTick([]);
        for (var i = 0; i < 3; i++)
        {
            output.Finish();
            voice.Update();
        }

        Assert.Equal(2, turn.GuideLines.Count);
        var played = Played(logs);
        Assert.Equal(3, played.Length);
        Assert.StartsWith("shop_give.", played[0], StringComparison.Ordinal);
        Assert.Equal(["shop_offer.1", "card.botella"], played[1..]);
        Assert.Equal(0, output.Stops);
        Assert.DoesNotContain(logs, static line => line.StartsWith("voice: cut off", StringComparison.Ordinal));
        Assert.False(voice.Busy);
    }

    /// <summary>The ids the voice started, read from its log (the missing and no-device lines are not clips).</summary>
    private static string[] Played(List<string> logs) =>
        logs.Where(static line => line.StartsWith("voice: ", StringComparison.Ordinal) && !line.StartsWith("voice: missing", StringComparison.Ordinal)
            && !line.StartsWith("voice: no audio device", StringComparison.Ordinal) && !line.StartsWith("voice: fallback", StringComparison.Ordinal)
            && !line.StartsWith("voice: text only", StringComparison.Ordinal) && !line.StartsWith("voice: cut off", StringComparison.Ordinal))
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
