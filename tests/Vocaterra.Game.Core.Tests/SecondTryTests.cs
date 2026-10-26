using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Tests;

/// <summary>
/// The second try after "no sé" (X04, R02): the queue's rules (one retry per phrase, at most two waiting, never a phrase already asked), the moment it is asked (the
/// next talk with its NPC, when no lesson holds the conversation), its English note, the answer it logs, and the cases it never touches (a check, "ayuda").
/// The phrase use of the retry is counted like any review answer, so the spacing gap still decides any credit (D-game-6).
/// </summary>
public sealed class SecondTryTests
{
    private const string Repeat = "{ \"id\": \"say\", \"type\": \"repeat\", \"phrase\": \"hola\", \"prompt\": \"guide-open\", \"english\": \"Say hello.\", \"help\": \"Say: Hola.\" }";
    private const string Answer = "{ \"id\": \"reply\", \"type\": \"answer\", \"phrase\": \"gracias\", \"prompt\": \"guide-open\", \"line\": \"guide_offer_yes.1\", \"translation\": \"Of course! Here you are.\", \"english\": \"Thank her.\" }";

    private static string LessonWith(string steps) =>
        "{ \"schemaVersion\": 1, \"lessons\": [ { \"id\": \"t\", \"npc\": \"guide\", \"title\": \"T\", \"english\": \"E\", \"autoStart\": false, \"steps\": [ " + steps + " ] } ] }";

    private static List<AppliedTurn> Drain(VoxelSession session, int count)
    {
        var all = new List<AppliedTurn>();
        var deadline = Environment.TickCount64 + 30000;
        while (all.Count < count)
        {
            all.AddRange(session.Collect());
            if (all.Count >= count)
            {
                break;
            }

            Assert.True(Environment.TickCount64 < deadline, $"Only {all.Count} of {count} check answers came back.");
            Thread.Sleep(2);
        }

        return all;
    }

