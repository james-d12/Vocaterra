namespace Vocaterra.Game.Core.Stats;

/// <summary>One entry of the equipment panel's stats line (I04): a stat that is not at its default, as the icon's key, the number to print and the Spanish noun that names it when the lexicon has one.</summary>
/// <param name="Key">The stat's key (<see cref="StatKeys"/>), which the panel's icons are found by.</param>
/// <param name="Value">The number as printed (<c>3</c>, <c>+1.5</c>, <c>+10%</c>, <c>-20%</c>).</param>
/// <param name="Lemma">The Spanish noun for the stat (<c>armadura</c>), or <see langword="null"/> when the game has no word for it yet (the chip is an icon and a number only).</param>
public readonly record struct StatChip(string Key, string Value, string? Lemma);
