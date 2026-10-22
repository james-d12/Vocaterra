namespace Vocaterra.Game.Tests;

/// <summary>
/// The guide's phrase review (S8, S11): the guide accepts the review's request, yes and no with a reply from the bundle (<c>answer_accepted</c>),
/// through her own states, not through a host substitution.
/// </summary>
public sealed class PhraseReviewReplyTests
{
    private static readonly string[] Replies = ["¡Muy bien!", "¡Bien!", "¡Eso es!"];

    private static GuideTurn Review(VoxelSession session, string line, string prompt) =>
        session.Conversation.Say(NpcService.GuideAgent, line, new GuideScene(TestGame.ViewpointOf(session), PromptOverride: prompt), session.StoredTime, "hash");

    [Fact]
    public void TheRequestForWaterIsAcceptedWithAReplyInTheGuidesIdleState()
    {
        var session = TestGame.NewSession();

        var turn = Review(session, "quiero agua, por favor", "stall-need");

        Assert.True(turn.Communicated);
        Assert.Equal("request_item", turn.Intent);
        Assert.Contains(turn.GuideLines, line => Replies.Contains(line));
        Assert.DoesNotContain(turn.GuideLines, static line => line.StartsWith("No te entendí", StringComparison.Ordinal));
    }

    [Fact]
    public void YesAndNoAtTheOfferAreAcceptedWithAReply()
    {
        var session = TestGame.NewSession();

        var yes = Review(session, "sí, por favor", "stall-offer");
        var no = Review(session, "no, gracias", "stall-offer");

        Assert.Equal("confirm", yes.Intent);
        Assert.Equal("deny", no.Intent);
        Assert.Contains(yes.GuideLines, line => Replies.Contains(line));
        Assert.Contains(no.GuideLines, line => Replies.Contains(line));
    }
}
