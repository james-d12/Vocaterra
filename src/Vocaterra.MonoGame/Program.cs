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
    session = new GameSession(GameBundle.Load(options.ContentDirectory), options.Seed);
    session.Conversation.WarmUp(); // the first typed line should not stall a frame
}
catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException or ContentValidationException or JsonException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

using var game = new VocaterraGame(options, session);
game.Run();
return game.ExitCode;
