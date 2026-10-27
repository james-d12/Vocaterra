namespace Vocaterra.Game.Core.Input;

/// <summary>Something the player can ask the game to do: a key binding runs it, and so can another path to the same thing (the book item opens the book as J does).</summary>
public interface IGameCommand
{
    /// <summary>Does it.</summary>
    void Execute();
}
