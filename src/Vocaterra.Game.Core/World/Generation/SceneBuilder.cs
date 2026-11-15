using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Currents;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Generation;

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

    /// <summary>The name of the stream that shapes each tried tree's crown and species (S14); a separate stream, so the trees themselves did not move.</summary>
    private const string ShapePassName = "treeshape";

    /// <summary>The name of the undergrowth's noise (S14).</summary>
    private const string UndergrowthPassName = "undergrowth";

    /// <summary>How far in front of a lookout, inside its sight cone, the undergrowth stays at flower height (S14).</summary>
    private const int LookoutForegroundBlocks = 44;

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
        private readonly bool[] _hard; // columns of props, points, NPCs, the spawn and the bridge: the undergrowth keeps off them (S14)
        private bool[] _trunkColumns = [];
        private readonly SortedDictionary<string, BlockPos> _points = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<BlockPos>> _propCells = new(StringComparer.Ordinal);
        private Terrain _terrain = new(0, 0);
        private TerrainNoise _forest = new(0, ForestPassName);
        private ulong _seed;

        public Builder(SceneLayout layout)
        {
            _layout = layout;
            _grid = new VoxelGrid(layout.SizeX, layout.SizeY, layout.SizeZ);
            _reserved = new bool[layout.SizeX * layout.SizeZ];
            _hard = new bool[layout.SizeX * layout.SizeZ];
        }

        public Scene Run(ulong seed)
        {
            _seed = seed;
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
            var plants = PlantUndergrowth(seed);
            _grid.SealPristine(); // B05: what the build wrote is the world as generated; only a later write makes a chunk changed (state digest, saves)
            return new Scene(_layout.Name, seed, _grid, new SpawnPoint(spawnFeet, _layout.Spawn.Yaw, _layout.Spawn.Pitch), npcs, _points, trees, _layout.WaterLevel, plants, _layout.Labels, _propCells, RiverCurrents.Build(_layout.River, _grid));
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
                    else if (h >= spec.SnowLine + (noise.Column(1, x, z) % 7) - 3 && slope <= 5)
                    {
                        top = BlockId.Snow;
                        sub = BlockId.Stone;
                    }
                    else if (h >= spec.RockLine || slope >= 5)
                    {
                        // Low rock in the forest is mossy (S14); the high crags are bare.
                        var mossy = h < spec.RockLine - 20;
                        top = mossy ? BlockId.MossyStone : BlockId.Stone;
                        sub = mossy && noise.Column(2, x, z) % 2 == 0 ? BlockId.MossyStone : BlockId.Stone;
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

            WaterAtRest.Apply(_grid, _terrain);
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
                _hard[(z * _layout.SizeX) + x] = true;
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
                    BuildSignpost(prop, where);
                    break;
                case "lamp":
                    BuildLamp(prop, where);
                    break;
                case "block":
                    BuildBlock(prop, where);
                    break;
                case "pergola":
                    BuildPergola(prop, where);
                    break;
                case "field":
                    BuildField(prop, where);
                    break;
                case "fence_line":
                    BuildFenceLine(prop, where);
                    break;

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
            var back = front switch
            {
                Direction.North => Direction.South,
                Direction.South => Direction.North,
                Direction.West => Direction.East,
                _ => Direction.West,
            };
            var rng = WorldRng.For(_seed, StallPassName);
            for (var z = prop.Z; z <= z1; z++)
            {
                for (var x = prop.X; x <= x1; x++)
                {
                    var cornerX = x == prop.X || x == x1;
                    var cornerZ = z == prop.Z || z == z1;

                    // The awning's stripes run across the counter: the stripe is picked by the cell's place along the counter's own line.
                    var stripe = front is Direction.North or Direction.South ? x - prop.X : z - prop.Z;
                    var awning = stripe % 2 == 0 ? BlockId.AwningRed : BlockId.AwningWhite;
                    if (cornerX && cornerZ)
                    {
                        // Thin fence posts at the front (a customer in front of the counter looks past them), thick timber posts at the back.
                        var post = OnSide(front, x, z, prop.X, prop.Z, x1, z1) ? BlockId.Fence : BlockId.Timber;
                        for (var y = h + 1; y <= h + 3; y++)
                        {
                            Put(x, y, z, post, where);
                        }
                    }
                    else if (OnSide(front, x, z, prop.X, prop.Z, x1, z1))
                    {
                        Put(x, h + 1, z, BlockId.Stall, where);
                        Put(x, h + 2, z, StallGoods(rng.NextUInt32()), where);
                    }
                    else if (OnSide(back, x, z, prop.X, prop.Z, x1, z1) && !IsMiddle(back, x, z, prop.X, prop.Z, x1, z1))
                    {
                        // The shopkeeper stands at the middle of the back row; the crates beside him hold the goods.
                        Put(x, h + 1, z, BlockId.Crate, where);
                        Put(x, h + 2, z, StallGoods(rng.NextUInt32()), where);
                    }

                    Put(x, h + 4, z, awning, where);
                }
            }

            // The awning reaches one block out over the counter's side, so the stripes show from the front.
            var (ox, oz) = front switch
            {
                Direction.North => (0, -1),
                Direction.South => (0, 1),
                Direction.West => (-1, 0),
                _ => (1, 0),
            };
            for (var z = prop.Z; z <= z1; z++)
            {
                for (var x = prop.X; x <= x1; x++)
                {
                    if (OnSide(front, x, z, prop.X, prop.Z, x1, z1))
                    {
                        var stripe = front is Direction.North or Direction.South ? x - prop.X : z - prop.Z;
                        Put(x + ox, h + 4, z + oz, stripe % 2 == 0 ? BlockId.AwningRed : BlockId.AwningWhite, where);
                    }
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

        private const string StallPassName = "stall";

        // What lies on a counter or a crate: bread, apples or bottles, from the pass's draw.
        private static BlockId StallGoods(uint draw) => (draw % 3) switch
        {
            0 => BlockId.Loaves,
            1 => BlockId.Apples,
            _ => BlockId.Bottles,
        };

        private static bool IsMiddle(Direction side, int x, int z, int x0, int z0, int x1, int z1) =>
            side is Direction.North or Direction.South ? x == x0 + ((x1 - x0 + 1) / 2) : z == z0 + ((z1 - z0 + 1) / 2);

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
                        // A timber frame: posts at the corners, a beam under the eaves, plaster between.
                        var corner = (x == prop.X || x == x1) && (z == prop.Z || z == z1);
                        var isDoor = x == doorX && z == doorZ;
                        for (var y = h + 1; y <= h + wall; y++)
                        {
                            var block = isDoor && y <= h + 2 ? BlockId.Door : corner || y == h + wall ? BlockId.Timber : BlockId.Wall;
                            Put(x, y, z, block, where);
                        }
                    }
                }
            }

            // The roof steps in one block a layer, from the eaves to a ridge cap, and the chimney rises from the second step at a corner the seed picks.
            var layers = 0;
            for (var k = 0; width - (2 * k) >= 1 && depth - (2 * k) >= 1; k++)
            {
                for (var z = prop.Z + k; z <= z1 - k; z++)
                {
                    for (var x = prop.X + k; x <= x1 - k; x++)
                    {
                        Put(x, h + wall + 1 + k, z, BlockId.Roof, where);
                    }
                }

                layers = k + 1;
            }

            if (layers >= 2)
            {
                var draw = WorldRng.Mix(((ulong)(uint)prop.X << 20) ^ (uint)prop.Z ^ 0x68AB9D1UL); // by place, not by seed: a site never changes with the seed
                var east = (draw & 1) == 0;
                var cx = east ? x1 - 1 : prop.X + 1;
                var cz = (draw & 2) == 0 ? prop.Z + 1 : z1 - 1;
                var top = h + wall + 1 + layers + 1;
                for (var y = h + wall + 2; y <= top; y++)
                {
                    Put(cx, y, cz, BlockId.Cobble, where);
                }
            }

            Register(prop, prop.X + (width / 2), prop.Z + (depth / 2));
        }

        // A signpost (S15): a post and up to three boards stacked on it, each carrying a place name and an arrow, readable from the side the prop faces.
        // The boards extend from the post to the reader's right; the names are in prop.Stamp, separated by commas (default: tienda, rio, montana).
        private void BuildSignpost(PropSpec prop, string where)
        {
            var reader = prop.Facing ?? Direction.South;
            if (reader is not (Direction.South or Direction.West))
            {
                throw new SceneLayoutException($"{where}: a signpost is read from its south or its west side (the words are drawn on those faces).");
            }

            var (ax, az) = reader == Direction.South ? (1, 0) : (0, 1);
            var boards = (prop.Stamp ?? "tienda,rio,montana").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var x0 = Math.Min(prop.X, prop.X + (2 * ax));
            var z0 = Math.Min(prop.Z, prop.Z + (2 * az));
            var h = FlatHeight(x0, z0, Math.Abs(2 * ax) + 1, Math.Abs(2 * az) + 1, where);
            Put(prop.X, h + 1, prop.Z, BlockId.Fence, where);
            Put(prop.X, h + 2, prop.Z, BlockId.Fence, where);
            for (var row = 0; row < boards.Length; row++)
            {
                var y = h + 3 + row;
                Put(prop.X, y, prop.Z, BlockId.Sign, where);
                var cells = boards[row] switch
                {
                    "tienda" => new[] { BlockId.SignTiendaA, BlockId.SignTiendaB },
                    "rio" => new[] { BlockId.SignRio },
                    "montana" => new[] { BlockId.SignMontanaA, BlockId.SignMontanaB },
                    _ => throw new SceneLayoutException($"{where}: unknown sign board '{boards[row]}'."),
                };
                for (var i = 0; i < cells.Length; i++)
                {
                    Put(prop.X + (ax * (i + 1)), y, prop.Z + (az * (i + 1)), cells[i], where);
                }
            }

            Register(prop, prop.X, prop.Z);
        }

        // A lamp post: two fence blocks and a lantern on top.
        private void BuildLamp(PropSpec prop, string where)
        {
            var h = FlatHeight(prop.X, prop.Z, 1, 1, where);
            Put(prop.X, h + 1, prop.Z, BlockId.Fence, where);
            Put(prop.X, h + 2, prop.Z, BlockId.Fence, where);
            Put(prop.X, h + 3, prop.Z, BlockId.Lantern, where);
            Register(prop, prop.X, prop.Z);
        }

        // A stack of one kind of block (a crate, a chest) on the ground.
        private void BuildBlock(PropSpec prop, string where)
        {
            var h = FlatHeight(prop.X, prop.Z, 1, 1, where);
            var count = prop.Height > 0 ? prop.Height : 1;
            for (var i = 1; i <= count; i++)
            {
                Put(prop.X, h + i, prop.Z, prop.Block, where);
            }

            Register(prop, prop.X, prop.Z);
        }

        // A timber pergola: thin fence posts at the four corners and a frame of beams across the top with slats, a torch on the north-west post and a lantern hanging in the middle.
        private void BuildPergola(PropSpec prop, string where)
        {
            var width = prop.Width > 0 ? prop.Width : 4;
            var depth = prop.Depth > 0 ? prop.Depth : 3;
            var post = prop.Height > 0 ? prop.Height : 3;
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
                        // Thin fence posts, like the stall's front posts (S22: full timber blocks filled the view at the workbench); the beams on top stay timber.
                        for (var y = h + 1; y <= h + post; y++)
                        {
                            Put(x, y, z, BlockId.Fence, where);
                        }
                    }

                    if (x == prop.X && z == prop.Z)
                    {
                        Put(x, h + post + 1, z, BlockId.PostTorch, where);
                    }
                    else if (cornerX || cornerZ)
                    {
                        Put(x, h + post + 1, z, BlockId.Timber, where);
                    }
                    else if (x % 2 == 0)
                    {
                        Put(x, h + post + 1, z, BlockId.Timber, where);
                    }
                }
            }

            Put(prop.X + (width / 2), h + post, prop.Z + (depth / 2), BlockId.Lantern, where);
            Register(prop, prop.X, prop.Z);
        }

        // A crop field: tilled soil in rows of wheat and bare furrows, inside a fence with a gap on the south side. The seed leaves a few cells bare.
        private void BuildField(PropSpec prop, string where)
        {
            var width = prop.Width > 0 ? prop.Width : 7;
            var depth = prop.Depth > 0 ? prop.Depth : 7;
            var h = FlatHeight(prop.X, prop.Z, width, depth, where);
            var x1 = prop.X + width - 1;
            var z1 = prop.Z + depth - 1;
            var rng = WorldRng.For(_seed, "crops");
            for (var z = prop.Z; z <= z1; z++)
            {
                for (var x = prop.X; x <= x1; x++)
                {
                    var edge = x == prop.X || x == x1 || z == prop.Z || z == z1;
                    if (edge)
                    {
                        var gate = z == z1 && x == prop.X + (width / 2);
                        if (!gate)
                        {
                            Put(x, h + 1, z, BlockId.Fence, where);
                        }

                        continue;
                    }

                    Put(x, h, z, BlockId.Farmland, where);
                    var draw = rng.NextUInt32();
                    if (z % 2 == 0 && draw % 10 != 0)
                    {
                        Put(x, h + 1, z, BlockId.Wheat, where);
                    }
                }
            }

            Register(prop, prop.X + (width / 2), prop.Z + (depth / 2));
        }

        // A run of fence along the ground, following its height, with no block under it changed.
        private void BuildFenceLine(PropSpec prop, string where)
        {
            var length = prop.Length > 0 ? prop.Length : 1;
            for (var i = 0; i < length; i++)
            {
                var x = prop.X + (prop.AlongZ ? 0 : i);
                var z = prop.Z + (prop.AlongZ ? i : 0);
                CheckColumn(x, z, where);
                if (IsWater(x, z))
                {
                    continue;
                }

                Put(x, HeightAt(x, z) + 1, z, BlockId.Fence, where);
                Reserve(x, z);
            }
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

        // A small structure drawn in the layout (a tent), turned to face a side and set on level ground. The turning is StampSpec's (Y12: the camp the player places goes down the same way).
        private void BuildStamp(PropSpec prop, string where)
        {
            var stamp = prop.Stamp is { } name && _layout.Stamps.TryGetValue(name, out var found)
                ? found
                : throw new SceneLayoutException($"{where}: there is no stamp '{prop.Stamp}'.");
            var facing = prop.Facing ?? Direction.North;
            var width = stamp.WidthFacing(facing);
            var depth = stamp.DepthFacing(facing);
            var h = FlatHeight(prop.X, prop.Z, width, depth, where);
            var cells = new List<BlockPos>();
            foreach (var (dx, dz, layer, block) in stamp.Cells(facing))
            {
                Put(prop.X + dx, h + 1 + layer, prop.Z + dz, block, where);
                cells.Add(new BlockPos(prop.X + dx, h + 1 + layer, prop.Z + dz));
            }

            if (prop.Name is not null)
            {
                _propCells[prop.Name] = cells; // W07: the station lookup (E at a cell of the oven)
            }

            Register(prop, prop.X + (width / 2), prop.Z + (depth / 2));
        }

        // ---------------------------------------------------------------- the forest

        // The three species (S14). Oaks fill the valley with big rounded crowns, spruces take over on the slopes with tiered cones, and a few birches
        // stand in the lowlands, away from the sites.
        private enum Species
        {
            Oak,
            Spruce,
            Birch,
        }

        private static BlockId TrunkOf(Species species) => species switch
        {
            Species.Spruce => BlockId.SpruceTrunk,
            Species.Birch => BlockId.BirchTrunk,
            _ => BlockId.Trunk,
        };

        private static BlockId LeavesOf(Species species) => species switch
        {
            Species.Spruce => BlockId.SpruceLeaves,
            Species.Birch => BlockId.BirchLeaves,
            _ => BlockId.Leaves,
        };

        // The trunk of a species in blocks, from the draw the forest makes for every cell (4 or 5 by default): a spruce is much taller, a birch a little.
        private static int TrunkLength(Species species, int drawn) => species switch
        {
            Species.Spruce => drawn + 6,
            Species.Birch => drawn + 2,
            _ => drawn,
        };

        // The crown, from the top downwards: the squared distance (in columns) from the trunk inside which a layer has leaves. The layer's offset from the top
        // of the trunk is OakTop[0] for the first entry, and so on downwards.
        private static readonly int[] OakCrown = [1, 5, 10, 10, 5];
        private static readonly int[] SmallOakCrown = [0, 2, 5, 5, 2];
        private static readonly int[] BirchCrown = [0, 2, 5, 5, 2];
        private static readonly int[] SpruceCone = [0, 1, 2, 2, 5, 5, 5, 10, 10, 10];

        // The block above the trunk's top where the crown starts (offset from the top of the trunk, for the first entry of the list).
        private static int CrownTopOffset(Species species) => species switch
        {
            Species.Spruce => 1,
            _ => 3,
        };

        private static int TreeHeight(Species species, int trunk) => TrunkLength(species, trunk) + CrownTopOffset(species);

        private void PlantTree(int x, int z, int trunk, string where) => PlantTree(x, z, trunk, Species.Oak, 1, where, true);

        private void PlantTree(int x, int z, int drawnTrunk, Species species, ulong shape, string where, bool exact = false)
        {
            var baseY = HeightAt(x, z);
            var length = exact ? drawnTrunk : TrunkLength(species, drawnTrunk);
            var top = baseY + length;
            var trunkBlock = TrunkOf(species);
            var leaves = LeavesOf(species);
            for (var y = baseY + 1; y <= top; y++)
            {
                Put(x, y, z, trunkBlock, where);
            }

            // The crown: layers from the top downwards. Oaks have a big and a small crown (a third are small), and the outer rim of every layer is ragged.
            int[] profile;
            int first;
            if (species == Species.Spruce)
            {
                var layers = length - 1;
                profile = new int[layers];
                for (var k = 0; k < layers; k++)
                {
                    profile[k] = SpruceCone[Math.Min(k, SpruceCone.Length - 1)];
                }

                first = top + 1;
            }
            else
            {
                profile = species == Species.Birch ? BirchCrown : ((shape & 3) == 0 ? SmallOakCrown : OakCrown);
                first = top + 3;
            }

            for (var k = 0; k < profile.Length; k++)
            {
                var y = first - k;
                var limit = profile[k];
                var reach = limit == 0 ? 0 : limit <= 1 ? 1 : limit <= 2 ? 1 : limit <= 5 ? 2 : 3;
                for (var dz = -reach; dz <= reach; dz++)
                {
                    for (var dx = -reach; dx <= reach; dx++)
                    {
                        var d2 = (dx * dx) + (dz * dz);
                        if (d2 > limit || !_grid.InBounds(x + dx, y, z + dz) || _grid.GetBlock(x + dx, y, z + dz) != BlockId.Air)
                        {
                            continue;
                        }

                        // A ragged rim: the layer's outermost blocks go now and then, never the ones next to the trunk.
                        if (d2 >= limit - 1 && d2 > 1 && (WorldRng.Mix(shape ^ (ulong)((dx + 8) | ((dz + 8) << 4) | (k << 8))) & 3) == 0)
                        {
                            continue;
                        }

                        _grid.SetBlock(x + dx, y, z + dz, leaves);
                    }
                }
            }
        }

        // Whole-map distance (in chamfer units, 3 per column along an axis, 4 along a diagonal) from the water's edge, capped at 90.
        private int[] ShoreDistances()
        {
            const int Cap = 90;
            var w = _layout.SizeX;
            var d = _layout.SizeZ;
            var dist = new int[w * d];
            for (var i = 0; i < dist.Length; i++)
            {
                dist[i] = (_terrain.Flags[i] & Terrain.ShoreFlag) != 0 ? 0 : Cap;
            }

            for (var z = 0; z < d; z++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = (z * w) + x;
                    var best = dist[i];
                    if (x > 0)
                    {
                        best = Math.Min(best, dist[i - 1] + 3);
                    }

                    if (z > 0)
                    {
                        best = Math.Min(best, dist[i - w] + 3);
                        if (x > 0)
                        {
                            best = Math.Min(best, dist[i - w - 1] + 4);
                        }

                        if (x < w - 1)
                        {
                            best = Math.Min(best, dist[i - w + 1] + 4);
                        }
                    }

                    dist[i] = best;
                }
            }

            for (var z = d - 1; z >= 0; z--)
            {
                for (var x = w - 1; x >= 0; x--)
                {
                    var i = (z * w) + x;
                    var best = dist[i];
                    if (x < w - 1)
                    {
                        best = Math.Min(best, dist[i + 1] + 3);
                    }

                    if (z < d - 1)
                    {
                        best = Math.Min(best, dist[i + w] + 3);
                        if (x < w - 1)
                        {
                            best = Math.Min(best, dist[i + w + 1] + 4);
                        }

                        if (x > 0)
                        {
                            best = Math.Min(best, dist[i + w - 1] + 4);
                        }
                    }

                    dist[i] = best;
                }
            }

            return dist;
        }

        // The share of trees (percent) the meadows and the shore leave at a column: 100 in the forest, less towards a site's meadow or the water.
        private int KeptPercent(int x, int z, int[] shore)
        {
            var spec = _layout.Trees;
            var kept = 100;
            if (spec.ShoreClear > 0)
            {
                var blocks16 = shore[(z * _layout.SizeX) + x] * 16 / 3;
                var weight = IntMath.Ramp(blocks16, spec.ShoreClear * 16, (spec.ShoreClear + spec.ShoreFade) * 16);
                kept = Math.Min(kept, IntMath.Lerp(spec.ShoreKeep, 100, weight));
            }

            foreach (var clearing in spec.ClearingList)
            {
                var site = _layout.Site(clearing.Site)!;
                var dist16 = (int)IntMath.ISqrt((long)(((x - site.X) * (x - site.X)) + ((z - site.Z) * (z - site.Z))) * 256);
                var weight = IntMath.Ramp(dist16, clearing.Radius * 16, (clearing.Radius + clearing.Fade) * 16);
                kept = Math.Min(kept, IntMath.Lerp(clearing.Keep, 100, weight));
            }

            return kept;
        }

        // Whether the column is inside the sight cone of a vista (within a percent of its radius), and how far it is from the lookout in 1/16 blocks.
        private bool InVista(VistaSpec vista, int x, int z, int radiusPercent, out int dist16)
        {
            var site = _layout.Site(vista.Site)!;
            var dx = x - site.X;
            var dz = z - site.Z;
            dist16 = (int)IntMath.ISqrt((long)((dx * dx) + (dz * dz)) * 256);
            if (dist16 > vista.Radius * 16 * radiusPercent / 100)
            {
                return false;
            }

            var forward = Angles.Direction(Angles.FromDegrees(vista.YawDegrees), 0);
            var dot = ((long)dx * forward.X) + ((long)dz * forward.Z); // blocks * One
            var cosHalf = Angles.Cos(Angles.FromDegrees(vista.HalfAngleDegrees));
            return dot * 16 >= (long)cosHalf * dist16;
        }

        // Whether the column is in the near part of a sight cone, where nothing taller than a flower may grow: the grass would hide the valley from the lookout.
        private bool InLookoutForeground(int x, int z)
        {
            foreach (var vista in _layout.Trees.VistaList)
            {
                if (InVista(vista, x, z, 100, out var dist16) && dist16 <= LookoutForegroundBlocks * 16)
                {
                    return true;
                }
            }

            return false;
        }

        // Whether a crown of this height at this column stands inside a sight cone and above the line the lookout needs kept clear.
        private bool HidesAVista(int x, int z, int crownTop)
        {
            foreach (var vista in _layout.Trees.VistaList)
            {
                if (!InVista(vista, x, z, 100, out var dist16))
                {
                    continue;
                }

                var site = _layout.Site(vista.Site)!;
                var down = Angles.FromDegrees(vista.DownDegrees);
                var tanMilli = (int)((long)Angles.Sin(down) * 1000 / Math.Max(1, Angles.Cos(down)));
                var eyeMilli = ((long)(site.Y ?? 0) + 3) * 1000;
                if ((long)crownTop * 1000 > eyeMilli - ((long)dist16 * tanMilli / 16))
                {
                    return true;
                }
            }

            return false;
        }

        // Whether any site that has level ground is within the distance of the column.
        private bool NearASite(int x, int z, int distance)
        {
            foreach (var site in _layout.Sites)
            {
                if (site.Radius > 0 && ((x - site.X) * (x - site.X)) + ((z - site.Z) * (z - site.Z)) <= distance * distance)
                {
                    return true;
                }
            }

            return false;
        }

        // One tree is tried in every square of the ground at a jittered place. The draws are made for every square whether or not a tree is kept, so the stream
        // stays in step and a change to the margins moves only the trees it touches. (S14: the species, the shape of the crown, the meadows, the shore and the
        // sight cones only decide which of the tried trees stay and what they are; the first stream is the one S13 drew, so no trunk moved.)
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

            var shore = ShoreDistances();
            var rng = WorldRng.For(seed, TreePassName);
            var shapes = WorldRng.For(seed, ShapePassName);
            var planted = new bool[sizeX * sizeZ];
            _trunkColumns = planted;
            var count = 0;
            for (var cz = 0; cz < sizeZ; cz += spec.Cell)
            {
                for (var cx = 0; cx < sizeX; cx += spec.Cell)
                {
                    var x = cx + rng.NextInt(spec.Cell);
                    var z = cz + rng.NextInt(spec.Cell);
                    var roll = rng.NextInt(100);
                    var trunk = rng.NextInt(spec.MinTrunk, spec.MaxTrunk + 1);
                    var shape = shapes.NextUInt64();
                    if (x < 2 || z < 2 || x >= sizeX - 2 || z >= sizeZ - 2 || closed[(z * sizeX) + x])
                    {
                        continue;
                    }

                    var i = (z * sizeX) + x;
                    var h = _terrain.Height[i];
                    if (h > spec.TreeLine || _terrain.IsWater(x, z) || _grid.GetBlock(x, h, z) != BlockId.Grass || !Gentle(x, z))
                    {
                        continue;
                    }

                    // Thinner with height, a few clearings where the slow noise is low, and the meadows round the sites and along the water.
                    var chance = spec.Chance * (100 - Math.Clamp((h - 56) * 3, 0, 80)) / 100;
                    if (_forest.Value(8, x, z, 40) < 700)
                    {
                        chance /= 4;
                    }

                    chance = chance * KeptPercent(x, z, shore) / 100;
                    if (roll >= chance || NearTree(planted, x, z, spec.Spacing))
                    {
                        continue;
                    }

                    // Spruces on the slopes (all of them 20 blocks above where they start), a few birches in the lowlands, oaks otherwise.
                    var species = Species.Oak;
                    var spruceShare = IntMath.Ramp(h, spec.SpruceFrom, spec.SpruceFrom + 20) * 100 / IntMath.One;
                    if ((int)((shape >> 8) % 100) < spruceShare)
                    {
                        species = Species.Spruce;
                    }
                    else if ((int)((shape >> 20) % 100) < spec.Birch && !NearASite(x, z, spec.BirchAway))
                    {
                        species = Species.Birch;
                    }

                    var crownTop = h + TreeHeight(species, trunk);
                    if (crownTop + 1 >= _layout.SizeY || HidesAVista(x, z, crownTop))
                    {
                        continue;
                    }

                    PlantTree(x, z, trunk, species, shape, "forest");
                    planted[i] = true;
                    count++;
                }
            }

            return count;
        }

        // ---------------------------------------------------------------- the undergrowth

        // Tall grass, ferns, reeds and flowers on the open ground (S14). Each column's chance comes from the noise of the pass 'undergrowth' (keyed from WorldRng),
        // modulated by slow patches (thick grass here, a flower meadow there); nothing grows on a path or beside a prop, and a third as much one column further.
        private int PlantUndergrowth(ulong seed)
        {
            var spec = _layout.Undergrowth;
            if (spec.Grass <= 0 && spec.Flowers <= 0 && spec.Reeds <= 0)
            {
                return 0;
            }

            var sizeX = _layout.SizeX;
            var sizeZ = _layout.SizeZ;
            var noise = new TerrainNoise(seed, UndergrowthPassName);

            // 2 on the columns of a path, a prop, a point, an NPC or the spawn, widened by the margin (nothing grows) and one more (a third as much).
            var near = new byte[sizeX * sizeZ];
            var margin = spec.RouteMargin;
            for (var z = 0; z < sizeZ; z++)
            {
                for (var x = 0; x < sizeX; x++)
                {
                    var i = (z * sizeX) + x;
                    if (!_hard[i] && (_terrain.Flags[i] & Terrain.PathFlag) == 0)
                    {
                        continue;
                    }

                    for (var dz = -(margin + 1); dz <= margin + 1; dz++)
                    {
                        for (var dx = -(margin + 1); dx <= margin + 1; dx++)
                        {
                            var cx = x + dx;
                            var cz = z + dz;
                            if (cx < 0 || cx >= sizeX || cz < 0 || cz >= sizeZ)
                            {
                                continue;
                            }

                            var near1 = Math.Abs(dx) <= margin && Math.Abs(dz) <= margin;
                            var j = (cz * sizeX) + cx;
                            near[j] = (byte)Math.Max(near[j], near1 ? 2 : 1);
                        }
                    }
                }
            }

            var count = 0;
            for (var z = 1; z < sizeZ - 1; z++)
            {
                for (var x = 1; x < sizeX - 1; x++)
                {
                    var i = (z * sizeX) + x;
                    if (near[i] == 2 || _terrain.IsWater(x, z))
                    {
                        continue;
                    }

                    var h = _terrain.Height[i];
                    var ground = _grid.GetBlock(x, h, z);
                    if ((ground != BlockId.Grass && ground != BlockId.Sand) || h + 2 >= _layout.SizeY || _grid.GetBlock(x, h + 1, z) != BlockId.Air)
                    {
                        continue;
                    }

                    // In front of a lookout only flowers grow, in meadows of their own: the grass would stand between the player and the valley.
                    if (InLookoutForeground(x, z))
                    {
                        if (ground == BlockId.Grass && spec.Flowers > 0 && noise.Column(14, x, z) % 100 < 26)
                        {
                            var yellow = noise.Value(2, x, z, 26) > 2048;
                            _grid.SetBlock(x, h + 1, z, yellow != (noise.Column(12, x, z) % 9 == 0) ? BlockId.YellowFlower : BlockId.WhiteFlower);
                            count++;
                        }

                        continue;
                    }

                    var thin = near[i] == 1 ? 3 : 1; // a third as much one column from the route
                    var pick = noise.Column(10, x, z) * 100 / 4096; // 0 to 99
                    var kind = noise.Column(11, x, z) * 100 / 4096;
                    var block = BlockId.Air;

                    // Reeds stand on the bank, within two columns of the water.
                    if (spec.Reeds > 0 && NearWater(x, z, 2) && pick * thin < spec.Reeds)
                    {
                        block = BlockId.Reeds;
                    }
                    else if (ground == BlockId.Grass)
                    {
                        var patch = noise.Value(0, x, z, 10);
                        var flowerBed = noise.Value(1, x, z, 22);
                        if (spec.Flowers > 0 && patch > 2900 && flowerBed > 1100 && pick * thin < spec.Flowers)
                        {
                            // A flower meadow is mostly one colour, with some of the other.
                            var yellowPatch = noise.Value(2, x, z, 26) > 2048;
                            var other = noise.Column(12, x, z) % 9 == 0;
                            block = yellowPatch != other ? BlockId.YellowFlower : BlockId.WhiteFlower;
                        }

                        if (block == BlockId.Air && spec.Grass > 0)
                        {
                            var density = 70 + (noise.Value(3, x, z, 18) * 30 / 4096); // 70 to 100
                            if (pick * thin * 100 < spec.Grass * density)
                            {
                                var shade = HasTrunkNear(x, z, 2);
                                var fern = shade ? Math.Min(100, spec.Fern + 45) : spec.Fern;
                                block = kind < fern ? BlockId.Fern : BlockId.TallGrass;
                            }
                        }
                    }

                    if (block == BlockId.Air)
                    {
                        continue;
                    }

                    _grid.SetBlock(x, h + 1, z, block);
                    count++;
                    if (block == BlockId.Reeds && noise.Column(13, x, z) % 3 == 0 && _grid.GetBlock(x, h + 2, z) == BlockId.Air)
                    {
                        _grid.SetBlock(x, h + 2, z, BlockId.Reeds);
                        count++;
                    }
                }
            }

            return count;
        }

        private bool NearWater(int x, int z, int distance)
        {
            for (var dz = -distance; dz <= distance; dz++)
            {
                for (var dx = -distance; dx <= distance; dx++)
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

        private bool HasTrunkNear(int x, int z, int distance)
        {
            for (var dz = -distance; dz <= distance; dz++)
            {
                for (var dx = -distance; dx <= distance; dx++)
                {
                    var cx = x + dx;
                    var cz = z + dz;
                    if (cx >= 0 && cx < _layout.SizeX && cz >= 0 && cz < _layout.SizeZ && _trunkColumns[(cz * _layout.SizeX) + cx])
                    {
                        return true;
                    }
                }
            }

            return false;
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


