namespace Vocaterra.Game.Core.Language.Learner;

/// <summary>
/// The grammatical genders the game accepts in a noun and in the learner's record (T04, research G8): <c>m</c>, <c>f</c> and <c>n</c> (the neuter of German;
/// Spanish has none). The values are the lexicon's own letters.
/// </summary>
public static class Genders
{
    /// <summary>Masculine.</summary>
    public const string Masculine = "m";

    /// <summary>Feminine.</summary>
    public const string Feminine = "f";

    /// <summary>Neuter.</summary>
    public const string Neuter = "n";

    /// <summary>Every gender, in the order the lexicon lists them (<c>m</c>, <c>f</c>, <c>n</c>).</summary>
    public static IReadOnlyList<string> All { get; } = [Masculine, Feminine, Neuter];

    /// <summary>Whether the text is a gender the game knows.</summary>
    /// <param name="gender">The gender letter.</param>
    public static bool IsKnown(string? gender) => gender is Masculine or Feminine or Neuter;
}
