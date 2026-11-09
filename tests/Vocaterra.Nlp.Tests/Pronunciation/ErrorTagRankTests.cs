using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>M87 (c): tags at the same place are ranked by the error tag set the scorer was given (a pack's), not by the Spanish list.</summary>
public class ErrorTagRankTests
{
    private static IReadOnlyList<PredictedErrorVariant> TwoTagsForOneSite(IReadOnlyList<G2pResult> variants)
    {
        var predicted = TestSupport.Predicted(variants);
        var r = predicted.First(static p => p.ErrorTag == ErrorTags.PronR);
        return [.. predicted, r with { ErrorTag = ErrorTags.PronVowel }];
    }

    private static string[] Tags(ErrorTagSet? set)
    {
        var variants = TestSupport.Primary("perro");
        var result = new GreedyPhoneScorer(errorTags: set).Score(
            TestSupport.Fake.FromPhones(TestSupport.Ipa("peɹo")), variants, TwoTagsForOneSite(variants), Difficulty.Intermediate, TestSupport.FakeHash);
        return TestSupport.Describe(result.Errors);
    }

    [Fact]
    public void TheDefaultSetIsTheV1Order()
    {
        Assert.Equal(Tags(ErrorTagSet.V1), Tags(null));
        Assert.Equal(2, Tags(null).Length);
    }

    [Fact]
    public void AReversedSetReversesTheOrderOfTagsAtOneSite()
    {
        var v1 = Tags(ErrorTagSet.V1);
        var reversed = Tags(new ErrorTagSet(ErrorTags.All.Reverse()));
        Assert.Equal(v1.Reverse(), reversed);
    }
}
