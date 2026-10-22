using System.Globalization;
using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Prompts;

namespace Vocaterra.Game;

/// <summary>The kinds of lesson step (S16, <c>docs/SCENARIO_PLAN.md</c> section 8).</summary>
public enum LessonStepType
{
    /// <summary><c>explain</c>: English text only; the learner reads it and continues.</summary>
    Explain,

    /// <summary><c>model</c>: the NPC says a Spanish line (its pre-rendered WAV plays) with its meaning under it; the learner continues.</summary>
    Model,

    /// <summary><c>listen</c>: the NPC says a line and the learner picks what it means among a few English options.</summary>
    Listen,

    /// <summary><c>repeat</c>: the learner says a phrase with its card and audio up: a supported use.</summary>
    Repeat,

    /// <summary><c>answer</c>: the NPC says a line and the learner answers it with a phrase, with no card: a real turn through the core at a bundle prompt.</summary>
    Answer,

    /// <summary><c>do</c>: the learner does something in the world (picks something up); the step waits on the world event the game raises for it.</summary>
    Do,

    /// <summary><c>recap</c>: English text and the phrases practised so far; the learner continues.</summary>
    Recap,
}

/// <summary>The wire names of the lesson step types, as <c>game/lessons.json</c> and the session log spell them.</summary>
public static class LessonStepTypes
{
    /// <summary>The wire name of a step type: <c>explain</c>, <c>model</c>, <c>listen</c>, <c>repeat</c>, <c>answer</c>, <c>do</c> or <c>recap</c>.</summary>
    /// <param name="type">The type.</param>
    public static string Of(LessonStepType type) => type switch
    {
        LessonStepType.Explain => "explain",
        LessonStepType.Model => "model",
        LessonStepType.Listen => "listen",
        LessonStepType.Repeat => "repeat",
        LessonStepType.Answer => "answer",
        LessonStepType.Do => "do",
        LessonStepType.Recap => "recap",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown lesson step type."),
    };

    /// <summary>The step type a wire name stands for, or <see langword="null"/>.</summary>
    /// <param name="name">The wire name.</param>
    public static LessonStepType? Parse(string name) => name switch
    {
        "explain" => LessonStepType.Explain,
        "model" => LessonStepType.Model,
        "listen" => LessonStepType.Listen,
        "repeat" => LessonStepType.Repeat,
        "answer" => LessonStepType.Answer,
        "do" => LessonStepType.Do,
        "recap" => LessonStepType.Recap,
        _ => null,
    };
}

/// <summary>
/// One step of a lesson, as <c>game/lessons.json</c> gives it. Which fields a step uses depends on its type (<see cref="LessonBook"/> lists them); the others
/// are empty. Every step has a skip and a help path whatever its type: <see cref="Help"/> is its English hint (a default is used when there is none).
/// </summary>
/// <param name="Id">The step's id, unique in its lesson.</param>
/// <param name="Type">What the step asks.</param>
/// <param name="English">The English text shown for the step: the explanation, the instruction or the situation.</param>
/// <param name="Help">The English hint shown by the help path, or <see langword="null"/> for the default.</param>
/// <param name="Retry">The English line shown after an attempt that did not work, or <see langword="null"/> for the default.</param>
/// <param name="Line">For <c>model</c>, <c>listen</c> and <c>answer</c>: the pre-rendered line the NPC says (its speech id, speaker and Spanish text).</param>
/// <param name="Translation">For a step with a <see cref="Line"/>: what the line means, in English.</param>
/// <param name="Phrase">For <c>repeat</c> and <c>answer</c>: the phrase sheet's id of the phrase the learner says.</param>
/// <param name="Prompt">For <c>repeat</c> and <c>answer</c>: the bundle prompt the turn is evaluated at.</param>
/// <param name="Options">For <c>listen</c>: the English options, in the order shown.</param>
/// <param name="Correct">For <c>listen</c>: the index of the right option.</param>
/// <param name="Event">For <c>do</c>: the world event the step waits for (<see cref="WorldEvents"/>).</param>
/// <param name="Detail">For <c>do</c>: a text the event's detail must contain (ignoring case), or <see langword="null"/> for any.</param>
/// <param name="Phrases">For <c>recap</c>: the phrases it lists; empty means every phrase the lesson's <c>repeat</c> and <c>answer</c> steps teach.</param>
/// <param name="Attempts">For <c>listen</c>, <c>repeat</c> and <c>answer</c>: how many tries the step allows before it moves on (fail-forward, D-game-9).</param>
/// <param name="Point">For <c>do</c> (S17): a catalogue noun the beacon is pointed at when the step begins (the nearest block of it), or <see langword="null"/> for none.</param>
/// <param name="Hud">For <c>do</c> (S17): the short English line the objective area shows while the step waits and the panel is closed, or <see langword="null"/> for none.</param>
public sealed record LessonStep(
    string Id,
    LessonStepType Type,
    string English,
    string? Help,
    string? Retry,
    SpeechLine? Line,
    string? Translation,
    string? Phrase,
    string? Prompt,
    IReadOnlyList<string> Options,
    int Correct,
    string? Event,
    string? Detail,
    IReadOnlyList<string> Phrases,
    int Attempts,
    string? Point = null,
    string? Hud = null)
{
    /// <summary>The attempts a step allows when the file says nothing: one try and one retry, then the lesson moves on.</summary>
    public const int DefaultAttempts = 2;

    /// <summary>Whether the step waits for the learner to say something (a real turn through the core).</summary>
    public bool WaitsOnTurn => Type is LessonStepType.Repeat or LessonStepType.Answer;
}

