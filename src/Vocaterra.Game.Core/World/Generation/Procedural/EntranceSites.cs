using System.Collections.Concurrent;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The cave entrances of a world (B11b, <see cref="EntranceSpec"/>): the world is cut into sites, each of which may hold one <see cref="EntranceRamp"/>, a stair a body walks down from the surface into a
/// cave. A site's ramp is a pure function of the seed, the data and the site: its mouth is drawn from the site's own random stream, and whether it opens, in which of the four directions and how
/// far it must go to meet a cave are read from the height model and the cave field directly (the ground there, the water and the river banks, the regions and their margin, the cave air under it), never from
/// a chunk's window, so every chunk column that the passage crosses finds the same ramp whichever it is generated in and in whatever order. A resolved site is remembered (the answer never changes), so
/// the work is done once for a site.
/// <para>
/// The ground the passage is planned on is the height model's own. A column the water passes or a surface strategy could change is never used: the passage keeps <see cref="EntranceSpec.WaterReach"/>
/// columns clear of any water, river or lake carve, and stands only on plain biomes, so the final ground is the model's and the passage's floor always meets it (a test reads the generated blocks to check).
/// </para>
/// </summary>
internal sealed class EntranceSites
{
    private static readonly (int X, int Z)[] Directions = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    // The unit vectors of the 64-step turn, in 1/1024 (step 0 east, 16 south, 32 west, 48 north): the path's heading drifts between them.
    private static readonly int[] HeadingCos = [1024, 1019, 1004, 980, 946, 903, 851, 792, 724, 650, 569, 483, 392, 297, 200, 100, 0, -100, -200, -297, -392, -483, -569, -650, -724, -792, -851, -903, -946, -980, -1004, -1019, -1024, -1019, -1004, -980, -946, -903, -851, -792, -724, -650, -569, -483, -392, -297, -200, -100, 0, 100, 200, 297, 392, 483, 569, 650, 724, 792, 851, 903, 946, 980, 1004, 1019];
    private static readonly int[] HeadingSin = [0, 100, 200, 297, 392, 483, 569, 650, 724, 792, 851, 903, 946, 980, 1004, 1019, 1024, 1019, 1004, 980, 946, 903, 851, 792, 724, 650, 569, 483, 392, 297, 200, 100, 0, -100, -200, -297, -392, -483, -569, -650, -724, -792, -851, -903, -946, -980, -1004, -1019, -1024, -1019, -1004, -980, -946, -903, -851, -792, -724, -650, -569, -483, -392, -297, -200, -100];

    private readonly GenerationWorld _world;
    private readonly UndergroundWorld _underground;
    private readonly EntranceSpec _spec;
    private readonly ConcurrentDictionary<(int X, int Z), EntranceRamp?> _sites = new();
    private readonly int _dampReach;
    private readonly int _tubeReach;

    private int BowlReach { get; }

    /// <summary>Creates the sites of a world.</summary>
    /// <param name="world">The world; it must have an underground and its height model, climate and biome table already built.</param>
    public EntranceSites(GenerationWorld world)
    {
        _world = world;
        _underground = world.Underground ?? throw new InvalidOperationException("The world has no underground.");
        _spec = _underground.Data.Entrances;
        _dampReach = _underground.Data.Linings?.DampReach ?? 0;
        BowlReach = (_spec.BowlDepth * _spec.BowlSlope / 1000) + 2;
        _tubeReach = (((_spec.WidthMax - 1) * (1000 + _spec.Funnel) / 2000) + 1) + ((_spec.Roughness + 999) / 1000) + 1 + BowlReach;
    }

    /// <summary>How far, in blocks, a passage and its damp zone reach beyond the site it lies in.</summary>
    public int Reach => _tubeReach + 2 + _dampReach + 4;

    /// <summary>The ramps of the sites near a box of columns whose cells, rim or damp zone may touch the box (the box's corners are inclusive).</summary>
    /// <param name="minX">The box's first x.</param>
    /// <param name="minZ">The box's first z.</param>
    /// <param name="maxX">The box's last x.</param>
    /// <param name="maxZ">The box's last z.</param>
    public IReadOnlyList<EntranceRamp> Near(int minX, int minZ, int maxX, int maxZ)
    {
        var reach = Reach;
        var first = (X: IntMath.DivDown(minX - reach, _spec.Cell), Z: IntMath.DivDown(minZ - reach, _spec.Cell));
        var last = (X: IntMath.DivDown(maxX + reach, _spec.Cell), Z: IntMath.DivDown(maxZ + reach, _spec.Cell));
        var found = new List<EntranceRamp>();
        for (var sz = first.Z; sz <= last.Z; sz++)
        {
            for (var sx = first.X; sx <= last.X; sx++)
            {
                if (Site(sx, sz) is { } ramp)
                {
                    var b = ramp.Bounds(_dampReach);
                    if (b.MaxX >= minX && b.MinX <= maxX && b.MaxZ >= minZ && b.MinZ <= maxZ)
                    {
                        found.Add(ramp);
                    }
                }
            }
        }

        return found;
    }

