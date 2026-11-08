using Vocaterra.Game.Core.Items;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Host.Tests;

/// <summary>The host's <c>--bag</c> (V23a): a sale or a purchase judged against the bag the run was given, with the NPC's own answer to a miss; without a bag, the host as before.</summary>
public sealed class BagTests
{
    private static readonly StoredTimestamp Now = new(1_760_000_000);

    private static string Said(string line, string prompt, Inventory? bag, out ExchangeAnswer? exchange)
    {
        var session = new ConversationSession(GameContent.Bundle, bag: bag);
        var result = session.Evaluate(line, prompt, Now);
        exchange = session.LastExchange;
        using var writer = new StringWriter();
        VerdictReport.Write(writer, prompt, line, result, GameContent.Bundle.Language.Orthography, exchange);
        return writer.ToString();
    }

    private static Inventory Bag(params (ItemId Item, int Count)[] stacks)
    {
        var bag = new Inventory();
        foreach (var (item, count) in stacks)
        {
            Assert.Equal(0, bag.Add(item, count));
        }

        return bag;
    }

    [Fact]
    public void ThePurchaseWithNoCoinsGetsTheCantPayLineInPlaceOfTheSaleLine()
    {
        var asBefore = Said("hola, quiero dos panes", "baker-open", null, out var none);
        var broke = Said("hola, quiero dos panes", "baker-open", Bag(), out var exchange);

        Assert.Empty(none!.Lines); // no bag: nothing the bag could miss (the report is the game's reply, V21b)
        Assert.NotNull(exchange);
        var saleLine = Assert.Single(asBefore.Split('\n'), static l => l.StartsWith("  agent:", StringComparison.Ordinal) && !l.Contains("Hola", StringComparison.Ordinal));
        Assert.DoesNotContain(saleLine, broke.Split('\n'));
        Assert.Contains("  agent: " + Assert.Single(exchange.Lines), broke.Split('\n').Select(static l => l.TrimEnd('\r')));
    }

    [Fact]
    public void ThePurchaseWithTheCoinsIsAsBefore()
    {
        var asBefore = Said("hola, quiero dos panes", "baker-open", null, out _);
        var paid = Said("hola, quiero dos panes", "baker-open", Bag((ItemId.Coin, 4)), out var exchange);

        Assert.Empty(exchange!.Lines);
        Assert.Equal(asBefore, paid);
    }

    [Fact]
    public void ASaleWithNoWoodGetsTheNoWoodLineAndWithWoodNone()
    {
        Said("vendo madera", "smith-open", Bag(), out var empty);
        Said("vendo madera", "smith-open", Bag((ItemId.Wood, 4)), out var held);

        Assert.NotNull(empty);
        Assert.Equal(["No tienes madera."], empty.Lines);
        Assert.Empty(held!.Lines);
    }

    [Theory]
    [InlineData("wood:4,coin:2", 2)]
    [InlineData("", 0)]
    [InlineData("wood:0", 1)]
    public void TheBagOptionIsParsedAsItemAndCount(string text, int entries)
    {
        var options = HostOptions.Parse(["--bag", text]);

        Assert.Equal(entries, options.Bag!.Count);
    }

    [Theory]
    [InlineData("wood")]
    [InlineData("wood:x")]
    [InlineData("wood:-1")]
    public void AMalformedBagIsAUsageError(string text) =>
        Assert.Throws<ArgumentException>(() => HostOptions.Parse(["--bag", text]));
}
