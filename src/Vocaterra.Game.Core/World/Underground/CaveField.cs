using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>What a cave field says about a block.</summary>
public enum CaveCell : byte
{
    /// <summary>Rock stays.</summary>
    Solid,

    /// <summary>The cave is open air.</summary>
    Air,

    /// <summary>The cave air lies at or below the lava level and is lava.</summary>
    Lava,
}

/// <summary>The kinds of cave a block can belong to (flags).</summary>
[Flags]
public enum CaveKind : byte
{
    /// <summary>Neither: solid.</summary>
    None = 0,

    /// <summary>A cheese cavern: a large chamber where one noise field is high.</summary>
    Cheese = 1,

    /// <summary>A spaghetti tunnel: where two noise fields are both near zero.</summary>
    Spaghetti = 2,
}

/// <summary>The caves of a world (B11, biomes plan section 3.5): a pure function of the position and of how high the column allows a cave to reach.</summary>
public interface ICaveField
{
    /// <summary>What a block is: solid, cave air or lava.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="ceiling">The highest y the column lets a cave reach (the surface cover, the water rule and the regions' floor already applied); above it the block is solid.</param>
    CaveCell At(int x, int y, int z, int ceiling);
}

/// <summary>
/// The noise caves in the manner of Minecraft 1.18 (<see cref="CaveSpec"/>): cheese caverns where one 3D field passes a threshold that rises with height (so they lie deep), and spaghetti tunnels
/// where two fields are both within a thickness of zero. All three fields are sampled on a coarse lattice (4 x 8 x 4 blocks in the shipped data) and interpolated trilinearly, which is what keeps a
/// chunk cheap; <see cref="Fill"/> evaluates the lattice once for a chunk and gives exactly the kinds <see cref="KindAt"/> gives one by one. Cave air at or below the lava level is lava.
/// Pure, immutable and safe from any thread.
/// </summary>
internal sealed class CaveField : ICaveField
{
    private readonly CaveSpec _spec;
    private readonly NoiseField3 _cheeseField;
    private readonly NoiseField3 _tunnelAField;
    private readonly NoiseField3 _tunnelBField;
    private readonly int _deepThreshold;
    private readonly int _shallowThreshold;
    private readonly int _thickness;
    private readonly int _lavaLevel;

    /// <summary>Creates the caves of one world seed.</summary>
    /// <param name="spec">The caves.</param>
    /// <param name="lavaLevel">The highest y that holds lava.</param>
    /// <param name="seed">The world seed.</param>
    public CaveField(CaveSpec spec, int lavaLevel, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _spec = spec;
        _lavaLevel = lavaLevel;
        _deepThreshold = FieldScale.FromThousandths(spec.Cheese.DeepThreshold);
        _shallowThreshold = FieldScale.FromThousandths(spec.Cheese.ShallowThreshold);
        _thickness = FieldScale.FromThousandths(spec.Spaghetti.Thickness);
        _cheeseField = new NoiseField3(seed, "gen/caves/cheese", spec.Cheese.Cell, spec.Cheese.CellY, spec.Cheese.Octaves);
        _tunnelAField = new NoiseField3(seed, "gen/caves/spaghetti-a", spec.Spaghetti.Cell, spec.Spaghetti.CellY, spec.Spaghetti.Octaves);
        _tunnelBField = new NoiseField3(seed, "gen/caves/spaghetti-b", spec.Spaghetti.Cell, spec.Spaghetti.CellY, spec.Spaghetti.Octaves);
    }

    /// <summary>The lattice spacing across, in blocks.</summary>
    public int LatticeXz => _spec.LatticeXz;

    /// <summary>The lattice spacing up, in blocks.</summary>
    public int LatticeY => _spec.LatticeY;

    /// <summary>The highest y a cave reaches at all.</summary>
    public int TopY => _spec.TopY;

    /// <summary>The cheese caverns' threshold at a height, in the fixed point of the noise: <see cref="CheeseSpec.DeepThreshold"/> deep down, rising in a straight line to <see cref="CheeseSpec.ShallowThreshold"/>.</summary>
    /// <param name="y">The height.</param>
    public int CheeseThresholdAt(int y)
    {
        var c = _spec.Cheese;
        if (y <= c.DeepY)
        {
            return _deepThreshold;
        }

        if (y >= c.ShallowY)
        {
            return _shallowThreshold;
        }

        return _deepThreshold + (int)((long)(_shallowThreshold - _deepThreshold) * (y - c.DeepY) / (c.ShallowY - c.DeepY));
    }

