namespace Vocaterra.Nlp.De.Tests.Text;

/// <summary>Tests for <see cref="GermanPunctuation"/>, <see cref="GermanRepairLines"/> and <see cref="GermanOrthographyNotes"/>.</summary>
public class GermanPunctuationTests
{
    [Theory]
    [InlineData("Hallo  Welt", "Hallo Welt")]
    [InlineData("Hallo , Welt !", "Hallo, Welt!")]
    [InlineData("Wie geht es dir ?", "Wie geht es dir?")]
    [InlineData("„ Hallo “", "„Hallo“")]
    [InlineData("» Hallo «", "»Hallo«")]
    [InlineData("Ja ; nein : vielleicht …", "Ja; nein: vielleicht…")]
    [InlineData("  Hallo  ", "Hallo")]
    [InlineData("Er sagte „ Ja “ .", "Er sagte „Ja“.")]
    [InlineData("", "")]
    [InlineData("Wie geht es dir?", "Wie geht es dir?")]
    [InlineData("Guten Tag!\n\nWie geht's?", "Guten Tag! Wie geht's?")]
    public void FixesTheSpacing(string text, string expected)
    {
        Assert.Equal(expected, GermanPunctuation.Instance.Fix(text));
    }

    [Fact]
    public void AddsNoInvertedMarks()
    {
        Assert.Equal("Wie geht es dir?", GermanPunctuation.Instance.Fix("Wie geht es dir?"));
        Assert.DoesNotContain('¿', GermanPunctuation.Instance.Fix("Was?"));
    }

    [Fact]
    public void OfferFormatQuotesTheChoicesTheGermanWay()
    {
        var line = GermanRepairLines.OfferFormat.Format(["Haus", "Hund", "Hand"]);

        Assert.Equal("Meinst du „Haus“, „Hund“ oder „Hand“?", line);
    }

    [Fact]
    public void RepairLinesAreTheFourGermanLines()
    {
        var lines = GermanLanguagePack.Instance.RepairFallback;

        Assert.Equal("Wie bitte?", lines.AskAgain);
        Assert.False(string.IsNullOrWhiteSpace(lines.SlowRepeat));
        Assert.False(string.IsNullOrWhiteSpace(lines.GiveUp));
        Assert.NotNull(lines.Offer);
    }

    [Fact]
    public void AnOrthographyNoteNamesTheAccentedForm()
    {
        var note = new AccentNote(new TextSpan(0, 3), "fur", "für", AccentMark.Person, new LexicalFeatures("für", "prep", null, null, null, null, null));

        Assert.Contains("für", GermanLanguagePack.Instance.Orthography.Describe(note), StringComparison.Ordinal);
    }
}
