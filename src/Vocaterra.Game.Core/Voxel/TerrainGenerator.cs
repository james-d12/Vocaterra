namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// The shape of the ground as numbers: the top block of every column, the top of the water above it, and which columns belong to the route
/// (paths, level sites, the river's banks) where trees keep away. Made by <see cref="TerrainGenerator"/>; the <see cref="SceneBuilder"/> turns it
/// into blocks.
/// </summary>
internal sealed class Terrain
{
    /// <summary>The column is part of the route: trees keep a margin from it.</summary>
    public const byte RouteFlag = 1;

    /// <summary>The column is trodden path (its top block is <see cref="BlockId.Path"/>).</summary>
    public const byte PathFlag = 2;

    /// <summary>The column is on the bed or the bank of the river, the lake or the pool.</summary>
    public const byte ShoreFlag = 4;

    /// <summary>Creates empty terrain of a size.</summary>
    public Terrain(int sizeX, int sizeZ)
    {
        SizeX = sizeX;
        SizeZ = sizeZ;
        Height = new int[sizeX * sizeZ];
        WaterTop = new int[sizeX * sizeZ];
        Flags = new byte[sizeX * sizeZ];
    }

    /// <summary>The width in columns.</summary>
    public int SizeX { get; }

    /// <summary>The depth in columns.</summary>
    public int SizeZ { get; }

    /// <summary>The y of the top solid block of each column, indexed <c>z * SizeX + x</c>.</summary>
    public int[] Height { get; }

    /// <summary>The y of the top water block of each column, or -1 when the column holds no water above its ground.</summary>
    public int[] WaterTop { get; }

    /// <summary>The <see cref="RouteFlag"/>, <see cref="PathFlag"/> and <see cref="ShoreFlag"/> bits of each column.</summary>
    public byte[] Flags { get; }

    /// <summary>Whether the column holds water above its ground.</summary>
    public bool IsWater(int x, int z) => WaterTop[(z * SizeX) + x] > Height[(z * SizeX) + x];
}

/// <summary>
/// Makes the terrain of a <see cref="SceneLayout"/> for a seed, in integers only. The macro relief (a coarse grid in the layout) gives the mountains
/// along the north and the west, the foothills and the valley floor; the seed's noise adds hills and crags, and stepped faces make the cliffs. Then,
/// in this order: the waterfall's plateau, the river (a curve whose water never runs uphill), the pool, the lake, the paths (a graded switchback trail
/// and a road that follows the valley floor) and the sites' level ground. The noise is damped near the river, the paths and the sites, so the
/// seed changes the land around them and never the places themselves.
/// </summary>
internal static class TerrainGenerator
{
    private const int CalmReach = 24;
    private const int PathBlend = 3;
    private const string Pass = "terrain";

    /// <summary>Generates the terrain of the layout for the seed.</summary>
    public static Terrain Generate(SceneLayout layout, ulong seed) => new Generator(layout, seed).Run();

    private sealed class Generator
    {
        private readonly SceneLayout _layout;
        private readonly TerrainSpec _spec;
        private readonly TerrainNoise _noise;
        private readonly int _w;
        private readonly int _d;
        private readonly int[] _nat16;
        private readonly int[] _h16;
        private readonly Terrain _terrain;
        private readonly int[] _feature16;
        private int[] _riverDist = [];
        private int[] _riverAlong = [];
        private int[] _riverSeg = [];
        private Polyline? _river;
        private int[] _riverSurface = [];
        private readonly List<PathRaster> _paths = [];

        public Generator(SceneLayout layout, ulong seed)
        {
            _layout = layout;
            _spec = layout.Terrain;
            _noise = new TerrainNoise(seed, Pass);
            _w = layout.SizeX;
            _d = layout.SizeZ;
            _nat16 = new int[_w * _d];
            _h16 = new int[_w * _d];
            _terrain = new Terrain(_w, _d);
            _feature16 = new int[_w * _d];
            Array.Fill(_feature16, int.MaxValue);
        }

        public Terrain Run()
        {
            RasterizeFeatures();
            BuildNaturalGround();
            RaisePlateau();
            var surfaceOfPool = CarveRiver();
            CarveLake();
            ResolveSiteHeights();
            ApplyPaths();
            ApplySites();
            Finish(surfaceOfPool);
            return _terrain;
        }

        // ---------------------------------------------------------------- features

