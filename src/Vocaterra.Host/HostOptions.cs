namespace Vocaterra.Host;

/// <summary>The host's command line: where the bundle is, the prompt to start on and an optional one-shot transcript.</summary>
/// <param name="ContentDirectory">The content bundle directory.</param>
/// <param name="PromptId">The prompt the conversation starts on, or null for the first prompt in the bundle.</param>
/// <param name="Transcript">The line to evaluate once and exit, or null for the text loop.</param>
/// <param name="ShowHelp">True when the usage text was asked for.</param>
/// <param name="GrammarStringsOut">The file to write the game's Spanish strings to for the grammar check, or null to run the host as usual.</param>
public sealed record HostOptions(string ContentDirectory, string? PromptId, string? Transcript, bool ShowHelp, string? GrammarStringsOut = null)
{
    /// <summary>The bundle the host loads when none is named.</summary>
    public const string DefaultContentDirectory = "content/game-v1";

    /// <summary>The usage text printed by <c>--help</c> and after a malformed command line.</summary>
    public const string Usage =
        """
        Vocaterra host: a console host on the Vocaterra.Nlp packages.

        Usage:
          dotnet run --project src/Vocaterra.Host -- [options]

        Without --transcript the host runs a text loop: type a Spanish line and press Enter. /prompt <id> switches prompt, /quit leaves.
        With --transcript it evaluates that one line on --prompt, prints the outcome and exits (the form tests and scripts use).

        Options:
          --transcript "<text>"   evaluate one line and exit (needs --prompt)
          --prompt <id>           the prompt to evaluate on, or the prompt the loop starts on
          --content <dir>         the content bundle (default content/game-v1 in the current directory, else the game's bundle found above the executable)
          --grammar-strings <file>  write the game's Spanish strings as JSON Lines for the grammar check, then exit
          --help                  print this text
        """;

    /// <summary>Parses the arguments. Throws <see cref="ArgumentException"/> for a malformed command line.</summary>
    public static HostOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var contentDirectory = DefaultContentDirectory;
        string? promptId = null;
        string? transcript = null;
        var showHelp = false;
        string? grammarStringsOut = null;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    showHelp = true;
                    break;
                case "--content":
                    contentDirectory = Value(args, ref i);
                    break;
                case "--prompt":
                    promptId = Value(args, ref i);
                    break;
                case "--transcript":
                    transcript = Value(args, ref i);
                    break;
                case "--grammar-strings":
                    grammarStringsOut = Value(args, ref i);
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }

        if (transcript is not null && promptId is null)
        {
            throw new ArgumentException("--transcript needs --prompt, the prompt to evaluate the line on.");
        }

        return new HostOptions(contentDirectory, promptId, transcript, showHelp, grammarStringsOut);
    }

    private static string Value(IReadOnlyList<string> args, ref int index)
    {
        if (index + 1 >= args.Count)
        {
            throw new ArgumentException($"{args[index]} needs a value.");
        }

        index++;
        return args[index];
    }
}
