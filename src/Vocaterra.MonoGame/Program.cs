using Vocaterra.MonoGame;
using Vocaterra.MonoGame.Frontend;

if (!LaunchOptions.TryParse(args, out var options, out var error))
{
    if (error.Length > 0)
    {
        Console.Error.WriteLine(error);
    }

    Console.Error.Write(LaunchOptions.Usage);
    return error.Length > 0 ? 2 : 0;
}

using var game = new VocaterraGame(options);
game.Run();
return game.ExitCode;
