using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Noise;

/// <summary>
/// A field computed only at the points of a coarse lattice and interpolated linearly between them (B01): the climate is sampled every 4 blocks this way, because its
/// noise is smooth over hundreds, and it costs a sixteenth of sampling every column. <see cref="Fill"/> evaluates the lattice once for a whole area and gives exactly the
/// numbers <see cref="At"/> gives one by one. The source is a pure function of the lattice point.
/// </summary>
internal sealed class LatticeSampler2
{
    private readonly int _cell;
    private readonly Func<int, int, int> _source;

    /// <summary>Creates the sampler.</summary>
    /// <param name="cell">The lattice spacing in blocks (positive).</param>
    /// <param name="source">The field at a block (x, z); it is read only at multiples of <paramref name="cell"/>.</param>
    public LatticeSampler2(int cell, Func<int, int, int> source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cell);
        ArgumentNullException.ThrowIfNull(source);
        _cell = cell;
        _source = source;
    }

    /// <summary>The field at a block, from its four lattice corners.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int At(int x, int z)
    {
        var ix = IntMath.DivDown(x, _cell);
        var iz = IntMath.DivDown(z, _cell);
        return Blend(
            _source(ix * _cell, iz * _cell),
            _source((ix + 1) * _cell, iz * _cell),
            _source(ix * _cell, (iz + 1) * _cell),
            _source((ix + 1) * _cell, (iz + 1) * _cell),
            IntMath.Mod(x, _cell) * IntMath.One / _cell,
            IntMath.Mod(z, _cell) * IntMath.One / _cell);
    }

    /// <summary>The field over a rectangle of blocks, row by row (x fastest), the lattice evaluated once.</summary>
    /// <param name="x0">The first block's x.</param>
    /// <param name="z0">The first block's z.</param>
    /// <param name="width">The blocks along x.</param>
    /// <param name="depth">The blocks along z.</param>
    /// <param name="into">The values, at least <paramref name="width"/> times <paramref name="depth"/> of them.</param>
    public void Fill(int x0, int z0, int width, int depth, Span<int> into)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(into.Length, width * depth);
        if (width <= 0 || depth <= 0)
        {
            return;
        }

        var ix0 = IntMath.DivDown(x0, _cell);
        var iz0 = IntMath.DivDown(z0, _cell);
        var nx = IntMath.DivDown(x0 + width - 1, _cell) - ix0 + 2;
        var nz = IntMath.DivDown(z0 + depth - 1, _cell) - iz0 + 2;
        var lattice = new int[nx * nz];
        for (var j = 0; j < nz; j++)
        {
            for (var i = 0; i < nx; i++)
            {
                lattice[(j * nx) + i] = _source((ix0 + i) * _cell, (iz0 + j) * _cell);
            }
        }

        for (var z = 0; z < depth; z++)
        {
            var bz = z0 + z;
            var j = IntMath.DivDown(bz, _cell) - iz0;
            var fz = IntMath.Mod(bz, _cell) * IntMath.One / _cell;
            for (var x = 0; x < width; x++)
            {
                var bx = x0 + x;
                var i = IntMath.DivDown(bx, _cell) - ix0;
                var fx = IntMath.Mod(bx, _cell) * IntMath.One / _cell;
                into[(z * width) + x] = Blend(lattice[(j * nx) + i], lattice[(j * nx) + i + 1], lattice[((j + 1) * nx) + i], lattice[((j + 1) * nx) + i + 1], fx, fz);
            }
        }
    }

    private static int Blend(int a, int b, int c, int d, int fx, int fz) => IntMath.Lerp(IntMath.Lerp(a, b, fx), IntMath.Lerp(c, d, fx), fz);
}

/// <summary>
/// A field computed only at the points of a coarse three-dimensional lattice and interpolated trilinearly between them (B01, for the caves of B11, which are smooth over
/// tens of blocks). <see cref="Fill"/> evaluates the lattice once for a box and gives exactly the numbers <see cref="At"/> gives one by one.
/// </summary>
internal sealed class LatticeSampler3
{
    private readonly int _cellXz;
    private readonly int _cellY;
    private readonly Func<int, int, int, int> _source;

