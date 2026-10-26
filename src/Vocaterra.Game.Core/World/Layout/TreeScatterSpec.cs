namespace Vocaterra.Game.Core.World.Layout;

/// <summary>
/// The forest: one tree is tried in every <paramref name="Cell"/> x <paramref name="Cell"/> square of the ground, at a jittered position, and kept
/// with a chance of <paramref name="Chance"/> percent (less on higher ground) unless it is too near another trunk or the route.
/// </summary>
/// <param name="Cell">The side of the square one tree is tried in.</param>
/// <param name="Chance">The percent chance a tree is kept on the valley floor.</param>
/// <param name="Spacing">The least Chebyshev distance between two trunks.</param>
/// <param name="Margin">The least Chebyshev distance between a trunk and any route cell (paths, sites' level ground, props, spawn, NPCs and points). The canopy reaches 2, so a margin of 3 keeps every leaf off the route.</param>
/// <param name="MinTrunk">The shortest trunk in blocks.</param>
/// <param name="MaxTrunk">The longest trunk in blocks.</param>
/// <param name="TreeLine">The height above which no tree grows.</param>
/// <param name="SpruceFrom">S14: the ground height where spruces start to take over from oaks; the share of spruces rises over the next 20 blocks to all of them.</param>
/// <param name="Birch">S14: the percent of the lowland trees that are birches (none within <paramref name="BirchAway"/> blocks of a site).</param>
/// <param name="BirchAway">S14: the distance from every site with a radius inside which no birch grows (the guide's clearing keeps its oaks).</param>
/// <param name="ShoreClear">S14: the distance from the water (the river, the pool, the lake) inside which the forest keeps only <paramref name="ShoreKeep"/> percent of its trees, so the banks are meadow.</param>
/// <param name="ShoreFade">S14: how many more blocks the forest takes to come back to full density beyond <paramref name="ShoreClear"/>.</param>
/// <param name="ShoreKeep">S14: the percent of trees kept inside the shore clearing.</param>
/// <param name="Clearings">S14: round meadows round a site.</param>
/// <param name="Vistas">S14: sight cones kept clear of the trees that would hide the valley.</param>
public sealed record TreeScatterSpec(
    int Cell,
    int Chance,
    int Spacing,
    int Margin,
    int MinTrunk,
    int MaxTrunk,
    int TreeLine,
    int SpruceFrom = 48,
    int Birch = 10,
    int BirchAway = 30,
    int ShoreClear = 0,
    int ShoreFade = 1,
    int ShoreKeep = 100,
    IReadOnlyList<ClearingSpec>? Clearings = null,
    IReadOnlyList<VistaSpec>? Vistas = null)
{
    /// <summary>The meadows round sites (empty when the layout names none).</summary>
    public IReadOnlyList<ClearingSpec> ClearingList => Clearings ?? [];

    /// <summary>The sight cones (empty when the layout names none).</summary>
    public IReadOnlyList<VistaSpec> VistaList => Vistas ?? [];
}

