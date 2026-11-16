using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>
/// A field evaluated at the points of a coarse 3D lattice over one box of blocks, for interpolating cell by cell (B11): the box starts on a lattice point, so its blocks lie in whole lattice cells.
/// <see cref="Blend"/> and <see cref="At"/> interpolate with the same arithmetic (along y and z first, then x), so a chunk's numbers equal a block's one by one, and <see cref="Min"/> and
/// <see cref="Max"/> bound every value in a cell (a trilinear blend with rounding towards zero never leaves the range of its eight corners), so a pass can skip a cell the field cannot pass a
/// threshold in. Pure; one grid is made, read and dropped by one call.
/// </summary>
internal sealed class LatticeGrid
{
    private readonly int[] _values;
    private readonly int _nx;
    private readonly int _ny;

    /// <summary>Evaluates the lattice over a box.</summary>
    /// <param name="source">The field at a block, read only at lattice points.</param>
    /// <param name="cellXz">The lattice spacing across, in blocks.</param>
    /// <param name="cellY">The lattice spacing up, in blocks.</param>
    /// <param name="x0">The box's first x (a multiple of <paramref name="cellXz"/>).</param>
    /// <param name="y0">The box's first y (a multiple of <paramref name="cellY"/>).</param>
    /// <param name="z0">The box's first z (a multiple of <paramref name="cellXz"/>).</param>
    /// <param name="size">The box's side in blocks (a multiple of both spacings).</param>
    public LatticeGrid(Func<int, int, int, int> source, int cellXz, int cellY, int x0, int y0, int z0, int size)
    {
        CellsXz = size / cellXz;
        CellsY = size / cellY;
        _nx = CellsXz + 1;
        _ny = CellsY + 1;
        _values = new int[_nx * _ny * _nx];
        var index = 0;
        for (var k = 0; k <= CellsXz; k++)
        {
            for (var j = 0; j <= CellsY; j++)
            {
                for (var i = 0; i <= CellsXz; i++)
                {
                    _values[index++] = source(x0 + (i * cellXz), y0 + (j * cellY), z0 + (k * cellXz));
                }
            }
        }
    }

    /// <summary>The lattice cells along x and along z.</summary>
    public int CellsXz { get; }

    /// <summary>The lattice cells along y.</summary>
    public int CellsY { get; }

    /// <summary>The lowest corner of a cell.</summary>
    /// <param name="i">The cell's x.</param>
    /// <param name="j">The cell's y.</param>
    /// <param name="k">The cell's z.</param>
    public int Min(int i, int j, int k)
    {
        var m = int.MaxValue;
        for (var c = 0; c < 8; c++)
        {
            m = Math.Min(m, Corner(i, j, k, c));
        }

        return m;
    }

    /// <summary>The highest corner of a cell.</summary>
    /// <param name="i">The cell's x.</param>
    /// <param name="j">The cell's y.</param>
    /// <param name="k">The cell's z.</param>
    public int Max(int i, int j, int k)
    {
        var m = int.MinValue;
        for (var c = 0; c < 8; c++)
        {
            m = Math.Max(m, Corner(i, j, k, c));
        }

        return m;
    }

    /// <summary>
    /// The two edges of a cell at fractions along y and z: the field on the cell's x = 0 face and on its x = 1 face at that height and depth. A row of blocks along x shares them, so it blends them
    /// with <see cref="Across"/> and the lerps along y and z are done once for the row.
    /// </summary>
    /// <param name="i">The cell's x.</param>
    /// <param name="j">The cell's y.</param>
    /// <param name="k">The cell's z.</param>
    /// <param name="fy">The fraction along y.</param>
    /// <param name="fz">The fraction along z.</param>
    /// <param name="left">The field on the x = 0 face.</param>
    /// <param name="right">The field on the x = 1 face.</param>
    public void Edges(int i, int j, int k, int fy, int fz, out int left, out int right)
    {
        left = Edge(Corner(i, j, k, 0), Corner(i, j, k, 2), Corner(i, j, k, 4), Corner(i, j, k, 6), fy, fz);
        right = Edge(Corner(i, j, k, 1), Corner(i, j, k, 3), Corner(i, j, k, 5), Corner(i, j, k, 7), fy, fz);
    }

    /// <summary>The field between the two faces of a cell at a fraction along x.</summary>
    /// <param name="left">The field on the x = 0 face.</param>
    /// <param name="right">The field on the x = 1 face.</param>
    /// <param name="fx">The fraction along x.</param>
    public static int Across(int left, int right, int fx) => IntMath.Lerp(left, right, fx);

    /// <summary>The field inside a cell, at fractions (0 to <see cref="IntMath.One"/>) along x, y and z.</summary>
    /// <param name="i">The cell's x.</param>
    /// <param name="j">The cell's y.</param>
    /// <param name="k">The cell's z.</param>
    /// <param name="fx">The fraction along x.</param>
    /// <param name="fy">The fraction along y.</param>
    /// <param name="fz">The fraction along z.</param>
    public int Blend(int i, int j, int k, int fx, int fy, int fz)
    {
        Edges(i, j, k, fy, fz, out var left, out var right);
        return Across(left, right, fx);
    }

    /// <summary>
    /// The field at one block, from its eight lattice corners, with exactly the arithmetic of the grid's own cells (so the numbers equal <see cref="Blend"/>'s over a box).
    /// </summary>
    /// <param name="source">The field at a block, read only at lattice points.</param>
    /// <param name="cellXz">The lattice spacing across, in blocks.</param>
    /// <param name="cellY">The lattice spacing up, in blocks.</param>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public static int At(Func<int, int, int, int> source, int cellXz, int cellY, int x, int y, int z)
    {
        var ix = IntMath.DivDown(x, cellXz);
        var iy = IntMath.DivDown(y, cellY);
        var iz = IntMath.DivDown(z, cellXz);
        int Corner(int dx, int dy, int dz) => source((ix + dx) * cellXz, (iy + dy) * cellY, (iz + dz) * cellXz);
        var fx = IntMath.Mod(x, cellXz) * IntMath.One / cellXz;
        var fy = IntMath.Mod(y, cellY) * IntMath.One / cellY;
        var fz = IntMath.Mod(z, cellXz) * IntMath.One / cellXz;
        var left = Edge(Corner(0, 0, 0), Corner(0, 1, 0), Corner(0, 0, 1), Corner(0, 1, 1), fy, fz);
        var right = Edge(Corner(1, 0, 0), Corner(1, 1, 0), Corner(1, 0, 1), Corner(1, 1, 1), fy, fz);
        return Across(left, right, fx);
    }

    /// <summary>One face of a cell: the lerp along y of the two columns of corners (c00 and c10 at z = 0, c01 and c11 at z = 1, each low then high), then along z.</summary>
    /// <param name="c00">The low corner at the near z.</param>
    /// <param name="c10">The high corner at the near z.</param>
    /// <param name="c01">The low corner at the far z.</param>
    /// <param name="c11">The high corner at the far z.</param>
    /// <param name="fy">The fraction along y.</param>
    /// <param name="fz">The fraction along z.</param>
    public static int Edge(int c00, int c10, int c01, int c11, int fy, int fz) => IntMath.Lerp(IntMath.Lerp(c00, c10, fy), IntMath.Lerp(c01, c11, fy), fz);

    // Corner c of a cell: bit 0 is +x, bit 1 is +y, bit 2 is +z.
    private int Corner(int i, int j, int k, int c) => _values[(((k + ((c >> 2) & 1)) * _ny) + j + ((c >> 1) & 1)) * _nx + i + (c & 1)];
}
