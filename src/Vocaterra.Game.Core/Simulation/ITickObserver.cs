
namespace Vocaterra.Game.Core.Simulation;

/// <summary>Something that follows the world tick by tick: after each step it reads the tick's state and events (and writes nothing back to the world).</summary>
public interface ITickObserver
{
    /// <summary>Once a tick, after the step, with the commands the tick ran.</summary>
    void AfterTick(IReadOnlyList<VoxelCommand> commands);
}
