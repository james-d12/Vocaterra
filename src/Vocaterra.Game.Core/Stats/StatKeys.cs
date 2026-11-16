using System.Text.RegularExpressions;

namespace Vocaterra.Game.Core.Stats;

/// <summary>
/// The stat keys code reads by name (I03a). A key is an open lowercase-first camelCase word in data (<c>warmth</c>, <c>walkSpeed</c>, <c>maxStamina</c>); a stat nobody reads yet needs no member here, and
/// a new reader adds its own constant beside the first one.
/// </summary>
public static partial class StatKeys
{
    /// <summary>What the player wears against the cold, in tenths of a degree (the temperature system's feeling adds it, I06).</summary>
    public const string Warmth = "warmth";

    /// <summary>Whether <paramref name="key"/> is a well formed stat key: a lowercase letter, then letters and digits.</summary>
    /// <param name="key">The key.</param>
    public static bool IsWellFormed(string? key) => key is not null && Pattern().IsMatch(key);

    [GeneratedRegex("^[a-z][a-zA-Z0-9]*$")]
    private static partial Regex Pattern();
}
