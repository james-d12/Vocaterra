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
try
{
    var started = MonotonicClock.Milliseconds();
    var directory = GameBundle.ResolveDirectory(options.ContentDirectory);
    var bundle = GameBundle.Load(directory);
    var sceneStarted = MonotonicClock.Milliseconds();
    var layoutFile = SceneLocator.Resolve(options.SceneFile, directory);
    scene = SceneBuilder.Build(SceneLayout.Load(layoutFile), options.Seed);
    session = new VoxelSession(bundle, options.Seed, GuideSession.DefaultStartedAt, NounCatalog.Load(bundle), scene, options.Credit);
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
catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException or IOException or UnauthorizedAccessException or ContentValidationException or System.Text.Json.JsonException or InvalidDataException or SceneLayoutException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

// The turn worker is started by the game itself (it warms the guide up and loads Vosk behind the loading screen).
using var game = new VoxelGame(options, session, scene, voice);
game.Run();
return game.ExitCode;
