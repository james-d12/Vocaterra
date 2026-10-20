using Slg.Core.Taxonomy;
using Slg.Core.Verdicts;

namespace Slg.Phonology.Tests;

/// <summary>Tests for the predicted English-speaker error variants.</summary>
public class PredictedErrorVariantTests
{
    private static IReadOnlyList<PredictedErrorVariant> Variants(string text, string tag)
    {
        return PredictedErrorVariants.Generate(TestHelpers.La(text)).Where(v => v.ErrorTag == tag).ToList();
    }

    [Fact]
    public void PerroGetsOneRVariantOverTheDigraph()
    {
        var variants = Variants("perro", ErrorTags.PronR);

        var variant = Assert.Single(variants);
        Assert.Equal(new[] { "p", "e", "ɹ", "o" }, variant.Phones);
        Assert.Equal(new TextSpan(2, 2), variant.GraphemeSpan);
        Assert.Equal(-1, variant.StressedSyllableIndex);
    }

    [Fact]
    public void EachTapOrTrillIsItsOwnSite()
    {
        var variants = Variants("carretera", ErrorTags.PronR);

        Assert.Equal(2, variants.Count);
        Assert.Equal(new TextSpan(2, 2), variants[0].GraphemeSpan);
        Assert.Equal(new TextSpan(7, 1), variants[1].GraphemeSpan);
        Assert.Equal(new[] { "k", "a", "ɹ", "e", "t", "e", "ɾ", "a" }, variants[0].Phones);
        Assert.Equal(new[] { "k", "a", "r", "e", "t", "e", "ɹ", "a" }, variants[1].Phones);
    }

    [Fact]
    public void HolaGetsASilentHVariant()
    {
        var variants = Variants("hola", ErrorTags.PronSilentH);

        var variant = Assert.Single(variants);
        Assert.Equal(new[] { "h", "o", "l", "a" }, variant.Phones);
        Assert.Equal(new TextSpan(0, 1), variant.GraphemeSpan);
    }

    [Fact]
    public void SilentHInTheMiddleOfAPhraseIsInsertedAtTheRightPhone()
    {
        var variant = Assert.Single(Variants("la hora", ErrorTags.PronSilentH));

        Assert.Equal(new[] { "l", "a", "h", "o", "ɾ", "a" }, variant.Phones);
        Assert.Equal(new TextSpan(3, 1), variant.GraphemeSpan);
    }

    [Fact]
    public void ChIsNotASilentH()
    {
        Assert.Empty(Variants("chico", ErrorTags.PronSilentH));
    }

    [Fact]
    public void CasaGetsAReducedFinalAOnly()
    {
        var variants = Variants("casa", ErrorTags.PronVowel);

        var variant = Assert.Single(variants);
        Assert.Equal(new[] { "k", "a", "s", "ə" }, variant.Phones);
        Assert.Equal(new TextSpan(3, 1), variant.GraphemeSpan);
    }

    [Fact]
    public void EAndOAreDiphthongizedAndOnlyUnstressedAIsReduced()
    {
        var variants = Variants("mesero", ErrorTags.PronVowel);

        Assert.Equal(3, variants.Count);
        Assert.Equal(new[] { "m", "eɪ", "s", "e", "ɾ", "o" }, variants[0].Phones);
        Assert.Equal(new[] { "m", "e", "s", "eɪ", "ɾ", "o" }, variants[1].Phones);
        Assert.Equal(new[] { "m", "e", "s", "e", "ɾ", "oʊ" }, variants[2].Phones);
        Assert.DoesNotContain(Variants("pan", ErrorTags.PronVowel), v => v.Phones.Contains("ə"));
    }

    [Fact]
    public void VowelsInsideDiphthongsAreLeftAlone()
    {
        Assert.DoesNotContain(Variants("aire", ErrorTags.PronVowel), v => v.GraphemeSpan.Start == 0);
        Assert.Contains(Variants("aire", ErrorTags.PronVowel), v => v.Phones.SequenceEqual(new[] { "a", "j", "ɾ", "eɪ" }));
        Assert.DoesNotContain(Variants("cuando", ErrorTags.PronVowel), v => v.GraphemeSpan.Start < 3);
    }

