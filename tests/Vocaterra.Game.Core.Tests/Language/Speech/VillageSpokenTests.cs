using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>
/// The phrases El pueblo asks the learner to say (village-plan V10), spoken: Kokoro says each at its step and Vosk hears it through the worker at the step's prompt
/// (the round trip every content milestone runs once, village-plan section 4). A word the recognizer cannot hear would fail here; it would go to the learner's hearing
/// side or to a typed-only variant. Skipped without the models.
/// </summary>
[Collection("speech models")]
public sealed class VillageSpokenTests(ITestOutputHelper output)
{
    private static readonly (string Step, string Spoken, string Intent)[] Said =
    [
        ("v1-bakery", "¿Dónde está la panadería?", "ask_directions"),
        ("v1-smithy", "¿Dónde está la herrería?", "ask_directions"),
        ("v2-far", "¿Está lejos?", "ask_far"),
        ("v3-well", "¿Hay un pozo en el pueblo?", "ask_availability"),
        ("v4-going", "Voy a la panadería.", "state_action"),
        ("v4-plan", "Voy a comprar pan.", "state_action"),
        ("v7-went", "Sí, fui a la panadería.", "state_action"),
    ];

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void ElPueblosPhrasesAreHeardAtTheirSteps()
    {
        foreach (var (stepId, spoken, intent) in Said)
        {
            using var session = VillageLessonTests.AtStep(stepId);
            Assert.True(session.StartWorker().WaitUntilReady(60000));

            _ = session.SubmitSpeak(spoken);
            var turn = Drain(session).Turn!;

            output.WriteLine($"{stepId}: said '{spoken}', heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, errors [{string.Join(", ", turn.Errors.Select(static e => e.Tag))}]");
            Assert.Equal(intent, turn.Intent);
            Assert.True(turn.Communicated, stepId);
            Assert.Empty(turn.Errors);
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
