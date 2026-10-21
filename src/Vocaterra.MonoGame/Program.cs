using System.Text.Json;
using Vocaterra.Game;
using Vocaterra.MonoGame;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Content;

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
try
{
    var started = MonotonicClock.Milliseconds();
    var directory = GameBundle.ResolveDirectory(options.ContentDirectory);
    var bundle = GameBundle.Load(directory);
    session = new GameSession(bundle, options.Seed, GameSession.DefaultStartedAt, NounCatalog.Load(bundle), credit: options.Credit);
    Console.WriteLine($"bundle: loaded in {MonotonicClock.Milliseconds() - started:0} ms; typed credit {(options.Credit.TypedUnits < WordProgress.UnitsPerUse ? "half" : "full")}, spoken credit full");

    // The turn worker (started by the game) warms the guide up and loads Vosk behind the loading screen.
}
catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException or ContentValidationException or JsonException or InvalidDataException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

using var game = new VocaterraGame(options, session);
game.Run();
return game.ExitCode;
