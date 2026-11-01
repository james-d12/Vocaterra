using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>
/// The phrases Mañana en el pueblo asks the learner to say (VILLAGE_PLAN V17), spoken: Kokoro says each at its step and Vosk hears it through the worker at the step's prompt
/// (the round trip every content milestone runs once, VILLAGE_PLAN section 4). The lesson's simple futures must be heard: compraré, buscaré, iré and hará. A word the
/// recognizer cannot hear would fail here; it would go to the learner's hearing side or to a typed-only variant. And the baker's name said as a vocative before a plan, at
/// her open prompt (which takes state_action since V17), is heard and read without it. Skipped without the models.
/// </summary>
[Collection("speech models")]
public sealed class TomorrowSpokenTests(ITestOutputHelper output)
{
    private static readonly (string Step, string Spoken, string[] MustHear)[] Said =
    [
        ("m1-buy", "Mañana compraré pan.", ["mañana", "compraré", "pan"]),
        ("m2-wood", "Buscaré madera para Luz.", ["buscaré", "madera", "para", "luz"]),
        ("m2-go", "Iré al río.", ["iré", "río"]),
        ("m3-luz", "Luz hará pan.", ["luz", "hará", "pan"]),
    ];

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void MananaEnElPuebloPhrasesAreHeardWordForWordAtTheirSteps()
    {
        foreach (var (stepId, spoken, mustHear) in Said)
        {
            using var session = TomorrowLessonTests.AtStep(stepId);
            Assert.True(session.StartWorker().WaitUntilReady(60000));

            _ = session.SubmitSpeak(spoken);
            var turn = Drain(session).Turn!;

            output.WriteLine($"{stepId}: said '{spoken}', heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, errors [{string.Join(", ", turn.Errors.Select(static e => e.Tag))}]");
            Assert.Equal("state_action", turn.Intent);
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
    public void APlanSaidToTheBakerWithHerNameIsHeardAndReadWithoutIt()
    {
        using var session = TestGame.NewSessionWithShippedLessons(); // session 1: the baker talks as herself
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");
        Assert.True(session.StartWorker().WaitUntilReady(60000));

        _ = session.SubmitSpeak("Luz, mañana compraré pan.");
        var turn = Drain(session).Turn!;

        output.WriteLine($"baker-open: heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, errors [{string.Join(", ", turn.Errors.Select(static e => e.Tag))}]");
        Assert.Equal(("state_action", true), (turn.Intent, turn.Communicated));
        Assert.Empty(turn.Errors);
        Assert.Contains("compraré", turn.Line, StringComparison.OrdinalIgnoreCase);
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
