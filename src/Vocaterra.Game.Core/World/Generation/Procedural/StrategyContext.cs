using Vocaterra.Game.Core.World.Biomes;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>What a strategy is built from when a world is loaded (B03): the seed that keys its noise, and the data it may read.</summary>
/// <param name="Seed">The world seed.</param>
/// <param name="World">The world's numbers.</param>
/// <param name="Biomes">The biome rows.</param>
public readonly record struct StrategyContext(ulong Seed, WorldGenData World, BiomeData Biomes);
