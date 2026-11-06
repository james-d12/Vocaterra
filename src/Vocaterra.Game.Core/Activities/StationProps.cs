using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Activities;

/// <summary>
/// The lookup from a block of the scene to the station it belongs to (W07, design 10.8): a station names the scene prop it stands on (<c>oven</c>), the scene records the cells each
/// named stamp placed (<see cref="Scene.PropCells"/>, as the fire ring's stones are found), and E at any cell of the prop opens the station.
/// </summary>
public static class StationProps
{
    /// <summary>The station whose prop placed this cell, or <see langword="null"/>.</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="stations">The stations of the bundle.</param>
    /// <param name="cell">The block the crosshair is on.</param>
    public static StationDefinition? At(Scene scene, StationBook stations, BlockPos cell)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(stations);
        return scene.PropAt(cell) is { } prop ? stations.Stations.FirstOrDefault(s => string.Equals(s.Prop, prop, StringComparison.Ordinal)) : null;
    }
}
