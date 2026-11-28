using Vocaterra.Game;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Title;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.Worlds;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Flow;
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

// The content bundle, the locale and the scene's layout are loaded once, before the first window opens (a title screen or the game's), so a missing or invalid bundle is a message and an exit code, not a crash.
GameSetup? setup;
try
{
    setup = GameSetup.Load(options, out var usageError);
    if (setup is null)
    {
        Console.Error.WriteLine(usageError);
        Console.Error.Write(LaunchOptions.Usage);
        return 2;
    }
}
catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException or IOException or UnauthorizedAccessException or ContentValidationException or System.Text.Json.JsonException or InvalidDataException or SceneLayoutException or SaveException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

// The worlds on disk and the one settings file (Q01). A scripted run reads and writes no settings file but the one it is told of, so its pictures are the same on every machine.
var store = new WorldStore(UserFolders.Saves(options), static () => WallClock.Now.ToUnixTimeSeconds());
var settingsPath = UserFolders.SettingsPath(options);
WorldEntry? current = null; // the world of the title screen that is being played, whose last-played time is touched when it starts and when it ends

// A run opens on the title screen when it has no start flag (or asks for it); a run with a start flag, or a scripted one, builds its world first, as it always did, and opens in it.
StartedWorld? initial = null;
if (!options.ShowsTitle)
{
    try
    {
        initial = WorldStart.Build(options, setup, ReadSettings(), setup.StartedMilliseconds, out var exitCode);
        if (initial is null)
        {
            return exitCode;
        }
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException or IOException or UnauthorizedAccessException or ContentValidationException or System.Text.Json.JsonException or InvalidDataException or SceneLayoutException or SaveException or ArgumentException)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

// B46, B48: a scripted run (a screenshot) and any run in a Wayland session tell SDL not to follow the X server's outputs, so its display cannot vanish under it when the monitor
// sleeps (see SdlDisplays); SDL starts in the game's constructor.
if (SdlDisplays.IgnoreRandRChangesIfNeeded(options.IsScreenshotRun) && !options.IsScreenshotRun)
{
    Console.WriteLine($"display: SDL does not follow the X server's outputs ({SdlDisplays.RandRVariable}=0; a Wayland session, B48)");
}

// The one window of the run: the title screen, and the world of the run or of a choice made there, one after the other (a Main menu returns to the title; another world starts in the same window).
// The turn worker is started by the game itself (it warms the guide up and loads Vosk behind the loading screen).
using var app = new AppGame(options, setup.Data.Locale, store, settingsPath, ReadSettings, static () => (ulong)System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 1_000_000_000), initial, StartFromTitle, world => crash.Attach(world.CrashSource), WorldEnded, notice: null);
try
{
    app.Run();
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
    Environment.Exit(app.ExitCode);
}

return app.ExitCode;

// The settings the settings file holds, read when the title opens and when a world starts, so what a settings page changed is what the next one starts with; none for a run that keeps no file.
SaveSettings? ReadSettings() => settingsPath is null ? null : SettingsFile.Read(settingsPath);

// A world chosen on the title screen: its seed and type, its folder (its save, which the autosave, the Exit save and F6 all write), and, for a world that has a save, loading it as --load does.
WorldAttempt StartFromTitle(TitleChoice choice)
{
    var chosen = choice.World!;
    var worldOptions = options.WithoutStartFlags() with
    {
        Seed = chosen.Meta!.Seed,
        World = WorldChoices.Of(chosen.Meta.World),
        WorldFolder = chosen.Directory,
        SavePath = chosen.SavePath,
        LoadPath = chosen.HasSave ? chosen.SavePath : null,
    };
    try
    {
        var started = WorldStart.Build(worldOptions, setup, ReadSettings(), MonotonicClock.Milliseconds(), out var exitCode);
        if (started is null)
        {
            return new WorldAttempt(null, ExitCode: exitCode);
        }

        current = Touch(chosen); // played now: it comes first in the Load list
        Console.WriteLine($"world: {current.Name} ({current.Folder}), seed {current.Meta!.Seed}, {current.Meta.World}; its save is {current.SavePath}");
        return new WorldAttempt(started);
    }
    catch (Exception ex) when (ex is SaveException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or ArgumentException)
    {
        // A world that cannot be started (its save is of another seed or an older version, or unreadable): the title opens again on the Load page, and says why.
        Console.Error.WriteLine(ex.Message);
        return new WorldAttempt(null, ex.Message);
    }
}

// The world is over and closed: its last-played time is now.
void WorldEnded(VoxelGame world)
{
    if (current is not null)
    {
        _ = Touch(current);
        current = null;
    }
}

// The world's last-played time is now (it was started, or left); a folder that cannot be written is said and the game goes on.
WorldEntry Touch(WorldEntry world)
{
    try
    {
        return store.Touch(world);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"world: {world.Name}: not touched: {ex.Message}");
        return world;
    }
}
