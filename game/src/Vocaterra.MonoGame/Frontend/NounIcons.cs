namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The colour of a noun's icon in the book (S22). With <c>--greyscale</c> (the playtest A/B, D-game-6) a noun's icon is grey until the word is known and
/// then in colour, as the world draws its block; with the switch off every icon is in colour. The book had drawn every icon in colour in both arms.
/// </summary>
public static class NounIcons
{
    /// <summary>The saturation the icon is drawn with: 0 for the grey copy, 1 for full colour.</summary>
    /// <param name="greyscale">Whether <c>--greyscale</c> is on.</param>
    /// <param name="known">Whether the noun is known.</param>
    public static double Saturation(bool greyscale, bool known) => greyscale && !known ? 0.0 : 1.0;
}
