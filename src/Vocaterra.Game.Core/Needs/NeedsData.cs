using System.Globalization;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Needs;

/// <summary>What one stage of a need does (design 9.3). An absent field is the neutral value. Each stage replaces the effects of the one before it.</summary>
/// <param name="StaminaPenalty">Taken off stamina's maximum (the three drives' penalties add).</param>
/// <param name="RefillFactor">Stamina's refill factor (the three drives' factors multiply).</param>
/// <param name="WalkFactor">The walking factor (the slowest in force applies).</param>
/// <param name="SprintAllowed">Whether the stage lets the player sprint.</param>
/// <param name="HealthLoss">Health lost every <paramref name="HealthLossEverySeconds"/> (the drives' losses add); zero for none.</param>
/// <param name="HealthLossEverySeconds">The interval of the loss, in seconds of active play.</param>
/// <param name="BlocksRecovery">Whether health stops recovering.</param>
/// <param name="View">The name of the view effect the HUD draws, or <see langword="null"/>.</param>
public sealed record StageEffects(
    double StaminaPenalty = 0,
    double RefillFactor = 1,
    double WalkFactor = 1,
    bool SprintAllowed = true,
    double HealthLoss = 0,
    double HealthLossEverySeconds = 0,
    bool BlocksRecovery = false,
    string? View = null)
{
    /// <summary>The effects of a healthy need: nothing.</summary>
    public static StageEffects None { get; } = new();
}

/// <summary>One stage of a need: the point it applies at, the key of its line and its effects.</summary>
/// <param name="Point">A drive's stages 1 to 3 apply below their point and stage 4 at its point; a health warning applies below its point.</param>
/// <param name="Line">The key of the stage's line (its text is the HUD's, N05).</param>
/// <param name="Effects">What the stage does.</param>
public sealed record NeedStageSpec(double Point, string Line, StageEffects Effects);

/// <summary>One drive's rules.</summary>
/// <param name="Drive">Which drive.</param>
/// <param name="Detail">The detail of the <c>need_stage</c> event (<c>hambre</c>).</param>
/// <param name="SecondsPerPoint">Seconds of active play for the meter to fall by 1.</param>
/// <param name="SprintFactor">How much faster it falls while sprinting.</param>
/// <param name="GatherFactor">How much faster it falls while gathering.</param>
/// <param name="Stages">Stages 1 to 4, in order.</param>
public sealed record DriveSpec(Drive Drive, string Detail, double SecondsPerPoint, double SprintFactor, double GatherFactor, IReadOnlyList<NeedStageSpec> Stages);

/// <summary>Health's rules: its recovery and its warning stages.</summary>
/// <param name="Detail">The detail of the <c>need_stage</c> event (<c>salud</c>).</param>
/// <param name="RecoverySeconds">Seconds of active play for 1 health to come back.</param>
/// <param name="Warnings">The warning stages in order (below 50, below 25).</param>
public sealed record HealthSpec(string Detail, double RecoverySeconds, IReadOnlyList<NeedStageSpec> Warnings);

/// <summary>Stamina's costs and refill.</summary>
/// <param name="Maximum">The full maximum.</param>
/// <param name="MinimumMaximum">The maximum never falls below this.</param>
/// <param name="RefillPerSecond">The refill speed.</param>
/// <param name="RefillDelaySeconds">Seconds without effort before it refills.</param>
/// <param name="MinimumRefillFactor">The product of the refill factors never falls below this.</param>
/// <param name="SprintPerSecond">Spent a second of sprinting.</param>
/// <param name="GatherPerTick">Spent a gather tick.</param>
/// <param name="SprintSpeedFactor">Sprinting walks this many times as fast (N02; 1.6 when the data names none).</param>
/// <param name="GatherSlowFactor">Gathering at zero stamina goes this fast, never 0 (N02; a half when the data names none).</param>
public sealed record StaminaSpec(
    double Maximum,
    double MinimumMaximum,
    double RefillPerSecond,
    double RefillDelaySeconds,
    double MinimumRefillFactor,
    double SprintPerSecond,
    double GatherPerTick,
    double SprintSpeedFactor = 1.6,
    double GatherSlowFactor = 0.5);

