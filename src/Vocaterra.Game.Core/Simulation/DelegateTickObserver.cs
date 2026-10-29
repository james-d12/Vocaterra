
namespace Vocaterra.Game.Core.Simulation;

/// <summary>A tick observer made of an action.</summary>
/// <param name="afterTick">What it does after each tick.</param>
public sealed class DelegateTickObserver(Action<IReadOnlyList<VoxelCommand>> afterTick) : ITickObserver
{
    /// <inheritdoc />
    public void AfterTick(IReadOnlyList<VoxelCommand> commands) => afterTick(commands);
}
