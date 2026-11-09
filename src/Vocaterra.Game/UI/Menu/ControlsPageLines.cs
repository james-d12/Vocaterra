using FontStashSharp;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Graphics;

namespace Vocaterra.Game.UI.Menu;

/// <summary>One drawn line of the controls page: a group's heading, or a line of a control (the first carries its keys).</summary>
/// <param name="IsHeading">Whether this is a group's heading.</param>
/// <param name="Keys">The keys, on the first line of a control; empty on the others.</param>
/// <param name="Text">The line of the description, or the group's name.</param>
public readonly record struct ControlsPageLine(bool IsHeading, string Keys, string Text);

/// <summary>The controls page's rows (<see cref="ControlsListing"/>, made from the control table) wrapped into the lines the panel draws.</summary>
public static class ControlsPageLines
{
    /// <summary>Wraps the page's rows to the panel's columns.</summary>
    /// <param name="rows">The page's rows.</param>
    /// <param name="layout">The panel's layout.</param>
    /// <param name="font">The font the lines are drawn in.</param>
    public static IReadOnlyList<ControlsPageLine> Build(IReadOnlyList<ControlsRow> rows, MenuLayout layout, SpriteFontBase font)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(font);
        var lines = new List<ControlsPageLine>();
        foreach (var row in rows)
        {
            if (row.IsHeading)
            {
                lines.Add(new ControlsPageLine(true, string.Empty, row.Text));
                continue;
            }

            var keys = TextWrap.Wrap(row.Keys, layout.KeysWidth - 8, t => DrawHelpers.Width(font, t));
            var text = TextWrap.Wrap(row.Text, layout.TextWidth, t => DrawHelpers.Width(font, t));
            for (var i = 0; i < Math.Max(keys.Count, text.Count); i++)
            {
                lines.Add(new ControlsPageLine(false, i < keys.Count ? keys[i] : string.Empty, i < text.Count ? text[i] : string.Empty));
            }
        }

        return lines;
    }
}
