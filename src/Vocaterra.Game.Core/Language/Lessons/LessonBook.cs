using System.Globalization;
using System.Text;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Prompts;

namespace Vocaterra.Game.Core.Language.Lessons;

/// <summary>The kinds of lesson step (S16, <c>docs/game/SCENARIO_PLAN.md</c> section 8).</summary>
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
/// <param name="Trigger">For <c>do</c> (C06): the world event the step waits for (<see cref="WorldEvents"/>), with the NPC and the exact detail it must have when it names them.</param>
/// <param name="Phrases">For <c>recap</c>: the phrases it lists; empty means every phrase the lesson's <c>repeat</c> and <c>answer</c> steps teach.</param>
/// <param name="Attempts">For <c>listen</c>, <c>repeat</c> and <c>answer</c>: how many tries the step allows before it moves on (fail-forward, D-game-9).</param>
/// <param name="Point">For <c>do</c> (S17): a catalogue noun the beacon is pointed at when the step begins (the nearest block of it), or <see langword="null"/> for none.</param>
/// <param name="Hud">For <c>do</c> (S17): the short English line the objective area shows while the step waits and the panel is closed, or <see langword="null"/> for none.</param>
/// <param name="Npc">The NPC the step is said at (S26: a lesson may move from one NPC to another), or <see langword="null"/> for the lesson's own NPC.</param>
/// <param name="Person">For <c>repeat</c> and <c>answer</c> (S26): the person and number of the finite verb the answer must have (<c>1sg</c>, <c>2sg</c>, <c>3sg</c>, <c>1pl</c>, <c>2pl</c> or <c>3pl</c>), or <see langword="null"/> to judge nothing of it.</param>
/// <param name="PersonRetry">The English line shown when the answer's verb has another person than <see cref="Person"/> (it names the ending), or <see langword="null"/> for the step's retry line.</param>
/// <param name="Tense">For <c>repeat</c> and <c>answer</c> (S27): the tense the answer's verb must have (<c>pres</c>, <c>pret</c> or <c>ir-a</c>, the near future: a form of <c>ir</c>, <c>a</c> and an infinitive), or <see langword="null"/> to judge nothing of it.</param>
/// <param name="TenseRetry">The English line shown when the answer's verb has another tense than <see cref="Tense"/> (it names the form), or <see langword="null"/> for the step's retry line.</param>
/// <param name="PresentNote">For an <c>ir-a</c> step (S27): the English note shown after a plan said in the present (<c>mañana voy al río</c>), which is right Spanish and is credited; it names the form the lesson practises. <see langword="null"/> for the step with no note.</param>
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
    Trigger? Trigger,
    IReadOnlyList<string> Phrases,
    int Attempts,
    string? Point = null,
    string? Hud = null,
    string? Npc = null,
    string? Person = null,
    string? PersonRetry = null,
    string? Tense = null,
    string? TenseRetry = null,
    string? PresentNote = null)
{
    /// <summary>The attempts a step allows when the file says nothing: one try and one retry, then the lesson moves on.</summary>
    public const int DefaultAttempts = 2;

    /// <summary>Whether the step waits for the learner to say something (a real turn through the core).</summary>
    public bool WaitsOnTurn => Type is LessonStepType.Repeat or LessonStepType.Answer;

    /// <summary>For <c>do</c>: the world event's name the step waits for (<see cref="Trigger"/>).</summary>
    public string? Event => Trigger?.Event;

    /// <summary>For <c>do</c>: the detail the event must have, exactly, or <see langword="null"/> for any.</summary>
    public string? Detail => Trigger?.Detail;

    /// <summary>For <c>do</c> with an NPC in its trigger (S26): the NPC the learner is to walk up to; the step ends when the event happens at that NPC.</summary>
    public string? At => Trigger?.Npc;
}

/// <summary>
/// One verb form a lesson teaches (S26): the lemma, the grammatical person (<c>1</c>, <c>2</c> or <c>3</c>), the number (<c>sg</c> or <c>pl</c>) and the tense, spelled as the
/// core's lexicon features spell them. <c>usted</c> shares the third person singular and <c>ustedes</c> the third person plural; the book's tables place them.
/// </summary>
/// <param name="Lemma">The verb's infinitive.</param>
/// <param name="Person">The person: <c>1</c>, <c>2</c> or <c>3</c>.</param>
/// <param name="Number">The number: <c>sg</c> or <c>pl</c>.</param>
/// <param name="Tense">The tense: <c>pres</c> (present), <c>pret</c> (preterite, S27), <c>ir-a</c> (the near future, S27) or <c>fut</c> (the simple future, A2, not taught yet).</param>
public sealed record TaughtVerbForm(string Lemma, string Person, string Number, string Tense)
{
    /// <summary>The tense of the near future, <c>ir a</c> + infinitive (S27).</summary>
    public const string NearFuture = "ir-a";

