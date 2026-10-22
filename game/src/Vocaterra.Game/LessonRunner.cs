using System.Globalization;

namespace Vocaterra.Game;

/// <summary>Where a lesson stands.</summary>
public enum LessonStatus
{
    /// <summary>Not begun.</summary>
    NotStarted,

    /// <summary>Begun and not finished (it may be paused: the panel is closed, or the learner left it).</summary>
    InProgress,

    /// <summary>Every step ended (done, skipped or moved on from).</summary>
    Completed,
}

/// <summary>
/// What the lesson strip draws for the lesson step in progress: plain values, no engine type. The English is the step's text, in its own colour; the
/// Spanish is the line to hear or the phrase to say.
/// </summary>
/// <param name="LessonId">The lesson's id.</param>
/// <param name="Title">The lesson's English title.</param>
/// <param name="StepNumber">The step's number from 1.</param>
/// <param name="StepCount">The number of steps.</param>
/// <param name="Type">What the step asks.</param>
/// <param name="Heading">The step's kind in English (<c>Say it</c>).</param>
/// <param name="English">The step's English text.</param>
/// <param name="Spanish">The Spanish line (the NPC's line, or the phrase to say), or <see langword="null"/>.</param>
/// <param name="Translation">The meaning of the Spanish line in English, or <see langword="null"/>.</param>
/// <param name="Hint">The English hint after the help path or a retry, or <see langword="null"/>.</param>
/// <param name="Options">For a listen step, the English options.</param>
/// <param name="Picked">For a listen step, the option picked (-1: none yet).</param>
/// <param name="CanContinue">Whether Continue ends the step now.</param>
/// <param name="WaitsOnTurn">Whether the step waits for the learner to say something.</param>
/// <param name="WaitsOnWorld">Whether the step waits for something to happen in the world.</param>
/// <param name="RecapLines">For a recap step, the phrases practised as <c>Hola. = Hello.</c>.</param>
public sealed record LessonView(
    string LessonId,
    string Title,
    int StepNumber,
    int StepCount,
    LessonStepType Type,
    string Heading,
    string English,
    string? Spanish,
    string? Translation,
    string? Hint,
    IReadOnlyList<string> Options,
    int Picked,
    bool CanContinue,
    bool WaitsOnTurn,
    bool WaitsOnWorld,
    IReadOnlyList<string> RecapLines);

/// <summary>A lesson on offer at an NPC: the strip says so and L starts (or resumes) it.</summary>
/// <param name="LessonId">The lesson's id.</param>
/// <param name="Title">The lesson's English title.</param>
/// <param name="English">One line about it.</param>
/// <param name="Resume">Whether the learner has begun it already.</param>
public sealed record LessonOffer(string LessonId, string Title, string English, bool Resume);

/// <summary>One step of a lesson as the record shows it: the step's id and how it ended.</summary>
/// <param name="StepId">The step's id.</param>
/// <param name="Outcome">Done, skipped or moved on.</param>
public sealed record LessonUnitRow(string StepId, StepOutcome Outcome);

/// <summary>
/// One lesson in the learner's record (S19): where it stands, the units (steps) finished so far in the current run, the session it was last completed in,
/// the turns taken at its turn steps, the help used (the English hint, "no sé", the repeats asked for), and the phrases it teaches. The counts are over every
/// run of the lesson; <see cref="Units"/> is the current run's.
/// </summary>
/// <param name="LessonId">The lesson's id.</param>
/// <param name="Title">The lesson's English title.</param>
/// <param name="English">The lesson's English line.</param>
/// <param name="Status">Not started, in progress or completed.</param>
/// <param name="StepCount">The number of steps (units) in the lesson.</param>
/// <param name="Units">The finished steps, in order.</param>
/// <param name="CompletedSession">The play session the lesson was last completed in, or <see langword="null"/> (never, or before this record was kept: a load of a version 1 save).</param>
/// <param name="Turns">The turns the learner took at the lesson's turn steps (a repeat or answer step).</param>
/// <param name="HelpUses">The times the help path was used (the English hint shown, the model line again, the card).</param>
/// <param name="DontKnowUses">The times "no sé" was said or pressed in the lesson.</param>
/// <param name="RepeatUses">The times the learner asked the guide to say the lesson's line again ("Otra vez, por favor", "Más despacio, por favor").</param>
/// <param name="Phrases">The phrases the lesson teaches, in order.</param>
/// <param name="Summary">The lesson's English summary for a completed lesson (what the learner can say now), or <see langword="null"/>.</param>
/// <param name="StepIds">The lesson's step ids in order (the book groups them into units), or <see langword="null"/> for a version 1 load.</param>
public sealed record LessonRecordRow(
    string LessonId,
    string Title,
    string English,
    LessonStatus Status,
    int StepCount,
    IReadOnlyList<LessonUnitRow> Units,
    int? CompletedSession,
    int Turns,
    int HelpUses,
    int DontKnowUses,
    int RepeatUses,
    IReadOnlyList<string> Phrases,
    string? Summary = null,
    IReadOnlyList<string>? StepIds = null);

