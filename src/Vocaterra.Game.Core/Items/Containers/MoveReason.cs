namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>
/// Why a move was refused (I02, inventory-plan 3.9). A refusal changes nothing: no container and not the held stack. When a container's own rules refuse (the equipment's), the reason is <see cref="SlotRefused"/> and
/// <see cref="MoveResult.Equip"/> says why in <see cref="Gear.EquipResult"/> terms. The numbers are carried in <see cref="Event.SimulationEvent.Detail"/>; they are never changed or reused, a new reason is appended.
/// </summary>
public enum MoveReason
{
    /// <summary>The move was made.</summary>
    Done = 0,

    /// <summary>No container has the id the command named.</summary>
    NoSuchContainer = 1,

    /// <summary>The container has no slot with that index (the bag's active slots only).</summary>
    NoSuchSlot = 2,

    /// <summary>There was nothing to pick up, and nothing held to put down.</summary>
    NothingToMove = 3,

    /// <summary>Right click: one cannot be put down on a stack of another item.</summary>
    DifferentItem = 4,

    /// <summary>Right click: the stack of the same item is full already.</summary>
    SlotFull = 5,

    /// <summary>A quick move found no room for any of the stack where it would go.</summary>
    NoRoom = 6,

    /// <summary>The stack is above what the slot holds (a count above the item's stack limit).</summary>
    StackTooLarge = 7,

    /// <summary>The container refused the stack: for the equipment <see cref="MoveResult.Equip"/> says why.</summary>
    SlotRefused = 8,

    /// <summary>The command's action is not one the operations know.</summary>
    NoSuchAction = 9,
}
