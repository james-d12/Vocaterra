namespace Vocaterra.Simulation.Voxel;

/// <summary>
/// Builds a <see cref="Scene"/> from a <see cref="SceneLayout"/> and a world seed. It is a pure function of the two, in integers only: the
/// <see cref="TerrainGenerator"/> makes the ground (mountains, river, lake, waterfall, paths, level sites) from the layout's parameters and the
/// seed's noise; the builder fills the grid, builds the props and stamps at their sites, places the NPCs and the spawn, and plants the forest from
/// <see cref="WorldRng"/> (the pass name <c>trees</c>). A different seed changes the land and the forest and never moves a site, a point, an NPC or
/// the spawn, and nothing is built where a site says there is a prop.
/// </summary>
public static class SceneBuilder
{
    /// <summary>The name of the forest's random stream (<see cref="WorldRng.For"/>); renaming it changes every golden.</summary>
    public const string TreePassName = "trees";

    private const string ForestPassName = "forest";

    /// <summary>Builds the scene of <paramref name="layout"/> for <paramref name="seed"/>.</summary>
    /// <exception cref="SceneLayoutException">A prop does not fit its footprint, a position is out of the grid or on water, or a name repeats.</exception>
    public static Scene Build(SceneLayout layout, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var builder = new Builder(layout);
        return builder.Run(seed);
    }

    private sealed class Builder
    {
        private readonly SceneLayout _layout;
        private readonly VoxelGrid _grid;
        private readonly bool[] _reserved;
        private readonly SortedDictionary<string, BlockPos> _points = new(StringComparer.Ordinal);
        private Terrain _terrain = new(0, 0);
        private TerrainNoise _forest = new(0, ForestPassName);

        public Builder(SceneLayout layout)
        {
            _layout = layout;
            _grid = new VoxelGrid(layout.SizeX, layout.SizeY, layout.SizeZ);
            _reserved = new bool[layout.SizeX * layout.SizeZ];
        }

        public Scene Run(ulong seed)
        {
            _terrain = TerrainGenerator.Generate(_layout, seed);
            _forest = new TerrainNoise(seed, ForestPassName);
            FillTerrain();
            foreach (var prop in _layout.Props)
            {
                BuildProp(prop);
            }

            foreach (var site in _layout.Sites)
            {
                AddPoint(site.Name, site.X, site.Z);
            }

            foreach (var point in _layout.Points)
            {
                AddPoint(point.Name, point.X, point.Z);
            }

            var spawnFeet = FeetCell(_layout.Spawn.X, _layout.Spawn.Z, "spawn");
            Reserve(_layout.Spawn.X, _layout.Spawn.Z);
            var npcs = new List<NpcPlacement>();
            foreach (var npc in _layout.Npcs)
            {
                npcs.Add(new NpcPlacement(npc.Name, FeetCell(npc.X, npc.Z, $"npc '{npc.Name}'"), npc.Yaw));
                Reserve(npc.X, npc.Z);
            }

            foreach (var point in _points.Values)
            {
                Reserve(point.X, point.Z);
            }

            var trees = PlantForest(seed);
            return new Scene(_layout.Name, seed, _grid, new SpawnPoint(spawnFeet, _layout.Spawn.Yaw), npcs, _points, trees, _layout.WaterLevel);
        }

        // ---------------------------------------------------------------- the ground

        private int HeightAt(int x, int z) => _terrain.Height[(z * _layout.SizeX) + x];

        private bool IsWater(int x, int z) => _terrain.IsWater(x, z);

