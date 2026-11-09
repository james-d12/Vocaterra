using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>
/// The phrases Ayer en el pueblo asks the learner to say (village-plan V14), spoken: Kokoro says each at its step and Vosk hears it through the worker at the step's prompt
/// (the round trip every content milestone runs once, village-plan section 4). The lesson's new forms must be heard: hizo, pescó, fuimos, comieron, tengo que and compró, and
/// fueron, which the learner hears in "¿Adónde fueron?". A word the recognizer cannot hear would fail here; it would go to the learner's hearing side or to a typed-only
/// variant. And a name said as a vocative to the smith, whose answer set holds the village's names, is heard and read without it. Skipped without the models.
/// </summary>
[Collection("speech models")]
public sealed class YesterdaySpokenTests(ITestOutputHelper output)
{
    private static readonly (string Step, string Spoken, string Intent, string[] MustHear)[] Said =
    [
        ("a1-luz", "Luz hizo pan.", "state_action", ["luz", "hizo", "pan"]),
        ("a1-mar", "Mar pescó en el río.", "state_action", ["mar", "pescó", "río"]),
        ("a2-went", "Fuimos al río.", "state_action", ["fuimos", "río"]),
        ("a3-ate", "Ellos comieron pan.", "state_action", ["ellos", "comieron", "pan"]),
        ("a3-must", "Tengo que ir al río.", "state_obligation", ["tengo", "que", "ir", "río"]),
        ("a4-bought", "Luz compró pescado.", "state_action", ["luz", "compró", "pescado"]),
    ];

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void AyerEnElPuebloPhrasesAreHeardWordForWordAtTheirSteps()
    {
        foreach (var (stepId, spoken, intent, mustHear) in Said)
        {
            using var session = YesterdayLessonTests.AtStep(stepId);
            Assert.True(session.StartWorker().WaitUntilReady(60000));

            _ = session.SubmitSpeak(spoken);
            var turn = Drain(session).Turn!;

            output.WriteLine($"{stepId}: said '{spoken}', heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, errors [{string.Join(", ", turn.Errors.Select(static e => e.Tag))}]");
            Assert.Equal(intent, turn.Intent);
            Assert.True(turn.Communicated, stepId);
            Assert.Empty(turn.Errors);
            foreach (var word in mustHear)
            {
                Assert.Contains(word, turn.Line, StringComparison.OrdinalIgnoreCase);
            }

            Assert.NotEqual(stepId, session.Lessons.Current?.Id); // the step is done
        }
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void FueronIsInTheRecognizersVocabulary()
    {
        // The learner hears fueron (¿Adónde fueron?) and says fuimos, so no step listens for it: at the step the recognizer is held to the step's answers and hears
        // "Ellos fueron al río." as "fuimos al río" (V14's run). Where it listens to anything (the villager's open prompt), fueron is heard as said.
        using var session = TestGame.NewSessionWithShippedLessons();
        _ = session.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalkForClass("villager");
        Assert.True(session.StartWorker().WaitUntilReady(60000));

        _ = session.SubmitSpeak("Luz y Mar fueron al río.");
        var turn = Drain(session).Turn!;

        output.WriteLine($"villager-open: heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}");
        Assert.Contains("fueron", turn.Line, StringComparison.OrdinalIgnoreCase);
    }

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void TheSmithsNameSaidAsAVocativeIsHeardAndReadWithoutIt()
    {
        using var session = TestGame.NewSessionWithShippedLessons(); // session 1: the smith talks as himself
        Stage.TeleportToNpc(session, "smith");
        _ = session.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        _ = session.OpenTalkForClass("smith");
        Assert.True(session.StartWorker().WaitUntilReady(60000));

        _ = session.SubmitSpeak("Domingo, necesito un hacha.");
        var turn = Drain(session).Turn!;

        output.WriteLine($"smith-open: heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, errors [{string.Join(", ", turn.Errors.Select(static e => e.Tag))}]");
        Assert.Equal(("state_want", true), (turn.Intent, turn.Communicated));
        Assert.Empty(turn.Errors);
        Assert.Contains("hacha", turn.Line, StringComparison.OrdinalIgnoreCase);
    }

    private static AppliedTurn Drain(VoxelSession session)
    {
        var deadline = Environment.TickCount64 + 120000;
        while (true)
        {
            if (session.Collect().FirstOrDefault() is { } applied)
            {
                return applied;
            }

            Assert.True(Environment.TickCount64 < deadline, "The turn did not come back.");
            Thread.Sleep(5);
        }
    }
}
