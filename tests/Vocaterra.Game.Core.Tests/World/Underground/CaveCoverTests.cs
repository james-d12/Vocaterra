using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>
/// The caves keep under the ground (B11c): no hole opens in the surface except through an entrance's passage. The owner's bug of 2026-11-16 (three small pits in a diagonal row in a meadow at 857, 71, 227 on seed
/// 42) was B11's entrance shafts on their lattice, which B11b replaced by walkable passages; these tests keep every other way a cave could break the surface out of the world.
/// <list type="bullet">
/// <item>On the free world (the generator alone): a block of air between the highest solid block of a column and the ground's height (the shaped height) is a pit in the surface, whatever made it.</item>
/// <item>On the streamed world the game plays (the valley's land bias moves the ground, so the owner's spot is only there): a column whose highest solid block lies 3 or more under each of its four neighbours' is a pit.</item>
/// </list>
/// Columns within reach of an entrance's passage are left out (the mouth is meant to be a hole). The samples are square windows of chunk columns round the owner's spot and the origin.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class CaveCoverTests
{
    private const int FreeHalf = 8;
    private const int StreamedHalf = 8;
    private const int OwnersX = 857;
    private const int OwnersZ = 227;

    public static TheoryData<ulong> Seeds => new() { 42UL, 7UL, 2026UL };

    private static bool NotSolid(BlockId b) => b is BlockId.Air or BlockId.Water or BlockId.Trunk or BlockId.Leaves or BlockId.SpruceTrunk or BlockId.BirchTrunk or BlockId.SpruceLeaves or BlockId.BirchLeaves
        or BlockId.Bush or BlockId.BerryBush or BlockId.TallGrass or BlockId.Fern or BlockId.Reeds or BlockId.YellowFlower or BlockId.WhiteFlower;

    // The y of the highest solid block of a column of generated layers (int.MinValue when none is).
    private static int Top(IReadOnlyList<ChunkData> layers, int minChunkY, int x, int z, int from)
    {
        for (var y = from; y >= minChunkY << 4; y--)
        {
            var ci = (y >> 4) - minChunkY;
            if (ci < 0 || ci >= layers.Count)
            {
                continue;
            }

            var chunk = layers[ci];
            if (!chunk.IsEmpty && !NotSolid(chunk.Blocks[ChunkData.Index(x & 15, y & 15, z & 15)]))
            {
                return y;
            }
        }

        return int.MinValue;
    }

    /// <summary>The columns read and the pits found outside every entrance's reach, in the free world, in a square of chunk columns.</summary>
    internal static (int Columns, List<(int X, int Y, int Z)> Pits) ScanFree(ulong seed, int centreChunkX, int centreChunkZ, int half)
    {
        var source = ProceduralTestWorld.Source(seed);
        var ramps = new List<EntranceRamp>();
        for (var cz = centreChunkZ - half - 8; cz <= centreChunkZ + half + 8; cz++)
        {
            for (var cx = centreChunkX - half - 8; cx <= centreChunkX + half + 8; cx++)
            {
                ramps.AddRange(source.Ramps(cx, cz));
            }
        }

        var pits = new List<(int, int, int)>();
        var columns = 0;
        for (var cz = centreChunkZ - half; cz <= centreChunkZ + half; cz++)
        {
            for (var cx = centreChunkX - half; cx <= centreChunkX + half; cx++)
            {
                var ctx = source.ShapedColumn(cx, cz);
                var layers = source.GenerateColumn(cx, cz);
                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        var (x, z) = ((cx << 4) + lx, (cz << 4) + lz);
                        columns++;
                        var ground = ctx.HeightAt(x, z);
                        var top = Top(layers, source.MinChunkY, x, z, ground + 40);
                        if (top == int.MinValue || top >= ground || ramps.Any(r => r.Column(x, z, out _, out _, out _, out _) || r.Clears(x, z)))
                        {
                            continue;
                        }

                        pits.Add((x, top + 1, z));
                    }
                }
            }
        }

        return (columns, pits);
    }

    /// <summary>The columns read and the pits found in the streamed world (the interior 14 x 14 columns of each chunk column).</summary>
    internal static (int Columns, List<(int X, int Y, int Z)> Pits) ScanStreamed(ulong seed, int centreChunkX, int centreChunkZ, int half)
    {
        var source = (CompositeChunkSource)WorldFactory.Create(TestGame.Data, seed, WorldMode.Streamed).ChunkSource!;
        var pits = new List<(int, int, int)>();
        var columns = 0;
        for (var cz = centreChunkZ - half; cz <= centreChunkZ + half; cz++)
        {
            for (var cx = centreChunkX - half; cx <= centreChunkX + half; cx++)
            {
                var near = new List<(int X, int Z)>();
                for (var ez = cz - 1; ez <= cz + 1; ez++)
                {
                    for (var ex = cx - 1; ex <= cx + 1; ex++)
                    {
                        near.AddRange(source.CaveEntrances(ex, ez).SelectMany(static e => e.Path));
                    }
                }

                var layers = source.GenerateColumn(cx, cz);
                var tops = new int[16 * 16];
                var minY = source.MinChunkY;
                var high = (source.MaxChunkY << 4) - 1;
                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        tops[(lz * 16) + lx] = Top(layers, minY, (cx << 4) + lx, (cz << 4) + lz, high);
                    }
                }

                for (var lz = 1; lz < 15; lz++)
                {
                    for (var lx = 1; lx < 15; lx++)
                    {
                        columns++;
                        var t = tops[(lz * 16) + lx];
                        var lowest = Math.Min(Math.Min(tops[(lz * 16) + lx - 1], tops[(lz * 16) + lx + 1]), Math.Min(tops[((lz - 1) * 16) + lx], tops[((lz + 1) * 16) + lx]));
                        var (x, z) = ((cx << 4) + lx, (cz << 4) + lz);
                        if (t != int.MinValue && lowest != int.MinValue && lowest - t >= 3 && !near.Any(p => Math.Abs(p.X - x) <= 16 && Math.Abs(p.Z - z) <= 16))
                        {
                            pits.Add((x, t + 1, z));
                        }
                    }
                }
            }
        }

        return (columns, pits);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheFreeWorldOfASeed_When_EveryColumnRoundTheOwnersSpotAndTheOriginIsRead_Then_NoCaveOpensInTheSurfaceOutsideAnEntrance(ulong seed)
    {
        var (c1, p1) = ScanFree(seed, OwnersX >> 4, OwnersZ >> 4, FreeHalf);
        var (c2, p2) = ScanFree(seed, 0, 0, FreeHalf);

        Assert.True(c1 + c2 >= 2 * 17 * 17 * 256, $"{c1 + c2} columns");
        Assert.True(p1.Count + p2.Count == 0, $"seed {seed}: {p1.Count + p2.Count} pits in {c1 + c2} columns, the first at {string.Join(", ", p1.Concat(p2).Take(5))}");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_TheStreamedWorldOfASeed_When_EveryColumnRoundTheOwnersSpotIsRead_Then_NoColumnIsAPitInTheMeadowOutsideAnEntrance(ulong seed)
    {
        var (columns, pits) = ScanStreamed(seed, OwnersX >> 4, OwnersZ >> 4, StreamedHalf);

        Assert.True(columns >= 17 * 17 * 196, $"{columns} columns");
        Assert.True(pits.Count == 0, $"seed {seed}: {pits.Count} pits in {columns} columns, the first at {string.Join(", ", pits.Take(5))}");
    }
}
