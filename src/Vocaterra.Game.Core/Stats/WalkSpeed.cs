namespace Vocaterra.Game.Core.Stats;

/// <summary>Where the walking speed's factors become the simulation's per-mille value, and the one place the product is clamped (I05a).</summary>
public static class WalkSpeed
{
    /// <summary>The slowest walk any mix of factors gives, a tenth of the normal pace (so the player always moves).</summary>
    public const int MinPermille = 100;

    /// <summary>The fastest walk any mix gives: two and a half times the normal pace (the sprint's 1.6 with the best gear stays under it).</summary>
    public const int MaxPermille = 2500;

    /// <summary>The per-mille speed of a product of factors (1 is 1000), rounded and clamped to <see cref="MinPermille"/> and <see cref="MaxPermille"/>.</summary>
    /// <param name="product">The needs', cold's, gear's and sprint's factors multiplied.</param>
    public static int Permille(double product) => double.IsFinite(product) ? Math.Clamp((int)Math.Round(1000 * product), MinPermille, MaxPermille) : 1000;
}
