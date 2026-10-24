using Vocaterra.Game;
using Vocaterra.Game.Language.Checks;
using Vocaterra.Game.Session;
using Vocaterra.MonoGame.UI;
using Vocaterra.MonoGame.Utilities;

namespace Vocaterra.MonoGame.Tests;

/// <summary>
/// The playtest profile (S12) without a window: the launch options it implies, the end rules (when the post-check starts, the end key, the save
/// file), the screenshot's size gate, and the check script's start step.
/// </summary>
public sealed class PlaytestTests
{
    private static LaunchOptions Parse(params string[] args)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        return options;
    }

    [Fact]
    public void ThePlaytestFlagImpliesThePreCheckAndTheMicCheckAndNothingElseChanges()
    {
        var fresh = Parse("--playtest");
        Assert.True(fresh.Playtest);
        Assert.Equal(CheckKind.Pre, fresh.Check);
        Assert.Equal(MicCheckMode.Force, fresh.MicCheck);
        Assert.True(fresh.ShowsMicCheck);
        Assert.False(fresh.Greyscale); // the A/B arm A is the default; --greyscale is arm B
        Assert.False(fresh.IsPlaytestRecall);
        Assert.Equal(LaunchOptions.DefaultSeed, fresh.Seed);

        var plain = Parse();
        Assert.False(plain.Playtest);
        Assert.Null(plain.Check);
        Assert.Equal(MicCheckMode.Auto, plain.MicCheck);
    }

    [Fact]
    public void AStrictSaveIsOnForAPlaytestAndWithTheFlagAndOffOtherwise()
    {
        // X01: a --load across content bundles keeps the progress by default; a playtest's recall session needs the frozen bundle (D-game-10), so it refuses.
        Assert.False(Parse().StrictSave);
        Assert.False(Parse("--load", "x.save").StrictSave);
        Assert.True(Parse("--load", "x.save", "--strict-save").StrictSave);
        Assert.True(Parse("--playtest").StrictSave);
        Assert.True(Parse("--playtest", "--load", "x.save").StrictSave);
    }

    [Fact]
    public void TheArmsAndTheExplicitOptionsStillApply()
    {
        Assert.True(Parse("--playtest", "--greyscale").Greyscale);
        Assert.False(Parse("--playtest", "--no-mic-check").ShowsMicCheck);
        Assert.Equal(CheckKind.Post, Parse("--playtest", "--check", "post").Check);
        Assert.Equal("sessions", Parse("--playtest", "--session-dir", "sessions").SessionDir);
        Assert.True(Parse("--playtest", "--no-speech").Playtest); // the flag stands; no mic check can run without speech
        Assert.False(Parse("--playtest", "--no-speech").ShowsMicCheck);
    }

    [Fact]
    public void TheScriptedPlaytestTakesPicksBecauseItImpliesACheck()
    {
        // A scripted playtest answers its pre-check (and its post-check) with --pick and --say: the implied check makes them valid.
        var scripted = Parse("--playtest", "--screenshot", "a.png", "--pick", "3", "--say", "Sí, por favor.", "--leave");
        Assert.Equal(CheckKind.Pre, scripted.Check);
        Assert.Equal(3, scripted.Script.Count);
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--pick", "3"], out _, out var error));
        Assert.Contains("--check", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ALoadedPlaytestIsTheNextDayRecallAndSavesNothing()
    {
        var recall = Parse("--playtest", "--load", "session-1/playtest.save");
        Assert.Equal(CheckKind.Recall, recall.Check);
        Assert.True(recall.IsPlaytestRecall);
        Assert.Equal("session-1/playtest.save", recall.LoadPath);
        Assert.Equal(MicCheckMode.Force, recall.MicCheck);

        // A plain load is not a playtest: no check is implied.
        var plainLoad = Parse("--load", "x.save");
        Assert.Null(plainLoad.Check);
        Assert.False(plainLoad.IsPlaytestRecall);
    }

    [Fact]
    public void TheSaveFileIsPlaytestSaveInTheSessionFolder()
    {
        var folder = Path.Combine("sessions", "20261022-101530-s1");
        Assert.Equal(Path.Combine(folder, "playtest.save"), PlaytestEnd.SaveIn(folder));
        Assert.Throws<ArgumentException>(() => PlaytestEnd.SaveIn(string.Empty));
    }

    [Fact]
    public void F6WritesThePlaytestSaveInTheSessionFolderNotTheWorkingFolder()
    {
        var playtestSave = PlaytestEnd.SaveIn(Path.Combine("sessions", "s1"));

        // A playtest's F6 goes to its session folder (the build folder is the tester's working folder); --save still wins; an ordinary run keeps its own.
        Assert.Equal(playtestSave, PlaytestEnd.ManualSavePath(null, playtestSave, "vocaterra.save.json"));
        Assert.Equal("chosen.json", PlaytestEnd.ManualSavePath("chosen.json", playtestSave, "vocaterra.save.json"));
        Assert.Equal("vocaterra.save.json", PlaytestEnd.ManualSavePath(null, null, "vocaterra.save.json"));
    }

    [Fact]
    public void TheTransferIsOfferedWhenTheGuideMadeHerOffer()
    {
        var offered = new GameEvent[] { new WorldEvent(10, 1, WorldEvents.TalkOpened, "guide") };
        Assert.False(PlaytestEnd.TransferOffered(offered));

        var made = offered.Append(new WorldEvent(12, 1, WorldEvents.TransferOffered, "guide", "¿Quieres agua?"));
        Assert.True(PlaytestEnd.TransferOffered(made));
    }

    [Fact]
    public void ThePostCheckStartsByItselfOnlyWhenEveryConditionHolds()
    {
        // playtest, worldRunning, started, campDone, transferOffered, talkOpen
        Assert.True(PlaytestEnd.StartsOnItsOwn(true, true, false, true, true, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(false, true, false, true, true, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(true, false, false, true, true, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(true, true, true, true, true, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(true, true, false, false, true, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(true, true, false, true, false, false));
        Assert.False(PlaytestEnd.StartsOnItsOwn(true, true, false, true, true, true)); // a talk still open finishes first
    }

    [Fact]
    public void TheEndKeyStartsTheRunOnlyOnceInThePlaytestWorld()
    {
        Assert.True(PlaytestEnd.EndKeyStarts(true, true, false));
        Assert.Equal("F10", PlaytestEnd.EndKeyName);
        Assert.False(PlaytestEnd.EndKeyStarts(false, true, false));
        Assert.False(PlaytestEnd.EndKeyStarts(true, false, false)); // a battery or the mic check is on
        Assert.False(PlaytestEnd.EndKeyStarts(true, true, true)); // already started
    }

    [Fact]
    public void AScreenshotWaitsForTheWantedSizeAndThenForPatienceOnly()
    {
        // The window's first frame at the default 800 x 480 is not the final size; the wanted 1280 x 720 is.
        Assert.False(ScreenshotGate.IsFinalSize(800, 480, 1280, 720, 800, 480));
        Assert.False(ScreenshotGate.IsFinalSize(1280, 720, 1280, 720, 800, 480));
        Assert.True(ScreenshotGate.IsFinalSize(1280, 720, 1280, 720, 1280, 720));

        Assert.False(ScreenshotGate.MayShoot(false, 0));
        Assert.False(ScreenshotGate.MayShoot(false, ScreenshotGate.PatienceFrames - 1));
        Assert.True(ScreenshotGate.MayShoot(false, ScreenshotGate.PatienceFrames));
        Assert.True(ScreenshotGate.MayShoot(true, 0));
    }

    [Fact]
    public void ACheckScriptStartsFromTheStepGivenAndRefusesAStepOutsideTheScript()
    {
        var bank = CheckBank.Load(GameBundle.Load());
        var plan = CheckPlan.For(bank, CheckKind.Post, 42);
        var steps = new List<ScriptStep> { new(ScriptStepKind.Leave), new(ScriptStepKind.Pick, Value: "1") };
        var hooks = new CheckScriptHooks(_ => { }, _ => { }, _ => { }, _ => { }, () => false);
        var screen = new CheckScreen(CheckKind.Post, plan, () => 0);

        var script = new CheckScript(steps, screen, hooks, _ => { }, startIndex: 1);
        Assert.Equal(1, script.Index);
        Assert.Equal(0, new CheckScript(steps, screen, hooks, _ => { }).Index);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CheckScript(steps, screen, hooks, _ => { }, startIndex: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CheckScript(steps, screen, hooks, _ => { }, startIndex: -1));
    }

    [Fact]
    public void TheClosingNoteReplacesTheSessionLogFooterOnlyWhenSet()
    {
        var screen = new CheckScreen(CheckKind.Recall, [], () => 0);
        Assert.True(screen.IsDone);
        Assert.Null(screen.Closing);
        screen.Closing = "Thank you.";
        Assert.Equal("Thank you.", screen.Closing);
    }
}