/// <summary>
/// Runs the lessons of <c>game/lessons.json</c> (S16): a data-driven sequence of steps attached to an NPC. One lesson runs at a time; each step has a skip and a
/// help path; a failed or uncertain turn moves on after the step's attempts (fail-forward, D-game-9: the recognition's trouble is never the learner's failure and
/// nothing is gated behind language, rule 8). The runner is plain state with no engine type, no clock and no random numbers: the session feeds it the turns
/// and world events it applies, and everything it does is a <see cref="LessonEvent"/> in the session's events (the console and the session log).
/// </summary>
/// <remarks>
/// <para>
/// <b>The steps.</b> <c>explain</c>, <c>model</c> and <c>recap</c> end on <see cref="Continue"/>. <c>listen</c> ends on a right pick (then Continue), or moves on after
/// its attempts with the right meaning shown. <c>repeat</c> keeps the phrase's card and audio up and counts the learner's use as a supported use of the phrase
/// (S8); <c>answer</c> shows no card and counts the use by the S8 rules for a phrase the NPC asked (unaided, or supported after help). Both are real turns through the
/// core, evaluated at the step's bundle prompt: a turn the core understood as the phrase ends the step (a slip in it is the phrase tracking's business, not the
/// lesson's); a turn it did not understand, an uncertain recognition or another phrase is an attempt that did not work, and after <see cref="LessonStep.Attempts"/>
/// of them the step moves on. Asking for help or saying "no sé" is never an attempt. <c>do</c> ends on the world event the game raises (a pickup...).
/// </para>
/// <para>
/// <b>Every step can be skipped</b> (<see cref="Skip"/>) <b>and has a help path</b> (<see cref="Help"/>: the step's English hint, its model line again and, for a step with a
/// phrase, the card and the English). Progress is saved with the game (<see cref="Capture"/> and <see cref="Restore"/>).
/// </para>
/// <para>
/// <b>Engaged or not.</b> A lesson only takes over the NPC's conversation while it is <i>engaged</i> (<see cref="Engaged"/>): started with <see cref="Start"/> (the L key) or
/// by itself for a lesson with <c>autoStart</c>. Closing the panel pauses a lesson that is not <c>autoStart</c>; an <c>autoStart</c> lesson engages again the next time
/// the player talks to its NPC. A paused lesson is offered again (<see cref="OfferFor"/>); the NPC's ordinary conversation is never hijacked by a lesson the
/// learner left.
/// </para>
/// </remarks>
public sealed class LessonRunner
{
    private sealed class State
    {
        public LessonStatus Status { get; set; }

        public int Step { get; set; }

        public List<(string StepId, StepOutcome Outcome)> Outcomes { get; } = [];

        public int Attempts { get; set; }

        public bool Helped { get; set; }

        public int Picked { get; set; } = -1;

        public StepOutcome? Pending { get; set; }

        public string? Hint { get; set; }

        // The record of the lesson (S19), over every run of it: the turns that reached its turn steps, the help uses, the "no sé" uses, the
        // repeats asked for, and the session it was last completed in.
        public int Turns { get; set; }

        public int HelpUses { get; set; }

