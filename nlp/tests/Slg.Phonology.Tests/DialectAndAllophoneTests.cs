namespace Slg.Phonology.Tests;

/// <summary>Tests for dialect switches and the cross-word allophone rules.</summary>
public class DialectAndAllophoneTests
{
    [Fact]
    public void PresetsMatchTheirDefinitions()
    {
        Assert.Equal(new DialectConfig(true, true, LlSound.Fricative, false), DialectConfig.LatinAmericanNeutral);
        Assert.Equal(new DialectConfig(false, true, LlSound.Fricative, false), DialectConfig.CastilianStandard);
        Assert.True(DialectConfig.Caribbean.SAspiration);
        Assert.False(DialectConfig.AndeanLleista.Yeismo);
        Assert.Equal(LlSound.Sh, DialectConfig.Rioplatense.LlSound);
    }

    [Fact]
    public void AllShippedListsEveryPresetWithTheDefaultFirst()
    {
        Assert.Equal(DialectConfig.LatinAmericanNeutral, DialectConfig.AllShipped[0]);
        Assert.Equal(DialectConfig.CastilianStandard, DialectConfig.AllShipped[1]);
        Assert.Equal(DialectConfig.AllShipped.Count, DialectConfig.AllShipped.Distinct().Count());
        Assert.Contains(DialectConfig.Rioplatense, DialectConfig.AllShipped);
        Assert.Contains(DialectConfig.Caribbean, DialectConfig.AllShipped);
        Assert.Contains(DialectConfig.AndeanLleista, DialectConfig.AllShipped);
    }

    [Fact]
    public void YeismoMergesLlWithYAndLleismoKeepsTheLateral()
    {
        Assert.Equal(new[] { "ʝ", "a", "β", "e" }, TestHelpers.La("llave").Phones);
        Assert.Equal(new[] { "ʝ", "a", "β", "e" }, TestHelpers.Es("llave").Phones);
        Assert.Equal(new[] { "ʎ", "a", "β", "e" }, SpanishG2p.Phonemize("llave", DialectConfig.AndeanLleista).Phones);
        Assert.Equal(new[] { "ʃ", "a", "β", "e" }, SpanishG2p.Phonemize("llave", DialectConfig.Rioplatense).Phones);
        Assert.Equal(new[] { "ʃ", "o" }, SpanishG2p.Phonemize("yo", DialectConfig.Rioplatense).Phones);
        Assert.Equal(new[] { "ʝ", "o" }, SpanishG2p.Phonemize("yo", DialectConfig.AndeanLleista).Phones);
        Assert.Equal(new[] { "ʒ", "o" }, SpanishG2p.Phonemize("yo", new DialectConfig(true, true, LlSound.Zh, false)).Phones);
    }

    [Fact]
    public void LlSoundIsIgnoredWithoutYeismo()
    {
        var dialect = new DialectConfig(true, false, LlSound.Sh, false);

        Assert.Equal(new[] { "ʎ", "o" }, SpanishG2p.Phonemize("llo", dialect).Phones);
        Assert.Equal(new[] { "ʝ", "o" }, SpanishG2p.Phonemize("yo", dialect).Phones);
    }

    [Fact]
    public void SpirantizationAfterAVowelInsideAndAcrossWords()
    {
        var result = TestHelpers.La("la bodega");

        Assert.Equal(new[] { "l", "a", "β", "o", "ð", "e", "ɣ", "a" }, result.Phones);
        Assert.Equal("ˈla βo.ˈðe.ɣa", result.ToIpaString());
    }

    [Fact]
    public void UtteranceInitialBDGAreStops()
    {
        Assert.Equal(new[] { "b", "o", "ð", "e", "ɣ", "a" }, TestHelpers.La("bodega").Phones);
        Assert.Equal(new[] { "d", "a", "ð", "o" }, TestHelpers.La("dado").Phones);
        Assert.Equal(new[] { "g", "a", "t", "o" }, TestHelpers.La("gato").Phones);
    }

    [Fact]
    public void NasalAssimilationBeforeBilabialsAndVelars()
    {
        Assert.Equal(new[] { "u", "m", "b", "e", "s", "o" }, TestHelpers.La("un beso").Phones);
        Assert.Equal("ˈum ˈbe.so", TestHelpers.La("un beso").ToIpaString());
        Assert.Equal(new[] { "u", "m", "p", "o", "k", "o" }, TestHelpers.La("un poco").Phones);
        Assert.Equal(new[] { "u", "m", "m", "e", "s" }, TestHelpers.La("un mes").Phones);
        Assert.Equal(new[] { "u", "ŋ", "k", "i", "l", "o" }, TestHelpers.La("un kilo").Phones);
        Assert.Equal(new[] { "k", "o", "ŋ", "g", "u", "s", "t", "o" }, TestHelpers.La("con gusto").Phones);
        Assert.Equal(new[] { "e", "ŋ", "x", "a", "p", "o", "n" }, TestHelpers.La("en Japón").Phones);
        Assert.Equal(new[] { "u", "n", "t", "o", "ɾ", "o" }, TestHelpers.La("un toro").Phones);
    }

