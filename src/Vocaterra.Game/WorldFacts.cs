using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game;

/// <summary>
/// The nearest tile of a kind found by <see cref="WorldFacts.Nearest"/>, and where it lies from the place the search started.
/// </summary>
/// <param name="Tile">The tile found.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Dx">Columns from the start to the tile (positive is to the right).</param>
/// <param name="Dy">Rows from the start to the tile (positive is down).</param>
public sealed record NearestTile(TilePoint Tile, TileId Kind, int Dx, int Dy)
{
    /// <summary>The distance in blocks: the larger of the two axes, which is how many blocks the player walks or climbs to get level.</summary>
    public int Blocks => Math.Max(Math.Abs(Dx), Math.Abs(Dy));

    /// <summary>The direction as a Spanish phrase (see <see cref="WorldFacts.DirectionPhrase"/>).</summary>
    public string Direction => WorldFacts.DirectionPhrase(Dx, Dy);

    /// <summary>The distance in blocks spelled in Spanish, for example <c>doce</c>.</summary>
    public string BlocksInWords => WorldFacts.NumberInWords(Blocks);
}

/// <summary>
/// What the guide can say about the world, as plain facts: the nearest tile of a kind, the direction to it in Spanish and its distance in
/// blocks. Everything is integer arithmetic over the tile grid, so the same world and the same start tile give the same answer on every
/// machine. The words are the pointing map (<c>árbol</c> is a trunk, <c>piedra</c> is stone); which tile is which noun for the colour of the
/// world is the bundle's <c>game/nouns.json</c> (<see cref="NounCatalog"/>), and the two agree on every noun the guide can point at.
/// </summary>
public sealed class WorldFacts
{
    /// <summary>How far from the start the search looks, in tiles on each axis.</summary>
    public const int DefaultRadius = 64;

    /// <summary>The Spanish direction phrases.</summary>
    public const string Left = "a la izquierda";

    /// <summary>The phrase for a target to the right.</summary>
    public const string Right = "a la derecha";

    /// <summary>The phrase for a target above.</summary>
    public const string Up = "arriba";

    /// <summary>The phrase for a target below.</summary>
    public const string Down = "abajo";

    private static readonly Dictionary<string, TileId[]> TilesByNoun = new(StringComparer.Ordinal)
    {
        ["árbol"] = [TileId.Trunk],
        ["hoja"] = [TileId.Leaves],
        ["hierba"] = [TileId.Grass],
        ["tierra"] = [TileId.Dirt],
        ["piedra"] = [TileId.Stone],
        ["roca"] = [TileId.Stone],
        ["arena"] = [TileId.Sand],
        ["flor"] = [TileId.Flower],
        ["seta"] = [TileId.Mushroom],
        ["madera"] = [TileId.Planks],
        ["puerta"] = [TileId.Door],
        ["mesa"] = [TileId.Table],
        ["silla"] = [TileId.Chair],
        ["cama"] = [TileId.Bed],
        ["caja"] = [TileId.Chest],
        ["horno"] = [TileId.Furnace],
        ["cristal"] = [TileId.Glass],
        ["ladrillo"] = [TileId.Brick],
    };

    private readonly int _width;
    private readonly int _height;
    private readonly Func<int, int, TileId> _tileAt;

    /// <summary>Creates the facts over a world (read only; the world may change under it, every call reads the grid as it is).</summary>
    public WorldFacts(World world)
        : this((world ?? throw new ArgumentNullException(nameof(world))).Width, world.Height, world.GetTile)
    {
    }

    /// <summary>Creates the facts over any grid of the given size, read through <paramref name="tileAt"/> (a function of column and row).</summary>
    public WorldFacts(int width, int height, Func<int, int, TileId> tileAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(tileAt);
        _width = width;
        _height = height;
        _tileAt = tileAt;
    }

    /// <summary>
    /// The tile kinds a Spanish noun (a lemma such as <c>árbol</c>, <c>piedra</c> or <c>hierba</c>) names. A noun the world does not have yet
    /// (<c>cueva</c> waits for P7) gives an empty list.
    /// </summary>
    public static IReadOnlyList<TileId> TilesOfNoun(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        return TilesByNoun.TryGetValue(lemma, out var tiles) ? tiles : [];
    }

    /// <summary>The kind of the tile at a position; air outside the world.</summary>
    public TileId TileAt(TilePoint tile) => tile.X >= 0 && tile.Y >= 0 && tile.X < _width && tile.Y < _height ? _tileAt(tile.X, tile.Y) : TileId.Air;

