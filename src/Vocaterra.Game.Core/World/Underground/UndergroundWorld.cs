using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>
/// One world's underground, built once from the data and the seed (B11): the rock layers, the bedrock floor, the ores, the caves with their lava, and the entrance mask. The passes hold a reference and
/// read it; nothing here changes after construction.
/// </summary>
internal sealed class UndergroundWorld
{
    /// <summary>Builds the underground of a world seed.</summary>
    /// <param name="data">The data.</param>
    /// <param name="seed">The world seed.</param>
    /// <exception cref="ArgumentException">A block name of the data is not a block of the game.</exception>
    public UndergroundWorld(UndergroundData data, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
        Strata = new StrataTable(data.Strata, seed);
        Floor = new BedrockFloor(data.Floor, seed);
        Bedrock = BlockNamed(data.Floor.Block);
        Ores = new OreTable(data.Ores, seed);
        Lava = BlockNamed(data.Lava.Block);
        Caves = new CaveField(data.Caves, data.Lava.Level, seed);
        Entrances = new EntranceMask(data.Entrances, seed);
    }

    /// <summary>The data.</summary>
    public UndergroundData Data { get; }

    /// <summary>The rock layers.</summary>
    public StrataTable Strata { get; }

    /// <summary>The floor.</summary>
    public BedrockFloor Floor { get; }

    /// <summary>The floor's block.</summary>
    public BlockId Bedrock { get; }

    /// <summary>The ores.</summary>
    public OreTable Ores { get; }

    /// <summary>The lava's block.</summary>
    public BlockId Lava { get; }

    /// <summary>The caves.</summary>
    public CaveField Caves { get; }

    /// <summary>The entrance mask.</summary>
    public EntranceMask Entrances { get; }

    /// <summary>The highest y that holds lava.</summary>
    public int LavaLevel => Data.Lava.Level;

    /// <summary>How many columns round a chunk the generator must see final heights for: the water rule's reach and the entrances' reach.</summary>
    public int WaterReach => Math.Max(Data.Caves.WaterMargin, Data.Entrances.WaterReach);

    /// <summary>The block a name stands for.</summary>
    /// <param name="name">The block's name.</param>
    /// <exception cref="ArgumentException">The game has no such block.</exception>
    internal static BlockId BlockNamed(string name) =>
        BlockDefinition.FromName(name)?.Id ?? throw new ArgumentException($"game/underground.json names the block '{name}', which the game does not have.", nameof(name));
}
