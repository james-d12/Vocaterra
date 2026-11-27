namespace Vocaterra.Game.Core.Save;

/// <summary>
/// A run of cells holding one state byte other than 0, inside a chunk (K01): <see cref="Length"/> cells from storage position <see cref="Start"/>, all holding
/// <see cref="State"/>. A cell that no run covers holds state 0, so a chunk with no state saves none.
/// </summary>
/// <param name="Start">The first cell's index in the chunk's storage order (0 to 4 095).</param>
/// <param name="Length">How many cells the run holds (at least 1).</param>
/// <param name="State">The state byte (1 to 255).</param>
public readonly record struct StateRun(int Start, int Length, int State);
