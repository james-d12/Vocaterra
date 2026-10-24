using Vocaterra.Game;
using Vocaterra.MonoGame.UI;
using Vocaterra.MonoGame.Utilities;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The check battery's screen model and its scripted answers (S11), and the command-line options that start a battery.</summary>
public sealed class CheckFrontendTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());

    private static IReadOnlyList<CheckItem> Bank => CheckBank.Load(Bundle.Value);

    // The seeded plan of a kind (the order is the world seed's; the tests that need a particular first item use Ordered or ProductionFirst).
    private static IReadOnlyList<CheckPlanEntry> Plan(CheckKind kind = CheckKind.Pre) => CheckPlan.For(Bank, kind, 42);

    // The bank in its own order: the first item is a comprehension item (hear.que-necesitas).
    private static IReadOnlyList<CheckPlanEntry> Ordered() => Entries(Bank);

    // The first item is a production item (say.pedir-agua), then the rest of the bank.
    private static IReadOnlyList<CheckPlanEntry> ProductionFirst()
    {
        var first = Bank.Single(static i => i.Id == "say.pedir-agua");
        return Entries([first, .. Bank.Where(i => i != first)]);
    }

    private static IReadOnlyList<CheckPlanEntry> Entries(IEnumerable<CheckItem> items) =>
        items.Select(static (item, position) => new CheckPlanEntry(item, position, item.Kind == CheckItemKind.Comprehension ? [0, 1, 2] : [])).ToList();

    private static CheckGrade Grade(bool correct, InputKind input = InputKind.Typed, bool dontKnow = false) =>
        new("Quiero agua, por favor.", input, correct, correct ? "request_item" : "greet", [], input == InputKind.Spoken ? 0.9 : null, dontKnow, correct, new ReplayEntry(null, null));

    // Picks every comprehension item on the screen, so the battery stands at its first production item (or at its end).
    private static void PickThroughComprehension(CheckScreen screen)
    {
        while (screen.Current is { Item.Kind: CheckItemKind.Comprehension })
        {
            _ = screen.Pick(0);
        }
    }

    [Fact]
    public void TheScreenShowsTheFirstItemAndCountsItsAnswers()
    {
        var plan = Plan();
        var screen = new CheckScreen(CheckKind.Pre, plan, () => 0);

        Assert.Equal(0, screen.Index);
        Assert.Same(plan[0], screen.Current);
        Assert.Equal(CheckPhase.Showing, screen.Phase);
        Assert.False(screen.IsDone);
    }

    [Fact]
    public void APickIsRecordedWithItsLatencyAndTheBatteryMovesOn()
    {
        var clock = 1000.0;
        var plan = Ordered();
        var screen = new CheckScreen(CheckKind.Pre, plan, () => clock);

        clock = 1812.4;
        Assert.True(screen.Pick(plan[0].ShownCorrect));

        var result = Assert.Single(screen.Results);
        Assert.Equal(CheckInput.Pick, result.Input);
        Assert.Equal(plan[0].ShownCorrect, result.Option);
        Assert.True(result.Correct);
        Assert.Equal(812.4, result.LatencyMs, 6);
        Assert.Equal(1, screen.Index);
    }

    [Fact]
    public void AWrongPickIsNotCorrectAndAPickOnAProductionItemIsRefused()
    {
        var screen = new CheckScreen(CheckKind.Pre, Ordered(), () => 0);
        var wrong = (screen.Current!.ShownCorrect + 1) % 3;

        Assert.True(screen.Pick(wrong));
        Assert.False(screen.Results[0].Correct);

        PickThroughComprehension(screen);
        Assert.Equal(CheckItemKind.Production, screen.Current!.Item.Kind);
        Assert.False(screen.Pick(0));
    }

    [Fact]
    public void AProductionAnswerGoesThroughTheWorkerAndItsGradeIsRecorded()
    {
        var clock = 0.0;
        var reported = new List<CheckResult>();
        var screen = new CheckScreen(CheckKind.Pre, Plan(), () => clock, reported.Add);
        PickThroughComprehension(screen);
        var at = screen.Index;

        Assert.True(screen.Sent());
        Assert.Equal(CheckPhase.Thinking, screen.Phase);
        Assert.False(screen.Sent()); // a second send while one is with the worker is refused

        clock = 340;
        screen.Graded(Grade(correct: true));

        var result = Assert.Single(reported, static r => r.Kind == CheckItemKind.Production);
        Assert.True(result.Correct);
        Assert.Equal(CheckInput.Typed, result.Input);
        Assert.Equal(340, result.LatencyMs, 6);
        Assert.Equal("request_item", result.Intent);
        Assert.Equal(at + 1, screen.Index);
        Assert.Equal(CheckPhase.Showing, screen.Phase);
    }

    [Fact]
    public void NoSeIsRecordedAsDontKnowWithItsButtonInput()
    {
        var screen = new CheckScreen(CheckKind.Post, Plan(CheckKind.Post), () => 0);
        PickThroughComprehension(screen);

        _ = screen.Sent();
        screen.Graded(Grade(correct: false, input: InputKind.Button, dontKnow: true));

        var result = screen.Results[^1];
        Assert.Equal(CheckInput.Button, result.Input);
        Assert.True(result.DontKnow);
        Assert.False(result.Correct);
    }

    [Fact]
    public void AFailedGradeKeepsTheItemOnTheScreenWithTheReason()
    {
        var screen = new CheckScreen(CheckKind.Pre, Plan(), () => 0);
        PickThroughComprehension(screen);

        var item = screen.Current;
        _ = screen.Sent();
        screen.Failed("No hay reconocedor de voz.");

        Assert.Same(item, screen.Current);
        Assert.Equal(CheckPhase.Showing, screen.Phase);
        Assert.Equal("No hay reconocedor de voz.", screen.Notice);
    }

    [Fact]
    public void EscSkipsTheRestAndEveryItemIsRecorded()
    {
        var reported = new List<CheckResult>();
        var screen = new CheckScreen(CheckKind.Pre, Plan(), () => 0, reported.Add);
        var answered = screen.Pick(0) ? 1 : 0; // the first item of the plan is a comprehension item or not, depending on the bank and the seed

        screen.SkipRest();

        Assert.True(screen.IsDone);
        Assert.Equal(31, screen.Results.Count);
        Assert.Equal(31 - answered, screen.Results.Count(static r => r.Input == CheckInput.Skipped));
        Assert.Equal(31, reported.Count);
    }

    [Fact]
    public void TheWholeBatteryEndsWithItsLastAnswer()
    {
        var screen = new CheckScreen(CheckKind.Pre, Plan(), () => 0);
        while (!screen.IsDone)
        {
            if (screen.Current is { Item.Kind: CheckItemKind.Comprehension })
            {
                _ = screen.Pick(0);
            }
            else
            {
                _ = screen.Sent();
                screen.Graded(Grade(correct: true));
            }
        }

        Assert.Equal(31, screen.Results.Count);
        Assert.Null(screen.Current);
        Assert.Equal(31, screen.Index);
    }

    [Fact]
    public void AScriptedPickAnswersTheItemAndTheScriptStopsAtAWorldStep()
    {
        var picks = new List<int>();
        var shots = new List<string>();
        var screen = new CheckScreen(CheckKind.Pre, Ordered(), () => 0);
        var steps = new List<ScriptStep>
        {
            new(ScriptStepKind.Pick, Value: "1"),
            new(ScriptStepKind.Shot, Value: "a.png"),
            new(ScriptStepKind.Talk, Value: "shopkeeper"),
        };
        var script = new CheckScript(
            steps,
            screen,
            new CheckScriptHooks(picked => { picks.Add(picked); _ = screen.Pick(picked - 1); }, _ => { }, _ => { }, shots.Add, () => false),
            _ => { });

        script.Fill(); // the pick, then the shot (the screen is saved at the next frame)
        script.Fill(); // the world step stops the battery's script

        Assert.Equal([1], picks);
        Assert.Equal(["a.png"], shots);
        Assert.Equal(2, script.Index);
        Assert.True(script.Rests);
        Assert.Equal(1, screen.Index);
    }

    [Fact]
    public void ASayStepWaitsForAComprehensionItemAndLogsThatOnce()
    {
        var logged = new List<string>();
        var screen = new CheckScreen(CheckKind.Pre, Ordered(), () => 0);
        var steps = new List<ScriptStep> { new(ScriptStepKind.Say, Value: "Quiero agua, por favor.") };
        var script = new CheckScript(steps, screen, new CheckScriptHooks(_ => { }, _ => { }, _ => { }, _ => { }, () => false), logged.Add);

        script.Fill();
        script.Fill();

        Assert.True(script.Rests);
        Assert.Equal(0, script.Index);
        Assert.Single(logged);
    }

    [Fact]
    public void AScriptedLineAnswersAProductionItemThenWaitsForTheGradeAndTheWait()
    {
        var typed = new List<string>();
        var screen = new CheckScreen(CheckKind.Pre, ProductionFirst(), () => 0);
        var steps = new List<ScriptStep>
        {
            new(ScriptStepKind.Say, Value: "Quiero agua, por favor."),
            new(ScriptStepKind.Wait, Value: "1"),
            new(ScriptStepKind.Shot, Value: "b.png"),
        };
        var script = new CheckScript(
            steps,
            screen,
            new CheckScriptHooks(_ => { }, text => { typed.Add(text); _ = screen.Sent(); }, _ => { }, _ => { }, () => false),
            _ => { });

        script.Fill();
        Assert.Equal(["Quiero agua, por favor."], typed);
        Assert.Equal(1, script.Index);

        script.Fill(); // the answer is with the worker: the script waits
        Assert.Equal(1, script.Index);

        screen.Graded(Grade(correct: true));
        script.Fill(); // the wait (one second of frames) starts
        Assert.Equal(2, script.Index);
        Assert.False(script.Rests);
    }

    [Fact]
    public void TheCheckOptionsParseFromTheCommandLine()
    {
        Assert.True(LaunchOptions.TryParse(["--check", "pre", "--session-dir", "x", "--screenshot", "s.png", "--pick", "2", "--say", "hola"], out var options, out var error), error);
        Assert.Equal(CheckKind.Pre, options.Check);
        Assert.Equal("x", options.SessionDir);
        Assert.Equal(ScriptStepKind.Pick, options.Script[0].Kind);
        Assert.Equal("2", options.Script[0].Value);

        Assert.True(LaunchOptions.TryParse(["--check", "recall"], out options, out error), error);
        Assert.Equal(CheckKind.Recall, options.Check);

        Assert.False(LaunchOptions.TryParse(["--check", "later"], out _, out error));
        Assert.Contains("pre, post or recall", error, StringComparison.Ordinal);

        Assert.False(LaunchOptions.TryParse(["--screenshot", "s.png", "--pick", "1"], out _, out error));
        Assert.Contains("needs --check", error, StringComparison.Ordinal);

        Assert.False(LaunchOptions.TryParse(["--screenshot", "s.png", "--check", "pre", "--pick", "4"], out _, out error));
        Assert.Contains("1 to 3", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCheckLayerNamesEachKindOfBattery()
    {
        Assert.Equal("Check: before the game", CheckLayer.TitleOf(CheckKind.Pre));
        Assert.Equal("Check: after the game", CheckLayer.TitleOf(CheckKind.Post));
        Assert.Equal("Check: recall", CheckLayer.TitleOf(CheckKind.Recall));
    }
}
