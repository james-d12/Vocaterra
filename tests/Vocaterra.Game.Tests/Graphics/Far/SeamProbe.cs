using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Graphics.Far;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>
/// B51: measures a seam between two pieces of drawn far land without a card. Along a vertical plane each side hangs a curtain: its highest ground next to the plane, and the wall faces in the plane that continue it downwards. The seam is closed when the two curtains overlap
/// (the lower side's ground reaches the higher side's lowest wall face); when they do not, the gap between them is open air, and over it a ray goes through both pieces into the sky. The meshes' quads are read as drawn (the ids a window's runs give for a cut stand-in).
/// </summary>
internal static class SeamProbe
{
    internal sealed record Quad(Vector3 Min, Vector3 Max, Vector3 Normal);

    /// <summary>The height of the ground (the highest solid voxel that is not a crown, a trunk or water: what a skirt hangs from) of every column of a tile, read from its sections, in blocks. A gap in a crown shows the ground behind it, so the ground is what a seam has to close.</summary>
    internal sealed class GroundMap
    {
        private readonly float[] _top = new float[LodSection.Size * LodSection.Size];
        private readonly float _x0;
        private readonly float _z0;
        private readonly int _cell;

        public GroundMap(ILodSectionSource world, FarViewSettings settings, FarTileKey key)
        {
            var stack = world.Stack(key.Level, key.Tx, key.Tz);
            _cell = settings.CellOf(key.Level);
            (_x0, _z0) = (key.MinX(settings), key.MinZ(settings));
            for (var z = 0; z < LodSection.Size; z++)
            {
                for (var x = 0; x < LodSection.Size; x++)
                {
                    var found = float.NaN;
                    for (var s = stack.Sections.Length - 1; s >= 0 && float.IsNaN(found); s--)
                    {
                        var section = stack.Sections[s];
                        for (var y = LodSection.Size - 1; y >= 0; y--)
                        {
                            var block = section.IsUniform ? section.FillBlock : section.BlockAt(LodSection.Index(x, y, z));
                            if (block != BlockId.Air && block != BlockId.Water && !BlockDefinition.IsLeaves(block) && !BlockDefinition.IsTrunk(block))
                            {
                                found = (((stack.SyMin + s) * LodSection.Size) + y + 1) * _cell;
                                break;
                            }

                            if (section.IsUniform)
                            {
                                break;
                            }
                        }
                    }

                    _top[(z * LodSection.Size) + x] = found;
                }
            }
        }

        /// <summary>The top of the ground of the column at a block position, or <see langword="null"/> outside the tile or with no ground.</summary>
        public float? TopAt(float blockX, float blockZ)
        {
            var (x, z) = ((int)Math.Floor((blockX - _x0) / _cell), (int)Math.Floor((blockZ - _z0) / _cell));
            if ((uint)x >= LodSection.Size || (uint)z >= LodSection.Size)
            {
                return null;
            }

            var top = _top[(z * LodSection.Size) + x];
            return float.IsNaN(top) ? null : top;
        }
    }

    internal sealed record Seam(string Where, int Samples, int Open, float WorstGap, string Worst, int OpenUnderPositive = 0, int OpenUnderNegative = 0)
    {
        // Openings where the positive side's land is the lower one (the negative side hangs its wall too short), and where the negative side's is.
    }

    /// <summary>The quads of a mesh: all of them, or the ones whose ids are given.</summary>
    internal static List<Quad> Quads(FarTileMesh mesh, IEnumerable<int>? ids = null)
    {
        var all = ids ?? Enumerable.Range(0, mesh.OpaqueIndices / 6);
        var quads = new List<Quad>();
        foreach (var q in all)
        {
            var v = mesh.Vertices.Skip(q * 4).Take(4).Select(static x => x.Position).ToArray();
            quads.Add(new Quad(v.Aggregate(Vector3.Min), v.Aggregate(Vector3.Max), Vector3.Normalize(Vector3.Cross(v[1] - v[0], v[2] - v[0]))));
        }

        return quads;
    }

