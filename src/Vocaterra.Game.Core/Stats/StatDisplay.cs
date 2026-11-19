using System.Globalization;

namespace Vocaterra.Game.Core.Stats;

/// <summary>How one stat reads on the equipment panel's stats line (I04): its key, how its number is printed and the Spanish noun that names it, if the lexicon has one. A row of <see cref="Defaults"/>.</summary>
/// <param name="Key">The stat's key (<see cref="StatKeys"/>).</param>
/// <param name="Format">How the number is made of the sheet.</param>
/// <param name="Lemma">The Spanish noun, or <see langword="null"/> for none.</param>
public sealed record StatDisplay(string Key, StatDisplayFormat Format, string? Lemma)
{
    /// <summary>The stats the panel shows, in the order it shows them: armour and warmth first, then what the accessories change.</summary>
    public static IReadOnlyList<StatDisplay> Defaults { get; } =
    [
        new(StatKeys.Armour, StatDisplayFormat.Rating, "armadura"),
        new(StatKeys.Warmth, StatDisplayFormat.Tenths, "calor"),
        new(StatKeys.WalkSpeed, StatDisplayFormat.Percent, null),
        new(StatKeys.HungerDrain, StatDisplayFormat.Percent, null),
        new(StatKeys.ThirstDrain, StatDisplayFormat.Percent, null),
        new(StatKeys.MaxStamina, StatDisplayFormat.Whole, null),
        new(StatKeys.Light, StatDisplayFormat.Whole, null), // I10b, QA-2026-11-19-ui-2: the lantern's and the amulet's light (the sum of the worn items', 0 to 16)
    ];

    /// <summary>The chips of a stat sheet: every row whose stat is not at its default, in the table's order.</summary>
    /// <param name="sheet">The player's stats.</param>
    /// <param name="rows">The table of rows, or <see langword="null"/> for <see cref="Defaults"/>.</param>
    /// <param name="hasNoun">Whether the game has a noun for a lemma (the catalogue and the lexicon); a lemma that fails it is left out of the chip.</param>
    public static IReadOnlyList<StatChip> Chips(StatSheet sheet, IReadOnlyList<StatDisplay>? rows = null, Func<string, bool>? hasNoun = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var chips = new List<StatChip>();
        foreach (var row in rows ?? Defaults)
        {
            if (row.Text(sheet) is { } text)
            {
                chips.Add(new StatChip(row.Key, text, row.Lemma is { } lemma && (hasNoun?.Invoke(lemma) ?? true) ? lemma : null));
            }
        }

        return chips;
    }

    /// <summary>The printed number of the row's stat on a sheet, or <see langword="null"/> when the stat is at its default (nothing is shown for it).</summary>
    /// <param name="sheet">The player's stats.</param>
    public string? Text(StatSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        switch (Format)
        {
            case StatDisplayFormat.Whole:
                var whole = sheet.GetWhole(Key);
                return whole == 0 ? null : Signed(whole.ToString(CultureInfo.InvariantCulture), whole);
            case StatDisplayFormat.Rating:
                var rating = sheet.GetWhole(Key);
                return rating == 0 ? null : rating.ToString(CultureInfo.InvariantCulture);
            case StatDisplayFormat.Tenths:
                var tenths = sheet.GetWhole(Key);
                return tenths == 0 ? null : Signed((tenths / 10.0).ToString("0.#", CultureInfo.InvariantCulture), tenths);
            case StatDisplayFormat.Percent:
                var percent = (int)Math.Round((sheet.Factor(Key) - 1.0) * 100.0, MidpointRounding.AwayFromZero);
                return percent == 0 ? null : Signed(percent.ToString(CultureInfo.InvariantCulture) + "%", percent);
            default:
                return null;
        }
    }

    private static string Signed(string text, int sign) => sign > 0 && !text.StartsWith('+') ? "+" + text : text;
}
