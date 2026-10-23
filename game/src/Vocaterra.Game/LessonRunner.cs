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
/// <param name="Warmup">Whether it is the review deck's warm-up (X06) and not a lesson: the strip then offers Start and Skip.</param>
public sealed record LessonOffer(string LessonId, string Title, string English, bool Resume, bool Warmup = false);

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

        // The review step in progress, resolved to the candidate it asks (X06): the step with that candidate's phrase, line, prompt and translation. Null for any other step.
        public LessonStep? Asked { get; set; }

        // The record of the lesson (S19), over every run of it: the turns that reached its turn steps, the help uses, the "no sé" uses, the
        // repeats asked for, and the session it was last completed in.
        public int Turns { get; set; }

        public int HelpUses { get; set; }

        public int DontKnowUses { get; set; }

        public int RepeatUses { get; set; }

        public int? CompletedSession { get; set; }

        // Whether the lesson was ever completed (S26): a replay puts the lesson back in progress, and what unlocks after it, and the objective, must not forget.
        public bool Done { get; set; }
    }

    private readonly GuideSession _session;
    private readonly LessonBook _book;
    private readonly EnglishText _english;
    private readonly Dictionary<string, State> _states = new(StringComparer.Ordinal);
    private string? _activeId;
    private string? _replayId;
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
    public LessonStep? Current => Active is { } lesson ? StepOf(lesson, _states[lesson.Id]) : null;

    /// <summary>The number of the step in progress, from 1 (0 when no lesson is active).</summary>
    public int StepNumber => Active is { } lesson ? _states[lesson.Id].Step + 1 : 0;

    /// <summary>Where a lesson stands.</summary>
    /// <param name="lessonId">The lesson's id.</param>
    public LessonStatus StatusOf(string lessonId) => _states.TryGetValue(lessonId, out var state) ? state.Status : LessonStatus.NotStarted;

    /// <summary>Whether the lesson was completed at some time (S26): true while a replay of it is under way too.</summary>
    /// <param name="lessonId">The lesson's id.</param>
    public bool IsDone(string lessonId) => _states.TryGetValue(lessonId, out var state) && (state.Done || state.Status == LessonStatus.Completed);

    /// <summary>Whether the play session has reached the session a lesson unlocks in, and the lesson it comes after is done (S26).</summary>
    /// <param name="lesson">The lesson.</param>
    public bool IsUnlocked(LessonDefinition lesson)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        return _session.PlaySession >= lesson.Session && (lesson.After is null || IsDone(lesson.After));
    }

    /// <summary>The id of the lesson the learner asked to practise again from the book, which starts when they talk to its NPC; <see langword="null"/> when none.</summary>
    public string? ReplayPending => _replayId;

    /// <summary>
    /// The lesson of a later session that is unlocked and not begun (S26): the session's new lesson, whose <see cref="LessonDefinition.Objective"/> the objective area shows,
    /// or <see langword="null"/> when there is none. Lessons of session 1 are not new lessons: the opening sends the player to the guide.
    /// </summary>
    public LessonDefinition? NewLesson =>
        _activeId is not null ? null : _book.Lessons.Where(l => l.Session > 1 && l.Objective is not null && _states[l.Id].Status == LessonStatus.NotStarted && IsUnlocked(l)).OrderByDescending(static l => l.Session).FirstOrDefault();

    /// <summary>
    /// Asks to practise a completed lesson again (the book's "Practise again", S26). The lesson starts when the learner talks to its NPC, from its first step; its progress
    /// stays "done" meanwhile. Refused (<see langword="false"/>) for a lesson that was never completed, an unknown lesson, or while another lesson is under way.
    /// </summary>
    /// <param name="lessonId">The lesson's id.</param>
    public bool RequestReplay(string lessonId)
    {
        ArgumentNullException.ThrowIfNull(lessonId);
        if (_book.Find(lessonId) is not { } lesson || !IsDone(lessonId) || (_activeId is not null && !string.Equals(_activeId, lessonId, StringComparison.Ordinal)))
        {
            return false;
        }

        if (string.Equals(_activeId, lessonId, StringComparison.Ordinal))
        {
            return true; // already under way: nothing to ask for
        }

        _replayId = lesson.Id;
        _session.LogEvent(new LessonEvent(_session.CurrentTick, _session.StoredTime.UnixSeconds, LessonEventKind.ReplayRequested, lesson.Id, 0, string.Empty, string.Empty, null, null));
        _session.LessonReplayAsked(lesson.Npc);
        _version++;
        return true;
    }

    /// <summary>The English objective line for a lesson practised again (S26): where to go and which lesson, or <see langword="null"/> when none is pending.</summary>
    public string? ReplayHud => _replayId is { } id && _book.Find(id) is { } lesson && _activeId is null
        ? string.Create(CultureInfo.InvariantCulture, $"Practise again: talk to {_english.NpcName(lesson.Npc).ToLowerInvariant()} to start \u201c{lesson.Title}\u201d")
        : null;

    /// <summary>The NPC the step in progress is said at: the step's own, or its lesson's; <see langword="null"/> when no lesson is active.</summary>
    public string? CurrentNpc => Active is { } lesson && Current is { } step ? lesson.NpcOf(step) : null;

    /// <summary>
    /// The NPC the learner is to be talking to for the step in progress (S26): the NPC a <c>do</c> step walks them to (<see cref="LessonStep.At"/>), else <see cref="CurrentNpc"/>.
    /// The scripted run walks there.
    /// </summary>
    public string? TalkTarget => Current is { At: { } at } ? at : CurrentNpc;

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

        var lesson = _activeId is not null && _book.Find(_activeId) is { } paused && string.Equals(CurrentNpcOf(paused), npc, StringComparison.Ordinal)
            ? paused
            : Available(npc);
        return lesson is null ? null : new LessonOffer(lesson.Id, lesson.Title, lesson.English, StatusOf(lesson.Id) == LessonStatus.InProgress);
    }

    // The lesson that can start at an NPC now (S26): the one the learner asked to practise again, else the lesson attached to it that is not completed, whose session has
    // come and whose earlier lesson is done, the newest session first (S27).
    private LessonDefinition? Available(string npc)
    {
        if (_activeId is not null)
        {
            return null;
        }

        if (_replayId is { } replay && _book.Find(replay) is { } again && string.Equals(again.Npc, npc, StringComparison.Ordinal))
        {
            return again;
        }

        // The newest session's lesson first (S27): a learner who reaches session 3 without having done session 2's lesson is offered session 3's, and the older one stays on offer after it.
        // Every lesson is written to stand on its own (it teaches the forms it uses).
        return _book.Lessons
            .Where(l => string.Equals(l.Npc, npc, StringComparison.Ordinal) && StatusOf(l.Id) != LessonStatus.Completed && IsUnlocked(l))
            .OrderByDescending(static l => l.Session)
            .FirstOrDefault();
    }

    // The NPC the active lesson's step in progress is said at.
    private string CurrentNpcOf(LessonDefinition lesson) => lesson.NpcOf(lesson.Steps[_states[lesson.Id].Step]);

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
                state.Done = true; // a replay: the lesson stays done while it runs again (S26)
                state.Outcomes.Clear();
                state.Step = 0;
            }

            if (string.Equals(_replayId, lesson.Id, StringComparison.Ordinal))
            {
                _replayId = null;
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
            var step = StepOf(lesson, state);
            var spanish = step.Line?.Text ?? (step.Phrase is { } phrase ? _session.Phrases.Find(phrase)?.Spanish : null);
            var heading = _english.Ui(step.Type switch
            {
                LessonStepType.Explain => "stepExplain",
                LessonStepType.Model => "stepModel",
                LessonStepType.Listen => "stepListen",
                LessonStepType.Repeat => "stepRepeat",
                LessonStepType.Answer => "stepAnswer",
                LessonStepType.Do => "stepDo",
                LessonStepType.Review => "stepReview",
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

    /// <summary>The phrase the step in progress has in play, for the support state: the card is up for a <c>repeat</c> step and down for an <c>answer</c> step, and a <c>review</c> step follows the phrase's rung.</summary>
    public LessonFocus? Focus => TurnStep() is { Phrase: { } phrase } step ? new LessonFocus(phrase, step.Type == LessonStepType.Repeat, step.Type == LessonStepType.Review) : null;

    /// <summary>Whether the help and "no sé" buttons belong to the lesson now (a lesson is engaged and the panel is open on its NPC).</summary>
    public bool HandlesHelp => Engaged && CurrentNpc is { } npc && _session.TalkOpenOn(npc);

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
        Engaged && Current is { WaitsOnTurn: true } step && CurrentNpc is { } npc && _session.TalkOpenOn(npc) ? step : null;

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
        var step = StepOf(lesson, state);
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

        var step = StepOf(lesson, _states[lesson.Id]);
        _states[lesson.Id].DontKnowUses++;
        _session.LogVoice(VoiceEventKind.NoSe);
        if (step.WaitsOnTurn && step.Phrase is { } phrase)
        {
            _session.LogLessonDontKnow(lesson.NpcOf(step), step.Prompt ?? string.Empty, phrase);
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
            return _book.Find(_activeId) is { } active && (_engaged || active.AutoStart)
                && (string.Equals(CurrentNpcOf(active), npc, StringComparison.Ordinal) || Current is { At: { } at } && string.Equals(at, npc, StringComparison.Ordinal)); // or the NPC a do step walks the learner to (S26)
        }

        return Available(npc) is { } next && (next.AutoStart || string.Equals(_replayId, next.Id, StringComparison.Ordinal));
    }

    // ---- What the session tells the runner.

    // The lesson unit ("lesson/step") a line said to this NPC now lands on: the engaged lesson's step that waits on a turn, at that NPC; otherwise null.
    internal string? UnitFor(string npc) =>
        Engaged && Active is { } lesson && Current is { WaitsOnTurn: true } step && string.Equals(lesson.NpcOf(step), npc, StringComparison.Ordinal)
            ? $"{lesson.Id}/{step.Id}"
            : null;

    // The learner asked the lesson's NPC to say her last line again, while the lesson has her conversation (S19's repeats).
    internal void NoteRepeat(string npc)
    {
        if (Engaged && Active is { } lesson && string.Equals(CurrentNpcOf(lesson), npc, StringComparison.Ordinal))
        {
            _states[lesson.Id].RepeatUses++;
        }
    }

    internal void OnTalkOpened(string npc)
    {
        if (Active is { } lesson && string.Equals(CurrentNpcOf(lesson), npc, StringComparison.Ordinal))
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

        if (_activeId is null && Available(npc) is { } next && (next.AutoStart || string.Equals(_replayId, next.Id, StringComparison.Ordinal)))
        {
            _ = Start(next.Id); // a lesson that starts by itself, or one the learner asked to practise again (S26)
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
        if (!Engaged || Active is not { } lesson || !string.Equals(turn.Npc, CurrentNpcOf(lesson), StringComparison.Ordinal))
        {
            return false;
        }

        var state = _states[lesson.Id];
        var step = StepOf(lesson, state);
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
            if (FormMiss(step, turn) is { } miss)
            {
                // The answer is the right phrase with another person's or another tense's verb and no error for the core to flag (no subject pronoun said, no time word): quieres for quiero,
                // or quiero at "¿Qué quieren ustedes?" (S26); comí at "¿Qué vas a comer?" (S27). It is a miss, and the retry names the ending or the form. A wrong person next to a
                // pronoun (yo quieres) or a tense next to a time word (mañana comí) is the core's conj.*: a slip, the step ends.
                Miss(lesson, state, step, miss.Reason, miss.Retry);
            }
            else
            {
                var plan = PresentForPlan(step, turn);
                Finish(lesson, state, StepOutcome.Done, plan ? "present for a plan, credited" : turn.Errors.Count == 0 ? null : "with a slip");
                if (plan && step.PresentNote is { } note && _activeId is not null)
                {
                    // Correct Spanish is never a miss (S27): the plan said in the present is credited, and the next step opens with a gentle note naming the form this lesson practises.
                    state.Hint = note;
                    _version++;
                }
            }
        }
        else
        {
            Miss(lesson, state, step, $"said {turn.Intent}");
        }

        return taken;
    }

    /// <summary>
    /// Whether the lesson will count this turn as a miss for the person or the tense of its verb (S26, S27), without changing anything. The session asks before it credits the phrase
    /// the turn used, so a wrong-person or wrong-tense answer is not credited as a use of the phrase it was not.
    /// </summary>
    /// <param name="turn">The turn the session applied.</param>
    internal bool IsFormMiss(GuideTurn turn)
    {
        if (!Engaged || Active is not { } lesson || !string.Equals(turn.Npc, CurrentNpcOf(lesson), StringComparison.Ordinal))
        {
            return false;
        }

        var step = StepOf(lesson, _states[lesson.Id]);
        return step.WaitsOnTurn
            && turn.Communicated && !turn.Unsure && !turn.Uncertain && !turn.Choice
            && !IsServiceIntent(turn.Intent)
            && MatchesPhrase(turn, step.Phrase!)
            && FormMiss(step, turn) is not null;
    }

    // The reason and the retry line when the answer is the step's phrase with the wrong person or tense of verb, else null. Person first, then tense.
    private static (string Reason, string? Retry)? FormMiss(LessonStep step, GuideTurn turn)
    {
        if (step.Person is { } person && WrongPerson(turn, person) is { } said)
        {
            return ($"wrong person: said {said}, expected {person}", step.PersonRetry);
        }

        if (step.Tense is { } tense && WrongTense(turn, tense) is { } saidTense)
        {
            return ($"wrong tense: said {saidTense}, expected {tense}", step.TenseRetry);
        }

        return null;
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

        if (step.At is { } wantedNpc && world.Npc is not null && !string.Equals(world.Npc, wantedNpc, StringComparison.Ordinal))
        {
            return; // a do step that waits for the panel to open on an NPC (S26: the walk from the villager to the stall) ignores another NPC's
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
                state.CompletedSession,
                state.Done || state.Status == LessonStatus.Completed);
        }).ToArray(),
        _activeId,
        _replayId);

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
            state.Done = progress.EverCompleted ?? progress.Status == LessonStatus.Completed; // a save before S26 has no flag: a completed lesson was done
            state.Picked = -1;
            state.Pending = null;
            state.Hint = null;
            state.Asked = null;
            state.Outcomes.Clear();
            state.Outcomes.AddRange(progress.Outcomes.Select(static o => (o.StepId, o.Outcome)));
            active += progress.Status == LessonStatus.InProgress ? 1 : 0;
        }

        if (save.Active is { } id && (_book.Find(id) is null || StatusOf(id) != LessonStatus.InProgress || active != 1))
        {
            throw new ArgumentException($"The active lesson '{id}' is not an unfinished lesson of the game's.", nameof(save));
        }

        if (save.Replay is { } replay && (_book.Find(replay) is null || !IsDone(replay) || save.Active is not null))
        {
            throw new ArgumentException($"The lesson '{replay}' is waiting to be practised again, but it is not a done lesson of the game's, or another lesson is active.", nameof(save));
        }

        _activeId = save.Active;
        _replayId = save.Replay;
        _engaged = false;
        _version++;
    }

    // ---- Steps.

    // A step begins: its state is fresh, its sounds are queued and the support state follows.
    private void Enter(LessonDefinition lesson, State state)
    {
        var step = lesson.Steps[state.Step];
        state.Asked = null;
        if (step.Type == LessonStepType.Review)
        {
            // A review step asks the first of its candidates that is due (X06, R07), and is skipped when none is: nothing not yet met, and nothing still inside its spacing gap.
            if (ChooseReview(step) is not { } chosen)
            {
                Finish(lesson, state, StepOutcome.Skipped, "nothing due");
                return;
            }

            step = state.Asked = step with { Phrase = chosen.Phrase, Prompt = chosen.Prompt, Line = chosen.Line, Translation = chosen.Translation };
        }

        state.Attempts = 0;
        state.Helped = false;
        state.Picked = -1;
        state.Hint = null;
        state.Pending = step.Type is LessonStepType.Explain or LessonStepType.Model or LessonStepType.Recap ? StepOutcome.Done : null;
        Emit(LessonEventKind.StepStarted, lesson, state, step, null, step.Phrase ?? step.Line?.LineId ?? step.Event);
        if (step.Line is { } line)
        {
            _session.Support.Enqueue(new AudioCue(line.Speaker, line.Text, line.LineId, "lesson"));
            _session.NoteNpcLine(lesson.NpcOf(step), line.Text); // what "Otra vez, por favor" repeats (S17)
        }

        if (step.Point is { } noun)
        {
            _session.LessonPointAt(lesson.NpcOf(step), noun); // a do step may send the beacon to what the learner is to pick up (S17)
        }

        _version++;
        _session.RefreshLessonSupport();
    }

    // The step in progress: for a review step, the step as resolved to the candidate asked (X06).
    private static LessonStep StepOf(LessonDefinition lesson, State state) => state.Asked ?? lesson.Steps[state.Step];

    // The candidate of a review step that is due now (the stored time, the spacing gaps of D-game-6): the most overdue of those due, the step's order of preference
    // breaking a tie. Null when none is due: the phrase was never credited, its help is gone, or its gap has not passed.
    private ReviewCandidate? ChooseReview(LessonStep step)
    {
        var due = _session.PhraseProgress.DuePhrases(_session.StoredTime.UnixSeconds, _session.Credit);
        ReviewCandidate? best = null;
        var bestOverdue = long.MinValue;
        foreach (var candidate in step.Candidates ?? [])
        {
            var found = due.FirstOrDefault(d => string.Equals(d.PhraseId, candidate.Phrase, StringComparison.Ordinal));
            if (found is not null && found.OverdueSeconds > bestOverdue)
            {
                best = candidate;
                bestOverdue = found.OverdueSeconds;
            }
        }

        return best;
    }

    private void Finish(LessonDefinition lesson, State state, StepOutcome outcome, string? detail)
    {
        var step = StepOf(lesson, state);
        state.Outcomes.Add((step.Id, outcome));
        Emit(LessonEventKind.StepDone, lesson, state, step, outcome, detail);
        if (step.Type == LessonStepType.Review && outcome == StepOutcome.Done)
        {
            _session.NoteReviewed(step.Phrase!, "lesson"); // the summary card counts the phrases reviewed (X06)
        }

        state.Asked = null;
        if (state.Step + 1 < lesson.Steps.Count)
        {
            state.Step++;
            Enter(lesson, state);
            return;
        }

        state.Status = LessonStatus.Completed;
        state.Done = true;
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
    // The person and number (1sg...) of the answer's finite verb when they are not the expected ones and the core found no error to flag, else null. A line with no finite verb
    // is not judged. When the line has several verbs, one with the expected person is enough.
    private static string? WrongPerson(GuideTurn turn, string wanted)
    {
        if (turn.Errors.Any(static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal)) || turn.Words is not { } words)
        {
            return null;
        }

        var verbs = FiniteVerbs(words)
            .Select(static w => w.Person![0] + (w.Number![0] is 'p' or 'P' ? "pl" : "sg"))
            .ToList();
        return verbs.Count == 0 || verbs.Contains(wanted, StringComparer.Ordinal) ? null : verbs[0];
    }

    // The finite verbs of an answer: those with a person and a number that are not infinitives.
    private static List<GuideWord> FiniteVerbs(IReadOnlyList<GuideWord> words) =>
        words.Where(static w => string.Equals(w.Pos, "verb", StringComparison.Ordinal) && w.Person is { Length: > 0 } && w.Number is { Length: > 0 }
            && !string.Equals(w.Mood, "inf", StringComparison.OrdinalIgnoreCase) && !string.Equals(w.Mood, "infinitive", StringComparison.OrdinalIgnoreCase)).ToList();

    // Whether the answer of an ir-a step is a plan said in the present with a future-time word (S27): "mañana voy al río", "mañana como pan". It is correct Spanish, so it is no miss.
    private static bool PresentForPlan(LessonStep step, GuideTurn turn) =>
        string.Equals(step.Tense, TaughtVerbForm.NearFuture, StringComparison.Ordinal) && !turn.Errors.Any(static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal))
        && turn.Words is { } words && PresentForPlan(turn, words, FiniteVerbs(words)) && !HasNearFuture(words, FiniteVerbs(words));

    private static bool PresentForPlan(GuideTurn turn, IReadOnlyList<GuideWord> words, List<GuideWord> finite) =>
        words.Any(static w => string.Equals(w.Text, "mañana", StringComparison.Ordinal)) && finite.Count > 0 && finite.Any(static w => string.Equals(w.Tense, "pres", StringComparison.OrdinalIgnoreCase))
        && !finite.Any(static w => string.Equals(w.Tense, "pret", StringComparison.OrdinalIgnoreCase) || string.Equals(w.Tense, "impf", StringComparison.OrdinalIgnoreCase));

    private static bool HasNearFuture(IReadOnlyList<GuideWord> words, List<GuideWord> finite)
    {
        for (var i = 0; i + 2 < words.Count; i++)
        {
            if (string.Equals(words[i].Lemma, "ir", StringComparison.Ordinal) && finite.Contains(words[i]) && string.Equals(words[i + 1].Text, "a", StringComparison.Ordinal)
                && string.Equals(words[i + 2].Pos, "verb", StringComparison.Ordinal) && (words[i + 2].Mood is "inf" or "infinitive" || words[i + 2].Person is not { Length: > 0 }))
            {
                return true;
            }
        }

        return false;
    }

    // The tense (S27) of the answer's verb when it is not the expected one and the core found no error to flag, else null: the tense said (pres, pret, ...) or "none". The near future
    // (ir-a) is a finite form of ir, then "a", then an infinitive. A line with no finite verb is not judged. When the line has several verbs, one with the expected tense is enough.
    private static string? WrongTense(GuideTurn turn, string wanted)
    {
        if (turn.Errors.Any(static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal)) || turn.Words is not { } words)
        {
            return null;
        }

        var finite = FiniteVerbs(words);
        if (finite.Count == 0)
        {
            return null;
        }

        if (string.Equals(wanted, TaughtVerbForm.NearFuture, StringComparison.Ordinal))
        {
            if (PresentForPlan(turn, words, finite))
            {
                return null; // right Spanish for a plan, said in the present: credited, with a note (see OnTurn)
            }

            var nearFuture = false;
            for (var i = 0; i + 2 < words.Count; i++)
            {
                nearFuture |= string.Equals(words[i].Lemma, "ir", StringComparison.Ordinal) && finite.Contains(words[i]) && string.Equals(words[i + 1].Text, "a", StringComparison.Ordinal)
                    && string.Equals(words[i + 2].Pos, "verb", StringComparison.Ordinal) && (words[i + 2].Mood is "inf" or "infinitive" || words[i + 2].Person is not { Length: > 0 });
            }

            return nearFuture ? null : finite[0].Tense ?? "none";
        }

        return finite.Any(w => string.Equals(w.Tense, wanted, StringComparison.OrdinalIgnoreCase)) ? null : finite[0].Tense ?? "none";
    }

    private void Miss(LessonDefinition lesson, State state, LessonStep step, string reason, string? retry = null)
    {
        state.Attempts++;
        if (state.Attempts >= step.Attempts)
        {
            Finish(lesson, state, StepOutcome.MovedOn, reason);
            return;
        }

        state.Hint = retry ?? step.Retry ?? _english.Ui("retry");
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
