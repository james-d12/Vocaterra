using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Language.Matching;

namespace Vocaterra.Nlp.De.Tests.Normalization;

/// <summary>Tests for <see cref="GermanPhoneticKey"/>: sound-alike spellings share a key, different sounds do not, and the key is a-z, 0-9 and space.</summary>
public class GermanPhoneticKeyTests
{
    [Theory]
    [InlineData("Vater", "Fater")]
    [InlineData("Straße", "Schtrasse")]
    [InlineData("Straße", "Strasse")]
    [InlineData("Fuß", "Fuss")]
    [InlineData("schön", "schoen")]
    [InlineData("müde", "muede")]
    [InlineData("Mädchen", "Medchen")]
    [InlineData("Käse", "Kese")]
    [InlineData("Hallo", "HALLO")]
    [InlineData("sehen", "sen")]
    [InlineData("Bahn", "Ban")]
    [InlineData("Haar", "Har")]
    [InlineData("See", "Se")]
    [InlineData("Fisch", "Fiesch")]
    [InlineData("Phase", "Fase")]
    [InlineData("Quelle", "Kwelle")]
    [InlineData("Hund", "Hunt")]
    [InlineData("Tag", "Tak")]
    [InlineData("Lieb", "Lip")]
    [InlineData("Katze", "Kaze")]
    [InlineData("Katze", "Katse")]
    [InlineData("Zeit", "Tseit")]
    [InlineData("Eis", "Ays")]
    [InlineData("Haus", "Hauss")]
    [InlineData("Euro", "Oyro")]
    [InlineData("Häuser", "Hoyser")]
    [InlineData("Stein", "Schtein")]
    [InlineData("sprechen", "schprechen")]
    [InlineData("Dank", "Danck")]
    public void SoundAlikeSpellingsShareAKey(string a, string b)
    {
        Assert.Equal(GermanPhoneticKey.Key(a), GermanPhoneticKey.Key(b));
    }

    [Theory]
    [InlineData("schön", "schon")]
    [InlineData("Bär", "Bar")]
    [InlineData("müde", "mude")]
    [InlineData("Hund", "Haus")]
    [InlineData("Katze", "Kaffee")]
    [InlineData("Mann", "Mohn")]
    [InlineData("Ofen", "Öfen")]
    [InlineData("zwei", "drei")]
    [InlineData("Tisch", "Tasche")]
    [InlineData("kann", "kam")]
    public void DifferentSoundsGetDifferentKeys(string a, string b)
    {
        Assert.NotEqual(GermanPhoneticKey.Key(a), GermanPhoneticKey.Key(b));
    }

    [Theory]
    [InlineData("Hallo", "halo")]
    [InlineData("Haus", "haus")]
    [InlineData("Straße", "ctrase")]
    [InlineData("Tisch", "tic")]
    [InlineData("Fisch", "fic")]
    [InlineData("Mädchen", "medxen")]
    [InlineData("22 Euro", "22 oyro")]
    [InlineData("11", "11")]
    [InlineData("Hallo, Welt!", "halo velt")]
    [InlineData("", "")]
    [InlineData("?!", "")]
    public void ComputesTheDocumentedKey(string word, string expected)
    {
        Assert.Equal(expected, GermanPhoneticKey.Key(word));
    }

    [Theory]
    [InlineData("Straße")]
    [InlineData("Österreich")]
    [InlineData("Übung")]
    [InlineData("Grüß Gott")]
    [InlineData("café naïve")]
    [InlineData("a\uD800b")]
    [InlineData("\uDC00")]
    [InlineData("日本語")]
    [InlineData("Hallo Welt 123")]
    public void TheKeyIsLowerCaseAsciiDigitsAndSpace(string text)
    {
        var key = GermanPhoneticKey.Key(text);

        Assert.All(key, static c => Assert.True(c is >= 'a' and <= 'z' or >= '0' and <= '9' or ' ', $"'{c}'"));
    }

    [Fact]
    public void ThePackExposesTheKey()
    {
        Assert.Equal(GermanPhoneticKey.Key("Straße"), GermanLanguagePack.Instance.PhoneticKey.Compute("Straße"));
    }

    [Fact]
    public void NullIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => GermanPhoneticKey.Key(null!));
    }

    [Fact]
    public void ASoundAlikeSpellingIsAFuzzyMatchOfDistanceZeroThroughTheEngineMatcher()
    {
        var set = new AnswerSet("a", [new Answer("a1", "Vater", [], [])]);
        var pack = GermanLanguagePack.Instance;

        var result = AnswerMatcher.Match(pack.Normalizer.Normalize("Fater"), set, Difficulty.Intermediate, pack);

        Assert.Equal("a1", result.AnswerId);
        Assert.Equal(0, result.Distance);
        Assert.True(result.LikelyPronunciation);
    }
}
