using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;

namespace Vocaterra.Game.Input;

/// <summary>The one table between the engine's keys and the game's (D-p2-1a): each <see cref="GameKey"/> is the MonoGame key of the same name.</summary>
public static class KeyMap
{
    private static readonly Keys[] ToEngine = Enum.GetValues<GameKey>().Select(static key => Enum.Parse<Keys>(key.ToString())).ToArray();

    private static readonly Dictionary<Keys, GameKey> FromEngine = Enum.GetValues<GameKey>().ToDictionary(static key => ToEngine[(int)key]);

    /// <summary>The engine's key for a game key.</summary>
    /// <param name="key">The game key.</param>
    public static Keys ToKeys(GameKey key) => ToEngine[(int)key];

    /// <summary>The game key for an engine key, or <see langword="null"/> for a key the game does not name.</summary>
    /// <param name="key">The engine's key.</param>
    public static GameKey? ToGameKey(Keys key) => FromEngine.TryGetValue(key, out var game) ? game : null;
}