        public int DontKnowUses { get; set; }

        public int RepeatUses { get; set; }

        public int? CompletedSession { get; set; }
    }

    private readonly GuideSession _session;
    private readonly LessonBook _book;
    private readonly EnglishText _english;
    private readonly Dictionary<string, State> _states = new(StringComparer.Ordinal);
    private string? _activeId;
    private bool _engaged;
    private int _version;

    internal LessonRunner(GuideSession session, LessonBook book, EnglishText english)
    {
        _session = session;
        _book = book;
        _english = english;
        foreach (var lesson in book.Lessons)
        {
            _states[lesson.Id] = new State();
        }
    }

    /// <summary>The lessons the runner can run.</summary>
    public LessonBook Book => _book;

    /// <summary>A number that changes whenever what the strip draws changes (a step began or ended, a hint appeared, a pick was made).</summary>
    public int Version => _version;

    /// <summary>
    /// The learner's record of every lesson (S19), in the bundle's lesson order: where each stands, its finished steps, the session it was completed in, the turns
    /// taken and the help used. It is kept by the runner's own events and saved with the game.
    /// </summary>
    public IReadOnlyList<LessonRecordRow> Record => _book.Lessons.Select(lesson =>
    {
        var state = _states[lesson.Id];
        return new LessonRecordRow(
            lesson.Id,
            lesson.Title,
            lesson.English,
            state.Status,
            lesson.Steps.Count,
            state.Outcomes.Select(static o => new LessonUnitRow(o.StepId, o.Outcome)).ToArray(),
            state.CompletedSession,
            state.Turns,
            state.HelpUses,
            state.DontKnowUses,
            state.RepeatUses,
            lesson.TaughtPhrases,
            lesson.Summary,
            lesson.Steps.Select(static step => step.Id).ToArray());
    }).ToArray();

    /// <summary>The lesson that has begun and not finished, or <see langword="null"/>.</summary>
    public LessonDefinition? Active => _activeId is null ? null : _book.Find(_activeId);

    /// <summary>Whether the active lesson has the NPC's conversation (see the type's remarks).</summary>
    public bool Engaged => _engaged && _activeId is not null;

    /// <summary>The step in progress of the active lesson, or <see langword="null"/>.</summary>
    public LessonStep? Current => Active is { } lesson ? lesson.Steps[_states[lesson.Id].Step] : null;

    /// <summary>The number of the step in progress, from 1 (0 when no lesson is active).</summary>
    public int StepNumber => Active is { } lesson ? _states[lesson.Id].Step + 1 : 0;

    /// <summary>Where a lesson stands.</summary>
    /// <param name="lessonId">The lesson's id.</param>
    public LessonStatus StatusOf(string lessonId) => _states.TryGetValue(lessonId, out var state) ? state.Status : LessonStatus.NotStarted;

    /// <summary>How the steps of a lesson ended so far, in order.</summary>
    /// <param name="lessonId">The lesson's id.</param>
    public IReadOnlyList<(string StepId, StepOutcome Outcome)> OutcomesOf(string lessonId) =>
        _states.TryGetValue(lessonId, out var state) ? state.Outcomes.ToArray() : [];

    /// <summary>The tries used on the step in progress.</summary>
    public int Attempts => Active is { } lesson ? _states[lesson.Id].Attempts : 0;

    /// <summary>Whether the help path was used on the step in progress.</summary>
    public bool Helped => Active is { } lesson && _states[lesson.Id].Helped;

    /// <summary>
    /// The lesson on offer at an NPC: its first lesson that is not completed and whose <c>after</c> lesson is, when no lesson is engaged. <see langword="null"/>
    /// when there is none (or a lesson already has the conversation).
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    public LessonOffer? OfferFor(string npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (Engaged)
        {
            return null;
        }

        var lesson = _activeId is not null && _book.Find(_activeId) is { } paused && string.Equals(paused.Npc, npc, StringComparison.Ordinal)
            ? paused
            : Available(npc);
        return lesson is null ? null : new LessonOffer(lesson.Id, lesson.Title, lesson.English, StatusOf(lesson.Id) == LessonStatus.InProgress);
    }

