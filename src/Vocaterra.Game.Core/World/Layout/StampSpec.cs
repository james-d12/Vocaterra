using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Layout;

/// <summary>A small block stamp: layers of rows of characters, bottom layer first, north row first, west column first; a space leaves the cell alone.</summary>
/// <param name="Name">The stamp's name.</param>
/// <param name="Legend">The block of each character.</param>
/// <param name="Layers">The layers from the ground up; each layer is a list of rows.</param>
public sealed record StampSpec(string Name, IReadOnlyDictionary<char, BlockId> Legend, IReadOnlyList<IReadOnlyList<string>> Layers);