/// <summary>Rest at a lit fire.</summary>
/// <param name="RadiusBlocks">How near the fire, in blocks.</param>
/// <param name="RefillFactor">Stamina's refill is this many times as fast.</param>
/// <param name="RecoveryFactor">Health's recovery is this many times as fast.</param>
public sealed record RestSpec(int RadiusBlocks, double RefillFactor, double RecoveryFactor);

/// <summary>What a night or a collapse does (once).</summary>
/// <param name="SleepTo">Sleep is set to this.</param>
/// <param name="Health">Health added, up to 100.</param>
/// <param name="HungerCost">Taken off hunger.</param>
/// <param name="ThirstCost">Taken off thirst.</param>
/// <param name="Floor">No drive is lowered below this; a drive already below it is left where it is.</param>
public sealed record SleepChange(double SleepTo, double Health, double HungerCost, double ThirstCost, double Floor);

/// <summary>What a food gives. The water bottle is a food too (thirst, and the empty bottle it leaves).</summary>
/// <param name="Item">The item's key (<c>berry</c>).</param>
/// <param name="Hunger">Hunger restored.</param>
/// <param name="Thirst">Thirst restored.</param>
/// <param name="Healing">Health restored at once.</param>
/// <param name="Leaves">The item left in the bag, or <see langword="null"/>.</param>
public sealed record NeedFood(string Item, double Hunger, double Thirst, double Healing, string? Leaves);

/// <summary>What happens at a death (design 9.7). The rules about repeated deaths and an occupied chest site are N04's.</summary>
/// <param name="WakeAt">The id of the sleep place (<see cref="SleepPlaceSpec"/>) the player wakes at.</param>
/// <param name="Health">Health on waking.</param>
/// <param name="Hunger">Hunger on waking.</param>
/// <param name="Thirst">Thirst on waking.</param>
/// <param name="Sleep">Sleep on waking.</param>
/// <param name="KeepInBag">What stays in the bag (<see cref="StacksOfOne"/>); <see cref="Keeps"/> reads it. The loader refuses a value it does not know.</param>
/// <param name="ChestLastsUntil">When the chest goes (<c>night</c>).</param>
public sealed record DeathSpec(string WakeAt, double Health, double Hunger, double Thirst, double Sleep, string KeepInBag, string ChestLastsUntil)
{
    /// <summary>The <see cref="KeepInBag"/> rule of the first game: the items that stack to one (tools, gear, bags) stay in their slots; every stackable goes to the chest. Worn items and the worn bag are not in the bag and always stay.</summary>
    public const string StacksOfOne = "stacksOfOne";

    /// <summary>The values of <see cref="KeepInBag"/> the code knows.</summary>
    public static IReadOnlyList<string> KnownKeepRules { get; } = [StacksOfOne];

    /// <summary>Whether a stack of <paramref name="item"/> stays in the bag at a death under <see cref="KeepInBag"/> (the rest goes to the belongings chest).</summary>
    /// <param name="item">The item's definition.</param>
    /// <exception cref="InvalidOperationException">The rule is one the code does not know (the loader refuses that, so only a hand-built spec gets here).</exception>
    public bool Keeps(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return string.Equals(KeepInBag, StacksOfOne, StringComparison.Ordinal)
            ? item.MaxStack == 1
            : throw new InvalidOperationException($"The death rule keepInBag '{KeepInBag}' is not one of {string.Join(", ", KnownKeepRules)}.");
    }
}

/// <summary>
/// A place where the player can sleep (Y01, <c>game/needs.json</c>'s <c>sleepPlaces</c>; not <see cref="RestSpec"/>, which is resting by a lit fire): a fire is usable while its ring burns, a bed
/// when its <see cref="Access"/> is met. A place whose <see cref="Point"/> the scene lacks is left out.
/// </summary>
/// <param name="Id">The place's id (<c>guide.fire</c>), which <see cref="DeathSpec.WakeAt"/> names.</param>
/// <param name="Point">The scene point the place stands at (<c>firering</c>, Luna's fire ring).</param>
/// <param name="Kind"><see cref="SleepPlaceSpec.KindFire"/> or <see cref="SleepPlaceSpec.KindBed"/>.</param>
/// <param name="RadiusBlocks">How far across, in blocks, of the point the player may stand to sleep there.</param>
/// <param name="HeightBlocks">How far in height, in blocks, of the point.</param>
/// <param name="Access"><see cref="SleepPlaceSpec.AccessAlways"/> or <see cref="SleepPlaceSpec.AccessLodging"/> (met once lodging is given).</param>
public sealed record SleepPlaceSpec(string Id, string Point, string Kind, int RadiusBlocks, int HeightBlocks, string Access)
{
    /// <summary>A fire: usable while a lit ring stands at the place.</summary>
    public const string KindFire = "fire";

