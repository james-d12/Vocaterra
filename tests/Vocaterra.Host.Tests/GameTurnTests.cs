using Vocaterra.Game.Core.Items;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Host.Tests;

/// <summary>The host's replies are the game's own turn (V21b, item 8): the same dropped offer, the same one reply per turn, the same accent note.</summary>
public sealed class GameTurnTests
{
    private static readonly StoredTimestamp Now = new(1_760_000_000);

    private static string Agent(string line, string prompt, Inventory? bag = null)
    {
        var session = new ConversationSession(GameContent.Bundle, bag: bag);
        var result = session.Evaluate(line, prompt, Now);
        using var writer = new StringWriter();
        VerdictReport.Write(writer, prompt, line, result, session.LastExchange);
        return writer.ToString();
    }

    private static List<string> AgentLines(string report) =>
        [.. report.Split('\n').Select(static l => l.TrimEnd('\r')).Where(static l => l.StartsWith("  agent: ", StringComparison.Ordinal)).Select(static l => l[9..])];

    [Fact]
    public void AMissedGiftDropsTheOfferOfBreadWithAnEmptyBag()
    {
        var bag = new Inventory();

        var lines = AgentLines(Agent("quiero madera", "stall-need", bag));

        Assert.Equal(["No tengo eso. Tengo agua y pan."], lines);
    }

    [Fact]
    public void AMissedGiftIsRefusedWithoutABagToo()
    {
        Assert.Equal(["No tengo eso. Tengo agua y pan."], AgentLines(Agent("quiero madera", "stall-need")));
    }

    [Fact]
    public void ARequestForBreadAtTheStallGivesTheBreadLine()
    {
        var lines = AgentLines(Agent("quiero pan", "stall-need"));

        Assert.True(Assert.Single(lines) is "Aquí tienes el pan." or "Toma el pan.");
    }

    [Fact]
    public void ARecastIsOneReplyNotTheRecastAndTheAcknowledgement()
    {
        var lines = AgentLines(Agent("compré three manzanas", "farmer-open"));

        Assert.Single(lines);
        Assert.DoesNotContain("¡Qué bien!", lines);
    }

    [Fact]
    public void ALineTheNpcDoesNotTakeGetsOnlyTheRetry()
    {
        var lines = AgentLines(Agent("buscaré madeda", "lesson-will-fetch"));

        Assert.All(lines, static l => Assert.DoesNotContain("Muy bien", l, StringComparison.Ordinal));
        Assert.Contains(lines, static l => l.StartsWith("No te entendí", StringComparison.Ordinal));
    }

    [Fact]
    public void AVerbWithoutItsAccentIsAcceptedWithTheCoresNote()
    {
        var report = Agent("ayer compre pan", "lesson-bought");

        Assert.Contains("  note: «compré», con tilde: es pasado", report, StringComparison.Ordinal);
    }
}
