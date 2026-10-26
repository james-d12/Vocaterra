namespace Vocaterra.Game.Input;

/// <summary>Something the player can ask the game to do: a key binding runs it, and so can another path to the same thing (the book item opens the book as J does).</summary>
public interface IGameCommand
{
    /// <summary>Does it.</summary>
    void Execute();
}

/// <summary>A command made of an action.</summary>
/// <param name="execute">What the command does.</param>
public sealed class DelegateCommand(Action execute) : IGameCommand
{
    /// <inheritdoc />
    public void Execute() => execute();
}
