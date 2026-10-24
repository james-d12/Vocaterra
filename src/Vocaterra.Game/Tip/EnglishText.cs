using System.Globalization;
using System.Text;
using System.Text.Json;
using Vocaterra.Game.Language.Learner;
using Vocaterra.Game.Language.Phrases;
using Vocaterra.Game.Language.Words;
using Vocaterra.Game.Npc;
using Vocaterra.Game.Session;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Taxonomy;

namespace Vocaterra.Game.Tip;

/// <summary>One line of the controls help: the keys and what they do (<c>game/english.json</c>).</summary>
/// <param name="Id">A stable id.</param>
/// <param name="Keys">The keys or buttons, as the screen shows them.</param>
/// <param name="Text">What they do, in English.</param>
public sealed record ControlHelp(string Id, string Keys, string Text);

/// <summary>A first-time tip: the English text shown once, when something happens for the first time.</summary>
/// <param name="Id">A stable id (the save keeps which tips were shown).</param>
/// <param name="When">The trigger: <c>start</c>, <c>aimed_npc</c> or the name of a world event (<see cref="WorldEvents"/>).</param>
/// <param name="Text">The tip, in English.</param>
/// <param name="DelaySeconds">How many seconds of game time after its trigger the tip waits before it may come up (S17: the opening tips are paced so they do not stack up); 0 for none.</param>
public sealed record TipSpec(string Id, string When, string Text, int DelaySeconds = 0);

/// <summary>What a piece of the bilingual objective line is, so the HUD can colour it.</summary>
public enum ObjectivePieceKind
{
    /// <summary>The checklist's title (<c>Campamento</c>).</summary>
    Title,

    /// <summary>A Spanish word of a step (<c>madera</c>).</summary>
    Word,

    /// <summary>An English word in brackets (<c>(wood)</c>), dropped once the noun is known.</summary>
    English,

    /// <summary>The count of a step (<c>0/5</c>).</summary>
    Count,

    /// <summary>The separator between steps and any plain text.</summary>
    Plain,
}

/// <summary>One piece of the objective line: its text, what it is, and whether its step is done.</summary>
/// <param name="Text">The text, including any space before it.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Done">Whether the step it belongs to is done.</param>
public sealed record ObjectivePiece(string Text, ObjectivePieceKind Kind, bool Done = false);

