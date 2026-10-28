namespace Vocaterra.Game.Core.Reporting;

/// <summary>The keys the report panel takes (S21), named by what they do; the game maps the keyboard to them.</summary>
public enum ReportKey
{
    /// <summary>Cancel: the report is dropped and nothing is kept.</summary>
    Escape,

    /// <summary>Switch between Bug and Feature request.</summary>
    Tab,

    /// <summary>A new line in the text.</summary>
    Enter,

    /// <summary>Save the report (Ctrl+Enter).</summary>
    Save,

    /// <summary>Remove the character before the caret.</summary>
    Backspace,

    /// <summary>Remove the character after the caret.</summary>
    Delete,

    /// <summary>Caret left.</summary>
    Left,

    /// <summary>Caret right.</summary>
    Right,

    /// <summary>Caret up a line.</summary>
    Up,

    /// <summary>Caret down a line.</summary>
    Down,

    /// <summary>Caret to the start of the line.</summary>
    Home,

    /// <summary>Caret to the end of the line.</summary>
    End,
}