        private void RasterizeFeatures()
        {
            if (_layout.River is { } river)
            {
                _river = Polyline.Curve(river.Path, 4);
                _riverDist = NewDistances();
                _riverAlong = new int[_w * _d];
                _riverSeg = new int[_w * _d];
                var extent = river.HalfWidth + river.Bank;
                _river.Rasterize(extent + CalmReach, _w, _d, _riverDist, _riverAlong, _riverSeg);
                LowerFeature(_riverDist, extent * 16);
            }

            foreach (var spec in _layout.Paths)
            {
                var points = new List<(int X, int Z)> { Position(spec.From) };
                points.AddRange(spec.Via);
                points.Add(Position(spec.To));
                var line = Polyline.Straight(points);
                var raster = new PathRaster(spec, line, NewDistances(), new int[_w * _d], new int[_w * _d]);
                var extent = spec.HalfWidth + PathBlend + 1;
                line.Rasterize(extent + CalmReach, _w, _d, raster.Dist, raster.Along, raster.Segment);
                LowerFeature(raster.Dist, extent * 16);
                _paths.Add(raster);
            }

            foreach (var site in _layout.Sites)
            {
                if (site.Radius <= 0)
                {
                    continue;
                }

                var reach = site.Radius + site.Blend + CalmReach;
                for (var z = Math.Max(0, site.Z - reach); z <= Math.Min(_d - 1, site.Z + reach); z++)
                {
                    for (var x = Math.Max(0, site.X - reach); x <= Math.Min(_w - 1, site.X + reach); x++)
                    {
                        var dist = (int)IntMath.ISqrt((long)(((x - site.X) * (x - site.X)) + ((z - site.Z) * (z - site.Z))) * 256);
                        var beyond = Math.Max(0, dist - ((site.Radius + site.Blend) * 16));
                        var index = (z * _w) + x;
                        _feature16[index] = Math.Min(_feature16[index], beyond);
                    }
                }
            }
        }

        private int[] NewDistances()
        {
            var array = new int[_w * _d];
            Array.Fill(array, int.MaxValue);
            return array;
        }

        private void LowerFeature(int[] dist16, int extent16)
        {
            for (var i = 0; i < dist16.Length; i++)
            {
                if (dist16[i] != int.MaxValue)
                {
                    _feature16[i] = Math.Min(_feature16[i], Math.Max(0, dist16[i] - extent16));
                }
            }
        }

        private (int X, int Z) Position(string site)
        {
            var s = _layout.Site(site) ?? throw new SceneLayoutException($"there is no site '{site}'.");
            return (s.X, s.Z);
        }

        // ---------------------------------------------------------------- the natural ground

        private void BuildNaturalGround()
        {
            var valley16 = _spec.Valley * 16;
            var ceiling16 = _spec.Ceiling * 16;
            for (var z = 0; z < _d; z++)
            {
                for (var x = 0; x < _w; x++)
                {
                    var i = (z * _w) + x;
                    var calm = Math.Max(300, IntMath.Ramp(_feature16[i] == int.MaxValue ? CalmReach * 16 : _feature16[i], 0, CalmReach * 16));

                    // S14: the relief is looked up at a point moved by slow noise (a wide bend and a small wiggle), so the walls of the valley are not the straight
                    // lines of the chunk grid. The move is damped near the sites, the paths and the river, like the other noise, so they keep their ground.
                    var warpX = ((_noise.Value(20, x, z, 120) - 2048) * 64 / 2048) + ((_noise.Value(22, x, z, 44) - 2048) * 22 / 2048);
                    var warpZ = ((_noise.Value(21, x, z, 120) - 2048) * 64 / 2048) + ((_noise.Value(23, x, z, 44) - 2048) * 22 / 2048);
                    warpX = (int)((long)warpX * calm / IntMath.One);
                    warpZ = (int)((long)warpZ * calm / IntMath.One);
                    var basis = Relief16(Math.Clamp(x + warpX, 0, _w - 1), Math.Clamp(z + warpZ, 0, _d - 1));
                    var above16 = Math.Max(0, basis - valley16);
                    var amp16 = 24 + (int)(above16 * 16L / 100);
                    var detail = (int)((long)_noise.Hills(x, z) * amp16 / IntMath.One * calm / IntMath.One);
                    var crag = (int)((long)(_noise.Value(5, x, z, 5) - 2048) * 48 / 2048 * IntMath.Ramp(above16, 30 * 16, 60 * 16) / IntMath.One * calm / IntMath.One);
                    var h = basis + detail + crag;
                    h = Terrace(h);
                    _nat16[i] = Math.Clamp(h, 4 * 16, ceiling16);
                    _h16[i] = _nat16[i];
                }
            }
        }

