using Vocaterra.Game.Core.Damage;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Session;

public sealed partial class VoxelSession
{
    private DrowningDamage? _drowning;

    // The drowning rule over the needs' health. It hurts only while the needs run in active play (off, like hunger and thirst, nothing takes health).
    private DrowningDamage Drowning => _drowning ??= new DrowningDamage(Modified(new NeedsHealth(Needs, () => NeedsEnabled && PlayActive)));

    // I05a: the health behind the damage pipeline, which runs the modifiers game/damage.json names for each cause over the stat sheet.
    private ModifiedHealth Modified(IPlayerHealth inner) => new(inner, Data.Damage, DamageModifiers.Standard(), () => Simulation.Stats);

    /// <summary>The health the water hurts: <see cref="NeedsHealth"/> over the player's needs, unless a test puts another in.</summary>
    internal IPlayerHealth PlayerHealth
    {
        get => Drowning.Health;
        set => Drowning.Health = Modified(value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>Feeds the tick's out-of-air events to the drowning rule.</summary>
    private void StepWaterEvents()
    {
        foreach (var e in Simulation.Events)
        {
            if (e.Kind == SimulationEventKind.OutOfAir)
            {
                Drowning.Apply(BreathEvents.OutOfAir);
            }
        }

        if (NeedsEnabled && PlayActive)
        {
            SyncNeedStages();
            DieIfDead(); // drowning takes health after the needs' tick: the death flow is the needs' own
        }
    }
}