    /// <summary>A bed: usable when its access is met.</summary>
    public const string KindBed = "bed";

    /// <summary>Access that is always met.</summary>
    public const string AccessAlways = "always";

    /// <summary>Access met once the session has lodging.</summary>
    public const string AccessLodging = "lodging";

    /// <summary>The known kinds.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [KindFire, KindBed];

    /// <summary>The known accesses.</summary>
    public static IReadOnlyList<string> Accesses { get; } = [AccessAlways, AccessLodging];
}

/// <summary>A block that grows back (N06, design 9.6): what the player gathered by hand returns after the next night.</summary>
/// <param name="Block">The block's name (<c>berry-bush</c>): the block the world has before a gather. The gathered cell holds the block's <c>BecomesWhenGathered</c> until it grows back.</param>
/// <param name="After">When it grows back: <c>night</c> (a sleep or a collapse, <c>RunDayResets</c>).</param>
public sealed record RegrowRule(string Block, string After);

/// <summary>
/// The berry the player plants at the camp (N08, design decision 14): planted on grass or dirt inside the radius of the camp's fire ring, it becomes <paramref name="Block"/> at once
/// and <paramref name="GrownBlock"/> after the next night (through <see cref="RegrowRule"/>, so it is picked and grown back like any berry bush).
/// </summary>
/// <param name="Item">The item planted (<c>berry</c>).</param>
/// <param name="Block">The block the planted cell becomes at once (<c>bush</c>, the plain bush a picked berry bush is).</param>
/// <param name="GrownBlock">The block it grows into after a night (<c>berry-bush</c>): it must have a regrowth rule.</param>
/// <param name="RadiusBlocks">How far from the camp's fire ring, horizontally, a bush may be planted (blocks).</param>
/// <param name="MaxBushes">The most planted bushes alive at once.</param>
public sealed record PlantingRule(string Item, string Block, string GrownBlock, int RadiusBlocks, int MaxBushes);

/// <summary>A gift an NPC gives once a day (N06, design 9.6). A gift a lesson step requires is always given.</summary>
/// <param name="Npc">The NPC's agent id (<c>shopkeeper</c>, <c>villager</c>, <c>guide</c>).</param>
/// <param name="Item">The item's key (<c>bread</c>): the NPC's host event that gives it is the gift.</param>
/// <param name="Per">How often: <c>day</c>, once between two nights.</param>
public sealed record GiftRule(string Npc, string Item, string Per)
{
    /// <summary>The gift's key in the session and the save: <c>shopkeeper:bread</c>.</summary>
    public string Key => KeyOf(Npc, Item);

    /// <summary>The key of an NPC's item.</summary>
    /// <param name="npc">The agent id.</param>
    /// <param name="item">The item's key.</param>
    public static string KeyOf(string npc, string item) => $"{npc}:{item}";
}