    private LessonDefinition? Available(string npc) =>
        _activeId is not null
            ? null
            : _book.Lessons.FirstOrDefault(l => string.Equals(l.Npc, npc, StringComparison.Ordinal) && StatusOf(l.Id) != LessonStatus.Completed
                && (l.After is null || StatusOf(l.After) == LessonStatus.Completed));

    /// <summary>
    /// Starts a lesson, or takes up the paused one again where it stopped. Refused (<see langword="false"/>) for an unknown lesson or when another lesson is
    /// active. A completed lesson starts again from its first step.
    /// </summary>
    /// <param name="lessonId">The lesson's id.</param>
    public bool Start(string lessonId)
    {
        ArgumentNullException.ThrowIfNull(lessonId);
        if (_book.Find(lessonId) is not { } lesson || (_activeId is not null && !string.Equals(_activeId, lessonId, StringComparison.Ordinal)))
        {
            return false;
        }

        var state = _states[lesson.Id];
        if (_activeId is null)
        {
            if (state.Status == LessonStatus.Completed)
            {
                state.Outcomes.Clear();
                state.Step = 0;
            }

            _activeId = lesson.Id;
            state.Status = LessonStatus.InProgress;
            _engaged = true;
            Emit(LessonEventKind.LessonStarted, lesson, null, null, null, state.Step > 0 ? "resumed" : null);
            Enter(lesson, state);
            return true;
        }

        if (!_engaged)
        {
            _engaged = true;
            Enter(lesson, state); // taken up again after a pause: the same step, as new
        }

        return true;
    }

    /// <summary>What the lesson strip draws now, or <see langword="null"/> when no lesson is engaged.</summary>
    public LessonView? View
    {
        get
        {
            if (!Engaged || Active is not { } lesson)
            {
                return null;
            }

            var state = _states[lesson.Id];
            var step = lesson.Steps[state.Step];
            var spanish = step.Line?.Text ?? (step.Phrase is { } phrase ? _session.Phrases.Find(phrase)?.Spanish : null);
            var heading = _english.Ui(step.Type switch
            {
                LessonStepType.Explain => "stepExplain",
                LessonStepType.Model => "stepModel",
                LessonStepType.Listen => "stepListen",
                LessonStepType.Repeat => "stepRepeat",
                LessonStepType.Answer => "stepAnswer",
                LessonStepType.Do => "stepDo",
                _ => "stepRecap",
            });
            return new LessonView(
                lesson.Id,
                lesson.Title,
                state.Step + 1,
                lesson.Steps.Count,
                step.Type,
                heading,
                step.English,
                spanish,
                step.Translation,
                state.Hint,
                step.Options,
                state.Picked,
                state.Pending is not null,
                step.WaitsOnTurn,
                step.Type == LessonStepType.Do,
                step.Type == LessonStepType.Recap ? RecapLines(lesson, step) : []);
        }
    }

    /// <summary>
    /// The short English line the objective area shows while an engaged lesson waits on a <c>do</c> step that has one (S17: "Pick a fruit from a berry bush, then
    /// go back to Rosa"), or <see langword="null"/>.
    /// </summary>
    public string? HudLine => Engaged && Current is { Type: LessonStepType.Do, Hud: { } hud } ? hud : null;

    /// <summary>
    /// The prompt the next line the learner sends is evaluated at, while the step in progress waits on a turn and the panel is open on the lesson's NPC; otherwise
    /// <see langword="null"/> (the NPC's own prompt).
    /// </summary>
    public string? ActivePrompt => TurnStep()?.Prompt;

    /// <summary>The phrase the step in progress has in play, for the support state: the card is up for a <c>repeat</c> step and down for an <c>answer</c> step.</summary>
    public LessonFocus? Focus => TurnStep() is { Phrase: { } phrase } step ? new LessonFocus(phrase, step.Type == LessonStepType.Repeat) : null;

    /// <summary>Whether the help and "no sé" buttons belong to the lesson now (a lesson is engaged and the panel is open on its NPC).</summary>
    public bool HandlesHelp => Engaged && Active is { } lesson && _session.TalkOpenOn(lesson.Npc);

