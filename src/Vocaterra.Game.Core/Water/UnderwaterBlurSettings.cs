using System.Globalization;

namespace Vocaterra.Game.Core.Water;

/// <summary>
/// What the underwater pass does for one <see cref="UnderwaterBlur"/> level: how many times smaller the blurred copy is, how much of the final image it
/// supplies, and how far the copy's four draws are offset. A value object the pass reads, so the pass holds no setting logic.
/// </summary>
/// <param name="Downscale">How many times smaller than the picture the blurred copy is (1 when there is no blur).</param>
/// <param name="Weight">The share of the final image that comes from the blurred copy, 0 to 1 (0 when there is no blur).</param>
/// <param name="OffsetPixels">How far, in picture pixels, each of the copy's four draws is moved away from the middle.</param>
public readonly record struct UnderwaterBlurSettings(int Downscale, float Weight, float OffsetPixels)
{
    /// <summary>The number of offset draws of the blurred copy (left, right, up and down).</summary>
    public const int Taps = 4;

    /// <summary>The level used when none is given.</summary>
    public const UnderwaterBlur Default = UnderwaterBlur.Low;

    /// <summary>The level in force: the player's choice, or off while the view effects are switched off (the accessibility switch; the fog and the tint stay).</summary>
    /// <param name="chosen">The setting.</param>
    /// <param name="effectsOn">Whether the view effects are on.</param>
    public static UnderwaterBlur Effective(UnderwaterBlur chosen, bool effectsOn) => effectsOn ? chosen : UnderwaterBlur.Off;

    /// <summary>Whether the pass blurs at all (needs a render target) or only tints.</summary>
    public bool Enabled => Weight > 0f;

    /// <summary>The numbers for a level: off draws nothing extra, low is a half-size copy at 40 %, medium a quarter-size copy at 60 %, high a quarter-size copy at 80 %.</summary>
    /// <param name="level">The setting.</param>
    public static UnderwaterBlurSettings For(UnderwaterBlur level) => level switch
    {
        UnderwaterBlur.Low => new UnderwaterBlurSettings(2, 0.4f, 0.5f),
        UnderwaterBlur.Medium => new UnderwaterBlurSettings(4, 0.6f, 1f),
        UnderwaterBlur.High => new UnderwaterBlurSettings(4, 0.8f, 1.5f),
        _ => new UnderwaterBlurSettings(1, 0f, 0f),
    };

    /// <summary>Reads a level from its text (<c>off</c>, <c>low</c>, <c>medium</c>, <c>high</c>, in any case), or returns <see langword="null"/> when the text is none of them.</summary>
    /// <param name="text">The text.</param>
    public static UnderwaterBlur? Parse(string? text) => text?.Trim().ToLower(CultureInfo.InvariantCulture) switch
    {
        "off" => UnderwaterBlur.Off,
        "low" => UnderwaterBlur.Low,
        "medium" => UnderwaterBlur.Medium,
        "high" => UnderwaterBlur.High,
        _ => null,
    };

    /// <summary>
    /// The opacity of each of the <see cref="Taps"/> draws of the blurred copy, in the order they are drawn over the full picture, so that after all of
    /// them each copy supplies <c>Weight / Taps</c> of the image and the picture itself the rest (<c>1 - Weight</c>). Drawing four copies each at
    /// <c>Weight / Taps</c> would let the later ones bury the earlier ones, so the opacities rise.
    /// </summary>
    public float[] TapOpacities()
    {
        var share = Weight / Taps;
        var opacities = new float[Taps];
        for (var i = 0; i < Taps; i++)
        {
            // The copies drawn after this one cover it, each leaving (1 - share) of what is under it, so this one needs the opacity
            // that leaves it exactly a share once they are over it: share / (1 - share * copies drawn after it). The test sums the weights back.
            var laterWeight = share * (Taps - 1 - i);
            opacities[i] = share / (1f - laterWeight);
        }

        return opacities;
    }
}