/// <summary>
/// The game's English text (S16, D-game-13), read from the bundle's host file <c>game/english.json</c>: the controls, the first-time tips, the bilingual
/// objective line, the English under the NPCs' Spanish lines, the short English explanation of each phrase, the small interface words, and the plain-English
/// feedback after a turn keyed by the core's error codes and by the outcome of the turn. It is content: the game's words live in <c>content/game-v1</c>, the core is
/// not changed and keeps no English. Only Spanish is spoken; every word here is drawn, never voiced.
/// </summary>
/// <remarks>
/// <para>
/// <b>Feedback.</b> <see cref="FeedbackFor(GuideTurn)"/> turns a finished turn into one to three English lines: the outcome (understood, understood with a mistake, not
/// understood, an uncertain recognition, "no sé", help, a choice, a fail-forward, a pointed arrow) and, for a turn that was understood with mistakes, one line per
/// distinct error code (at most two). An error code the file has no line for gets the fallback line, so a new core code never leaves the player with nothing.
/// An uncertain recognition is said to be the microphone's trouble, never the language's (D-game-9).
/// </para>
/// <para>
/// <b>Objectives.</b> <see cref="ObjectivePieces"/> writes the checklist as <c>Campamento (camp): madera (wood) 0/5 · ...</c>; the English of a step goes once its
/// noun is known (D-game-6's rule: three spaced correct uses), and the title's once every step's has gone.
/// </para>
/// <para>Plain values, no clock, no random numbers; every comparison is ordinal.</para>
/// </remarks>
public sealed class EnglishText
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/english.json";

    /// <summary>The feedback outcome keys the file must have a line for.</summary>
    public static IReadOnlyList<string> OutcomeKeys { get; } =
    [
        "understood", "understood_with_mistake", "not_understood", "uncertain", "dont_know", "help", "choice", "fail_forward", "pointed",
    ];

    /// <summary>The interface words the file must have.</summary>
    public static IReadOnlyList<string> UiKeys { get; } =
    [
        "english", "lesson", "stepOf", "continue", "skip", "help", "startLesson", "lessonDone", "retry", "helpDefault", "recapTitle", "controlsTitle", "controlsClose",
        "tipNext", "controlsHint", "stepExplain", "stepModel", "stepListen", "stepRepeat", "stepAnswer", "stepDo", "stepRecap", "outcomeDone", "outcomeSkipped", "outcomeMovedOn",
        "objectiveFindGuide", "objectiveLightFire", "objectiveComplete", "spareTorch", "sessionCompleteTitle", "sessionCompleteLine", "sessionCompleteKeys",
        "btnRepeat", "btnSlower", "btnHelp", "btnDontKnow", "btnType",
        "summaryTitle", "summaryLessons", "summaryLessonDone", "summaryLessonAdvanced", "summaryWords", "summaryUnaided", "summaryForms", "summaryPractise",
        "summaryPractiseNoSe", "summaryPractiseSlip", "summaryPractiseSupport", "summaryPractiseNone", "summaryReplay", "summaryNone", "summaryMore",
        "tensePres", "tensePret", "tenseImpf", "tenseFut", "person1", "person2", "person3", "singular", "plural", "retryAgain",
    ];

    /// <summary>The most error lines one turn's feedback carries.</summary>
    public const int MaxErrorLines = 2;

    private readonly Dictionary<string, string> _ui;
    private readonly Dictionary<string, string> _npcNames;
    private readonly Dictionary<string, string> _nouns;
    private readonly Dictionary<string, string> _phrases;
    private readonly Dictionary<string, string> _lines;
    private readonly Dictionary<string, string> _outcomes;
    private readonly Dictionary<string, string> _errors;
    private readonly string _unknownError;
    private readonly Dictionary<string, (string? Noun, string English)> _objective;
    private readonly (string? Noun, string English) _objectiveTitle;

    private EnglishText(
        Dictionary<string, string> ui,
        Dictionary<string, string> npcNames,
        IReadOnlyList<ControlHelp> controls,
        IReadOnlyList<TipSpec> tips,
        (string? Noun, string English) objectiveTitle,
        Dictionary<string, (string? Noun, string English)> objective,
        Dictionary<string, string> nouns,
        Dictionary<string, string> phrases,
        Dictionary<string, string> lines,
        Dictionary<string, string> outcomes,
        Dictionary<string, string> errors,
        string unknownError)
    {
        _ui = ui;
        _npcNames = npcNames;
        Controls = controls;
        Tips = tips;
        _objectiveTitle = objectiveTitle;
        _objective = objective;
        _nouns = nouns;
        _phrases = phrases;
        _lines = lines;
        _outcomes = outcomes;
        _errors = errors;
        _unknownError = unknownError;
    }

    /// <summary>The controls help, in the file's order.</summary>
    public IReadOnlyList<ControlHelp> Controls { get; }

    /// <summary>The first-time tips, in the file's order.</summary>
    public IReadOnlyList<TipSpec> Tips { get; }

    /// <summary>Reads the English text from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <param name="sheet">The phrase sheet the explanations are checked against, or <see langword="null"/> to skip that check.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static EnglishText Load(ContentBundle bundle, PhraseSheet? sheet = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (!bundle.TryReadHostFile(RelativePath, out var bytes))
        {
            throw new InvalidDataException($"The bundle has no host file {RelativePath}: it is missing from the manifest, or the bundle was loaded without hash verification.");
        }

        return Parse(Encoding.UTF8.GetString(bytes.Span), sheet);
    }

    /// <summary>Builds the English text from the file's text, checking that it holds together.</summary>
    /// <param name="json">The contents of <c>game/english.json</c>.</param>
    /// <param name="sheet">The phrase sheet the explanations are checked against (each explained id must be a row), or <see langword="null"/> to skip that check.</param>
    /// <exception cref="InvalidDataException">The text is malformed, lacks a required entry, or names a phrase that is not in the sheet.</exception>
    public static EnglishText Parse(string json, PhraseSheet? sheet = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion 1.");
            }

            var ui = Map(root, "ui");
            Require(ui, UiKeys, "ui");
            var npcNames = Map(root, "npcNames");
            var controls = new List<ControlHelp>();
            foreach (var element in root.GetProperty("controls").EnumerateArray())
            {
                controls.Add(new ControlHelp(Text(element, "id"), Text(element, "keys"), Text(element, "text")));
            }

            RequireUnique(controls.Select(static c => c.Id), "controls");
            var tips = new List<TipSpec>();
            foreach (var element in root.GetProperty("tips").EnumerateArray())
            {
                var delay = element.TryGetProperty("delay", out var delayElement) ? delayElement.GetInt32() : 0;
                if (delay is < 0 or > 300)
                {
                    throw new InvalidDataException($"{RelativePath}: the tip '{Text(element, "id")}' waits {delay.ToString(CultureInfo.InvariantCulture)} seconds; it must be 0 to 300.");
                }

                var tip = new TipSpec(Text(element, "id"), Text(element, "when"), Text(element, "text"), delay);
                if (!TipTriggers.Contains(tip.When, StringComparer.Ordinal))
                {
                    throw new InvalidDataException($"{RelativePath}: the tip '{tip.Id}' has the trigger '{tip.When}', which is neither 'start', 'aimed_npc' nor a world event.");
                }

                tips.Add(tip);
            }

            RequireUnique(tips.Select(static t => t.Id), "tips");
            var objective = root.GetProperty("objective");
            var title = ObjectiveEntry(objective.GetProperty("title"));
            var steps = new Dictionary<string, (string? Noun, string English)>(StringComparer.Ordinal);
            foreach (var property in objective.GetProperty("steps").EnumerateObject())
            {
                steps[property.Name] = ObjectiveEntry(property.Value);
            }

            foreach (var step in Objective.Camp().Steps)
            {
                if (!steps.ContainsKey(step.Key))
                {
                    throw new InvalidDataException($"{RelativePath}: the objective has no English for the step '{step.Key}'.");
                }
            }

            var nouns = Map(root, "nouns");
            var phrases = Map(root, "phrases");
            if (sheet is not null)
            {
                foreach (var id in phrases.Keys)
                {
                    if (sheet.Find(id) is null)
                    {
                        throw new InvalidDataException($"{RelativePath}: the explanation names the phrase '{id}', which is not in the phrase sheet.");
                    }
                }
            }

            var lines = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var element in root.GetProperty("lines").EnumerateArray())
            {
                // Lines are matched without case, accents or punctuation ("De nada." and "¡De nada!" are one line); two spellings must agree.
                var key = PhraseSheet.Plain(Text(element, "spanish"));
                var english = Text(element, "english");
                if (lines.TryGetValue(key, out var earlier) && !string.Equals(earlier, english, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"{RelativePath}: the line '{Text(element, "spanish")}' has two different English texts.");
                }

                lines[key] = english;
            }

            var feedback = root.GetProperty("feedback");
            var outcomes = Map(feedback, "outcomes");
            Require(outcomes, OutcomeKeys, "feedback.outcomes");
            var errors = Map(feedback, "errors");
            var unknownError = Text(feedback, "unknownError");
            return new EnglishText(ui, npcNames, controls, tips, title, steps, nouns, phrases, lines, outcomes, errors, unknownError);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>The triggers a tip may name: <c>start</c>, <c>aimed_npc</c> and every world event.</summary>
    public static IReadOnlyList<string> TipTriggers { get; } = ["start", "aimed_npc", .. WorldEvents.All];

    /// <summary>An interface word (<c>continue</c>, <c>skip</c>...).</summary>
    /// <param name="key">One of <see cref="UiKeys"/>.</param>
    /// <exception cref="KeyNotFoundException">The key is not one of the file's.</exception>
    public string Ui(string key) => _ui[key];

    /// <summary>The interface word with <c>{name}</c> placeholders filled in.</summary>
    /// <param name="key">One of <see cref="UiKeys"/>.</param>
    /// <param name="values">The placeholder values as name/value pairs.</param>
    public string Ui(string key, params (string Name, string Value)[] values) => Fill(_ui[key], values);

    /// <summary>
    /// The English of a speech button or of the hold-to-talk hint (S17), written after the Spanish label: <c>repeat</c> for <c>Repetir</c>.
    /// </summary>
    /// <param name="button">One of <see cref="SpeechButtonKeys.All"/>.</param>
    public string ButtonEnglish(string button) => _ui["btn" + char.ToUpperInvariant(button[0]) + button[1..]];

    /// <summary>
    /// The line the objective area shows at a stage that has one instead of the checklist (S17): <c>Go down the trail and find the guide</c> before the guide is met,
    /// <c>Go back up the trail and light your own fire</c> once the checklist is complete, and the closing line once the fire is lit; <see langword="null"/> while the
    /// checklist is the objective.
    /// </summary>
    /// <param name="stage">The session's stage.</param>
    public string? StageLine(SessionStage stage) => stage switch
    {
        SessionStage.FindGuide => _ui["objectiveFindGuide"],
        SessionStage.LightFire => _ui["objectiveLightFire"],
        SessionStage.Complete => _ui["objectiveComplete"],
        _ => null,
    };

    /// <summary>The English explanation of a phrase, or <see langword="null"/> when the file has none.</summary>
    /// <param name="phraseId">The phrase sheet's id.</param>
    public string? Explanation(string phraseId) => _phrases.TryGetValue(phraseId, out var text) ? text : null;

    /// <summary>The English of an NPC's Spanish line (compared without case, accents or punctuation), or <see langword="null"/> when the file has none.</summary>
    /// <param name="spanish">The line as the panel shows it.</param>
    public string? ForLine(string spanish)
    {
        ArgumentNullException.ThrowIfNull(spanish);
        return _lines.TryGetValue(PhraseSheet.Plain(spanish), out var english) ? english : null;
    }

    /// <summary>The Spanish keys (plain form) of every line the file glosses, for the tests that check the bundle's lines are covered.</summary>
    public IReadOnlyCollection<string> GlossedLines => _lines.Keys;

    /// <summary>The English word of a catalogue noun (<c>fruta</c> gives <c>fruit</c>), or <see langword="null"/> when the file has none.</summary>
    /// <param name="lemma">The noun.</param>
    public string? NounEnglish(string lemma) => _nouns.TryGetValue(lemma, out var english) ? english : null;

    /// <summary>The name of an NPC for the feedback (<c>The guide</c>); the agent's id when the file has none.</summary>
    /// <param name="npc">The agent id.</param>
    public string NpcName(string npc) => _npcNames.TryGetValue(npc, out var name) ? name : npc;

    // ---- The objective line.

    /// <summary>
    /// The camp checklist as coloured pieces: <c>Campamento (camp): madera (wood) 0/5 · frutas (fruit) 0/3 · ...</c>. The English of a step is left out once its noun
    /// is known (<paramref name="isKnown"/>), and the title's once every step's is.
    /// </summary>
    /// <param name="objective">The checklist.</param>
    /// <param name="isKnown">Whether a noun (<c>madera</c>) counts as known (D-game-6).</param>
    public IReadOnlyList<ObjectivePiece> ObjectivePieces(Objective objective, Func<string, bool> isKnown)
    {
        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(isKnown);
        var pieces = new List<ObjectivePiece>();
        var allKnown = objective.Steps.All(step => StepKnown(step.Key, isKnown));
        pieces.Add(new ObjectivePiece(Objective.Title, ObjectivePieceKind.Title));
        if (!allKnown)
        {
            pieces.Add(new ObjectivePiece(" (" + _objectiveTitle.English + ")", ObjectivePieceKind.English));
        }

        pieces.Add(new ObjectivePiece(": ", ObjectivePieceKind.Plain));
        for (var i = 0; i < objective.Steps.Count; i++)
        {
            var step = objective.Steps[i];
            if (i > 0)
            {
                pieces.Add(new ObjectivePiece(" · ", ObjectivePieceKind.Plain));
            }

            pieces.Add(new ObjectivePiece(step.Label, ObjectivePieceKind.Word, step.Done));
            if (!StepKnown(step.Key, isKnown) && _objective.TryGetValue(step.Key, out var entry))
            {
                pieces.Add(new ObjectivePiece(" (" + entry.English + ")", ObjectivePieceKind.English, step.Done));
            }

            pieces.Add(new ObjectivePiece(string.Create(CultureInfo.InvariantCulture, $" {Math.Min(step.Count, step.Target)}/{step.Target}"), ObjectivePieceKind.Count, step.Done));
        }

        return pieces;
    }

    /// <summary>The objective line as one string (see <see cref="ObjectivePieces"/>).</summary>
    /// <param name="objective">The checklist.</param>
    /// <param name="isKnown">Whether a noun counts as known.</param>
    public string ObjectiveLine(Objective objective, Func<string, bool> isKnown) =>
        string.Concat(ObjectivePieces(objective, isKnown).Select(static p => p.Text));

    private bool StepKnown(string key, Func<string, bool> isKnown) =>
        _objective.TryGetValue(key, out var entry) && entry.Noun is { } noun && isKnown(noun);

    // ---- Feedback after a turn.

    /// <summary>
    /// The plain-English feedback for a finished turn (see the type's remarks). Never empty for a turn: every outcome has a line.
    /// </summary>
    /// <param name="turn">The turn.</param>
    public IReadOnlyList<string> FeedbackFor(GuideTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        var npc = NpcName(turn.Npc);
        var lines = new List<string>();
        if (turn.Uncertain && turn.Input == InputKind.Spoken)
        {
            // A spoken line the NPC did not understand, or the recognizer was not sure of: the microphone's trouble before the language's (D-game-9).
            lines.Add(Fill(_outcomes["uncertain"], ("npc", npc)));
            return lines;
        }

        if (!turn.Communicated)
        {
            lines.Add(Fill(_outcomes["not_understood"], ("npc", npc)));
            return lines;
        }

        var isHelp = string.Equals(turn.Intent, "ask_help", StringComparison.Ordinal);
        var isDontKnow = string.Equals(turn.Intent, "dont_know", StringComparison.Ordinal);
        if (isDontKnow || isHelp)
        {
            lines.Add(Fill(_outcomes[isDontKnow ? "dont_know" : "help"], ("npc", npc)));
        }
        else if (turn.Choice)
        {
            lines.Add(Fill(_outcomes["choice"], ("npc", npc)));
        }
        else if (turn.Errors.Count == 0)
        {
            lines.Add(Fill(_outcomes["understood"], ("npc", npc)));
        }
        else
        {
            lines.Add(Fill(_outcomes["understood_with_mistake"], ("npc", npc)));
            foreach (var error in turn.Errors.DistinctBy(static e => e.Tag, StringComparer.Ordinal).Take(MaxErrorLines))
            {
                lines.Add(ErrorLine(error));
            }
        }

        if (turn.FailForward)
        {
            lines.Add(Fill(_outcomes["fail_forward"], ("npc", npc)));
        }

        if (turn.Pointer is { } pointer)
        {
            var noun = NounEnglish(pointer.Place) ?? pointer.Place;
            lines.Add(Fill(_outcomes["pointed"], ("npc", npc), ("noun", noun), ("blocks", pointer.Blocks.ToString(CultureInfo.InvariantCulture))));
        }

        return lines;
    }

    /// <summary>
    /// The plain-English feedback for a line the learner gave, as the book's "What I said" page shows it (S20): the same outcome lines as
    /// <see cref="FeedbackFor(GuideTurn)"/>, read from the history entry (its core verdict and errors; a held or unsure line says so).
    /// </summary>
    /// <param name="entry">The history entry.</param>
    public IReadOnlyList<string> FeedbackFor(InputEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var npc = NpcName(entry.Npc ?? string.Empty);
        if (entry.Held)
        {
            return [Fill(_outcomes["uncertain"], ("npc", npc))];
        }

        if (!entry.Communicated)
        {
            return [Fill(_outcomes["not_understood"], ("npc", npc))];
        }

        if (string.Equals(entry.Intent, "dont_know", StringComparison.Ordinal))
        {
            return [Fill(_outcomes["dont_know"], ("npc", npc))];
        }

        if (string.Equals(entry.Intent, "ask_help", StringComparison.Ordinal))
        {
            return [Fill(_outcomes["help"], ("npc", npc))];
        }

        if (entry.Errors.Count == 0)
        {
            return [Fill(_outcomes["understood"], ("npc", npc))];
        }

        var lines = new List<string> { Fill(_outcomes["understood_with_mistake"], ("npc", npc)) };
        foreach (var error in entry.Errors.DistinctBy(static e => e.Tag, StringComparer.Ordinal).Take(MaxErrorLines))
        {
            lines.Add(ErrorLine(error));
        }

        return lines;
    }

    /// <summary>The English line for one error of the core: the file's line for its code, or the fallback line for a code it does not know (or a line that needs a form the core did not give).</summary>
    /// <param name="error">The error.</param>
    public string ErrorLine(GuideError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var values = new[] { ("expected", error.Expected), ("actual", error.Actual), ("tag", error.Tag) };
        if (_errors.TryGetValue(error.Tag, out var template) && !NeedsMissingForm(template, error))
        {
            return Fill(template, values);
        }

        return Fill(_unknownError, values);
    }

    /// <summary>Whether the file has its own line for an error code (as opposed to the fallback).</summary>
    /// <param name="tag">The core's error code.</param>
    public bool HasErrorLine(string tag) => _errors.ContainsKey(tag);

    private static bool NeedsMissingForm(string template, GuideError error) =>
        (template.Contains("{expected}", StringComparison.Ordinal) && error.Expected.Length == 0)
        || (template.Contains("{actual}", StringComparison.Ordinal) && error.Actual.Length == 0);

    // ---- Helpers.

    /// <summary>The error codes of the core the file has its own line for must be all of them: the tests use this to keep the file in step with <see cref="ErrorTags"/>.</summary>
    public IReadOnlyList<string> MissingErrorTags() => ErrorTags.All.Where(tag => !_errors.ContainsKey(tag)).ToList();

    private static string Fill(string template, params (string Name, string Value)[] values)
    {
        var text = template;
        foreach (var (name, value) in values)
        {
            text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }

        return text;
    }

    private static (string? Noun, string English) ObjectiveEntry(JsonElement element)
    {
        var english = Text(element, "english");
        var noun = element.TryGetProperty("noun", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return (noun, english);
    }

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"{RelativePath}: the text '{name}' is missing or empty{(element.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? $" (in '{id.GetString()}')" : string.Empty)}.");
        }

        return value.GetString()!;
    }

    private static Dictionary<string, string> Map(JsonElement parent, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in parent.GetProperty(name).EnumerateObject())
        {
            var text = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidDataException($"{RelativePath}: {name}.{property.Name} must not be empty.");
            }

            map[property.Name] = text;
        }

        return map;
    }

    private static void Require(Dictionary<string, string> map, IReadOnlyList<string> keys, string section)
    {
        foreach (var key in keys)
        {
            if (!map.ContainsKey(key))
            {
                throw new InvalidDataException($"{RelativePath}: {section} has no '{key}'.");
            }
        }
    }

    private static void RequireUnique(IEnumerable<string> ids, string section)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (!seen.Add(id))
            {
                throw new InvalidDataException($"{RelativePath}: {section} has the id '{id}' twice.");
            }
        }
    }
}
