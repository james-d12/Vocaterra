using Vocaterra.Game.Core.World.Biomes;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The code strategies a biome row may name (B03, D-game-34 item 13; the owner: "the data should just be for basic stuff like the types of biomes, but the biome generation code might be
/// different"). A row names a surface strategy, a decoration strategy, and tree species and crown shapes; this registry maps each name to the code. It is immutable: <c>With...</c>
/// returns a registry with one more, so a test or a later milestone adds a strategy with no change to the pipeline. The names are checked by <see cref="BiomeValidator"/> (an unknown one is a
/// validator problem) and resolved once when a world is loaded (<see cref="Resolve"/>), never per column. Every registered strategy has a name of its own, never a biome id.
/// </summary>
public sealed class StrategyRegistry
{
    /// <summary>The surface strategy of a row that names none: the ground as the height model made it.</summary>
    public const string PlainSurface = "plain";

    /// <summary>The decoration strategy of a row that names none: its own <c>decor</c> blocks, sown by noise.</summary>
    public const string UndergrowthDecoration = "undergrowth";

    private readonly Dictionary<string, Func<StrategyContext, ISurfaceStrategy>> _surfaces;
    private readonly Dictionary<string, Func<StrategyContext, IDecorationStrategy>> _decorations;
    private readonly Dictionary<string, ITreeSpecies> _species;
    private readonly Dictionary<string, ICrownShape> _crowns;

    private StrategyRegistry(
        Dictionary<string, Func<StrategyContext, ISurfaceStrategy>> surfaces,
        Dictionary<string, Func<StrategyContext, IDecorationStrategy>> decorations,
        Dictionary<string, ITreeSpecies> species,
        Dictionary<string, ICrownShape> crowns)
    {
        _surfaces = surfaces;
        _decorations = decorations;
        _species = species;
        _crowns = crowns;
    }

    /// <summary>The strategies the code ships: the plain surface, the undergrowth, the oak, spruce and birch, and the round, small-round, narrow and cone crowns.</summary>
    public static StrategyRegistry Default { get; } = new(
        new(StringComparer.Ordinal) { [PlainSurface] = static _ => new PlainSurface() },
        new(StringComparer.Ordinal) { [UndergrowthDecoration] = static c => new UndergrowthStrategy(c) },
        StandardTrees.Species.ToDictionary(static s => s.Name, StringComparer.Ordinal),
        StandardTrees.Crowns.ToDictionary(static c => c.Name, StringComparer.Ordinal));

    /// <summary>The surface strategies' names, in a fixed order.</summary>
    public IReadOnlyList<string> SurfaceNames => [.. _surfaces.Keys.Order(StringComparer.Ordinal)];

    /// <summary>The decoration strategies' names, in a fixed order.</summary>
    public IReadOnlyList<string> DecorationNames => [.. _decorations.Keys.Order(StringComparer.Ordinal)];

    /// <summary>The tree species' names, in a fixed order.</summary>
    public IReadOnlyList<string> SpeciesNames => [.. _species.Keys.Order(StringComparer.Ordinal)];

    /// <summary>The crown shapes' names, in a fixed order.</summary>
    public IReadOnlyList<string> CrownNames => [.. _crowns.Keys.Order(StringComparer.Ordinal)];

    /// <summary>A registry with one more surface strategy.</summary>
    /// <param name="name">The name a row writes (lowercase kebab-case).</param>
    /// <param name="create">Builds the strategy of one world (its seed and data).</param>
    /// <exception cref="ArgumentException">The name is taken.</exception>
    public StrategyRegistry WithSurface(string name, Func<StrategyContext, ISurfaceStrategy> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        return new(Added(_surfaces, name, create), _decorations, _species, _crowns);
    }

    /// <summary>A registry with one more decoration strategy.</summary>
    /// <param name="name">The name a row writes.</param>
    /// <param name="create">Builds the strategy of one world.</param>
    /// <exception cref="ArgumentException">The name is taken.</exception>
    public StrategyRegistry WithDecoration(string name, Func<StrategyContext, IDecorationStrategy> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        return new(_surfaces, Added(_decorations, name, create), _species, _crowns);
    }

    /// <summary>A registry with one more tree species.</summary>
    /// <param name="species">The species.</param>
    /// <exception cref="ArgumentException">The name is taken.</exception>
    public StrategyRegistry WithSpecies(ITreeSpecies species)
    {
        ArgumentNullException.ThrowIfNull(species);
        return new(_surfaces, _decorations, Added(_species, species.Name, species), _crowns);
    }

    /// <summary>A registry with one more crown shape.</summary>
    /// <param name="crown">The shape.</param>
    /// <exception cref="ArgumentException">The name is taken.</exception>
    public StrategyRegistry WithCrown(ICrownShape crown)
    {
        ArgumentNullException.ThrowIfNull(crown);
        return new(_surfaces, _decorations, _species, Added(_crowns, crown.Name, crown));
    }

    /// <summary>The species with this name, or <see langword="null"/>.</summary>
    /// <param name="name">The name.</param>
    public ITreeSpecies? Species(string name) => _species.GetValueOrDefault(name);

    /// <summary>The crown with this name, or <see langword="null"/>.</summary>
    /// <param name="name">The name.</param>
    public ICrownShape? Crown(string name) => _crowns.GetValueOrDefault(name);

    /// <summary>
    /// Looks up every name the rows use and builds what they name for one world, once. The result holds, row by row, the surface and decoration strategy and the trees, so no name is
    /// looked up while chunks are made.
    /// </summary>
    /// <param name="context">The world's seed and data.</param>
    /// <exception cref="ArgumentException">A row names a strategy, species or crown the registry does not have (the validator reports the same).</exception>
    internal ResolvedStrategies Resolve(in StrategyContext context)
    {
        var rows = context.Biomes.Rows;
        var surfaces = new Dictionary<string, ISurfaceStrategy>(StringComparer.Ordinal);
        var decorations = new Dictionary<string, IDecorationStrategy>(StringComparer.Ordinal);
        var surface = new ISurfaceStrategy[rows.Count];
        var decoration = new IDecorationStrategy[rows.Count];
        var trees = new ResolvedTrees[rows.Count];
        foreach (var row in rows)
        {
            surface[row.Index] = Built(surfaces, _surfaces, row.SurfaceStrategy, "surface strategy", row, context);
            decoration[row.Index] = Built(decorations, _decorations, row.DecorStrategy, "decoration strategy", row, context);
            var entries = new List<ResolvedTrees.Entry>();
            foreach (var tree in row.Trees.Species)
            {
                var species = Species(tree.Species) ?? throw Unknown("tree species", tree.Species, row, SpeciesNames);
                var crown = Crown(tree.Crown ?? species.DefaultCrown) ?? throw Unknown("crown", tree.Crown ?? species.DefaultCrown, row, CrownNames);
                entries.Add(new ResolvedTrees.Entry(species, crown, tree.Weight));
            }

            trees[row.Index] = new ResolvedTrees(row.Trees.Density, [.. entries]);
        }

        return new ResolvedStrategies(surface, decoration, trees);
    }

    private static T Built<T>(Dictionary<string, T> made, Dictionary<string, Func<StrategyContext, T>> known, string name, string what, BiomeRow row, in StrategyContext context)
    {
        if (made.TryGetValue(name, out var strategy))
        {
            return strategy;
        }

        if (!known.TryGetValue(name, out var create))
        {
            throw Unknown(what, name, row, [.. known.Keys.Order(StringComparer.Ordinal)]);
        }

        return made[name] = create(context);
    }

    private static ArgumentException Unknown(string what, string name, BiomeRow row, IReadOnlyList<string> known) =>
        new($"Biome '{row.Id}' names the unknown {what} '{name}' (known: {string.Join(", ", known)}).");

    private static Dictionary<string, T> Added<T>(Dictionary<string, T> source, string name, T value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (source.ContainsKey(name))
        {
            throw new ArgumentException($"The strategy '{name}' is already registered.", nameof(name));
        }

        return new Dictionary<string, T>(source, StringComparer.Ordinal) { [name] = value };
    }
}

/// <summary>The trees of one biome row with their names looked up: how many cells hold one, and the species to draw from by weight.</summary>
internal sealed class ResolvedTrees(int density, ResolvedTrees.Entry[] entries)
{
    /// <summary>A species with its crown and weight.</summary>
    public readonly record struct Entry(ITreeSpecies Species, ICrownShape Crown, int Weight);

    /// <summary>The percent of the cells that hold a tree.</summary>
    public int Density { get; } = density;

    /// <summary>The species to draw from.</summary>
    public Entry[] Entries { get; } = entries;

    /// <summary>The sum of the weights.</summary>
    public int TotalWeight { get; } = entries.Sum(static e => e.Weight);
}

/// <summary>What <see cref="StrategyRegistry.Resolve"/> hands the pipeline: the strategies and trees of every biome row, by the row's index.</summary>
internal sealed class ResolvedStrategies(ISurfaceStrategy[] surface, IDecorationStrategy[] decoration, ResolvedTrees[] trees)
{
    /// <summary>The widest fade any row's surface strategy asks for, in columns.</summary>
    public int MaxFade { get; } = surface.Length == 0 ? 0 : surface.Max(static s => s.FadeWidth);

    /// <summary>The farthest any crown a row grows reaches sideways, in columns: how far a chunk's neighbours reach into it.</summary>
    public int MaxCrownReach { get; } = trees.SelectMany(static t => t.Entries).Select(static e => e.Crown.Reach).DefaultIfEmpty(0).Max();

    /// <summary>The tallest a tree of any row can stand over its ground: its longest trunk plus the highest rise of its crown.</summary>
    public int MaxTreeHeight { get; } = trees.SelectMany(static t => t.Entries).Select(static e => e.Species.MaxTrunk + e.Crown.Rise).DefaultIfEmpty(0).Max();

    /// <summary>The surface strategy of each row.</summary>
    public ISurfaceStrategy[] Surface { get; } = surface;

    /// <summary>The decoration strategy of each row.</summary>
    public IDecorationStrategy[] Decoration { get; } = decoration;

    /// <summary>The trees of each row.</summary>
    public ResolvedTrees[] Trees { get; } = trees;
}