    /// <summary>What a Spanish line of a lesson means in English (the translation of the <c>model</c>, <c>listen</c> or <c>answer</c> step that plays it), or <see langword="null"/>.</summary>
    /// <param name="spanish">The line's text.</param>
    public string? TranslationOf(string spanish)
    {
        ArgumentNullException.ThrowIfNull(spanish);
        var plain = PhraseSheet.Plain(spanish);
        return _book.Lessons
            .SelectMany(static l => l.Steps)
            .FirstOrDefault(s => s.Line is not null && s.Translation is not null && string.Equals(PhraseSheet.Plain(s.Line.Text), plain, StringComparison.Ordinal))
            ?.Translation;
    }

    // The step in progress when it waits on a turn, the lesson is engaged and the panel is open on its NPC.
    private LessonStep? TurnStep() =>
        Engaged && Current is { WaitsOnTurn: true } step && Active is { } lesson && _session.TalkOpenOn(lesson.Npc) ? step : null;

    // ---- What the learner does.

    /// <summary>Ends the step in progress when it is waiting for Continue (explain, model, recap, and a listen step that was picked). Returns whether it did.</summary>
    public bool Continue()
    {
        if (!Engaged || Active is not { } lesson)
        {
            return false;
        }

        var state = _states[lesson.Id];
        if (state.Pending is not { } outcome)
        {
            return false;
        }

        Finish(lesson, state, outcome, null);
        return true;
    }

    /// <summary>Skips the step in progress (every step can be skipped). Returns whether a lesson was engaged.</summary>
    public bool Skip()
    {
        if (!Engaged || Active is not { } lesson)
        {
            return false;
        }

        Finish(lesson, _states[lesson.Id], StepOutcome.Skipped, null);
        return true;
    }

    /// <summary>
    /// The help path of the step in progress: its English hint, its model line again and, for a step with a phrase, the phrase's card and English (the help key shows
    /// the English at every rung). Never an attempt, never a failure. Returns whether a lesson was engaged.
    /// </summary>
    public bool Help()
    {
        if (!Engaged || Active is not { } lesson)
        {
            return false;
        }

        var state = _states[lesson.Id];
        var step = lesson.Steps[state.Step];
        state.Helped = true;
        state.HelpUses++;
        state.Hint = step.Help ?? _english.Ui("helpDefault");
        Emit(LessonEventKind.Help, lesson, state, step, null, null);
        if (step.Line is { } line)
        {
            _session.Support.Enqueue(new AudioCue(line.Speaker, line.Text, line.LineId, "lesson-help"));
        }

        if (step.WaitsOnTurn)
        {
            _session.RevealLessonSupport();
        }

        _version++;
        return true;
    }

    /// <summary>
    /// "No sé" during the lesson: the help path, and for a step with a phrase a "no sé" use of it in the phrase tracking (the ladder steps up). Never an attempt.
    /// Returns whether a lesson was engaged.
    /// </summary>
    public bool DontKnow()
    {
        if (!Engaged || Active is not { } lesson)
        {
            return false;
        }

        var step = lesson.Steps[_states[lesson.Id].Step];
        _states[lesson.Id].DontKnowUses++;
        _session.LogVoice(VoiceEventKind.NoSe);
        if (step.WaitsOnTurn && step.Phrase is { } phrase)
        {
            _session.LogLessonDontKnow(lesson.Npc, step.Prompt ?? string.Empty, phrase);
        }

        return Help();
    }

