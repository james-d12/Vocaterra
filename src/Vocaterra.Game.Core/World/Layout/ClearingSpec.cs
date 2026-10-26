namespace Vocaterra.Game.Core.World.Layout;

/// <summary>A meadow round a site (S14): inside <paramref name="Radius"/> the forest keeps only <paramref name="Keep"/> percent of its trees, and it grows back to full density over <paramref name="Fade"/> more blocks.</summary>
/// <param name="Site">The site the meadow is round.</param>
/// <param name="Radius">The meadow's radius in blocks.</param>
/// <param name="Fade">The width of the edge where the forest comes back.</param>
/// <param name="Keep">The percent of trees kept inside.</param>
public sealed record ClearingSpec(string Site, int Radius, int Fade, int Keep);

