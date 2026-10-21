namespace Vocaterra.Simulation;

/// <summary>The kinds of command a front-end sends. New kinds are appended; an unknown kind is ignored by older builds.</summary>
public enum PlayerCommandKind
{
    /// <summary>Move left during this tick (a held key sends it every tick).</summary>
    MoveLeft = 0,

    /// <summary>Move right during this tick.</summary>
    MoveRight = 1,

    /// <summary>The jump button is down during this tick (a press, then a hold for a higher jump; send it every tick it is held).</summary>
    Jump = 2,

    /// <summary>Select a hotbar slot: <see cref="PlayerCommand.X"/> is the slot (0 to 9). Applied before mining and placing in a tick.</summary>
    SelectSlot = 3,

    /// <summary>
    /// Mine the tile at (<see cref="PlayerCommand.X"/>, <see cref="PlayerCommand.Y"/>). Held: send it every tick the button is down; progress
    /// builds up while the target and the selected tool stay the same, and a tick without it resets the progress.
    /// </summary>
    Mine = 4,

    /// <summary>
    /// Place the selected hotbar item at the tile (<see cref="PlayerCommand.X"/>, <see cref="PlayerCommand.Y"/>). A press: one block per command.
    /// </summary>
    Place = 5,

    /// <summary>
    /// Craft the recipe at index <see cref="PlayerCommand.X"/> of <see cref="GameSimulation.Recipes"/>. A press: one craft per command,
    /// applied after the placements in a tick.
    /// </summary>
    Craft = 6,
}

/// <summary>
/// One input for one tick. The state-like commands (move, jump) describe what is held <em>during</em> the tick, so a front-end sends the
/// held ones every tick. A command has a kind and two integer arguments for the kinds that need a target (a tile position, a slot). A kind
/// the simulation does not know yet is ignored, so commands added in later milestones never break an older build.
/// </summary>
/// <param name="Kind">What the command asks for.</param>
/// <param name="X">The first argument (unused by the movement commands).</param>
/// <param name="Y">The second argument (unused by the movement commands).</param>
public readonly record struct PlayerCommand(PlayerCommandKind Kind, int X = 0, int Y = 0)
{
    /// <summary>Move left.</summary>
    public static PlayerCommand MoveLeft { get; } = new(PlayerCommandKind.MoveLeft);

    /// <summary>Move right.</summary>
    public static PlayerCommand MoveRight { get; } = new(PlayerCommandKind.MoveRight);

    /// <summary>Jump button down.</summary>
    public static PlayerCommand Jump { get; } = new(PlayerCommandKind.Jump);

    /// <summary>Select the hotbar slot <paramref name="slot"/> (0 to 9).</summary>
    public static PlayerCommand SelectSlot(int slot) => new(PlayerCommandKind.SelectSlot, slot);

    /// <summary>Mine the tile at (<paramref name="x"/>, <paramref name="y"/>) this tick.</summary>
    public static PlayerCommand Mine(int x, int y) => new(PlayerCommandKind.Mine, x, y);

    /// <summary>Place the selected item at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static PlayerCommand Place(int x, int y) => new(PlayerCommandKind.Place, x, y);

    /// <summary>Craft the recipe at <paramref name="recipeIndex"/> of the simulation's recipe list.</summary>
    public static PlayerCommand Craft(int recipeIndex) => new(PlayerCommandKind.Craft, recipeIndex);
}
