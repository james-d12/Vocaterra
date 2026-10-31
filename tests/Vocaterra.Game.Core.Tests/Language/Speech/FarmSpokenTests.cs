using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>
/// The phrases ¿Cómo es? asks the learner to say (VILLAGE_PLAN V13), spoken: Kokoro says each at its step and Vosk hears it through the worker at the step's prompt (the
/// round trip every content milestone runs once, VILLAGE_PLAN section 4). The lesson's new words must be heard: gusta, gustan and gustó, manzanas, the colour (roja,
/// rojas), grande, agua and río. A word the recognizer cannot hear would fail here; it would go to the learner's hearing side or to a typed-only variant. Skipped
/// without the models.
/// </summary>
[Collection("speech models")]
public sealed class FarmSpokenTests(ITestOutputHelper output)
{
    private static readonly (string Step, string Spoken, string Intent, string[] MustHear)[] Said =
    [
        ("c1-how", "La manzana es roja.", "describe", ["manzana", "roja"]),
        ("c2-bread", "Sí, me gusta el pan.", "state_like", ["gusta", "pan"]),
        ("c2-apples", "Sí, me gustan las manzanas.", "state_like", ["gustan", "manzanas"]),
        ("c3-want", "Quiero tres manzanas rojas.", "request_item", ["tres", "manzanas", "rojas"]),
        ("c3-plan", "Voy a buscar agua.", "state_action", ["buscar", "agua"]),
        ("c4-big", "Es grande.", "describe", ["grande"]),
        ("c5-went", "Sí, fui al río.", "state_action", ["fui", "río"]),
        ("c6-liked", "Sí, me gustó el pan de Luz.", "state_like", ["gustó", "luz"]),
    ];

    [ModelFact(needsKokoro: true)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void ComoEsPhrasesAreHeardWordForWordAtTheirSteps()
    {
        foreach (var (stepId, spoken, intent, mustHear) in Said)
        {
            using var session = FarmLessonTests.AtStep(stepId);
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
