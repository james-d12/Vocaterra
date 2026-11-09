namespace Vocaterra.Game.Core.Input;

/// <summary>
/// The keys of the controls by name (content-plan 3.2, C04), read from the one <see cref="ControlTable"/>: <see cref="KeyBindings"/> binds them, and the support language
/// names them with <c>{key:&lt;control&gt;}</c> (<c>Hold {key:listen} to speak</c>), spelled with the locale's <c>key.&lt;GameKey&gt;</c>, so a text never names a key the bindings
/// do not use.
/// </summary>
public static class InputKeys
{
    /// <summary>The main key of each control that has a key, by the control's name.</summary>
    public static IReadOnlyDictionary<string, GameKey> Defaults { get; } = ControlTable.All
        .Where(static entry => entry.Keys.Count > 0)
        .ToDictionary(static entry => entry.Id, static entry => entry.Key, StringComparer.Ordinal);

    /// <summary>The main key of a named control.</summary>
    /// <param name="command">The control's name (<c>listen</c>).</param>
    /// <exception cref="KeyNotFoundException">No control with a key has that name.</exception>
    public static GameKey Of(string command) => Defaults[command];
}
