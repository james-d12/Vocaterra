using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Session;

// Planting a berry at the camp (N08, adaptation design decision 14): the berry the player holds goes on grass or dirt inside the radius of the camp's fire ring (Luna's, or from Y12 the
// player's own plot: the plot has its own count of bushes), and the cell above
// becomes a plain bush recorded as picked, so the night grows it into a berry bush through N06's rule. The rule is needs.json's planting; with the needs off nothing is planted.
public sealed partial class VoxelSession
{
    /// <summary>What a planting attempt did: <see cref="NotApplied"/> leaves the click to the other uses (the eating); the other two take it, the refusal with a line.</summary>
    public enum PlantResult
    {
        /// <summary>Not planted and not refused: the needs are off, the file has no planting, or the player is dead.</summary>
        NotApplied,
        /// <summary>The berry was planted.</summary>
        Planted,
        /// <summary>The berry was kept: the cell is outside the camp's radius, occupied, or the camp has its most planted bushes.</summary>
        Refused,
    }

    // The ui keys of the lines (content/locale/en/ui.json).
    private const string PlantedHint = "plantedBush";
    private const string PlantFarHint = "plantFar";
    private const string PlantOccupiedHint = "plantOccupied";
    private const string PlantFullHint = "plantFull";

    // The cells of the bushes planted here, in the order they were planted; a cell counts while it still holds a bush of the rule.
    private readonly List<BlockPos> _planted = [];

    /// <summary>The bushes planted at the camp that still stand (plain or berry bushes), in the order they were planted.</summary>
    public IReadOnlyList<BlockPos> PlantedBushes => _planted.Where(PlantedStands).ToList();

    /// <summary>How many planted bushes stand: the camp allows the rule's <c>maxBushes</c> at once.</summary>
    public int PlantedAlive => _planted.Count(PlantedStands);

    /// <summary>How many planted bushes stand on the owned plot (<paramref name="onPlot"/>) or at Luna's camp: each place allows the rule's <c>maxBushes</c> at once (Y12).</summary>
    /// <param name="onPlot">Whether to count the bushes on the owned plot rather than those at Luna's camp.</param>
    public int PlantedAliveOn(bool onPlot) => _planted.Count(c => PlantedStands(c) && IsOwned(c.X, c.Z) == onPlot);

    /// <summary>
    /// Plants the berry the player holds on the ground block the crosshair is on (a right click on grass or dirt; N08): the berry leaves the bag, the cell above becomes a
    /// plain bush and is recorded as picked. A cell outside the camp's radius, one with something on it, or the camp at its most planted bushes is refused with a line.
    /// </summary>
    /// <param name="ground">The grass or dirt block.</param>
    public PlantResult PlantOn(BlockPos ground)
    {
        if (!NeedsEnabled || Data.Needs.Planting is not { } rule || Needs.IsDead || !Scene.Grid.InBounds(ground.X, ground.Y, ground.Z))
        {
            return PlantResult.NotApplied;
        }

        var item = Data.Items.Find(rule.Item);
        var bush = BlockDefinition.FromName(rule.Block);
        if (item is null || bush is null || Simulation.Inventory.Count(item.Id) < 1 || Scene.Grid.GetBlock(ground.X, ground.Y, ground.Z) is not (BlockId.Grass or BlockId.Dirt))
        {
            return PlantResult.NotApplied;
        }

        var cell = ground.Offset(BlockFace.Up);
        var onPlot = IsOwned(ground.X, ground.Z); // Y12: the owned plot takes bushes as well, apart from Luna's camp (its columns are protected, the plot never overlaps them)
        if (!onPlot && !InPlantingRadius(rule, ground))
        {
            _hints.Add(Locale.Ui(PlantFarHint));
            return PlantResult.Refused;
        }

        var above = Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z);
        if (!Scene.Grid.InBounds(cell.X, cell.Y, cell.Z) || (above != BlockId.Air && !BlockDefinition.IsDecor(above)))
        {
            _hints.Add(Locale.Ui(PlantOccupiedHint));
            return PlantResult.Refused;
        }

        if (PlantedAliveOn(onPlot) >= rule.MaxBushes)
        {
            _hints.Add(Locale.Ui(PlantFullHint));
            return PlantResult.Refused;
        }

        Simulation.TakeItems(item.Id, 1);
        Scene.Grid.SetBlock(cell.X, cell.Y, cell.Z, bush.Id);
        if (!_picked.Contains(cell))
        {
            _picked.Add(cell); // the night grows it back, as a picked berry bush (N06)
        }

        _planted.Add(cell);
        _hints.Add(Locale.Ui(PlantedHint));
        LogWorld(WorldEvents.BushPlanted, null, NounOf(item.Id));
        return PlantResult.Planted;
    }

    // Inside the radius of the camp's fire ring, horizontally; a scene with no camp ring has no planting.
    private bool InPlantingRadius(PlantingRule rule, BlockPos ground)
    {
        if (!Scene.Points.TryGetValue(CampFireRing, out var camp))
        {
            return false;
        }

        long dx = camp.X - ground.X;
        long dz = camp.Z - ground.Z;
        return (dx * dx) + (dz * dz) <= (long)rule.RadiusBlocks * rule.RadiusBlocks;
    }

    // A planted cell stands while it holds the planted block or the grown one.
    private bool PlantedStands(BlockPos cell)
    {
        if (Data.Needs.Planting is not { } rule || !Scene.Grid.InBounds(cell.X, cell.Y, cell.Z))
        {
            return false;
        }

        var id = Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z);
        return id == BlockDefinition.FromName(rule.Block)?.Id || id == BlockDefinition.FromName(rule.GrownBlock)?.Id;
    }

    /// <summary>The planted bushes that stand, for the save (N08), or <see langword="null"/> for none.</summary>
    internal BushSave[]? CapturePlanted() => PlantedBushes.Count == 0 ? null : PlantedBushes.Select(static c => new BushSave(c.X, c.Y, c.Z)).ToArray();

    // A save's planted bushes back in: a cell that no longer holds a bush of the rule is let go.
    internal void RestorePlanted(BushSave[]? bushes)
    {
        _planted.Clear();
        foreach (var bush in bushes ?? [])
        {
            var cell = new BlockPos(bush.X, bush.Y, bush.Z);
            if (PlantedStands(cell) && !_planted.Contains(cell))
            {
                _planted.Add(cell);
            }
        }
    }
}
