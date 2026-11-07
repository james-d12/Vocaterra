namespace Vocaterra.Game.Core.Save;

/// <summary>
/// A run of water cells of one level other than source, inside a chunk (water-plan H06): <see cref="Length"/> cells from storage position <see cref="Start"/>, all
/// holding <see cref="Level"/>. A cell of a chunk that no run covers is source (or not water), so a chunk of the built scene saves none.
/// </summary>
/// <param name="Start">The first cell's index in the chunk's storage order (0 to 4 095).</param>
/// <param name="Length">How many cells the run holds (at least 1).</param>
/// <param name="Level">The level, as <see cref="Vocaterra.Game.Core.Voxel.WaterLevel.Encoded"/> (1 to 15).</param>
public readonly record struct WaterRun(int Start, int Length, int Level);
