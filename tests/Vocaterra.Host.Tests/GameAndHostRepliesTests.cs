using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Host.Tests;

/// <summary>
/// One reply, two paths (village-plan V29, item 6): the host's <c>ConversationSession</c> and the game's <c>NpcService</c> share the turn rules (V30), and these lines prove they say the
/// same: a line the NPC does not understand, an off-prompt line, and the weather and «¿cómo estás?» replies.
/// </summary>
public sealed class GameAndHostRepliesTests
{
    private const ulong Seed = 42;
    private static readonly StoredTimestamp Now = new(1_760_000_000);

    [Theory]
    [InlineData("smith", "smith-open", "xyzzy qwerty")]
    [InlineData("farmer", "farmer-open", "la tienda está en el pueblo")]
    [InlineData("smith", "smith-open", "me gusta el río")]
    [InlineData("fisher", "fisher-open", "tengo dos manzanas")]
    [InlineData("baker", "baker-open", "hace frío")]
    [InlineData("baker", "baker-open", "¿qué tiempo hace?")]
    [InlineData("villager", "villager-open", "llueve")]
    [InlineData("fisher", "fisher-open", "¿cómo estás?")]
    [InlineData("smith", "smith-open", "hola")]
    public void TheHostAndTheGameSayTheSameReply(string npc, string prompt, string line)
    {
        var hostSession = new ConversationSession(GameContent.Bundle, Seed);
        _ = hostSession.Evaluate(line, prompt, Now);
        var host = hostSession.LastExchange!;

        var game = new NpcService(GameContent.Data, new NoFacts(), Seed, new StoredTimestamp(1_760_000_000))
            .Say(npc, line, new GuideScene(new GuideCell(0, 0, 0)), Now, "hash");

        Assert.Equal(game.Replies, host.Replies);
        Assert.Equal(game.Feedback, host.Feedback);
        Assert.Equal(game.OffPromptIntent, host.OffPromptIntent);
    }

    [Theory]
    [InlineData("hace frío", "smith-open", "Sí, hace frío.")]
    [InlineData("llueve", "farmer-open", "¡Sí!")]
    [InlineData("¿qué tiempo hace?", "baker-open", "Hoy hace sol.")]
    [InlineData("¿cómo estás?", "fisher-open", "Bien, gracias.")]
    public void TheHostPrintsTheNewRepliesAsItsAgentLine(string line, string prompt, string reply)
    {
        var session = new ConversationSession(GameContent.Bundle);
        var result = session.Evaluate(line, prompt, Now);
        using var writer = new StringWriter();
        VerdictReport.Write(writer, prompt, line, result, GameContent.Bundle.Language.Orthography, session.LastExchange);

        Assert.Contains("  agent: " + reply, writer.ToString().Split('\n').Select(static l => l.TrimEnd('\r')));
    }

    private sealed class NoFacts : IGuideFacts
    {
        public GuideNearest? NearestOfNoun(GuideViewpoint from, string lemma) => null;

        public GuideNearest? NearestOfNounJoinedTo(GuideViewpoint from, string lemma, GuideCell body) => null;

        public IReadOnlyList<string> NounsAt(GuideCell cell) => [];
    }
}