    /// <summary>The ramp of one site, or <see langword="null"/> when the site opens none.</summary>
    /// <param name="siteX">The site's x (the world's x divided by <see cref="EntranceSpec.Cell"/>, rounded down).</param>
    /// <param name="siteZ">The site's z.</param>
    public EntranceRamp? Site(int siteX, int siteZ) => _sites.GetOrAdd((siteX, siteZ), Resolve);

    private EntranceRamp? Resolve((int X, int Z) site)
    {
        var rng = WorldRng.For(_world.Seed, "gen/entrances", site.X, site.Z);
        if (rng.NextInt(1000) >= _spec.Chance)
        {
            return null;
        }

        var inner = _spec.Cell - (2 * _spec.Margin);
        var keep = _tubeReach + 2;
        var box = ((site.X * _spec.Cell) + keep, (site.Z * _spec.Cell) + keep, ((site.X + 1) * _spec.Cell) - 1 - keep, ((site.Z + 1) * _spec.Cell) - 1 - keep); // a passage stays inside its own site, so two never meet
        EntranceRamp? best = null;
        var bestCover = int.MinValue;
        for (var attempt = 0; attempt < _spec.Attempts; attempt++)
        {
            var ax = (site.X * _spec.Cell) + _spec.Margin + rng.NextInt(inner);
            var az = (site.Z * _spec.Cell) + _spec.Margin + rng.NextInt(inner);
            var first = rng.NextInt(Directions.Length);
            var probe = new Probe(this);
            var mouth = probe.At(ax, az);
            if (!mouth.Usable || mouth.Height < _world.SeaLevel + _spec.MinHeightAboveSea)
            {
                continue;
            }

            for (var k = 0; k < Directions.Length; k++)
            {
                for (var variant = 0; variant < 2; variant++)
                {
                    var direction = (first + k) % Directions.Length;
                    var path = WorldRng.For(_world.Seed, $"gen/entrances/path/{attempt}/{direction}/{variant}", site.X, site.Z);
                    if (Plan(probe, ax, az, direction, mouth.Height, path, box, out var cover) is { } ramp && cover > bestCover)
                    {
                        best = ramp;
                        bestCover = cover;
                    }
                }
            }
        }

        return best;
    }

    // The path of one heading and one draw of bends and slopes, or null when the ground, the water, a region or the lack of a cave under it forbids it; cover is how much ground the first cells lie under (a
    // hillside has more). The path is a walk of 4-connected cells along a heading that drifts, its floor drops a block at most from cell to cell, and it ends where it meets a cave deep enough, then a landing.
    private EntranceRamp? Plan(Probe probe, int ax, int az, int direction, int mouthY, WorldRng rng, (int MinX, int MinZ, int MaxX, int MaxZ) box, out int cover)
    {
        cover = 0;
        var spec = _spec;
        var (dx0, dz0) = Directions[direction];
        var before = probe.At(ax - dx0, az - dz0);
        if (!before.Usable || Math.Abs(before.Height - mouthY) > 1)
        {
            return null; // the block before the mouth is the ground a body comes from: at most one block over or under the mouth's floor, so the first step is one block
        }

        var walk = new Walk(spec, rng, ax, az, direction, mouthY);
        var lava = _underground.LavaLevel;
        var length = -1;
        while (length < 0 && walk.Nodes.Count < spec.MaxLength)
        {
            var from = walk.Nodes.Count;
            if (from > 0 && !walk.Advance(descend: true))
            {
                return null;
            }

            for (var i = from; i < walk.Nodes.Count; i++)
            {
                var node = walk.Nodes[i];
                if (node.Floor + 1 <= lava + 3 || !InBox(node, box) || !NodeHolds(probe, node, i))
                {
                    return null;
                }

                if (i < 12)
                {
                    cover += probe.At(node.X, node.Z).Height - node.Floor;
                }
            }

            var last = walk.Nodes[^1];
            if (mouthY - last.Floor >= spec.MinDrop && InCave(probe, last.X, last.Floor + 1, last.Z))
            {
                length = walk.Nodes.Count - 1;
            }
        }

        if (length < 0)
        {
            return null;
        }

        for (var i = 0; i < spec.Landing; i++)
        {
            if (!walk.Advance(descend: false) || !InBox(walk.Nodes[^1], box) || !NodeHolds(probe, walk.Nodes[^1], walk.Nodes.Count - 1))
            {
                return null;
            }
        }

        if (!WaterClear(probe, walk.Nodes))
        {
            return null;
        }

        var key = WorldRng.For(_world.Seed, "gen/entrances/shape", ax, az).NextUInt64();
        return new EntranceRamp([.. walk.Nodes], length, ax - dx0, az - dz0, key, spec.Roughness, spec.RoughnessCell, spec.Ledges, BowlReach);
    }

