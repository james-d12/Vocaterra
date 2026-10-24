using Vocaterra.Game.Voxel;

namespace Vocaterra.MonoGame.Sound;

/// <summary>A point on the ground, in blocks (x runs east, z runs south). The middle of a block is at its index plus one half.</summary>
/// <param name="X">The east-west position.</param>
/// <param name="Z">The north-south position.</param>
public readonly record struct Point2(double X, double Z);

/// <summary>The listener: the player's feet and facing, in blocks (the height is the feet's) and in <see cref="Angles"/> steps (1/4096 of a turn).</summary>
/// <param name="X">The middle of the feet, east-west.</param>
/// <param name="Y">The feet, up.</param>
/// <param name="Z">The middle of the feet, north-south.</param>
/// <param name="Yaw">The facing: 0 north, 1 024 east, 2 048 south, 3 072 west.</param>
public readonly record struct Listener(double X, double Y, double Z, int Yaw);

/// <summary>A named place of the scene (a site), for describing where the player is in the sound log.</summary>
/// <param name="Name">The site's name.</param>
/// <param name="At">Its middle.</param>
/// <param name="Reach">How near the listener must be for the place to be named, in blocks.</param>
public sealed record NamedPlace(string Name, Point2 At, double Reach);

/// <summary>
/// The places the ambient beds listen to (S25), read from the scene layout: the village, the waterfall, the lake, the river's path, the player's fire ring (the one
/// lit at the end of the session) and the lamps of the village. Engine free.
/// </summary>
public sealed class AmbientPlaces
{
    /// <summary>Creates the places from their parts (the tests use fixed ones).</summary>
    /// <param name="village">The village's middle, or <see langword="null"/>.</param>
    /// <param name="waterfall">The waterfall's lip, or <see langword="null"/>.</param>
    /// <param name="lake">The lake's middle, or <see langword="null"/>.</param>
    /// <param name="river">The river's path, in order from the waterfall to the lake.</param>
    /// <param name="playerFire">The player's own fire ring, or <see langword="null"/>.</param>
    /// <param name="lamps">The lamps of the village.</param>
    /// <param name="named">The named places (for the sound log).</param>
    public AmbientPlaces(Point2? village, Point2? waterfall, Point2? lake, IReadOnlyList<Point2> river, Point2? playerFire, IReadOnlyList<Point2> lamps, IReadOnlyList<NamedPlace> named)
    {
        Village = village;
        Waterfall = waterfall;
        Lake = lake;
        River = river ?? throw new ArgumentNullException(nameof(river));
        PlayerFire = playerFire;
        Lamps = lamps ?? throw new ArgumentNullException(nameof(lamps));
        Named = named ?? throw new ArgumentNullException(nameof(named));
    }

    /// <summary>The village's middle.</summary>
    public Point2? Village { get; }

    /// <summary>The waterfall's lip.</summary>
    public Point2? Waterfall { get; }

    /// <summary>The lake's middle.</summary>
    public Point2? Lake { get; }

    /// <summary>The river's path.</summary>
    public IReadOnlyList<Point2> River { get; }

    /// <summary>The player's own fire ring (<c>firering.player</c>).</summary>
    public Point2? PlayerFire { get; }

    /// <summary>The lamps of the village (the torches of the evening).</summary>
    public IReadOnlyList<Point2> Lamps { get; }

    /// <summary>The named places.</summary>
    public IReadOnlyList<NamedPlace> Named { get; }

    /// <summary>Reads the places of a scene layout: its sites, river, lake, waterfall, fire rings and lamps.</summary>
    /// <param name="layout">The layout.</param>
    public static AmbientPlaces From(SceneLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        static Point2 Middle(int x, int z) => new(x + 0.5, z + 0.5);
        Point2? SiteAt(string name) => layout.Site(name) is { } site ? Middle(site.X, site.Z) : null;
        var river = layout.River is { } spec ? spec.Path.Select(static p => Middle(p.X, p.Z)).ToList() : new List<Point2>();
        var lamps = layout.Props.Where(static p => p.Type == "lamp").Select(static p => Middle(p.X, p.Z)).ToList();
        var playerFire = layout.Props.FirstOrDefault(static p => p.Type == "fire_ring" && p.Name == "firering.player") is { } ring ? Middle(ring.X, ring.Z) : (Point2?)null;
        var named = new List<NamedPlace>();
        foreach (var site in layout.Sites)
        {
            var reach = site.Name switch
            {
                "camp.player" or "camp.ledge" => 12,
                "camp.guide" => 16,
                "village" => 22,
                "waterfall" => 12,
                "lake" => 30,
                "bridge.west" or "bridge.east" => 6,
                _ => 0,
            };
            if (reach > 0)
            {
                named.Add(new NamedPlace(site.Name, Middle(site.X, site.Z), reach));
            }
        }

        return new AmbientPlaces(SiteAt("village"), SiteAt("waterfall"), SiteAt("lake"), river, playerFire, lamps, named);
    }
}

