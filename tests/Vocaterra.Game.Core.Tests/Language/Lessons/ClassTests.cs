using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>Lessons as optional classes (L01, D-game-25): the teacher's one-line offer with Sí or No, the request «¿Me enseñas?», Esc, the order that opens on meeting, the old saves.</summary>
public sealed class ClassTests
{
    private const string OfferText = "¿Una clase hoy? ¿Sí o no?";

    // Day 2 at the villager: the introduction done, the villager met on day 1.
    private static VoxelSession DayTwo()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.Lessons.CountAsCompleted("first-words", 1);
        session.Day = 2;
        session.Sitting = 2;
        Stage.TeleportToNpc(session, "villager");
        return session;
    }

    [Theory]
    [InlineData("Sí", ClassAnswer.Yes)]
    [InlineData("sí, por favor", ClassAnswer.Yes)]
    [InlineData("Claro.", ClassAnswer.Yes)]
    [InlineData("No", ClassAnswer.No)]
    [InlineData("No, gracias.", ClassAnswer.No)]
    [InlineData("¿Me enseñas?", ClassAnswer.Ask)]
    [InlineData("enséñame", ClassAnswer.Ask)]
    [InlineData("quiero pan", ClassAnswer.None)]
    [InlineData("", ClassAnswer.None)]
    public void TheShippedLinesAreReadAsYesNoOrAnAskForAClass(string line, ClassAnswer expected) =>
        Assert.Equal(expected, TestGame.Data.Words.Classes.Read(line));

    [Fact]
    public void ATeacherOffersTheClassInOneLineAndDoesNotStartItAndTheTalkOpensAsBefore()
    {
        var session = DayTwo();

        var opening = session.OpenTalk("villager");

        Assert.NotEmpty(opening.Lines); // his own greeting and trade prompt come as before
        Assert.Equal("personas", session.Lessons.OfferStandingAt("villager"));
        Assert.Equal([OfferText], session.Lessons.TakeLines());
        Assert.Contains(session.Support.TakeCues(), static c => c.LineId == "guide.lesson.villager.offer.1");
        Assert.Null(session.Lessons.Active);
        Assert.False(session.Lessons.Engaged);
        Assert.Equal(opening.Prompt, session.Conversation.PromptOf("villager"));
    }

    [Fact]
    public void AfterASleepTheVillagerOffersTheClassAndNoGraciasDeclinesIt()
    {
        var session = Vocaterra.Game.Core.Tests.Save.SleepTests.EndedFirstSession();
        SaveGame.Sleep(session, SaveGame.Capture(session, TestGame.Route()));
        Stage.TeleportToNpc(session, "villager");

        _ = session.OpenTalk("villager");

        Assert.Equal("personas", session.Lessons.OfferStandingAt("villager"));
        var turn = session.Say("No, gracias.");
        Assert.True(turn.LessonHandled);
        Assert.Null(session.Lessons.Active);
    }

    [Fact]
    public void NoCostsNothingTheTradePromptStaysAndTheTeacherDoesNotAskAgainThatDay()
    {
        var session = DayTwo();
        var opening = session.OpenTalk("villager");

        var turn = session.Say("No, gracias.");

        Assert.True(turn.LessonHandled); // the class took the line: the NPC's ordinary reply is not shown
        Assert.Null(session.Lessons.Active);
        Assert.Null(session.Lessons.OfferStandingAt("villager"));
        Assert.Equal(opening.Prompt, session.Conversation.PromptOf("villager")); // the NPC's prompt is where it was
        session.EndTalk();

        _ = session.OpenTalk("villager"); // the same day: no second offer
        Assert.Null(session.Lessons.OfferStandingAt("villager"));
        session.EndTalk();

        session.Day = 3; // another day: the offer comes again, and Sí takes it
        _ = session.OpenTalk("villager");
        Assert.Equal("personas", session.Lessons.OfferStandingAt("villager"));
        var yes = session.Say("Sí");
        Assert.True(yes.LessonHandled);
        Assert.True(session.Lessons.Engaged);
        Assert.Equal("personas", session.Lessons.Active?.Id);
    }

    [Fact]
    public void AnyOtherLineWithdrawsTheOfferSoALaterSiIsTheNpcsOwn()
    {
        var session = DayTwo();
        _ = session.OpenTalk("villager");

        var turn = session.Say("¿dónde está el río?");

        Assert.False(turn.LessonHandled);
        Assert.Null(session.Lessons.OfferStandingAt("villager"));
        _ = session.Say("Sí");
        Assert.Null(session.Lessons.Active); // no class was taken by it
    }

    [Fact]
    public void AskingMeEnsenasStartsTheClassWithoutAnOfferStanding()
    {
        var session = DayTwo();
        _ = session.OpenTalk("villager");
        _ = session.Say("No");
        Assert.Null(session.Lessons.Active);

        var turn = session.Say("¿Me enseñas?");

        Assert.True(turn.LessonHandled);
        Assert.Equal("personas", session.Lessons.Active?.Id);
        Assert.True(session.Lessons.Engaged);
    }

    [Fact]
    public void TheListsAskForClassStartsItAtItsTeacherAndEscPausesAndFreesTheNpc()
    {
        var session = DayTwo();
        Assert.False(session.Lessons.RequestReplay("personas")); // the villager is not met: the page shows no button
        _ = session.OpenTalk("villager");
        session.EndTalk();
        Assert.True(session.Lessons.RequestReplay("personas")); // the Lessons page's "Ask for a class"
        Assert.Equal("personas", session.Lessons.ReplayPending);
        _ = session.OpenTalk("villager");
        Assert.True(session.Lessons.Engaged);
        Assert.True(session.Lessons.OwnsConversation("villager"));

        session.EndTalk(); // Esc

        Assert.Equal("personas", session.Lessons.Active?.Id);
        Assert.False(session.Lessons.Engaged);
        Assert.False(session.Lessons.OwnsConversation("villager")); // the NPC is free
        Assert.Equal("personas", session.Lessons.OfferFor("villager")?.LessonId); // and it is offered again
        _ = session.OpenTalk("villager");
        Assert.Equal("personas", session.Lessons.OfferStandingAt("villager"));
        _ = session.Say("Sí");
        Assert.True(session.Lessons.Engaged); // it resumes where it stopped
    }

    [Fact]
    public void ATraderWhoHasAnOfferAndAnOpenYesNoQuestionOffersNothing()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.Lessons.CountAsCompleted("first-words", 1);
        session.Lessons.CountAsCompleted("first-class", 1);
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        Assert.Null(session.Lessons.OfferStandingAt("guide")); // nothing else of Luna is open
    }

    [Fact]
    public void AnOrderShowsOnceItsNpcIsMetOnTheFirstDayAndAClassIsOpenWithIt()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        var order = session.Objectives.Single(static o => o.Id == "fruit-for-mar");
        _ = session.Simulation.GrantItems(Vocaterra.Game.Core.Items.ItemId.Berry, 3);
        Assert.False(session.Shown(order.Spec));

        Stage.TeleportToNpc(session, "fisher");
        var opening = session.OpenTalk("fisher");

        Assert.True(session.Shown(order.Spec));
        Assert.True(session.HandOverOpen); // no lesson and no day holds it: Mar asks for the fruit at once (D-game-27)
        Assert.Equal(["¡Hola! ¿Qué tienes?"], opening.Lines);
        _ = session.HandOverGoods();
        Assert.True(order.Complete);
    }

    [Fact]
    public void AnEngagedClassInAnOldSaveLoadsPausedAndAFirstWordsSaveSplitsIntoTheIntroductionAndHerFirstClass()
    {
        var session = DayTwo();
        _ = session.OpenTalk("villager");
        Assert.True(session.Lessons.Start("personas"));
        var text = SaveGame.Save(session, TestGame.Route());

        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped);

        Assert.Equal("personas", loaded.Lessons.Active?.Id);
        Assert.False(loaded.Lessons.Engaged); // loaded paused

        // A save of the old first-words (37 steps): mid-way in the verb unit, the introduction goes on at getting unstuck and the class is not begun...
        var old = SaveGame.Parse(SaveGame.Save(DayTwo(), TestGame.Route()));
        var oldLessons = old.Lessons!.Lessons.Where(static l => l.Id != "first-class").Select(static l => l.Id == "first-words"
            ? l with { Status = LessonStatus.InProgress, Step = 20, EverCompleted = false, Outcomes = [] }
            : l).ToArray();
        var midway = SaveGame.Load(SaveGame.Serialize(old with { Lessons = old.Lessons with { Lessons = oldLessons, Active = "first-words" } }), TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped);
        Assert.Equal(("first-words", 16), (midway.Lessons.Active?.Id, midway.Lessons.StepNumber - 1));
        Assert.Equal(LessonStatus.NotStarted, midway.Lessons.StatusOf("first-class"));

        // ... and past it, the class counts as done.
        var past = oldLessons.Select(static l => l.Id == "first-words" ? l with { Step = 30 } : l).ToArray();
        var after = SaveGame.Load(SaveGame.Serialize(old with { Lessons = old.Lessons with { Lessons = past, Active = "first-words" } }), TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped);
        Assert.Equal(LessonStatus.Completed, after.Lessons.StatusOf("first-class"));
        Assert.Equal(18, after.Lessons.StepNumber - 1);
    }
}
