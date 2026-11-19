using Vocaterra.Game;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Sound;
using Vocaterra.Game.Startup;
using Vocaterra.Nlp.Content;

// The crash handler (X05) is installed before anything else, so a crash at any point writes its report. The process-wide hooks cover what
// the game loop's catch does not: an exception on another thread, or an unobserved task exception (the game goes on after that one).
var crash = new CrashHandler(static () => WallClock.Now, Console.Error, BuildCommit.Read());
AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    if (eventArgs.ExceptionObject is Exception unhandled)
    {
        crash.Handle(unhandled, "an unhandled exception", stopped: true);
    }

    Environment.Exit(CrashHandler.ExitCode);
};
TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
{
    crash.Handle(eventArgs.Exception, "an unobserved task exception", stopped: false);
    eventArgs.SetObserved();
};

if (!LaunchOptions.TryParse(args, out var options, out var error))
{
    if (error.Length > 0)
    {
        Console.Error.WriteLine(error);
    }

    Console.Error.Write(LaunchOptions.Usage);
    return error.Length > 0 ? 2 : 0;
}

// The content bundle and the scene are loaded before the window opens, so a missing or invalid bundle is a message and an exit code, not a crash.
VoxelSession session;
Scene scene;
NpcVoice voice;
SaveSettings? savedSettings = null; // the settings a loaded save holds: the menu's (the volumes, the text size, the graphics, the mouse and the needs' settings)
IReadOnlyList<DialoguePageSave>? savedDialogue = null; // the NPCs' conversation pages a loaded save holds (U08)
SceneLayout? sceneLayout = null; // the scene's layout: the ambient beds read its places (S25)
try
{
    var started = MonotonicClock.Milliseconds();
    var directory = GameBundle.ResolveDirectory(options.ContentDirectory);
    var bundle = GameBundle.Load(directory);
    // Every game file is read once, from the bytes the bundle's load verified, and checked against the others (content-plan C02): the scene too,
    // unless --scene names a file of its own, which is read from disk and skips the bundle's check.
    var locales = LocaleBundle.Load(LocaleBundle.DirectoryBeside(directory));
    var data = GameData.Load(bundle, locales.Text(LocaleBundle.ChooseCode(options.Locale, bundle)), Vocaterra.Game.Graphics.Blocks.BlockAtlasLayout.IconKeys); // I01: an item whose icon has no art is reported at start
    if (!options.CheckTalkTargets(data.Npcs.Ids, out var talkError))
    {
        // The NPCs --talk may name are the bundle's (game/npcs.json), so the check waits for the roster: a usage error like the others.
        Console.Error.WriteLine(talkError);
        Console.Error.Write(LaunchOptions.Usage);
        return 2;
    }

    if (!options.CheckWeather([.. data.Weather.Kinds.Select(static kind => kind.Id)], out var weatherError))
    {
        // The kinds --weather and --set-weather may name are the bundle's (game/weather.json), so the check waits for the data: a usage error like the others (E03, E05).
        Console.Error.WriteLine(weatherError);
        Console.Error.Write(LaunchOptions.Usage);
        return 2;
    }

    Console.WriteLine($"locale: {data.Locale.Code}, bundle {locales.BundleHash[..12]} ({string.Join(", ", locales.Locales.Select(static l => l.Code))} shipped)");
    var sceneStarted = MonotonicClock.Milliseconds();
    var layoutFile = SceneLocator.Resolve(options.SceneFile, directory);
    var layout = options.SceneFile is null ? data.Scene : SceneLayout.Load(layoutFile);
    if (options.SceneFile is not null)
    {
        Console.WriteLine($"scene: {layoutFile} is read from disk (--scene); it skips the bundle's hash check");
    }

    sceneLayout = layout;
    var worldMode = options.World == WorldChoice.Valley ? WorldMode.Bounded : WorldMode.Streamed; // B07: the streamed tall world by default, --world valley keeps the bounded valley
    Func<ulong, Scene> buildFor = seed => WorldFactory.Create(data, seed, worldMode, layout);
    if (options.LoadPath is { } loadPath)
    {
        // A save (S10): the seed is the save's unless --seed asked for another, the bundle must be the one it was made from, and the session is the next one.
        var document = SaveGame.Parse(SaveFile.Read(loadPath));
        // A save of another bundle is refused in strict mode (--strict-save, and every --playtest: its recall check needs the frozen bundle, D-game-10); otherwise it is carried into this bundle (X01).
        SaveGame.Check(document, bundle, options.SeedGiven ? options.Seed : null, migrate: !options.StrictSave);
        scene = buildFor(document.WorldSeed);
        session = SaveGame.Restore(document, data, scene, options.Credit, migrate: !options.StrictSave);
        options = options with { Seed = document.WorldSeed };
        savedSettings = document.Settings?.Clamped();
        savedDialogue = document.DialoguePages;
        Console.WriteLine($"load: {loadPath}: saved in session {document.Session}, now session {session.Day}, sitting {session.Sitting}; tick {document.Tick}, stored time {document.StoredTime}");
        if (session.Migration is { } migration)
        {
            Console.WriteLine($"load: world digest {session.Simulation.ComputeStateDigest()[..16]}, the save's {document.StateDigest[..16]} ({(migration.WorldDigestMatched ? "equal" : "differs: the content changed")}); bundle {bundle.ContentHash[..12]}, the save's {document.BundleHash[..12]}");
            foreach (var line in migration.Lines())
            {
                Console.WriteLine(line);
            }
        }
        else
        {
            Console.WriteLine($"load: world digest {session.Simulation.ComputeStateDigest()[..16]} = the save's {document.StateDigest[..16]} (restored equal); bundle {bundle.ContentHash[..12]}");
        }
    }
    else
    {
        scene = buildFor(options.Seed);
        session = new VoxelSession(data, options.Seed, GuideSession.DefaultStartedAt, scene, options.Credit);
    }

    if (options.Pregen is { } pregenRadius)
    {
        // B07d: the far view's real sections of the world round a point, made on all the workers with no window, then exit.
        return PregenCommand.Run(options, session, scene, savedSettings, pregenRadius, Console.Out);
    }

    if (options.SkipToSession is { } skipTo)
    {
        // A test aid (V12c): the new game stands where a learner who did sessions 1 to n-1 would, built from the data; the console, the log and the saves say so.
        var last = SessionSkip.LastSession(session.Lessons.Book);
        if (skipTo > last)
        {
            Console.Error.WriteLine($"--skip-to-session needs a play session from 2 to {last}, the last session that has a lesson; got {skipTo}.");
            Console.Error.Write(LaunchOptions.Usage);
            return 2;
        }

        SessionSkip.Apply(session, skipTo);
        Console.WriteLine($"session: {SessionSkip.Line(skipTo)}");
    }

    if (options.OwnPlot is { } ownPlot)
    {
        // A dev aid (Y09): the plot is checked by the land rules and owned at once, with no coins and no stake; a refusal names the reason.
        var check = session.SetPendingPlot(ownPlot.X, ownPlot.Z);
        if (!check.Ok || !session.OwnPlot())
        {
            var reason = check.Ok ? "a plot is owned already" : check.Protection == Protection.None ? check.Fault.ToString() : $"{check.Fault}, {check.Protection}";
            Console.Error.WriteLine($"--own-plot {ownPlot.X},{ownPlot.Z}: the plot cannot be owned there ({reason}).");
            return 2;
        }

        Console.WriteLine($"land: the plot {ownPlot.X},{ownPlot.Z} ({session.OwnedPlot!.Value.Width} by {session.OwnedPlot.Value.Depth}) is owned (--own-plot)");
    }

    if (options.MarkPlot is { } markPlot)
    {
        // A dev aid (Y10): the stake's use on the column, with the land rules' answer on the console and the hint line.
        var use = session.MarkPlotWithHint(markPlot.X, markPlot.Z);
        var reason = use.Check.Protection == Protection.None ? use.Check.Fault.ToString() : $"{use.Check.Fault}, {use.Check.Protection}";
        Console.WriteLine(use.Result switch
        {
            VoxelSession.StakeResult.Marked => $"land: the stake at {markPlot.X},{markPlot.Z} marked the pending plot {session.PendingPlot!.Value.X},{session.PendingPlot.Value.Z} ({session.PendingPlot.Value.Width} by {session.PendingPlot.Value.Depth}) (--mark-plot)",
            VoxelSession.StakeResult.AlreadyOwned => $"land: the stake at {markPlot.X},{markPlot.Z} marked nothing: the plot is owned already (--mark-plot)",
            _ => $"land: the stake at {markPlot.X},{markPlot.Z} was refused: {reason} (--mark-plot)",
        });
    }

    if (options.Lodging)
    {
        session.GrantLodging(); // a dev aid (Y05): the inn's bed is usable from the start
        Console.WriteLine("lodging: given (--lodging)");
    }

    if (options.Session is { } playSession)
    {
        // A run started at a given session (S26, --session): the lessons of that session are unlocked, and the objective names the new one.
        session.Day = playSession;
        session.Sitting = playSession;
        session.BeginPlaySession();
        Console.WriteLine($"session: this run is play session {playSession}");
    }

    Console.WriteLine($"scene: {layoutFile} built in {MonotonicClock.Milliseconds() - sceneStarted:0} ms");
    Console.WriteLine($"bundle: loaded in {MonotonicClock.Milliseconds() - started:0} ms; typed credit {(options.Credit.TypedUnits < WordProgress.UnitsPerUse ? "half" : "full")}, spoken credit full");

    // The NPCs' pre-rendered voices (S9): the startup line counts the speakable lines' files; the playback never builds Kokoro.
    var speech = SpeechLines.Enumerate(bundle, session.Nouns, session.Phrases, session.Data.Npcs);
    var speechDirectory = Path.Combine(directory, SpeechLines.FolderName);
    Console.WriteLine(SpeechSummary.Of(speech, speechDirectory));
    voice = new NpcVoice(speechDirectory, new SpeechIndex(speech), new DynamicSoundOutput(), options.Muted, Console.WriteLine);
    if (options.Muted)
    {
        Console.WriteLine("voice: muted (--mute)");
    }
}
catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException or IOException or UnauthorizedAccessException or ContentValidationException or System.Text.Json.JsonException or InvalidDataException or SceneLayoutException or SaveException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

