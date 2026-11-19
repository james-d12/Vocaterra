namespace Vocaterra.Game.Core.Stats;

/// <summary>How a stat's number is read off the sheet for the stats line (I04).</summary>
public enum StatDisplayFormat
{
    /// <summary>The stat's value rounded to a whole number (the stamina bonus).</summary>
    Whole = 0,

    /// <summary>The stat's value rounded to a whole number and printed with no sign (an armour rating).</summary>
    Rating = 3,

    /// <summary>The stat's value in tenths of a degree, printed with one decimal at most (warmth).</summary>
    Tenths = 1,

    /// <summary>The stat's factor as a percentage change from 1 (walk speed, the drains).</summary>
    Percent = 2,
}
