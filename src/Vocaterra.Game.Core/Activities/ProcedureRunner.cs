using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.Activities;

/// <summary>The states of a run (design 10.13).</summary>
public enum ProcedureState
{
    /// <summary>No run: nothing started, or the run was abandoned.</summary>
    NotStarted,

    /// <summary>A run is at a step. Leaving the station keeps it.</summary>
    InRun,

    /// <summary>Every step is done but the bag has no room for the exchange: nothing was used; <see cref="ProcedureRunner.Collect"/> tries again.</summary>
    ReadyToCollect,

    /// <summary>The exchange was made, in the same transaction that set this state; the run grants nothing more.</summary>
    Done,
}

/// <summary>What <see cref="ProcedureRunner.Start"/> did.</summary>
public enum StartOutcome
{
    /// <summary>A new run began.</summary>
    Started,

    /// <summary>The run in progress is this procedure's: it goes on.</summary>
    Resumed,

    /// <summary>The bundle has no such procedure, or it is not done at this station.</summary>
    Unknown,

    /// <summary>Another procedure is running.</summary>
    Busy,

    /// <summary>The procedure names an item the game does not have yet.</summary>
    Unavailable,
}

/// <summary>What an act was (design 10.9: three kinds of wrong act).</summary>
public enum ActOutcome
{
    /// <summary>No run is at a step.</summary>
    NotRunning,

    /// <summary>The act the instruction asked for.</summary>
    Right,

    /// <summary>The right thing and target with another verb (abrir when the instruction says saca).</summary>
    WrongVerb,

    /// <summary>Not the thing or the target of this step.</summary>
    WrongThing,

    /// <summary>A later step's act: Luz says "Primero." and repeats the instruction.</summary>
    OutOfOrder,
}

/// <summary>What the station does after a wrong act (design 10.10): the first slip at the English rung shows the highlight; at the other rungs it repeats, then plays slowly, then shows the highlight (help).</summary>
public enum SlipResponse
{
    /// <summary>Nothing (a right act).</summary>
    None,

    /// <summary>Show the thing to hold and its target.</summary>
    Highlight,

    /// <summary>Play the instruction again.</summary>
    Repeat,

    /// <summary>Play the instruction slowly.</summary>
    Slower,
}

/// <summary>What the station shows for: a step, a chunk (steps folded) or the goal (chunks folded).</summary>
public enum UnitKind
{
    /// <summary>One step's instruction.</summary>
    Step,

    /// <summary>A chunk's one instruction.</summary>
    Chunk,

    /// <summary>The goal alone.</summary>
    Goal,
}

/// <summary>The result of an act (or of collecting).</summary>
/// <param name="Outcome">What the act was.</param>
/// <param name="Response">What the station does after a wrong act.</param>
/// <param name="State">The run's state after the act.</param>
/// <param name="Missing">The nouns of the ingredients gone at the end (their steps are open again), or empty.</param>
/// <param name="Made">How many of the procedure's item the exchange gave, or 0.</param>
public sealed record ActResult(ActOutcome Outcome, SlipResponse Response, ProcedureState State, IReadOnlyList<string> Missing, int Made);

/// <summary>What the station shows now (W07 draws it).</summary>
/// <param name="State">The run's state.</param>
/// <param name="ProcedureId">The procedure, or <see langword="null"/> with no run.</param>
/// <param name="Unit">Whether a step, a chunk or the goal is shown.</param>
/// <param name="Row">The instruction row shown.</param>
/// <param name="StepId">The current step (the one the next act is judged against).</param>
/// <param name="Rung">The rung of the row shown, 1 to 4.</param>
/// <param name="ShowEnglish">Whether the English is shown (rung 1, or help).</param>
/// <param name="ShowText">Whether the Spanish text is shown (rungs 1 and 2, or help).</param>
/// <param name="HighlightUse">The thing to hold, highlighted, or <see langword="null"/>.</param>
/// <param name="HighlightOn">The target, highlighted, or <see langword="null"/>.</param>
/// <param name="StepsDone">Steps done.</param>
/// <param name="StepsTotal">Steps of the procedure.</param>
/// <param name="TeacherPresent">Whether the teacher is with the learner (W08's hooks).</param>
public sealed record ProcedureView(
    ProcedureState State,
    string? ProcedureId,
    UnitKind Unit,
    string? Row,
    string? StepId,
    int Rung,
    bool ShowEnglish,
    bool ShowText,
    string? HighlightUse,
    string? HighlightOn,
    int StepsDone,
    int StepsTotal,
    bool TeacherPresent)
{
    /// <summary>The audio of the row is spoken at every rung.</summary>
    public bool PlayAudio => Row is not null;

    /// <summary>The view with no run.</summary>
    public static ProcedureView None { get; } = new(ProcedureState.NotStarted, null, UnitKind.Step, null, null, 0, false, false, null, null, 0, 0, false);
}

