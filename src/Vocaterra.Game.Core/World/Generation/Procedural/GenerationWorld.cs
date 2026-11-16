using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// One world loaded for generation (B03): its seed and data, the climate, the height model, the biome table and the strategies the rows name, all built once. The passes hold a reference and
/// read it; nothing here changes after construction.
/// </summary>
internal sealed class GenerationWorld
{
    /// <summary>
    /// The tallest wall beside water too shallow to swim that the edge pass cuts down to a step over the bed: the depth within which the exitable-edge scan looks for a bank's floor, so a wall any
    /// taller is a rock face with no floor to leave onto and any shorter is a bank to cut. (The valley cuts only a shoulder of 4: H12.)
    /// </summary>
    public const int BankCutReach = 12;

    /// <summary>How close two trunks may stand, squared (the trees' spacing).</summary>
    public const int TreeSpacingSquared = 9;

    public GenerationWorld(WorldGenData data, BiomeData biomes, ulong seed, StrategyRegistry strategies, int extraMargin, RegionBlend? blend = null, UndergroundData? underground = null)
    {
        Regions = blend;
        Underground = underground is null ? null : new UndergroundWorld(underground, seed);
        Data = data;
        Biomes = biomes;
        Seed = seed;
        Climate = blend is null ? new ClimateSampler(data, seed) : new BlendedClimateSampler(new ClimateSampler(data, seed), blend);
        Blended = blend is null ? null : new BlendedHeightModel(new HeightModel(data, seed), Climate, blend);
        Height = Blended is null ? new HeightModel(data, seed) : Blended;
        Table = new BiomeTable(biomes, data, seed);
        Strategies = strategies.Resolve(new StrategyContext(seed, data, biomes));
        SeaLevel = data.Vertical.SeaLevel;
        MinY = data.Vertical.MinY;
        MaxY = data.Vertical.MaxY;
        MaxFillerDepth = biomes.Rows.Max(static r => r.FillerDepth);
        HasSurfaceStrategies = biomes.Rows.Any(static r => r.SurfaceStrategy != StrategyRegistry.PlainSurface);
        Margin = 1 + Math.Max(Strategies.MaxCrownReach, Underground?.WaterReach ?? 0) + Strategies.MaxFade + extraMargin;
    }

    /// <summary>The world's numbers.</summary>
    public WorldGenData Data { get; }

    /// <summary>The biome rows.</summary>
    public BiomeData Biomes { get; }

    /// <summary>The world seed.</summary>
    public ulong Seed { get; }

    /// <summary>The climate.</summary>
    public IClimateSampler Climate { get; }

    /// <summary>The height model.</summary>
    public IHeightModel Height { get; }

    /// <summary>The height model with the regions' edges blended in, or <see langword="null"/> in a world with no region (it is <see cref="Height"/> when there is one).</summary>
    public BlendedHeightModel? Blended { get; }

    /// <summary>The regions' blend, or <see langword="null"/> in a world with no region: the caves read the regions' footprints and margins from it (B11).</summary>
    public RegionBlend? Regions { get; }

    /// <summary>The underground (rock layers, floor, ores, caves, lava, entrances), or <see langword="null"/> for a world whose ground is plain stone (B11).</summary>
    public UndergroundWorld? Underground { get; }

    /// <summary>The biome table.</summary>
    public BiomeTable Table { get; }

    /// <summary>The strategies and trees of every row.</summary>
    public ResolvedStrategies Strategies { get; }

    /// <summary>The water's surface y.</summary>
    public int SeaLevel { get; }

    /// <summary>The lowest block row of the world.</summary>
    public int MinY { get; }

    /// <summary>One above the highest block row of the world.</summary>
    public int MaxY { get; }

    /// <summary>The most filler layers any row has under its top layer.</summary>
    public int MaxFillerDepth { get; }

    /// <summary>Whether any row names a surface strategy other than the plain one.</summary>
    public bool HasSurfaceStrategies { get; }

    /// <summary>
    /// How many columns beyond a chunk's own 16 the generator looks at. A crown reaches in from a trunk <see cref="ResolvedStrategies.MaxCrownReach"/> columns outside the chunk; the trunk's ground
    /// is the ground after the water's edge was cut (one column more), and after the surface strategies faded at the biome's border (their widest fade more). The underground reads final heights as far out as its
    /// water rule and its entrances reach (B11), so the margin is at least that and the fade and the cut beyond it.
    /// </summary>
    public int Margin { get; }
}
