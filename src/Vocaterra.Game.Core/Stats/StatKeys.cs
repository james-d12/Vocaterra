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

    /// <summary>
    /// A factor on walking speed (I05a): 1 leaves it alone, 0.9 is a tenth slower, 1.1 a tenth faster. Worn items multiply (<see cref="StatCombine.Factor"/>: two 10% boots are 21%, never a sum that could pass 100%).
    /// Read by the session's tick beside the needs', the cold's and the sprint's, and the product is clamped in <see cref="WalkSpeed"/>.
    /// </summary>
    public const string WalkSpeed = "walkSpeed";

    /// <summary>A factor on how fast hunger falls (I05a): 1 leaves it alone, 0.8 is a fifth slower. Items multiply (<see cref="StatCombine.Factor"/>); read by the needs, on top of the needs rate and relaxed mode.</summary>
    public const string HungerDrain = "hungerDrain";

    /// <summary>A factor on how fast thirst falls (I05a): the same rule as <see cref="HungerDrain"/>, for thirst.</summary>
    public const string ThirstDrain = "thirstDrain";

    /// <summary>A bonus to stamina's ceiling, in the stamina meter's own units (the meter is 0 to 100; I05a). Items add (<see cref="StatCombine.Sum"/>); the needs add it where the drives' stage penalties are taken off the ceiling.</summary>
    public const string MaxStamina = "maxStamina";

    /// <summary>An armour rating, a whole number (I05a). Items add (<see cref="StatCombine.Sum"/>); read only by the damage pipeline's armour modifier, for the causes <c>game/damage.json</c> names.</summary>
    public const string Armour = "armour";

    /// <summary>Whether <paramref name="key"/> is a well formed stat key: a lowercase letter, then letters and digits.</summary>
    /// <param name="key">The key.</param>
    public static bool IsWellFormed(string? key) => key is not null && Pattern().IsMatch(key);

    [GeneratedRegex("^[a-z][a-zA-Z0-9]*$")]
    private static partial Regex Pattern();
}
