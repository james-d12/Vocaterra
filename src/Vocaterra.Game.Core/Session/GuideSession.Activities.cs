using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;

namespace Vocaterra.Game.Core.Session;

// The procedures' part of the session (W06): the runner is plain state; here it is given the live inventory and the stored time, and its events become world events (so tips,
// lessons and objectives may wait for them). The station view (W07) calls these members.
public abstract partial class GuideSession
{
    /// <summary>The procedure runner (W06): the run in progress, the records, the evidence of acts.</summary>
    public ProcedureRunner Procedures { get; }

    /// <summary>Starts (or resumes) a procedure at its station.</summary>
    /// <param name="procedureId">The procedure.</param>
    /// <param name="stationId">The station.</param>
    /// <param name="teacherPresent">Whether the teacher is with the learner.</param>
    public StartOutcome StartProcedure(string procedureId, string stationId, bool teacherPresent)
    {
        var outcome = Procedures.Start(procedureId, stationId, teacherPresent);
        FlushProcedureEvents();
        if (outcome is StartOutcome.Started or StartOutcome.Resumed && Data.Procedures.Find(procedureId) is { } procedure)
        {
            if (teacherPresent)
            {
                BeginTeaching(procedure.Teacher);
            }
            else
            {
                EndTeaching();
            }
        }

        return outcome;
    }

    /// <summary>The learner leaves the station (Esc, or walks away): the run stays where it is, nothing is taken, and the teacher stops listening (<c>&lt;npc&gt;.teach_end</c>).</summary>
    public void LeaveStation()
    {
        Procedures.Leave();
        EndTeaching();
    }

    /// <summary>An act at the station, judged against the current step; the final right act makes the exchange with the live inventory.</summary>
    /// <param name="use">The noun held.</param>
    /// <param name="on">The fixture acted on.</param>
    /// <param name="verb">The verb lemma.</param>
    /// <param name="presented">How the instruction was presented (decision 19).</param>
    public ActResult ActInProcedure(string use, string on, string verb, InstructionPresentation presented = InstructionPresentation.Heard | InstructionPresentation.Read)
    {
        var result = Procedures.Act(use, on, verb, LiveInventory, StoredTime.UnixSeconds, presented);
        FlushProcedureEvents();
        EndTeachingWhenOver();
        return result;
    }

    /// <summary>Tries the exchange of a run left at <see cref="ProcedureState.ReadyToCollect"/>.</summary>
    public ActResult CollectProcedure()
    {
        var result = Procedures.Collect(LiveInventory, StoredTime.UnixSeconds);
        FlushProcedureEvents();
        EndTeachingWhenOver();
        return result;
    }

    /// <summary>Help at the station (the Ayuda key, "ayuda" or "no sé").</summary>
    public ProcedureView HelpInProcedure()
    {
        var view = Procedures.Help(StoredTime.UnixSeconds);
        FlushProcedureEvents();
        return view;
    }