    /// <summary>
    /// The Spanish noun for a tile kind, or <see langword="null"/> for air. The leaves are part of the tree, so they are <c>árbol</c> too (the
    /// same noun as the catalogue's <c>game/nouns.json</c>, which colours the whole tree with it).
    /// </summary>
    public static string? NounOfTile(TileId tile) => tile switch
    {
        TileId.Trunk => "árbol",
        TileId.Leaves => "árbol",
        TileId.Grass => "hierba",
        TileId.Dirt => "tierra",
        TileId.Stone => "piedra",
        TileId.Sand => "arena",
        TileId.Flower => "flor",
        TileId.Mushroom => "seta",
        TileId.Planks => "madera",
        TileId.Door => "puerta",
        TileId.Table => "mesa",
        TileId.Chair => "silla",
        TileId.Bed => "cama",
        TileId.Chest => "caja",
        TileId.Furnace => "horno",
        TileId.Glass => "cristal",
        TileId.Brick => "ladrillo",
        _ => null,
    };

    /// <summary>The tile under the middle of the player's body, which is where the guide measures from.</summary>
    public static TilePoint CenterTile(PlayerState player) => new(
        FloorDiv(player.X + (PhysicsConstants.PlayerWidth / 2), PhysicsConstants.UnitsPerTile),
        FloorDiv(player.Y + (PhysicsConstants.PlayerHeight / 2), PhysicsConstants.UnitsPerTile));

    /// <summary>
    /// The direction from a start tile to a target as a Spanish phrase. The axis with the larger distance leads (a tie goes to the horizontal
    /// one); the other axis is added, vertical first, when it is at least two blocks and at least half the leading one:
    /// <c>a la derecha</c>, <c>arriba</c>, <c>arriba, a la izquierda</c>. A zero offset gives an empty string.
    /// </summary>
    public static string DirectionPhrase(int dx, int dy)
    {
        var ax = Math.Abs(dx);
        var ay = Math.Abs(dy);
        if (ax == 0 && ay == 0)
        {
            return string.Empty;
        }

        var horizontal = dx < 0 ? Left : Right;
        var vertical = dy < 0 ? Up : Down;
        var horizontalLeads = ax >= ay;
        var lead = horizontalLeads ? ax : ay;
        var other = horizontalLeads ? ay : ax;
        if (other < 2 || other * 2 < lead)
        {
            return horizontalLeads ? horizontal : vertical;
        }

        return vertical + ", " + horizontal;
    }

    /// <summary>A whole number spelled in Spanish (the core's <c>SpanishNumbers</c>): 12 is <c>doce</c>.</summary>
    public static string NumberInWords(int value) => SpanishNumbers.ToWords(value);

    /// <summary>
    /// The nearest tile of one of the kinds within <paramref name="radius"/> tiles of <paramref name="from"/> on each axis, by the squared
    /// straight-line distance between tile centres. Ties go to the tile on the higher row (smaller y), then to the one on the left (smaller x):
    /// the search reads rows top to bottom and columns left to right and keeps the first tile of the smallest distance. Returns
    /// <see langword="null"/> when none is in range. The search sees through rock: it measures distance, not a path.
    /// </summary>
    public NearestTile? Nearest(TilePoint from, IReadOnlyCollection<TileId> kinds, int radius = DefaultRadius)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        if (kinds.Count == 0)
        {
            return null;
        }

        long bestDistance = long.MaxValue;
        NearestTile? best = null;
        var top = Math.Max(0, from.Y - radius);
        var bottom = Math.Min(_height - 1, from.Y + radius);
        var left = Math.Max(0, from.X - radius);
        var right = Math.Min(_width - 1, from.X + radius);
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                var tile = _tileAt(x, y);
                if (tile == TileId.Air || !Contains(kinds, tile))
                {
                    continue;
                }

                var dx = x - from.X;
                var dy = y - from.Y;
                var distance = ((long)dx * dx) + ((long)dy * dy);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new NearestTile(new TilePoint(x, y), tile, dx, dy);
                }
            }
        }

        return best;
    }

    /// <summary>The nearest tile that a Spanish noun names (see <see cref="TilesOfNoun"/>), or <see langword="null"/>.</summary>
    public NearestTile? NearestOfNoun(TilePoint from, string lemma, int radius = DefaultRadius) => Nearest(from, TilesOfNoun(lemma), radius);

    private static bool Contains(IReadOnlyCollection<TileId> kinds, TileId tile)
    {
        foreach (var kind in kinds)
        {
            if (kind == tile)
            {
                return true;
            }
        }

        return false;
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        return (value % divisor != 0 && (value < 0) != (divisor < 0)) ? quotient - 1 : quotient;
    }
}