    /// <summary>
    /// Picks an option of a listen step (0-based). A right pick ends the step once the learner continues; a wrong one is an attempt, and when the attempts run out the
    /// step shows the right meaning and moves on at Continue. Returns whether the pick was taken.
    /// </summary>
    /// <param name="option">The index in the step's options.</param>
    public bool Pick(int option)
    {
        if (!Engaged || Active is not { } lesson)
        {
            return false;
        }

        var state = _states[lesson.Id];
        var step = lesson.Steps[state.Step];
        if (step.Type != LessonStepType.Listen || state.Pending is not null || option < 0 || option >= step.Options.Count)
        {
            return false;
        }

        state.Picked = option;
        if (option == step.Correct)
        {
            state.Pending = StepOutcome.Done;
            state.Hint = null;
        }
        else
        {
            state.Attempts++;
            if (state.Attempts >= step.Attempts)
            {
                state.Pending = StepOutcome.MovedOn;
                state.Hint = $"{step.Translation} ({step.Options[step.Correct]})";
            }
            else
            {
                state.Picked = -1;
                state.Hint = step.Retry ?? _english.Ui("retry");
            }

            Emit(LessonEventKind.Retry, lesson, state, step, null, $"pick {(option + 1).ToString(CultureInfo.InvariantCulture)}");
        }

        _version++;
        return true;
    }

    /// <summary>
    /// Whether a lesson has, or is about to take, this NPC's conversation: one is engaged at the NPC, or an <c>autoStart</c> lesson is active or available there. The
    /// guide then opens with the lesson and not with a review question of a due phrase or word (the two would both be waiting for the learner's next line).
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    public bool OwnsConversation(string npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (_activeId is not null)
        {
            return _book.Find(_activeId) is { } active && string.Equals(active.Npc, npc, StringComparison.Ordinal) && (_engaged || active.AutoStart);
        }

        return Available(npc) is { AutoStart: true };
    }

    // ---- What the session tells the runner.

    // The lesson unit ("lesson/step") a line said to this NPC now lands on: the engaged lesson's step that waits on a turn, at that NPC; otherwise null.
    internal string? UnitFor(string npc) =>
        Engaged && Active is { } lesson && Current is { WaitsOnTurn: true } step && string.Equals(lesson.Npc, npc, StringComparison.Ordinal)
            ? $"{lesson.Id}/{step.Id}"
            : null;

    // The learner asked the lesson's NPC to say her last line again, while the lesson has her conversation (S19's repeats).
    internal void NoteRepeat(string npc)
    {
        if (Engaged && Active is { } lesson && string.Equals(lesson.Npc, npc, StringComparison.Ordinal))
        {
            _states[lesson.Id].RepeatUses++;
        }
    }

    internal void OnTalkOpened(string npc)
    {
        if (Active is { } lesson && string.Equals(lesson.Npc, npc, StringComparison.Ordinal))
        {
            if (lesson.AutoStart && !_engaged)
            {
                _engaged = true;
                Enter(lesson, _states[lesson.Id]);
            }
            else if (_engaged)
            {
                _session.RefreshLessonSupport();
            }

            return;
        }

        if (_activeId is null && Available(npc) is { AutoStart: true } auto)
        {
            _ = Start(auto.Id);
        }
    }

    internal void OnTalkClosed()
    {
        if (_engaged && Active is { AutoStart: false })
        {
            _engaged = false; // paused: the NPC's ordinary conversation is not taken over by a lesson the learner left
            _version++;
        }
    }

    // A turn the session applied. Only a turn at the lesson's NPC, while a repeat or answer step waits, is the lesson's. Returns whether the lesson took the
    // turn (the core understood it): the lesson then speaks for the guide, so her ordinary reply is not shown (S17). A turn the core did not understand, or that
    // the recognizer was unsure of, is not taken: her not-understood line and the help lines stay.
    internal bool OnTurn(GuideTurn turn)
    {
        if (!Engaged || Active is not { } lesson || !string.Equals(turn.Npc, lesson.Npc, StringComparison.Ordinal))
        {
            return false;
        }

        var state = _states[lesson.Id];
        var step = lesson.Steps[state.Step];
        if (!step.WaitsOnTurn)
        {
            return false;
        }

        state.Turns++;
        if (turn.Communicated && string.Equals(turn.Intent, "dont_know", StringComparison.Ordinal))
        {
            state.DontKnowUses++; // "no sé" said at the turn step, whether it is the step's phrase or a help request (S19)
        }

        var taken = true;
        if (turn.Unsure || turn.Uncertain || !turn.Communicated)
        {
            Miss(lesson, state, step, turn.Unsure || (turn.Uncertain && turn.Input == InputKind.Spoken) ? "uncertain recognition" : "not understood");
            taken = false;
        }
        else if (IsServiceIntent(turn.Intent) && !MatchesPhrase(turn, step.Phrase!))
        {
            state.Helped = true;
            state.HelpUses++;
            state.Hint = step.Help ?? _english.Ui("helpDefault");
            Emit(LessonEventKind.Help, lesson, state, step, null, turn.Intent);
            _version++;
        }
        else if (turn.Choice)
        {
            Finish(lesson, state, StepOutcome.MovedOn, "picked among the offered choices"); // recognition only: the lesson goes on, the phrase tracking counts it as a choice
        }
        else if (MatchesPhrase(turn, step.Phrase!))
        {
            Finish(lesson, state, StepOutcome.Done, turn.Errors.Count == 0 ? null : "with a slip");
        }
        else
        {
            Miss(lesson, state, step, $"said {turn.Intent}");
        }

        return taken;
    }

