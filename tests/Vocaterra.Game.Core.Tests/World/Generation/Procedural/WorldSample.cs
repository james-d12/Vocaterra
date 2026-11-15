using System.Collections.Concurrent;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>A square of the procedural world sampled on a grid (B02b): the ground, the water, the biome and the snow of every sample, for the tests of the tuned world's shape. Pure; one is built per seed and size and shared.</summary>
internal sealed class WorldSample
{
    private static readonly ConcurrentDictionary<(ulong Seed, int Half, int Step), Lazy<WorldSample>> Cache = new();

    private WorldSample(ulong seed, int half, int step)
    {
        var data = TestGame.Data;
        var climate = new ClimateSampler(data.WorldGen, seed);
        var height = new HeightModel(data.WorldGen, seed);
        var table = new BiomeTable(data.Biomes, data.WorldGen, seed);
        Seed = seed;
        Half = half;
        Step = step;
        Size = (2 * half) / step;
        Rows = table.Rows.Select(static r => r.Id).ToList();
        var n = Size * Size;
        Ground = new short[n];
        Water = new bool[n];
        Snow = new bool[n];
        Biome = new byte[n];
        Temperature = new short[n];
        Parallel.For(0, Size, new ParallelOptions { MaxDegreeOfParallelism = 2 }, j =>
        {
            for (var i = 0; i < Size; i++)
            {
                var (x, z) = (-half + (i * step), -half + (j * step));
                var c = climate.At(x, z);
                var shape = height.Shape(x, z, c);
                var column = table.Classify(c, shape.Height, x, z);
                var k = (j * Size) + i;
                Ground[k] = (short)shape.Height;
                Water[k] = shape.Water;
                Snow[k] = column.Snow;
                Biome[k] = (byte)column.Row.Index;
                Temperature[k] = (short)(c.Temperature * 1000 / 4096);
            }
        });
    }

    /// <summary>The sample of a seed over a square of 2 x <paramref name="half"/> blocks centred on the origin, one every <paramref name="step"/> blocks.</summary>
    public static WorldSample Of(ulong seed, int half = 8192, int step = 32) =>
        Cache.GetOrAdd((seed, half, step), static key => new Lazy<WorldSample>(() => new WorldSample(key.Seed, key.Half, key.Step))).Value;

    public ulong Seed { get; }

    public int Half { get; }

    public int Step { get; }

    /// <summary>Samples along each side.</summary>
    public int Size { get; }

    public IReadOnlyList<string> Rows { get; }

    public short[] Ground { get; }

    public bool[] Water { get; }

    public bool[] Snow { get; }

    public byte[] Biome { get; }

    /// <summary>The sea-level temperature in thousandths.</summary>
    public short[] Temperature { get; }

    /// <summary>The position of a biome row by id.</summary>
    public int Row(string id) => Rows.ToList().IndexOf(id);

    /// <summary>The sizes of the four-connected (or eight-connected) bodies of the samples a predicate selects, largest first, with the bounding box of each.</summary>
    public List<Body> Bodies(Func<int, bool> selected, bool eight = false)
    {
        var seen = new bool[Size * Size];
        var bodies = new List<Body>();
        var stack = new Stack<int>();
        for (var start = 0; start < seen.Length; start++)
        {
            if (seen[start] || !selected(start))
            {
                continue;
            }

            seen[start] = true;
            stack.Push(start);
            var (count, minX, maxX, minZ, maxZ) = (0, int.MaxValue, int.MinValue, int.MaxValue, int.MinValue);
            while (stack.Count > 0)
            {
                var k = stack.Pop();
                int x = k % Size, z = k / Size;
                count++;
                (minX, maxX, minZ, maxZ) = (Math.Min(minX, x), Math.Max(maxX, x), Math.Min(minZ, z), Math.Max(maxZ, z));
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dz == 0) || (!eight && dx != 0 && dz != 0))
                        {
                            continue;
                        }

                        int nx = x + dx, nz = z + dz;
                        if ((uint)nx >= (uint)Size || (uint)nz >= (uint)Size)
                        {
                            continue;
                        }

                        var m = (nz * Size) + nx;
                        if (!seen[m] && selected(m))
                        {
                            seen[m] = true;
                            stack.Push(m);
                        }
                    }
                }
            }

            bodies.Add(new Body(count, (maxX - minX + 1) * Step, (maxZ - minZ + 1) * Step));
        }

        bodies.Sort(static (a, b) => b.Count.CompareTo(a.Count));
        return bodies;
    }

    /// <summary>
    /// Labels the eight-connected bodies of the samples a predicate selects (0 for none, 1 and up for each body), and says for each whether any sample within <paramref name="reach"/> samples of it satisfies
    /// <paramref name="touches"/>.
    /// </summary>
    public (int[] Labels, bool[] Touching) Label(Func<int, bool> selected, Func<int, bool> touches, int reach)
    {
        var labels = new int[Size * Size];
        var touching = new List<bool> { false };
        var stack = new Stack<int>();
        for (var start = 0; start < labels.Length; start++)
        {
            if (labels[start] != 0 || !selected(start))
            {
                continue;
            }

            var id = touching.Count;
            touching.Add(false);
            labels[start] = id;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var k = stack.Pop();
                int x = k % Size, z = k / Size;
                for (var dz = -reach; dz <= reach; dz++)
                {
                    for (var dx = -reach; dx <= reach; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if ((uint)nx >= (uint)Size || (uint)nz >= (uint)Size)
                        {
                            continue;
                        }

                        var m = (nz * Size) + nx;
                        if (touches(m))
                        {
                            touching[id] = true;
                        }
                        else if (Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1 && labels[m] == 0 && selected(m))
                        {
                            labels[m] = id;
                            stack.Push(m);
                        }
                    }
                }
            }
        }

        return (labels, [.. touching]);
    }

    /// <summary>One connected body of samples: how many, and the width and depth of its bounding box in blocks.</summary>
    public readonly record struct Body(int Count, int WidthBlocks, int DepthBlocks);
}
