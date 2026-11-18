using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>The caves (B11): sampled on the coarse lattice and interpolated, cheese mostly deep, spaghetti long and winding, lava at and under its level, and a chunk's kinds equal the block-by-block ones.</summary>
public sealed class CaveFieldTests
{
    private static readonly UndergroundData Data = TestGame.Data.Underground!;

    private static CaveField Field(ulong seed = 42) => new(Data.Caves, Data.Lava.Level, seed);

    [Fact]
    public void Given_TheShippedCaves_When_TheLatticeIsRead_Then_ItIsFourByEightByFour()
    {
        var field = Field();

        Assert.Equal((4, 8), (field.LatticeXz, field.LatticeY));
        Assert.Equal(240, field.TopY);
    }

    [Theory]
    [InlineData(42UL, 0, -3, 0)]
    [InlineData(42UL, 5, -1, -4)]
    [InlineData(7UL, -6, -2, 2)]
    [InlineData(2026UL, 3, -2, 7)]
    [InlineData(2026UL, -9, 12, 1)]
    public void Given_AChunk_When_ItsKindsAreFilledAtOnce_Then_TheyEqualTheBlockByBlockKinds(ulong seed, int cx, int cy, int cz)
    {
        var field = Field(seed);
        var kinds = new CaveKind[VoxelChunk.Volume];

        field.Fill(cx << 4, cy << 4, cz << 4, kinds);

        var index = 0;
        var cave = 0;
        for (var ly = 0; ly < 16; ly++)
        {
            for (var lz = 0; lz < 16; lz++)
            {
                for (var lx = 0; lx < 16; lx++, index++)
                {
                    Assert.Equal(field.KindAt((cx << 4) + lx, (cy << 4) + ly, (cz << 4) + lz), kinds[index]);
                    cave += kinds[index] != CaveKind.None ? 1 : 0;
                }
            }
        }

        Assert.True(cave > 0 || cy > 8, $"chunk {cx},{cy},{cz} holds no cave");
    }

    [Fact]
    public void Given_AColumn_When_ItsHighestCaveBlockIsAsked_Then_ItIsTheHighestBlockKindAtFindsInTheRange()
    {
        var field = Field();
        var found = 0;
        for (var z = -20; z < 20; z += 3)
        {
            for (var x = -20; x < 20; x += 3)
            {
                foreach (var (high, low) in new[] { (30, 20), (-5, -40), (47, 31), (1, 1) })
                {
                    var expected = int.MinValue;
                    for (var y = high; y >= low; y--)
                    {
                        if (field.KindAt(x, y, z) != CaveKind.None)
                        {
                            expected = y;
                            break;
                        }
                    }

                    Assert.Equal(expected, field.HighestCaveBlock(x, z, high, low));
                    found += expected != int.MinValue ? 1 : 0;
                }
            }
        }

        Assert.True(found > 20);
    }

    [Fact]
    public void Given_TheLattice_When_AFieldIsInterpolated_Then_ItIsTheLinearBlendOfItsCornersSoTheCavesLieOnTheLattice()
    {
        // A grid over one lattice cell: the value at the midpoint of an edge is the mean of the edge's two corners, and the corners are the field at lattice points only.
        Func<int, int, int, int> source = static (x, y, z) => (x * 7) + (y * 3) - (z * 5);
        var at = (int x, int y, int z) => LatticeGrid.At(source, 4, 8, x, y, z);

        Assert.Equal(source(8, 16, 4), at(8, 16, 4));
        Assert.Equal((source(8, 16, 4) + source(12, 16, 4)) / 2, at(10, 16, 4));
        Assert.Equal((source(8, 16, 4) + source(8, 24, 4)) / 2, at(8, 20, 4));
        Assert.Equal((source(8, 16, 4) + source(8, 16, 8)) / 2, at(8, 16, 6));
        Assert.Equal(at(-3, -5, -1), LatticeGrid.At(source, 4, 8, -3, -5, -1)); // negative blocks use the floor cell
        var grid = new LatticeGrid(source, 4, 8, 16, 32, -16, 16);
        Assert.Equal((4, 2), (grid.CellsXz, grid.CellsY));
        Assert.Equal(at(21, 37, -9), grid.Blend(1, 0, 1, 1 * 4096 / 4, 5 * 4096 / 8, 3 * 4096 / 4));
    }