    // "Ayuda" and "no sé" ask for help and are never an attempt, except in the step that teaches the phrase itself (S17: "No sé" is a lesson phrase).
    private static bool IsServiceIntent(string intent) =>
        string.Equals(intent, "ask_help", StringComparison.Ordinal) || string.Equals(intent, "dont_know", StringComparison.Ordinal);

    // A world event the session logged: a do step waits for it.
    internal void OnEvent(GameEvent gameEvent)
    {
        if (!Engaged || Active is not { } lesson || gameEvent is not WorldEvent world)
        {
            return;
        }

        var state = _states[lesson.Id];
        var step = lesson.Steps[state.Step];
        if (step.Type != LessonStepType.Do || !string.Equals(step.Event, world.Name, StringComparison.Ordinal))
        {
            return;
        }

        if (step.Detail is { } wanted && (world.Detail is null || !world.Detail.Contains(wanted, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Finish(lesson, state, StepOutcome.Done, world.Detail);
    }

    // ---- The save (S10).

    internal LessonsSave Capture() => new(
        _book.Lessons.Select(lesson =>
        {
            var state = _states[lesson.Id];
            return new LessonProgressSave(
                lesson.Id,
                state.Status,
                state.Step,
                state.Outcomes.Select(static o => new StepOutcomeSave(o.StepId, o.Outcome)).ToArray(),
                state.Attempts,
                state.Helped,
                state.Turns,
                state.HelpUses,
                state.DontKnowUses,
                state.RepeatUses,
                state.CompletedSession);
        }).ToArray(),
        _activeId);

    internal void Restore(LessonsSave save)
    {
        var active = 0;
        foreach (var progress in save.Lessons)
        {
            if (_book.Find(progress.Id) is not { } lesson || !_states.TryGetValue(progress.Id, out var state))
            {
                throw new ArgumentException($"The lesson '{progress.Id}' is not one of the game's.", nameof(save));
            }

            if (progress.Step < 0 || progress.Step >= lesson.Steps.Count || progress.Outcomes.Count > lesson.Steps.Count)
            {
                throw new ArgumentException($"The lesson '{progress.Id}' is saved at a step it does not have.", nameof(save));
            }

            state.Status = progress.Status;
            state.Step = progress.Step;
            state.Attempts = progress.Attempts;
            state.Helped = progress.Helped;
            state.Turns = progress.Turns;
            state.HelpUses = progress.HelpUses;
            state.DontKnowUses = progress.DontKnowUses;
            state.RepeatUses = progress.RepeatUses;
            state.CompletedSession = progress.CompletedSession;
            state.Picked = -1;
            state.Pending = null;
            state.Hint = null;
            state.Outcomes.Clear();
            state.Outcomes.AddRange(progress.Outcomes.Select(static o => (o.StepId, o.Outcome)));
            active += progress.Status == LessonStatus.InProgress ? 1 : 0;
        }

        if (save.Active is { } id && (_book.Find(id) is null || StatusOf(id) != LessonStatus.InProgress || active != 1))
        {
            throw new ArgumentException($"The active lesson '{id}' is not an unfinished lesson of the game's.", nameof(save));
        }

        _activeId = save.Active;
        _engaged = false;
        _version++;
    }

    // ---- Steps.

    // A step begins: its state is fresh, its sounds are queued and the support state follows.
    private void Enter(LessonDefinition lesson, State state)
    {
        var step = lesson.Steps[state.Step];
        state.Attempts = 0;
        state.Helped = false;
        state.Picked = -1;
        state.Hint = null;
        state.Pending = step.Type is LessonStepType.Explain or LessonStepType.Model or LessonStepType.Recap ? StepOutcome.Done : null;
        Emit(LessonEventKind.StepStarted, lesson, state, step, null, step.Phrase ?? step.Line?.LineId ?? step.Event);
        if (step.Line is { } line)
        {
            _session.Support.Enqueue(new AudioCue(line.Speaker, line.Text, line.LineId, "lesson"));
            _session.NoteNpcLine(lesson.Npc, line.Text); // what "Otra vez, por favor" repeats (S17)
        }

        if (step.Point is { } noun)
        {
            _session.LessonPointAt(lesson.Npc, noun); // a do step may send the beacon to what the learner is to pick up (S17)
        }

        _version++;
        _session.RefreshLessonSupport();
    }

    private void Finish(LessonDefinition lesson, State state, StepOutcome outcome, string? detail)
    {
        var step = lesson.Steps[state.Step];
        state.Outcomes.Add((step.Id, outcome));
        Emit(LessonEventKind.StepDone, lesson, state, step, outcome, detail);
        if (state.Step + 1 < lesson.Steps.Count)
        {
            state.Step++;
            Enter(lesson, state);
            return;
        }

        state.Status = LessonStatus.Completed;
        state.CompletedSession = _session.PlaySession;
        _activeId = null;
        _engaged = false;
        var done = state.Outcomes.Count(static o => o.Outcome == StepOutcome.Done);
        var skipped = state.Outcomes.Count(static o => o.Outcome == StepOutcome.Skipped);
        var moved = state.Outcomes.Count(static o => o.Outcome == StepOutcome.MovedOn);
        Emit(LessonEventKind.LessonCompleted, lesson, null, null, null, string.Create(CultureInfo.InvariantCulture, $"done {done}, skipped {skipped}, moved on {moved}"));
        _version++;
        _session.RefreshLessonSupport();
    }

    // An attempt that did not work: try again, or after the step's attempts move on (fail-forward).
    private void Miss(LessonDefinition lesson, State state, LessonStep step, string reason)
    {
        state.Attempts++;
        if (state.Attempts >= step.Attempts)
        {
            Finish(lesson, state, StepOutcome.MovedOn, reason);
            return;
        }

        state.Hint = step.Retry ?? _english.Ui("retry");
        Emit(LessonEventKind.Retry, lesson, state, step, null, reason);
        _version++;
    }

    // Whether the core understood the line as the step's phrase: the intent of its row. Phrases that share an intent (Sí and Sí, por favor) are one answer to
    // the core, and the step's own phrase is the one the tracking credits (GuideSession.PhraseInPlayFor).
    private bool MatchesPhrase(GuideTurn turn, string phraseId) =>
        _session.Phrases.Find(phraseId) is { Intent: { } intent } && string.Equals(intent, turn.Intent, StringComparison.Ordinal);

    private IReadOnlyList<string> RecapLines(LessonDefinition lesson, LessonStep step)
    {
        var ids = step.Phrases.Count > 0 ? step.Phrases : lesson.TaughtPhrases;
        var lines = new List<string>();
        foreach (var id in ids)
        {
            if (_session.Phrases.Find(id) is { } row)
            {
                lines.Add($"{row.Spanish} = {row.English}");
            }
        }

        return lines;
    }

    private void Emit(LessonEventKind kind, LessonDefinition lesson, State? state, LessonStep? step, StepOutcome? outcome, string? detail) =>
        _session.LogEvent(new LessonEvent(
            _session.CurrentTick,
            _session.StoredTime.UnixSeconds,
            kind,
            lesson.Id,
            state is null ? 0 : state.Step + 1,
            step?.Id ?? string.Empty,
            step is null ? string.Empty : LessonStepTypes.Of(step.Type),
            outcome,
            detail));
}
