using Vocaterra.Game.Core.Input;

namespace Vocaterra.Game.Core.Menu;

/// <summary>
/// The controls page's content, made from the control table (<see cref="ControlTable"/>) and nothing else: each group's heading, then each of its controls with its keys spelled by the
/// locale (<c>key.&lt;GameKey&gt;</c>) and its description (<c>ui.control.&lt;id&gt;</c>). Because the keys come from the table the game's input reads, the page cannot name a key the game does not use.
/// </summary>
public static class ControlsListing
{
    /// <summary>The locale's interface words the page asks for: one per group and per control, and one per mouse control.</summary>
    public static IReadOnlyList<string> UiKeys { get; } =
    [
        .. Enum.GetValues<ControlGroup>().Select(GroupKey),
        .. ControlTable.All.Select(static entry => "control." + entry.Id),
        .. Enum.GetValues<MouseControl>().Where(static mouse => mouse != MouseControl.None).Select(MouseKey),
    ];

    /// <summary>The locale key of a group's name.</summary>
    /// <param name="group">The group.</param>
    public static string GroupKey(ControlGroup group) => "controlGroup" + group;

    /// <summary>The locale key of a mouse control's name.</summary>
    /// <param name="mouse">The mouse control.</param>
    public static string MouseKey(MouseControl mouse) => "mouse" + mouse;

    /// <summary>The rows of the page in order.</summary>
    /// <param name="ui">The locale's interface words by key (without <c>ui.</c>).</param>
    /// <param name="keyName">The locale's name of a key.</param>
    public static IReadOnlyList<ControlsRow> Rows(Func<string, string> ui, Func<GameKey, string> keyName)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(keyName);
        var rows = new List<ControlsRow>();
        foreach (var group in Enum.GetValues<ControlGroup>())
        {
            rows.Add(new ControlsRow(true, string.Empty, ui(GroupKey(group))));
            rows.AddRange(ControlTable.All.Where(entry => entry.Group == group).Select(entry => new ControlsRow(false, KeysText(entry, ui, keyName), ui("control." + entry.Id))));
        }

        return rows;
    }

    /// <summary>The keys of a control as the page spells them: each by name with "or" between, a run as "first to last", and the mouse control after them.</summary>
    /// <param name="entry">The control.</param>
    /// <param name="ui">The locale's interface words by key.</param>
    /// <param name="keyName">The locale's name of a key.</param>
    public static string KeysText(ControlEntry entry, Func<string, string> ui, Func<GameKey, string> keyName)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(keyName);
        var parts = new List<string>();
        if (entry.Keys.Count > 0)
        {
            parts.Add(entry.Range && entry.Keys.Count > 1
                ? ui("controlsRange").Replace("{first}", keyName(entry.Keys[0]), StringComparison.Ordinal).Replace("{last}", keyName(entry.Keys[^1]), StringComparison.Ordinal)
                : string.Join(" " + ui("controlsOr") + " ", entry.Keys.Select(keyName)));
        }

        if (entry.Mouse != MouseControl.None)
        {
            parts.Add(ui(MouseKey(entry.Mouse)));
        }

        return string.Join(" " + ui("controlsOr") + " ", parts);
    }
}
