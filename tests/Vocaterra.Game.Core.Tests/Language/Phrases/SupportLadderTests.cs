using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;

namespace Vocaterra.Game.Core.Tests.Language.Phrases;

/// <summary>
/// The support ladder of S8 with its first rung from S16 (D-game-13), rung by rung: what each rung shows, the rules that move a phrase from one rung to
/// the next, and the help key, which shows the English at every rung (rule 8: nothing is gated behind language). The ladder runs English with the
/// Spanish card and audio, then the Spanish card and audio (the English on the help key), then only the audio, then nothing.
/// </summary>
public sealed class SupportLadderTests
{
    private const long T0 = 1_000_000;
    private const long Minute = 60;

    private static readonly CreditRules Rules = CreditRules.Default;

    private static PhraseSheet Sheet => PhraseSheet.Load(TestGame.Bundle);

    private static PhraseUse Use(PhraseUseKind kind, long time, InputKind input = InputKind.Spoken, string phrase = "pedir-agua") =>
        new(time * 60, time, phrase, kind, "shopkeeper", "stall-need", "line", null, input, true, SupportLevel.None, null);

    // A progress and a support state for the stall's question with the phrase brought to a rung by correct spoken uses at the right times.
    private static (PhraseProgress Progress, PhraseSupport Support) At(SupportLevel rung)
    {
        var sheet = Sheet;
        var progress = new PhraseProgress(sheet);
        var times = new[] { T0, T0 + (11 * Minute), T0 + (72 * Minute) };
        for (var i = 0; i < (int)rung; i++)
        {
            _ = progress.Apply(Use(PhraseUseKind.Supported, times[i]), Rules);
        }

        Assert.Equal(rung, progress.LevelOf("pedir-agua"));
        var support = new PhraseSupport(sheet, progress, TestGame.Bundle, EnglishText.Load(TestGame.Bundle));
        _ = support.Show("shopkeeper", "stall-need");
        return (progress, support);
    }

    [Fact]
    public void TheLadderHasFourRungsInOrderAndASchemeOfWhatEachShows()
    {
        Assert.Equal(
            [SupportLevel.EnglishAndCard, SupportLevel.CardAndAudio, SupportLevel.AudioOnly, SupportLevel.None],
            Enum.GetValues<SupportLevel>());
        Assert.Equal([true, true, false, false], Enum.GetValues<SupportLevel>().Select(SupportLevels.ShowsCard));
        Assert.Equal([true, false, false, false], Enum.GetValues<SupportLevel>().Select(SupportLevels.ShowsEnglish));
        Assert.Equal(
            ["english-and-card", "card-and-audio", "audio-only", "none"],
            Enum.GetValues<SupportLevel>().Select(SupportLevelNames.Of));
    }

    [Fact]
    public void Rung1ShowsTheEnglishTheSpanishCardAndTheAudio()
    {
        var (_, support) = At(SupportLevel.EnglishAndCard);

        var card = Assert.Single(support.Cards);
        Assert.True(card.EnglishShown);
        Assert.Equal("I want water, please.", card.Meaning);
        Assert.NotEmpty(card.Glosses);
        Assert.StartsWith("Use this to ask for something", card.Explanation, StringComparison.Ordinal);
        Assert.True(support.EnglishForLines);
        Assert.Contains(support.TakeCues(), static c => c.Reason == "card"); // the audio plays when the card appears
    }

    [Fact]
    public void Rung2ShowsTheSpanishCardAndTheAudioAndKeepsTheEnglishOnTheHelpKey()
    {
        var (_, support) = At(SupportLevel.CardAndAudio);

        var card = Assert.Single(support.Cards);
        Assert.False(card.EnglishShown);
        Assert.Equal("Quiero agua, por favor.", card.Phrase);
        Assert.False(support.EnglishForLines);
        Assert.Contains(support.TakeCues(), static c => c.Reason == "card");
        _ = support.Reveal(); // the help key
        Assert.True(Assert.Single(support.Cards).EnglishShown);
        Assert.True(support.EnglishForLines);
    }

    [Fact]
    public void Rung3IsOnlyTheAudioAndRung4NothingAndTheHelpKeyBringsTheCardAndTheEnglishBackAtBoth()
    {
        foreach (var rung in new[] { SupportLevel.AudioOnly, SupportLevel.None })
        {
            var (_, support) = At(rung);
            Assert.Empty(support.Cards);
            Assert.False(support.EnglishForLines);
            Assert.Equal(rung == SupportLevel.AudioOnly ? new[] { "pedir-agua" } : Array.Empty<string>(), support.AudioOnly);

            _ = support.Reveal();

            var card = Assert.Single(support.Cards);
            Assert.True(card.EnglishShown, $"the help key shows the English at {rung}");
            Assert.Equal("I want water, please.", card.Meaning);
            Assert.True(support.EnglishForLines);
            Assert.True(support.Scaffolded("pedir-agua")); // and the next use counts as supported
        }
    }

    [Fact]
    public void TheHelpKeyShowsTheEnglishAtEveryRungInARealSession()
    {
        var gaps = new[] { 11 * Minute, 61 * Minute, 0L };
        foreach (var rung in Enum.GetValues<SupportLevel>())
        {
            var session = TestGame.NewSession();
            Stage.TeleportToNpc(session, "villager");
            for (var i = 0; i < (int)rung; i++)
            {
                _ = session.OpenTalk("villager");
                _ = session.Say("¿dónde está el río?");
                session.EndTalk();
                session.SkipTime(gaps[i]);
            }

            Assert.Equal(rung, session.PhraseProgress.LevelOf("donde-esta-el-rio"));
            _ = session.OpenTalk("villager");
            Assert.Equal(rung is SupportLevel.EnglishAndCard, session.Support.Cards.Any(static c => c.EnglishShown));

            _ = session.Say("ayuda");

            var card = Assert.Single(session.Support.Cards);
            Assert.True(card.EnglishShown, $"Ayuda shows the English at {rung}");
            Assert.Equal("Where is the river?", card.Meaning);
        }
    }

