namespace Vocaterra.Nlp.Es.Tests.Normalization;

/// <summary>
/// Regression tests for the fuzz suite finding that <c>Accents.Strip</c> and <c>SpanishPhoneticKey.Compute</c> threw <see cref="ArgumentException"/>
/// on a lone surrogate (it reached <c>string.Normalize</c>). Every normalization entry point now reads a lone surrogate as U+FFFD.
/// </summary>
public class IllFormedUtf16Tests
{
    private const char High = (char)0xD800;
    private const char Low = (char)0xDC00;
    private const char Replacement = (char)0xFFFD;
    private const char Acute = (char)0x0301;

    // A plain array rather than theory data: xUnit names a theory case after its text, and a lone high and a lone low surrogate print alike.
    private static readonly string[] IllFormed =
    [
        $"{High}",
        $"{Low}",
        $"x{Low}y",
        $"hola{High}",
        $"{High} adiós",
        $"{Low}{High}",
        $"café{High}{Acute}",
        $"{High}{High}{Low}{Low}",
    ];

    [Fact]
    public void StripReplacesALoneSurrogateInsteadOfThrowing()
    {
        foreach (var text in IllFormed)
        {
            var stripped = Accents.Strip(text);

            Assert.Equal(Accents.Strip(Repaired(text)), stripped);
            Assert.Equal(stripped, Repaired(stripped));
        }
    }

    [Fact]
    public void PhoneticKeyTreatsALoneSurrogateAsAWordSeparator()
    {
        foreach (var text in IllFormed)
        {
            var key = SpanishPhoneticKey.Compute(text);

            Assert.Equal(SpanishPhoneticKey.Compute(Repaired(text)), key);
            Assert.All(key, static c => Assert.True(c is >= 'a' and <= 'z' or >= '0' and <= '9' or ' '));
        }
    }

    [Fact]
    public void NormalizeReadsALoneSurrogateAsTheReplacementCharacter()
    {
        foreach (var text in IllFormed)
        {
            var normalized = SpanishTextNormalizer.Normalize(text);
            var expected = SpanishTextNormalizer.Normalize(Repaired(text));

            Assert.Equal(expected.Text, normalized.Text);
            Assert.Equal(expected.Tokens, normalized.Tokens);
        }
    }

    [Fact]
    public void AValidSurrogatePairIsLeftAlone()
    {
        var emoji = $"hola {High}{Low} amigo";

        Assert.Equal(emoji, Accents.Strip(emoji));
        Assert.Equal(SpanishPhoneticKey.Compute("hola amigo"), SpanishPhoneticKey.Compute(emoji));
    }

    [Fact]
    public void AStrippedStringKeepsItsLettersAndMarksTheLoneSurrogate()
    {
        Assert.Equal($"cafe{Replacement}", Accents.Strip($"café{High}"));
        Assert.Equal($"a{Replacement}ñ", Accents.Strip($"á{Low}ñ"));
    }

    // The documented reading of an ill-formed string: every lone surrogate becomes U+FFFD, valid pairs stay.
    private static string Repaired(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsHighSurrogate(chars[i]) && i + 1 < chars.Length && char.IsLowSurrogate(chars[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(chars[i]))
            {
                chars[i] = Replacement;
            }
        }

        return new string(chars);
    }
}
