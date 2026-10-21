using System.Text.Json;
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

// The content bundle and the world are loaded before the window opens, so a missing or invalid bundle is a message and an exit code, not a crash.
GameSession session;
Scene? scene = null;
try
{
    var started = MonotonicClock.Milliseconds();
    var directory = GameBundle.ResolveDirectory(options.ContentDirectory);
    var bundle = GameBundle.Load(directory);
    session = new GameSession(bundle, options.Seed, GameSession.DefaultStartedAt, NounCatalog.Load(bundle), credit: options.Credit);
    Console.WriteLine($"bundle: loaded in {MonotonicClock.Milliseconds() - started:0} ms; typed credit {(options.Credit.TypedUnits < WordProgress.UnitsPerUse ? "half" : "full")}, spoken credit full");

    // The 3D scene (--voxel): the layout file at the world seed. The turn worker is not started in 3D (the guide comes in S3).
    if (options.Voxel)
    {
        var sceneStarted = MonotonicClock.Milliseconds();
        var layoutFile = SceneLocator.Resolve(options.SceneFile, directory);
        scene = SceneBuilder.Build(SceneLayout.Load(layoutFile), options.Seed);
        Console.WriteLine($"scene: {layoutFile} built in {MonotonicClock.Milliseconds() - sceneStarted:0} ms");
    }

    // The turn worker (started by the 2D game) warms the guide up and loads Vosk behind the loading screen.
}
catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException or IOException or UnauthorizedAccessException or ContentValidationException or JsonException or InvalidDataException or SceneLayoutException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

if (scene is not null)
{
    using var voxelGame = new VoxelGame(options, session, scene);
    voxelGame.Run();
    return voxelGame.ExitCode;
}

using var game = new VocaterraGame(options, session);
game.Run();
return game.ExitCode;
