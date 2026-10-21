using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The committed font must be able to draw Spanish, and must be recorded as CC0 (D-game-5).</summary>
public sealed class FontTests
{
    private const string SpanishLetters = "áéíóúüñÁÉÍÓÚÜÑ¿¡";

    private static string CommittedFont => Repo.Path("assets", "fonts", "pixel-operator", "PixelOperator.ttf");

    [Fact]
    public void TheCommittedFontCoversEverySpanishLetter()
    {
        var cmap = TrueTypeCmap.Load(CommittedFont);
        var missing = SpanishLetters.Where(c => !cmap.Covers(c)).Select(c => $"U+{(int)c:X4} {c}").ToList();
        Assert.True(missing.Count == 0, "The font has no glyph for: " + string.Join(", ", missing));
    }

    [Fact]
    public void TheFontCoversPlainLettersDigitsAndPunctuation()
    {
        var cmap = TrueTypeCmap.Load(CommittedFont);
        foreach (var c in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,:;?!()-'\"/")
        {
            Assert.True(cmap.Covers(c), $"The font has no glyph for '{c}'.");
        }
    }

    [Fact]
    public void TheDebugOverlaysSpanishTestLineIsCoveredByTheFont()
    {
        var cmap = TrueTypeCmap.Load(CommittedFont);
        Assert.All(DebugText.SpanishTestLine, c => Assert.True(cmap.Covers(c), $"No glyph for '{c}' in the overlay's test line."));
    }

    [Fact]
    public void TheCmapReaderReportsAMissingGlyph()
    {
        var cmap = TrueTypeCmap.Load(CommittedFont);
        Assert.False(cmap.Covers(0x4E2D), "A CJK ideograph should not be in a 16 px Latin pixel font.");
    }

    [Fact]
    public void TheFontIsCopiedNextToTheExecutable()
    {
        Assert.True(File.Exists(AssetPaths.Font), $"The project file should copy the font to {AssetPaths.Font}.");
        Assert.Equal(File.ReadAllBytes(CommittedFont), File.ReadAllBytes(AssetPaths.Font));
    }

    [Fact]
    public void TheFontAndItsLicenceAreRecordedAsCc0()
    {
        var licence = File.ReadAllText(Repo.Path("assets", "fonts", "pixel-operator", "LICENSE.txt"));
        Assert.Contains("CC0 1.0 Universal", licence, StringComparison.Ordinal);

        var attribution = File.ReadAllText(Repo.Path("assets", "ATTRIBUTION.md"));
        Assert.Contains("PixelOperator.ttf", attribution, StringComparison.Ordinal);
        Assert.Contains("CC0", attribution, StringComparison.Ordinal);
        Assert.Contains("https://", attribution, StringComparison.Ordinal);
    }
}
