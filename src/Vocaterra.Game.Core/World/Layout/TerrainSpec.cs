namespace Vocaterra.Game.Core.World.Layout;

/// <summary>The shape of the land before the sites, the river and the paths are cut into it.</summary>
/// <param name="Relief">The macro relief: the height in blocks at every chunk-column corner (x = 16 * column, z = 16 * row), row by row; smooth between the corners.</param>
/// <param name="ReliefColumns">The columns of <paramref name="Relief"/> (the grid's width in chunks).</param>
/// <param name="ReliefRows">The rows of <paramref name="Relief"/> (the grid's depth in chunks).</param>
/// <param name="Valley">The height of the valley floor; the seed's noise is strongest on the mountains and weakest at this height.</param>
/// <param name="RockLine">The height above which the ground is bare stone.</param>
/// <param name="Terrace">The height in blocks of one step of the stepped cliff faces (0: no terracing).</param>
/// <param name="Ceiling">The highest the ground goes.</param>
public sealed record TerrainSpec(int[] Relief, int ReliefColumns, int ReliefRows, int Valley, int RockLine, int Terrace, int Ceiling);

