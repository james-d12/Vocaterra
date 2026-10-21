using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Pass 9: caves below the surface, by two methods. Worms: a seeded random walk of 30 to 70 steps with a heading that drifts, carving a disc
/// of radius 1 or 2 at each step. Caverns: a 20 x 14 box where 45 % of the cells start as rock and four steps of the 4-5 rule turn a cell into
/// rock when five or more of its eight neighbours are rock (the box edge counts as rock), so the rest is open cave. Each chunk draws its own
/// worm and cavern from its own stream.
/// <para>
/// The rule that keeps the surface sealed: a cave only carves a tile of dirt or stone whose depth below the surface column is at least
/// <see cref="MinDepth"/> (7). The six solid tiles above any cave therefore stay in place, so no cave opens within six tiles of the surface,
/// anywhere (the spawn area included), and the grass pass, which runs earlier, never sees a cave. The pass runs after the surface passes.
/// </para>
/// </summary>
internal sealed class CavePass : IGenerationPass
{
    /// <summary>The shallowest depth below the surface column a cave may carve (no cave tile lies above this depth).</summary>
    public const int MinDepth = 7;

    private const int WormChancePercent = 20;
    private const int MinWormSteps = 30;
    private const int MaxWormStepsExclusive = 71;
    private const int CavernChancePercent = 12;
    private const int CavernWidth = 20;
    private const int CavernHeight = 14;
    private const int CavernFillPercent = 45;
    private const int CavernSteps = 4;
    private const int RockNeighbours = 5;

    private static readonly (int Dx, int Dy)[] Headings =
    [
        (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1),
    ];

    /// <inheritdoc />
    public string Name => "caves";

    /// <inheritdoc />
    public void Run(GenerationContext context)
    {
        var tiles = context.Tiles;
        for (var cy = 0; cy < tiles.ChunksY; cy++)
        {
            for (var cx = 0; cx < tiles.ChunksX; cx++)
            {
                var rng = context.Rng(Name, cx, cy);
                var originX = cx * Chunk.Size;
                var originY = cy * Chunk.Size;
                if (rng.NextChance(WormChancePercent, 100))
                {
                    Worm(context, rng, originX + rng.NextInt(Chunk.Size), originY + rng.NextInt(Chunk.Size));
                }

                if (rng.NextChance(CavernChancePercent, 100))
                {
                    Cavern(context, rng, originX + rng.NextInt(Chunk.Size), originY + rng.NextInt(Chunk.Size));
                }
            }
        }
    }

    /// <summary>Whether a tile at this position may be carved: inside the world, not in the bottom edge, dirt or stone, deep enough.</summary>
    internal static bool MayCarve(GenerationContext context, int x, int y)
    {
        var tiles = context.Tiles;
        if (x < 2 || x > tiles.Width - 3 || y < 2 || y > tiles.Height - 3)
        {
            return false;
        }

        if (y - context.Record.SurfaceY[x] < MinDepth)
        {
            return false;
        }

        var tile = tiles.GetTile(x, y);
        return tile is TileId.Dirt or TileId.Stone;
    }

    private static void Worm(GenerationContext context, WorldRng rng, int startX, int startY)
    {
        var length = rng.NextInt(MinWormSteps, MaxWormStepsExclusive);
        var heading = rng.NextInt(Headings.Length);
        var x = startX;
        var y = startY;
        for (var step = 0; step < length; step++)
        {
            var radius = 1 + rng.NextInt(2);
            Carve(context, x, y, radius);
            if (rng.NextChance(1, 3))
            {
                heading = (heading + rng.NextInt(3) + Headings.Length - 1) % Headings.Length;
            }

            var (dx, dy) = Headings[heading];
            x += dx;
            y += dy;
            if (x < 2 || x > context.Tiles.Width - 3 || y < 2 || y > context.Tiles.Height - 3)
            {
                break;
            }
        }
    }

    private static void Carve(GenerationContext context, int centreX, int centreY, int radius)
    {
        for (var y = centreY - radius; y <= centreY + radius; y++)
        {
            for (var x = centreX - radius; x <= centreX + radius; x++)
            {
                var dx = x - centreX;
                var dy = y - centreY;
                if ((dx * dx) + (dy * dy) <= (radius * radius) + 1 && MayCarve(context, x, y))
                {
                    context.Tiles.SetTile(x, y, TileId.Air);
                }
            }
        }
    }

    private static void Cavern(GenerationContext context, WorldRng rng, int originX, int originY)
    {
        var rock = new bool[CavernWidth * CavernHeight];
        for (var i = 0; i < rock.Length; i++)
        {
            rock[i] = rng.NextChance(CavernFillPercent, 100);
        }

        for (var step = 0; step < CavernSteps; step++)
        {
            var next = new bool[rock.Length];
            for (var y = 0; y < CavernHeight; y++)
            {
                for (var x = 0; x < CavernWidth; x++)
                {
                    next[(y * CavernWidth) + x] = CountRock(rock, x, y) >= RockNeighbours;
                }
            }

            rock = next;
        }

        for (var y = 0; y < CavernHeight; y++)
        {
            for (var x = 0; x < CavernWidth; x++)
            {
                if (!rock[(y * CavernWidth) + x] && MayCarve(context, originX + x, originY + y))
                {
                    context.Tiles.SetTile(originX + x, originY + y, TileId.Air);
                }
            }
        }
    }

    /// <summary>The rock neighbours of a cell among its eight; the edge of the box counts as rock.</summary>
    private static int CountRock(bool[] rock, int x, int y)
    {
        var count = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= CavernWidth || ny >= CavernHeight || rock[(ny * CavernWidth) + nx])
                {
                    count++;
                }
            }
        }

        return count;
    }
}
