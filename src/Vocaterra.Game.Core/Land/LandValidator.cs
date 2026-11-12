using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Land;

/// <summary>
/// The checks of <c>game/land.json</c> that need more than its shape (Y09): every site it names is a site of <c>scene.json</c> (a protected site has a radius, so the disc is
/// not empty), every block name is a block, the margins are not negative, the radii and the plot's size are positive and the price and the slope are not negative.
/// <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static class LandValidator
{
    private const string File = LandData.RelativePath;

    /// <summary>Every problem of the rules, in a fixed order; empty when they hold together and name only what the scene has.</summary>
    /// <param name="land">The rules.</param>
    /// <param name="scene">The scene layout the site names refer to.</param>
    public static IReadOnlyList<string> Validate(LandData land, SceneLayout scene)
    {
        ArgumentNullException.ThrowIfNull(land);
        ArgumentNullException.ThrowIfNull(scene);
        var problems = new List<string>();
        var sites = scene.Sites.ToDictionary(static s => s.Name, StringComparer.Ordinal);
        foreach (var protectedSite in land.ProtectedSites)
        {
            if (!sites.TryGetValue(protectedSite.Site, out var site))
            {
                problems.Add($"{File}: protectedSites names the site '{protectedSite.Site}', which scene.json does not have.");
            }
            else if (site.Radius + (protectedSite.IncludeBlend ? site.Blend : 0) + protectedSite.Margin <= 0)
            {
                problems.Add($"{File}: the protected site '{protectedSite.Site}' covers nothing (its radius{(protectedSite.IncludeBlend ? ", blend" : string.Empty)} and the margin add up to 0).");
            }

            if (protectedSite.Margin < 0)
            {
                problems.Add($"{File}: the margin of the protected site '{protectedSite.Site}' must not be negative (it is {protectedSite.Margin}).");
            }
        }

        foreach (var protectedBlocks in land.ProtectedBlocks)
        {
            if (protectedBlocks.Blocks.Count == 0)
            {
                problems.Add($"{File}: a protectedBlocks entry names no block.");
            }

            foreach (var name in protectedBlocks.Blocks.Where(static name => BlockDefinition.FromName(name) is null))
            {
                problems.Add($"{File}: protectedBlocks names the block '{name}', which is not a block (the names are those of BlockDefinition, like path and bridge).");
            }

            if (protectedBlocks.Margin < 0)
            {
                problems.Add($"{File}: the margin of protectedBlocks ({string.Join(", ", protectedBlocks.Blocks)}) must not be negative (it is {protectedBlocks.Margin}).");
            }
        }

        if (land.Eligible.Count == 0)
        {
            problems.Add($"{File}: eligible names no region, so no plot could be bought anywhere.");
        }

        foreach (var meadow in land.Eligible)
        {
            if (!sites.ContainsKey(meadow.Site))
            {
                problems.Add($"{File}: eligible names the site '{meadow.Site}', which scene.json does not have.");
            }

            if (meadow.Radius <= 0)
            {
                problems.Add($"{File}: the radius of the eligible region '{meadow.Site}' must be positive (it is {meadow.Radius}).");
            }
        }

        if (land.PlotWidth <= 0 || land.PlotDepth <= 0)
        {
            problems.Add($"{File}: the plot's width and depth must be positive (they are {land.PlotWidth} and {land.PlotDepth}).");
        }

        if (land.PlotPrice < 0)
        {
            problems.Add($"{File}: the plot's price must not be below 0 (it is {land.PlotPrice}).");
        }

        if (land.MaxSlope < 0)
        {
            problems.Add($"{File}: the plot's maxSlope must not be negative (it is {land.MaxSlope}).");
        }

        return problems;
    }
}
