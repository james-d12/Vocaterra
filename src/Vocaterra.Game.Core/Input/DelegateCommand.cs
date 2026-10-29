namespace Vocaterra.Game.Core.Input;

/// <summary>A command made of an action.</summary>
/// <param name="execute">What the command does.</param>
public sealed class DelegateCommand(Action execute) : IGameCommand
{
    /// <inheritdoc />
    public void Execute() => execute();
}