    [Fact]
    public void StopsAfterNasalsAndDAfterLButApproximantsOtherwise()
    {
        Assert.Equal(new[] { "k", "w", "a", "n", "d", "o" }, TestHelpers.La("cuando").Phones);
        Assert.Equal(new[] { "e", "l", "d", "e", "ð", "o" }, TestHelpers.La("el dedo").Phones);
        Assert.Equal(new[] { "e", "l", "ɣ", "a", "t", "o" }, TestHelpers.La("el gato").Phones);
        Assert.Equal(new[] { "e", "l", "β", "a", "s", "o" }, TestHelpers.La("el vaso").Phones);
        Assert.Equal(new[] { "a", "l", "d", "e", "a" }, TestHelpers.La("aldea").Phones);
        Assert.Equal(new[] { "a", "m", "b", "o", "s" }, TestHelpers.La("ambos").Phones);
    }

    [Fact]
    public void SVoicesBeforeVoicedConsonantsOnly()
    {
        Assert.Equal(new[] { "m", "i", "z", "m", "o" }, TestHelpers.La("mismo").Phones);
        Assert.Equal(new[] { "l", "o", "z", "ð", "e", "ð", "o", "s" }, TestHelpers.La("los dedos").Phones);
        Assert.Equal(new[] { "l", "o", "s", "p", "e", "r", "o", "s" }, TestHelpers.La("los perros").Phones);
        Assert.Equal(new[] { "l", "o", "s", "a", "m", "i", "ɣ", "o", "s" }, TestHelpers.La("los amigos").Phones);
        Assert.Equal(new[] { "i", "z", "l", "a" }, TestHelpers.La("isla").Phones);
    }

    [Fact]
    public void SeseoSVoicesBeforeVoicedConsonantsToo()
    {
        Assert.Equal(new[] { "x", "u", "z", "ɣ", "a", "ɾ" }, TestHelpers.La("juzgar").Phones);
        Assert.Equal(new[] { "x", "u", "θ", "ɣ", "a", "ɾ" }, TestHelpers.Es("juzgar").Phones);
    }

    [Fact]
    public void SAspirationAppliesOnlyInSyllableFinalPosition()
    {
        var caribbean = DialectConfig.Caribbean;

        Assert.Equal("eh.ˈpa.ða", SpanishG2p.Phonemize("espada", caribbean).ToIpaString());
        Assert.Equal("ˈloh ˈpe.ɾoh", SpanishG2p.Phonemize("los peros", caribbean).ToIpaString());
        Assert.Equal("ˈsa.po", SpanishG2p.Phonemize("sapo", caribbean).ToIpaString());
        Assert.Equal("ˈmih.mo", SpanishG2p.Phonemize("mismo", caribbean).ToIpaString());
        Assert.Equal("ˈmiz.mo", TestHelpers.La("mismo").ToIpaString());
    }

    [Fact]
    public void PhonemizeAllReturnsDistinctVariantsPrimaryFirst()
    {
        var all = SpanishG2p.PhonemizeAll("cerveza", DialectConfig.AllShipped);

        Assert.Equal(2, all.Count);
        Assert.Equal(DialectConfig.LatinAmericanNeutral, all[0].Dialect);
        Assert.Equal(DialectConfig.CastilianStandard, all[1].Dialect);
        Assert.Equal("seɾ.ˈβe.sa", all[0].ToIpaString());
        Assert.Equal("θeɾ.ˈβe.θa", all[1].ToIpaString());
    }

    [Fact]
    public void PhonemizeAllKeepsVariantsThatDifferInRioplatenseCaribbeanAndLleistaSpeech()
    {
        var all = SpanishG2p.PhonemizeAll("las llaves");

        Assert.Equal(4, all.Count);
        Assert.Equal("ˈlaz ˈʝa.βes", all[0].ToIpaString());
        Assert.Equal(DialectConfig.LatinAmericanNeutral, all[0].Dialect);
        Assert.Contains(all, r => r.ToIpaString() == "ˈlas ˈʃa.βes");
        Assert.Contains(all, r => r.ToIpaString() == "ˈlah ˈʝa.βeh");
        Assert.Contains(all, r => r.ToIpaString() == "ˈlaz ˈʎa.βes");
    }

    [Fact]
    public void PhonemizeAllDeduplicatesIdenticalRealizations()
    {
        var all = SpanishG2p.PhonemizeAll("hola", [DialectConfig.LatinAmericanNeutral, DialectConfig.CastilianStandard, DialectConfig.LatinAmericanNeutral]);

        Assert.Single(all);
        Assert.Equal(DialectConfig.LatinAmericanNeutral, all[0].Dialect);
    }
}
