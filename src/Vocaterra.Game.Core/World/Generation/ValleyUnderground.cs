using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.World.Generation;

/// <summary>What the valley's underground needs to know about the surface the scene builder has just made (B13).</summary>
/// <param name="Layout">The layout.</param>
/// <param name="Terrain">The ground's shape: heights, water, flags.</param>
/// <param name="Points">The named points (the sites and the layout's own points).</param>
/// <param name="Npcs">The NPCs' places.</param>
/// <param name="PropCells">The cells of the named props.</param>
/// <param name="PropLow">The lowest y a prop wrote in each column, <see cref="int.MaxValue"/> where none did.</param>
/// <param name="AirLow">The lowest y a prop left non-solid in each column, <see cref="int.MaxValue"/> where none did.</param>
internal sealed record ValleyGround(
    SceneLayout Layout,
    Terrain Terrain,
    IReadOnlyDictionary<string, BlockPos> Points,
    IReadOnlyList<NpcPlacement> Npcs,
    IReadOnlyDictionary<string, IReadOnlyList<BlockPos>> PropCells,
    int[] PropLow,
    int[] AirLow);

/// <summary>
/// The underground of the valley's own stone (B13, biomes plan section 3.5, "Under the valley"): the rock layers, the caves, the lava and the ores of <see cref="UndergroundWorld"/>, laid into the
/// valley's stone when the scene is built, so the region template carries them and every reader, bounded or streamed, sees the same blocks. The passes are the procedural ones' (the strata, then
/// the caves, then the lava, then the ores, over the same fields of the same seed), so at y 0 the valley's caves and rock meet the procedural ones below without a seam.
/// <para>
/// The surface does not change. A block is changed only where it is plain stone and lies at or under the column's <i>reach</i>, the highest y at which nothing the player can see, walk on or edit is
/// within a margin: <see cref="ValleySpec.SurfaceDistance"/> from any air cell, <see cref="ValleySpec.WaterDistance"/> from any water cell, <see cref="ValleySpec.BorderMargin"/> columns from the
/// valley's edge, and the margins of the sites, paths, props, village and pass from the surface they stand on. Distances are cubes (the largest axis), found with sliding-window minima over the
/// columns, so the reach costs a few passes over the 147 456 columns and no search through the blocks.
/// </para>
/// </summary>
internal sealed class ValleyUnderground
{
    private readonly UndergroundWorld _world;
    private readonly ValleySpec _spec;