    [Fact]
    public void Given_ASampleOfTheUnderground_When_CheeseCellsAreCounted_Then_MostLieDeep()
    {
        var field = Field();
        long deepCheese = 0;
        long highCheese = 0;
        long spaghettiOnly = 0;
        long everything = 0;
        for (var y = -63; y < 120; y += 2)
        {
            for (var z = -300; z < 300; z += 6)
            {
                for (var x = -300; x < 300; x += 6)
                {
                    var kind = field.KindAt(x, y, z);
                    everything++;
                    if ((kind & CaveKind.Cheese) != 0)
                    {
                        if (y < 0)
                        {
                            deepCheese++;
                        }
                        else if (y >= 40)
                        {
                            highCheese++;
                        }
                    }

                    spaghettiOnly += kind == CaveKind.Spaghetti ? 1 : 0;
                }
            }
        }

        Assert.True(deepCheese > 1000, $"{deepCheese} deep cheese samples of {everything}");
        Assert.True(deepCheese > 20 * (highCheese + 1), $"deep {deepCheese} against high {highCheese}");
        Assert.True(spaghettiOnly > 500, $"{spaghettiOnly} tunnel samples");
    }

    [Fact]
    public void Given_ATunnelBox_When_ItsSpaghettiIsFollowed_Then_TheTunnelsAreLongThinAndWinding()
    {
        var field = Field(42);
        const int Size = 96;
        var thinnest = new List<int>();
        var cells = new bool[Size, 48, Size];
        for (var y = 0; y < 48; y++)
        {
            for (var z = 0; z < Size; z++)
            {
                for (var x = 0; x < Size; x++)
                {
                    cells[x, y, z] = (field.KindAt(x, y - 40, z) & CaveKind.Spaghetti) != 0 && (field.KindAt(x, y - 40, z) & CaveKind.Cheese) == 0;
                }
            }
        }

        // Flood fill the biggest tunnel network (6-connected) and measure how far it reaches against how much of it there is.
        var seen = new bool[Size, 48, Size];
        (int Cells, int Reach, List<(int X, int Y, int Z)> Members) best = (0, 0, []);
        for (var sy = 0; sy < 48; sy++)
        {
            for (var sz = 0; sz < Size; sz++)
            {
                for (var sx = 0; sx < Size; sx++)
                {
                    if (!cells[sx, sy, sz] || seen[sx, sy, sz])
                    {
                        continue;
                    }

                    var stack = new Stack<(int X, int Y, int Z)>();
                    var members = new List<(int X, int Y, int Z)>();
                    stack.Push((sx, sy, sz));
                    seen[sx, sy, sz] = true;
                    var (minX, maxX, minY, maxY, minZ, maxZ) = (sx, sx, sy, sy, sz, sz);
                    while (stack.Count > 0)
                    {
                        var (x, y, z) = stack.Pop();
                        members.Add((x, y, z));
                        (minX, maxX, minY, maxY, minZ, maxZ) = (Math.Min(minX, x), Math.Max(maxX, x), Math.Min(minY, y), Math.Max(maxY, y), Math.Min(minZ, z), Math.Max(maxZ, z));
                        foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
                        {
                            var (nx, ny, nz) = (x + dx, y + dy, z + dz);
                            if ((uint)nx < Size && (uint)ny < 48 && (uint)nz < Size && cells[nx, ny, nz] && !seen[nx, ny, nz])
                            {
                                seen[nx, ny, nz] = true;
                                stack.Push((nx, ny, nz));
                            }
                        }
                    }

                    var reach = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ)) + 1;
                    if (members.Count > best.Cells)
                    {
                        best = (members.Count, reach, members);
                    }
                }
            }
        }

        // Thin: through each cell, the shortest of the three runs along the axes is the tunnel's width there.
        foreach (var (x, y, z) in best.Members.Where((_, i) => i % 7 == 0))
        {
            int Run(int dx, int dy, int dz)
            {
                var n = 1;
                for (var s = 1; s <= 12 && Inside(x + (s * dx), y + (s * dy), z + (s * dz)) && cells[x + (s * dx), y + (s * dy), z + (s * dz)]; s++)
                {
                    n++;
                }

                for (var s = 1; s <= 12 && Inside(x - (s * dx), y - (s * dy), z - (s * dz)) && cells[x - (s * dx), y - (s * dy), z - (s * dz)]; s++)
                {
                    n++;
                }

                return n;
            }

            thinnest.Add(Math.Min(Run(1, 0, 0), Math.Min(Run(0, 1, 0), Run(0, 0, 1))));
        }

        var width = thinnest.Average();
        var length = best.Cells / (width * width); // the network's length as a tunnel of that width
        Assert.True(best.Reach >= 40, $"the biggest tunnel network reaches {best.Reach} blocks ({best.Cells} cells)");
        Assert.True(width <= 8, $"the tunnels are {width:F1} blocks across at their narrowest");
        Assert.True(thinnest.Count(t => t > 10) * 5 < thinnest.Count, $"few cavern-sized rooms in the tunnels: {thinnest.Count(t => t > 10)} of {thinnest.Count} cells are over 10 blocks across (mean {width:F1}, widest {thinnest.Max()})");
        Assert.True(length >= 1.5 * best.Reach, $"the network is {length:F0} blocks of tunnel in a reach of {best.Reach}: it winds");

        static bool Inside(int x, int y, int z) => (uint)x < Size && (uint)y < 48 && (uint)z < Size;
    }

    [Fact]
    public void Given_TheCheeseThreshold_When_ItIsReadAtHeights_Then_ItIsTheDeepOneUnderDeepYAndRisesInAStraightLine()
    {
        var field = Field();
        var c = Data.Caves.Cheese;
        var deep = field.CheeseThresholdAt(c.DeepY - 20);

        Assert.Equal(deep, field.CheeseThresholdAt(c.DeepY));
        Assert.Equal(4096 * c.DeepThreshold / 1000, deep);
        Assert.True(field.CheeseThresholdAt(c.DeepY + 10) > deep);
        Assert.True(field.CheeseThresholdAt(c.ShallowY) >= field.CheeseThresholdAt(c.ShallowY - 10));
        Assert.Equal(field.CheeseThresholdAt(c.ShallowY), field.CheeseThresholdAt(c.ShallowY + 50));
        var mid = (c.DeepY + c.ShallowY) / 2;
        Assert.InRange(field.CheeseThresholdAt(mid), ((deep + field.CheeseThresholdAt(c.ShallowY)) / 2) - 40, ((deep + field.CheeseThresholdAt(c.ShallowY)) / 2) + 40);
    }

    [Fact]
    public void Given_TheCaveField_When_BlocksAreAskedWithACeiling_Then_SolidAboveItAirAboveTheLavaAndLavaAtAndUnderTheLevel()
    {
        var field = Field();
        var (air, lava, solidAbove, topSolid) = (0, 0, 0, 0);
        for (var z = -100; z < 100; z += 2)
        {
            for (var x = -100; x < 100; x += 2)
            {
                for (var y = -63; y < 20; y++)
                {
                    var cell = field.At(x, y, z, 5);
                    var kind = field.KindAt(x, y, z);
                    if (y > 5)
                    {
                        Assert.Equal(CaveCell.Solid, cell);
                        solidAbove++;
                    }
                    else if (kind == CaveKind.None)
                    {
                        Assert.Equal(CaveCell.Solid, cell);
                        topSolid++;
                    }
                    else if (y <= Data.Lava.Level)
                    {
                        Assert.Equal(CaveCell.Lava, cell);
                        lava++;
                    }
                    else
                    {
                        Assert.Equal(CaveCell.Air, cell);
                        air++;
                    }
                }
            }
        }

        Assert.True(air > 500 && lava > 500 && solidAbove > 0 && topSolid > 0, $"air {air}, lava {lava}");
    }

    [Fact]
    public void Given_AHeightAboveTheTop_When_ItsKindIsAsked_Then_ItIsSolid()
    {
        var field = Field();

        for (var x = 0; x < 100; x++)
        {
            Assert.Equal(CaveKind.None, field.KindAt(x, field.TopY + 1, x * 3));
        }

        var kinds = new CaveKind[VoxelChunk.Volume];
        field.Fill(0, 256, 0, kinds);
        Assert.All(kinds, static k => Assert.Equal(CaveKind.None, k));
    }
}