    [Fact]
    public void ARungStepsDownOnACorrectUseAndTheEnglishRungNeedsNoGapWhileTheOthersDo()
    {
        var progress = new PhraseProgress(Sheet);

        var one = progress.Apply(Use(PhraseUseKind.Supported, T0), Rules).Single();
        var early = progress.Apply(Use(PhraseUseKind.Supported, T0 + 30), Rules).Single();
        var two = progress.Apply(Use(PhraseUseKind.Supported, T0 + (11 * Minute)), Rules).Single();
        var earlier = progress.Apply(Use(PhraseUseKind.Supported, T0 + (12 * Minute)), Rules).Single();
        var three = progress.Apply(Use(PhraseUseKind.Supported, T0 + (72 * Minute)), Rules).Single();

        Assert.Equal((PhraseStep.SteppedDown, SupportLevel.CardAndAudio), (one.Step, one.Level)); // English to card: at once
        Assert.Equal((PhraseStep.TooEarly, SupportLevel.CardAndAudio), (early.Step, early.Level)); // card to audio: the ten minutes
        Assert.Equal((PhraseStep.SteppedDown, SupportLevel.AudioOnly), (two.Step, two.Level));
        Assert.Equal((PhraseStep.TooEarly, SupportLevel.AudioOnly), (earlier.Step, earlier.Level)); // audio to nothing: an hour after the last
        Assert.Equal((PhraseStep.SteppedDown, SupportLevel.None), (three.Step, three.Level));
    }

    [Fact]
    public void ANewSessionLiftsTheGapOfTheCardRungButTheEnglishRungNeverHadOne()
    {
        var progress = new PhraseProgress(Sheet);
        _ = progress.Apply(Use(PhraseUseKind.Supported, T0), Rules); // card rung, credited at T0
        progress.Session = 2;

        var result = progress.Apply(Use(PhraseUseKind.Supported, T0 + 5), Rules).Single();

        Assert.Equal((PhraseStep.SteppedDown, SupportLevel.AudioOnly), (result.Step, result.Level));
    }

    [Fact]
    public void NoSeAyudaAChoiceAndAFailForwardStepOneRungUpAndNeverAboveTheEnglishRung()
    {
        foreach (var kind in new[] { PhraseUseKind.DontKnow, PhraseUseKind.Choice, PhraseUseKind.FailForward })
        {
            var progress = new PhraseProgress(Sheet);
            _ = progress.Apply(Use(PhraseUseKind.Supported, T0), Rules);
            _ = progress.Apply(Use(PhraseUseKind.Supported, T0 + (11 * Minute)), Rules);
            _ = progress.Apply(Use(PhraseUseKind.Supported, T0 + (72 * Minute)), Rules);
            Assert.Equal(SupportLevel.None, progress.LevelOf("pedir-agua"));

            var levels = new List<SupportLevel>();
            for (var i = 0; i < 4; i++)
            {
                var use = kind == PhraseUseKind.DontKnow
                    ? new PhraseUse((T0 + 100 + i) * 60, T0 + 100 + i, "no-se", kind, "shopkeeper", "stall-need", "no sé", null, InputKind.Button, true, SupportLevel.None, ["pedir-agua"])
                    : Use(kind, T0 + 100 + i, InputKind.Button);
                _ = progress.Apply(use, Rules);
                levels.Add(progress.LevelOf("pedir-agua"));
            }

            Assert.Equal([SupportLevel.AudioOnly, SupportLevel.CardAndAudio, SupportLevel.EnglishAndCard, SupportLevel.EnglishAndCard], levels);
        }
    }

    [Fact]
    public void AHalfCreditTypedUseNeedsTwoToStepAndAnEarlyCorrectUseIsLoggedAndDoesNotStep()
    {
        var progress = new PhraseProgress(Sheet);
        _ = progress.Apply(Use(PhraseUseKind.Supported, T0), Rules); // card rung

        var first = progress.Apply(Use(PhraseUseKind.Supported, T0 + (11 * Minute), InputKind.Typed), Rules).Single();
        var second = progress.Apply(Use(PhraseUseKind.Supported, T0 + (80 * Minute), InputKind.Typed), Rules).Single();

        Assert.Equal((PhraseStep.Credited, SupportLevel.CardAndAudio), (first.Step, first.Level));
        Assert.Equal((PhraseStep.SteppedDown, SupportLevel.AudioOnly), (second.Step, second.Level));
        Assert.Equal(6 + 3 + 3, progress.RecordOf("pedir-agua").Supported);
    }

    [Fact]
    public void ThePhrasesALessonTeachesJoinTheLadderAndTheOthersStayOffIt()
    {
        var sheet = Sheet;
        var plain = new PhraseProgress(sheet);
        var taught = new PhraseProgress(sheet, 1, ["hola", "gracias"]);

        Assert.Equal(SupportLevel.None, plain.LevelOf("hola"));
        Assert.Equal(SupportLevel.EnglishAndCard, taught.LevelOf("hola"));
        Assert.Equal(SupportLevel.EnglishAndCard, taught.LevelOf("gracias"));
        Assert.Equal(SupportLevel.None, taught.LevelOf("adios")); // no lesson teaches it
        Assert.True(taught.HasScaffold("hola"));
        Assert.True(taught.HasScaffold("pedir-agua")); // the four of the stall and the river stay
        Assert.False(plain.HasScaffold("hola"));
    }
}
