using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.Language.Checks;

/// <summary>When a check battery runs (D-game-10): before the game, right after it, or on a later day.</summary>
public enum CheckKind
{
    /// <summary>The pre-check, before the world (the baseline).</summary>
    Pre,

    /// <summary>The post-check, right after the play (the immediate measure).</summary>
    Post,

    /// <summary>The recall check, in a later session (the delayed measure).</summary>
    Recall,
}

/// <summary>The wire names of the check kinds (<c>pre</c>, <c>post</c>, <c>recall</c>), for the command line and the session log.</summary>
public static class CheckKinds
{
    /// <summary>The wire name of a check kind.</summary>
    public static string Of(CheckKind kind) => kind switch
    {
        CheckKind.Pre => "pre",
        CheckKind.Post => "post",
        CheckKind.Recall => "recall",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown check kind."),
    };

    /// <summary>Reads a check kind from its wire name, or <see langword="null"/> when the name is none of the three.</summary>
    public static CheckKind? Parse(string? name) => name switch
    {
        "pre" => CheckKind.Pre,
        "post" => CheckKind.Post,
        "recall" => CheckKind.Recall,
        _ => null,
    };
}

/// <summary>
/// One item of a check battery as it is shown: the item, its place in the battery, and for a comprehension item the order its three options
/// are shown in (<see cref="OptionOrder"/>, which holds the bundle index of each shown option).
/// </summary>
/// <param name="Item">The item.</param>
/// <param name="Position">Its place in the battery, from 0.</param>
/// <param name="OptionOrder">The bundle index of each option, in the order shown; empty for a production item.</param>
public sealed record CheckPlanEntry(CheckItem Item, int Position, IReadOnlyList<int> OptionOrder)
{
    /// <summary>The position, among the shown options, of the right option (-1 for a production item).</summary>
    public int ShownCorrect => Item.Kind == CheckItemKind.Comprehension ? OptionOrder.ToList().IndexOf(Item.Correct) : -1;

    /// <summary>The options in the order shown.</summary>
    public IReadOnlyList<CheckOption> ShownOptions => OptionOrder.Select(index => Item.Options[index]).ToList();

    /// <summary>Whether a shown option (from 0) is the right one (a comprehension item only).</summary>
    /// <param name="shown">The position the learner picked, among the shown options.</param>
    public bool IsCorrect(int shown) => Item.Kind == CheckItemKind.Comprehension && shown == ShownCorrect;
}

/// <summary>
/// The order of a check battery: which items come in which order and how each comprehension item's options are shown, drawn from the world seed
/// and the check kind (<see cref="WorldRng"/>, never <c>System.Random</c>). A pre-check and a post-check have the same items and options, in other
/// orders; the same seed and kind always give the same plan.
/// </summary>
public static class CheckPlan
{
    /// <summary>Builds the plan of a battery for a kind and a world seed.</summary>
    /// <param name="bank">The items, in the bundle's order.</param>
    /// <param name="kind">The check kind.</param>
    /// <param name="worldSeed">The world seed.</param>
    public static IReadOnlyList<CheckPlanEntry> For(IReadOnlyList<CheckItem> bank, CheckKind kind, ulong worldSeed)
    {
        ArgumentNullException.ThrowIfNull(bank);
        var name = CheckKinds.Of(kind);
        var order = Enumerable.Range(0, bank.Count).ToArray();
        Shuffle(order, WorldRng.For(worldSeed, "check.order." + name));
        var plan = new List<CheckPlanEntry>(bank.Count);
        for (var position = 0; position < order.Length; position++)
        {
            var item = bank[order[position]];
            var options = item.Kind == CheckItemKind.Comprehension
                ? Shuffle(Enumerable.Range(0, item.Options.Count).ToArray(), WorldRng.For(worldSeed, "check.options." + name + "." + item.Id))
                : [];
            plan.Add(new CheckPlanEntry(item, position, options));
        }

        return plan;
    }

    // A Fisher-Yates shuffle over the seeded generator: the same generator state gives the same permutation on every machine.
    private static int[] Shuffle(int[] values, WorldRng rng)
    {
        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        return values;
    }
}