        private void FillTerrain()
        {
            var spec = _layout.Terrain;
            var w = _layout.SizeX;
            var d = _layout.SizeZ;
            var noise = _forest;
            for (var z = 0; z < d; z++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = (z * w) + x;
                    var h = _terrain.Height[i];
                    var slope = Math.Max(
                        Math.Max(Math.Abs(h - HeightAt(Math.Max(x - 1, 0), z)), Math.Abs(h - HeightAt(Math.Min(x + 1, w - 1), z))),
                        Math.Max(Math.Abs(h - HeightAt(x, Math.Max(z - 1, 0))), Math.Abs(h - HeightAt(x, Math.Min(z + 1, d - 1)))));
                    var flags = _terrain.Flags[i];
                    if ((flags & Terrain.RouteFlag) != 0)
                    {
                        slope = 0; // the sites' ground and the paths are never bare rock, whatever the land beside them does (and so never depend on the seed)
                    }

                    BlockId top;
                    BlockId sub;
                    if (_terrain.IsWater(x, z) || (NearWater(x, z) && (flags & Terrain.ShoreFlag) != 0 && slope <= 3))
                    {
                        top = BlockId.Sand;
                        sub = BlockId.Sand;
                    }
                    else if ((flags & Terrain.PathFlag) != 0)
                    {
                        top = BlockId.Path;
                        sub = BlockId.Dirt;
                    }
                    else if (h >= spec.SnowLine + (noise.Column(1, x, z) % 7) - 3 && slope <= 3)
                    {
                        top = BlockId.Snow;
                        sub = BlockId.Stone;
                    }
                    else if (h >= spec.RockLine || slope >= 5)
                    {
                        top = BlockId.Stone;
                        sub = BlockId.Stone;
                    }
                    else
                    {
                        top = BlockId.Grass;
                        sub = BlockId.Dirt;
                    }

                    _grid.FillColumn(x, z, 0, h - 4, BlockId.Stone);
                    _grid.FillColumn(x, z, h - 3, h - 1, sub);
                    _grid.FillColumn(x, z, h, h, top);
                    if (_terrain.IsWater(x, z))
                    {
                        _grid.FillColumn(x, z, h + 1, _terrain.WaterTop[i], BlockId.Water);
                    }

                    if ((flags & Terrain.RouteFlag) != 0)
                    {
                        _reserved[i] = true;
                    }
                }
            }
        }

        // Whether the column, or one next to it, holds water (the bed and the bank are sand).
        private bool NearWater(int x, int z)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var cx = x + dx;
                    var cz = z + dz;
                    if (cx >= 0 && cx < _layout.SizeX && cz >= 0 && cz < _layout.SizeZ && IsWater(cx, cz))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- props

        private void Reserve(int x, int z)
        {
            if (x >= 0 && x < _layout.SizeX && z >= 0 && z < _layout.SizeZ)
            {
                _reserved[(z * _layout.SizeX) + x] = true;
            }
        }

        private void Put(int x, int y, int z, BlockId block, string where)
        {
            if (!_grid.InBounds(x, y, z))
            {
                throw new SceneLayoutException($"{where}: block ({x}, {y}, {z}) lies outside the grid.");
            }

            _grid.SetBlock(x, y, z, block);
        }

        private void CheckColumn(int x, int z, string where)
        {
            if (x < 0 || x >= _layout.SizeX || z < 0 || z >= _layout.SizeZ)
            {
                throw new SceneLayoutException($"{where}: column ({x}, {z}) lies outside the grid.");
            }
        }

        private void AddPoint(string name, int x, int z)
        {
            CheckColumn(x, z, $"point '{name}'");
            var y = IsWater(x, z) ? _terrain.WaterTop[(z * _layout.SizeX) + x] : HeightAt(x, z);
            if (!_points.TryAdd(name, new BlockPos(x, y, z)))
            {
                throw new SceneLayoutException($"point '{name}' is defined twice.");
            }
        }

        private BlockPos FeetCell(int x, int z, string where)
        {
            CheckColumn(x, z, where);
            if (IsWater(x, z))
            {
                throw new SceneLayoutException($"{where}: column ({x}, {z}) is in the water.");
            }

            var feet = new BlockPos(x, HeightAt(x, z) + 1, z);
            if (_grid.IsSolid(feet.X, feet.Y, feet.Z) || _grid.IsSolid(feet.X, feet.Y + 1, feet.Z))
            {
                throw new SceneLayoutException($"{where}: there is a solid block where the feet or head would be at ({x}, {feet.Y}, {z}).");
            }

            return feet;
        }