    /// <summary>The ids of the quads a window draws of a tile built cut: its land and the walls under its cut edges (what the renderer draws with the tile's runs).</summary>
    internal static List<int> Drawn(FarTileMesh mesh, FarWindow window)
    {
        var runs = new List<(int First, int Count)>();
        mesh.Granules!.Runs(FarBucket.Opaque, [window], runs);
        mesh.Granules.Runs(FarBucket.Skirt, [window], runs, append: true);
        var ids = new List<int>();
        foreach (var (first, count) in runs)
        {
            for (var at = first; at < first + count; at += 6)
            {
                ids.Add(mesh.IndexAt(at) / 4);
            }
        }

        return ids;
    }

    /// <summary>
    /// The curtain of land on one side of a plane at one place along it: the highest top face that covers the block next to the plane, and how far down the wall faces in the plane (facing the other side) reach in one piece below it; <see langword="null"/> for no ground there.
    /// </summary>
    /// <param name="quads">The drawn quads of that side.</param>
    /// <param name="alongX">Whether the plane is perpendicular to x (at x = <paramref name="c"/>, p runs along z) or to z.</param>
    /// <param name="c">The plane's coordinate.</param>
    /// <param name="side">1 when the land lies at larger coordinates than the plane, -1 when at smaller.</param>
    /// <param name="p">The place along the plane, the lower edge of a one-block step.</param>
    internal static (float Top, float Bottom)? Curtain(List<Quad> quads, GroundMap ground, bool alongX, float c, int side, float p)
    {
        var inside = c + (side * 0.5f);
        var along = p + 0.5f;
        var top = alongX ? ground.TopAt(inside, along) : ground.TopAt(along, inside);
        if (top is null)
        {
            return null;
        }

        var bottom = top.Value;
        var grew = true;
        while (grew)
        {
            grew = false;
            foreach (var q in quads)
            {
                var (a0, a1, b0, b1) = alongX ? (q.Min.X, q.Max.X, q.Min.Z, q.Max.Z) : (q.Min.Z, q.Max.Z, q.Min.X, q.Max.X);
                var facing = alongX ? q.Normal.X : q.Normal.Z;
                if (facing * side < -0.99f && Math.Abs(a0 - c) < 0.01f && Math.Abs(a1 - c) < 0.01f && b0 <= along && along <= b1 && q.Max.Y >= bottom - 0.01f && q.Min.Y < bottom - 0.01f)
                {
                    bottom = q.Min.Y;
                    grew = true;
                }
            }
        }

        return (top.Value, bottom);
    }

    // The quads that touch the plane's neighbourhood: the only ones a curtain is made of.
    private static List<Quad> Near(List<Quad> quads, bool alongX, float c) => [.. quads.Where(q => alongX ? q.Min.X <= c + 0.5f && q.Max.X >= c - 0.5f : q.Min.Z <= c + 0.5f && q.Max.Z >= c - 0.5f)];

    /// <summary>Walks a plane from <paramref name="from"/> to <paramref name="to"/> a block at a time and measures how far apart the two curtains are where they do not overlap.</summary>
    internal static Seam Measure(string where, List<Quad> landOnPositiveSide, GroundMap groundOfPositive, List<Quad> landOnNegativeSide, GroundMap groundOfNegative, bool alongX, float c, int from, int to)
    {
        var (samples, open, worst, at) = (0, 0, 0f, "");
        var (underPositive, underNegative) = (0, 0);
        landOnPositiveSide = Near(landOnPositiveSide, alongX, c);
        landOnNegativeSide = Near(landOnNegativeSide, alongX, c);
        for (var p = from; p < to; p++)
        {
            var a = Curtain(landOnPositiveSide, groundOfPositive, alongX, c, 1, p);
            var b = Curtain(landOnNegativeSide, groundOfNegative, alongX, c, -1, p);
            if (a is null || b is null)
            {
                continue;
            }

            samples++;
            var gap = Math.Max(a.Value.Bottom, b.Value.Bottom) - Math.Min(a.Value.Top, b.Value.Top);
            if (gap > 0.01f)
            {
                open++;
                if (a.Value.Top < b.Value.Top)
                {
                    underPositive++;
                }
                else
                {
                    underNegative++;
                }

                if (gap > worst)
                {
                    (worst, at) = (gap, $"{(alongX ? "x" : "z")}={c} at {p}: + side {a.Value.Bottom}..{a.Value.Top}, - side {b.Value.Bottom}..{b.Value.Top}");
                }
            }
        }

        return new Seam(where, samples, open, worst, at, underPositive, underNegative);
    }
}
