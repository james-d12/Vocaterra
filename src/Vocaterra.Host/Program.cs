// The Vocaterra host: a console host on the Vocaterra.Nlp packages. It loads the game's content bundle, builds the services with the core's
// scripted recognizer (no model files and no audio), then either evaluates one line and exits (--transcript) or runs the text loop.
// Exit codes: 0 success, 1 the bundle is invalid, 2 the command line is wrong.
using System.Globalization;
using System.Text;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.World.Layout;
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
    bundle = GameLanguages.Load(contentDirectory);
}
catch (Exception ex) when (ex is ContentValidationException or IOException or UnauthorizedAccessException or JsonException)
{
    Console.Error.WriteLine(ex.Message);
    return ExitInvalidContent;
}

if (options.ValidateContent || options.GrammarStringsOut is not null)
{
    // The game's content (content-plan C02): every game file read once from the verified bundle and checked against the others. The ContentBuilder
    // cannot run this check (it references only the core), so the host does.
    GameData data;
    LocaleBundle locales;
    try
    {
        // The support language comes from the locale bundle beside the game's (content-plan C03); every shipped locale is checked, the default one loaded.
        locales = LocaleBundle.Load(LocaleBundle.DirectoryBeside(contentDirectory));
        data = GameData.Load(bundle, locales.Text(LocaleBundle.ChooseCode(null, bundle)));
        foreach (var other in locales.Locales.Where(locale => !string.Equals(locale.Code, data.Locale.Code, StringComparison.Ordinal)))
        {
            GameData.Load(bundle, locales.Text(other.Code));
        }
    }
    catch (Exception ex) when (ex is InvalidDataException or SceneLayoutException or JsonException or DirectoryNotFoundException or ArgumentException)
    {
        Console.Error.WriteLine(ex.Message);
        return ExitInvalidContent;
    }

    if (options.GrammarStringsOut is { } stringsOut)
    {
        // The grammar check's strings (scripts/content.sh grammar): written from the bundle and the game's files, then the host exits.
        var written = GrammarStringsExtractor.Write(data, contentDirectory, stringsOut);
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"grammar strings: {written.Count} written to {stringsOut} ({string.Join(", ", written.GroupBy(static line => line.Kind).OrderBy(static group => group.Key, StringComparer.Ordinal).Select(static group => group.Key + " " + group.Count()))})"));
        return ExitOk;
    }

    Console.WriteLine(string.Create(
        CultureInfo.InvariantCulture,
        $"content: {bundle.Manifest.BundleId} {bundle.ContentHash[..12]} and locale {locales.BundleHash[..12]} ({string.Join(", ", locales.Locales.Select(static l => l.Code))}): the game files hold together ({data.Nouns.Nouns.Count} nouns, {data.Phrases.Rows.Count} phrases, {data.Lessons.Lessons.Count} lessons, {data.Checks.Count} check items, {data.Speech.Count} speech lines, {data.Locale.Ids.Count} strings)"));
    return ExitOk;
}

// The NPCs' names, so that a line naming its listener (Luz, quiero pan) is read as the game reads it (V14).
var names = NpcRoster.Load(bundle).All.Where(static npc => npc.Name is not null).ToDictionary(static npc => npc.Id, static npc => npc.Name!, StringComparer.Ordinal);
Inventory? bag = null;
if (options.Bag is { } entries)
{
    bag = new Inventory();
    foreach (var entry in entries)
    {
        if (ItemDefinition.All.FirstOrDefault(item => string.Equals(item.Name, entry.Item, StringComparison.Ordinal)) is not { } definition)
        {
            Console.Error.WriteLine($"There is no item '{entry.Item}'. The items are: {string.Join(", ", ItemDefinition.All.Select(static item => item.Name))}.");
            return ExitUsage;
        }

        if (entry.Count > 0 && bag.Add(definition.Id, entry.Count) > 0)
        {
            Console.Error.WriteLine($"The bag has no room for {entry.Count} {entry.Item}.");
            return ExitUsage;
        }
    }
}

var session = new ConversationSession(bundle, listenerNames: names, bag: bag);
if (options.Transcript is { } transcript && options.PromptId is { } oneShotPrompt)
{
    if (!session.HasPrompt(oneShotPrompt))
    {
        Console.Error.WriteLine($"There is no prompt '{oneShotPrompt}'. The prompts are: {string.Join(", ", session.PromptIds)}.");
        return ExitUsage;
    }

    VerdictReport.Write(Console.Out, oneShotPrompt, transcript, session.Evaluate(transcript, oneShotPrompt, oneShotTime), bundle.Language.Orthography, session.Words, session.LastExchange, names.Values);
    return ExitOk;
}

if (options.PromptId is { } startPrompt && !session.HasPrompt(startPrompt))
{
    Console.Error.WriteLine($"There is no prompt '{startPrompt}'. The prompts are: {string.Join(", ", session.PromptIds)}.");
    return ExitUsage;
}

return RunLoop(session, options.PromptId ?? session.PromptIds[0], names.Values);

// The text loop: show the current prompt, read a line, evaluate it and print the outcome. The session carries the agent states, the learner
// model and the repeat counts from one line to the next; /prompt <id> changes the prompt and /quit leaves. A correction line spells the NPCs' names as the game shows it.
static int RunLoop(ConversationSession session, string promptId, IEnumerable<string> names)
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

        VerdictReport.Write(Console.Out, promptId, text, session.Evaluate(text, promptId, Now()), bundle.Language.Orthography, session.Words, session.LastExchange, names);
        Console.WriteLine();
    }
}

static StoredTimestamp Now()
{
    return new StoredTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
}
