namespace Vocaterra.Game.Core;

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

    /// <summary>
    /// (3D, S1) A gather tick on a block: <see cref="SimulationEvent.X"/>, <see cref="SimulationEvent.Y"/> and <see cref="SimulationEvent.Z"/> are
    /// the block, <see cref="SimulationEvent.Detail"/> the ticks spent so far and <see cref="SimulationEvent.Amount"/> the ticks it needs.
    /// </summary>
    GatherProgress = 5,

    /// <summary>
    /// (3D, S1) A block was gathered: the position is in X, Y, Z and <see cref="SimulationEvent.Detail"/> is the old block id (a cast
    /// <c>BlockId</c>). The cell now holds the block's replacement (air, or a plain bush for a berry bush).
    /// </summary>
    BlockRemoved = 6,

    /// <summary>(3D, S1) An item went into the inventory: <see cref="SimulationEvent.Detail"/> is the item id and <see cref="SimulationEvent.Amount"/> how many.</summary>
    ItemPickedUp = 7,

    /// <summary>(3D, S1) A block was placed: the position is in X, Y, Z, <see cref="SimulationEvent.Detail"/> the new block id and <see cref="SimulationEvent.Amount"/> the item id used.</summary>
    BlockPlaced = 8,

    /// <summary>(3D, S6) An empty bottle was filled at the water: the position is the water block in X, Y, Z and <see cref="SimulationEvent.Detail"/> is the new item id.</summary>
    BottleFilled = 9,

    /// <summary>(3D, S6) The player worked on water with no bottle: the position is the water block. Raised once per hold; the front-end shows a short hint (nothing is blocked).</summary>
    WaterWithoutBottle = 10,
}

/// <summary>An event a tick produced, for sounds, particles and the guide. </summary>
/// <param name="Kind">What happened.</param>
/// <param name="Tick">The tick in which it happened (the simulation's tick after that step).</param>
/// <param name="X">The block or tile column of a 3D event, otherwise 0.</param>
/// <param name="Y">The block layer of a 3D event, otherwise 0.</param>
/// <param name="Z">The block row of a 3D event, otherwise 0.</param>
/// <param name="Detail">A kind-specific number (see the kind), otherwise 0.</param>
/// <param name="Amount">A second kind-specific number (see the kind), otherwise 0.</param>
public readonly record struct SimulationEvent(SimulationEventKind Kind, long Tick, int X = 0, int Y = 0, int Z = 0, int Detail = 0, int Amount = 0);
