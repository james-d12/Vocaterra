namespace Vocaterra.Simulation.Generation;

/// <summary>
/// World generation: a pure function of the seed, the size and <see cref="Version"/>. The passes run in a fixed order and each draws from its
/// own streams (see <see cref="WorldRng.For"/>). Bump <see cref="Version"/> in the same commit that changes any pass, and update the golden.
/// </summary>
public static class WorldGenerator
{
    /// <summary>The generator version; saves store it next to the seed. Any change to a pass bumps it.</summary>
    public const int Version = 2;

    /// <summary>The smallest world side the passes support, in tiles.</summary>
    public const int MinimumSide = 128;

    /// <summary>
    /// The passes in the order they run. A later pass (caves, ores, vegetation) is inserted here; the two spawn passes stay last because they
    /// need the final tiles.
    /// </summary>
    internal static IReadOnlyList<IGenerationPass> DefaultPasses { get; } =
    [
        new TerrainPass(),
        new LayersPass(),
        new GrassPass(),
        new ForestPass(),
        new SpawnPass(),
        new SpawnTreePass(),
    ];

    /// <summary>The first row the terrain pass may put the surface on for a world of the given height.</summary>
    public static int MinSurfaceY(int worldHeight) => SurfaceBaseY(worldHeight) - SurfaceAmplitude(worldHeight);

    /// <summary>The last row the terrain pass may put the surface on for a world of the given height.</summary>
    public static int MaxSurfaceY(int worldHeight) => SurfaceBaseY(worldHeight) + SurfaceAmplitude(worldHeight);

    /// <summary>Generates a world of <see cref="WorldSize.Small"/> from the seed.</summary>
    public static World Generate(ulong seed) => Generate(seed, WorldSize.Small);

    /// <summary>Generates a world from the seed.</summary>
    /// <exception cref="ArgumentException">A side of <paramref name="size"/> is below <see cref="MinimumSide"/>.</exception>
    public static World Generate(ulong seed, WorldSize size) => Generate(seed, size, DefaultPasses);

    internal static int SurfaceBaseY(int worldHeight) => worldHeight * 45 / 128;

    internal static int SurfaceAmplitude(int worldHeight) => worldHeight / 20;

    internal static World Generate(ulong seed, WorldSize size, IReadOnlyList<IGenerationPass> passes)
    {
        if (size.Width < MinimumSide || size.Height < MinimumSide)
        {
            throw new ArgumentException($"A world needs at least {MinimumSide} tiles on each side.", nameof(size));
        }

        var context = new GenerationContext(seed, size);
        foreach (var pass in passes)
        {
            pass.Run(context);
            context.Record.AddPass(pass.Name);
        }

        var spawn = context.Spawn ?? throw new InvalidOperationException("The pass list has no spawn pass.");
        context.Tiles.ClearDirty();
        return new World(new WorldInfo(seed, size, Version), context.Tiles, spawn, context.Record);
    }
}
