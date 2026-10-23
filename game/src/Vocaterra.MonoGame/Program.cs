using Vocaterra.Game;
using Vocaterra.MonoGame;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Content;
using Vocaterra.Simulation.Voxel;

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
        SaveGame.Check(document, bundle, options.SeedGiven ? options.Seed : null);
        scene = buildFor(document.WorldSeed);
        session = SaveGame.Restore(document, bundle, scene, nouns, options.Credit);
        options = options with { Seed = document.WorldSeed };
        savedVolumes = document.Settings?.Sound.Clamped();
        Console.WriteLine($"load: {loadPath}: saved in session {document.Session}, now session {session.PlaySession}; tick {document.Tick}, stored time {document.StoredTime}");
        Console.WriteLine($"load: world digest {session.Simulation.ComputeStateDigest()[..16]} = the save's {document.StateDigest[..16]} (restored equal); bundle {bundle.ContentHash[..12]}");
    }
    else
    {
        scene = buildFor(options.Seed);
        session = new VoxelSession(bundle, options.Seed, GuideSession.DefaultStartedAt, nouns, scene, options.Credit);
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
using var game = new VoxelGame(options, session, scene, voice, pristine, savedVolumes);
game.Run();
return game.ExitCode;
