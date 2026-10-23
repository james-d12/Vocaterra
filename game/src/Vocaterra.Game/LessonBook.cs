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

    /// <summary>
    /// <c>review</c> (X06, R07): an earlier lesson's phrase comes back. The step lists candidates (a phrase, the NPC's line that asks it and what the line means); at run time
    /// the runner asks the first of them that is due (<see cref="PhraseProgress.DuePhrases"/>) as an answer step, with the help of the phrase's own rung, and skips the step
    /// when none is due. It is never judged for a person or a tense.
    /// </summary>
    Review,
}

/// <summary>
/// One phrase a <c>review</c> step may ask (X06, R07): the phrase of an earlier lesson, the line its NPC says to ask it (an existing speech line of the step's NPC), what the
/// line means and the prompt the answer is evaluated at. No new Spanish: the line and the phrase are ones the game already has.
/// </summary>
/// <param name="Phrase">The phrase sheet's id of the phrase the learner says.</param>
/// <param name="Prompt">The bundle prompt the turn is evaluated at.</param>
/// <param name="Line">The pre-rendered line the NPC says.</param>
/// <param name="Translation">What the line means, in English.</param>
public sealed record ReviewCandidate(string Phrase, string Prompt, SpeechLine Line, string Translation);

/// <summary>The wire names of the lesson step types, as <c>game/lessons.json</c> and the session log spell them.</summary>
public static class LessonStepTypes
{
    /// <summary>The wire name of a step type: <c>explain</c>, <c>model</c>, <c>listen</c>, <c>repeat</c>, <c>answer</c>, <c>do</c>, <c>recap</c> or <c>review</c>.</summary>
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
        LessonStepType.Review => "review",
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
        "review" => LessonStepType.Review,
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
/// <param name="Npc">The NPC the step is said at (S26: a lesson may move from one NPC to another), or <see langword="null"/> for the lesson's own NPC.</param>
/// <param name="Person">For <c>repeat</c> and <c>answer</c> (S26): the person and number of the finite verb the answer must have (<c>1sg</c>, <c>2sg</c>, <c>3sg</c>, <c>1pl</c>, <c>2pl</c> or <c>3pl</c>), or <see langword="null"/> to judge nothing of it.</param>
/// <param name="PersonRetry">The English line shown when the answer's verb has another person than <see cref="Person"/> (it names the ending), or <see langword="null"/> for the step's retry line.</param>
/// <param name="Tense">For <c>repeat</c> and <c>answer</c> (S27): the tense the answer's verb must have (<c>pres</c>, <c>pret</c> or <c>ir-a</c>, the near future: a form of <c>ir</c>, <c>a</c> and an infinitive), or <see langword="null"/> to judge nothing of it.</param>
/// <param name="TenseRetry">The English line shown when the answer's verb has another tense than <see cref="Tense"/> (it names the form), or <see langword="null"/> for the step's retry line.</param>
/// <param name="PresentNote">For an <c>ir-a</c> step (S27): the English note shown after a plan said in the present (<c>mañana voy al río</c>), which is right Spanish and is credited; it names the form the lesson practises. <see langword="null"/> for the step with no note.</param>
/// <param name="At">For <c>do</c> with the <c>talk_opened</c> event (S26): the NPC the learner is to walk up to and talk to; the step ends when the panel opens on that NPC.</param>
/// <param name="Candidates">For <c>review</c> (X06): the phrases the step may ask, in order of preference; <see cref="Line"/>, <see cref="Translation"/>, <see cref="Phrase"/> and <see cref="Prompt"/> are those of the first. At run time the runner asks the first that is due.</param>
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
    string? Hud = null,
    string? Npc = null,
    string? At = null,
    string? Person = null,
    string? PersonRetry = null,
    string? Tense = null,
    string? TenseRetry = null,
    string? PresentNote = null,
    IReadOnlyList<ReviewCandidate>? Candidates = null)
{
    /// <summary>The attempts a step allows when the file says nothing: one try and one retry, then the lesson moves on.</summary>
    public const int DefaultAttempts = 2;

    /// <summary>Whether the step waits for the learner to say something (a real turn through the core).</summary>
    public bool WaitsOnTurn => Type is LessonStepType.Repeat or LessonStepType.Answer or LessonStepType.Review;
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
    /// <returns>The form's text, or <see langword="null"/> when the core cannot generate it.</returns>
    public string? Text(Vocaterra.Nlp.Language.Morphology.MorphologyGenerator morphology)
    {
        ArgumentNullException.ThrowIfNull(morphology);
        if (!string.Equals(Tense, NearFuture, StringComparison.Ordinal))
        {
            return morphology.Conjugate(Lemma, Person, Number, Tense);
        }

        return morphology.Conjugate("ir", Person, Number, "pres") is { } ir ? $"{ir} a {Lemma}" : null;
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

    /// <summary>The phrases the lesson teaches: those of its <c>repeat</c> and <c>answer</c> steps, in order, each once (a <c>review</c> step teaches nothing: it asks what an earlier lesson taught).</summary>
    public IReadOnlyList<string> TaughtPhrases =>
        Steps.Where(static s => s.Type is LessonStepType.Repeat or LessonStepType.Answer).Select(static s => s.Phrase!).Distinct(StringComparer.Ordinal).ToList();
}

/// <summary>
/// The lessons of the game (S16), read from the bundle's host file <c>game/lessons.json</c> (hashed, listed in <c>provenance.json</c>). A lesson is data: a
/// list of steps of the eight types, so a new lesson needs no code. The file is checked at load against the phrase sheet, the bundle's prompts and
/// speech lines and the world-event names, so a bad lesson is a load error, not a surprise in a playtest.
/// </summary>
/// <remarks>
/// <para>The file is <c>{ "schemaVersion": 1, "lessons": [ { "id", "npc", "title", "english", "autoStart"?, "after"?, "steps": [ ... ] } ] }</c>. A step has an <c>id</c>, a
/// <c>type</c> and <c>english</c> always, and optionally <c>help</c> (the hint of the help path), <c>retry</c>, <c>attempts</c> (1 to 5, default 2) and <c>npc</c> (S26: the NPC the step is said at, when the lesson moves from one NPC to another), plus:</para>
/// <list type="bullet">
/// <item><c>explain</c>: nothing more.</item>
/// <item><c>model</c>: <c>line</c> (a speech id such as <c>phrase.hola</c> or <c>guide_greet.1</c>) and <c>translation</c>.</item>
/// <item><c>listen</c>: <c>line</c>, <c>translation</c>, <c>options</c> (two to four English strings) and <c>correct</c> (the index).</item>
/// <item><c>repeat</c>: <c>phrase</c> (a row the learner says) and optionally <c>prompt</c> (default: the row's).</item>
/// <item><c>answer</c>: <c>phrase</c>, <c>line</c>, <c>translation</c> and optionally <c>prompt</c>.</item>
/// <item><c>do</c>: <c>event</c> (a world event name) and optionally <c>detail</c>, <c>point</c> (a noun the beacon goes to when the step begins), <c>hud</c> (the short line the objective area shows while the step waits) and, with the event <c>talk_opened</c>, <c>at</c> (the NPC to walk up to; S26).</item>
/// <item><c>recap</c>: optionally <c>phrases</c> (default: every phrase the lesson teaches).</item>
/// <item><c>review</c> (X06, R07): <c>candidates</c>, a non-empty list of <c>{ phrase, line, translation, prompt? }</c> naming phrases an earlier lesson (or the stall and the river) taught and the existing line of the step's NPC that asks each. At run time the first candidate that is due is asked as an answer step (the help of the phrase's own rung); when none is due the step is skipped.</item>
/// </list>
/// <para>A lesson may also carry <c>session</c> (the play session it unlocks in, default 1), <c>objective</c> (the English line that names it while it is unlocked and not begun; needed when <c>session</c> is above 1)
/// and <c>verbs</c> (the verb forms it teaches: <c>lemma</c>, <c>person</c> 1 to 3, <c>number</c> sg or pl, <c>tense</c> pres, pret, ir-a or fut; S26, S27). A repeat or answer step may declare the <c>person</c>
/// of the verb it expects (<c>1sg</c>...) with a <c>personRetry</c>, and its <c>tense</c> (<c>pres</c>, <c>pret</c>, <c>ir-a</c>) with a <c>tenseRetry</c> (S27).</para>
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
            var taughtBefore = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in root.GetProperty("lessons").EnumerateArray())
            {
                var lesson = ParseLesson(element, sheet, speech, prompts, ids, taughtBefore);
                if (!ids.Add(lesson.Id))
                {
                    throw new InvalidDataException($"{RelativePath}: the lesson id '{lesson.Id}' is used twice.");
                }

                lessons.Add(lesson);
                taughtBefore.UnionWith(lesson.TaughtPhrases);
            }

            return new LessonBook(lessons);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static LessonDefinition ParseLesson(JsonElement element, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech, IReadOnlyDictionary<string, PromptSpec> prompts, ISet<string> earlier, ISet<string> taughtBefore)
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

        foreach (var step in steps)
        {
            var stepNpc = step.Npc ?? npc;
            foreach (var line in step.Candidates is { } asked ? asked.Select(static c => (SpeechLine?)c.Line) : [step.Line])
            {
                if (line is { Kind: SpeechKind.Dialogue or SpeechKind.Prompt } spoken && !string.Equals(spoken.Speaker, stepNpc, StringComparison.Ordinal))
                {
                    throw Bad(id, $"step '{step.Id}' is said at {stepNpc}, but its line '{spoken.LineId}' is spoken by {spoken.Speaker}");
                }
            }

            foreach (var candidate in step.Candidates ?? [])
            {
                if (!taughtBefore.Contains(candidate.Phrase) && !PhraseIds.HasScaffold(candidate.Phrase))
                {
                    throw Bad(id, $"step '{step.Id}' reviews the phrase '{candidate.Phrase}', which no earlier lesson of the file teaches");
                }
            }
        }

        var autoStart = element.TryGetProperty("autoStart", out var auto) && auto.ValueKind == JsonValueKind.True;
        var session = element.TryGetProperty("session", out var sessionElement) ? sessionElement.GetInt32() : 1;
        if (session < 1)
        {
            throw Bad(id, $"unlocks in session {session.ToString(CultureInfo.InvariantCulture)}; sessions start at 1");
        }

        var objective = Optional(element, "objective");
        if (session > 1 && objective is null)
        {
            throw Bad(id, "unlocks in a later session and needs an English \"objective\" line that names it");
        }

        var lesson = new LessonDefinition(id, npc, Text(element, "title"), Text(element, "english"), autoStart, after, steps, Optional(element, "summary"), session, objective, ParseVerbs(id, element));
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

    private static LessonStep ParseStep(string lesson, JsonElement element, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech, IReadOnlyDictionary<string, PromptSpec> prompts)
    {
        var id = Text(element, "id");
        var typeName = Text(element, "type");
        var type = LessonStepTypes.Parse(typeName) ?? throw Bad(lesson, $"step '{id}' has the type '{typeName}', which is not one of explain, model, listen, repeat, answer, do, recap, review");
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
        IReadOnlyList<ReviewCandidate>? candidates = null;
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
            case LessonStepType.Review:
                candidates = ParseCandidates(lesson, id, element, sheet, speech, prompts);
                (phrase, prompt, line, translation) = (candidates[0].Phrase, candidates[0].Prompt, candidates[0].Line, candidates[0].Translation);
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

        var stepNpc = Optional(element, "npc");
        if (stepNpc is not null && !NpcService.AgentIds.Contains(stepNpc, StringComparer.Ordinal))
        {
            throw Bad(lesson, $"step '{id}' is said at '{stepNpc}', which is not one of the NPCs ({string.Join(", ", NpcService.AgentIds)})");
        }

        var at = type == LessonStepType.Do ? Optional(element, "at") : null;
        if (at is not null && (!NpcService.AgentIds.Contains(at, StringComparer.Ordinal) || !string.Equals(worldEvent, WorldEvents.TalkOpened, StringComparison.Ordinal)))
        {
            throw Bad(lesson, $"step '{id}' waits at '{at}': \"at\" names an NPC ({string.Join(", ", NpcService.AgentIds)}) and goes with the event {WorldEvents.TalkOpened}");
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

        return new LessonStep(id, type, Text(element, "english"), Optional(element, "help"), Optional(element, "retry"), line, translation, phrase, prompt, options, correct, worldEvent, detail, phrases, attempts, type == LessonStepType.Do ? Optional(element, "point") : null, type == LessonStepType.Do ? Optional(element, "hud") : null, stepNpc, at, person, person is null ? null : Optional(element, "personRetry"), tense, tense is null ? null : Optional(element, "tenseRetry"), tense == TaughtVerbForm.NearFuture ? Optional(element, "presentNote") : null, candidates);
    }

    // The candidates of a review step (X06): each names a phrase, the NPC's line that asks it, what the line means, and optionally the prompt (default: the row's).
    private static List<ReviewCandidate> ParseCandidates(string lesson, string step, JsonElement element, PhraseSheet sheet, IReadOnlyList<SpeechLine> speech, IReadOnlyDictionary<string, PromptSpec> prompts)
    {
        if (!element.TryGetProperty("candidates", out var listed) || listed.ValueKind != JsonValueKind.Array || listed.GetArrayLength() == 0)
        {
            throw Bad(lesson, $"step '{step}' is a review and needs a non-empty \"candidates\" list");
        }

        var candidates = new List<ReviewCandidate>();
        foreach (var item in listed.EnumerateArray())
        {
            var (phrase, prompt) = PhraseAndPrompt(lesson, step, item, sheet, prompts);
            if (candidates.Any(c => string.Equals(c.Phrase, phrase, StringComparison.Ordinal)))
            {
                throw Bad(lesson, $"step '{step}' lists the phrase '{phrase}' twice");
            }

            candidates.Add(new ReviewCandidate(phrase, prompt, LineOf(lesson, step, Text(item, "line"), speech), Text(item, "translation")));
        }

        return candidates;
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
