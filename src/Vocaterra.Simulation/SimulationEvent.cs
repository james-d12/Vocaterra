namespace Vocaterra.Simulation;

/// <summary>What happened during a tick.</summary>
public enum SimulationEventKind
{
    /// <summary>The player left the ground with a jump.</summary>
    PlayerJumped = 0,

    /// <summary>The player touched the ground after being in the air.</summary>
    PlayerLanded = 1,

    /// <summary>A tile was mined and its drop (if any) went to the inventory.</summary>
    TileMined = 2,

    /// <summary>A block was placed from the selected hotbar slot.</summary>
    TilePlaced = 3,

    /// <summary>A recipe was crafted: the ingredients left the inventory and the output went in.</summary>
    ItemCrafted = 4,
}

/// <summary>An event a tick produced, for sounds, particles and the guide. </summary>
/// <param name="Kind">What happened.</param>
/// <param name="Tick">The tick in which it happened (the value of <see cref="GameSimulation.Tick"/> after that step).</param>
public readonly record struct SimulationEvent(SimulationEventKind Kind, long Tick);