        // The macro relief at a column in 1/16 blocks: a smooth surface through the chunk-corner heights (Catmull-Rom both ways).
        private int Relief16(int x, int z)
        {
            var cols = _spec.ReliefColumns;
            var rows = _spec.ReliefRows;
            var gx = x >> 4;
            var gz = z >> 4;
            var tx = (x & 15) << 8;
            var tz = (z & 15) << 8;
            Span<int> column = stackalloc int[4];
            for (var j = 0; j < 4; j++)
            {
                var row = Math.Clamp(gz + j - 1, 0, rows - 1);
                int P(int i) => _spec.Relief[(row * cols) + Math.Clamp(gx + i - 1, 0, cols - 1)] * 16;
                column[j] = IntMath.CatmullRom(P(0), P(1), P(2), P(3), tx);
            }

            return IntMath.CatmullRom(column[0], column[1], column[2], column[3], tz);
        }

        // Stepped faces: above the foothills the ground climbs in flat shelves of Terrace blocks joined by short risers, so a steep slope becomes a stepped cliff.
        private int Terrace(int h16)
        {
            if (_spec.Terrace <= 1)
            {
                return h16;
            }

            var step = _spec.Terrace * 16;
            var strength = IntMath.Ramp(h16, 64 * 16, 90 * 16);
            if (strength == 0)
            {
                return h16;
            }

            var baseLevel = h16 / step * step;
            var f = (h16 - baseLevel) * IntMath.One / step;
            var g = IntMath.Ramp(f, 1500, 2700);
            var stepped = baseLevel + (int)((long)g * step / IntMath.One);
            return IntMath.Lerp(h16, stepped, strength);
        }

        // ---------------------------------------------------------------- the waterfall

        private void RaisePlateau()
        {
            if (_layout.Waterfall is not { } fall)
            {
                return;
            }

            var site = _layout.Site(fall.Site)!;
            var top16 = fall.Top * 16;
            var reach = fall.Plateau + 12;
            for (var z = 0; z < site.Z; z++)
            {
                for (var x = Math.Max(0, site.X - reach); x <= Math.Min(_w - 1, site.X + reach); x++)
                {
                    var dx = Math.Abs(x - site.X);
                    var weight = IntMath.Ramp(dx, reach, fall.Plateau);
                    if (weight == 0)
                    {
                        continue;
                    }

                    var edge = site.Z + (dx <= (fall.Width / 2) + 2 ? 0 : (_noise.Value(6, x, 0, 10) * 7 / IntMath.One) - 3);
                    if (z >= edge)
                    {
                        continue;
                    }

                    var i = (z * _w) + x;
                    var target = top16 + (_noise.Hills(x, z) * 12 / IntMath.One);
                    _nat16[i] = Math.Clamp(IntMath.Lerp(_nat16[i], Math.Max(_nat16[i], target), weight), 4 * 16, _spec.Ceiling * 16);
                    _h16[i] = _nat16[i];
                }
            }
        }

        // ---------------------------------------------------------------- the river and the pool

