using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Simulation;

public sealed partial class VoxelSimulation
{
    private readonly Breath _breath = new();

    /// <summary>The player's air (water-plan H03). Read only for the HUD and the session; the simulation steps it.</summary>
    public Breath Breath => _breath;

    /// <summary>Reads the water round the body after the move and steps the breath, raising <see cref="SimulationEventKind.OutOfAir"/> and <see cref="SimulationEventKind.BreathRestored"/>.</summary>
    private void StepWater()
    {
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
}
