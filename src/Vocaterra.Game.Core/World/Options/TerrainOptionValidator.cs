using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vocaterra.Game.Core.World.Biomes;

namespace Vocaterra.Game.Core.World.Options;

/// <summary>
/// The checks of <c>game/world-options.json</c> that need more than its shape (Q02): every row has an id of its own, a label, a grid that holds (a step that reaches the maximum from the minimum, a default on it) and
/// effects that name targets the code has and the world file contains; a target is moved by one option only; a <c>set</c> effect's default is the file's own value, and a <c>scale</c> of the default and an <c>offset</c> of
/// the default leave the numbers exactly as they are, so a world at the defaults is the shipped world; and every value on every grid, and the lowest and the highest of all the options together, gives numbers that
/// <see cref="WorldGenValidator"/> and <see cref="BiomeValidator"/> accept (the validators' own ranges are the ranges of the sliders). <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static partial class TerrainOptionValidator
{
    private const string File = TerrainOptionData.RelativePath;
    private const int MostStops = 64;

    /// <summary>Every problem of the rows, in a fixed order; empty when they hold together.</summary>
    /// <param name="table">The rows.</param>
    /// <param name="world">The shipped world numbers the options are laid over.</param>
    /// <param name="biomes">The biome rows, which the extreme worlds are checked against too.</param>
    public static IReadOnlyList<string> Validate(TerrainOptionData table, WorldGenData world, BiomeData biomes)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(biomes);
        var problems = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);
        var usable = new List<TerrainOption>();
        foreach (var option in table.Options)
        {
            var rowProblems = problems.Count;
            if (!IdPattern().IsMatch(option.Id))
            {
                problems.Add($"{File}: the option id '{option.Id}' must be lower-case letters and digits, starting with a letter.");
            }
            else if (!ids.Add(option.Id))
            {
                problems.Add($"{File}: the option id '{option.Id}' is used twice.");
            }

            if (string.IsNullOrWhiteSpace(option.Label))
            {
                problems.Add($"{File}: option '{option.Id}' needs a label.");
            }

            CheckGrid(option, problems);
            CheckEffects(option, world, targets, problems);
            if (problems.Count == rowProblems)
            {
                usable.Add(option);
            }
        }

        if (table.Options.Count == 0)
        {
            problems.Add($"{File}: there must be at least one option.");
        }

        CheckWorlds(usable, world, biomes, problems);
        return problems;
    }

    private static void CheckGrid(TerrainOption option, List<string> problems)
    {
        var id = option.Id;
        if (option.Step < 1 || option.Min >= option.Max || (option.Max - option.Min) % option.Step != 0)
        {
            problems.Add($"{File}: option '{id}' needs a min below its max and a step of at least 1 that reaches the max from the min (they are {option.Min}, {option.Max} and {option.Step}).");
            return;
        }

        if (option.Stops > MostStops)
        {
            problems.Add($"{File}: option '{id}' has {option.Stops} stops; a slider has at most {MostStops}.");
        }

        if (!option.OnGrid(option.Default))
        {
            problems.Add($"{File}: option '{id}' has a default of {option.Default}, which is not a stop from {option.Min} to {option.Max} in steps of {option.Step}.");
        }

        if (option.Labels.Count > 0 && (option.Labels.Count != option.Stops || option.Labels.Any(string.IsNullOrWhiteSpace)))
        {
            problems.Add($"{File}: option '{id}' has {option.Labels.Count} labels for {option.Stops} stops; give a label for every stop or none.");
        }
    }

    private static void CheckEffects(TerrainOption option, WorldGenData world, Dictionary<string, string> targets, List<string> problems)
    {
        var id = option.Id;
        if (option.Effects.Count == 0)
        {
            problems.Add($"{File}: option '{id}' changes nothing: it needs at least one effect.");
        }

        foreach (var effect in option.Effects)
        {
            if (!TerrainTarget.All.TryGetValue(effect.Target, out var target))
            {
                problems.Add($"{File}: option '{id}' names the target '{effect.Target}', which the generator does not offer (the targets are {string.Join(", ", TerrainTarget.All.Keys.Order(StringComparer.Ordinal))}).");
                continue;
            }

            if (!targets.TryAdd(effect.Target, id))
            {
                problems.Add($"{File}: option '{id}' and option '{targets[effect.Target]}' both change '{effect.Target}'; one option moves a target.");
            }

            if (target.Read(world) is not { } current)
            {
                problems.Add($"{File}: option '{id}' changes '{effect.Target}', which game/world.json has no section for.");
                continue;
            }

            switch (effect.Mode)
            {
                case TerrainMode.Scale when option.Default != 100 || option.Min < 0:
                    problems.Add($"{File}: option '{id}' scales '{effect.Target}': its default must be 100 (percent of today's value) and its min 0 or more.");
                    break;
                case TerrainMode.ScaleInverse when option.Default != 100 || option.Min < 1:
                    problems.Add($"{File}: option '{id}' scales '{effect.Target}' inversely: its default must be 100 (percent) and its min 1 or more.");
                    break;
                case TerrainMode.Offset when option.Default != 0:
                    problems.Add($"{File}: option '{id}' offsets '{effect.Target}': its default must be 0.");
                    break;
                case TerrainMode.Set when current.Any(value => value != option.Default):
                    problems.Add($"{File}: option '{id}' sets '{effect.Target}': its default {option.Default} must be the world file's value ({string.Join(", ", current)}).");
                    break;
                default:
                    break;
            }
        }
    }

    // The numbers at every stop of every usable option, and with all options at their lowest and at their highest together, must pass the world's validators; at its default every option changes nothing.
    private static void CheckWorlds(List<TerrainOption> options, WorldGenData world, BiomeData biomes, List<string> problems)
    {
        var shipped = JsonSerializer.Serialize(world);
        foreach (var option in options)
        {
            if (!string.Equals(JsonSerializer.Serialize(TerrainOptionData.ApplyOne(world, option, option.Default)), shipped, StringComparison.Ordinal))
            {
                problems.Add($"{File}: option '{option.Id}' at its default {option.Default} changes the world's numbers; the default must leave them as game/world.json has them.");
            }

            for (var value = option.Min; value <= option.Max; value += option.Step)
            {
                var found = WorldGenValidator.Validate(TerrainOptionData.ApplyOne(world, option, value));
                if (found.Count > 0)
                {
                    problems.Add(string.Create(CultureInfo.InvariantCulture, $"{File}: option '{option.Id}' at {value} gives numbers game/world.json's checks refuse: {found[0]}"));
                }
            }
        }

        foreach (var (name, pick) in new (string, Func<TerrainOption, int>)[] { ("lowest", static o => o.Min), ("highest", static o => o.Max) })
        {
            var all = world;
            foreach (var option in options)
            {
                all = TerrainOptionData.ApplyOne(all, option, pick(option));
            }

            var found = WorldGenValidator.Validate(all).Concat(BiomeValidator.Validate(biomes, all)).ToList();
            if (found.Count > 0)
            {
                problems.Add($"{File}: with every option at its {name} value together the numbers are refused: {found[0]}");
            }
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9]*$")]
    private static partial Regex IdPattern();
}
