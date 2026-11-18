using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// A kind of tree, by name (B03): the blocks of its wood and leaves, how long its trunk is and the crown it has when its row names none. A biome row's tree entry names the species; a new
/// species is a new strategy in the <see cref="StrategyRegistry"/> and a new name in a row. Implementations are immutable.
/// </summary>
public interface ITreeSpecies
{
    /// <summary>The species' name, as a biome row writes it.</summary>
    string Name { get; }

    /// <summary>The crown shape the species has when its row names none.</summary>
    string DefaultCrown { get; }

    /// <summary>The block of the trunk.</summary>
    BlockId Trunk { get; }

    /// <summary>The block of the leaves.</summary>
    BlockId Leaves { get; }

    /// <summary>The shortest trunk, in blocks.</summary>
    int MinTrunk { get; }

    /// <summary>The longest trunk, in blocks.</summary>
    int MaxTrunk { get; }
}
