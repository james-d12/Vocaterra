using Vocaterra.Nlp.Nlp.Matching;

namespace Vocaterra.Nlp.Nlp.Tests.Matching;

/// <summary>Tests for <see cref="DamerauLevenshtein"/>.</summary>
public class DamerauLevenshteinTests
{
    [Theory]
    [InlineData("", "", 0)]
    [InlineData("abc", "abc", 0)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "", 3)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("espado", "espada", 1)]
    [InlineData("espda", "espada", 1)]
    [InlineData("ab", "ba", 1)]
    [InlineData("casa", "caas", 1)]
    [InlineData("ca", "abc", 3)]
    [InlineData("A", "a", 1)]
    [InlineData("xyzzy", "espada", 6)]
    public void ComputesOptimalStringAlignmentDistance(string a, string b, int expected)
    {
        Assert.Equal(expected, DamerauLevenshtein.Distance(a, b));
        Assert.Equal(expected, DamerauLevenshtein.Distance(b, a));
    }

    [Fact]
    public void ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => DamerauLevenshtein.Distance(null!, "a"));
        Assert.Throws<ArgumentNullException>(() => DamerauLevenshtein.Distance("a", null!));
    }
}