/// <summary>A world event the runner raises (4.7); the session logs it as a <see cref="Event.WorldEvent"/>.</summary>
/// <param name="Name">One of <see cref="Event.WorldEvents"/>.</param>
/// <param name="Detail">The procedure id, or for <c>procedure_step</c> the step id.</param>
public sealed record ProcedureEvent(string Name, string Detail);

/// <summary>
/// The procedure runner (W06; activities plan 4.2, design 10.9 to 10.15): deterministic plain state. It accepts actions and reports events; the host owns the time, the
/// seed and the inventory. A wrong act changes nothing and costs nothing. Nothing is used up during a run: at the last step one exchange takes the ingredients, gives the
/// containers back and adds the yield, or changes nothing. The run is saved (<see cref="Capture"/>) and restored (<see cref="Restore"/>); a finished run cannot grant twice.
/// </summary>
public sealed class ProcedureRunner
{
    private const string GoalId = "goal";

    private readonly ProcedureBook _procedures;
    private readonly StationBook _stations;
    private readonly NounCatalog _nouns;
    private readonly PhraseProgress _phrases;
    private readonly CreditRules _credit;
    private readonly ulong _seed;
    private readonly List<ProcedureEvent> _events = [];
    private Run? _run;

    /// <summary>Creates the runner of a game.</summary>
    /// <param name="procedures">The procedures.</param>
    /// <param name="stations">The stations.</param>
    /// <param name="nouns">The noun catalogue (an ingredient gone at the end is named by its noun).</param>
    /// <param name="phrases">The phrase progress whose instruction rows the acts credit.</param>
    /// <param name="credit">The credit rules (the gaps).</param>
    /// <param name="seed">The world seed that draws the order of a group of steps.</param>
    /// <param name="policy">Decision 19's policy (<see cref="InstructionEvidencePolicy.Default"/> when <see langword="null"/>).</param>
    public ProcedureRunner(ProcedureBook procedures, StationBook stations, NounCatalog nouns, PhraseProgress phrases, CreditRules credit, ulong seed, InstructionEvidencePolicy? policy = null)
    {
        _procedures = procedures ?? throw new ArgumentNullException(nameof(procedures));
        _stations = stations ?? throw new ArgumentNullException(nameof(stations));
        _nouns = nouns ?? throw new ArgumentNullException(nameof(nouns));
        _phrases = phrases ?? throw new ArgumentNullException(nameof(phrases));
        _credit = credit ?? throw new ArgumentNullException(nameof(credit));
        _seed = seed;
        Policy = policy ?? InstructionEvidencePolicy.Default;
    }

    /// <summary>Decision 19's policy.</summary>
    public InstructionEvidencePolicy Policy { get; }

    /// <summary>The learner's records.</summary>
    public ProcedureProgress Progress { get; } = new();

    /// <summary>The state of the run.</summary>
    public ProcedureState State => _run?.State ?? ProcedureState.NotStarted;

    /// <summary>The procedure being run, or <see langword="null"/>.</summary>
    public ProcedureDefinition? Current => _run is null ? null : _procedures.Find(_run.Procedure);

    /// <summary>The order of the steps drawn for this run (the group draw), empty with no run.</summary>
    public IReadOnlyList<string> Order => _run?.Order ?? [];

    /// <summary>Times the instruction was replayed in this run (telemetry; never help).</summary>
    public int Repeats => _run?.Repeats ?? 0;

    /// <summary>Times the instruction was played slowly in this run (telemetry; never help).</summary>
    public int Slowers => _run?.Slowers ?? 0;