/// <summary>
/// Where the ambient beds sit for a listener (S25): the weight of each bed, 0 to 1, and its pan. It is a pure function of the listener's place and facing, the
/// scene's places, <c>--dusk</c> and whether the player's fire is lit, so the same route gives the same weights at the same tick.
/// </summary>
/// <remarks>
/// The beds: <c>wind</c> on the cliff and the heights; <c>birds</c> in the lowland forest by day (scheduled chirps); <c>river</c> along the river's path and at the
/// lake; <c>waterfall</c>, louder near its lip; <c>village</c> by the houses and the stall; <c>fire</c> (scheduled crackles) by the lit fire ring and the lamps;
/// <c>insects</c> in the lowland at dusk. Distances fall off smoothly. Pan is the bearing of the source from the way the player faces: simple stereo, not 3D.
/// </remarks>
public static class AmbientBeds
{
    /// <summary>The wind on the cliff and the heights.</summary>
    public const string Wind = "wind";

    /// <summary>The forest's birds by day (scheduled chirps).</summary>
    public const string Birds = "birds";

    /// <summary>The river and the lake.</summary>
    public const string River = "river";

    /// <summary>The waterfall, louder near it.</summary>
    public const string Waterfall = "waterfall";

    /// <summary>The village's murmur, by the houses and the stall.</summary>
    public const string Village = "village";

    /// <summary>The fire: the lit fire ring and the lamps (scheduled crackles).</summary>
    public const string Fire = "fire";

    /// <summary>The evening insects (with <c>--dusk</c>).</summary>
    public const string Insects = "insects";

    /// <summary>Every bed, in a fixed order.</summary>
    public static IReadOnlyList<string> All { get; } = [Wind, Birds, River, Waterfall, Village, Fire, Insects];

    /// <summary>The level of a bed's loop, before the ambient volume (the scheduled beds, birds and fire, scale their own chirps and crackles).</summary>
    /// <param name="bed">The bed.</param>
    public static double Gain(string bed) => bed switch
    {
        Wind => 0.5,
        River => 0.45,
        Waterfall => 0.6,
        Village => 0.35,
        Insects => 0.3,
        _ => 1.0,
    };

    /// <summary>A full turn in the facing's units (1/4096 of a turn each).</summary>
    public const int FullTurn = 4096;

    /// <summary>The height (in blocks) above which the wind is full; below <see cref="WindLow"/> there is none.</summary>
    public const double WindHigh = 68;

    /// <summary>The height below which the wind is silent.</summary>
    public const double WindLow = 58;

    /// <summary>The weight of a distance: 1 within <paramref name="full"/> blocks, 0 beyond <paramref name="silent"/>, a smooth step between.</summary>
    /// <param name="distance">The distance in blocks.</param>
    /// <param name="full">The distance within which the weight is 1.</param>
    /// <param name="silent">The distance beyond which the weight is 0.</param>
    public static double Falloff(double distance, double full, double silent)
    {
        if (distance <= full)
        {
            return 1;
        }

        if (distance >= silent)
        {
            return 0;
        }

        var t = (distance - full) / (silent - full);
        return 1 - (t * t * (3 - (2 * t)));
    }

    /// <summary>A ramp from 0 at <paramref name="from"/> to 1 at <paramref name="to"/> (it goes the other way when <paramref name="to"/> is lower), clamped.</summary>
    /// <param name="value">The value.</param>
    /// <param name="from">Where the ramp starts.</param>
    /// <param name="to">Where it is full.</param>
    public static double Ramp(double value, double from, double to) => Math.Clamp((value - from) / (to - from), 0, 1);

    /// <summary>The distance from a point to the nearest point of a path (its segments), in blocks.</summary>
    /// <param name="point">The point.</param>
    /// <param name="path">The path's points, in order.</param>
    /// <param name="nearest">The nearest point of the path.</param>
    public static double DistanceToPath(Point2 point, IReadOnlyList<Point2> path, out Point2 nearest)
    {
        nearest = path.Count > 0 ? path[0] : point;
        var best = double.PositiveInfinity;
        for (var i = 0; i < path.Count; i++)
        {
            var a = path[i];
            var b = i + 1 < path.Count ? path[i + 1] : path[i];
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var lengthSquared = (dx * dx) + (dz * dz);
            var t = lengthSquared <= 0 ? 0 : Math.Clamp((((point.X - a.X) * dx) + ((point.Z - a.Z) * dz)) / lengthSquared, 0, 1);
            var p = new Point2(a.X + (dx * t), a.Z + (dz * t));
            var distance = Distance(point, p);
            if (distance < best)
            {
                best = distance;
                nearest = p;
            }
        }

        return best;
    }