    // One walk of the path under construction: the heading that drifts (bends of a few blocks in a random sense, never straying far from the first heading), the floor that falls in stretches of one slope or
    // runs level, and the width and headroom that drift between draws.
    private sealed class Walk
    {
        private readonly EntranceSpec _spec;
        private readonly WorldRng _rng;
        private readonly HashSet<(int, int)> _seen = [];
        private readonly int _heading0;
        private int _heading;
        private int _px;
        private int _pz;
        private int _cx;
        private int _cz;
        private int _floor;
        private int _turn;
        private int _turnLeft;
        private int _run;
        private int _runLeft;
        private int _sinceDrop;
        private (int Half, int Head) _from;
        private (int Half, int Head) _to;
        private int _driftLength = 1;
        private int _driftLeft;

        public Walk(EntranceSpec spec, WorldRng rng, int ax, int az, int direction, int mouthY)
        {
            _spec = spec;
            _rng = rng;
            _heading0 = direction * 16 * 16; // sixteenths of a step of the 64-step turn: 0 east, 16 south, 32 west, 48 north
            _heading = _heading0;
            (_px, _pz) = ((ax << 10) + 512, (az << 10) + 512);
            (_cx, _cz) = (ax, az);
            _floor = mouthY;
            _to = DrawSize();
            _from = _to;
            Add((ax, az));
        }

        public List<PathNode> Nodes { get; } = [];

        // The next cell of the path (a cell between when the step is diagonal, so the path stays 4-connected); false when the path crosses itself.
        public bool Advance(bool descend)
        {
            for (var tries = 0; tries < 12; tries++)
            {
                var table = (_heading >> 4) & 63;
                if (descend)
                {
                    Bend();
                }

                _px += HeadingCos[table];
                _pz += HeadingSin[table];
                var (nx, nz) = (_px >> 10, _pz >> 10);
                if (nx == _cx && nz == _cz)
                {
                    continue;
                }

                if (nx != _cx && nz != _cz)
                {
                    var corner = _rng.NextInt(2) == 0 ? (nx, _cz) : (_cx, nz);
                    if (!Add(corner, descend))
                    {
                        return false;
                    }
                }

                (_cx, _cz) = (nx, nz);
                return Add((nx, nz), descend);
            }

            return false;
        }

        private void Bend()
        {
            if (_turnLeft-- <= 0)
            {
                _turn = _rng.NextInt(-_spec.Bend, _spec.Bend + 1);
                _turnLeft = _rng.NextInt(_spec.BendSegmentMin, _spec.BendSegmentMax + 1);
            }

            _heading += _turn;
            if (Math.Abs(_heading - _heading0) > _spec.MaxTurn * 16)
            {
                _heading = _heading0 + (Math.Sign(_heading - _heading0) * _spec.MaxTurn * 16);
                _turn = -_turn;
            }
        }

        private bool Add((int X, int Z) cell, bool descend = true)
        {
            if (!_seen.Add(cell))
            {
                return false;
            }

            if (Nodes.Count > 0 && descend)
            {
                if (_runLeft-- <= 0)
                {
                    _run = _rng.NextInt(1000) < _spec.LevelShare ? 0 : _rng.NextInt(1, _spec.RunMax + 1);
                    _runLeft = _run == 0 ? _rng.NextInt(2, 5) : _rng.NextInt(_spec.SegmentMin, _spec.SegmentMax + 1);
                    _sinceDrop = 0;
                }

                if (_run > 0 && ++_sinceDrop >= _run)
                {
                    _floor--;
                    _sinceDrop = 0;
                }
            }

            if (_driftLeft <= 0)
            {
                _from = _to;
                _to = DrawSize();
                _driftLength = _rng.NextInt(4, 9);
                _driftLeft = _driftLength;
            }

            var step = _driftLength - _driftLeft--;
            var half = _from.Half + ((_to.Half - _from.Half) * step / _driftLength);
            var head = _from.Head + ((_to.Head - _from.Head) * step / _driftLength);
            if (Nodes.Count < _spec.FunnelLength)
            {
                half += half * _spec.Funnel * (_spec.FunnelLength - Nodes.Count) / (_spec.FunnelLength * 1000);
            }

            Nodes.Add(new PathNode(cell.X, cell.Z, _floor, Math.Max(4, half), Math.Clamp((head + 8) / 16, _spec.HeadroomMin, _spec.HeadroomMax)));
            return true;
        }