    /// <summary>Creates the sampler.</summary>
    /// <param name="cellXz">The lattice spacing across, in blocks (positive).</param>
    /// <param name="cellY">The lattice spacing up, in blocks (positive).</param>
    /// <param name="source">The field at a block (x, y, z); it is read only at lattice points.</param>
    public LatticeSampler3(int cellXz, int cellY, Func<int, int, int, int> source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellXz);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellY);
        ArgumentNullException.ThrowIfNull(source);
        _cellXz = cellXz;
        _cellY = cellY;
        _source = source;
    }

    /// <summary>The field at a block, from its eight lattice corners.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public int At(int x, int y, int z)
    {
        var ix = IntMath.DivDown(x, _cellXz);
        var iy = IntMath.DivDown(y, _cellY);
        var iz = IntMath.DivDown(z, _cellXz);
        int Corner(int dx, int dy, int dz) => _source((ix + dx) * _cellXz, (iy + dy) * _cellY, (iz + dz) * _cellXz);
        return Blend(
            Corner(0, 0, 0),
            Corner(1, 0, 0),
            Corner(0, 1, 0),
            Corner(1, 1, 0),
            Corner(0, 0, 1),
            Corner(1, 0, 1),
            Corner(0, 1, 1),
            Corner(1, 1, 1),
            IntMath.Mod(x, _cellXz) * IntMath.One / _cellXz,
            IntMath.Mod(y, _cellY) * IntMath.One / _cellY,
            IntMath.Mod(z, _cellXz) * IntMath.One / _cellXz);
    }

    /// <summary>The field over a box of blocks, x fastest, then z, then y: the value at (x, y, z) of the box is at <c>((y * depth) + z) * width + x</c>.</summary>
    /// <param name="x0">The first block's x.</param>
    /// <param name="y0">The first block's y.</param>
    /// <param name="z0">The first block's z.</param>
    /// <param name="width">The blocks along x.</param>
    /// <param name="height">The blocks along y.</param>
    /// <param name="depth">The blocks along z.</param>
    /// <param name="into">The values, at least width times height times depth of them.</param>
    public void Fill(int x0, int y0, int z0, int width, int height, int depth, Span<int> into)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(into.Length, width * height * depth);
        if (width <= 0 || height <= 0 || depth <= 0)
        {
            return;
        }

        var ix0 = IntMath.DivDown(x0, _cellXz);
        var iy0 = IntMath.DivDown(y0, _cellY);
        var iz0 = IntMath.DivDown(z0, _cellXz);
        var nx = IntMath.DivDown(x0 + width - 1, _cellXz) - ix0 + 2;
        var ny = IntMath.DivDown(y0 + height - 1, _cellY) - iy0 + 2;
        var nz = IntMath.DivDown(z0 + depth - 1, _cellXz) - iz0 + 2;
        var lattice = new int[nx * ny * nz];
        for (var k = 0; k < nz; k++)
        {
            for (var j = 0; j < ny; j++)
            {
                for (var i = 0; i < nx; i++)
                {
                    lattice[(((k * ny) + j) * nx) + i] = _source((ix0 + i) * _cellXz, (iy0 + j) * _cellY, (iz0 + k) * _cellXz);
                }
            }
        }

        for (var y = 0; y < height; y++)
        {
            var by = y0 + y;
            var j = IntMath.DivDown(by, _cellY) - iy0;
            var fy = IntMath.Mod(by, _cellY) * IntMath.One / _cellY;
            for (var z = 0; z < depth; z++)
            {
                var bz = z0 + z;
                var k = IntMath.DivDown(bz, _cellXz) - iz0;
                var fz = IntMath.Mod(bz, _cellXz) * IntMath.One / _cellXz;
                for (var x = 0; x < width; x++)
                {
                    var bx = x0 + x;
                    var i = IntMath.DivDown(bx, _cellXz) - ix0;
                    var fx = IntMath.Mod(bx, _cellXz) * IntMath.One / _cellXz;
                    int L(int di, int dj, int dk) => lattice[(((k + dk) * ny) + j + dj) * nx + i + di];
                    into[(((y * depth) + z) * width) + x] = Blend(L(0, 0, 0), L(1, 0, 0), L(0, 1, 0), L(1, 1, 0), L(0, 0, 1), L(1, 0, 1), L(0, 1, 1), L(1, 1, 1), fx, fy, fz);
                }
            }
        }
    }

    private static int Blend(int c000, int c100, int c010, int c110, int c001, int c101, int c011, int c111, int fx, int fy, int fz)
    {
        var low = IntMath.Lerp(IntMath.Lerp(c000, c100, fx), IntMath.Lerp(c010, c110, fx), fy);
        var high = IntMath.Lerp(IntMath.Lerp(c001, c101, fx), IntMath.Lerp(c011, c111, fx), fy);
        return IntMath.Lerp(low, high, fz);
    }
}
