using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>The exact squared Euclidean distance from every cell of a block array to the nearest cell of a kind (three passes of the lower-envelope transform), for the tests that measure how far a change lies from the air.</summary>
internal static class Distance
{
    private const int Far = 1_000_000_000;

    /// <summary>The squared distance of every cell to the nearest cell whose block passes <paramref name="source"/>.</summary>
    /// <param name="blocks">The blocks, <c>(y * sizeZ + z) * sizeX + x</c>.</param>
    /// <param name="sizeX">The width.</param>
    /// <param name="sizeY">The height.</param>
    /// <param name="sizeZ">The depth.</param>
    /// <param name="source">Which blocks are the sources.</param>
    /// <param name="skyAbove">Whether the layer above the grid is made of sources.</param>
    public static int[] Squared(BlockId[] blocks, int sizeX, int sizeY, int sizeZ, Func<BlockId, bool> source, bool skyAbove)
    {
        var d = new int[blocks.Length];
        for (var i = 0; i < d.Length; i++)
        {
            d[i] = source(blocks[i]) ? 0 : Far;
        }

        var longest = Math.Max(sizeX, Math.Max(sizeY + 1, sizeZ));
        var line = new int[longest];
        var output = new int[longest];
        var hull = new int[longest];
        var bounds = new double[longest + 1];

        for (var y = 0; y < sizeY; y++)
        {
            for (var z = 0; z < sizeZ; z++)
            {
                var start = ((y * sizeZ) + z) * sizeX;
                for (var x = 0; x < sizeX; x++)
                {
                    line[x] = d[start + x];
                }

                Transform(line, output, hull, bounds, sizeX);
                for (var x = 0; x < sizeX; x++)
                {
                    d[start + x] = output[x];
                }
            }
        }

        for (var y = 0; y < sizeY; y++)
        {
            for (var x = 0; x < sizeX; x++)
            {
                for (var z = 0; z < sizeZ; z++)
                {
                    line[z] = d[(((y * sizeZ) + z) * sizeX) + x];
                }

                Transform(line, output, hull, bounds, sizeZ);
                for (var z = 0; z < sizeZ; z++)
                {
                    d[(((y * sizeZ) + z) * sizeX) + x] = output[z];
                }
            }
        }

        for (var z = 0; z < sizeZ; z++)
        {
            for (var x = 0; x < sizeX; x++)
            {
                for (var y = 0; y < sizeY; y++)
                {
                    line[y] = d[(((y * sizeZ) + z) * sizeX) + x];
                }

                line[sizeY] = skyAbove ? 0 : Far;
                Transform(line, output, hull, bounds, sizeY + 1);
                for (var y = 0; y < sizeY; y++)
                {
                    d[(((y * sizeZ) + z) * sizeX) + x] = output[y];
                }
            }
        }

        return d;
    }

    // The 1D transform: output[q] = min over p of line[p] + (q - p)^2.
    private static void Transform(int[] line, int[] output, int[] hull, double[] bounds, int n)
    {
        var k = 0;
        hull[0] = 0;
        bounds[0] = double.NegativeInfinity;
        bounds[1] = double.PositiveInfinity;
        for (var q = 1; q < n; q++)
        {
            double s;
            while (true)
            {
                var p = hull[k];
                s = (((double)line[q] + ((double)q * q)) - ((double)line[p] + ((double)p * p))) / ((2.0 * q) - (2.0 * p));
                if (s <= bounds[k] && k > 0)
                {
                    k--;
                }
                else
                {
                    break;
                }
            }

            if (s <= bounds[k])
            {
                hull[k] = q; // k is 0 here: the new parabola replaces the first
                bounds[k] = double.NegativeInfinity;
                bounds[k + 1] = double.PositiveInfinity;
                continue;
            }

            k++;
            hull[k] = q;
            bounds[k] = s;
            bounds[k + 1] = double.PositiveInfinity;
        }

        k = 0;
        for (var q = 0; q < n; q++)
        {
            while (bounds[k + 1] < q)
            {
                k++;
            }

            var p = hull[k];
            output[q] = (int)Math.Min(Far, (long)line[p] + ((long)(q - p) * (q - p)));
        }
    }
}
