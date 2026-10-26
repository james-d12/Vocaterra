using Vocaterra.Game;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Generation;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Sound;
using Vocaterra.Game.Utilities;
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
Func<Scene> pristine;
SoundVolumes? savedVolumes = null; // the volumes a loaded save holds (S24)
int? savedTextScale = null; // the text size a loaded save holds (X08)
SceneLayout? sceneLayout = null; // the scene's layout: the ambient beds read its places (S25)
try
{
    var started = MonotonicClock.Milliseconds();
    var directory = GameBundle.ResolveDirectory(options.ContentDirectory);
    var bundle = GameBundle.Load(directory);
    var sceneStarted = MonotonicClock.Milliseconds();
    var layoutFile = SceneLocator.Resolve(options.SceneFile, directory);
    var layout = SceneLayout.Load(layoutFile);
    sceneLayout = layout;
    var nouns = NounCatalog.Load(bundle);
    Func<ulong, Scene> buildFor = seed => SceneBuilder.Build(layout, seed);
    if (options.LoadPath is { } loadPath)
    {
        // A save (S10): the seed is the save's unless --seed asked for another, the bundle must be the one it was made from, and the session is the next one.
        var document = SaveGame.Parse(SaveFile.Read(loadPath));
        // A save of another bundle is refused in strict mode (--strict-save, and every --playtest: its recall check needs the frozen bundle, D-game-10); otherwise it is carried into this bundle (X01).
        SaveGame.Check(document, bundle, options.SeedGiven ? options.Seed : null, migrate: !options.StrictSave);
        scene = buildFor(document.WorldSeed);
        session = SaveGame.Restore(document, bundle, scene, nouns, options.Credit, migrate: !options.StrictSave);
        options = options with { Seed = document.WorldSeed };
        savedVolumes = document.Settings?.Sound.Clamped();
        savedTextScale = document.Settings?.Clamped().TextScale;
        Console.WriteLine($"load: {loadPath}: saved in session {document.Session}, now session {session.PlaySession}; tick {document.Tick}, stored time {document.StoredTime}");
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
        session = new VoxelSession(bundle, options.Seed, GuideSession.DefaultStartedAt, nouns, scene, options.Credit);
    }

    if (options.Session is { } playSession)
    {
        // A run started at a given session (S26, --session): the lessons of that session are unlocked, and the objective names the new one.
        session.PlaySession = playSession;
        session.BeginPlaySession();
        Console.WriteLine($"session: this run is play session {playSession}");
    }

    // The fresh scene a save is taken against: a new build of the session's seed and layout (the session's own scene is changed by the play).
    pristine = () => buildFor(session.WorldSeed);
    Console.WriteLine($"scene: {layoutFile} built in {MonotonicClock.Milliseconds() - sceneStarted:0} ms");
    Console.WriteLine($"bundle: loaded in {MonotonicClock.Milliseconds() - started:0} ms; typed credit {(options.Credit.TypedUnits < WordProgress.UnitsPerUse ? "half" : "full")}, spoken credit full");

    // The NPCs' pre-rendered voices (S9): the startup line counts the speakable lines' files; the playback never builds Kokoro.
    var speech = SpeechLines.Enumerate(bundle, session.Nouns, session.Phrases);
    var speechDirectory = Path.Combine(directory, SpeechLines.FolderName);
    Console.WriteLine(SpeechSummary.Of(speech, speechDirectory));
    voice = new NpcVoice(speechDirectory, new SpeechIndex(speech), new DynamicSoundOutput(), options.Muted, Console.WriteLine);
    if (options.Muted)
    {
        Console.WriteLine("voice: muted (--mute)");
    }
}
catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException or IOException or UnauthorizedAccessException or ContentValidationException or System.Text.Json.JsonException or InvalidDataException or SceneLayoutException or SaveException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

// The turn worker is started by the game itself (it warms the guide up and loads Vosk behind the loading screen).
using var game = new VoxelGame(options, session, scene, voice, pristine, savedVolumes, savedTextScale: savedTextScale);
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

return game.ExitCode;
