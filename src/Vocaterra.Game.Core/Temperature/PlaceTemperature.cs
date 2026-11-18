namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The temperature where the player stands (I06): the sum of the terms it was built with, in tenths of a degree Celsius, as an integer. It owns no rule of its own and
/// names no term, so adding one is a new <see cref="ITemperatureTerm"/> and one more entry where the terms are registered (<see cref="TemperatureTerms"/>). The sum is
/// whole-number arithmetic, kept inside <see cref="LimitTenths"/> either way, so it is the same on every machine and cannot overflow.
/// </summary>
public sealed class PlaceTemperature
{
    /// <summary>The most the sum may be, either way, in tenths (1000 degrees): a limit against a bad row's overflow, far outside any real value.</summary>
    public const int LimitTenths = 10_000;

    private readonly ITemperatureTerm[] _terms;

    /// <summary>A temperature of these terms.</summary>
    /// <param name="terms">The terms, in the order a breakdown lists them (the order does not change the sum).</param>
    public PlaceTemperature(IEnumerable<ITemperatureTerm> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        _terms = [.. terms];
    }

    /// <summary>The terms, in registration order.</summary>
    public IReadOnlyList<ITemperatureTerm> Terms => _terms;

    /// <summary>The same terms and one more (registering a term: the new class and nothing else is touched).</summary>
    /// <param name="term">The term.</param>
    public PlaceTemperature With(ITemperatureTerm term)
    {
        ArgumentNullException.ThrowIfNull(term);
        return new PlaceTemperature([.. _terms, term]);
    }

    /// <summary>The temperature here and now, in tenths of a degree.</summary>
    /// <param name="situation">The place and the moment.</param>
    public int TenthsAt(in TemperatureSituation situation)
    {
        long sum = 0;
        foreach (var term in _terms)
        {
            sum += term.TenthsAt(situation);
        }

        return (int)Math.Clamp(sum, -LimitTenths, LimitTenths);
    }

    /// <summary>What the term of that name adds here and now, in tenths (0 when no term has the name).</summary>
    /// <param name="name">The term's <see cref="ITemperatureTerm.Name"/>.</param>
    /// <param name="situation">The place and the moment.</param>
    public int TenthsOf(string name, in TemperatureSituation situation)
    {
        foreach (var term in _terms)
        {
            if (string.Equals(term.Name, name, StringComparison.Ordinal))
            {
                return term.TenthsAt(situation);
            }
        }

        return 0;
    }

    /// <summary>What each term adds here and now, in registration order (the debug overlay and the tests read it).</summary>
    /// <param name="situation">The place and the moment.</param>
    public IReadOnlyList<TemperatureContribution> Breakdown(in TemperatureSituation situation)
    {
        var list = new List<TemperatureContribution>(_terms.Length);
        foreach (var term in _terms)
        {
            list.Add(new TemperatureContribution(term.Name, term.TenthsAt(situation)));
        }

        return list;
    }
}
