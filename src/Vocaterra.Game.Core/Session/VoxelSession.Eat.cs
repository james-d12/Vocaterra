using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Session;

// Eating, drinking and sprinting (N02, adaptation design 9.6): the player's actions on the needs. Eat is a right click with a food selected, a drink is a right click with the
// water bottle or E at water by hand; sprint is read before every tick. The meters' rules stay in PlayerNeeds; here the actions use the items, raise the events and tell the player.
public sealed partial class VoxelSession
{
    /// <summary>The ui key of the line for food kept at full hunger.</summary>
    public const string FoodKeptHint = "foodKept";

    /// <summary>The ui key of the line for a drink kept (or water refused) at full thirst.</summary>
    public const string DrinkKeptHint = "drinkKept";

    // How far from the well's point (blocks across) the water is the well's, and from the lake's point it is the lake's; any other water is the river's.
    private const int WellRadiusBlocks = 5;
    private const int LakeRadiusBlocks = 46;

    /// <summary>Whether the sprint key is held (set by the game each frame; the session decides whether it counts: a stage, stamina and moving all matter).</summary>
    public bool SprintHeld { get; set; }

    /// <summary>The fire rings of the world and which burn (design 9.4's rest).</summary>
    public FireRings Fires => Simulation.Fires;

    /// <summary>Whether the crosshair is on water within reach: E then drinks by hand, with nothing held and without Spanish.</summary>
    public bool AimedAtWater => Simulation.InteractTarget is { Id: BlockId.Water };

    /// <summary>
    /// Eats or drinks the selected hotbar item (the right click): a food in <c>needs.json</c> is used up and its hunger, thirst and healing added, capped; the water bottle comes
    /// back as the empty bottle. At full hunger (or thirst, for the bottle) the item is kept and a line says so. Raises <see cref="WorldEvents.MealEaten"/> with the food's noun
    /// (a food that gives hunger) and <see cref="WorldEvents.WaterDrunk"/> with <c>botella</c> (a drink). With the needs off the item is still used up, the meters stay where they are.
    /// </summary>
    public NeedActionResult EatSelected() => Simulation.SelectedStack is { IsEmpty: false } stack ? EatItem(stack.Item) : NeedActionResult.Nothing;

    /// <summary>
    /// Eats or drinks one of an item from the bag (what <see cref="EatSelected"/> does with the selected stack): nothing when the bag has none, the item is no food or the player is
    /// dead. The scripted <c>--eat</c> calls it after giving the item.
    /// </summary>
    /// <param name="item">The food.</param>
    public NeedActionResult EatItem(ItemId item)
    {
        if (Simulation.Inventory.Count(item) < 1 || Data.Needs.FoodOf(ItemDefinition.Get(item).Name) is not { } food || Needs.IsDead)
        {
            return NeedActionResult.Nothing;
        }

        var isDrink = food.Hunger <= 0 && food.Thirst > 0;
        if (NeedsEnabled && !Needs.Eat(food))
        {
            _hints.Add(Locale.Ui(isDrink ? DrinkKeptHint : FoodKeptHint));
            return NeedActionResult.Kept;
        }

        Simulation.TakeItems(item, 1);
        var kept = item; // what stays in the bag, and names a drink in the event: the empty bottle after the water bottle
        if (food.Leaves is { } leaves && ItemDefinition.All.FirstOrDefault(d => string.Equals(d.Name, leaves, StringComparison.Ordinal)) is { } left)
        {
            Simulation.GrantItems(left.Id, 1);
            kept = left.Id;
        }

        if (food.Hunger > 0)
        {
            LogWorld(WorldEvents.MealEaten, null, NounOf(item));
        }

        if (food.Thirst > 0)
        {
            LogWorld(WorldEvents.WaterDrunk, null, NounOf(kept));
        }

        SyncNeedStages();
        return NeedActionResult.Done;
    }

    /// <summary>
    /// A drink by hand (E while aiming at water within reach: the river, the lake or the well): thirst rises by the data's amount, with nothing held and without a bottle. At full
    /// thirst nothing is drunk and a line says so. Raises <see cref="WorldEvents.WaterDrunk"/> with the place (<c>pozo</c>, <c>lago</c> or the river's noun). With the needs off the
    /// event is raised and the meter stays.
    /// </summary>
    public NeedActionResult DrinkByHand()
    {
        if (Simulation.InteractTarget is not { Id: BlockId.Water } hit || Needs.IsDead)
        {
            return NeedActionResult.Nothing;
        }

        if (NeedsEnabled && !Needs.Drink())
        {
            _hints.Add(Locale.Ui(DrinkKeptHint));
            return NeedActionResult.Kept;
        }

        LogWorld(WorldEvents.WaterDrunk, null, WaterPlaceAt(hit.Block));
        SyncNeedStages();
        return NeedActionResult.Done;
    }

    // The noun of an item for an event's detail: the catalogue's lemma, else the item's own key (as a pickup does).
    private string NounOf(ItemId item) => Nouns.ForItem(item)?.Lemma ?? ItemDefinition.Get(item).Name;

    // Where the water is: the well's label near the well's point, the lake's label near the lake's, else the river's noun.
    private string WaterPlaceAt(BlockPos water)
    {
        foreach (var (point, radius) in new[] { ("well", WellRadiusBlocks), ("lake", LakeRadiusBlocks) })
        {
            if (Scene.Points.TryGetValue(point, out var at))
            {
                long dx = at.X - water.X;
                long dz = at.Z - water.Z;
                if ((dx * dx) + (dz * dz) <= (long)radius * radius)
                {
                    return Scene.LabelOf(point);
                }
            }
        }

        return Data.Words.Noun("river");
    }

    // What the tick before it steps shows: whether the sprint counts (held, moving, needs allowing it), the speed and the gathering speed. Set on the simulation each tick.
    private bool PrepareTick(IReadOnlyList<VoxelCommand> commands)
    {
        var moving = commands.Any(static c => c.Kind is VoxelCommandKind.Move or VoxelCommandKind.MoveRelative);
        var sprinting = SprintHeld && moving && (!NeedsEnabled || (PlayActive && Needs.CanSprint));
        var stamina = Data.Needs.Stamina;
        var walk = NeedsEnabled ? Needs.Effects().WalkFactor : 1.0;
        Simulation.WalkPermille = (int)Math.Round(1000 * walk * (sprinting ? stamina.SprintSpeedFactor : 1.0));
        Simulation.GatherPermille = NeedsEnabled && PlayActive && Needs.Value(NeedMeter.Stamina) <= 0 ? (int)Math.Round(1000 * stamina.GatherSlowFactor) : 1000;
        return sprinting;
    }
}
