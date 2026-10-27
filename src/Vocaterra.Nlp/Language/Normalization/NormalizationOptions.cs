namespace Vocaterra.Nlp.Language.Normalization;

/// <summary>Switches for a language's <see cref="Packs.ITextNormalizer"/> (the neutral <see cref="TextTokenizer"/> reads only <c>CaptureFinalMark</c>); the defaults match the plan (numbers to digits, no contraction expansion).</summary>
/// <param name="ExpandContractions">Expand <c>al</c> to <c>a el</c> and <c>del</c> to <c>de el</c>.</param>
/// <param name="NumbersToDigits">Replace the language's number words (Spanish: 0 to 999 999) with digits.</param>
/// <param name="ConvertIndefiniteArticles">
/// Also convert a standalone <c>un</c> or <c>una</c> to <c>1</c>. When false they stay articles; <c>un</c> and <c>una</c> inside
/// a longer number such as <c>treinta y un</c> are always converted, and a standalone <c>uno</c> is always converted.
/// </param>
/// <param name="CaptureFinalMark">
/// Record the final <c>?</c> or <c>!</c> of the text in <c>NormalizedText.FinalMark</c> (M34, additive). Off by default: with it off the result is exactly
/// what it was before M34.
/// </param>
public sealed record NormalizationOptions(
    bool ExpandContractions = false,
    bool NumbersToDigits = true,
    bool ConvertIndefiniteArticles = true,
    bool CaptureFinalMark = false)
{
    /// <summary>The default options.</summary>
    public static NormalizationOptions Default { get; } = new();
}