        // A width (half of it, in sixteenths of a block) and a headroom (in sixteenths) drawn from the data's ranges.
        private (int Half, int Head) DrawSize() => (
            (8 * (_spec.WidthMin - 1)) + _rng.NextInt((8 * (_spec.WidthMax - _spec.WidthMin)) + 1),
            (16 * _spec.HeadroomMin) + _rng.NextInt((16 * (_spec.HeadroomMax - _spec.HeadroomMin)) + 1));
    }

    private static bool InBox(PathNode node, (int MinX, int MinZ, int MaxX, int MaxZ) box) => node.X >= box.MinX && node.X <= box.MaxX && node.Z >= box.MinZ && node.Z <= box.MaxZ;

    // One cell of the path: its centre plain and dry, with ground that reaches the floor, away from every region and its margin (checked every fourth cell).
    private bool NodeHolds(Probe probe, PathNode node, int index)
    {
        var column = probe.At(node.X, node.Z);
        if (!column.Usable || column.Height < node.Floor)
        {
            return false;
        }

        return !(index % 4 == 0 && _world.Regions is { } blend && blend.Regions.Nearest(node.X, node.Z).Distance < blend.Data.Margin + 16);
    }

    // No water, river or lake carve within the water reach of the whole passage (sampled every second column), so no water pass or bank cut touches the ground it stands on and no cave of the passage comes
    // within the caves' water margin of water.
    private bool WaterClear(Probe probe, List<PathNode> nodes)
    {
        var reach = _spec.WaterReach + 4;
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        foreach (var node in nodes)
        {
            minX = Math.Min(minX, node.X);
            minZ = Math.Min(minZ, node.Z);
            maxX = Math.Max(maxX, node.X);
            maxZ = Math.Max(maxZ, node.Z);
        }

        for (var z = minZ - reach; z <= maxZ + reach; z += 2)
        {
            for (var x = minX - reach; x <= maxX + reach; x += 2)
            {
                if (probe.At(x, z).Wet)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // Whether the block is cave air as the caves cut it (not lava, under the surface cover): the passage ends where its standing block and the one above it are.
    private bool InCave(Probe probe, int x, int y, int z)
    {
        var height = probe.At(x, z).Height;
        var ceiling = Math.Min(height - _underground.Data.Caves.SurfaceCover, _underground.Data.Caves.TopY);
        return _underground.Caves.At(x, y, z, ceiling) == CaveCell.Air && _underground.Caves.At(x, y + 1, z, ceiling) == CaveCell.Air;
    }

    /// <summary>What the planner reads of one column.</summary>
    /// <param name="Height">The height model's ground.</param>
    /// <param name="Wet">Whether water or a river's or lake's carve touches it.</param>
    /// <param name="Plain">Whether the biome's surface strategy leaves the ground as the model made it.</param>
    private readonly record struct Column(int Height, bool Wet, bool Plain)
    {
        public bool Usable => Plain && !Wet;
    }

    // The columns the planner reads, remembered for one site's resolution.
    private sealed class Probe
    {
        private readonly EntranceSites _owner;
        private readonly Dictionary<(int X, int Z), Column> _columns = [];

        public Probe(EntranceSites owner) => _owner = owner;

        public Column At(int x, int z)
        {
            if (_columns.TryGetValue((x, z), out var known))
            {
                return known;
            }

            var world = _owner._world;
            var climate = world.Climate.At(x, z);
            var shape = world.Height.Shape(x, z, climate);
            var plain = true;
            if (world.HasSurfaceStrategies)
            {
                var row = world.Table.Classify(climate, shape.Height, x, z, shape.Water).Row;
                plain = string.Equals(row.SurfaceStrategy, StrategyRegistry.PlainSurface, StringComparison.Ordinal);
            }

            var column = new Column(shape.Height, shape.Water || shape.RiverWeight > 0 || shape.LakeWeight > 0, plain);
            _columns[(x, z)] = column;
            return column;
        }
    }
}
