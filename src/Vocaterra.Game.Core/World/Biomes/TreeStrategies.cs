namespace Vocaterra.Game.Core.World.Biomes;

/// <summary>
/// The tree species and crown shapes the code knows (B01): a biome row names them and the decorators of B03 build them. They are code strategies, so the data holds only their names;
/// a new species is a new strategy here and a new name in a row.
/// </summary>
public static class TreeStrategies
{
    private static readonly string[] SpeciesNames = ["oak", "spruce", "birch"];
    private static readonly string[] CrownNames = ["round", "small-round", "narrow", "cone"];

    /// <summary>The species' names.</summary>
    public static IReadOnlyList<string> Species => SpeciesNames;

    /// <summary>The crown shapes' names.</summary>
    public static IReadOnlyList<string> Crowns => CrownNames;

    /// <summary>The crown a species has when its row names none, or <see langword="null"/> for an unknown species.</summary>
    /// <param name="species">The species' name.</param>
    public static string? DefaultCrown(string species) => species switch
    {
        "oak" => "round",
        "birch" => "narrow",
        "spruce" => "cone",
        _ => null,
    };
}
