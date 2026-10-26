using Vocaterra.Game.Core.Simulation;

namespace Vocaterra.Game.Frame;

/// <summary>Something that follows the world tick by tick: after each step it reads the tick's state and events (and writes nothing back to the world).</summary>
public interface ITickObserver
{
    /// <summary>Once a tick, after the step, with the commands the tick ran.</summary>
    void AfterTick(IReadOnlyList<VoxelCommand> commands);
}

/// <summary>A tick observer made of an action.</summary>
/// <param name="afterTick">What it does after each tick.</param>
public sealed class DelegateTickObserver(Action<IReadOnlyList<VoxelCommand>> afterTick) : ITickObserver
{
    /// <inheritdoc />
    public void AfterTick(IReadOnlyList<VoxelCommand> commands) => afterTick(commands);
}