    /// <summary>
    /// The form as the learner meets it. For the tenses the core's morphology generates (<c>pres</c>, <c>pret</c>, <c>fut</c>) it is the conjugated word (<c>comí</c>). The near future is a
    /// <i>periphrastic</i> form (S27, for the book's conjugation tables, S28): it has no word of its own, so the form is <c>ir</c> in the present for that person and number, then
    /// <c>a</c>, then the lemma as the infinitive: <c>comer</c> 1 sg <c>ir-a</c> is <c>voy a comer</c>. The lemma of an <c>ir-a</c> form is the verb that follows the <c>a</c>, so a table
    /// of <c>comer</c> has a column "future (ir a)" whose cells are built from <c>ir</c>'s present column and the infinitive, never typed by hand.
    /// </summary>
    /// <param name="morphology">The bundle's morphology.</param>
    /// <param name="nearFuture">The near future's verb and joiner (<c>ir</c>, <c>a</c>; <c>game/words.json</c>, C05).</param>
    /// <returns>The form's text, or <see langword="null"/> when the core cannot generate it.</returns>
    public string? Text(Vocaterra.Nlp.Es.Morphology.MorphologyGenerator morphology, NearFutureWords nearFuture)
    {
        ArgumentNullException.ThrowIfNull(morphology);
        ArgumentNullException.ThrowIfNull(nearFuture);
        if (!string.Equals(Tense, NearFuture, StringComparison.Ordinal))
        {
            return morphology.Conjugate(Lemma, Person, Number, Tense);
        }

        return morphology.Conjugate(nearFuture.Verb, Person, Number, "pres") is { } ir ? $"{ir} {nearFuture.Joiner} {Lemma}" : null;
    }
}

