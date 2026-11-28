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
var displayChecked = false;

// The loop of a run: the title screen (when the run opens on it), a world, and, from the world's Main menu, the title again and another world, all in this process.
var wantTitle = options.ShowsTitle;
var worldOptions = options;
TitleNotice? notice = null;
var titleOptions = options;
var firstWorld = true;
while (true)
{
    WorldEntry? entry = null;
    if (wantTitle)
    {
        CheckDisplay();
        using var title = new TitleGame(titleOptions, setup.Data.Locale, store, ReadSettings(), settingsPath, static () => (ulong)System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 1_000_000_000), notice);
        try
        {
            title.Run();
        }
        catch (Exception ex)
        {
            crash.Handle(ex, "the title screen's loop", stopped: true);
            return CrashHandler.ExitCode;
        }

        if (options.IsScreenshotRun)
        {
            return title.ExitCode; // a scripted title picture ends the run
        }

        if (title.Choice is not { Kind: not TitleChoiceKind.Quit, World: { } chosen })
        {
            return 0; // Quit, or the window was closed
        }

        entry = chosen;
        worldOptions = TitleChosen(options.WithoutStartFlags(), chosen);
        notice = null;
    }

    StartedWorld? started;
    try
    {
        started = WorldStart.Build(worldOptions, setup, ReadSettings(), firstWorld ? setup.StartedMilliseconds : MonotonicClock.Milliseconds(), out var exitCode);
        if (started is null)
        {
            return exitCode;
        }
    }
    catch (Exception ex) when (wantTitle && ex is SaveException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or ArgumentException)
    {
        // A world chosen on the title screen that cannot be started (its save is of another seed or an older version, or unreadable): the title opens again on the Load page, and says why.
        Console.Error.WriteLine(ex.Message);
        notice = new TitleNotice("titleLoadFailed", entry?.Name ?? string.Empty, ex.Message);
        titleOptions = options.WithoutStartFlags() with { TitlePage = TitlePage.Load, TitleName = null, TitleSeed = null };
        continue;
    }
    catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException or IOException or UnauthorizedAccessException or ContentValidationException or System.Text.Json.JsonException or InvalidDataException or SceneLayoutException or SaveException or ArgumentException)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }

    firstWorld = false;
    if (entry is not null)
    {
        entry = Touch(entry); // played now: it comes first in the Load list
        Console.WriteLine($"world: {entry.Name} ({entry.Folder}), seed {entry.Meta!.Seed}, {entry.Meta.World}; its save is {entry.SavePath}");
    }

    CheckDisplay();

    // The turn worker is started by the game itself (it warms the guide up and loads Vosk behind the loading screen).
    bool returnToTitle;
    int exit;
    using (var game = new VoxelGame(started.Options, started.Session, started.Scene, started.Voice, savedSettings: ReadSettings(), savedDialogue: started.SavedDialogue, settingsPath: settingsPath))
    {
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

        returnToTitle = game.ReturnToTitle;
        exit = game.ExitCode;
    }

    if (entry is not null)
    {
        _ = Touch(entry);
    }

    if (returnToTitle)
    {
        // The Main menu: the world was saved and closed (the game is disposed above), and the title opens again for another.
        Console.WriteLine("menu: back to the title screen");
        wantTitle = true;
        titleOptions = options.WithoutStartFlags() with { TitleName = null, TitleSeed = null };
        continue;
    }

    // B46: with no display left SDL's window destruction (the game's disposal, above) dies with SIGSEGV; the run is over and its output written, so the process ends here instead.
    if (SdlDisplays.NoneLeft())
    {
        Console.Out.Flush();
        Console.Error.Flush();
        Environment.Exit(exit);
    }

    return exit;
}

// The settings the settings file holds, read when a window opens so what the title's settings page changed is what the world starts with; none for a run that keeps no file.
SaveSettings? ReadSettings() => settingsPath is null ? null : SettingsFile.Read(settingsPath);

// A world chosen on the title screen: its seed and type, its folder (its save, which the autosave, the Exit save and F6 all write), and, for a world that has a save, loading it as --load does.
LaunchOptions TitleChosen(LaunchOptions baseOptions, WorldEntry chosen) => baseOptions with
{
    Seed = chosen.Meta!.Seed,
    World = WorldChoices.Of(chosen.Meta.World),
    WorldFolder = chosen.Directory,
    SavePath = chosen.SavePath,
    LoadPath = chosen.HasSave ? chosen.SavePath : null,
};

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

// B46, B48: a scripted run (a screenshot) and any run in a Wayland session tell SDL not to follow the X server's outputs, so its display cannot vanish under it when the monitor
// sleeps (see SdlDisplays); SDL starts in the game's constructor, so this is done before the first window of the run, once.
void CheckDisplay()
{
    if (displayChecked)
    {
        return;
    }

    displayChecked = true;
    if (SdlDisplays.IgnoreRandRChangesIfNeeded(options.IsScreenshotRun) && !options.IsScreenshotRun)
    {
        Console.WriteLine($"display: SDL does not follow the X server's outputs ({SdlDisplays.RandRVariable}=0; a Wayland session, B48)");
    }
}
