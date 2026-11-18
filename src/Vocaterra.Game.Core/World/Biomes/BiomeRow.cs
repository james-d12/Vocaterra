namespace Vocaterra.Game.Core.World.Biomes;

/// <summary>A closed range of a number, open at an end that is <see cref="int.MinValue"/> or <see cref="int.MaxValue"/>: one condition of a biome row.</summary>
/// <param name="Min">The lowest value that matches (inclusive).</param>
/// <param name="Max">The highest value that matches (inclusive).</param>
public readonly record struct IntRange(int Min, int Max)
{
    /// <summary>The range that holds every number.</summary>
    public static IntRange Any { get; } = new(int.MinValue, int.MaxValue);

    /// <summary>Whether the range is open at both ends.</summary>
    public bool IsAny => Min == int.MinValue && Max == int.MaxValue;

    /// <summary>Whether the value lies in the range.</summary>
    /// <param name="value">The value.</param>
    public bool Contains(int value) => value >= Min && value <= Max;
}

/// <summary>
/// What a biome row asks of a column (<c>when</c> in <c>game/biomes.json</c>). Every part is optional; a row matches when all its parts do. Field values are thousandths of the
/// field's range, heights are blocks.
/// </summary>
/// <param name="Water">Whether the ground is below the sea (so water covers the column): <see langword="true"/> for water only, <see langword="false"/> for land only, <see langword="null"/> for either.</param>
/// <param name="Continentalness">The continentalness.</param>
/// <param name="Erosion">The erosion.</param>
/// <param name="Ridges">The ridges.</param>
/// <param name="Temperature">The sea-level temperature (after the border jitter).</param>
/// <param name="Humidity">The humidity (after the border jitter).</param>
/// <param name="River">The absolute value of the river field: 0 on a river's centre line.</param>
/// <param name="Height">The surface height in blocks.</param>
/// <param name="SurfaceTemperature">The temperature at the surface height (the lapse of the altitude applied): what snow and trees follow.</param>
public sealed record BiomeConditions(
    bool? Water,
    IntRange Continentalness,
    IntRange Erosion,
    IntRange Ridges,
    IntRange Temperature,
    IntRange Humidity,
    IntRange River,
    IntRange Height,
    IntRange SurfaceTemperature)
{
    /// <summary>The conditions of a catch-all row: none.</summary>
    public static BiomeConditions None { get; } = new(null, IntRange.Any, IntRange.Any, IntRange.Any, IntRange.Any, IntRange.Any, IntRange.Any, IntRange.Any, IntRange.Any);

    /// <summary>Whether the row asks nothing: it matches every column.</summary>
    public bool IsNone => this == None || (Water is null && Continentalness.IsAny && Erosion.IsAny && Ridges.IsAny && Temperature.IsAny && Humidity.IsAny && River.IsAny && Height.IsAny && SurfaceTemperature.IsAny);
}

/// <summary>One kind of tree a biome grows, by the name of a code strategy.</summary>
/// <param name="Species">The species' name (<see cref="Generation.Procedural.StrategyRegistry.SpeciesNames"/>).</param>
/// <param name="Weight">Its share among the biome's species (positive).</param>
/// <param name="Crown">The crown shape's name (<see cref="Generation.Procedural.StrategyRegistry.CrownNames"/>), or <see langword="null"/> for the species' own.</param>
public sealed record TreeEntry(string Species, int Weight, string? Crown);

/// <summary>The trees of a biome.</summary>
/// <param name="Density">The percent of the 4 x 4 cells of a chunk that hold a tree, 0 to 100.</param>
/// <param name="Species">The species to draw from, by weight.</param>
public sealed record TreeRule(int Density, IReadOnlyList<TreeEntry> Species)
{
    /// <summary>No trees.</summary>
    public static TreeRule None { get; } = new(0, []);
}

/// <summary>One block of undergrowth a biome sows.</summary>
/// <param name="Block">The block's name (<see cref="Voxel.BlockDefinition.Name"/>).</param>
/// <param name="Weight">Its share among the biome's undergrowth (positive).</param>
public sealed record DecorEntry(string Block, int Weight);

/// <summary>The undergrowth of a biome.</summary>
/// <param name="Density">The thousandths of the columns that hold a plant, 0 to 1 000.</param>
/// <param name="Blocks">The plants to draw from, by weight.</param>
public sealed record DecorRule(int Density, IReadOnlyList<DecorEntry> Blocks)
{
    /// <summary>No undergrowth.</summary>
    public static DecorRule None { get; } = new(0, []);
}

/// <summary>How much one ambient sound bed is heard in a biome (B14): a bed named by its id and a weight in thousandths.</summary>
/// <param name="Bed">The bed's id (one of <see cref="Sound.AmbientBeds.ByPlace"/>).</param>
/// <param name="Weight">How loud it is at the heart of the biome, 0 to 1 000 thousandths of the bed's full weight.</param>
public sealed record BedShare(string Bed, int Weight);

/// <summary>
/// One row of <c>game/biomes.json</c> (B01, D-game-34): a biome is data, never an enum. The table tries the rows in order and the first whose conditions hold wins; what the row
/// carries is what the surface passes and the decorators read (the ground's blocks, the trees and the undergrowth), by name.
/// </summary>
/// <param name="Index">The row's place in the file: consumers index their own arrays by it.</param>
/// <param name="Id">The biome's id (lowercase kebab-case, unique).</param>
/// <param name="When">What the row asks of a column.</param>
/// <param name="Top">The block name of the top layer of the ground.</param>
/// <param name="Filler">The block name of the layers under it.</param>
/// <param name="FillerDepth">How many filler layers lie under the top one.</param>
/// <param name="Underwater">The block name that replaces the top layer under water, or <see langword="null"/> to keep <paramref name="Top"/>.</param>
/// <param name="Trees">The trees.</param>
/// <param name="Decor">The undergrowth.</param>
/// <param name="SnowCap">Whether snow covers the top where the surface temperature is below the snow threshold.</param>
/// <param name="BaseTemperature">An optional base temperature of the biome in thousandths, for the temperature system to come (data only: nothing reads it yet).</param>
/// <param name="SurfaceStrategy">The name of the code strategy that shapes the biome's own ground (B03, D-game-34 item 13), a name of the strategy and never a biome id.</param>
/// <param name="DecorStrategy">The name of the code strategy that dresses the biome's ground with plants.</param>
/// <param name="Ambient">The ambient sound beds heard in the biome (B14, <c>ambient</c> in <c>game/biomes.json</c>), or <see langword="null"/> for none: the sound reads these and never the biome's id.</param>
public sealed record BiomeRow(
    int Index,
    string Id,
    BiomeConditions When,
    string Top,
    string Filler,
    int FillerDepth,
    string? Underwater,
    TreeRule Trees,
    DecorRule Decor,
    bool SnowCap,
    int? BaseTemperature,
    string SurfaceStrategy = Generation.Procedural.StrategyRegistry.PlainSurface,
    string DecorStrategy = Generation.Procedural.StrategyRegistry.UndergrowthDecoration,
    IReadOnlyList<BedShare>? Ambient = null);
