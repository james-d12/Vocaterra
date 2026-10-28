using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tip;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// The camp checklist's steps done in a tick (S4): a toast with each, and one for the whole checklist when it is complete. Pickups show in the
/// hotbar and, the first time, as a word card; they do not toast (a toast per pickup would queue behind the checklist's completions for minutes).
/// </summary>
public sealed class ObjectiveAnnouncer(VoxelSession session, ToastQueue toasts, Action<string> console) : ITickObserver
{
    /// <inheritdoc />
    public void AfterTick(IReadOnlyList<VoxelCommand> commands)
    {
        var completed = session.Completed;
        foreach (var step in completed)
        {
            toasts.Show($"Done: {step.Text}");
            console($"objective: Done: {step.Text} -> {session.Objective.Summary}");
        }

        if (completed.Count > 0 && session.Objective.Complete)
        {
            toasts.Show("Objective complete");
            console("objective: complete");
        }
    }
}