/// <summary>One lesson: a short sequence of steps attached to an NPC.</summary>
/// <param name="Id">The lesson's id.</param>
/// <param name="Npc">The NPC it is attached to (an agent id); it is offered when the player talks to that NPC.</param>
/// <param name="Title">The lesson's English title.</param>
/// <param name="English">One line of English about what the lesson is.</param>
/// <param name="AutoStart">Whether the lesson starts by itself the first time the player talks to its NPC (otherwise it is offered, and L starts it).</param>
/// <param name="After">The id of a lesson that must be completed first, or <see langword="null"/>.</param>
/// <param name="Steps">The steps, in order.</param>
public sealed record LessonDefinition(string Id, string Npc, string Title, string English, bool AutoStart, string? After, IReadOnlyList<LessonStep> Steps)
{
    /// <summary>The phrases the lesson teaches: those of its <c>repeat</c> and <c>answer</c> steps, in order, each once.</summary>
    public IReadOnlyList<string> TaughtPhrases =>
        Steps.Where(static s => s.WaitsOnTurn).Select(static s => s.Phrase!).Distinct(StringComparer.Ordinal).ToList();
}

/// <summary>
/// The lessons of the game (S16), read from the bundle's host file <c>game/lessons.json</c> (hashed, listed in <c>provenance.json</c>). A lesson is data: a
/// list of steps of the seven types, so a new lesson needs no code. The file is checked at load against the phrase sheet, the bundle's prompts and
/// speech lines and the world-event names, so a bad lesson is a load error, not a surprise in a playtest.
/// </summary>
/// <remarks>
/// <para>The file is <c>{ "schemaVersion": 1, "lessons": [ { "id", "npc", "title", "english", "autoStart"?, "after"?, "steps": [ ... ] } ] }</c>. A step has an <c>id</c>, a
/// <c>type</c> and <c>english</c> always, and optionally <c>help</c> (the hint of the help path), <c>retry</c> and <c>attempts</c> (1 to 5, default 2), plus:</para>
/// <list type="bullet">
/// <item><c>explain</c>: nothing more.</item>
/// <item><c>model</c>: <c>line</c> (a speech id such as <c>phrase.hola</c> or <c>guide_greet.1</c>) and <c>translation</c>.</item>
/// <item><c>listen</c>: <c>line</c>, <c>translation</c>, <c>options</c> (two to four English strings) and <c>correct</c> (the index).</item>
/// <item><c>repeat</c>: <c>phrase</c> (a row the learner says) and optionally <c>prompt</c> (default: the row's).</item>
/// <item><c>answer</c>: <c>phrase</c>, <c>line</c>, <c>translation</c> and optionally <c>prompt</c>.</item>
/// <item><c>do</c>: <c>event</c> (a world event name) and optionally <c>detail</c>, <c>point</c> (a noun the beacon goes to when the step begins) and <c>hud</c> (the short line the objective area shows while the step waits).</item>
/// <item><c>recap</c>: optionally <c>phrases</c> (default: every phrase the lesson teaches).</item>
/// </list>
/// <para>The row's intent must be one the prompt allows, so the core can recognise the answer. Plain values, no clock, no random numbers.</para>
/// </remarks>
public sealed class LessonBook
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/lessons.json";

    private readonly List<LessonDefinition> _lessons;

    private LessonBook(List<LessonDefinition> lessons) => _lessons = lessons;

    /// <summary>The lessons, in the file's order.</summary>
    public IReadOnlyList<LessonDefinition> Lessons => _lessons;

    /// <summary>Every phrase some lesson teaches (the phrases that get a place on the support ladder besides the four of the stall and the river).</summary>
    public IReadOnlyList<string> TaughtPhrases => _lessons.SelectMany(static l => l.TaughtPhrases).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The lesson with this id, or <see langword="null"/>.</summary>
    /// <param name="id">The lesson's id.</param>
    public LessonDefinition? Find(string id) => _lessons.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.Ordinal));

    /// <summary>Reads the lessons from the bundle's verified host file and checks them against the bundle.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <param name="sheet">The phrase sheet.</param>
    /// <param name="speech">The bundle's speakable lines (<see cref="SpeechLines.Enumerate"/>).</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or a lesson is malformed or does not fit the bundle.</exception>
    public static LessonBook Load(ContentBundle bundle, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (!bundle.TryReadHostFile(RelativePath, out var bytes))
        {
            throw new InvalidDataException($"The bundle has no host file {RelativePath}: it is missing from the manifest, or the bundle was loaded without hash verification.");
        }

        return Parse(Encoding.UTF8.GetString(bytes.Span), sheet, speech, bundle.Prompts);
    }

    /// <summary>Builds the lessons from the file's text, checking that each one holds together.</summary>
    /// <param name="json">The contents of <c>game/lessons.json</c>.</param>
    /// <param name="sheet">The phrase sheet.</param>
    /// <param name="speech">The bundle's speakable lines.</param>
    /// <param name="prompts">The bundle's prompts.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a lesson does not hold together.</exception>
    public static LessonBook Parse(string json, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech, IReadOnlyDictionary<string, PromptSpec> prompts)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(prompts);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion 1.");
            }

            var lessons = new List<LessonDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in root.GetProperty("lessons").EnumerateArray())
            {
                var lesson = ParseLesson(element, sheet, speech, prompts, ids);
                if (!ids.Add(lesson.Id))
                {
                    throw new InvalidDataException($"{RelativePath}: the lesson id '{lesson.Id}' is used twice.");
                }

                lessons.Add(lesson);
            }

            return new LessonBook(lessons);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static LessonDefinition ParseLesson(JsonElement element, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech, IReadOnlyDictionary<string, PromptSpec> prompts, ISet<string> earlier)
    {
        var id = Text(element, "id");
        var npc = Text(element, "npc");
        if (!NpcService.AgentIds.Contains(npc, StringComparer.Ordinal))
        {
            throw Bad(id, $"is attached to '{npc}', which is not one of the NPCs ({string.Join(", ", NpcService.AgentIds)})");
        }

        var after = Optional(element, "after");
        if (after is not null && !earlier.Contains(after))
        {
            throw Bad(id, $"comes after the lesson '{after}', which is not an earlier lesson of the file");
        }

        var steps = new List<LessonStep>();
        var stepIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stepElement in element.GetProperty("steps").EnumerateArray())
        {
            var step = ParseStep(id, stepElement, sheet, speech, prompts);
            if (!stepIds.Add(step.Id))
            {
                throw Bad(id, $"has the step id '{step.Id}' twice");
            }

            steps.Add(step);
        }

        if (steps.Count == 0)
        {
            throw Bad(id, "has no steps");
        }

        var autoStart = element.TryGetProperty("autoStart", out var auto) && auto.ValueKind == JsonValueKind.True;
        var lesson = new LessonDefinition(id, npc, Text(element, "title"), Text(element, "english"), autoStart, after, steps);
        foreach (var recap in steps.Where(static s => s.Type == LessonStepType.Recap))
        {
            foreach (var phrase in recap.Phrases)
            {
                if (!lesson.TaughtPhrases.Contains(phrase, StringComparer.Ordinal))
                {
                    throw Bad(id, $"has a recap listing '{phrase}', which no repeat or answer step of the lesson teaches");
                }
            }
        }

        return lesson;
    }

    private static LessonStep ParseStep(string lesson, JsonElement element, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech, IReadOnlyDictionary<string, PromptSpec> prompts)
    {
        var id = Text(element, "id");
        var typeName = Text(element, "type");
        var type = LessonStepTypes.Parse(typeName) ?? throw Bad(lesson, $"step '{id}' has the type '{typeName}', which is not one of explain, model, listen, repeat, answer, do, recap");
        var attempts = element.TryGetProperty("attempts", out var count) ? count.GetInt32() : LessonStep.DefaultAttempts;
        if (attempts is < 1 or > 5)
        {
            throw Bad(lesson, $"step '{id}' allows {attempts.ToString(CultureInfo.InvariantCulture)} attempts; it must be 1 to 5");
        }

        SpeechLine? line = null;
        string? translation = null;
        string? phrase = null;
        string? prompt = null;
        var options = new List<string>();
        var correct = -1;
        string? worldEvent = null;
        string? detail = null;
        var phrases = new List<string>();
        switch (type)
        {
            case LessonStepType.Model:
            case LessonStepType.Listen:
            case LessonStepType.Answer:
                line = LineOf(lesson, id, Text(element, "line"), speech);
                translation = Text(element, "translation");
                break;
            default:
                break;
        }

        switch (type)
        {
            case LessonStepType.Listen:
                options = element.GetProperty("options").EnumerateArray().Select(static o => o.GetString() ?? string.Empty).ToList();
                correct = element.GetProperty("correct").GetInt32();
                if (options.Count is < 2 or > 4 || options.Any(static o => o.Length == 0))
                {
                    throw Bad(lesson, $"step '{id}' needs two to four non-empty options");
                }

                if (correct < 0 || correct >= options.Count)
                {
                    throw Bad(lesson, $"step '{id}' has its correct option at {correct.ToString(CultureInfo.InvariantCulture)}, outside the options");
                }

                break;
            case LessonStepType.Repeat:
            case LessonStepType.Answer:
                (phrase, prompt) = PhraseAndPrompt(lesson, id, element, sheet, prompts);
                break;
            case LessonStepType.Do:
                worldEvent = Text(element, "event");
                if (!WorldEvents.All.Contains(worldEvent, StringComparer.Ordinal))
                {
                    throw Bad(lesson, $"step '{id}' waits for '{worldEvent}', which is not a world event the game raises ({string.Join(", ", WorldEvents.All)})");
                }

                detail = Optional(element, "detail");
                break;
            case LessonStepType.Recap:
                if (element.TryGetProperty("phrases", out var listed))
                {
                    foreach (var item in listed.EnumerateArray())
                    {
                        var listedId = item.GetString() ?? string.Empty;
                        if (sheet.Find(listedId) is null)
                        {
                            throw Bad(lesson, $"step '{id}' lists the phrase '{listedId}', which is not in the phrase sheet");
                        }

                        phrases.Add(listedId);
                    }
                }

                break;
            default:
                break;
        }

        return new LessonStep(id, type, Text(element, "english"), Optional(element, "help"), Optional(element, "retry"), line, translation, phrase, prompt, options, correct, worldEvent, detail, phrases, attempts, type == LessonStepType.Do ? Optional(element, "point") : null, type == LessonStepType.Do ? Optional(element, "hud") : null);
    }

    private static (string Phrase, string Prompt) PhraseAndPrompt(string lesson, string step, JsonElement element, PhraseSheet sheet, IReadOnlyDictionary<string, PromptSpec> prompts)
    {
        var phraseId = Text(element, "phrase");
        var row = sheet.Find(phraseId) ?? throw Bad(lesson, $"step '{step}' names the phrase '{phraseId}', which is not in the phrase sheet");
        if (!row.IsLearnerPhrase || row.Intent is null)
        {
            throw Bad(lesson, $"step '{step}' names '{phraseId}', a phrase the learner does not say");
        }

        var promptId = Optional(element, "prompt") ?? row.Prompt ?? throw Bad(lesson, $"step '{step}' has no prompt and the phrase '{phraseId}' has none either");
        if (!prompts.TryGetValue(promptId, out var prompt))
        {
            throw Bad(lesson, $"step '{step}' is evaluated at the prompt '{promptId}', which the bundle does not have");
        }

        if (!prompt.AllowedIntentIds.Contains(row.Intent, StringComparer.Ordinal))
        {
            throw Bad(lesson, $"step '{step}': the prompt '{promptId}' does not allow the intent '{row.Intent}' of the phrase '{phraseId}', so the core could not recognise it");
        }

        return (phraseId, promptId);
    }

    private static SpeechLine LineOf(string lesson, string step, string lineId, IReadOnlyList<SpeechLine> speech) =>
        speech.FirstOrDefault(l => string.Equals(l.LineId, lineId, StringComparison.Ordinal))
        ?? throw Bad(lesson, $"step '{step}' plays the line '{lineId}', which has no pre-rendered speech line in the bundle");

    private static InvalidDataException Bad(string lesson, string problem) => new($"{RelativePath}: the lesson '{lesson}' {problem}.");

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"{RelativePath}: the text '{name}' is missing or empty{(element.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? $" (in '{id.GetString()}')" : string.Empty)}.");
        }

        return value.GetString()!;
    }

    private static string? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
}