    /// <summary>Takes the events raised since the last call, in order.</summary>
    public IReadOnlyList<ProcedureEvent> DrainEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }

    /// <summary>Starts a run (or resumes the one in progress) at a station.</summary>
    /// <param name="procedureId">The procedure.</param>
    /// <param name="stationId">The station the learner is at.</param>
    /// <param name="teacherPresent">Whether the teacher is with the learner (W08's hook; kept in the run).</param>
    public StartOutcome Start(string procedureId, string stationId, bool teacherPresent)
    {
        ArgumentNullException.ThrowIfNull(procedureId);
        ArgumentNullException.ThrowIfNull(stationId);
        var procedure = _procedures.Find(procedureId);
        if (procedure is null || !string.Equals(procedure.Station, stationId, StringComparison.Ordinal) || _stations.Find(stationId) is null)
        {
            return StartOutcome.Unknown;
        }

        if (_run is { State: ProcedureState.InRun or ProcedureState.ReadyToCollect } running)
        {
            if (!string.Equals(running.Procedure, procedureId, StringComparison.Ordinal))
            {
                return StartOutcome.Busy;
            }

            running.Teacher = teacherPresent;
            return StartOutcome.Resumed;
        }

        if (Items(procedure) is null)
        {
            return StartOutcome.Unavailable;
        }

        var bake = Progress.NoteStarted(procedureId);
        var run = new Run(procedureId, stationId) { Teacher = teacherPresent, Bake = bake, State = ProcedureState.InRun };
        run.Order.AddRange(Draw(procedure, bake));
        foreach (var chunk in procedure.Chunks.Where(c => c.Steps.All(id => StepRow(procedure, id) is { } row && _phrases.LevelOf(row) == SupportLevel.None)))
        {
            _ = run.Folded.Add(chunk.Id);
            _phrases.StartAt(chunk.Row, SupportLevel.CardAndAudio);
        }

        if (procedure.Chunks.Count > 0
            && procedure.Steps.All(s => procedure.Chunks.Any(c => run.Folded.Contains(c.Id) && c.Steps.Contains(s.Id) && _phrases.LevelOf(c.Row) == SupportLevel.None)))
        {
            _ = run.Folded.Add(GoalId);
            _phrases.StartAt(procedure.Goal.Row, SupportLevel.CardAndAudio);
        }

        _run = run;
        _events.Add(new ProcedureEvent(Event.WorldEvents.ProcedureStarted, procedureId));
        Present();
        return StartOutcome.Started;
    }

    /// <summary>
    /// An act of the learner: <paramref name="use"/> (what they hold or the tool) on <paramref name="on"/> (the fixture) with <paramref name="verb"/>, judged against the
    /// current step. A wrong act changes no inventory; at the last step a right act makes the exchange.
    /// </summary>
    /// <param name="use">The noun held.</param>
    /// <param name="on">The fixture the act is done on.</param>
    /// <param name="verb">The verb lemma.</param>
    /// <param name="inventory">The learner's inventory (changed only by the final exchange).</param>
    /// <param name="time">The stored time in Unix seconds.</param>
    /// <param name="presented">How the instruction was presented (decision 19); both by default.</param>
    public ActResult Act(string use, string on, string verb, Inventory inventory, long time, InstructionPresentation presented = InstructionPresentation.Heard | InstructionPresentation.Read)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(on);
        ArgumentNullException.ThrowIfNull(verb);
        ArgumentNullException.ThrowIfNull(inventory);
        if (_run is not { State: ProcedureState.InRun } run || CurrentStep() is not { } step)
        {
            return new ActResult(ActOutcome.NotRunning, SlipResponse.None, State, [], 0);
        }

        var procedure = Current!;
        if (Is(step, use, on, verb))
        {
            var unit = UnitOf(procedure, run, step);
            var unitSteps = unit.Steps;
            var quality = QualityOf(run, step.Id);
            _ = run.Done.Add(step.Id);
            Progress.NoteAct(unit.Row, presented);
            _events.Add(new ProcedureEvent(Event.WorldEvents.ProcedureStep, step.Id));
            if (unit.Kind == UnitKind.Step)
            {
                _ = _phrases.RecordAct(step.Row, Policy.Credits(presented) ? quality : ActQuality.Helped, time, _credit);
            }
            else if (unitSteps.All(run.Done.Contains))
            {
                var whole = unitSteps.Select(id => QualityOf(run, id)).Aggregate(ActQuality.FirstTime, static (a, b) => (ActQuality)Math.Max((int)a, (int)b));
                _ = _phrases.RecordAct(unit.Row, Policy.Credits(presented) ? whole : ActQuality.Helped, time, _credit);
            }

            if (CurrentStep() is null)
            {
                return Exchange(procedure, run, inventory, time, ActOutcome.Right);
            }

            Present();
            return new ActResult(ActOutcome.Right, SlipResponse.None, run.State, [], 0);
        }

        var outcome = Classify(procedure, run, step, use, on, verb);
        return new ActResult(outcome, Slip(procedure, run, step), run.State, [], 0);
    }

    /// <summary>Tries the exchange of a run left at <see cref="ProcedureState.ReadyToCollect"/>: the bag may have room now.</summary>
    /// <param name="inventory">The learner's inventory.</param>
    /// <param name="time">The stored time in Unix seconds.</param>
    public ActResult Collect(Inventory inventory, long time)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return _run is { State: ProcedureState.ReadyToCollect } run && Current is { } procedure
            ? Exchange(procedure, run, inventory, time, ActOutcome.Right)
            : new ActResult(ActOutcome.NotRunning, SlipResponse.None, State, [], 0);
    }

    /// <summary>Replays the instruction (a listening aid; telemetry, never help).</summary>
    /// <returns><see langword="true"/> when a run is at a step.</returns>
    public bool Repeat()
    {
        if (_run is not { State: ProcedureState.InRun } run)
        {
            return false;
        }

        run.Repeats++;
        return true;
    }

    /// <summary>Plays the instruction slowly (a listening aid; telemetry, never help).</summary>
    /// <returns><see langword="true"/> when a run is at a step.</returns>
    public bool Slower()
    {
        if (_run is not { State: ProcedureState.InRun } run)
        {
            return false;
        }

        run.Slowers++;
        return true;
    }

    /// <summary>
    /// Help (the Ayuda key, "ayuda" or "no sé"): shows the English and the highlight, steps the shown row up one rung, and unfolds a folded chunk (or goal) for this run.
    /// </summary>
    /// <param name="time">The stored time in Unix seconds.</param>
    public ProcedureView Help(long time)
    {
        if (_run is not { State: ProcedureState.InRun } run || CurrentStep() is not { } step)
        {
            return View();
        }

        var unit = UnitOf(Current!, run, step);
        _ = _phrases.RecordHelp(unit.Row, time);
        foreach (var id in unit.Steps.Where(id => !run.Done.Contains(id)))
        {
            _ = run.Helped.Add(id);
        }

        if (unit.Kind == UnitKind.Goal)
        {
            _ = run.Unfolded.Add(GoalId);
        }
        else if (unit.Kind == UnitKind.Chunk)
        {
            _ = run.Unfolded.Add(unit.Id);
        }

        Present();
        return View();
    }

    /// <summary>The learner leaves the station (Esc, or walks away): the run stays where it is and nothing is taken.</summary>
    public void Leave()
    {
        // The state is plain values and the inventory is never touched before the exchange: leaving changes nothing.
    }

    /// <summary>Throws the run away (start again): nothing is taken or given; the run stays counted.</summary>
    public void Abandon() => _run = null;

    /// <summary>What the station shows now.</summary>
    public ProcedureView View()
    {
        if (_run is not { } run || Current is not { } procedure)
        {
            return ProcedureView.None;
        }

        var done = run.Done.Count;
        if (CurrentStep() is not { } step)
        {
            return new ProcedureView(run.State, run.Procedure, UnitKind.Goal, procedure.Goal.Row, null, 4, false, false, null, null, done, procedure.Steps.Count, run.Teacher);
        }

        var unit = UnitOf(procedure, run, step);
        var rung = (int)_phrases.LevelOf(unit.Row) + 1;
        var helped = run.Helped.Contains(step.Id);
        var slips = run.Slips.GetValueOrDefault(step.Id);
        var highlight = helped || slips >= (rung == 1 ? 1 : 3);
        return new ProcedureView(
            run.State,
            run.Procedure,
            unit.Kind,
            unit.Row,
            step.Id,
            rung,
            rung == 1 || helped,
            rung <= 2 || helped,
            highlight ? step.ActUse : null,
            highlight ? step.ActOn : null,
            done,
            procedure.Steps.Count,
            run.Teacher);
    }

    /// <summary>Captures the learner's records and the run for the save, or <see langword="null"/> when there is nothing (a save then has no procedure entry).</summary>
    public Save.ProceduresSave? Capture()
    {
        if (Progress.IsEmpty && _run is null)
        {
            return null;
        }

        var run = _run is null
            ? null
            : new Save.ProcedureRunSave(
                _run.Procedure,
                _run.Station,
                _run.State.ToString(),
                _run.Teacher,
                _run.Bake,
                [.. _run.Order],
                [.. _run.Order.Where(_run.Done.Contains)],
                [.. _run.Slips.OrderBy(static s => s.Key, StringComparer.Ordinal).Select(static s => new Save.SlipSave(s.Key, s.Value))],
                [.. _run.Helped.Order(StringComparer.Ordinal)],
                [.. _run.Shown.Order(StringComparer.Ordinal)],
                [.. _run.Folded.Order(StringComparer.Ordinal)],
                [.. _run.Unfolded.Order(StringComparer.Ordinal)]);
        return new Save.ProceduresSave(
            1,
            [.. Progress.Known],
            [.. Progress.Records.Select(static r => new Save.ProcedureProgressSave(r.Id, r.Runs, r.Completed, [.. r.Sittings], r.FirstTimeSteps, r.Mastered))],
            [.. Progress.Evidence.Select(static e => new Save.ActEvidenceSave(e.Row, e.HeardActs, e.ReadActs))],
            run);
    }

    /// <summary>
    /// Puts a save's procedures back; a save with none (every save before W06) leaves the runner as a new game has it. A run of a procedure the bundle no longer has, or
    /// with steps it does not know, is dropped. The load raises no event.
    /// </summary>
    /// <param name="save">The save's entry, or <see langword="null"/>.</param>
    public void Restore(Save.ProceduresSave? save)
    {
        _run = null;
        _events.Clear();
        Progress.Restore([], [], []);
        if (save is null)
        {
            return;
        }

        Progress.Restore(
            save.Known ?? [],
            (save.Progress ?? []).Select(static p => new ProcedureRecord(p.Id, p.Runs, p.Completed, p.CleanSittings ?? [], p.FirstTimeSteps, p.Mastered)),
            (save.Evidence ?? []).Select(static e => new ActEvidence(e.Row, e.Heard, e.Read)));
        if (save.Run is not { } saved
            || _procedures.Find(saved.Procedure) is not { } procedure
            || !Enum.TryParse<ProcedureState>(saved.State, out var state)
            || state == ProcedureState.NotStarted)
        {
            return;
        }

        var ids = procedure.Steps.Select(static s => s.Id).ToHashSet(StringComparer.Ordinal);
        var order = (saved.Order ?? []).ToList();
        if (order.Count != ids.Count || !order.All(ids.Contains) || order.Distinct(StringComparer.Ordinal).Count() != order.Count)
        {
            return;
        }

        var run = new Run(saved.Procedure, saved.Station) { State = state, Teacher = saved.Teacher, Bake = saved.Bake };
        run.Order.AddRange(order);
        run.Done.UnionWith((saved.Done ?? []).Where(ids.Contains));
        foreach (var slip in saved.Slips ?? [])
        {
            if (ids.Contains(slip.Step))
            {
                run.Slips[slip.Step] = slip.Count;
            }
        }

        run.Helped.UnionWith((saved.Helped ?? []).Where(ids.Contains));
        run.Shown.UnionWith(saved.Shown ?? []);
        run.Folded.UnionWith(saved.Folded ?? []);
        run.Unfolded.UnionWith(saved.Unfolded ?? []);
        _run = run;
    }

    // The first step of the drawn order that is not done.
    private ProcedureStep? CurrentStep()
    {
        if (_run is null || Current is not { } procedure)
        {
            return null;
        }

        var id = _run.Order.FirstOrDefault(i => !_run.Done.Contains(i));
        return id is null ? null : procedure.Steps.First(s => string.Equals(s.Id, id, StringComparison.Ordinal));
    }

    private static bool Is(ProcedureStep step, string use, string on, string verb) =>
        string.Equals(step.ActUse, use, StringComparison.Ordinal) && string.Equals(step.ActOn, on, StringComparison.Ordinal) && string.Equals(step.Verb, verb, StringComparison.Ordinal);

    private static ActOutcome Classify(ProcedureDefinition procedure, Run run, ProcedureStep step, string use, string on, string verb)
    {
        if (procedure.Steps.Any(s => !run.Done.Contains(s.Id) && !string.Equals(s.Id, step.Id, StringComparison.Ordinal) && Is(s, use, on, verb)))
        {
            return ActOutcome.OutOfOrder;
        }

        return string.Equals(step.ActUse, use, StringComparison.Ordinal) && string.Equals(step.ActOn, on, StringComparison.Ordinal) ? ActOutcome.WrongVerb : ActOutcome.WrongThing;
    }

    // A wrong act counts a slip on the current step and answers by the ladder of design 10.10.
    private SlipResponse Slip(ProcedureDefinition procedure, Run run, ProcedureStep step)
    {
        var count = run.Slips.GetValueOrDefault(step.Id) + 1;
        run.Slips[step.Id] = count;
        var rung = (int)_phrases.LevelOf(UnitOf(procedure, run, step).Row) + 1;
        if (rung == 1)
        {
            return SlipResponse.Highlight;
        }

        switch (count)
        {
            case 1:
                return SlipResponse.Repeat;
            case 2:
                return SlipResponse.Slower;
            default:
                _ = run.Helped.Add(step.Id); // the third slip's highlight is help
                return SlipResponse.Highlight;
        }
    }

    private static ActQuality QualityOf(Run run, string stepId) =>
        run.Helped.Contains(stepId) ? ActQuality.Helped : run.Slips.GetValueOrDefault(stepId) > 0 ? ActQuality.AfterSlip : ActQuality.FirstTime;

    // What is shown for a step: the goal when it is folded, else the step's chunk when that is folded, else the step.
    private static Unit UnitOf(ProcedureDefinition procedure, Run run, ProcedureStep step)
    {
        if (run.Folded.Contains(GoalId) && !run.Unfolded.Contains(GoalId))
        {
            return new Unit(UnitKind.Goal, GoalId, procedure.Goal.Row, [.. procedure.Steps.Select(static s => s.Id)]);
        }

        var chunk = procedure.Chunks.FirstOrDefault(c => c.Steps.Contains(step.Id));
        return chunk is not null && run.Folded.Contains(chunk.Id) && !run.Unfolded.Contains(chunk.Id)
            ? new Unit(UnitKind.Chunk, chunk.Id, chunk.Row, chunk.Steps)
            : new Unit(UnitKind.Step, step.Id, step.Row, [step.Id]);
    }

    // The instruction shown counts once per row per run: Heard +1 on its row.
    private void Present()
    {
        if (_run is not { State: ProcedureState.InRun } run || CurrentStep() is not { } step)
        {
            return;
        }

        var row = UnitOf(Current!, run, step).Row;
        if (run.Shown.Add(row))
        {
            _phrases.NoteHeard(row);
        }
    }

    private static string? StepRow(ProcedureDefinition procedure, string stepId) => procedure.Steps.FirstOrDefault(s => string.Equals(s.Id, stepId, StringComparison.Ordinal))?.Row;

    // The steps of each group of anyOrder are put in the group's places in an order drawn for this run (the bake), the others keep theirs.
    private List<string> Draw(ProcedureDefinition procedure, int bake)
    {
        var order = procedure.Steps.Select(static s => s.Id).ToList();
        var rng = WorldRng.For(_seed, "proc." + procedure.Id, bake);
        foreach (var group in procedure.Steps.Where(static s => s.AnyOrder is not null).Select(static s => s.AnyOrder!).Distinct(StringComparer.Ordinal))
        {
            var members = procedure.Steps.Where(s => string.Equals(s.AnyOrder, group, StringComparison.Ordinal)).Select(static s => s.Id).ToList();
            var places = members.Select(m => order.IndexOf(m)).Order().ToList();
            for (var i = members.Count - 1; i > 0; i--)
            {
                var j = rng.NextInt(i + 1);
                (members[i], members[j]) = (members[j], members[i]);
            }

            for (var i = 0; i < places.Count; i++)
            {
                order[places[i]] = members[i];
            }
        }

        return order;
    }

    // The items of a procedure as inventory kinds, or null when the game does not have one of them.
    internal static ProcedureItems? Items(ProcedureDefinition procedure)
    {
        var uses = new List<(ItemId Item, int Count, ItemId? Leaves)>();
        foreach (var use in procedure.Uses)
        {
            if (ItemOf(use.Item) is not { } item)
            {
                return null;
            }

            ItemId? leaves = null;
            if (use.Leaves is not null)
            {
                if (ItemOf(use.Leaves) is not { } left)
                {
                    return null;
                }

                leaves = left;
            }

            uses.Add((item, use.Count, leaves));
        }

        return ItemOf(procedure.Makes.Item) is { } made ? new ProcedureItems(uses, made) : null;
    }

    private string NounOf(ItemId item) => _nouns.ForItem(item)?.Lemma ?? ItemDefinition.Get(item).Name;

    private bool Names(string noun, ItemId item) => _nouns.ForLemma(noun)?.Items.Contains(item) == true;

    private static ItemId? ItemOf(string name) =>
        ItemDefinition.All.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal))?.Id;

    // The exchange (design 10.13): everything or nothing. An ingredient gone re-opens the step that used it; a full bag leaves the run ready to collect.
    private ActResult Exchange(ProcedureDefinition procedure, Run run, Inventory inventory, long time, ActOutcome outcome)
    {
        var items = Items(procedure)!;
        var missing = items.Uses.Where(u => inventory.Count(u.Item) < u.Count).Select(static u => u.Item).ToList();
        if (missing.Count > 0)
        {
            var nouns = new List<string>();
            foreach (var item in missing)
            {
                nouns.Add(NounOf(item));
                var step = procedure.Steps.FirstOrDefault(s => new[] { s.What, s.Use, s.ActUse }.Any(n => n is not null && Names(n, item)))
                    ?? procedure.Steps[^1];
                _ = run.Done.Remove(step.Id);
                _ = run.Helped.Add(step.Id); // the step is done again: it earns no credit twice
            }

            run.State = ProcedureState.InRun;
            Present();
            return new ActResult(outcome, SlipResponse.None, run.State, nouns, 0);
        }

        var made = procedure.Makes.Count + (procedure.Makes.Bonus is { } bonus && procedure.Steps.Count(s => run.Slips.GetValueOrDefault(s.Id) == 0) >= bonus.FirstTimeSteps ? bonus.Count : 0);
        var scratch = inventory.Copy();
        if (!Apply(scratch, items, made))
        {
            run.State = ProcedureState.ReadyToCollect;
            return new ActResult(outcome, SlipResponse.None, run.State, [], 0);
        }

        _ = Apply(inventory, items, made);
        run.State = ProcedureState.Done; // in the same transaction that gave the yield: a replay after a load grants nothing
        var firstTime = procedure.Steps.Count(s => run.Slips.GetValueOrDefault(s.Id) == 0);
        Progress.NoteCompleted(procedure.Id, firstTime, run.Helped.Count > 0, _phrases.Session);
        _events.Add(new ProcedureEvent(Event.WorldEvents.ProcedureDone, procedure.Id));
        var record = Progress.RecordOf(procedure.Id);
        if (!record.Mastered && _phrases.LevelOf(procedure.Goal.Row) == SupportLevel.None && record.Sittings.Count >= ProcedureProgress.SittingsToMaster)
        {
            Progress.NoteMastered(procedure.Id);
            _events.Add(new ProcedureEvent(Event.WorldEvents.ProcedureMastered, procedure.Id));
        }

        return new ActResult(outcome, SlipResponse.None, run.State, [], made);
    }

    private static bool Apply(Inventory inventory, ProcedureItems items, int made)
    {
        foreach (var (item, count, leaves) in items.Uses)
        {
            if (!inventory.Remove(item, count) || (leaves is { } left && inventory.Add(left, count) != 0))
            {
                return false;
            }
        }

        return inventory.Add(items.Makes, made) == 0;
    }

    private sealed record Unit(UnitKind Kind, string Id, string Row, IReadOnlyList<string> Steps);

    private sealed class Run(string procedure, string station)
    {
        public string Procedure { get; } = procedure;

        public string Station { get; } = station;

        public ProcedureState State { get; set; }

        public bool Teacher { get; set; }

        public int Bake { get; init; }

        public int Repeats { get; set; }

        public int Slowers { get; set; }

        public List<string> Order { get; } = [];

        public HashSet<string> Done { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> Slips { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Helped { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Shown { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Folded { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Unfolded { get; } = new(StringComparer.Ordinal);
    }
}

/// <summary>A procedure's uses and yield as inventory kinds.</summary>
/// <param name="Uses">What is used up, how many, and the container left.</param>
/// <param name="Makes">The item made.</param>
internal sealed record ProcedureItems(IReadOnlyList<(ItemId Item, int Count, ItemId? Leaves)> Uses, ItemId Makes);
