namespace Vocaterra.Game.Core.Land;

/// <summary>
/// Why a column is protected (Y09, <c>game/land.json</c>): the player may not place blocks (other than a torch) on it. The kind picks the hint the player reads; the
/// meaning of each is the game's, the value is stored in <see cref="LandRules"/>' mask.
/// </summary>
public enum Protection : byte
{
    /// <summary>The column is not protected.</summary>
    None = 0,

    /// <summary>The village: its disc, its blend and a margin.</summary>
    Village = 1,

    /// <summary>Luna's camp (<c>camp.guide</c>).</summary>
    Camp = 2,

    /// <summary>A road, a trail or the bridge, and a margin round them.</summary>
    Road = 3,
}

/// <summary>The names the land file gives the protections.</summary>
public static class ProtectionNames
{
    /// <summary>The protection a file names, or <see langword="null"/> when it is none of them.</summary>
    /// <param name="name"><c>village</c>, <c>camp</c> or <c>road</c>.</param>
    public static Protection? Parse(string? name) => name switch
    {
        "village" => Protection.Village,
        "camp" => Protection.Camp,
        "road" => Protection.Road,
        _ => null,
    };

    /// <summary>The file's word for a protection (the part of the hint's locale key as well: <c>landVillage</c>).</summary>
    /// <param name="protection">The protection.</param>
    public static string Of(Protection protection) => protection switch
    {
        Protection.Village => "village",
        Protection.Camp => "camp",
        Protection.Road => "road",
        _ => "none",
    };
}
