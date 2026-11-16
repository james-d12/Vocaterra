namespace Vocaterra.Game.Core.Temperature;

/// <summary>The fixed-point step of the temperature terms: a 0 to 1 channel as whole thousandths, so the sums are integers.</summary>
internal static class Permille
{
    /// <summary>A share from 0 to 1 as thousandths, rounded half away from zero (outside 0 to 1 is clamped, and so is a number that is not a number).</summary>
    /// <param name="share">The share.</param>
    public static int Of(double share) => double.IsNaN(share) ? 0 : (int)Math.Round(Math.Clamp(share, 0, 1) * 1000, MidpointRounding.AwayFromZero);

    /// <summary>A number of tenths times thousandths, as tenths (truncated towards zero, the same on every machine).</summary>
    /// <param name="tenths">The tenths.</param>
    /// <param name="thousandths">The share in thousandths.</param>
    public static int Times(int tenths, int thousandths) => (int)((long)tenths * thousandths / 1000);
}
