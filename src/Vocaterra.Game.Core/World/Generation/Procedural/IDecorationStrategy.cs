using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Biomes;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>What a decoration strategy is asked about: one dry column whose top block is a soft cell short of a plant.</summary>
/// <param name="X">The block's x.</param>
/// <param name="Z">The block's z.</param>
/// <param name="Row">The column's biome.</param>
/// <param name="Ground">The block the plant would stand on.</param>
public readonly record struct DecorationColumn(int X, int Z, BiomeRow Row, BlockId Ground);

/// <summary>
/// How a kind of land is dressed (B03, D-game-34 item 13): the plants, and later a desert's cacti or a marsh's reeds. A biome row names a strategy as it names a tree species. The strategy
/// answers one question, which plant stands on a column, and the undergrowth pass puts it there; <see cref="StrategyRegistry.UndergrowthDecoration"/>, the default, sows the row's own
/// <c>decor</c> blocks by noise. Implementations are immutable, integer only and safe to call from any thread.
/// </summary>
public interface IDecorationStrategy
{
    /// <summary>The strategy's name, as a biome row writes it.</summary>
    string Name { get; }

    /// <summary>The block that stands on the column, or <see cref="BlockId.Air"/> for none.</summary>
    /// <param name="column">The column.</param>
    BlockId PlantAt(in DecorationColumn column);
}
