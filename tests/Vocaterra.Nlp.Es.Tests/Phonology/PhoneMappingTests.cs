namespace Vocaterra.Nlp.Es.Tests.Phonology;

/// <summary>Tests for the context-sensitive grapheme to phone rules, one family at a time.</summary>
public class PhoneMappingTests
{
    [Theory]
    [InlineData("casa", "kasa")]
    [InlineData("coco", "koko")]
    [InlineData("cuna", "kuna")]
    [InlineData("cielo", "sjelo")]
    [InlineData("cena", "sena")]
    [InlineData("zapato", "sapato")]
    [InlineData("azul", "asul")]
    public void CAndZUnderSeseo(string word, string expected)
    {
        Assert.Equal(Tokens(expected), TestHelpers.La(word).Phones);
    }

    [Theory]
    [InlineData("cielo", "θjelo")]
    [InlineData("cena", "θena")]
    [InlineData("zapato", "θapato")]
    [InlineData("casa", "kasa")]
    [InlineData("acción", "akθjon")]
    public void CAndZUnderDistincion(string word, string expected)
    {
        Assert.Equal(Tokens(expected), TestHelpers.Es(word).Phones);
    }

    [Fact]
    public void CervezaDiffersBetweenSeseoAndDistincion()
    {
        Assert.Equal(new[] { "s", "e", "ɾ", "β", "e", "s", "a" }, TestHelpers.La("cerveza").Phones);
        Assert.Equal(new[] { "θ", "e", "ɾ", "β", "e", "θ", "a" }, TestHelpers.Es("cerveza").Phones);
        Assert.Equal("seɾ.ˈβe.sa", TestHelpers.La("cerveza").ToIpaString());
        Assert.Equal("θeɾ.ˈβe.θa", TestHelpers.Es("cerveza").ToIpaString());
    }

    [Theory]
    [InlineData("gato", "gato")]
    [InlineData("gente", "xente")]
    [InlineData("gigante", "xiɣante")]
    [InlineData("guerra", "gera")]
    [InlineData("guitarra", "gitara")]
    [InlineData("pingüino", "piŋgwino")]
    [InlineData("jota", "xota")]
    [InlineData("reloj", "relox")]
    [InlineData("guapo", "gwapo")]
    public void GJAndGuRules(string word, string expected)
    {
        Assert.Equal(Tokens(expected), TestHelpers.La(word).Phones);
    }

    [Theory]
    [InlineData("queso", "keso")]
    [InlineData("quiero", "kjeɾo")]
    [InlineData("kilo", "kilo")]
    [InlineData("chico", "tʃiko")]
    [InlineData("ñandú", "ɲandu")]
    [InlineData("año", "aɲo")]
    [InlineData("hola", "ola")]
    [InlineData("vaca", "baka")]
    [InlineData("whisky", "wiski")]
    [InlineData("wolfram", "wolfɾam")]
    public void QuKChNHVAndW(string word, string expected)
    {
        Assert.Equal(Tokens(expected), TestHelpers.La(word).Phones);
    }

    [Theory]
    [InlineData("rojo", "roxo")]
    [InlineData("perro", "pero")]
    [InlineData("pero", "peɾo")]
    [InlineData("honra", "onra")]
    [InlineData("alrededor", "alreðeðoɾ")]
    [InlineData("Israel", "izrael")]
    [InlineData("tres", "tɾes")]
    [InlineData("comer", "komeɾ")]
    [InlineData("enredo", "enreðo")]
    public void TapAndTrill(string word, string expected)
    {
        Assert.Equal(Tokens(expected), TestHelpers.La(word).Phones);
    }

    [Theory]
    [InlineData("llave", "ʝaβe")]
    [InlineData("yo", "ʝo")]
    [InlineData("mayo", "maʝo")]
    [InlineData("y", "i")]
    [InlineData("hoy", "oj")]
    [InlineData("muy", "muj")]
    [InlineData("buey", "bwej")]
    [InlineData("ley", "lej")]
    [InlineData("estoy", "estoj")]
    [InlineData("inyección", "inʝeksjon")]
    public void YAndLlUnderYeismo(string word, string expected)
    {
        Assert.Equal(Tokens(expected), TestHelpers.La(word).Phones);
    }

    [Theory]
    [InlineData("examen", "eksamen")]
    [InlineData("taxi", "taksi")]
    [InlineData("texto", "teksto")]
    [InlineData("xilófono", "silofono")]
    [InlineData("extra", "ekstɾa")]
    public void XIsKsExceptWordInitially(string word, string expected)
    {
        Assert.Equal(Tokens(expected), TestHelpers.La(word).Phones);
    }

    [Fact]
    public void IntervocalicXSplitsItsPhonesAcrossTheSyllableBoundary()
    {
        var result = TestHelpers.La("examen");

        Assert.Equal("ek.ˈsa.men", result.ToIpaString());
        Assert.Equal(new[] { 2, 2, 3 }, result.Syllables.Select(s => s.PhoneCount).ToArray());
        Assert.Equal(new[] { 0, 2, 4 }, result.Syllables.Select(s => s.PhoneStart).ToArray());
        Assert.Equal(new Vocaterra.Nlp.Core.Verdicts.TextSpan(1, 1), result.PhoneSpans[1]);
        Assert.Equal(result.PhoneSpans[1], result.PhoneSpans[2]);
    }

    [Theory]
    [InlineData("hielo", "jelo")]
    [InlineData("huevo", "weβo")]
    [InlineData("cuatro", "kwatɾo")]
    [InlineData("aire", "ajɾe")]
    [InlineData("causa", "kawsa")]
    [InlineData("ciudad", "sjuðað")]
    [InlineData("ruido", "rwiðo")]
    [InlineData("Paraguay", "paɾaɣwaj")]
    [InlineData("país", "pais")]
    [InlineData("leer", "leeɾ")]
    [InlineData("día", "dia")]
    public void GlidesAndHiatus(string word, string expected)
    {
        Assert.Equal(Tokens(expected), TestHelpers.La(word).Phones);
    }

    [Fact]
    public void ResyllabificationAcrossSilentHFollowsSpeech()
    {
        Assert.Equal("i.nu.ˈma.no", TestHelpers.La("inhumano").ToIpaString());
        Assert.Equal("ek.si.ˈβiɾ", TestHelpers.La("exhibir").ToIpaString());
        Assert.Equal("a.ˈo.ɾa", TestHelpers.La("ahora").ToIpaString());

        var inhumano = TestHelpers.La("inhumano");
        Assert.Equal(new[] { "in", "hu", "ma", "no" }, TestHelpers.SyllableSpellings(inhumano));
        Assert.Equal(new[] { 1, 2, 2, 2 }, inhumano.Syllables.Select(s => s.PhoneCount).ToArray());
    }

    private static string[] Tokens(string ipa)
    {
        return TestHelpers.Tokens(ipa);
    }
}
