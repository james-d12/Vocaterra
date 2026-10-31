using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>
/// The phrases Gente del pueblo asks the learner to say (VILLAGE_PLAN V12), spoken: Kokoro says each at its step and Vosk hears it through the worker at the step's prompt
/// (the round trip every content milestone runs once, VILLAGE_PLAN section 4). The lesson's new words must be heard: llamas, haces, herrería, hacha, campamento, and the
/// name Domingo at the start of a line. A word the recognizer cannot hear would fail here; it would go to the learner's hearing side or to a typed-only variant. Skipped
/// without the models.
/// </summary>
[Collection("speech models")]
public sealed class PeopleSpokenTests(ITestOutputHelper output)
{
    private static readonly (string Step, string Spoken, string Intent, string MustHear)[] Said =
    [
        ("g1-who", "Es el herrero.", "name_object", "herrero"),
        ("g1-plan", "Va a hacer un hacha.", "state_action", "hacha"),
        ("g3-name", "¿Cómo te llamas?", "ask_name", "llamas"),
        ("g3-job", "¿Qué haces?", "ask_activity", "haces"),
        ("g3-lives", "Vivo en el campamento.", "state_action", "campamento"),
        ("g5-name", "¿Cómo te llamas?", "ask_name", "llamas"),
        ("g6-works", "Domingo trabaja en la herrería.", "state_action", "herrería"),
        ("g6-made", "Domingo hizo un hacha.", "state_action", "domingo"),
    ];

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void GenteDelPueblosPhrasesAreHeardWordForWordAtTheirSteps()
    {
        foreach (var (stepId, spoken, intent, mustHear) in Said)
        {
            using var session = PeopleLessonTests.AtStep(stepId);
            Assert.True(session.StartWorker().WaitUntilReady(60000));

            _ = session.SubmitSpeak(spoken);
            var turn = Drain(session).Turn!;

            output.WriteLine($"{stepId}: said '{spoken}', heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, errors [{string.Join(", ", turn.Errors.Select(static e => e.Tag))}]");
            Assert.Equal(intent, turn.Intent);
            Assert.True(turn.Communicated, stepId);
            Assert.Empty(turn.Errors);
            Assert.Contains(mustHear, turn.Line, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(stepId, session.Lessons.Current?.Id); // the step is done
        }
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
