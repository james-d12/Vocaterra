namespace Vocaterra.Game.Core.Voxel;

/// <summary>How much of a cell's face a shape covers: the mesher hides a neighbour's face only behind a <see cref="Full"/> cover.</summary>
public enum FaceCover
{
    /// <summary>No box of the shape touches the face's plane.</summary>
    None = 0,

    /// <summary>Boxes touch the face's plane but leave part of the face open.</summary>
    Partial = 1,

    /// <summary>The boxes that touch the face's plane cover all of it.</summary>
    Full = 2,
}
