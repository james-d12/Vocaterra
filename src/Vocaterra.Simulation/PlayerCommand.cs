namespace Vocaterra.Simulation;

/// <summary>The kinds of command a front-end sends. New kinds (use a tool on a tile, place a block, select a hotbar slot) are appended.</summary>
public enum PlayerCommandKind
{
    /// <summary>Move left during this tick (a held key sends it every tick).</summary>
    MoveLeft = 0,

    /// <summary>Move right during this tick.</summary>
    MoveRight = 1,

    /// <summary>The jump button is down during this tick (a press, then a hold for a higher jump; send it every tick it is held).</summary>
    Jump = 2,
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
}
