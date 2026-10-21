using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Response.Tests.Templates;

public class ReservedPoolsTests
{
    [Theory]
    [InlineData("not_understood", true)]
    [InlineData("answer_accepted", true)]
    [InlineData("feedback.recast", true)]
    [InlineData("feedback.bogus", true)]
    [InlineData("greet_reply", false)]
    [InlineData("feedback", false)]
    [InlineData("my_feedback.recast", false)]
    public void ReservedPoolsAreTheOnesThePipelineReachesByItself(string poolId, bool reserved)
    {
        Assert.Equal(reserved, ReservedPools.IsReserved(poolId));
    }

    [Theory]
    [InlineData("feedback.recast", true)]
    [InlineData("feedback.explicit_correction", true)]
    [InlineData("feedback.hint.gender.agree", true)]
    [InlineData("feedback.recast.conj.person", true)]
    [InlineData("feedback.slow_repeat", true)]
    [InlineData("feedback.model_pronunciation", true)]
    [InlineData("feedback.bogus", false)]
    [InlineData("feedback.bogus.tag", false)]
    [InlineData("feedback.hint.", false)]
    [InlineData("feedback.", false)]
    [InlineData("hint.recast", false)]
    public void FeedbackPoolsNameAKindAndOptionallyATag(string poolId, bool wellFormed)
    {
        Assert.Equal(wellFormed, ReservedPools.IsFeedbackPool(poolId));
    }

    [Fact]
    public void TheKindListMatchesTheFeedbackKindsAndThePlannerConstants()
    {
        Assert.Equal(
            [FeedbackKinds.ExplicitCorrection, FeedbackKinds.Hint, FeedbackKinds.ModelPronunciation, FeedbackKinds.Recast, FeedbackKinds.SlowRepeat],
            ReservedPools.FeedbackKindNames);
        Assert.Equal(ResponsePlanner.NotUnderstoodIntent, ReservedPools.NotUnderstood);
    }
}
