using Vocaterra.Game.Core.Event;

namespace Vocaterra.Game.Core.Tests.Event;

/// <summary>The console's event lines: each new event once, in order, with the support lines it adds after it.</summary>
public sealed class EventPrinterTests
{
    [Fact]
    public void EachEventIsPrintedOnceInOrderWithItsSupportLinesAfterIt()
    {
        var events = new List<GameEvent>();
        var lines = new List<string>();
        var printer = new EventPrinter(events, e => e is WorldEvent { Name: "b" } ? ["  support: b"] : [], lines.Add);
        printer.Print();
        events.Add(new WorldEvent(1, 0, "a"));
        printer.Print();
        events.Add(new WorldEvent(2, 0, "b", "guide"));
        events.Add(new WorldEvent(3, 0, "c", Detail: "x"));
        printer.Print();
        printer.Print();
        Assert.Equal(["  event: world a", "  event: world b at guide", "  support: b", "  event: world c (x)"], lines);
    }
}
