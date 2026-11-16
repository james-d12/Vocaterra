namespace Vocaterra.Game.Core.Event;

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

    /// <summary>(Water, H03) The player's air is at zero under water: raised the tick it ran out and every second while it stays out. <see cref="SimulationEvent.Detail"/> is 0.</summary>
    OutOfAir = 11,

    /// <summary>(Water, H03) The air became full again after the player came up.</summary>
    BreathRestored = 12,

    /// <summary>(Atmosphere, E01) The day clock crossed into another phase: <see cref="SimulationEvent.Detail"/> is the new phase (a cast <c>DayPhase</c>). Raised once at each boundary of the running clock, not for a jump (a sleep, a held hour, a load).</summary>
    DayPhaseChanged = 13,

    /// <summary>(Atmosphere, E04) The weather changed kind (a spell's end, a morning roll, a forced kind): <see cref="SimulationEvent.Detail"/> is the new kind's index in the weather data. Raised once per change, not for a load.</summary>
    WeatherChanged = 14,

    /// <summary>(Land, Y09) A block that would have been placed was refused because its column is protected: the cell is in X, Y, Z and <see cref="SimulationEvent.Detail"/> is the protection (a cast <c>Land.Protection</c>: village, camp or road). The item stays in the bag.</summary>
    PlaceRefused = 15,

    /// <summary>(Land, Y09) The player worked on a plank block outside their own land, where planks cannot be taken back: the position is the block. Raised once per hold, like <see cref="WaterWithoutBottle"/>.</summary>
    TakeBackRefused = 16,

    /// <summary>(Land, Y12) The player held the gather button on a block of their camp for the whole time it takes to pack it: the position is the block. The session packs the camp up.</summary>
    CampPackRequested = 17,

    /// <summary>(Equipment, I03a) An item was put on: <see cref="SimulationEvent.Detail"/> is the item id, <see cref="SimulationEvent.Amount"/> the flat index of the slot (<c>EquipmentData.FlatIndex</c>).</summary>
    ItemEquipped = 18,

    /// <summary>(Equipment, I03a) An item was taken off: <see cref="SimulationEvent.Detail"/> is the item id, <see cref="SimulationEvent.Amount"/> the flat index of the slot it left.</summary>
    ItemUnequipped = 19,

    /// <summary>(Equipment, I03a) An equip or unequip was refused and changed nothing: <see cref="SimulationEvent.Detail"/> is the reason (a cast <c>EquipResult</c>), <see cref="SimulationEvent.Amount"/> the flat index of the slot, or -1 when the slot does not exist.</summary>
    EquipRefused = 20,
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
