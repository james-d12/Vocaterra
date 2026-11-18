using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Pass;

namespace Vocaterra.Game.Core.Session;

// The lock of the pass through the east ring (B08b, D-game-34 items 6 and 15): the rule is PassState's; here it is fed the learner's completed lessons and the cheat setting, and its
// opening removes the rockfall.
public sealed partial class VoxelSession
{
    private PassState? _pass;
    private bool _passCheat;

    /// <summary>
    /// The pass's lock (<see cref="PassState.IsOpen"/>, <see cref="PassState.Opened"/>), which B07 reads to lift the valley's edge wall. It opens when every lesson of the world file's
    /// <c>pass.requiredLessons</c> is complete, or at once under <see cref="PassCheat"/>, and never closes again; the save keeps it.
    /// </summary>
    public PassState Pass
    {
        get
        {
            if (_pass is null)
            {
                _pass = new PassState(Data.Pass);
                _pass.Opened += OnPassOpened;
            }

            return _pass;
        }
    }

    /// <summary>
    /// The pass cheat (the settings menu, B08b): turning it on opens the pass at once; turning it off again closes nothing. It is the player's setting, not the game's, so a load does not
    /// restore it (the host sets it from the settings).
    /// </summary>
    public bool PassCheat
    {
        get => _passCheat;
        set
        {
            _passCheat = value;
            StepPass();
        }
    }

    /// <summary>Asks the lock whether the pass opens now (the learner's completed lessons, the cheat); called each tick and when the cheat is set.</summary>
    private void StepPass()
    {
        if (!Pass.IsOpen)
        {
            _ = Pass.Update(Lessons.IsDone, _passCheat);
        }

        SyncPassBarrier();
    }

    /// <summary>
    /// Puts the valley's edge wall as the pass has it (B07): a streamed world's body barrier stands while the pass is closed and lifts, all the way round, once it is open, and stays lifted
    /// after a load (a save restores <see cref="PassState.IsOpen"/>). A bounded grid has no barrier (its limits are its wall).
    /// </summary>
    internal void SyncPassBarrier()
    {
        if (Scene.Grid.Barrier is { } barrier)
        {
            barrier.Locked = !Pass.IsOpen;
        }
    }

    // The pass opened: the rockfall's blocks become what the valley has there without it (the road's ground and air: the heap stands over the ground, so every cell is air), set like any
    // edit so the changed chunks carry it in a save and it never comes back; a world event says it.
    private void OnPassOpened()
    {
        if (Scene.PropCells.TryGetValue(Pass.Data.Rockfall, out var cells))
        {
            foreach (var cell in cells)
            {
                _ = Scene.Grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Air);
            }
        }

        SyncPassBarrier();
        LogWorld(WorldEvents.PassOpened, null, Pass.Data.RequiredLessons.All(Lessons.IsDone) ? "lessons" : "cheat");
    }
}