/// <summary>
/// The player's needs as data (N01, <c>game/needs.json</c>; adaptation design 9.10): the three drives, health and stamina with their stages and links, the foods, and
/// the rules of a night, a collapse and a death. The loader reads the shape; <see cref="GameDataValidator"/> checks the values (every food an item, every rate
/// positive, the stages in order). The regrowth and gift rules are N06's, both optional in the file; the roast apple (N07) is not here yet: the schema takes it as an optional part later.
/// </summary>
/// <param name="Drives">Hunger, thirst and sleep, in that order.</param>
/// <param name="Health">Health's rules.</param>
/// <param name="Stamina">Stamina's rules.</param>
/// <param name="Rest">Rest at a lit fire.</param>
/// <param name="Night">What a night does.</param>
/// <param name="Collapse">What a collapse does.</param>
/// <param name="CollapseDelaySeconds">Seconds of active play at sleep's last stage before the collapse.</param>
/// <param name="DrinkByHandThirst">Thirst restored by a drink by hand.</param>
/// <param name="Foods">What each food gives.</param>
/// <param name="Death">The death rules.</param>
/// <param name="SleepPlaces">The places the player can sleep at (Y01).</param>
/// <param name="Regrowth">The blocks that grow back after a night (N06); none when the file has no <c>regrowth</c>.</param>
/// <param name="Gifts">The gifts given once a day (N06); none when the file has no <c>gifts</c>.</param>
/// <param name="Planting">The berry planted at the camp (N08), or <see langword="null"/> when the file has no <c>planting</c>.</param>
public sealed record NeedsData(
    IReadOnlyList<DriveSpec> Drives,
    HealthSpec Health,
    StaminaSpec Stamina,
    RestSpec Rest,
    SleepChange Night,
    SleepChange Collapse,
    double CollapseDelaySeconds,
    double DrinkByHandThirst,
    IReadOnlyList<NeedFood> Foods,
    DeathSpec Death,
    IReadOnlyList<SleepPlaceSpec> SleepPlaces,
    IReadOnlyList<RegrowRule> Regrowth,
    IReadOnlyList<GiftRule> Gifts,
    PlantingRule? Planting = null)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/needs.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The only moment a block grows back (<see cref="RegrowRule.After"/>).</summary>
    public const string AfterNight = "night";

    /// <summary>The only rate of a gift (<see cref="GiftRule.Per"/>).</summary>
    public const string PerDay = "day";

    /// <summary>The simulation's ticks a second (the gather cost is per tick).</summary>
    public const int TicksPerSecond = 60;

    /// <summary>One drive's rules.</summary>
    /// <param name="drive">Which drive.</param>
    public DriveSpec Of(Drive drive) => Drives.First(d => d.Drive == drive);

    /// <summary>The gift rule of an NPC's item, or <see langword="null"/> when the NPC gives it freely.</summary>
    /// <param name="npc">The agent id.</param>
    /// <param name="item">The item's key.</param>
    public GiftRule? GiftOf(string npc, string item) => Gifts.FirstOrDefault(g => string.Equals(g.Npc, npc, StringComparison.Ordinal) && string.Equals(g.Item, item, StringComparison.Ordinal));

    /// <summary>What a food item gives, or <see langword="null"/> when the item is not a food.</summary>
    /// <param name="item">The item's key (<c>bread</c>).</param>
    public NeedFood? FoodOf(string item) => Foods.FirstOrDefault(f => string.Equals(f.Item, item, StringComparison.Ordinal));

    /// <summary>Reads the needs from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static NeedsData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the needs from the file's text. The shape is checked here; the values are <see cref="GameDataValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/needs.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
    public static NeedsData Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (Number(root, "schemaVersion") != SchemaVersion)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion {SchemaVersion}.");
            }

            var drives = root.GetProperty("drives");
            var health = root.GetProperty("health");
            var stamina = root.GetProperty("stamina");
            var rest = root.GetProperty("rest");
            var collapse = root.GetProperty("collapse");
            var death = root.GetProperty("death");
            var wake = death.GetProperty("wake");
            var keepInBag = Text(death, "keepInBag");
            if (!DeathSpec.KnownKeepRules.Contains(keepInBag, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"{RelativePath}: death.keepInBag is '{keepInBag}'; the rules are {string.Join(", ", DeathSpec.KnownKeepRules)}.");
            }

            return new NeedsData(
                [DriveOf(Drive.Hunger, drives, "hunger"), DriveOf(Drive.Thirst, drives, "thirst"), DriveOf(Drive.Sleep, drives, "sleep")],
                new HealthSpec(Text(health, "detail"), Number(health, "recoverySeconds"), health.GetProperty("warnings").EnumerateArray().Select(StageOf).ToList()),
                new StaminaSpec(
                    Number(stamina, "maximum"), Number(stamina, "minimumMaximum"), Number(stamina, "refillPerSecond"), Number(stamina, "refillDelaySeconds"),
                    Number(stamina, "minimumRefillFactor"), Number(stamina, "sprintPerSecond"), Number(stamina, "gatherPerTick"),
                    Optional(stamina, "sprintSpeedFactor", 1.6), Optional(stamina, "gatherSlowFactor", 0.5)),
                new RestSpec((int)Number(rest, "radiusBlocks"), Number(rest, "refillFactor"), Number(rest, "recoveryFactor")),
                ChangeOf(root.GetProperty("night")),
                ChangeOf(collapse),
                Number(collapse, "delaySeconds"),
                Number(root.GetProperty("drinkByHand"), "thirst"),
                root.GetProperty("foods").EnumerateArray().Select(FoodOf).ToList(),
                new DeathSpec(Text(death, "wakeAt"), Number(wake, "health"), Number(wake, "hunger"), Number(wake, "thirst"), Number(wake, "sleep"), keepInBag, Text(death, "chestLastsUntil")),
                root.GetProperty("sleepPlaces").EnumerateArray().Select(static p => new SleepPlaceSpec(
                    Text(p, "id"), Text(p, "point"), Text(p, "kind"), (int)Number(p, "radiusBlocks"), (int)Number(p, "heightBlocks"), Text(p, "access"))).ToList(),
                root.TryGetProperty("regrowth", out var regrowth) ? regrowth.EnumerateArray().Select(static r => new RegrowRule(Text(r, "block"), Text(r, "after"))).ToList() : [],
                root.TryGetProperty("gifts", out var gifts) ? gifts.EnumerateArray().Select(static g => new GiftRule(Text(g, "npc"), Text(g, "item"), Text(g, "per"))).ToList() : [],
                root.TryGetProperty("planting", out var planting)
                    ? new PlantingRule(Text(planting, "item"), Text(planting, "block"), Text(planting, "grownBlock"), (int)Number(planting, "radiusBlocks"), (int)Number(planting, "maxBushes"))
                    : null);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static DriveSpec DriveOf(Drive drive, JsonElement drives, string name)
    {
        var element = drives.GetProperty(name);
        var effort = element.GetProperty("effort");
        return new DriveSpec(
            drive,
            Text(element, "detail"),
            Number(element, "secondsPerPoint"),
            Number(effort, "sprinting"),
            Number(effort, "gathering"),
            element.GetProperty("stages").EnumerateArray().Select(StageOf).ToList());
    }

    private static NeedStageSpec StageOf(JsonElement stage)
    {
        var effects = stage.GetProperty("effects");
        return new NeedStageSpec(
            Number(stage, "point"),
            Text(stage, "line"),
            new StageEffects(
                Optional(effects, "staminaPenalty", 0),
                Optional(effects, "refillFactor", 1),
                Optional(effects, "walkFactor", 1),
                !effects.TryGetProperty("sprintAllowed", out var sprint) || sprint.GetBoolean(),
                Optional(effects, "healthLoss", 0),
                Optional(effects, "healthLossEverySeconds", 0),
                effects.TryGetProperty("blocksRecovery", out var blocks) && blocks.GetBoolean(),
                effects.TryGetProperty("view", out var view) ? view.GetString() : null));
    }

    private static SleepChange ChangeOf(JsonElement element) =>
        new(Number(element, "sleepTo"), Number(element, "health"), Number(element, "hungerCost"), Number(element, "thirstCost"), Number(element, "floor"));

    private static NeedFood FoodOf(JsonElement food) =>
        new(Text(food, "item"), Optional(food, "hunger", 0), Optional(food, "thirst", 0), Optional(food, "healing", 0), food.TryGetProperty("leaves", out var leaves) ? leaves.GetString() : null);

    private static double Number(JsonElement element, string name) => element.GetProperty(name).GetDouble();

    private static double Optional(JsonElement element, string name, double fallback) => element.TryGetProperty(name, out var value) ? value.GetDouble() : fallback;

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"{RelativePath}: the text '{name}' is missing or empty.");

    /// <summary>A number as the data's text spells it, for a validator's message.</summary>
    internal static string Show(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
