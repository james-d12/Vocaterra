using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.World;

/// <summary>
/// The composition root of the world (B06, biomes plan section 3.2): it replaces the game's direct call to <see cref="SceneBuilder.Build"/>. The valley is always built by the unchanged scene
/// builder and frozen into a region; what differs is the grid. <see cref="WorldMode.Bounded"/> returns the scene as it is today (one dense grid), with the world's place query and the region
/// template's digest added, which the state digest then starts with. <see cref="WorldMode.Streamed"/> returns the same scene on a sparse grid whose generator is the composite source: inside
/// the valley, block for block what the bounded grid holds; everywhere else, the procedural land eased into the valley's edge.
/// </summary>
public static class WorldFactory
{
    /// <summary>The name of the valley's region.</summary>
    public const string ValleyRegionId = "valley";

    /// <summary>Makes a world.</summary>
    /// <param name="data">The game's content.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="mode">The kind of grid.</param>
    /// <param name="layout">The valley's layout when it is not the bundle's own (the game's <c>--scene</c>), or <see langword="null"/>.</param>
    /// <returns>The scene: the valley's spawn, NPCs, points and labels on the grid of the mode, with <see cref="Scene.Places"/> and <see cref="Scene.TemplateDigest"/> set.</returns>
    public static Scene Create(GameData data, ulong seed, WorldMode mode, SceneLayout? layout = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var built = SceneBuilder.Build(layout ?? data.Scene, seed);
        var region = AuthoredReliefRegion.Freeze(ValleyRegionId, built, keepChunks: mode == WorldMode.Streamed);
        var composition = Compose(data, seed, new RegionCatalog([region]));
        if (mode == WorldMode.Bounded)
        {
            return built.WithWorld(built.Grid, composition.Places, region.TemplateDigest, null);
        }

        return built.WithWorld(StreamedGrid(data, composition.Source), composition.Places, region.TemplateDigest, composition.Source);
    }

    /// <summary>The world's pure parts for some regions: the composite source and the place query, both over one blend and one seed.</summary>
    /// <param name="data">The game's content.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="regions">The regions (the tests' own as well as the valley).</param>
    /// <param name="strategies">The code strategies the biome rows may name, or <see langword="null"/> for the defaults.</param>
    internal static WorldComposition Compose(GameData data, ulong seed, RegionCatalog regions, StrategyRegistry? strategies = null)
    {
        var blend = new RegionBlend(regions, data.WorldGen.Blend, data.WorldGen.Vertical.SeaLevel);
        var procedural = new ProceduralChunkSource(data.WorldGen, data.Biomes, seed, strategies, 0, blend);
        return new WorldComposition(new CompositeChunkSource(regions, procedural), new WorldQuery(procedural.World, regions), procedural);
    }

    /// <summary>A streamed grid over the world's limits (x and z plus and minus 30 000, the data's vertical range) that generates what is read from a source.</summary>
    /// <param name="data">The game's content.</param>
    /// <param name="source">The source.</param>
    internal static VoxelGrid StreamedGrid(GameData data, IChunkSource source)
    {
        var vertical = data.WorldGen.Vertical;
        return new VoxelGrid(WorldLimits.Symmetric(WorldLimits.DefaultHalfExtent, vertical.MinY, vertical.MaxY), new ChunkSourceGenerator(source));
    }

    /// <summary>
    /// Installs a whole chunk column of a streamed grid from one window of the source (the cheap way, about 1.6 ms for 24 layers), skipping the chunks that are already resident.
    /// </summary>
    /// <param name="grid">The streamed grid.</param>
    /// <param name="source">The composite source.</param>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <returns>How many chunks were installed.</returns>
    internal static int InstallColumn(VoxelGrid grid, CompositeChunkSource source, int chunkX, int chunkZ)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(source);
        var installed = 0;
        foreach (var chunk in source.GenerateColumn(chunkX, chunkZ))
        {
            if (grid.Limits.Contains(chunk.Coord) && grid.InstallChunk(chunk.Coord, ChunkSourceGenerator.ContentOf(chunk)))
            {
                installed++;
            }
        }

        return installed;
    }

    /// <summary>Hands a grid what a pure source made, as installable content (<see langword="null"/> for air).</summary>
    private sealed class ChunkSourceGenerator(IChunkSource source) : IChunkGenerator
    {
        public ChunkContent? Generate(VoxelChunkCoord coord) => ContentOf(source.Generate(coord));

        public static ChunkContent? ContentOf(ChunkData data) =>
            data.IsEmpty ? null : new ChunkContent(data.Blocks.ToArray(), data.HasWaterLevels ? data.WaterLevels.ToArray() : null);
    }
}

/// <summary>The pure parts of one composed world (<see cref="WorldFactory.Compose"/>).</summary>
/// <param name="Source">The composite chunk source.</param>
/// <param name="Places">The place query.</param>
/// <param name="Procedural">The procedural half of the source, blended into the regions.</param>
internal sealed record WorldComposition(CompositeChunkSource Source, WorldQuery Places, ProceduralChunkSource Procedural);
