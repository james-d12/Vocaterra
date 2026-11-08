namespace Vocaterra.Game.Core.Menu;

/// <summary>What a row of the settings page is.</summary>
public enum MenuRowKind
{
    /// <summary>A heading over the rows below it (not selectable).</summary>
    Heading,

    /// <summary>A smaller heading over a group of rows inside a section (not selectable).</summary>
    Subheading,

    /// <summary>A line of text (not selectable).</summary>
    Note,

    /// <summary>A number moved in steps.</summary>
    Slider,

    /// <summary>One of a few named values, cycled.</summary>
    Choice,

    /// <summary>On or off.</summary>
    Toggle,

    /// <summary>A button that does something once.</summary>
    Action,
}