    /// <summary>The horizontal distance between two points, in blocks.</summary>
    /// <param name="a">One point.</param>
    /// <param name="b">The other.</param>
    public static double Distance(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    /// <summary>
    /// The pan of a source for a listener, from -1 (left) to 1 (right): the source's bearing measured against the listener's right-hand side. A source straight
    /// ahead or behind, or on the spot, is centred.
    /// </summary>
    /// <param name="listener">The listener.</param>
    /// <param name="source">The source.</param>
    public static double Pan(Listener listener, Point2 source)
    {
        var dx = source.X - listener.X;
        var dz = source.Z - listener.Z;
        var length = Math.Sqrt((dx * dx) + (dz * dz));
        if (length < 1e-6)
        {
            return 0;
        }

        var theta = listener.Yaw * 2 * Math.PI / FullTurn;
        var right = ((dx * Math.Cos(theta)) + (dz * Math.Sin(theta))) / length;
        return Math.Clamp(right, -1, 1);
    }

    /// <summary>The weights of every bed for a listener, in <see cref="All"/> order (each bed is listed, weight 0 when it is not heard).</summary>
    /// <param name="places">The scene's places.</param>
    /// <param name="listener">The listener.</param>
    /// <param name="dusk">Whether <c>--dusk</c> is on: the birds stop and the insects start.</param>
    /// <param name="fireLit">Whether the player's own fire is lit.</param>
    public static IReadOnlyList<BedInput> At(AmbientPlaces places, Listener listener, bool dusk, bool fireLit)
    {
        ArgumentNullException.ThrowIfNull(places);
        var here = new Point2(listener.X, listener.Z);

        var villageWeight = places.Village is { } village ? Falloff(Distance(here, village), 18, 34) : 0;

        var waterfallWeight = 0.0;
        var waterfallPan = 0.0;
        if (places.Waterfall is { } lip)
        {
            waterfallWeight = Falloff(Distance(here, lip), 6, 40);
            waterfallPan = Pan(listener, lip);
        }

        var riverWeight = 0.0;
        var riverPan = 0.0;
        if (places.River.Count > 0)
        {
            var distance = DistanceToPath(here, places.River, out var nearest);
            riverWeight = Falloff(distance, 5, 28);
            riverPan = Pan(listener, nearest);
        }

        if (places.Lake is { } lake)
        {
            var lakeWeight = Falloff(Distance(here, lake), 30, 60);
            if (lakeWeight > riverWeight)
            {
                riverWeight = lakeWeight;
                riverPan = Pan(listener, lake);
            }
        }

        var windWeight = Ramp(listener.Y, WindLow, WindHigh);
        var lowland = 1 - Ramp(listener.Y, 50, 64);
        var birdsWeight = dusk ? 0 : lowland * (1 - villageWeight);
        var insectsWeight = dusk ? lowland : 0;

        var fireWeight = 0.0;
        var firePan = 0.0;
        if (fireLit && places.PlayerFire is { } fire)
        {
            fireWeight = Falloff(Distance(here, fire), 6, 24);
            firePan = Pan(listener, fire);
        }

        foreach (var lamp in places.Lamps)
        {
            var lampWeight = 0.5 * Falloff(Distance(here, lamp), 2, 9);
            if (lampWeight > fireWeight)
            {
                fireWeight = lampWeight;
                firePan = Pan(listener, lamp);
            }
        }

        return
        [
            new BedInput(Wind, windWeight),
            new BedInput(Birds, birdsWeight),
            new BedInput(River, riverWeight, riverPan),
            new BedInput(Waterfall, waterfallWeight, waterfallPan),
            new BedInput(Village, villageWeight, places.Village is { } v ? Pan(listener, v) : 0),
            new BedInput(Fire, fireWeight, firePan),
            new BedInput(Insects, insectsWeight),
        ];
    }

    /// <summary>The name of the place a listener is in, for the sound log: a named site near it, the heights, or the valley.</summary>
    /// <param name="places">The scene's places.</param>
    /// <param name="listener">The listener.</param>
    public static string PlaceOf(AmbientPlaces places, Listener listener)
    {
        var here = new Point2(listener.X, listener.Z);
        NamedPlace? best = null;
        var bestRatio = double.PositiveInfinity;
        foreach (var named in places.Named)
        {
            var ratio = Distance(here, named.At) / named.Reach;
            if (ratio < 1 && ratio < bestRatio)
            {
                best = named;
                bestRatio = ratio;
            }
        }

        if (best is not null)
        {
            return best.Name;
        }

        if (places.River.Count > 0 && DistanceToPath(here, places.River, out _) < 6)
        {
            return "river";
        }

        return listener.Y >= WindLow + 4 ? "heights" : "valley";
    }
}