        // Returns the pool's water surface (0 when there is no river).
        private int CarveRiver()
        {
            if (_layout.River is not { } river || _river is not { } line)
            {
                return 0;
            }

            // The water never runs uphill: the surface at each point of the curve is the lowest the ground has been (less two blocks) from the source
            // to there, and never under the lake.
            _riverSurface = new int[line.Count];
            var surface = int.MaxValue;
            for (var k = 0; k < line.Count; k++)
            {
                var x = line.X16[k] >> 4;
                var z = line.Z16[k] >> 4;
                surface = Math.Min(surface, (_nat16[(z * _w) + x] >> 4) - 2);
                _riverSurface[k] = Math.Max(surface, _layout.WaterLevel);
            }

            var hw = river.HalfWidth;
            var extent = hw + river.Bank;
            for (var z = 0; z < _d; z++)
            {
                for (var x = 0; x < _w; x++)
                {
                    var i = (z * _w) + x;
                    var dist16 = _riverDist[i];
                    if (dist16 > extent * 16)
                    {
                        continue;
                    }

                    var surf = _riverSurface[_riverSeg[i]];
                    var d = dist16;
                    var hw16 = hw * 16;
                    if (d <= hw16)
                    {
                        var depth = river.Depth;
                        var bed16 = ((surf - 1) * 16) - (int)((long)(depth - 1) * 16 * ((hw16 * hw16) - ((long)d * d)) / ((long)hw16 * hw16));
                        _h16[i] = Math.Min(_h16[i], bed16);
                        _terrain.WaterTop[i] = surf;
                        _terrain.Flags[i] |= Terrain.ShoreFlag;
                    }
                    else
                    {
                        var weight = IntMath.Ramp(d - hw16, 0, river.Bank * 16);
                        var bank16 = IntMath.Lerp((surf + 1) * 16, Math.Max(_nat16[i], (surf + 1) * 16), weight);
                        _h16[i] = Math.Min(_h16[i], bank16);
                        if (weight < IntMath.One)
                        {
                            _terrain.Flags[i] |= Terrain.ShoreFlag;
                        }
                    }
                }
            }

            if (_layout.Waterfall is { } fall)
            {
                CarvePool(fall, line, _riverSurface[0]);
                return _riverSurface[0];
            }

            return 0;
        }

