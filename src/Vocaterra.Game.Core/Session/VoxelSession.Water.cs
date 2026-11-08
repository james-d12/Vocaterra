using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Session;

public sealed partial class VoxelSession
{
    private readonly DrowningDamage _drowning = new(new NoPlayerHealth());

    /// <summary>The health the water hurts. A <see cref="NoPlayerHealth"/> until the real health is joined (water-plan H11).</summary>
    internal IPlayerHealth PlayerHealth
    {
        get => _drowning.Health;
        set => _drowning.Health = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Feeds the tick's out-of-air events to the drowning rule.</summary>
    private void StepWaterEvents()
    {
        foreach (var e in Simulation.Events)
        {
            if (e.Kind == SimulationEventKind.OutOfAir)
            {
                _drowning.Apply(BreathEvents.OutOfAir);
            }
        }
    }
}
