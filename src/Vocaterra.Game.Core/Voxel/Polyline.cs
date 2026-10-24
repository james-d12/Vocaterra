namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// A chain of straight segments over the ground plane, in 1/16 block units, used for the river and the paths: it knows the length along it and
/// can say, for every column near it, how far the column is from the line and how far along the line the nearest point lies.
/// </summary>
internal sealed class Polyline
{
    private Polyline(int[] x16, int[] z16)
    {
        X16 = x16;
        Z16 = z16;
        Cum16 = new long[x16.Length];
        for (var i = 1; i < x16.Length; i++)
        {
            Cum16[i] = Cum16[i - 1] + IntMath.ISqrt(Sq(x16[i] - x16[i - 1]) + Sq(z16[i] - z16[i - 1]));
        }
    }

    /// <summary>The x of each point in 1/16 blocks.</summary>
    public int[] X16 { get; }

    /// <summary>The z of each point in 1/16 blocks.</summary>
    public int[] Z16 { get; }

    /// <summary>The length along the line at each point in 1/16 blocks.</summary>
    public long[] Cum16 { get; }

    /// <summary>The number of points.</summary>
    public int Count => X16.Length;

    /// <summary>The whole length in 1/16 blocks.</summary>
    public long Length16 => Cum16[^1];

    /// <summary>A line straight from one waypoint to the next (the waypoints are block columns).</summary>
    public static Polyline Straight(IReadOnlyList<(int X, int Z)> waypoints)
    {
        var x = new int[waypoints.Count];
        var z = new int[waypoints.Count];
        for (var i = 0; i < x.Length; i++)
        {
            x[i] = (waypoints[i].X * 16) + 8;
            z[i] = (waypoints[i].Z * 16) + 8;
        }

        return new Polyline(x, z);
    }

    /// <summary>
    /// A smooth curve through the waypoints (Catmull-Rom, one point about every <paramref name="spacing"/> blocks), so a river bends instead of
    /// turning at corners. Between two waypoints the curve stays inside their box, so it never loops.
    /// </summary>
    public static Polyline Curve(IReadOnlyList<(int X, int Z)> waypoints, int spacing)
    {
        var x = new List<int>();
        var z = new List<int>();
        for (var i = 0; i + 1 < waypoints.Count; i++)
        {
            var p0 = waypoints[Math.Max(i - 1, 0)];
            var p1 = waypoints[i];
            var p2 = waypoints[i + 1];
            var p3 = waypoints[Math.Min(i + 2, waypoints.Count - 1)];
            var length = (int)IntMath.ISqrt(Sq(p2.X - p1.X) + Sq(p2.Z - p1.Z));
            var steps = Math.Max(1, length / spacing);
            for (var k = 0; k < steps; k++)
            {
                var t = k * IntMath.One / steps;
                x.Add((IntMath.CatmullRom(p0.X * 16, p1.X * 16, p2.X * 16, p3.X * 16, t)) + 8);
                z.Add((IntMath.CatmullRom(p0.Z * 16, p1.Z * 16, p2.Z * 16, p3.Z * 16, t)) + 8);
            }
        }

        var last = waypoints[^1];
        x.Add((last.X * 16) + 8);
        z.Add((last.Z * 16) + 8);
        return new Polyline([.. x], [.. z]);
    }

    /// <summary>
    /// For every column within <paramref name="reachBlocks"/> of the line: the distance to it (<paramref name="dist16"/>, in 1/16 blocks, only
    /// lowered, so several lines can share the arrays), the length along the line at the nearest point (<paramref name="along16"/>) and the index of the
    /// nearest segment (<paramref name="segment"/>). The arrays are indexed by <c>z * sizeX + x</c>; a column's centre is half a block in.
    /// </summary>
    public void Rasterize(int reachBlocks, int sizeX, int sizeZ, int[] dist16, int[] along16, int[] segment)
    {
        var reach16 = reachBlocks * 16;
        for (var i = 0; i + 1 < Count; i++)
        {
            long ax = X16[i], az = Z16[i], bx = X16[i + 1], bz = Z16[i + 1];
            var minX = (int)Math.Max(0, (Math.Min(ax, bx) - reach16) / 16);
            var maxX = (int)Math.Min(sizeX - 1, (Math.Max(ax, bx) + reach16) / 16);
            var minZ = (int)Math.Max(0, (Math.Min(az, bz) - reach16) / 16);
            var maxZ = (int)Math.Min(sizeZ - 1, (Math.Max(az, bz) + reach16) / 16);
            var dx = bx - ax;
            var dz = bz - az;
            var length2 = (dx * dx) + (dz * dz);
            var segmentLength = IntMath.ISqrt(length2);
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    long px = (x * 16L) + 8;
                    long pz = (z * 16L) + 8;
                    var t = length2 == 0 ? 0 : Math.Clamp(((px - ax) * dx) + ((pz - az) * dz), 0, length2) * IntMath.One / length2;
                    var qx = ax + (dx * t / IntMath.One);
                    var qz = az + (dz * t / IntMath.One);
                    var d = IntMath.ISqrt(((px - qx) * (px - qx)) + ((pz - qz) * (pz - qz)));
                    var index = (z * sizeX) + x;
                    if (d < dist16[index] && d <= reach16)
                    {
                        dist16[index] = (int)d;
                        along16[index] = (int)(Cum16[i] + (((segmentLength * t) + (IntMath.One / 2)) / IntMath.One));
                        segment[index] = i;
                    }
                }
            }
        }
    }

    private static long Sq(long v) => v * v;
}
