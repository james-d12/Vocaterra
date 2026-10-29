namespace Vocaterra.Game.Core.Input;

/// <summary>A key that runs a command when it goes down, while its condition holds.</summary>
/// <param name="Key">The key.</param>
/// <param name="Command">What it runs.</param>
/// <param name="When">The condition, or <see langword="null"/> for always.</param>
public sealed record KeyBinding(GameKey Key, IGameCommand Command, Func<bool>? When = null);
