namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The temperature where the player stands (I06): the sum of the terms it was built with, in tenths of a degree Celsius, as an integer. It owns no rule of its own and
/// names no term, so adding one is a new <see cref="ITemperatureTerm"/> and one more entry in <see cref="TemperatureTermRegistry"/> and a row of data. The sum is
/// whole-number arithmetic, kept inside <see cref="LimitTenths"/> either way, so it is the same on every machine and cannot overflow.
/// </summary>
public sealed class PlaceTemperature
{
    /// <summary>The most the sum may be, either way, in tenths (1000 degrees): a limit against a bad row's overflow, far outside any real value.</summary>
    public const int LimitTenths = 10_000;

    private readonly ITemperatureTerm[] _terms;
    private readonly PlaceColumns _columns = new();

    /// <summary>A temperature of these terms.</summary>
    /// <param name="terms">The terms, in the order a breakdown lists them (the order does not change the sum).</param>
    /// <param name="environment">What the terms share (the ground, the biomes' adjustments); null for <see cref="TemperatureEnvironment.Plain"/>.</param>
    public PlaceTemperature(IEnumerable<ITemperatureTerm> terms, TemperatureEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(terms);
        _terms = [.. terms];
        Environment = environment ?? TemperatureEnvironment.Plain;
    }

    /// <summary>The temperature the data describes: its terms built by a registry (the game's own unless another is given) and its environment.</summary>
    /// <param name="data">The data.</param>
    /// <param name="registry">The registry that builds the data's terms (null for <see cref="TemperatureTermRegistry.Standard"/>).</param>
    public static PlaceTemperature From(TemperatureData data, TemperatureTermRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new PlaceTemperature((registry ?? TemperatureTermRegistry.Standard).Build(data.Terms), data.Environment);
    }

    /// <summary>What the terms share.</summary>
    public TemperatureEnvironment Environment { get; }

    /// <summary>The context of a moment, with this temperature's environment and its column cache.</summary>
    /// <param name="atmosphere">The sky and the weather.</param>
    /// <param name="feet">The block under the feet.</param>
    /// <param name="coveredShare">The share of cover round the feet.</param>
    /// <param name="fires">The fire rings, if any.</param>
    /// <param name="places">The world's place queries, or null.</param>
    public TemperatureContext Context(Atmosphere.AtmosphereSnapshot atmosphere, Voxel.BlockPos feet, double coveredShare, Needs.FireRings? fires, World.IPlaceInfo? places) =>
        new(atmosphere, feet, coveredShare, fires, places, Environment, _columns);

    /// <summary>The terms, in registration order.</summary>
    public IReadOnlyList<ITemperatureTerm> Terms => _terms;

    /// <summary>The same terms and one more (registering a term: the new class and nothing else is touched).</summary>
    /// <param name="term">The term.</param>
    public PlaceTemperature With(ITemperatureTerm term)
    {
        ArgumentNullException.ThrowIfNull(term);
        return new PlaceTemperature([.. _terms, term], Environment);
    }

    /// <summary>The temperature here and now, in tenths of a degree.</summary>
    /// <param name="context">The place and the moment.</param>
    public int TenthsAt(ITemperatureContext context)
    {
        long sum = 0;
        foreach (var term in _terms)
        {
            sum += term.TenthsAt(context);
        }

        return (int)Math.Clamp(sum, -LimitTenths, LimitTenths);
    }

    /// <summary>The sum of a breakdown, kept inside <see cref="LimitTenths"/> as <see cref="TenthsAt"/> keeps it.</summary>
    /// <param name="breakdown">The terms' contributions.</param>
    public static int Sum(IReadOnlyList<TemperatureContribution> breakdown)
    {
        ArgumentNullException.ThrowIfNull(breakdown);
        long sum = 0;
        foreach (var contribution in breakdown)
        {
            sum += contribution.Tenths;
        }

        return (int)Math.Clamp(sum, -LimitTenths, LimitTenths);
    }

    /// <summary>What the term of that name adds here and now, in tenths (0 when no term has the name).</summary>
    /// <param name="name">The term's <see cref="ITemperatureTerm.Name"/>.</param>
    /// <param name="context">The place and the moment.</param>
    public int TenthsOf(string name, ITemperatureContext context)
    {
        foreach (var term in _terms)
        {
            if (string.Equals(term.Name, name, StringComparison.Ordinal))
            {
                return term.TenthsAt(context);
            }
        }

        return 0;
    }

    /// <summary>What each term adds here and now, in registration order (the debug overlay and the tests read it).</summary>
    /// <param name="context">The place and the moment.</param>
    public IReadOnlyList<TemperatureContribution> Breakdown(ITemperatureContext context)
    {
        var list = new List<TemperatureContribution>(_terms.Length);
        foreach (var term in _terms)
        {
            list.Add(new TemperatureContribution(term.Name, term.TenthsAt(context)));
        }

        return list;
    }
}