    /// <summary>Which cave kinds a block belongs to, before any column's limit: none above <see cref="TopY"/>.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public CaveKind KindAt(int x, int y, int z) =>
        y > _spec.TopY ? CaveKind.None : Classify(Interpolated(_cheeseField, x, y, z), Interpolated(_tunnelAField, x, y, z), Interpolated(_tunnelBField, x, y, z), CheeseThresholdAt(y));

    private int Interpolated(NoiseField3 field, int x, int y, int z) => LatticeGrid.At(field.At, _spec.LatticeXz, _spec.LatticeY, x, y, z);

    /// <summary>
    /// The highest block of one column between two heights (both inclusive) that belongs to a cave kind, or <see cref="int.MinValue"/> when none does: <see cref="KindAt"/> for each block from the top
    /// down, with the lattice corners of the column's four lattice columns evaluated once for the whole range, which is what the entrance probe needs.
    /// </summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="yHigh">The highest y to look at.</param>
    /// <param name="yLow">The lowest y to look at.</param>
    public int HighestCaveBlock(int x, int z, int yHigh, int yLow)
    {
        yHigh = Math.Min(yHigh, _spec.TopY);
        if (yHigh < yLow)
        {
            return int.MinValue;
        }

        var ix = IntMath.DivDown(x, _spec.LatticeXz);
        var iz = IntMath.DivDown(z, _spec.LatticeXz);
        var iy0 = IntMath.DivDown(yLow, _spec.LatticeY);
        var levels = IntMath.DivDown(yHigh, _spec.LatticeY) - iy0 + 2;
        var corners = new int[3][];
        var fields = new[] { _cheeseField, _tunnelAField, _tunnelBField };
        for (var f = 0; f < fields.Length; f++)
        {
            corners[f] = new int[4 * levels];
            for (var level = 0; level < levels; level++)
            {
                for (var c = 0; c < 4; c++)
                {
                    corners[f][(level * 4) + c] = fields[f].At((ix + (c & 1)) * _spec.LatticeXz, (iy0 + level) * _spec.LatticeY, (iz + (c >> 1)) * _spec.LatticeXz);
                }
            }
        }

        var fx = IntMath.Mod(x, _spec.LatticeXz) * IntMath.One / _spec.LatticeXz;
        var fz = IntMath.Mod(z, _spec.LatticeXz) * IntMath.One / _spec.LatticeXz;
        for (var y = yHigh; y >= yLow; y--)
        {
            var level = IntMath.DivDown(y, _spec.LatticeY) - iy0;
            var fy = IntMath.Mod(y, _spec.LatticeY) * IntMath.One / _spec.LatticeY;
            int At(int[] field)
            {
                var o = level * 4;
                var p = (level + 1) * 4;
                var left = LatticeGrid.Edge(field[o], field[p], field[o + 2], field[p + 2], fy, fz);
                var right = LatticeGrid.Edge(field[o + 1], field[p + 1], field[o + 3], field[p + 3], fy, fz);
                return LatticeGrid.Across(left, right, fx);
            }

            if (Classify(At(corners[0]), At(corners[1]), At(corners[2]), CheeseThresholdAt(y)) != CaveKind.None)
            {
                return y;
            }
        }

        return int.MinValue;
    }

    /// <inheritdoc />
    public CaveCell At(int x, int y, int z, int ceiling)
    {
        if (y > ceiling || KindAt(x, y, z) == CaveKind.None)
        {
            return CaveCell.Solid;
        }

        return y <= _lavaLevel ? CaveCell.Lava : CaveCell.Air;
    }

