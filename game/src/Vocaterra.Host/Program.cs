// The Vocaterra host: a console host on the Vocaterra.Nlp packages. It loads the game's content bundle, builds the services with the core's
// scripted recognizer (no model files and no audio), then either evaluates one line and exits (--transcript) or runs the text loop.
// Exit codes: 0 success, 1 the bundle is invalid, 2 the command line is wrong.
using System.Globalization;
using System.Text;
using System.Text.Json;
using Vocaterra.Game;
using Vocaterra.Host;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;

const int ExitOk = 0;
const int ExitInvalidContent = 1;
const int ExitUsage = 2;

// A one-shot run uses a fixed stored time, so its output does not change from run to run; the text loop reads the clock.
var oneShotTime = new StoredTimestamp(1_760_000_000);

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

HostOptions options;
try
{
    options = HostOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(HostOptions.Usage);
    return ExitUsage;
}

if (options.ShowHelp)
{
    Console.WriteLine(HostOptions.Usage);
    return ExitOk;
}

ContentBundle bundle;
// The default is relative to the current directory; when it is not there (the host is run from the repository root, say), the game's
// bundle is searched for above the executable and the current directory, as the front-end does.
var contentDirectory = options.ContentDirectory;
if (contentDirectory == HostOptions.DefaultContentDirectory && !Directory.Exists(contentDirectory))
{
    contentDirectory = GameBundle.ResolveDirectory();
}

try
{
    bundle = ContentLoader.Load(contentDirectory);
}
catch (Exception ex) when (ex is ContentValidationException or IOException or UnauthorizedAccessException or JsonException)
{
    Console.Error.WriteLine(ex.Message);
    return ExitInvalidContent;
}

if (options.GrammarStringsOut is { } stringsOut)
{
    // The grammar check's strings (scripts/content.sh grammar): written from the bundle and the game's sheets, then the host exits.
    var written = GrammarStringsExtractor.Write(bundle, contentDirectory, stringsOut);
    Console.WriteLine(string.Create(
        CultureInfo.InvariantCulture,
        $"grammar strings: {written.Count} written to {stringsOut} ({string.Join(", ", written.GroupBy(static line => line.Kind).OrderBy(static group => group.Key, StringComparer.Ordinal).Select(static group => group.Key + " " + group.Count()))})"));
    return ExitOk;
}

var session = new ConversationSession(bundle);
if (options.Transcript is { } transcript && options.PromptId is { } oneShotPrompt)
{
    if (!session.HasPrompt(oneShotPrompt))
    {
        Console.Error.WriteLine($"There is no prompt '{oneShotPrompt}'. The prompts are: {string.Join(", ", session.PromptIds)}.");
        return ExitUsage;
    }

    VerdictReport.Write(Console.Out, oneShotPrompt, transcript, session.Evaluate(transcript, oneShotPrompt, oneShotTime));
    return ExitOk;
}

if (options.PromptId is { } startPrompt && !session.HasPrompt(startPrompt))
{
    Console.Error.WriteLine($"There is no prompt '{startPrompt}'. The prompts are: {string.Join(", ", session.PromptIds)}.");
    return ExitUsage;
}

return RunLoop(session, options.PromptId ?? session.PromptIds[0]);

// The text loop: show the current prompt, read a line, evaluate it and print the outcome. The session carries the agent states, the learner
// model and the repeat counts from one line to the next; /prompt <id> changes the prompt and /quit leaves.
static int RunLoop(ConversationSession session, string promptId)
{
    var bundle = session.Bundle;
    Console.WriteLine(string.Create(
        CultureInfo.InvariantCulture,
        $"Vocaterra host: bundle {bundle.Manifest.BundleId} {bundle.Manifest.ContentVersion}, hash {bundle.ContentHash}"));
    Console.WriteLine($"Prompts: {string.Join(", ", session.PromptIds)}");
    Console.WriteLine("Type a Spanish line and press Enter. /prompt <id> switches prompt, /quit leaves.");

    while (true)
    {
        VerdictReport.WritePrompt(Console.Out, session.Prompt(promptId));
        Console.Write("> ");
        var line = Console.In.ReadLine();
        if (line is null)
        {
            return ExitOk;
        }

        var text = line.Trim();
        if (text.Length == 0)
        {
            continue;
        }

        if (text == "/quit")
        {
            return ExitOk;
        }

        if (text.StartsWith('/'))
        {
            var command = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (command is ["/prompt", var id] && session.HasPrompt(id))
            {
                promptId = id;
            }
            else if (command is ["/prompt", var missing])
            {
                Console.WriteLine($"There is no prompt '{missing}'. The prompts are: {string.Join(", ", session.PromptIds)}.");
            }
            else
            {
                Console.WriteLine("Commands: /prompt <id> switches prompt, /quit leaves.");
            }

            continue;
        }

        VerdictReport.Write(Console.Out, promptId, text, session.Evaluate(text, promptId, Now()));
        Console.WriteLine();
    }
}

static StoredTimestamp Now()
{
    return new StoredTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
}
