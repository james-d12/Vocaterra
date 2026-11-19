using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>
/// The map from the generator's climate temperature to degrees (I06c): straight lines between points, flat beyond the first and the last. The input is the generator's fixed point (4 096 is
/// the top of the field's range, written 1 000 in thousandths as <c>world.json</c> writes it); the output is tenths of a degree. A flat stretch is where the valley's values lie.
/// </summary>
public sealed class ClimateCurve
{
    private readonly int[] _climate;
    private readonly int[] _tenths;

    /// <summary>A curve.</summary>
    /// <param name="points">The points as (climate in thousandths, tenths of a degree), the climate rising.</param>
    public ClimateCurve(IReadOnlyList<(int Thousandths, int Tenths)> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            throw new ArgumentException("A curve needs at least two points.", nameof(points));
        }

        _climate = [.. points.Select(static p => FieldScale.FromThousandths(p.Thousandths))];
        _tenths = [.. points.Select(static p => p.Tenths)];
    }

    /// <summary>The temperature, in tenths of a degree, at a climate temperature.</summary>
    /// <param name="climate">The generator's climate temperature, fixed point.</param>
    public int TenthsAt(int climate)
    {
        if (climate <= _climate[0])
        {
            return _tenths[0];
        }

        var last = _climate.Length - 1;
        if (climate >= _climate[last])
        {
            return _tenths[last];
        }

        var i = 1;
        while (climate > _climate[i])
        {
            i++;
        }

        var span = _climate[i] - _climate[i - 1];
        return _tenths[i - 1] + (int)((long)(climate - _climate[i - 1]) * (_tenths[i] - _tenths[i - 1]) / span);
    }
}