    [Fact]
    public void StressCanMoveToEitherNeighbouringSyllable()
    {
        var result = TestHelpers.La("teléfono");

        var variants = PredictedErrorVariants.Generate(result).Where(v => v.ErrorTag == ErrorTags.PronStress).ToList();

        Assert.Equal(2, variants.Count);
        Assert.Equal(0, variants[0].StressedSyllableIndex);
        Assert.Equal(2, variants[1].StressedSyllableIndex);
        Assert.Equal(result.Syllables[0].GraphemeSpan, variants[0].GraphemeSpan);
        Assert.Equal(result.Syllables[2].GraphemeSpan, variants[1].GraphemeSpan);
        Assert.Equal(result.Phones, variants[0].Phones);
    }

    [Fact]
    public void StressVariantsStayInsideTheirWordAndSkipMonosyllables()
    {
        var variants = PredictedErrorVariants.Generate(TestHelpers.La("la casa")).Where(v => v.ErrorTag == ErrorTags.PronStress).ToList();

        var variant = Assert.Single(variants);
        Assert.Equal(2, variant.StressedSyllableIndex);
        Assert.DoesNotContain(PredictedErrorVariants.Generate(TestHelpers.La("sol")), v => v.ErrorTag == ErrorTags.PronStress);
    }

    [Fact]
    public void OnlyPronunciationTagsFromTheTaxonomyAreUsed()
    {
        var all = PredictedErrorVariants.Generate(TestHelpers.La("la hora del perro"));

        Assert.NotEmpty(all);
        Assert.All(all, v => Assert.True(ErrorTags.IsKnown(v.ErrorTag) && ErrorTags.IsPronunciation(v.ErrorTag), v.ErrorTag));
        Assert.Contains(all, v => v.ErrorTag == ErrorTags.PronR);
        Assert.Contains(all, v => v.ErrorTag == ErrorTags.PronSilentH);
        Assert.Contains(all, v => v.ErrorTag == ErrorTags.PronVowel);
        Assert.Contains(all, v => v.ErrorTag == ErrorTags.PronStress);
    }

    [Fact]
    public void VariantsAreOrderedByPositionThenTagAndAreDistinct()
    {
        var all = PredictedErrorVariants.Generate(TestHelpers.La("hola perro"));

        var starts = all.Select(v => v.GraphemeSpan.Start).ToList();
        Assert.Equal(starts.OrderBy(static s => s), starts);
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void GenerationIsDeterministic()
    {
        var first = PredictedErrorVariants.Generate(TestHelpers.La("la hora del perro"));
        var second = PredictedErrorVariants.Generate(TestHelpers.La("la hora del perro"));

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i], second[i]);
            Assert.Equal(first[i].GetHashCode(), second[i].GetHashCode());
        }
    }

    [Fact]
    public void HandBuiltResultsWithoutExtrasStillProduceVariantsWithSyllableSpans()
    {
        var phones = new[] { "p", "e", "ɾ", "o" };
        var syllables = new[]
        {
            new SyllableAlignment(new TextSpan(0, 2), 0, 2, true),
            new SyllableAlignment(new TextSpan(2, 2), 2, 2, false),
        };
        var result = new G2pResult(phones, syllables, 0, DialectConfig.LatinAmericanNeutral);

        var variants = PredictedErrorVariants.Generate(result);

        var rVariant = Assert.Single(variants, v => v.ErrorTag == ErrorTags.PronR);
        Assert.Equal(new TextSpan(2, 2), rVariant.GraphemeSpan);
        Assert.Contains(variants, v => v.ErrorTag == ErrorTags.PronStress && v.StressedSyllableIndex == 1);
    }

    [Fact]
    public void EmptyResultHasNoVariants()
    {
        Assert.Empty(PredictedErrorVariants.Generate(TestHelpers.La(string.Empty)));
    }
}
