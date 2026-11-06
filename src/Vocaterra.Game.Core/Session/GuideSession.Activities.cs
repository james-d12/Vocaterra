using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
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
        return outcome;
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
        return result;
    }

    /// <summary>Tries the exchange of a run left at <see cref="ProcedureState.ReadyToCollect"/>.</summary>
    public ActResult CollectProcedure()
    {
        var result = Procedures.Collect(LiveInventory, StoredTime.UnixSeconds);
        FlushProcedureEvents();
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
        if (!Procedures.Progress.IsMastered(procedureId) || Data.Procedures.Find(procedureId) is not { } procedure || ProcedureRunner.Items(procedure) is not { } items)
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
        Log(new WorldEvent(Tick, StoredTime.UnixSeconds, WorldEvents.ItemCrafted, null, Nouns.ForItem(items.Makes)?.Lemma ?? ItemDefinition.Get(items.Makes).Name));
        return CraftResult.Done;
    }

    // The containers a use leaves (the bottle of the water-bottle) come back.
    private static bool GiveBack(Inventory inventory, ProcedureItems items) =>
        items.Uses.All(u => u.Leaves is not { } left || inventory.Add(left, u.Count) == 0);

    /// <summary>The procedures for the save (<see langword="null"/> when there are none).</summary>
    internal ProceduresSave? CaptureProcedures() => Procedures.Capture();

    /// <summary>Puts a save's procedures back; a save with none starts with none.</summary>
    /// <param name="save">The save's entry, or <see langword="null"/>.</param>
    internal void RestoreProcedures(ProceduresSave? save) => Procedures.Restore(save);

    // Each event the runner raised becomes a world event, in order.
    private void FlushProcedureEvents()
    {
        foreach (var raised in Procedures.DrainEvents())
        {
            Log(new WorldEvent(Tick, StoredTime.UnixSeconds, raised.Name, Procedures.Current?.Teacher, raised.Detail));
        }
    }
}
