using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>
/// The phrases Números y pan asks the learner to say (village-plan V11), spoken: Kokoro says each at its step and Vosk hears it through the worker at the step's
/// prompt (the round trip every content milestone runs once, village-plan section 4), the numbers included: "dos" and "cuatro" must be heard, since the purchase and
/// the coins hang on them. A word the recognizer cannot hear would fail here; it would go to the learner's hearing side or to a typed-only variant. Skipped without the
/// models.
/// </summary>
[Collection("speech models")]
public sealed class BakerySpokenTests(ITestOutputHelper output)
{
    private static readonly (string Step, string Spoken, string Intent, string? Qty)[] Said =
    [
        ("n2-want", "Quiero dos panes, por favor.", "request_item", "2"),
        ("n2-price", "¿Cuánto cuesta?", "ask_price", null),
        ("n3-oven", "Voy a buscar madera.", "state_action", null),
        ("n5-have", "Tengo cuatro monedas.", "state_have", "4"),
        ("n7-bought", "Compré dos panes.", "state_action", "2"),
        ("n8-hay", "¿Hay pan?", "ask_availability", null),
    ];

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void NumerosYPansPhrasesAreHeardAtTheirStepsWithTheirNumbers()
    {
        foreach (var (stepId, spoken, intent, qty) in Said)
        {
            using var session = BakeryLessonTests.AtStep(stepId);
            Assert.True(session.StartWorker().WaitUntilReady(60000));

            _ = session.SubmitSpeak(spoken);
            var turn = Drain(session).Turn!;

            output.WriteLine($"{stepId}: said '{spoken}', heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Intent}, errors [{string.Join(", ", turn.Errors.Select(static e => e.Tag))}]");
            Assert.Equal(intent, turn.Intent);
            Assert.True(turn.Communicated, stepId);
            Assert.Empty(turn.Errors);
            Assert.Equal(qty, turn.Slots.TryGetValue("qty", out var heard) ? heard : null);
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
