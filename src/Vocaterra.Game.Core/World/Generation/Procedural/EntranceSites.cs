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
/// The ground the ramp is planned on is the height model's own. A column the water passes or a surface strategy could change is never used: the passage keeps <see cref="EntranceSpec.WaterReach"/>
/// columns clear of any water, river or lake carve, and stands only on plain biomes, so the final ground is the model's and the passage's floor always meets it (a test reads the generated blocks to check).
/// </para>
/// </summary>
internal sealed class EntranceSites
{
    private static readonly (int X, int Z)[] Directions = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    private readonly GenerationWorld _world;
    private readonly UndergroundWorld _underground;
    private readonly EntranceSpec _spec;
    private readonly ConcurrentDictionary<(int X, int Z), EntranceRamp?> _sites = new();
    private readonly int _dampReach;

    /// <summary>Creates the sites of a world.</summary>
    /// <param name="world">The world; it must have an underground and its height model, climate and biome table already built.</param>
    public EntranceSites(GenerationWorld world)
    {
        _world = world;
        _underground = world.Underground ?? throw new InvalidOperationException("The world has no underground.");
        _spec = _underground.Data.Entrances;
        _dampReach = _underground.Data.Linings?.DampReach ?? 0;
    }

    /// <summary>How far, in blocks, a ramp's rim, landing and damp zone reach from its mouth.</summary>
    public int Reach => _spec.MaxLength + _spec.Landing + _spec.RimWidth + _dampReach + 8;

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
                var (dx, dz) = Directions[(first + k) % Directions.Length];
                if (Plan(probe, ax, az, dx, dz, mouth.Height, out var cover) is { } ramp && cover > bestCover)
                {
                    best = ramp;
                    bestCover = cover;
                }
            }
        }

        return best;
    }

    // The passage of one direction, or null when the ground, the water, a region or the lack of a cave under it forbids it; cover is how much ground the passage's first blocks lie under (a hillside has more).
    private EntranceRamp? Plan(Probe probe, int ax, int az, int dx, int dz, int mouthY, out int cover)
    {
        cover = 0;
        var spec = _spec;
        var lateralMin = -((spec.Width - 1) / 2);
        var lateralMax = spec.Width / 2;
        var lava = _underground.LavaLevel;
        for (var l = lateralMin; l <= lateralMax; l++)
        {
            // The block before the mouth is on the ground a body walks in from: at most one block over or under the mouth's floor, so the first step is one block.
            var before = probe.At(ax - dx - (dz * l), az - dz + (dx * l));
            if (!before.Usable || Math.Abs(before.Height - mouthY) > 1)
            {
                return null;
            }
        }

        var length = -1;
        for (var i = 0; i <= spec.MaxLength && length < 0; i++)
        {
            var floor = mouthY - (i / spec.Run);
            if (floor + 1 <= lava + 3)
            {
                return null; // never down into the lava's caves
            }

            if (!CrossSectionHolds(probe, ax + (dx * i), az + (dz * i), -dz, dx, lateralMin, lateralMax, floor, i % 4 == 0))
            {
                return null;
            }

            if (i <= 12)
            {
                cover += probe.At(ax + (dx * i), az + (dz * i)).Height - floor;
            }

            if (mouthY - floor >= spec.MinDrop && InCave(probe, ax + (dx * i), floor + 1, az + (dz * i)))
            {
                length = i;
            }
        }

        if (length < 0)
        {
            return null;
        }

        var end = mouthY - (length / spec.Run);
        for (var i = length + 1; i <= length + spec.Landing; i++)
        {
            if (!CrossSectionHolds(probe, ax + (dx * i), az + (dz * i), -dz, dx, lateralMin, lateralMax, end, i % 4 == 0))
            {
                return null;
            }
        }

        if (!WaterClear(probe, ax, az, ax + (dx * (length + spec.Landing)), az + (dz * (length + spec.Landing))))
        {
            return null;
        }

        return new EntranceRamp(ax, az, dx, dz, mouthY, length, spec.Landing, spec.Width, spec.Headroom, spec.Run, spec.RimLength, spec.RimWidth);
    }

    // One cross-section of the passage: every column plain, dry, away from every region and its margin (checked on the centre every fourth block), with ground that reaches the floor (the centre) or
    // one block under it (the sides, which the pass fills).
    private bool CrossSectionHolds(Probe probe, int cx, int cz, int px, int pz, int lateralMin, int lateralMax, int floor, bool checkRegion)
    {
        for (var l = lateralMin; l <= lateralMax; l++)
        {
            var column = probe.At(cx + (px * l), cz + (pz * l));
            if (!column.Usable || column.Height < floor - (l == 0 ? 0 : 1))
            {
                return false;
            }
        }

        if (checkRegion && _world.Regions is { } blend && blend.Regions.Nearest(cx, cz).Distance < blend.Data.Margin + _spec.RimWidth + 16)
        {
            return false;
        }

        return true;
    }

    // No water, river or lake carve within the water reach of the whole passage (sampled every second column), so no water pass or bank cut touches the ground it stands on and no cave of the passage comes
    // within the caves' water margin of water.
    private bool WaterClear(Probe probe, int ax, int az, int ex, int ez)
    {
        var reach = _spec.WaterReach + _spec.Width;
        var (minX, maxX) = (Math.Min(ax, ex) - reach, Math.Max(ax, ex) + reach);
        var (minZ, maxZ) = (Math.Min(az, ez) - reach, Math.Max(az, ez) + reach);
        for (var z = minZ; z <= maxZ; z += 2)
        {
            for (var x = minX; x <= maxX; x += 2)
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
