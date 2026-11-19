using System.Globalization;

namespace Vocaterra.Game.Startup;

/// <summary>The console line of the <c>--give</c> test aid (I10b, QA-2026-11-19-ui-5): it names the item and says when nothing went in.</summary>
public static class GiveLine
{
    /// <summary>The line for one item given.</summary>
    /// <param name="name">The item's name in the catalogue (<c>wood</c>, <c>bread</c>).</param>
    /// <param name="notGiven">How many of the one did not fit (the bag was full).</param>
    /// <param name="inBag">How many of the item the bag holds now.</param>
    public static string Text(string name, int notGiven, int inBag) =>
        notGiven > 0
            ? string.Create(CultureInfo.InvariantCulture, $"give: {name} not given, the bag is full, bag {inBag}")
            : string.Create(CultureInfo.InvariantCulture, $"give: {name}, bag {inBag}");
}
