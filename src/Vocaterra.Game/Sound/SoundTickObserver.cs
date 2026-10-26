using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Frame;

namespace Vocaterra.Game.Sound;

/// <summary>The sounds of a tick (S24, S25): the simulation's events, the footsteps and the ambient beds; they read the tick and write nothing back.</summary>
public sealed class SoundTickObserver(SoundFrontend sound, VoxelSession session) : ITickObserver
{
    /// <inheritdoc />
    public void AfterTick(IReadOnlyList<VoxelCommand> commands)
    {
        var sim = session.Simulation;
        sound.ObserveSimulation(sim.Events, (x, y, z) => sim.Grid.GetBlock(x, y, z));
        sound.ObserveWorld(sim.Tick, sim.Events, sim.Player, (x, y, z) => sim.Grid.GetBlock(x, y, z), session.FireLit); // the footsteps and the beds (S25)
        sound.Scripted.Tick(); // the cues of the frame play together, so an answer's chime and a step's chime fold into one
    }
}
