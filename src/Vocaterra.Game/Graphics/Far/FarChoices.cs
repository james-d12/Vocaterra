using System.Globalization;
using Vocaterra.Game.Core.Menu;

namespace Vocaterra.Game.Graphics.Far;

/// <summary>
/// What the settings page's far view rows and their launch options mean for the far view's shape (B07d): the setting values as the menu counts them, turned into the numbers of <see cref="QualityPreset"/> and <see cref="FarViewSettings"/>. Auto
/// (0) leaves the quality's own number; the world never depends on any of them, only how much of the far land is drawn, how finely and from what.
/// </summary>
public static class FarChoices
{
    /// <summary>The tree levels of the <see cref="SettingId.FarTrees"/> choices from 1: none, then 256, 512, 1,024, 2,048 and 4,096 blocks (level L ends at 128 x 2^L blocks of a 400-block voxel view, so the levels are 0 to 5).</summary>
    public static IReadOnlyList<int> TreeLevels { get; } = [0, 1, 2, 3, 4, 5];

    /// <summary>The real-chunk radii of the <see cref="SettingId.FarExact"/> choices from 1: none (the model alone), then 500, 1,000, 1,500 and 2,000 blocks.</summary>
    public static IReadOnlyList<int> ExactRadii { get; } = [0, 500, 1000, 1500, 2000];

    /// <summary>The distances of the tree choices from 2, in blocks.</summary>
    private static readonly int[] TreeBlocks = [256, 512, 1024, 2048, 4096];

    /// <summary>The setting value of a <c>--far-trees</c> word (<c>auto</c>, <c>off</c> or a distance in blocks), or <see langword="null"/> for anything else.</summary>
    /// <param name="text">The word.</param>
    public static int? TreesOf(string text) => text switch
    {
        "auto" => 0,
        "off" => 1,
        _ => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var blocks) && Array.IndexOf(TreeBlocks, blocks) is >= 0 and var at ? at + 2 : null,
    };

    /// <summary>The setting value of a <c>--far-exact</c> word (<c>auto</c>, <c>off</c> or a radius in blocks), or <see langword="null"/> for anything else.</summary>
    /// <param name="text">The word.</param>
    public static int? ExactOf(string text) => text switch
    {
        "auto" => 0,
        "off" => 1,
        _ => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var blocks) && blocks > 0 && ExactRadii.ToList().IndexOf(blocks) is >= 1 and var at ? at + 1 : null,
    };

    /// <summary>The preset with the far view's settings put over it.</summary>
    /// <param name="preset">The quality's preset.</param>
    /// <param name="distanceKm">The <see cref="SettingId.FarDistance"/> value: 0 for the preset's range, else km.</param>
    /// <param name="detailPercent">The <see cref="SettingId.FarDetail"/> value in percent.</param>
    /// <param name="trees">The <see cref="SettingId.FarTrees"/> value.</param>
    /// <param name="textures">The <see cref="SettingId.FarTextures"/> value.</param>
    /// <param name="exact">The <see cref="SettingId.FarExact"/> value.</param>
    public static QualityPreset Apply(QualityPreset preset, int distanceKm, int detailPercent, int trees, int textures, int exact)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var result = preset with { FarBandScale = detailPercent / 100.0, FarTexturedLevels = Math.Clamp(textures, 0, 3) };
        if (distanceKm > 0 && preset.FarRange > 0)
        {
            result = result with { FarRange = distanceKm * 1024 };
        }

        if (trees > 0)
        {
            result = result with { FarTreeLevels = TreeLevels[Math.Clamp(trees - 1, 0, TreeLevels.Count - 1)] };
        }

        if (exact > 0)
        {
            result = result with { FarRealRadius = ExactRadii[Math.Clamp(exact - 1, 0, ExactRadii.Count - 1)] };
        }

        return result;
    }
}
