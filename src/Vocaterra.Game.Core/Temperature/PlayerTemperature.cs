namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The temperature as the player feels it (I06): the place's temperature (<see cref="PlaceTemperature"/>) plus the player's warmth (<see cref="IWarmthSource"/>), read against the
/// data's bands. The session updates it once a tick; what it holds is derived from the stored time, the weather and the world, never saved, and the only thing it remembers
/// is whether the feeling is cold, so «Tengo frío» is said once on the way into the cold and again only after the feeling has risen out of it. Cold never harms.
/// </summary>
public sealed class PlayerTemperature
{
    private readonly TemperatureData _data;
    private readonly PlaceTemperature _place;
    private bool _chilled;
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

    /// <summary>The player's warmth: <see cref="NoWarmth"/> until the equipment gives one (I03a).</summary>
    public IWarmthSource Warmth { get; set; }

    /// <summary>The place's temperature held at this many tenths of a degree (<c>--temperature</c>), or <see langword="null"/> when it is worked out from the world. The warmth is still added.</summary>
    public int? HeldTenths { get; set; }

    /// <summary>The reading of the last <see cref="Update"/>, or <see langword="null"/> before the first.</summary>
    public TemperatureReading? Current => _current;

    /// <summary>Reads the temperature for this tick: the place (or the held value), the warmth and the band, and notes a first step into the cold.</summary>
    /// <param name="situation">The place and the moment.</param>
    public TemperatureReading Update(in TemperatureSituation situation)
    {
        var place = HeldTenths ?? _place.TenthsAt(situation);
        var warmth = Warmth.WarmthTenths();
        var band = _data.BandFor(place + warmth);
        var reading = new TemperatureReading(place, warmth, band, HeldTenths is not null);
        if (band.Chill > 0 && !_chilled && band.Line is not null)
        {
            _pendingLine = band.Line;
        }

        _chilled = band.Chill > 0;
        _current = reading;
        return reading;
    }

    /// <summary>The line key waiting to be shown (said once on entering the cold), taken and cleared; <see langword="null"/> when none waits.</summary>
    public string? TakeLine()
    {
        var line = _pendingLine;
        _pendingLine = null;
        return line;
    }
}