    /// <summary>Creates the valley's underground of one world.</summary>
    /// <param name="world">The world's underground.</param>
    /// <param name="spec">How far it reaches into the valley.</param>
    public ValleyUnderground(UndergroundWorld world, ValleySpec spec)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(spec);
        _world = world;
        _spec = spec;
    }

    /// <summary>The valley's underground of a world seed, or <see langword="null"/> when the data has none (a valley of plain stone).</summary>
    /// <param name="data">The underground's data, or <see langword="null"/>.</param>
    /// <param name="seed">The world seed.</param>
    public static ValleyUnderground? Create(UndergroundData? data, ulong seed) =>
        data?.Valley is { } valley ? new ValleyUnderground(new UndergroundWorld(data, seed), valley) : null;

    /// <summary>How far it reaches into the valley.</summary>
    public ValleySpec Spec => _spec;

    /// <summary>The most threads the laying uses (the game's two-worker rule and the machine's limits; B24a).</summary>
    internal const int MaxThreads = 2;

    /// <summary>Called with the reach of every column when the underground is laid (the tests read it; nothing else sets it).</summary>
    internal Action<int[]>? Observer { get; init; }

    /// <summary>
    /// The threads the laying uses, clamped to 1 to <see cref="MaxThreads"/>. Two is the game's worker rule (the owner's limit on the machine; it was eight before B24a); the result is
    /// the same at any count (the tests check one and two).
    /// </summary>
    internal int Threads { get; init; } = MaxThreads;

    /// <summary>The degree the laying runs at for a requested thread count: at least one and at most <see cref="MaxThreads"/>.</summary>
    /// <param name="requested">The requested count.</param>
    internal static int DegreeFor(int requested) => Math.Clamp(requested, 1, MaxThreads);

    /// <summary>
    /// Lays the underground into the stone of the grid. The chunks are worked out on at most <see cref="MaxThreads"/> threads (every one is a pure function of its coordinates, the seed and the
    /// reach, so the result is the same on any number of threads) and then written back one by one.
    /// </summary>
    /// <param name="grid">The valley's grid as built (its chunk columns start at 0, like the world's).</param>
    /// <param name="ground">The surface.</param>
    public void Lay(VoxelGrid grid, ValleyGround ground)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(ground);
        var reach = Reach(ground);
        Observer?.Invoke(reach);
        var sizeX = ground.Layout.SizeX;
        var limits = grid.Limits;
        var (chunksX, chunksY, chunksZ) = (limits.EndChunkX, limits.EndChunkY, limits.EndChunkZ); // the valley's grid starts at chunk 0

        // The grid's chunk table is read here, on one thread (its lookup keeps a one-chunk cache that threads must not share).
        var chunks = new VoxelChunk?[chunksX * chunksY * chunksZ];
        for (var cy = 0; cy < chunksY; cy++)
        {
            for (var cz = 0; cz < chunksZ; cz++)
            {
                for (var cx = 0; cx < chunksX; cx++)
                {
                    if (grid.TryGetChunk(new VoxelChunkCoord(cx, cy, cz), out var chunk) && !chunk.IsEmpty)
                    {
                        chunks[(((cy * chunksZ) + cz) * chunksX) + cx] = chunk;
                    }
                }
            }
        }

        var results = new BlockId[]?[chunks.Length];
        var options = new ParallelOptions { MaxDegreeOfParallelism = DegreeFor(Threads) };
        _ = Parallel.For(0, chunksX * chunksZ, options, () => new Scratch(), (column, _, scratch) =>
        {
            var (cx, cz) = (column % chunksX, column / chunksX);
            var highest = int.MinValue;
            for (var lz = 0; lz < VoxelChunk.Size; lz++)
            {
                for (var lx = 0; lx < VoxelChunk.Size; lx++)
                {
                    highest = Math.Max(highest, reach[((((cz << VoxelChunk.Shift) + lz) * sizeX) + (cx << VoxelChunk.Shift)) + lx]);
                }
            }

            for (var cy = 0; cy < chunksY && (cy << VoxelChunk.Shift) <= highest; cy++)
            {
                var at = (((cy * chunksZ) + cz) * chunksX) + cx;
                if (chunks[at] is { } chunk)
                {
                    results[at] = LayChunk(chunk, cx, cy, cz, reach, sizeX, highest, scratch);
                }
            }

            return scratch;
        }, static _ => { });

        for (var i = 0; i < chunks.Length; i++)
        {
            if (results[i] is { } blocks)
            {
                grid.ReplaceBlocks(chunks[i]!, blocks);
            }
        }

    }

    // The new blocks of one chunk, or null when the underground leaves it as it is.
    private BlockId[]? LayChunk(VoxelChunk chunk, int cx, int cy, int cz, int[] reach, int sizeX, int highest, Scratch scratch)
    {
        var (minX, minY, minZ) = (cx << VoxelChunk.Shift, cy << VoxelChunk.Shift, cz << VoxelChunk.Shift);
        var work = scratch.Work;
        chunk.Blocks.CopyTo(work);
        var changed = false;

        // The rock layers: the stone becomes the layer's rock.
        _ = _world.Strata.Fill(minX, minY, minZ, scratch.Rocks);
        LayRock(reach, sizeX, minX, minY, minZ, work, scratch.Rocks, _world.Strata.Stone, ref changed);

        // The caves, and the lava in the deepest of them.
        if (minY <= Math.Min(highest, _world.Caves.TopY))
        {
            _world.Caves.Fill(minX, minY, minZ, scratch.Kinds);
            CutCaves(reach, sizeX, minX, minY, minZ, work, scratch.Kinds, _world.Caves.TopY, _world.LavaLevel, ref changed);
        }

        // The ores, into the rock that is left.
        var ores = _world.Ores;
        if (minY + VoxelChunk.Size - 1 >= ores.MinY && minY <= ores.MaxY)
        {
            foreach (var vein in ores.Veins(cx, cy, cz))
            {
                foreach (var cell in vein.Cells)
                {
                    var (lx, lz, ly) = (cell & (VoxelChunk.Size - 1), (cell >> VoxelChunk.Shift) & (VoxelChunk.Size - 1), cell >> (2 * VoxelChunk.Shift));
                    if (minY + ly <= reach[(((minZ + lz) * sizeX) + minX) + lx] && ores.Replaces(vein.Row, work[cell]))
                    {
                        work[cell] = vein.Block;
                        changed = true;
                    }
                }
            }
        }

        return changed ? [.. work] : null;
    }

    private sealed class Scratch
    {
        public BlockId[] Work { get; } = new BlockId[VoxelChunk.Volume];

        public BlockId[] Rocks { get; } = new BlockId[VoxelChunk.Volume];

        public CaveKind[] Kinds { get; } = new CaveKind[VoxelChunk.Volume];
    }

    // The rock layers: stone that lies at or under the reach becomes the rock the strata give there.
    private static void LayRock(int[] reach, int sizeX, int minX, int minY, int minZ, BlockId[] work, BlockId[] rocks, BlockId stone, ref bool changed)
    {
        var index = 0;
        for (var ly = 0; ly < VoxelChunk.Size; ly++)
        {
            for (var lz = 0; lz < VoxelChunk.Size; lz++)
            {
                for (var lx = 0; lx < VoxelChunk.Size; lx++, index++)
                {
                    if (rocks[index] != stone && work[index] == stone && minY + ly <= reach[(((minZ + lz) * sizeX) + minX) + lx])
                    {
                        work[index] = rocks[index];
                        changed = true;
                    }
                }
            }
        }
    }

    private void CutCaves(int[] reach, int sizeX, int minX, int minY, int minZ, BlockId[] work, CaveKind[] kinds, int caveTop, int lavaLevel, ref bool changed)
    {
        var index = 0;
        for (var ly = 0; ly < VoxelChunk.Size; ly++)
        {
            var y = minY + ly;
            for (var lz = 0; lz < VoxelChunk.Size; lz++)
            {
                for (var lx = 0; lx < VoxelChunk.Size; lx++, index++)
                {
                    if (kinds[index] == CaveKind.None || y > caveTop || y > reach[(((minZ + lz) * sizeX) + minX) + lx])
                    {
                        continue;
                    }

                    var block = work[index];
                    if (block != BlockId.Air && block != BlockId.Water && block != _world.Bedrock)
                    {
                        work[index] = y <= lavaLevel ? _world.Lava : BlockId.Air;
                        changed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The highest y at which a block of each column may change, row by row (z), x fastest: <see cref="int.MinValue"/> where nothing may. It is the lowest of the distances to everything the
    /// valley's surface has, and it is what the tests check the changed blocks against.
    /// </summary>
    /// <param name="ground">The surface.</param>
    internal int[] Reach(ValleyGround ground)
    {
        var (sizeX, sizeZ) = (ground.Layout.SizeX, ground.Layout.SizeZ);
        var terrain = ground.Terrain;
        var count = sizeX * sizeZ;
        var none = int.MaxValue;

        // The lowest air cell of each column: the cell over the ground, or a prop's hollow in it; and, for the water, the lowest water cell.
        var air = new int[count];
        var water = new int[count];
        Array.Fill(water, none);
        for (var i = 0; i < count; i++)
        {
            air[i] = Math.Min(terrain.Height[i] + 1, ground.AirLow[i]);
            if (terrain.WaterTop[i] > terrain.Height[i])
            {
                water[i] = terrain.Height[i] + 1;
            }
        }

        foreach (var fall in terrain.Falls)
        {
            var i = (fall.Z * sizeX) + fall.X;
            air[i] = Math.Min(air[i], fall.Bottom);
            water[i] = Math.Min(water[i], fall.Bottom);
        }

        var reach = new int[count];
        Cap(reach, air, sizeX, sizeZ, _spec.SurfaceDistance, first: true);
        Cap(reach, water, sizeX, sizeZ, _spec.WaterDistance, first: false);

        // The features of the surface, each with the lowest y it stands at in a column.
        var routes = new int[count];
        var paths = new int[count];
        var props = new int[count];
        var village = new int[count];
        var pass = new int[count];
        Array.Fill(routes, none);
        Array.Fill(paths, none);
        Array.Fill(props, none);
        Array.Fill(village, none);
        Array.Fill(pass, none);
        for (var i = 0; i < count; i++)
        {
            var flags = terrain.Flags[i];
            if ((flags & Terrain.PathFlag) != 0)
            {
                paths[i] = terrain.Height[i];
            }
            else if ((flags & Terrain.RouteFlag) != 0)
            {
                routes[i] = terrain.Height[i];
            }

            if (ground.PropLow[i] != none)
            {
                props[i] = Math.Min(ground.PropLow[i], terrain.Height[i]);
            }
        }

        var layout = ground.Layout;
        void Mark(int[] into, int x, int z, int y)
        {
            if (x >= 0 && x < sizeX && z >= 0 && z < sizeZ)
            {
                var i = (z * sizeX) + x;
                into[i] = Math.Min(into[i], Math.Min(y, terrain.Height[i]));
            }
        }

        foreach (var point in ground.Points.Values)
        {
            Mark(routes, point.X, point.Z, point.Y);
        }

        foreach (var npc in ground.Npcs)
        {
            Mark(routes, npc.Feet.X, npc.Feet.Z, npc.Feet.Y);
        }

        Mark(routes, layout.Spawn.X, layout.Spawn.Z, int.MaxValue);
        if (layout.Sites.FirstOrDefault(site => site.Name == _spec.VillageSite) is { } hub)
        {
            var radius = hub.Radius + hub.Blend;
            for (var z = Math.Max(0, hub.Z - radius); z <= Math.Min(sizeZ - 1, hub.Z + radius); z++)
            {
                for (var x = Math.Max(0, hub.X - radius); x <= Math.Min(sizeX - 1, hub.X + radius); x++)
                {
                    village[(z * sizeX) + x] = terrain.Height[(z * sizeX) + x];
                }
            }
        }

        foreach (var name in _spec.PassPaths)
        {
            if (layout.Paths.FirstOrDefault(path => path.Name == name) is { } path)
            {
                MarkPath(layout, path, pass, terrain);
            }
        }

        foreach (var name in _spec.PassProps)
        {
            if (ground.PropCells.TryGetValue(name, out var cells))
            {
                foreach (var cell in cells)
                {
                    Mark(pass, cell.X, cell.Z, cell.Y);
                }
            }
        }

        Cap(reach, routes, sizeX, sizeZ, _spec.SiteMargin, first: false);
        Cap(reach, paths, sizeX, sizeZ, _spec.PathMargin, first: false);
        Cap(reach, props, sizeX, sizeZ, _spec.PropMargin, first: false);
        Cap(reach, village, sizeX, sizeZ, _spec.VillageMargin, first: false);
        Cap(reach, pass, sizeX, sizeZ, _spec.PassMargin, first: false);

        // The valley's edge: the land outside is procedural, with its own caves.
        for (var z = 0; z < sizeZ; z++)
        {
            for (var x = 0; x < sizeX; x++)
            {
                if (x < _spec.BorderMargin || z < _spec.BorderMargin || x >= sizeX - _spec.BorderMargin || z >= sizeZ - _spec.BorderMargin)
                {
                    reach[(z * sizeX) + x] = int.MinValue;
                }
            }
        }

        return reach;
    }

    // The columns within the path's half width (and one more) of its line from the first site through the waypoints to the last.
    private static void MarkPath(SceneLayout layout, PathSpec path, int[] into, Terrain terrain)
    {
        var line = new List<(int X, int Z)>();
        if (layout.Sites.FirstOrDefault(s => s.Name == path.From) is { } from)
        {
            line.Add((from.X, from.Z));
        }

        line.AddRange(path.Via);
        if (layout.Sites.FirstOrDefault(s => s.Name == path.To) is { } to)
        {
            line.Add((to.X, to.Z));
        }

        var reach = path.HalfWidth + 1;
        for (var k = 0; k + 1 < line.Count; k++)
        {
            var (a, b) = (line[k], line[k + 1]);
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var length2 = ((long)dx * dx) + ((long)dz * dz);
            for (var z = Math.Max(0, Math.Min(a.Z, b.Z) - reach); z <= Math.Min(terrain.SizeZ - 1, Math.Max(a.Z, b.Z) + reach); z++)
            {
                for (var x = Math.Max(0, Math.Min(a.X, b.X) - reach); x <= Math.Min(terrain.SizeX - 1, Math.Max(a.X, b.X) + reach); x++)
                {
                    // The squared distance of the column from the segment, in integers.
                    var t = length2 == 0 ? 0 : Math.Clamp((((long)(x - a.X) * dx) + ((long)(z - a.Z) * dz)) * 1024 / length2, 0, 1024);
                    var px = (a.X * 1024L) + (t * dx);
                    var pz = (a.Z * 1024L) + (t * dz);
                    var ex = (x * 1024L) - px;
                    var ez = (z * 1024L) - pz;
                    if (((ex * ex) + (ez * ez)) <= (long)reach * reach * 1024 * 1024)
                    {
                        var i = (z * terrain.SizeX) + x;
                        into[i] = Math.Min(into[i], terrain.Height[i]);
                    }
                }
            }
        }
    }

    // Lowers the reach of every column that has a feature within a cube of the margin to the feature's lowest y less the margin (and one, so the block is farther than the margin).
    private static void Cap(int[] reach, int[] lowest, int sizeX, int sizeZ, int margin, bool first)
    {
        var features = 0;
        foreach (var value in lowest)
        {
            if (value != int.MaxValue)
            {
                features++;
            }
        }

        var side = (2 * margin) + 1;
        if (!first && (long)features * side * side < (long)lowest.Length * 4)
        {
            // Few features: stamp each one's square instead of sliding a window over every column.
            for (var i = 0; i < lowest.Length; i++)
            {
                if (lowest[i] == int.MaxValue)
                {
                    continue;
                }

                var cap = lowest[i] - margin - 1;
                var (x, z) = (i % sizeX, i / sizeX);
                for (var cz = Math.Max(0, z - margin); cz <= Math.Min(sizeZ - 1, z + margin); cz++)
                {
                    for (var cx = Math.Max(0, x - margin); cx <= Math.Min(sizeX - 1, x + margin); cx++)
                    {
                        var at = (cz * sizeX) + cx;
                        if (cap < reach[at])
                        {
                            reach[at] = cap;
                        }
                    }
                }
            }

            return;
        }

        var low = WindowMin(lowest, sizeX, sizeZ, margin);
        for (var i = 0; i < reach.Length; i++)
        {
            var cap = low[i] == int.MaxValue ? int.MaxValue : low[i] - margin - 1;
            reach[i] = first ? cap : Math.Min(reach[i], cap);
        }
    }

    /// <summary>The smallest value in the (2 radius + 1) square round each column (clipped at the grid's edge), in two passes of a sliding window.</summary>
    /// <param name="values">The values, row by row.</param>
    /// <param name="sizeX">The width.</param>
    /// <param name="sizeZ">The depth.</param>
    /// <param name="radius">The radius.</param>
    internal static int[] WindowMin(int[] values, int sizeX, int sizeZ, int radius)
    {
        var across = new int[values.Length];
        var result = new int[values.Length];
        for (var z = 0; z < sizeZ; z++)
        {
            Slide(values, z * sizeX, across, z * sizeX, sizeX, radius);
        }

        var column = new int[sizeZ];
        var output = new int[sizeZ];
        for (var x = 0; x < sizeX; x++)
        {
            for (var z = 0; z < sizeZ; z++)
            {
                column[z] = across[(z * sizeX) + x];
            }

            Slide(column, 0, output, 0, sizeZ, radius);
            for (var z = 0; z < sizeZ; z++)
            {
                result[(z * sizeX) + x] = output[z];
            }
        }

        return result;
    }

    // out[i] = min(input[i - radius .. i + radius]) clipped to the line, by the block prefix and suffix minima of a window's width (van Herk and Gil-Werman).
    private static void Slide(int[] input, int from, int[] output, int to, int n, int radius)
    {
        var w = (2 * radius) + 1;
        var padded = n + (2 * radius);
        var prefix = new int[padded];
        var suffix = new int[padded];
        int Value(int p) => p >= radius && p < radius + n ? input[from + p - radius] : int.MaxValue;
        for (var p = 0; p < padded; p++)
        {
            prefix[p] = p % w == 0 ? Value(p) : Math.Min(prefix[p - 1], Value(p));
        }

        for (var p = padded - 1; p >= 0; p--)
        {
            suffix[p] = p == padded - 1 || (p + 1) % w == 0 ? Value(p) : Math.Min(suffix[p + 1], Value(p));
        }

        for (var i = 0; i < n; i++)
        {
            output[to + i] = Math.Min(suffix[i], prefix[i + w - 1]);
        }
    }
}