    /// <summary>
    /// The kinds of the 16 x 16 x 16 box whose first block is (x0, y0, z0) (a chunk: its corner is a multiple of 16), in <c>ChunkData</c> order, the three fields evaluated once on the lattice and
    /// interpolated only in the lattice cells where a kind can occur: a cell whose caverns' noise stays at or under the threshold at its foot, or whose tunnel noises cannot both lie within the
    /// thickness of zero, is skipped whole. The kinds equal <see cref="KindAt"/>'s one by one.
    /// </summary>
    /// <param name="x0">The box's first x.</param>
    /// <param name="y0">The box's first y.</param>
    /// <param name="z0">The box's first z.</param>
    /// <param name="into">The kinds, at least 4 096 of them.</param>
    public void Fill(int x0, int y0, int z0, Span<CaveKind> into)
    {
        const int Size = VoxelChunk.Size;
        ArgumentOutOfRangeException.ThrowIfLessThan(into.Length, VoxelChunk.Volume);
        into[..VoxelChunk.Volume].Clear();
        if (y0 > _spec.TopY)
        {
            return;
        }

        var cheese = new LatticeGrid(_cheeseField.At, _spec.LatticeXz, _spec.LatticeY, x0, y0, z0, Size);
        var a = new LatticeGrid(_tunnelAField.At, _spec.LatticeXz, _spec.LatticeY, x0, y0, z0, Size);
        var b = new LatticeGrid(_tunnelBField.At, _spec.LatticeXz, _spec.LatticeY, x0, y0, z0, Size);
        for (var k = 0; k < cheese.CellsXz; k++)
        {
            for (var j = 0; j < cheese.CellsY; j++)
            {
                var footThreshold = CheeseThresholdAt(y0 + (j * _spec.LatticeY));
                for (var i = 0; i < cheese.CellsXz; i++)
                {
                    var cheeseHere = cheese.Max(i, j, k) > footThreshold;
                    var spaghettiHere = a.Min(i, j, k) < _thickness && a.Max(i, j, k) > -_thickness && b.Min(i, j, k) < _thickness && b.Max(i, j, k) > -_thickness;
                    if (!cheeseHere && !spaghettiHere)
                    {
                        continue;
                    }

                    FillCell(cheese, a, b, (i, j, k), (x0, y0, z0), into);
                }
            }
        }
    }

    private void FillCell(LatticeGrid cheese, LatticeGrid a, LatticeGrid b, (int I, int J, int K) cell, (int X, int Y, int Z) origin, Span<CaveKind> into)
    {
        // A cell whose every corner passes is whole: a blend never leaves the range of its corners. The caverns' threshold is read at the cell's top, where it is highest.
        var topThreshold = CheeseThresholdAt(origin.Y + ((cell.J + 1) * _spec.LatticeY) - 1);
        var cheeseWhole = cheese.Min(cell.I, cell.J, cell.K) > topThreshold;
        var spaghettiWhole = a.Min(cell.I, cell.J, cell.K) > -_thickness && a.Max(cell.I, cell.J, cell.K) < _thickness && b.Min(cell.I, cell.J, cell.K) > -_thickness && b.Max(cell.I, cell.J, cell.K) < _thickness;
        for (var dy = 0; dy < _spec.LatticeY; dy++)
        {
            var ly = (cell.J * _spec.LatticeY) + dy;
            var y = origin.Y + ly;
            if (y > _spec.TopY)
            {
                break;
            }

            var fy = dy * IntMath.One / _spec.LatticeY;
            var threshold = CheeseThresholdAt(y);
            for (var dz = 0; dz < _spec.LatticeXz; dz++)
            {
                var lz = (cell.K * _spec.LatticeXz) + dz;
                var fz = dz * IntMath.One / _spec.LatticeXz;
                int cl = 0, cr = 0, al = 0, ar = 0, bl = 0, br = 0;
                if (!cheeseWhole)
                {
                    cheese.Edges(cell.I, cell.J, cell.K, fy, fz, out cl, out cr);
                }

                if (!spaghettiWhole)
                {
                    a.Edges(cell.I, cell.J, cell.K, fy, fz, out al, out ar);
                    b.Edges(cell.I, cell.J, cell.K, fy, fz, out bl, out br);
                }

                var row = (((ly * VoxelChunk.Size) + lz) * VoxelChunk.Size) + (cell.I * _spec.LatticeXz);
                for (var dx = 0; dx < _spec.LatticeXz; dx++)
                {
                    var fx = dx * IntMath.One / _spec.LatticeXz;
                    var kind = CaveKind.None;
                    if (cheeseWhole || LatticeGrid.Across(cl, cr, fx) > threshold)
                    {
                        kind |= CaveKind.Cheese;
                    }

                    if (spaghettiWhole || (Within(LatticeGrid.Across(al, ar, fx)) && Within(LatticeGrid.Across(bl, br, fx))))
                    {
                        kind |= CaveKind.Spaghetti;
                    }

                    into[row + dx] = kind;
                }
            }
        }
    }

    private bool Within(int value) => value > -_thickness && value < _thickness;

    private CaveKind Classify(int cheese, int a, int b, int cheeseThreshold)
    {
        var kind = CaveKind.None;
        if (cheese > cheeseThreshold)
        {
            kind |= CaveKind.Cheese;
        }

        if (Within(a) && Within(b))
        {
            kind |= CaveKind.Spaghetti;
        }

        return kind;
    }
}