        /// <summary>Checks that every column of the footprint exists, is dry and has the same height; returns that height.</summary>
        private int FlatHeight(int x0, int z0, int width, int depth, string where)
        {
            CheckColumn(x0, z0, where);
            CheckColumn(x0 + width - 1, z0 + depth - 1, where);
            var h = HeightAt(x0, z0);
            for (var z = z0; z < z0 + depth; z++)
            {
                for (var x = x0; x < x0 + width; x++)
                {
                    if (HeightAt(x, z) != h || IsWater(x, z))
                    {
                        throw new SceneLayoutException($"{where}: the footprint is not flat dry ground at ({x}, {z}).");
                    }

                    Reserve(x, z);
                }
            }

            return h;
        }

        private void BuildProp(PropSpec prop)
        {
            var where = $"prop '{prop.Type}' at ({prop.X}, {prop.Z})";
            switch (prop.Type)
            {
                case "workbench":
                    BuildWorkbench(prop, where);
                    break;
                case "fire_ring":
                    BuildFireRing(prop, where);
                    break;
                case "stall":
                    BuildStall(prop, where);
                    break;
                case "house":
                    BuildHouse(prop, where);
                    break;
                case "signpost":
                    {
                        var h = FlatHeight(prop.X, prop.Z, 1, 1, where);
                        Put(prop.X, h + 1, prop.Z, BlockId.Fence, where);
                        Put(prop.X, h + 2, prop.Z, BlockId.Fence, where);
                        Put(prop.X, h + 3, prop.Z, BlockId.Sign, where);
                        Register(prop, prop.X, prop.Z);
                        break;
                    }

                case "bush":
                case "berry_bush":
                    {
                        var h = FlatHeight(prop.X, prop.Z, 1, 1, where);
                        Put(prop.X, h + 1, prop.Z, prop.Type == "bush" ? BlockId.Bush : BlockId.BerryBush, where);
                        Register(prop, prop.X, prop.Z);
                        break;
                    }

                case "tree":
                    {
                        CheckColumn(prop.X, prop.Z, where);
                        PlantTree(prop.X, prop.Z, prop.Height > 0 ? prop.Height : 4, where);
                        Register(prop, prop.X, prop.Z);
                        break;
                    }

                case "bridge":
                    BuildBridge(prop, where);
                    break;
                case "paving":
                    BuildPaving(prop, where);
                    break;
                case "stamp":
                    BuildStamp(prop, where);
                    break;
                default:
                    throw new SceneLayoutException($"{where}: unknown prop type.");
            }
        }

        private void Register(PropSpec prop, int x, int z)
        {
            if (prop.Name is not null)
            {
                AddPoint(prop.Name, x, z);
            }
        }

        private void BuildWorkbench(PropSpec prop, string where)
        {
            var length = prop.Length > 0 ? prop.Length : 2;
            var h = prop.AlongZ ? FlatHeight(prop.X, prop.Z, 1, length, where) : FlatHeight(prop.X, prop.Z, length, 1, where);
            for (var i = 0; i < length; i++)
            {
                Put(prop.X + (prop.AlongZ ? 0 : i), h + 1, prop.Z + (prop.AlongZ ? i : 0), BlockId.Table, where);
            }

            Register(prop, prop.X, prop.Z);
        }