    /// <summary>
    /// The mastered procedure's one-click bake (design 10.15) through <see cref="CraftingRules"/>, with the procedure's <c>uses</c> and <c>makes</c> (one run's yield, no bonus):
    /// all or nothing, the containers given back, <c>item_crafted</c> logged. The guided bake stays on offer.
    /// </summary>
    /// <param name="procedureId">The procedure; it must be mastered.</param>
    public CraftResult BakeMastered(string procedureId)
    {
        ArgumentNullException.ThrowIfNull(procedureId);
        if (!Procedures.Progress.IsMastered(procedureId) || Data.Procedures.Find(procedureId) is not { } procedure || ProcedureRunner.Items(procedure, Data.Items) is not { } items)
        {
            return CraftResult.NoSuchRecipe;
        }

        var recipe = new Recipe(items.Makes, procedure.Makes.Count, [.. items.Uses.Select(static u => new Ingredient(u.Item, u.Count))], null);
        var scratch = LiveInventory.Copy();
        var result = CraftingRules.Craft(recipe, scratch, true);
        if (result != CraftResult.Done || !GiveBack(scratch, items))
        {
            return result == CraftResult.Done ? CraftResult.InventoryFull : result;
        }

        _ = CraftingRules.Craft(recipe, LiveInventory, true);
        _ = GiveBack(LiveInventory, items);
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.ItemCrafted, null, Nouns.ForItem(items.Makes)?.Lemma ?? Data.Items.Get(items.Makes).Name));
        return CraftResult.Done;
    }

    // The containers a use leaves (the bottle of the water-bottle) come back.
    private static bool GiveBack(Inventory inventory, ProcedureItems items) =>
        items.Uses.All(u => u.Leaves is not { } left || inventory.Add(left, u.Count) == 0);

    /// <summary>The procedures for the save (<see langword="null"/> when there are none).</summary>
    internal ProceduresSave? CaptureProcedures()
    {
        var save = Procedures.Capture();
        if (_grantsToday.Count == 0 && _offeredToday.Count == 0)
        {
            return save;
        }

        var granted = _grantsToday.Count == 0 ? null : _grantsToday.Order(StringComparer.Ordinal).ToArray();
        var offered = _offeredToday.Count == 0 ? null : _offeredToday.Order(StringComparer.Ordinal).ToArray();

        return save is null ? new ProceduresSave(1, [], [], [], null, granted, offered) : save with { GrantedToday = granted, OfferedToday = offered };
    }

    /// <summary>Puts a save's procedures back; a save with none starts with none.</summary>
    /// <param name="save">The save's entry, or <see langword="null"/>.</param>
    internal void RestoreProcedures(ProceduresSave? save)
    {
        Procedures.Restore(save);
        _grantsToday.Clear();
        foreach (var id in save?.GrantedToday ?? [])
        {
            if (Data.Procedures.Find(id) is not null)
            {
                _ = _grantsToday.Add(id);
            }
        }

        _offeredToday.Clear();
        foreach (var npc in save?.OfferedToday ?? [])
        {
            if (Data.Npcs.Find(npc) is not null)
            {
                _ = _offeredToday.Add(npc);
            }
        }

        TeachingNpc = null; // a load stands at no station: a teacher left in its teaching state is brought out of it when the player next talks to it (EndStaleTeaching)
    }

    // Each event the runner raised becomes a world event, in order.
    private void FlushProcedureEvents()
    {
        foreach (var raised in Procedures.DrainEvents())
        {
            Log(new WorldEvent(Tick, StoredTime.UnixSeconds, raised.Name, Procedures.Current?.Teacher, raised.Detail));
        }
    }

    // ---- The teacher in the loop (W08, design 10.14)

    // The procedures whose ingredients a teacher gave since the last night (the first bake's are not counted).
    private readonly HashSet<string> _grantsToday = new(StringComparer.Ordinal);

    // The NPC the talk went to before a teaching began, and the NPC whose teach_end waits for the turn on the worker to finish.
    private string _beforeTeaching = NpcService.GuideAgent;
    private string? _teachEndDue;

    /// <summary>Raised when the teacher's answer asks the station to do something the runner cannot (play the row again or slowly, show the help): the station panel listens.</summary>
    public event Action<ProcedureCue>? ProcedureCued;

    /// <summary>Raised when an accepted offer asks for the station to open (<c>start_procedure</c>): the station id and the procedure id. The front-end closes the dialogue and opens the station.</summary>
    public event Action<string, string>? StationRequested;

    /// <summary>The NPC that listens at its teaching prompt while a procedure with it as teacher runs at a station, or <see langword="null"/>.</summary>
    public string? TeachingNpc { get; private set; }

    /// <summary>The procedures whose ingredients a teacher gave since the last night, ordinal (the daily grant, design decision 20).</summary>
    public IReadOnlyCollection<string> GrantsGivenToday => _grantsToday.Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// W10's way to open the grant where a lesson needs it (design decision 20: the introductory class's do step): a grant for the procedure is then made whenever something is missing
    /// and does not count for the day, as a lesson step that requires an NPC's gift is always given (<c>GiftRequired</c>). No lesson says so yet.
    /// </summary>
    /// <param name="procedureId">The procedure.</param>
    protected virtual bool GrantRequiredByLesson(string procedureId) => false;

    /// <summary>
    /// What the teacher could give of the run's ingredients now (design decision 20), or <see langword="null"/> when no run is at a step. A grant is permitted only while a run is at
    /// a step (<see cref="ProcedureState.InRun"/>): it gives what <c>uses</c> lists and the bag lacks, all or nothing; it is open for the first bake (the procedure never finished) and for
    /// a lesson that requires it, and otherwise once per in-game day.
    /// </summary>
    public TeachingGrant? PlanGrant()
    {
        if (Procedures.State != ProcedureState.InRun || Procedures.Current is not { } procedure || ProcedureRunner.Items(procedure, Data.Items) is not { } items)
        {
            return null;
        }

        var bag = LiveInventory;
        var missing = items.Uses.Select(u => (u.Item, Count: u.Count - bag.Count(u.Item))).Where(static m => m.Count > 0).ToList();
        var scratch = bag.Copy();
        var fits = missing.All(m => scratch.Add(m.Item, m.Count) == 0);
        return new TeachingGrant(missing, GrantOpen(procedure.Id), fits);
    }

    // The offer was made: it is not made again today.
    private void NoteOffer(string npc)
    {
        if (string.Equals(_npcs.StateOf(npc), NpcHostEvents.OfferingState, StringComparison.Ordinal))
        {
            _ = _offeredToday.Add(npc);
        }
    }

    private bool GrantOpen(string procedureId) =>
        Procedures.Progress.RecordOf(procedureId).Completed == 0 || GrantRequiredByLesson(procedureId) || !_grantsToday.Contains(procedureId);

    /// <summary>Counts a grant made for the day, unless it was the first bake's or a lesson's.</summary>
    /// <param name="procedureId">The procedure.</param>
    protected void NoteGrant(string procedureId)
    {
        if (Procedures.Progress.RecordOf(procedureId).Completed > 0 && !GrantRequiredByLesson(procedureId))
        {
            _ = _grantsToday.Add(procedureId);
        }
    }

    /// <summary>The night passed: the day's grants are open again, and so is the teacher's offer.</summary>
    protected void ClearGrants()
    {
        _grantsToday.Clear();
        _offeredToday.Clear();
    }

    // The NPCs whose offer to bake was made since the last night.
    private readonly HashSet<string> _offeredToday = new(StringComparer.Ordinal);

    /// <summary>The NPCs whose offer to bake was made since the last night, ordinal.</summary>
    public IReadOnlyCollection<string> OffersMadeToday => _offeredToday.Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// The host event of the offer an NPC who teaches a procedure could make now (design 10.14), or <see langword="null"/>: <c>&lt;npc&gt;.offer_bake</c> when the talk opens (outside a bake,
    /// once per in-game day, until the learner has baked), <c>&lt;npc&gt;.offer_craft</c> as the answer to «¿cómo hago…?» (whenever no bake is under way). An NPC that teaches no procedure of the
    /// bundle (the bundle has none before W10) offers nothing, so the game is as it was.
    /// </summary>
    /// <param name="npc">The NPC's agent id.</param>
    /// <param name="asked">Whether the learner asked (the answer to <c>ask_craft</c>) instead of the talk opening.</param>
    internal string? BakeOfferOf(string npc, bool asked)
    {
        if (Data.Procedures.TaughtBy(npc) is not { } procedure || Data.Stations.Find(procedure.Station) is null || ProcedureRunner.Items(procedure, Data.Items) is null
            || TeachingNpc is not null || Procedures.State is ProcedureState.InRun or ProcedureState.ReadyToCollect || Lessons.Engaged || Review is not null || PhraseReview is not null)
        {
            return null;
        }

        if (!asked && (Procedures.Progress.RecordOf(procedure.Id).Completed > 0 || _offeredToday.Contains(npc) || !MayOfferClass(npc)))
        {
            return null;
        }

        return NpcHostEvents.Of(npc, asked ? NpcHostEvents.OfferCraft : NpcHostEvents.OfferBake);
    }

    /// <summary>Tells the station what a teacher's answer asked of it.</summary>
    /// <param name="cue">What to do.</param>
    protected void Cue(ProcedureCue cue) => ProcedureCued?.Invoke(cue);

    /// <summary>Asks the front-end to open a station (an accepted offer to bake).</summary>
    /// <param name="stationId">The station.</param>
    /// <param name="procedureId">The procedure.</param>
    protected void RequestStation(string stationId, string procedureId) => StationRequested?.Invoke(stationId, procedureId);

    /// <summary>
    /// The teacher's host turn on <c>&lt;npc&gt;.teach_start</c>: from now on the lines the learner says at the station go to the teacher, at its <c>teaching</c> state's prompt. An NPC whose
    /// agent has no such transition hears nothing (the station then has no talk). Not while a turn is on the worker.
    /// </summary>
    private void BeginTeaching(string npc)
    {
        if (string.Equals(TeachingNpc, npc, StringComparison.Ordinal) || PendingTurns > 0)
        {
            return;
        }

        EndTeaching();
        if (_npcs.HostTurn(npc, NpcHostEvents.Of(npc, NpcHostEvents.TeachStart), StoredTime, StateDigest()) is null)
        {
            return;
        }

        _beforeTeaching = CurrentNpc;
        CurrentNpc = npc;
        TeachingNpc = npc;
    }

    /// <summary>The teacher's host turn on <c>&lt;npc&gt;.teach_end</c>: the procedure ended, or the learner left the station. The lines go to the guide again.</summary>
    private void EndTeaching()
    {
        if (TeachingNpc is not { } npc)
        {
            return;
        }

        TeachingNpc = null;
        CurrentNpc = _beforeTeaching;
        if (PendingTurns > 0)
        {
            _teachEndDue = npc; // the turn on the worker may still change the teacher's state: the end follows it (CompleteTeachEnd)
            return;
        }

        _ = _npcs.HostTurn(npc, NpcHostEvents.Of(npc, NpcHostEvents.TeachEnd), StoredTime, StateDigest());
    }

    private void EndTeachingWhenOver()
    {
        if (TeachingNpc is not null && Procedures.State is ProcedureState.Done or ProcedureState.NotStarted)
        {
            EndTeaching();
        }
    }

    // The end of a teaching that waited for a turn on the worker, and the end of one a load left standing: the teacher is in its teaching state with no station open.
    internal void CompleteTeachEnd(string? npc = null)
    {
        if (PendingTurns > 0)
        {
            return;
        }

        if (_teachEndDue is { } due)
        {
            _teachEndDue = null;
            _ = _npcs.HostTurn(due, NpcHostEvents.Of(due, NpcHostEvents.TeachEnd), StoredTime, StateDigest());
        }

        if (npc is not null && TeachingNpc is null && string.Equals(_npcs.StateOf(npc), NpcHostEvents.TeachingState, StringComparison.Ordinal))
        {
            _ = _npcs.HostTurn(npc, NpcHostEvents.Of(npc, NpcHostEvents.TeachEnd), StoredTime, StateDigest());
        }
    }
}