    private static VoxelSession AtVillager()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        return session;
    }

    private static IEnumerable<WorldEvent> SecondTryEvents(VoxelSession session) =>
        session.Events.OfType<WorldEvent>().Where(static e => e.Name is WorldEvents.SecondTryAsked or WorldEvents.SecondTryAnswered);

    // ---- The queue (RetryQueue).

    [Fact]
    public void TheQueueTakesAPhraseForItsNpcOnceAndNeverAsksItAgain()
    {
        var queue = new RetryQueue();

        Assert.True(queue.Schedule("hola", "guide"));
        Assert.False(queue.Schedule("hola", "guide")); // already waiting
        Assert.Null(queue.TakeFor("shopkeeper")); // the talk is with another NPC: it waits

        var taken = queue.TakeFor("guide");
        Assert.Equal(new RetryItem("hola", "guide"), taken);
        Assert.Empty(queue.Pending);
        Assert.False(queue.Schedule("hola", "guide")); // asked in this session: a second "no sé" does not bring it back
        Assert.Contains("hola", queue.Asked);
    }

    [Fact]
    public void AtMostTwoPhrasesWaitAtOnceAndTheFirstSaidIsTheFirstAsked()
    {
        var queue = new RetryQueue();

        Assert.True(queue.Schedule("pedir-agua", "shopkeeper"));
        Assert.True(queue.Schedule("donde-esta-el-rio", "villager"));
        Assert.False(queue.Schedule("hola", "guide")); // the queue is full (MaxPending)
        Assert.Equal(["pedir-agua", "donde-esta-el-rio"], queue.Pending.Select(static p => p.PhraseId));

        _ = queue.TakeFor("shopkeeper");
        Assert.True(queue.Schedule("hola", "guide")); // room again
    }

    // ---- The session: when it is asked, what it says, and what it logs.

    [Fact]
    public void ANoSeAtAPhraseIsAskedOnceMoreAtTheNextTalkWithItsNpcWithAnEnglishNote()
    {
        var session = AtVillager();
        _ = session.Say("¿dónde está el río?");

        _ = session.Say("no sé");

        Assert.Equal([new RetryItem("donde-esta-el-rio", "villager")], session.Retries.Pending);
        Assert.Null(session.PhraseReview); // not asked in the talk it was said in

        session.EndTalk();
        var opening = session.OpenTalk("villager");

        Assert.NotNull(opening);
        Assert.Equal("donde-esta-el-rio", session.PhraseReview!.PhraseId);
        Assert.Equal("Let's try that one again: Where is the river?", session.RetryNote);
        Assert.Equal("donde-esta-el-rio", Assert.Single(SecondTryEvents(session)).Detail);
        Assert.Empty(session.Retries.Pending);
    }

    [Fact]
    public void TheSecondTryIsAnsweredAsAReviewAndLoggedAsRecovered()
    {
        var session = AtVillager();
        _ = session.Say("¿dónde está el río?");
        _ = session.Say("no sé");
        session.EndTalk();
        _ = session.OpenTalk("villager");

        var answer = session.Say("¿dónde está el río?");

        Assert.True(answer.Communicated);
        Assert.Null(session.PhraseReview); // answered
        var logged = SecondTryEvents(session).Last();
        Assert.Equal(WorldEvents.SecondTryAnswered, logged.Name);
        Assert.Equal("donde-esta-el-rio, recovered", logged.Detail);
        Assert.Contains(session.Events.OfType<PhraseUse>(), static u => u.PhraseId == "donde-esta-el-rio" && u.Kind is PhraseUseKind.Unaided or PhraseUseKind.Supported);
    }

    [Fact]
    public void TheSecondTryIsNeverAskedAgainInTheSession()
    {
        var session = AtVillager();
        _ = session.Say("¿dónde está el río?");
        _ = session.Say("no sé");
        session.EndTalk();
        _ = session.OpenTalk("villager");
        _ = session.Say("no sé"); // a second "no sé" on the retry is not an answer, and the phrase is not queued again

        Assert.Empty(session.Retries.Pending);
        Assert.DoesNotContain(SecondTryEvents(session), static e => e.Name == WorldEvents.SecondTryAnswered);

        session.EndTalk();
        _ = session.OpenTalk("villager");

        Assert.Null(session.PhraseReview);
        Assert.Null(session.RetryNote);
        Assert.Equal(1, SecondTryEvents(session).Count(static e => e.Name == WorldEvents.SecondTryAsked));
    }

    [Fact]
    public void TheSecondTryWaitsForItsNpcAndIsNotAskedWhenAnotherNpcIsTalkedTo()
    {
        var session = AtVillager();
        _ = session.Say("¿dónde está el río?");
        _ = session.Say("no sé");
        session.EndTalk();

        _ = session.OpenTalk("shopkeeper");

        Assert.Null(session.PhraseReview);
        Assert.Null(session.RetryNote);
        Assert.Equal("donde-esta-el-rio", Assert.Single(session.Retries.Pending).PhraseId);
    }

    [Fact]
    public void AyudaIsNotNoSeAndQueuesNothing()
    {
        var session = AtVillager();
        _ = session.Say("¿dónde está el río?");

        _ = session.Say("ayuda");

        Assert.Empty(session.Retries.Pending);
    }

    [Fact]
    public void NoSeAndAyudaAreServiceLinesAndNeverQueuedForASecondTry()
    {
        var sheet = TestGame.NewSession().Phrases;

        Assert.False(sheet.IsPractisable(PhraseIds.DontKnow));
        Assert.False(sheet.IsPractisable(PhraseIds.Help));
        Assert.True(sheet.IsPractisable("hola"));
    }

    [Fact]
    public void ANoSeInACheckIsGradedAndNeverRetried()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        session.OpenTalk("villager");
        var worker = session.StartWorker(loadSpeech: false);
        Assert.True(worker.WaitUntilReady(30000), "The worker did not load.");
        var item = CheckBank.Load(TestGame.Bundle).Single(static i => i.Id == "say.donde-esta-el-rio");

        _ = session.SubmitCheck(item, TurnSource.Button, GuideSession.DontKnowLine, null, null);
        var applied = Drain(session, 1).Single();

        Assert.True(applied.Outcome.Grade!.DontKnow);
        Assert.Empty(session.Retries.Pending);
        Assert.Empty(session.Retries.Asked);
        Assert.Null(session.PhraseReview);
    }

    // ---- A lesson run that says "no sé" and gets the item back.

    [Fact]
    public void ALessonRunThatSaysNoSeGetsTheItemBackAtTheNextTalkWithItsGuide()
    {
        var session = TestGame.NewSession(lessons: TestGame.Lessons(LessonWith(Repeat + "," + Answer)));
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        Assert.True(session.Lessons.Start("t"));

        Assert.True(session.Lessons.DontKnow()); // "no sé" at the repeat step of "hola"
        Assert.Equal([new RetryItem("hola", "guide")], session.Retries.Pending);
        Assert.Null(session.PhraseReview); // the lesson holds the conversation: nothing is asked in the middle of it

        var repeat = LessonDriver.DoStep(session); // the learner says "Hola." when the step asks for it
        var reply = LessonDriver.DoStep(session); // and "Gracias." at the answer step
        Assert.Equal(["say", "reply"], new[] { repeat.Id, reply.Id });
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("t"));
        Assert.Null(session.PhraseReview);
        Assert.Single(session.Retries.Pending);

        session.EndTalk();
        _ = session.OpenTalk("guide");

        Assert.Equal("hola", session.PhraseReview!.PhraseId);
        Assert.Equal("Let's try that one again: Hello.", session.RetryNote);
        Assert.Empty(session.Retries.Pending);

        _ = session.Say("Hola.");

        Assert.Equal("hola, recovered", SecondTryEvents(session).Last(static e => e.Name == WorldEvents.SecondTryAnswered).Detail);
    }

    // ---- The session's own logs of the schedule.

    [Fact]
    public void ALessonNoSeWithoutAPhraseOnTheStepQueuesNothing()
    {
        var session = TestGame.NewSession(lessons: TestGame.Lessons(LessonWith("{ \"id\": \"read\", \"type\": \"explain\", \"english\": \"Read this.\" }")));
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        _ = session.Lessons.Start("t");

        Assert.True(session.Lessons.DontKnow());

        Assert.Empty(session.Retries.Pending);
    }
}