        private void BuildFireRing(PropSpec prop, string where)
        {
            var h = FlatHeight(prop.X - 1, prop.Z - 1, 3, 3, where);
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx != 0 || dz != 0)
                    {
                        Put(prop.X + dx, h + 1, prop.Z + dz, BlockId.FireRing, where);
                    }
                }
            }

            Register(prop, prop.X, prop.Z);
        }

        private void BuildStall(PropSpec prop, string where)
        {
            var width = prop.Width > 0 ? prop.Width : 5;
            var depth = prop.Depth > 0 ? prop.Depth : 2;
            var front = prop.Facing ?? Direction.South;
            var frontLength = front is Direction.North or Direction.South ? width : depth;
            if (width < 2 || depth < 2 || frontLength < 3)
            {
                throw new SceneLayoutException($"{where}: a stall needs a counter side of at least 3 blocks and a size of at least 2 x 2.");
            }

            var h = FlatHeight(prop.X, prop.Z, width, depth, where);
            var x1 = prop.X + width - 1;
            var z1 = prop.Z + depth - 1;
            for (var z = prop.Z; z <= z1; z++)
            {
                for (var x = prop.X; x <= x1; x++)
                {
                    var cornerX = x == prop.X || x == x1;
                    var cornerZ = z == prop.Z || z == z1;
                    if (cornerX && cornerZ)
                    {
                        for (var y = h + 1; y <= h + 3; y++)
                        {
                            Put(x, y, z, BlockId.Fence, where);
                        }
                    }
                    else if (OnSide(front, x, z, prop.X, prop.Z, x1, z1))
                    {
                        Put(x, h + 1, z, BlockId.Stall, where);
                    }

                    Put(x, h + 4, z, BlockId.Roof, where);
                }
            }

            // The reference cell is the middle of the counter.
            var (rx, rz) = front switch
            {
                Direction.North => (prop.X + (width / 2), prop.Z),
                Direction.South => (prop.X + (width / 2), z1),
                Direction.West => (prop.X, prop.Z + (depth / 2)),
                _ => (x1, prop.Z + (depth / 2)),
            };
            Register(prop, rx, rz);
        }

        private static bool OnSide(Direction side, int x, int z, int x0, int z0, int x1, int z1) => side switch
        {
            Direction.North => z == z0,
            Direction.South => z == z1,
            Direction.West => x == x0,
            _ => x == x1,
        };

        private void BuildHouse(PropSpec prop, string where)
        {
            var width = prop.Width > 0 ? prop.Width : 5;
            var depth = prop.Depth > 0 ? prop.Depth : 5;
            var wall = prop.Height > 0 ? prop.Height : 3;
            var door = prop.Facing ?? Direction.South;
            if (width < 3 || depth < 3 || wall < 2)
            {
                throw new SceneLayoutException($"{where}: a house needs a size of at least 3 x 3 and walls at least 2 high.");
            }

            var h = FlatHeight(prop.X, prop.Z, width, depth, where);
            var x1 = prop.X + width - 1;
            var z1 = prop.Z + depth - 1;
            var (doorX, doorZ) = door switch
            {
                Direction.North => (prop.X + (width / 2), prop.Z),
                Direction.South => (prop.X + (width / 2), z1),
                Direction.West => (prop.X, prop.Z + (depth / 2)),
                _ => (x1, prop.Z + (depth / 2)),
            };
            for (var z = prop.Z; z <= z1; z++)
            {
                for (var x = prop.X; x <= x1; x++)
                {
                    Put(x, h, z, BlockId.Planks, where);
                    var perimeter = x == prop.X || x == x1 || z == prop.Z || z == z1;
                    if (perimeter)
                    {
                        var isDoor = x == doorX && z == doorZ;
                        for (var y = h + 1; y <= h + wall; y++)
                        {
                            Put(x, y, z, isDoor && y <= h + 2 ? BlockId.Door : BlockId.Wall, where);
                        }
                    }

                    Put(x, h + wall + 1, z, BlockId.Roof, where);
                }
            }

            Register(prop, prop.X + (width / 2), prop.Z + (depth / 2));
        }

        private void BuildBridge(PropSpec prop, string where)
        {
            var dir = prop.Facing ?? Direction.South;
            var length = prop.Length > 0 ? prop.Length : 9;
            var lanes = prop.Width > 0 ? prop.Width : 3;
            CheckColumn(prop.X, prop.Z, where);
            if (IsWater(prop.X, prop.Z))
            {
                throw new SceneLayoutException($"{where}: the bridge must start on dry ground.");
            }

            var deckY = HeightAt(prop.X, prop.Z);
            var (ax, az) = dir switch
            {
                Direction.North => (0, -1),
                Direction.East => (1, 0),
                Direction.South => (0, 1),
                _ => (-1, 0),
            };
            var (sx, sz) = (az != 0 ? 1 : 0, ax != 0 ? 1 : 0); // across the span
            for (var i = 0; i < length; i++)
            {
                for (var lane = 0; lane < lanes; lane++)
                {
                    var offset = lane - (lanes / 2);
                    var x = prop.X + (ax * i) + (sx * offset);
                    var z = prop.Z + (az * i) + (sz * offset);
                    CheckColumn(x, z, where);
                    Reserve(x, z);
                    if (!_grid.IsSolid(x, deckY, z))
                    {
                        Put(x, deckY, z, BlockId.Bridge, where);
                        if (lane == 0 || lane == lanes - 1)
                        {
                            Put(x, deckY + 1, z, BlockId.Fence, where);
                        }
                    }
                }
            }

            Register(prop, prop.X + (ax * (length / 2)), prop.Z + (az * (length / 2)));
        }

        // A rectangle of ground laid with one block (the village square): only the top block changes.
        private void BuildPaving(PropSpec prop, string where)
        {
            var width = prop.Width > 0 ? prop.Width : 1;
            var depth = prop.Depth > 0 ? prop.Depth : 1;
            CheckColumn(prop.X, prop.Z, where);
            CheckColumn(prop.X + width - 1, prop.Z + depth - 1, where);
            for (var z = prop.Z; z < prop.Z + depth; z++)
            {
                for (var x = prop.X; x < prop.X + width; x++)
                {
                    if (!IsWater(x, z))
                    {
                        Put(x, HeightAt(x, z), z, prop.Block, where);
                    }

                    Reserve(x, z);
                }
            }

            Register(prop, prop.X + (width / 2), prop.Z + (depth / 2));
        }

        // A small structure drawn in the layout (a tent), turned to face a side and set on level ground.
        private void BuildStamp(PropSpec prop, string where)
        {
            var stamp = prop.Stamp is { } name && _layout.Stamps.TryGetValue(name, out var found)
                ? found
                : throw new SceneLayoutException($"{where}: there is no stamp '{prop.Stamp}'.");
            var drawnWidth = stamp.Layers.Max(static layer => layer.Count == 0 ? 0 : layer.Max(static row => row.Length));
            var drawnDepth = stamp.Layers.Max(static layer => layer.Count);
            var facing = prop.Facing ?? Direction.North;
            var turned = facing is Direction.East or Direction.West;
            var width = turned ? drawnDepth : drawnWidth;
            var depth = turned ? drawnWidth : drawnDepth;
            var h = FlatHeight(prop.X, prop.Z, width, depth, where);
            for (var layer = 0; layer < stamp.Layers.Count; layer++)
            {
                var rows = stamp.Layers[layer];
                for (var r = 0; r < rows.Count; r++)
                {
                    for (var c = 0; c < rows[r].Length; c++)
                    {
                        if (rows[r][c] == ' ')
                        {
                            continue;
                        }

                        var (dx, dz) = facing switch
                        {
                            Direction.North => (c, r),
                            Direction.East => (drawnDepth - 1 - r, c),
                            Direction.South => (drawnWidth - 1 - c, drawnDepth - 1 - r),
                            _ => (r, drawnWidth - 1 - c),
                        };
                        Put(prop.X + dx, h + 1 + layer, prop.Z + dz, stamp.Legend[rows[r][c]], where);
                    }
                }
            }

            Register(prop, prop.X + (width / 2), prop.Z + (depth / 2));
        }

        // ---------------------------------------------------------------- the forest

        private void PlantTree(int x, int z, int trunk, string where)
        {
            var baseY = HeightAt(x, z);
            var top = baseY + trunk;
            for (var y = baseY + 1; y <= top; y++)
            {
                Put(x, y, z, BlockId.Trunk, where);
            }

            for (var dy = -1; dy <= 2; dy++)
            {
                var y = top + dy;
                for (var dz = -2; dz <= 2; dz++)
                {
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        var inside = dy <= 0
                            ? Math.Abs(dx) < 2 || Math.Abs(dz) < 2
                            : dy == 1 ? Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1 : Math.Abs(dx) + Math.Abs(dz) <= 1;
                        if (inside && _grid.InBounds(x + dx, y, z + dz) && _grid.GetBlock(x + dx, y, z + dz) == BlockId.Air)
                        {
                            _grid.SetBlock(x + dx, y, z + dz, BlockId.Leaves);
                        }
                    }
                }
            }
        }

        // One tree is tried in every square of the ground at a jittered place. The draws are made for every square whether or not a tree is kept, so the stream
        // stays in step and a change to the margins moves only the trees it touches.
        private int PlantForest(ulong seed)
        {
            var spec = _layout.Trees;
            if (spec.Chance <= 0)
            {
                return 0;
            }

            var sizeX = _layout.SizeX;
            var sizeZ = _layout.SizeZ;

            // Every column within the margin of a route cell is closed to trunks.
            var closed = new bool[sizeX * sizeZ];
            for (var z = 0; z < sizeZ; z++)
            {
                for (var x = 0; x < sizeX; x++)
                {
                    if (!_reserved[(z * sizeX) + x])
                    {
                        continue;
                    }

                    for (var dz = -spec.Margin; dz <= spec.Margin; dz++)
                    {
                        for (var dx = -spec.Margin; dx <= spec.Margin; dx++)
                        {
                            var cx = x + dx;
                            var cz = z + dz;
                            if (cx >= 0 && cx < sizeX && cz >= 0 && cz < sizeZ)
                            {
                                closed[(cz * sizeX) + cx] = true;
                            }
                        }
                    }
                }
            }

            var rng = WorldRng.For(seed, TreePassName);
            var planted = new bool[sizeX * sizeZ];
            var count = 0;
            for (var cz = 0; cz < sizeZ; cz += spec.Cell)
            {
                for (var cx = 0; cx < sizeX; cx += spec.Cell)
                {
                    var x = cx + rng.NextInt(spec.Cell);
                    var z = cz + rng.NextInt(spec.Cell);
                    var roll = rng.NextInt(100);
                    var trunk = rng.NextInt(spec.MinTrunk, spec.MaxTrunk + 1);
                    if (x < 2 || z < 2 || x >= sizeX - 2 || z >= sizeZ - 2 || closed[(z * sizeX) + x])
                    {
                        continue;
                    }

                    var i = (z * sizeX) + x;
                    var h = _terrain.Height[i];
                    if (h > spec.TreeLine || h + trunk + 3 >= _layout.SizeY || _terrain.IsWater(x, z) || _grid.GetBlock(x, h, z) != BlockId.Grass || !Gentle(x, z))
                    {
                        continue;
                    }

                    // Thinner with height, and a few clearings where the slow noise is low.
                    var chance = spec.Chance * (100 - Math.Clamp((h - 56) * 3, 0, 80)) / 100;
                    if (_forest.Value(8, x, z, 40) < 700)
                    {
                        chance /= 4;
                    }

                    if (roll >= chance || NearTree(planted, x, z, spec.Spacing))
                    {
                        continue;
                    }

                    PlantTree(x, z, trunk, "forest");
                    planted[i] = true;
                    count++;
                }
            }

            return count;
        }

        // No trees on a cliff: the four neighbours are within two blocks of the ground under the trunk.
        private bool Gentle(int x, int z)
        {
            var h = HeightAt(x, z);
            return Math.Abs(HeightAt(x - 1, z) - h) <= 2 && Math.Abs(HeightAt(x + 1, z) - h) <= 2 && Math.Abs(HeightAt(x, z - 1) - h) <= 2 && Math.Abs(HeightAt(x, z + 1) - h) <= 2;
        }

        private bool NearTree(bool[] planted, int x, int z, int spacing)
        {
            for (var dz = -(spacing - 1); dz <= spacing - 1; dz++)
            {
                for (var dx = -(spacing - 1); dx <= spacing - 1; dx++)
                {
                    var cx = x + dx;
                    var cz = z + dz;
                    if (cx >= 0 && cx < _layout.SizeX && cz >= 0 && cz < _layout.SizeZ && planted[(cz * _layout.SizeX) + cx])
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
