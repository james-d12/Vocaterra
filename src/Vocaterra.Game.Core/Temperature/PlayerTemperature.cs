namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The temperature as the player feels it (I06): the place's temperature (<see cref="PlaceTemperature"/>) plus the player's warmth (<see cref="IWarmthSource"/>), read against the
/// data's bands. The session updates it once a tick; what it holds is derived from the stored time, the weather and the world, never saved, and what it remembers
/// is how far the felt temperature has got towards the place's (the lag of <see cref="TemperatureLag"/>, not saved: a load starts at the place's value) and whether the feeling is cold or hot,
/// so «Tengo frío» («Tengo calor») is said once on the way in and again only after the feeling has left that side. Cold and heat never harm; they slow, and heat makes the player thirsty.
/// </summary>
public sealed class PlayerTemperature
{
    private readonly TemperatureData _data;
    private readonly PlaceTemperature _place;
    private const long Micro = 1_000_000;
    private const string Cold = "cold";
    private const string Heat = "heat";
    private string? _kind;
    private long? _feltMicro;
    private bool _wasHeld;
    private string? _pendingLine;
    private TemperatureReading? _current;

    /// <summary>A player's temperature.</summary>
    /// <param name="data">The numbers and bands.</param>
    /// <param name="place">The place's temperature, with its terms.</param>
    /// <param name="warmth">The player's warmth.</param>
    public PlayerTemperature(TemperatureData data, PlaceTemperature place, IWarmthSource warmth)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(warmth);
        _data = data;
        _place = place;
        Warmth = warmth;
    }

    /// <summary>The temperature data (the bands).</summary>
    public TemperatureData Data => _data;

    /// <summary>The place's temperature and its terms.</summary>
    public PlaceTemperature Place => _place;

    /// <summary>The player's warmth: the session sets the stat sheet's (<see cref="StatWarmth"/>, I03a); <see cref="NoWarmth"/> where there is no sheet (a test).</summary>
    public IWarmthSource Warmth { get; set; }

    /// <summary>The place's temperature held at this many tenths of a degree (<c>--temperature</c>), or <see langword="null"/> when it is worked out from the world. The warmth is still added.</summary>
    public int? HeldTenths { get; set; }

    /// <summary>The reading of the last <see cref="Update"/>, or <see langword="null"/> before the first.</summary>
    public TemperatureReading? Current => _current;

    /// <summary>
    /// Reads the temperature for this tick: the place's value moves towards what the terms give now by the lag's share of the gap (the first reading, a held value and a lag of 0 take it
    /// at once), the warmth is added, and the band is read; a first step into the cold or the heat is noted for its line.
    /// </summary>
    /// <param name="context">The place and the moment.</param>
    public TemperatureReading Update(ITemperatureContext context)
    {
        var terms = _place.Breakdown(context);
        var target = HeldTenths ?? PlaceTemperature.Sum(terms);
        var place = Felt(target);
        var warmth = Warmth.WarmthTenths();
        var band = _data.BandFor(place + warmth);
        var reading = new TemperatureReading(place, warmth, band, HeldTenths is not null, terms, target);
        var kind = band.Chill > 0 ? Cold : band.Heat > 0 ? Heat : null;
        if (kind is not null && kind != _kind && band.Line is not null)
        {
            _pendingLine = band.Line;
        }

        _kind = kind;
        _current = reading;
        return reading;
    }

    // The felt value, in millionths of a tenth so a small share of a small gap still moves: it closes the lag's share of the gap each tick (whole numbers, rounded towards the target by
    // the sign, so the approach is the same everywhere) and lands on the target when the gap is under a millionth-step.
    private int Felt(int target)
    {
        var goal = (long)target * Micro;
        var held = HeldTenths is not null;
        var resume = _wasHeld && !held; // a held value ending takes the place's at once: the flag is a test aid, not weather
        _wasHeld = held;
        if (_feltMicro is not { } felt || held || resume || _data.Lag.SecondsToSettle <= 0)
        {
            _feltMicro = goal;
            return target;
        }

        var gap = goal - felt;
        var step = gap * _data.Lag.StepPerMillion / 1_000_000;
        felt = step == 0 ? goal : felt + step;
        _feltMicro = felt;
        return (int)Math.Round(felt / (double)Micro, MidpointRounding.AwayFromZero);
    }

    /// <summary>The line key waiting to be shown (said once on entering the cold), taken and cleared; <see langword="null"/> when none waits.</summary>
    public string? TakeLine()
    {
        var line = _pendingLine;
        _pendingLine = null;
        return line;
    }
}