// B46, B48: a scripted run (a screenshot) and any run in a Wayland session tell SDL not to follow the X server's outputs, so its display cannot vanish under it when the monitor
// sleeps (see SdlDisplays); SDL starts in the game's constructor.
if (SdlDisplays.IgnoreRandRChangesIfNeeded(options.IsScreenshotRun) && !options.IsScreenshotRun)
{
    Console.WriteLine($"display: SDL does not follow the X server's outputs ({SdlDisplays.RandRVariable}=0; a Wayland session, B48)");
}

// The turn worker is started by the game itself (it warms the guide up and loads Vosk behind the loading screen).
using var game = new VoxelGame(options, session, scene, voice, savedSettings: savedSettings, savedDialogue: savedDialogue);
crash.Attach(game.CrashSource); // the crash report, the last-chance save and the state come from the game (X05)
try
{
    game.Run();
}
catch (Exception ex)
{
    // The game loop threw: the crash report and the last-chance save are written, the paths are printed, and the exit code is not zero (X05).
    crash.Handle(ex, "the game loop", stopped: true);
    return CrashHandler.ExitCode;
}

// B46: with no display left SDL's window destruction (the game's disposal, next) dies with SIGSEGV; the run is over and its output written, so the process ends here instead.
if (SdlDisplays.NoneLeft())
{
    Console.Out.Flush();
    Console.Error.Flush();
    Environment.Exit(game.ExitCode);
}

return game.ExitCode;