/// <summary>One lesson: a short sequence of steps attached to an NPC.</summary>
/// <param name="Id">The lesson's id.</param>
/// <param name="Npc">The NPC it is attached to (an agent id); it is offered when the player talks to that NPC.</param>
/// <param name="Title">The lesson's English title.</param>
/// <param name="English">One line of English about what the lesson is.</param>
/// <param name="AutoStart">Whether the lesson starts by itself the first time the player talks to its NPC (otherwise it is offered, and L starts it).</param>
/// <param name="After">The id of a lesson that must be completed first, or <see langword="null"/>.</param>
/// <param name="Steps">The steps, in order.</param>
/// <param name="Summary">What the learner can say once the lesson is done, in English (the book's line for a completed lesson, S20), or <see langword="null"/>.</param>
/// <param name="Session">The play session the lesson unlocks in (S26): 1 is open from the first session, 2 from the second, and so on.</param>
/// <param name="Objective">For a lesson of a later session: the English line the objective area shows once the lesson is unlocked and not begun (it names the lesson), or <see langword="null"/>.</param>
/// <param name="Verbs">The verb forms the lesson teaches (S26; the book's conjugation tables, S28, show what has been taught).</param>
public sealed record LessonDefinition(
    string Id,
    string Npc,
    string Title,
    string English,
    bool AutoStart,
    string? After,
    IReadOnlyList<LessonStep> Steps,
    string? Summary = null,
    int Session = 1,
    string? Objective = null,
    IReadOnlyList<TaughtVerbForm>? Verbs = null)
{
    /// <summary>The verb forms the lesson teaches, as the file declares them (empty when it declares none).</summary>
    public IReadOnlyList<TaughtVerbForm> TaughtVerbs => Verbs ?? [];

    /// <summary>The NPC a step is said at: its own, or the lesson's.</summary>
    /// <param name="step">The step.</param>
    public string NpcOf(LessonStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.Npc ?? Npc;
    }

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
/// <para>The file is <c>{ "schemaVersion": 1, "lessons": [ { "id", "npc", "autoStart"?, "after"?, "steps": [ ... ] } ] }</c>. It carries ids only: every word the
/// learner reads is the support language's, in the locale (CONTENT_PLAN C03): the lesson's <c>lesson.&lt;id&gt;.title</c> and <c>.text</c> (and <c>.summary</c>, <c>.objective</c>),
/// and each step's <c>lesson.&lt;id&gt;.&lt;step&gt;.text</c> (and <c>.help</c>, <c>.retry</c>, <c>.translation</c>, <c>.option.&lt;n&gt;</c>, <c>.hud</c>, <c>.personRetry</c>,
/// <c>.tenseRetry</c>, <c>.presentNote</c>). A step has an <c>id</c> and a <c>type</c> always, and optionally <c>attempts</c> (1 to 5, default 2) and <c>npc</c> (S26: the
/// NPC the step is said at, when the lesson moves from one NPC to another), plus:</para>
/// <list type="bullet">
/// <item><c>explain</c>: nothing more.</item>
/// <item><c>model</c>: <c>line</c> (a speech id such as <c>phrase.hola</c> or <c>guide_greet.1</c>); its translation is the locale's.</item>
/// <item><c>listen</c>: <c>line</c> and <c>correct</c> (the index of the right one of the locale's two to four options).</item>
/// <item><c>repeat</c>: <c>phrase</c> (a row the learner says) and optionally <c>prompt</c> (default: the row's).</item>
/// <item><c>answer</c>: <c>phrase</c>, <c>line</c> and optionally <c>prompt</c>.</item>
/// <item><c>do</c>: <c>trigger</c> (C06: <c>{ "event", "npc"?, "detail"? }</c>, a world event with the NPC and the exact detail it must have) and optionally <c>point</c> (a noun the beacon goes to when the step begins).</item>
/// <item><c>recap</c>: optionally <c>phrases</c> (default: every phrase the lesson teaches).</item>
/// </list>
/// <para>A lesson may also carry <c>session</c> (the play session it unlocks in, default 1; above 1 the locale needs its objective line)
/// and <c>verbs</c> (the verb forms it teaches: <c>lemma</c>, <c>person</c> 1 to 3, <c>number</c> sg or pl, <c>tense</c> pres, pret, ir-a or fut; S26, S27). A repeat or answer step may declare the <c>person</c>
/// of the verb it expects (<c>1sg</c>...) and its <c>tense</c> (<c>pres</c>, <c>pret</c>, <c>ir-a</c>), with their retry lines in the locale (S27).</para>
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

    /// <summary>The folder of the lesson files inside the bundle folder: one file per lesson, <c>game/lessons/&lt;id&gt;.json</c> (C06).</summary>
    public const string Folder = "game/lessons/";

    /// <summary>
    /// Reads the lessons from the bundle's verified host files and checks them against the bundle (C06): <c>game/lessons.json</c> lists the lessons' ids in
    /// their order (<c>{ "schemaVersion": 2, "lessons": [ "first-words", ... ] }</c>), and each lesson is its own file, <c>game/lessons/&lt;id&gt;.json</c>.
    /// </summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <param name="sheet">The phrase sheet.</param>
    /// <param name="speech">The bundle's speakable lines (<see cref="SpeechLines.Enumerate"/>).</param>
    /// <param name="locale">The support language, which holds the lessons' text (<c>lesson.&lt;id&gt;.*</c>).</param>
    /// <param name="npcs">The NPCs' ids (<c>game/npcs.json</c>): a lesson, a step and a trigger may name only these.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, a lesson file is missing, not listed or names another id, or a lesson is malformed or does not fit the bundle.</exception>
    public static LessonBook Load(ContentBundle bundle, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech, LocaleText locale, IReadOnlyList<string> npcs)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var ids = new List<string>();
        try
        {
            using var index = JsonDocument.Parse(GameData.HostText(bundle, RelativePath));
            if (index.RootElement.GetProperty("schemaVersion").GetInt32() != 2)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion 2 (the index of the lesson files).");
            }

            ids.AddRange(index.RootElement.GetProperty("lessons").EnumerateArray().Select(static e => e.GetString() ?? string.Empty));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }

        var files = new List<string>();
        foreach (var id in ids)
        {
            var path = Folder + id + ".json";
            var text = GameData.HostText(bundle, path);
            using (var lesson = JsonDocument.Parse(text))
            {
                if (!lesson.RootElement.TryGetProperty("id", out var fileId) || !string.Equals(fileId.GetString(), id, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"{path}: the lesson's id must be its file's name, '{id}'.");
                }
            }

            files.Add(text);
        }

        var unlisted = bundle.HostFilePaths.Where(path => path.StartsWith(Folder, StringComparison.Ordinal) && !ids.Contains(path[Folder.Length..^".json".Length], StringComparer.Ordinal)).ToList();
        if (unlisted.Count > 0)
        {
            throw new InvalidDataException($"{RelativePath} does not list the lesson file {string.Join(", ", unlisted)}.");
        }

        return Parse("{ \"schemaVersion\": 1, \"lessons\": [" + string.Join(",", files) + "] }", sheet, speech, bundle.Prompts, locale, npcs);
    }

    /// <summary>Builds the lessons from a book's text, checking that each one holds together: <c>{ "schemaVersion": 1, "lessons": [ { lesson }, ... ] }</c>, the lessons in order (the shipped lessons are one file each, which <see cref="Load"/> puts together).</summary>
    /// <param name="json">The book.</param>
    /// <param name="sheet">The phrase sheet.</param>
    /// <param name="speech">The bundle's speakable lines.</param>
    /// <param name="prompts">The bundle's prompts.</param>
    /// <param name="locale">The support language, which holds the lessons' text (<c>lesson.&lt;id&gt;.*</c>).</param>
    /// <param name="npcs">The NPCs' ids (<c>game/npcs.json</c>): a lesson, a step and a trigger may name only these.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a lesson does not hold together.</exception>
    public static LessonBook Parse(string json, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech, IReadOnlyDictionary<string, PromptSpec> prompts, LocaleText locale, IReadOnlyList<string> npcs)
    {
        ArgumentNullException.ThrowIfNull(npcs);
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(locale);
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
                var lesson = ParseLesson(element, sheet, speech, prompts, locale, npcs, ids);
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

    private static LessonDefinition ParseLesson(JsonElement element, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech, IReadOnlyDictionary<string, PromptSpec> prompts, LocaleText locale, IReadOnlyList<string> npcs, ISet<string> earlier)
    {
        var id = Text(element, "id");
        var npc = Text(element, "npc");
        if (!npcs.Contains(npc, StringComparer.Ordinal))
        {
            throw Bad(id, $"is attached to '{npc}', which is not one of the NPCs ({string.Join(", ", npcs)})");
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
            var step = ParseStep(id, stepElement, sheet, speech, prompts, locale, npcs);
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

        foreach (var step in steps)
        {
            var stepNpc = step.Npc ?? npc;
            if (step.Line is { Kind: SpeechKind.Dialogue or SpeechKind.Prompt } spoken && !string.Equals(spoken.Speaker, stepNpc, StringComparison.Ordinal))
            {
                throw Bad(id, $"step '{step.Id}' is said at {stepNpc}, but its line '{spoken.LineId}' is spoken by {spoken.Speaker}");
            }
        }

        var autoStart = element.TryGetProperty("autoStart", out var auto) && auto.ValueKind == JsonValueKind.True;
        var session = element.TryGetProperty("session", out var sessionElement) ? sessionElement.GetInt32() : 1;
        if (session < 1)
        {
            throw Bad(id, $"unlocks in session {session.ToString(CultureInfo.InvariantCulture)}; sessions start at 1");
        }

        var objective = locale.Find(LessonId(id, "objective"));
        if (session > 1 && objective is null)
        {
            throw Bad(id, $"unlocks in a later session and needs an objective line that names it ({LessonId(id, "objective")} in the locale)");
        }

        var lesson = new LessonDefinition(id, npc, Required(locale, id, LessonId(id, "title")), Required(locale, id, LessonId(id, "text")), autoStart, after, steps, locale.Find(LessonId(id, "summary")), session, objective, ParseVerbs(id, element));
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

    private static List<TaughtVerbForm> ParseVerbs(string lesson, JsonElement element)
    {
        var forms = new List<TaughtVerbForm>();
        if (!element.TryGetProperty("verbs", out var listed))
        {
            return forms;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in listed.EnumerateArray())
        {
            var form = new TaughtVerbForm(Text(item, "lemma"), Text(item, "person"), Text(item, "number"), Text(item, "tense"));
            if (form.Person is not ("1" or "2" or "3") || form.Number is not ("sg" or "pl") || form.Tense is not ("pres" or "pret" or "fut" or TaughtVerbForm.NearFuture))
            {
                throw Bad(lesson, $"declares the verb form {form.Lemma} {form.Person} {form.Number} {form.Tense}, which is not a person 1 to 3, a number sg or pl and a tense pres, pret, fut or ir-a");
            }

            if (!seen.Add($"{form.Lemma}/{form.Person}/{form.Number}/{form.Tense}"))
            {
                throw Bad(lesson, $"declares the verb form {form.Lemma} {form.Person} {form.Number} {form.Tense} twice");
            }

            forms.Add(form);
        }

        return forms;
    }

    private static LessonStep ParseStep(string lesson, JsonElement element, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech, IReadOnlyDictionary<string, PromptSpec> prompts, LocaleText locale, IReadOnlyList<string> npcs)
    {
        var id = Text(element, "id");
        string? Say(string field) => locale.Find(StepId(lesson, id, field));
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
        Trigger? trigger = null;
        var phrases = new List<string>();
        switch (type)
        {
            case LessonStepType.Model:
            case LessonStepType.Listen:
            case LessonStepType.Answer:
                line = LineOf(lesson, id, Text(element, "line"), speech);
                translation = Required(locale, lesson, StepId(lesson, id, "translation"));
                break;
            default:
                break;
        }

        switch (type)
        {
            case LessonStepType.Listen:
                options = Options(locale, lesson, id);
                correct = element.GetProperty("correct").GetInt32();
                if (options.Count is < 2 or > MaxOptions)
                {
                    throw Bad(lesson, $"step '{id}' needs two to four options ({StepId(lesson, id, "option.<n>")} in the locale)");
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
                if (!element.TryGetProperty("trigger", out var triggerElement))
                {
                    throw Bad(lesson, $"step '{id}' is a do step with no trigger (the world event it waits for)");
                }

                trigger = Trigger.Parse(triggerElement, WorldEvents.All, $"{RelativePath}: the lesson '{lesson}', step '{id}'");
                if (trigger.Npc is { } waitedAt && !npcs.Contains(waitedAt, StringComparer.Ordinal))
                {
                    throw Bad(lesson, $"step '{id}' waits at '{waitedAt}', which is not one of the NPCs ({string.Join(", ", npcs)})");
                }

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

        var stepNpc = Optional(element, "npc");
        if (stepNpc is not null && !npcs.Contains(stepNpc, StringComparer.Ordinal))
        {
            throw Bad(lesson, $"step '{id}' is said at '{stepNpc}', which is not one of the NPCs ({string.Join(", ", npcs)})");
        }

        var person = Optional(element, "person");
        if (person is not null && (type is not (LessonStepType.Repeat or LessonStepType.Answer) || !System.Text.RegularExpressions.Regex.IsMatch(person, "^[123](sg|pl)$")))
        {
            throw Bad(lesson, $"step '{id}' expects the person '{person}': \"person\" is 1sg, 2sg, 3sg, 1pl, 2pl or 3pl and goes with a repeat or answer step");
        }

        var tense = Optional(element, "tense");
        if (tense is not null && (type is not (LessonStepType.Repeat or LessonStepType.Answer) || tense is not ("pres" or "pret" or TaughtVerbForm.NearFuture)))
        {
            throw Bad(lesson, $"step '{id}' expects the tense '{tense}': \"tense\" is pres, pret or ir-a and goes with a repeat or answer step");
        }

        return new LessonStep(id, type, Required(locale, lesson, StepId(lesson, id, "text")), Say("help"), Say("retry"), line, translation, phrase, prompt, options, correct, trigger, phrases, attempts, type == LessonStepType.Do ? Optional(element, "point") : null, type == LessonStepType.Do ? Say("hud") : null, stepNpc, person, person is null ? null : Say("personRetry"), tense, tense is null ? null : Say("tenseRetry"), tense == TaughtVerbForm.NearFuture ? Say("presentNote") : null);
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

    /// <summary>The locale id of a lesson's own text: <c>lesson.&lt;id&gt;.&lt;field&gt;</c> (title, text, summary, objective).</summary>
    /// <param name="lesson">The lesson's id.</param>
    /// <param name="field">The field.</param>
    public static string LessonId(string lesson, string field) => "lesson." + lesson + "." + field;

    /// <summary>The locale id of a step's text: <c>lesson.&lt;id&gt;.&lt;step&gt;.&lt;field&gt;</c> (text, translation, help, retry, personRetry, tenseRetry, presentNote, hud, option.&lt;n&gt;).</summary>
    /// <param name="lesson">The lesson's id.</param>
    /// <param name="step">The step's id.</param>
    /// <param name="field">The field.</param>
    public static string StepId(string lesson, string step, string field) => "lesson." + lesson + "." + step + "." + field;

    /// <summary>The most options a listen step offers (<c>option.1</c> to <c>option.4</c>).</summary>
    public const int MaxOptions = 4;

    private static string Required(LocaleText locale, string lesson, string id) =>
        locale.Find(id) ?? throw Bad(lesson, $"has no text '{id}' in the locale {locale.Code}");

    // A listen step's options: option.1, option.2, ... in the locale, as many as follow one another.
    private static List<string> Options(LocaleText locale, string lesson, string step)
    {
        var options = new List<string>();
        while (options.Count < MaxOptions + 1 && locale.Find(StepId(lesson, step, "option." + (options.Count + 1).ToString(CultureInfo.InvariantCulture))) is { } option)
        {
            options.Add(option);
        }

        return options;
    }

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
