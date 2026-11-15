using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.Core.Needs;

/// <summary>
/// The checks of <c>game/needs.json</c> that need more than its shape (N01, adaptation design 9.10): every food and what it leaves is an item, every rate and interval is
/// positive, each need's stages fall in order and end at 0 (a drive) or below 100 (health), the factors are in range, and the waking values are meters a player can wake with.
/// <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static class NeedsValidator
{
    private const string File = NeedsData.RelativePath;

    /// <summary>Every problem of the needs, in a fixed order; empty when they hold together.</summary>
    /// <param name="needs">The needs.</param>
    /// <param name="catalogue">The item catalogue (<c>game/items.json</c>) the foods and what they leave are checked against.</param>
    public static IReadOnlyList<string> Validate(NeedsData needs, IItemCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(catalogue);
        var problems = new List<string>();
        var items = catalogue.All.Select(static d => d.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var drive in Enum.GetValues<Drive>())
        {
            var spec = needs.Drives.FirstOrDefault(d => d.Drive == drive);
            if (spec is null)
            {
                problems.Add($"{File}: the drive {drive} is missing.");
                continue;
            }

            var name = drive.ToString().ToLowerInvariant();
            Positive(problems, $"{name}'s secondsPerPoint", spec.SecondsPerPoint);
            Positive(problems, $"{name}'s sprinting factor", spec.SprintFactor);
            Positive(problems, $"{name}'s gathering factor", spec.GatherFactor);
            Text(problems, $"{name}'s detail", spec.Detail);
            Stages(problems, name, spec.Stages, 4, requireZeroEnd: true);
        }

        Text(problems, "health's detail", needs.Health.Detail);
        Positive(problems, "health's recoverySeconds", needs.Health.RecoverySeconds);
        Stages(problems, "health", needs.Health.Warnings, needs.Health.Warnings.Count, requireZeroEnd: false);
        if (needs.Health.Warnings.Count == 0)
        {
            problems.Add($"{File}: health has no warning stage.");
        }

        var stamina = needs.Stamina;
        Positive(problems, "stamina's maximum", stamina.Maximum);
        Positive(problems, "stamina's minimumMaximum", stamina.MinimumMaximum);
        Positive(problems, "stamina's refillPerSecond", stamina.RefillPerSecond);
        Positive(problems, "stamina's minimumRefillFactor", stamina.MinimumRefillFactor);
        if (stamina.MinimumMaximum > stamina.Maximum)
        {
            problems.Add($"{File}: stamina's minimumMaximum {NeedsData.Show(stamina.MinimumMaximum)} is above its maximum {NeedsData.Show(stamina.Maximum)}.");
        }

        if (stamina.MinimumRefillFactor > 1 || stamina.RefillDelaySeconds < 0 || stamina.SprintPerSecond < 0 || stamina.GatherPerTick < 0)
        {
            problems.Add($"{File}: stamina's costs and delay must not be negative, and minimumRefillFactor at most 1.");
        }

        if (stamina.SprintSpeedFactor <= 1 || stamina.GatherSlowFactor is <= 0 or > 1)
        {
            problems.Add($"{File}: stamina's sprintSpeedFactor must be above 1 and gatherSlowFactor above 0 and at most 1.");
        }

        Positive(problems, "rest's refillFactor", needs.Rest.RefillFactor);
        Positive(problems, "rest's recoveryFactor", needs.Rest.RecoveryFactor);
        if (needs.Rest.RadiusBlocks < 0)
        {
            problems.Add($"{File}: rest's radiusBlocks must not be negative.");
        }

        Positive(problems, "the collapse's delaySeconds", needs.CollapseDelaySeconds);
        Positive(problems, "drinkByHand's thirst", needs.DrinkByHandThirst);
        Change(problems, "the night", needs.Night);
        Change(problems, "the collapse", needs.Collapse);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var food in needs.Foods)
        {
            if (!items.Contains(food.Item))
            {
                problems.Add($"{File}: the food '{food.Item}' is not an item (one of {string.Join(", ", items.Order(StringComparer.Ordinal))}).");
            }

            if (!seen.Add(food.Item))
            {
                problems.Add($"{File}: the food '{food.Item}' is listed twice.");
            }

            if (food.Hunger < 0 || food.Thirst < 0 || food.Healing < 0 || (food.Hunger <= 0 && food.Thirst <= 0))
            {
                problems.Add($"{File}: the food '{food.Item}' must give hunger or thirst, and nothing may be negative.");
            }

            if (food.Leaves is { } leaves && !items.Contains(leaves))
            {
                problems.Add($"{File}: the food '{food.Item}' leaves '{leaves}', which is not an item.");
            }
        }

        var death = needs.Death;
        Text(problems, "death's wakeAt", death.WakeAt);
        SleepPlaces(problems, needs);
        if (death.Health is <= 0 or > 100 || new[] { death.Hunger, death.Thirst, death.Sleep }.Any(static v => v is < 0 or > 100))
        {
            problems.Add($"{File}: the waking values must be 0 to 100 and the waking health above 0.");
        }

        return problems;
    }

    // Y01: each sleep place has a unique id, a known kind and access, a point, and reach that is not negative; death's wakeAt names one of them.
    private static void SleepPlaces(List<string> problems, NeedsData needs)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var place in needs.SleepPlaces)
        {
            if (!ids.Add(place.Id))
            {
                problems.Add($"{File}: the sleep place '{place.Id}' is listed twice.");
            }

            if (!SleepPlaceSpec.Kinds.Contains(place.Kind, StringComparer.Ordinal))
            {
                problems.Add($"{File}: the sleep place '{place.Id}' has the kind '{place.Kind}' (one of {string.Join(", ", SleepPlaceSpec.Kinds)}).");
            }

            if (!SleepPlaceSpec.Accesses.Contains(place.Access, StringComparer.Ordinal))
            {
                problems.Add($"{File}: the sleep place '{place.Id}' has the access '{place.Access}' (one of {string.Join(", ", SleepPlaceSpec.Accesses)}).");
            }

            if (place.RadiusBlocks < 0 || place.HeightBlocks < 0)
            {
                problems.Add($"{File}: the sleep place '{place.Id}' must not have a negative radiusBlocks or heightBlocks.");
            }
        }

        if (!ids.Contains(needs.Death.WakeAt))
        {
            problems.Add($"{File}: death's wakeAt '{needs.Death.WakeAt}' is not a listed sleep place (one of {string.Join(", ", needs.SleepPlaces.Select(static p => p.Id))}).");
        }
    }

    private static void Stages(List<string> problems, string name, IReadOnlyList<NeedStageSpec> stages, int expected, bool requireZeroEnd)
    {
        if (stages.Count != expected)
        {
            problems.Add($"{File}: {name} needs {expected} stages, not {stages.Count}.");
        }

        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            var label = $"{name} stage {i + 1}";
            if (stage.Point is < 0 or > 100)
            {
                problems.Add($"{File}: the point of {label} must be 0 to 100, not {NeedsData.Show(stage.Point)}.");
            }

            if (i > 0 && stage.Point >= stages[i - 1].Point)
            {
                problems.Add($"{File}: the stages of {name} are out of order: stage {i + 1} ({NeedsData.Show(stage.Point)}) must be below stage {i} ({NeedsData.Show(stages[i - 1].Point)}).");
            }

            Text(problems, $"the line of {label}", stage.Line);
            var e = stage.Effects;
            if (e.StaminaPenalty < 0 || e.RefillFactor is <= 0 or > 1 || e.WalkFactor is <= 0 or > 1 || e.HealthLoss < 0 || (e.HealthLoss > 0 && e.HealthLossEverySeconds <= 0))
            {
                problems.Add($"{File}: the effects of {label} are out of range (a penalty or loss not negative, the refill and walking factors above 0 and at most 1, a loss with an interval above 0).");
            }
        }

        if (requireZeroEnd && stages.Count > 0 && stages[^1].Point != 0)
        {
            problems.Add($"{File}: the last stage of {name} must be at 0, not {NeedsData.Show(stages[^1].Point)}.");
        }
    }

    private static void Change(List<string> problems, string name, SleepChange change)
    {
        if (change.SleepTo is < 0 or > 100 || change.Health is < 0 or > 100 || change.Floor is < 0 or > 100 || change.HungerCost < 0 || change.ThirstCost < 0)
        {
            problems.Add($"{File}: {name} has a value out of range (sleepTo, health and floor 0 to 100; the costs not negative).");
        }
    }

    private static void Positive(List<string> problems, string what, double value)
    {
        if (!(value > 0) || !double.IsFinite(value))
        {
            problems.Add($"{File}: {what} must be above 0, not {NeedsData.Show(value)}.");
        }
    }

    private static void Text(List<string> problems, string what, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add($"{File}: {what} is empty.");
        }
    }
}
