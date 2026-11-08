using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Npc;

namespace Vocaterra.Game.Core.Session;

// The teacher in the loop (W08, design 10.14): what the host kinds of a teacher's answer do in the world. They arrive with every other host event (ApplyHostEvents), on the game
// thread, after the worker has judged the turn: the repeat, the slow recording and the help reach the runner and the station, the ingredients reach the bag (design decision 20), the
// recipe reaches the known list, and an accepted offer asks for the station.
public sealed partial class VoxelSession
{
    // The kinds of NpcEffectKind that belong to a teacher; the others are the world's (ApplyHostEvents).
    private void ApplyTeacherEffect(string npc, NpcEffect effect, string how)
    {
        switch (effect.Kind)
        {
            case NpcEffectKind.ProcedureRepeat:
                if (Procedures.Repeat())
                {
                    Cue(ProcedureCue.Repeat);
                }

                break;
            case NpcEffectKind.ProcedureSlower:
                if (Procedures.Slower())
                {
                    Cue(ProcedureCue.Slower);
                }

                break;
            case NpcEffectKind.ProcedureHelp:
                if (Procedures.State == ProcedureState.InRun)
                {
                    _ = HelpInProcedure();
                    Cue(ProcedureCue.Help);
                }

                break;
            case NpcEffectKind.GiveMissing:
                GiveMissing(npc, how);
                break;
            case NpcEffectKind.TeachRecipe:
                TeachRecipe(npc, effect.Procedure);
                break;
            case NpcEffectKind.StartProcedure:
                if (ProcedureOf(npc, effect.Procedure) is { } procedure)
                {
                    RequestStation(procedure.Station, procedure.Id);
                }

                break;
            default:
                break;
        }
    }

    // Decision 20's exception, W10's seam: a class whose current do step waits for the procedure to be done (the introductory class's bake) has the ingredients supplied whenever the
    // run lacks them, and the grant does not count for the day, as a lesson step that requires an NPC's gift always gets it (GiftRequired).
    /// <inheritdoc />
    protected override bool GrantRequiredByLesson(string procedureId) =>
        Lessons is { Engaged: true, Current: { Type: LessonStepType.Do, Trigger: { } trigger } }
        && string.Equals(trigger.Event, WorldEvents.ProcedureDone, StringComparison.Ordinal)
        && (trigger.Detail is null || string.Equals(trigger.Detail, procedureId, StringComparison.Ordinal))
        && (trigger.Npc is null || string.Equals(trigger.Npc, Data.Procedures.Find(procedureId)?.Teacher, StringComparison.Ordinal));

    // The procedure an event names, or the one the NPC teaches when it names none (so a bundle with no procedure has nothing to teach and nothing happens).
    private ProcedureDefinition? ProcedureOf(string npc, string id) => id.Length > 0 ? Data.Procedures.Find(id) : Data.Procedures.TaughtBy(npc);

    // Design decision 20: the ingredients the run lacks, all or nothing. The worker answered a miss already (nothing missing, today's given, no room), so this checks again and
    // gives nothing in those cases. The first bake's grant does not count for the day.
    private void GiveMissing(string npc, string how)
    {
        if (Procedures.Current is not { } procedure || PlanGrant() is not { Miss: null } plan)
        {
            return;
        }

        foreach (var (item, count) in plan.Missing)
        {
            _ = Simulation.GrantItems(item, count);
            LearnItem(item);
            _eventAmount = count;
            LogWorld(WorldEvents.ItemGiven, npc, $"{Nouns.ForItem(item)?.Lemma ?? ItemDefinition.Get(item).Name}, {how}");
            _eventAmount = 1;
        }

        NoteGrant(procedure.Id);
        LogWorld(WorldEvents.IngredientsGiven, npc, procedure.Id);
    }

    private void TeachRecipe(string npc, string id)
    {
        if (ProcedureOf(npc, id) is { } procedure && Procedures.Progress.NoteTaught(procedure.Id))
        {
            LogWorld(WorldEvents.RecipeTaught, npc, procedure.Id);
        }
    }
}
