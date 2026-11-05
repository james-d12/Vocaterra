namespace Vocaterra.Game.Core.Input;

/// <summary>
/// The keys of the world's commands by name (content-plan 3.2, C04): <see cref="KeyBindings"/> binds them, and the support language names them with
/// <c>{key:&lt;command&gt;}</c> (<c>Hold {key:listen} to speak</c>), spelled with the locale's <c>key.&lt;GameKey&gt;</c>, so a text never names a key the
/// bindings do not use. The keys of a screen's own controls (the dialogue panel's Enter, the book's Tab) are fixed in that screen and named in its text.
/// </summary>
public static class InputKeys
{
    /// <summary>The default key of each named command.</summary>
    public static IReadOnlyDictionary<string, GameKey> Defaults { get; } = new Dictionary<string, GameKey>(StringComparer.Ordinal)
    {
        ["listen"] = GameKey.V,
        ["listenAlt"] = GameKey.Space,
        ["type"] = GameKey.T,
        ["talk"] = GameKey.E,
        ["controls"] = GameKey.F1,
        ["nextTip"] = GameKey.Enter,
        ["sleep"] = GameKey.Enter, // while the session's closing card is up: sleep until the next session (V12c)
        ["book"] = GameKey.J,
        ["crafting"] = GameKey.C,
        ["inventory"] = GameKey.I,
        ["releaseMouse"] = GameKey.Escape,
        ["endRun"] = GameKey.F10,
        ["view"] = GameKey.F5,
        ["save"] = GameKey.F6,
        ["textSize"] = GameKey.F9,
        ["viewEffects"] = GameKey.F7, // N05: the needs' effects on the view (dim edges, a blink, red edges) on or off
        ["debug"] = GameKey.F3,
        ["sprint"] = GameKey.LeftShift, // held: walk about 1.6 times as fast while stamina lasts (N02)
    };

    /// <summary>The key of a named command.</summary>
    /// <param name="command">The command's name (<c>listen</c>).</param>
    /// <exception cref="KeyNotFoundException">No command has that name.</exception>
    public static GameKey Of(string command) => Defaults[command];
}
