
namespace Vocaterra.Simulation.Voxel;

/// <summary>
/// Builds a <see cref="Scene"/> from a hand-authored <see cref="SceneLayout"/> and a world seed. It is a pure function of the two: the
/// terrain, the props, the spawn, the NPCs and the points come from the layout alone, and the only thing the seed changes is the tree
/// scatter, drawn from <see cref="WorldRng"/> (the pass name <c>trees</c>). There is no noise, no cave, no ore and no light map.
/// </summary>
public static class SceneBuilder
{
    /// <summary>The name of the scatter's random stream (<see cref="WorldRng.For"/>); renaming it changes every golden.</summary>
    public const string TreePassName = "trees";

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

        public Builder(SceneLayout layout)
        {
            _layout = layout;
            _grid = new VoxelGrid(layout.SizeX, layout.SizeY, layout.SizeZ);
            _reserved = new bool[layout.SizeX * layout.SizeZ];
        }

        public Scene Run(ulong seed)
        {
            BuildTerrain();
            foreach (var prop in _layout.Props)
            {
                BuildProp(prop);
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

            var trees = ScatterTrees(seed);
            return new Scene(_layout.Name, seed, _grid, new SpawnPoint(spawnFeet, _layout.Spawn.Yaw), npcs, _points, trees, _layout.WaterLevel);
        }

        private void BuildTerrain()
        {
            for (var z = 0; z < _layout.SizeZ; z++)
            {
                for (var x = 0; x < _layout.SizeX; x++)
                {
                    var entry = _layout.Legend[_layout.FeatureAt(x, z)];
                    var h = _layout.HeightAt(x, z);
                    for (var y = 0; y <= h; y++)
                    {
                        var block = y == h ? entry.Top : y >= h - 3 ? entry.Sub : BlockId.Stone;
                        _grid.SetBlock(x, y, z, block);
                    }

                    if (entry.Water)
                    {
                        for (var y = h + 1; y <= _layout.WaterLevel; y++)
                        {
                            _grid.SetBlock(x, y, z, BlockId.Water);
                        }
                    }

                    if (entry.Reserved)
                    {
                        _reserved[(z * _layout.SizeX) + x] = true;
                    }
                }
            }
        }

        private bool IsWater(int x, int z) => _layout.Legend[_layout.FeatureAt(x, z)].Water && _layout.HeightAt(x, z) < _layout.WaterLevel;

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
            var y = IsWater(x, z) ? _layout.WaterLevel : _layout.HeightAt(x, z);
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

            var feet = new BlockPos(x, _layout.HeightAt(x, z) + 1, z);
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
            var h = _layout.HeightAt(x0, z0);
            for (var z = z0; z < z0 + depth; z++)
            {
                for (var x = x0; x < x0 + width; x++)
                {
                    if (_layout.HeightAt(x, z) != h || IsWater(x, z))
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

            var deckY = _layout.HeightAt(prop.X, prop.Z);
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

        private void PlantTree(int x, int z, int trunk, string where)
        {
            var baseY = _layout.HeightAt(x, z);
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

        private int ScatterTrees(ulong seed)
        {
            var spec = _layout.Trees;
            if (spec.Attempts <= 0 || spec.Max <= 0)
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
            for (var attempt = 0; attempt < spec.Attempts && count < spec.Max; attempt++)
            {
                var x = rng.NextInt(sizeX);
                var z = rng.NextInt(sizeZ);
                var trunk = rng.NextInt(spec.MinTrunk, spec.MaxTrunk + 1);
                if (x < 2 || z < 2 || x >= sizeX - 2 || z >= sizeZ - 2
                    || closed[(z * sizeX) + x]
                    || !_layout.Legend[_layout.FeatureAt(x, z)].Plantable
                    || _layout.HeightAt(x, z) + trunk + 2 >= _layout.SizeY
                    || NearTree(planted, x, z, spec.Spacing))
                {
                    continue;
                }

                PlantTree(x, z, trunk, "tree scatter");
                planted[(z * sizeX) + x] = true;
                count++;
            }

            return count;
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
