namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// Told when a cell of a <see cref="VoxelGrid"/> actually changes (the flow system wakes on it). Raised by block and water-level writes that
/// change something, never by the scene build's column fills, so building a scene wakes nothing.
/// </summary>
public interface IBlockChangeListener
{
    /// <summary>Called after the grid holds the new value.</summary>
    void OnBlockChanged(BlockChange change);
}
