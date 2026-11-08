using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Needs;

namespace Vocaterra.Game.Core.Session;

// A night and a collapse (N03, adaptation design 9.5): both move the needs, the day and the sitting, and run the day's resets. SaveGame.Sleep calls ApplyNight on both paths
// (the sleep at the fire, the closing card, --sleep, and the collapse where the player stands), so the one rule is in one place.
public sealed partial class VoxelSession
{
    /// <summary>How many nights have passed in this run (a sleep or a collapse): the day's resets have run this many times. A load does not count.</summary>
    public int DayResetCount { get; private set; }

    /// <summary>
    /// Whether the player has been at the sleep drive's last stage for the collapse's delay in active play (60 s, N01's timer) and play is active: the game then collapses them
    /// where they stand (<see cref="SaveGame.Sleep"/> with the collapse). Never true with the needs off.
    /// </summary>
    public bool CollapseDue => NeedsEnabled && PlayActive && Needs.CollapseDue;

    // The night's changes on the needs (PlayerNeeds.Sleep or Collapse, then the stages taken as they are, with no event: the night is its own event), the day's resets, and for
    // a collapse the two events that tell it. The day and the sitting are already moved by the caller. Needs off: the meters stay as they are.
    internal void ApplyNight(bool collapse)
    {
        if (NeedsEnabled)
        {
            if (collapse)
            {
                Needs.Collapse();
            }
            else
            {
                Needs.Sleep();
            }

            SettleNeedStages();
        }

        RunDayResets();
        if (collapse)
        {
            LogWorld(WorldEvents.PlayerCollapsed, null, Data.Needs.Of(Drive.Sleep).Detail);
            LogWorld(WorldEvents.PlayerWoke, null, "collapse");
        }
    }

    // The day's resets: one place where a passing night (a sleep or a collapse) resets what belongs to a day. The chest a death left goes (N04); N06's picked bushes grow back and its gifts
    // return (RenewAtNight). Never called by a load, and a death is not a night.
    private void RunDayResets()
    {
        DayResetCount++;
        RemoveChestAtNight();
        RenewAtNight();
        ClearGrants(); // W08: a teacher gives the ingredients again
    }
}
