using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>What a surface strategy is asked about: one column of its biome, before the strategy has changed anything.</summary>
/// <param name="X">The block's x.</param>
/// <param name="Z">The block's z.</param>
/// <param name="Height">The height model's ground at the column, in blocks.</param>
/// <param name="Climate">The column's climate.</param>
/// <param name="Row">The column's biome.</param>
public readonly record struct SurfaceColumn(int X, int Z, int Height, ClimateSample Climate, BiomeRow Row);

/// <summary>
/// How a kind of land shapes its own ground (B03, D-game-34 item 13): dunes in a desert, crags on a range. A biome row names a strategy, as it names a tree species, and the strategy is code:
/// the data holds only the name. The strategy has a name of its own (never a biome id), is registered in a <see cref="StrategyRegistry"/>, and asks for a change of height at full strength
/// in the middle of its biome; the pipeline fades that change out over <see cref="FadeWidth"/> columns towards the biome's border, so a border never makes a cliff. Implementations are
/// immutable, integer only and safe to call from any thread.
/// </summary>
public interface ISurfaceStrategy
{
    /// <summary>The strategy's name, as a biome row writes it.</summary>
    string Name { get; }

    /// <summary>How many columns inside the biome's border the change fades in over (0 for a strategy that never changes the height).</summary>
    int FadeWidth { get; }

    /// <summary>The change of the ground at the column, in blocks (negative lowers it), at full strength.</summary>
    /// <param name="column">The column.</param>
    int HeightDelta(in SurfaceColumn column);
}
