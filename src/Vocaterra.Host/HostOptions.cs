using System.Globalization;

namespace Vocaterra.Host;

/// <summary>The host's command line: where the bundle is, the prompt to start on and an optional one-shot transcript.</summary>
/// <param name="ContentDirectory">The content bundle directory.</param>
/// <param name="PromptId">The prompt the conversation starts on, or null for the first prompt in the bundle.</param>
/// <param name="Transcript">The line to evaluate once and exit, or null for the text loop.</param>
/// <param name="ShowHelp">True when the usage text was asked for.</param>
/// <param name="GrammarStringsOut">The file to write the game's Spanish strings to for the grammar check, or null to run the host as usual.</param>
/// <param name="ValidateContent">True when the host only checks the game's content files against each other (<c>--validate-content</c>) and exits.</param>
/// <param name="Bag">The bag a sale or a purchase is judged against (<c>--bag wood:4,coin:2</c>, item names as the noun catalogue keys them), or null: the host then says the NPC's line as before, whatever the learner holds.</param>
public sealed record HostOptions(string ContentDirectory, string? PromptId, string? Transcript, bool ShowHelp, string? GrammarStringsOut = null, bool ValidateContent = false, IReadOnlyList<BagEntry>? Bag = null)
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
          --bag <item:count,...>  the bag a sale or a purchase is judged against, e.g. wood:4,coin:2 (default: none; the NPC then says its line whatever the learner holds)
          --validate-content      load every game file of the bundle and check them against each other, then exit (1 on a problem)
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
        var validateContent = false;
        IReadOnlyList<BagEntry>? bag = null;
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
                case "--bag":
                    bag = ParseBag(Value(args, ref i));
                    break;
                case "--validate-content":
                    validateContent = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }

        if (transcript is not null && promptId is null)
        {
            throw new ArgumentException("--transcript needs --prompt, the prompt to evaluate the line on.");
        }

        return new HostOptions(contentDirectory, promptId, transcript, showHelp, grammarStringsOut, validateContent, bag);
    }

    // "wood:4,coin:2" (an empty text is an empty bag): each entry an item name and a count of zero or more.
    private static List<BagEntry> ParseBag(string text)
    {
        var entries = new List<BagEntry>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split(':');
            if (pieces.Length != 2 || pieces[0].Length == 0 || !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                throw new ArgumentException($"--bag wants entries like wood:4 (an item and a count), not '{part}'.");
            }

            entries.Add(new BagEntry(pieces[0], count));
        }

        return entries;
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

/// <summary>One entry of <c>--bag</c>: an item by name and how many the learner holds.</summary>
/// <param name="Item">The item's name (<c>wood</c>, <c>coin</c>, <c>apple</c>).</param>
/// <param name="Count">How many.</param>
public sealed record BagEntry(string Item, int Count);