        private void CarvePool(WaterfallSpec fall, Polyline line, int surface)
        {
            var cx = line.X16[0] >> 4;
            var cz = line.Z16[0] >> 4;
            var r = fall.PoolRadius;
            var reach = r + 4;
            var lip = _layout.Site(fall.Site)!.Z; // the pool is cut into the ground at the foot of the cliff, never into the plateau behind its edge
            for (var z = Math.Max(lip, cz - reach); z <= Math.Min(_d - 1, cz + reach); z++)
            {
                for (var x = Math.Max(0, cx - reach); x <= Math.Min(_w - 1, cx + reach); x++)
                {
                    var i = (z * _w) + x;
                    var d2 = ((x - cx) * (x - cx)) + ((z - cz) * (z - cz));
                    if (d2 <= r * r)
                    {
                        var bed16 = ((surface - 1) * 16) - (int)(4L * 16 * ((r * r) - d2) / (r * r));
                        _h16[i] = Math.Min(_h16[i], bed16);
                        _terrain.WaterTop[i] = Math.Max(_terrain.WaterTop[i], surface);
                        _terrain.Flags[i] |= Terrain.ShoreFlag;
                    }
                    else if (d2 <= reach * reach)
                    {
                        var d = (int)IntMath.ISqrt((long)d2 * 256);
                        var weight = IntMath.Ramp(d - (r * 16), 0, 4 * 16);
                        var bank16 = IntMath.Lerp((surface + 1) * 16, Math.Max(_nat16[i], (surface + 1) * 16), weight);
                        _h16[i] = Math.Min(_h16[i], bank16);
                        if (weight < IntMath.One)
                        {
                            _terrain.Flags[i] |= Terrain.ShoreFlag;
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------- the lake

        private void CarveLake()
        {
            if (_layout.Lake is not { } lake)
            {
                return;
            }

            var site = _layout.Site(lake.Site)!;
            var level = _layout.WaterLevel;
            var shore = 1300; // the shore slope's width in the ellipse's own units (4 096 is the lake's edge)
            var reachX = (lake.RadiusX * (IntMath.One + shore + 700) / IntMath.One) + 2;
            var reachZ = (lake.RadiusZ * (IntMath.One + shore + 700) / IntMath.One) + 2;
            for (var z = Math.Max(0, site.Z - reachZ); z <= Math.Min(_d - 1, site.Z + reachZ); z++)
            {
                for (var x = Math.Max(0, site.X - reachX); x <= Math.Min(_w - 1, site.X + reachX); x++)
                {
                    var i = (z * _w) + x;
                    var ex = (long)(x - site.X) * IntMath.One / lake.RadiusX;
                    var ez = (long)(z - site.Z) * IntMath.One / lake.RadiusZ;
                    var r = IntMath.ISqrt((ex * ex) + (ez * ez)); // 4 096 at the ellipse's edge
                    r = r * IntMath.One / (3584 + (_noise.Value(7, x, z, 24) / 4)); // the shoreline wanders by about 12 percent
                    if (r < IntMath.One)
                    {
                        var bed16 = ((level - 1) * 16) - (int)((long)(lake.Depth - 1) * 16 * ((IntMath.One * (long)IntMath.One) - (r * r)) / ((long)IntMath.One * IntMath.One));
                        _h16[i] = Math.Min(_h16[i], bed16);
                        _terrain.WaterTop[i] = Math.Max(_terrain.WaterTop[i], level);
                        _terrain.Flags[i] |= Terrain.ShoreFlag;
                    }
                    else if (r < IntMath.One + shore)
                    {
                        var weight = IntMath.Ramp((int)r - IntMath.One, 0, shore);
                        var bank16 = IntMath.Lerp((level + 1) * 16, Math.Max(_nat16[i], (level + 1) * 16), weight);
                        _h16[i] = Math.Min(_h16[i], bank16);
                        if (weight < IntMath.One)
                        {
                            _terrain.Flags[i] |= Terrain.ShoreFlag;
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------- sites and paths

        private readonly Dictionary<string, int> _siteY = new(StringComparer.Ordinal);

        // A site with no y of its own takes the height the ground has at its centre now (after the water has been cut into it).
        private void ResolveSiteHeights()
        {
            foreach (var site in _layout.Sites)
            {
                _siteY[site.Name] = site.Y ?? ((_h16[(site.Z * _w) + site.X] + 8) >> 4);
            }
        }

        private void ApplyPaths()
        {
            var riverHalf = _layout.River?.HalfWidth ?? 0;
            foreach (var raster in _paths)
            {
                var spec = raster.Spec;
                var profile = raster.Mode == PathMode.Grade ? GradeProfile(raster) : FollowProfile(raster);
                var surface16 = (spec.HalfWidth * 16) + 8;
                var reach16 = surface16 + (PathBlend * 16);
                for (var i = 0; i < raster.Dist.Length; i++)
                {
                    var d = raster.Dist[i];
                    if (d > reach16)
                    {
                        continue;
                    }

                    if (_river is not null && _riverDist[i] <= (riverHalf * 16) + 8)
                    {
                        continue; // the water: a bridge crosses it, the path does not fill it
                    }

                    var along = raster.Along[i];
                    var target = profile(along);
                    if (d <= surface16)
                    {
                        _h16[i] = target;
                        _terrain.Flags[i] |= Terrain.RouteFlag | Terrain.PathFlag;
                    }
                    else
                    {
                        var weight = IntMath.One - IntMath.Smooth((d - surface16) * IntMath.One / (PathBlend * 16));
                        _h16[i] = IntMath.Lerp(_h16[i], target, weight);
                    }
                }
            }
        }

        // A graded path changes its height evenly from the height of its first site to the height of its last.
        private Func<int, int> GradeProfile(PathRaster raster)
        {
            var y0 = _siteY[raster.Spec.From] * 16;
            var y1 = _siteY[raster.Spec.To] * 16;
            var length = Math.Max(1L, raster.Line.Length16);
            return along => (int)(y0 + ((long)(y1 - y0) * Math.Clamp(along, 0, length) / length));
        }

        // A path that follows the ground takes the natural height under its centre line, smoothed over about nine blocks so it never steps more than a block.
        private Func<int, int> FollowProfile(PathRaster raster)
        {
            var line = raster.Line;
            var length = (int)(line.Length16 / 16) + 1;
            var samples = new int[length + 1];
            var k = 0;
            for (var s = 0; s <= length; s++)
            {
                while (k + 2 < line.Count && line.Cum16[k + 1] < s * 16L)
                {
                    k++;
                }

                var span = Math.Max(1L, line.Cum16[k + 1] - line.Cum16[k]);
                var t = Math.Clamp((s * 16L) - line.Cum16[k], 0, span);
                var x = (int)((line.X16[k] + ((line.X16[k + 1] - line.X16[k]) * t / span)) >> 4);
                var z = (int)((line.Z16[k] + ((line.Z16[k + 1] - line.Z16[k]) * t / span)) >> 4);
                samples[s] = _nat16[(Math.Clamp(z, 0, _d - 1) * _w) + Math.Clamp(x, 0, _w - 1)];
            }

            var smooth = new int[samples.Length];
            for (var s = 0; s < samples.Length; s++)
            {
                long sum = 0;
                var count = 0;
                for (var j = Math.Max(0, s - 4); j <= Math.Min(samples.Length - 1, s + 4); j++)
                {
                    sum += samples[j];
                    count++;
                }

                smooth[s] = (int)(sum / count);
            }

            // Whole blocks, so the path stays level across its width and steps one block at a time.
            return along => (((smooth[Math.Clamp(along / 16, 0, smooth.Length - 1)] + 8) >> 4) * 16);
        }

        private void ApplySites()
        {
            foreach (var site in _layout.Sites)
            {
                if (site.Radius <= 0)
                {
                    continue;
                }

                var y16 = _siteY[site.Name] * 16;
                var r16 = site.Radius * 16;
                var b16 = Math.Max(1, site.Blend * 16);
                var reach = site.Radius + site.Blend + 1;
                for (var z = Math.Max(0, site.Z - reach); z <= Math.Min(_d - 1, site.Z + reach); z++)
                {
                    for (var x = Math.Max(0, site.X - reach); x <= Math.Min(_w - 1, site.X + reach); x++)
                    {
                        var i = (z * _w) + x;
                        var dist = (int)IntMath.ISqrt((long)(((x - site.X) * (x - site.X)) + ((z - site.Z) * (z - site.Z))) * 256);
                        if (dist <= r16)
                        {
                            _h16[i] = y16;
                            _terrain.Flags[i] |= Terrain.RouteFlag;
                        }
                        else if (dist < r16 + b16)
                        {
                            var weight = IntMath.One - IntMath.Smooth((dist - r16) * IntMath.One / b16);
                            _h16[i] = IntMath.Lerp(_h16[i], y16, weight);
                            _terrain.Flags[i] |= Terrain.RouteFlag;
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------- the result

        private void Finish(int poolSurface)
        {
            var ceiling = _spec.Ceiling;
            for (var i = 0; i < _h16.Length; i++)
            {
                var h = Math.Clamp((_h16[i] + 8) >> 4, 2, ceiling);
                _terrain.Height[i] = h;
                if (_terrain.WaterTop[i] <= h)
                {
                    _terrain.WaterTop[i] = h < _layout.WaterLevel ? _layout.WaterLevel : -1;
                }
                else if (_terrain.WaterTop[i] > 0)
                {
                    _terrain.Flags[i] |= Terrain.ShoreFlag;
                }
            }

            if (_layout.Waterfall is { } fall)
            {
                MakeWaterfall(fall, poolSurface);
            }
        }

        // The falling water: a sheet of water in the columns just beyond the cliff's edge, from the pool up to a small pond on the plateau.
        private void MakeWaterfall(WaterfallSpec fall, int poolSurface)
        {
            var site = _layout.Site(fall.Site)!;
            var half = fall.Width / 2;
            for (var x = site.X - half; x <= site.X + (fall.Width - 1 - half); x++)
            {
                var i = (site.Z * _w) + x;
                _terrain.Height[i] = Math.Min(_terrain.Height[i], poolSurface - 1);
                _terrain.WaterTop[i] = fall.Top;
                _terrain.Flags[i] |= Terrain.RouteFlag | Terrain.ShoreFlag;
                for (var z = site.Z - 4; z < site.Z; z++)
                {
                    var j = (z * _w) + x;
                    _terrain.Height[j] = fall.Top - 1;
                    _terrain.WaterTop[j] = fall.Top;
                    _terrain.Flags[j] |= Terrain.RouteFlag | Terrain.ShoreFlag;
                }
            }

            // The pond is a little wider than the falling sheet, so the edge of the cliff is not a notch.
            foreach (var x in new[] { site.X - half - 1, site.X + (fall.Width - 1 - half) + 1 })
            {
                for (var z = site.Z - 4; z < site.Z; z++)
                {
                    var j = (z * _w) + x;
                    _terrain.Height[j] = Math.Min(_terrain.Height[j], fall.Top - 1);
                    _terrain.WaterTop[j] = fall.Top;
                    _terrain.Flags[j] |= Terrain.RouteFlag | Terrain.ShoreFlag;
                }
            }
        }

        private sealed class PathRaster(PathSpec spec, Polyline line, int[] dist, int[] along, int[] segment)
        {
            public PathSpec Spec { get; } = spec;

            public PathMode Mode => Spec.Mode;

            public Polyline Line { get; } = line;

            public int[] Dist { get; } = dist;

            public int[] Along { get; } = along;

            public int[] Segment { get; } = segment;
        }
    }
}
