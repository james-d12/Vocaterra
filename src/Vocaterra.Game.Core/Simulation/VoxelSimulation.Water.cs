using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;
using Vocaterra.Game.Core.Water.Flow;

namespace Vocaterra.Game.Core.Simulation;

public sealed partial class VoxelSimulation
{
    private readonly Breath _breath = new();
    private FlowSimulation _flow = null!;

    /// <summary>The player's air (water-plan H03). Read only for the HUD and the session; the simulation steps it.</summary>
    public Breath Breath => _breath;

    /// <summary>The flow of water (water-plan H06): the cells waiting for an update. Read only for tests and tools; the simulation steps it.</summary>
    public FlowSimulation Flow => _flow;

    /// <summary>
    /// A host action, like <see cref="TeleportTo"/>: puts a water source in a cell of air, undergrowth or flowing water (the scripted run's <c>--pour</c>), which then
    /// flows as water does. Not a game rule and not a command.
    /// </summary>
    /// <returns>Whether the cell was changed (it was inside the grid, not on a column the land rules protect (<see cref="BuildRules"/>), and was not a source already or a block that stops water).</returns>
    public bool PourWater(BlockPos cell)
    {
        if (!Grid.InBounds(cell.X, cell.Y, cell.Z) || BuildRules?.ProtectionAt(cell.X, cell.Z) is { } protection && protection != Protection.None)
        {
            return false; // outside the world, or a protected column (Y09: the dev aids go through the land rules too)
        }

        var block = Grid.GetBlock(cell.X, cell.Y, cell.Z);
        if (block == BlockId.Water)
        {
            return Grid.SetWater(cell.X, cell.Y, cell.Z, WaterLevel.Source);
        }

        return IsEmptyOrDecor(block) && Grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Water);
    }

    // Makes the flow and registers it with the grid, so any change to a block wakes the water round it.
    private void StartFlow()
    {
        _flow = new FlowSimulation(Grid, () => Tick);
        Grid.AddListener(_flow);
    }

    /// <summary>Runs the flow step (every 15th tick), reads the water round the body after the move and steps the breath, raising <see cref="SimulationEventKind.OutOfAir"/> and <see cref="SimulationEventKind.BreathRestored"/>.</summary>
    private void StepWater()
    {
        _ = _flow.Step(Tick);
        var events = _breath.Step(Immersion.Of(Grid, Player).Eye);
        if ((events & BreathEvents.OutOfAir) != 0)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.OutOfAir, Tick));
        }

        if ((events & BreathEvents.Restored) != 0)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.BreathRestored, Tick));
        }
    }

    // The cells waiting to flow are part of the state: due tick, x, y, z of each, in queue order. Nothing waiting adds nothing, so the digests of dry runs are as they were.
    private void AppendFlowDigest(IncrementalHash hash)
    {
        var waiting = _flow.Queue.Snapshot();
        if (waiting.Count == 0)
        {
            return;
        }

        var buffer = new byte[waiting.Count * 20];
        for (var i = 0; i < waiting.Count; i++)
        {
            var span = buffer.AsSpan(i * 20);
            BinaryPrimitives.WriteInt64LittleEndian(span, waiting[i].Due);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], waiting[i].Position.X);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], waiting[i].Position.Y);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], waiting[i].Position.Z);
        }

        hash.AppendData(buffer);
    }
}
