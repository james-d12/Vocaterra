using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Noise;

/// <summary>One control point of a <see cref="Spline"/>: the input and the output, in whatever fixed-point units the data uses.</summary>
/// <param name="X">The input at the point.</param>
/// <param name="Y">The output at the point.</param>
public readonly record struct SplinePoint(int X, int Y);

/// <summary>
/// A curve through control points that come from data (B01): the height model's base, range factor and peaks splines. Between two points the output follows a
/// Catmull-Rom curve clamped to the two outputs, so it is smooth, passes through every point and never overshoots; before the first point and after the last it holds the end value.
/// Integer only and immutable.
/// </summary>
internal sealed class Spline
{
    private readonly int[] _x;
    private readonly int[] _y;

    /// <summary>Creates the spline of control points sorted by strictly rising input.</summary>
    /// <param name="points">At least one point, inputs strictly ascending.</param>
    /// <exception cref="ArgumentException">There is no point, or the inputs do not rise.</exception>
    public Spline(IReadOnlyList<SplinePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            throw new ArgumentException("A spline needs a control point.", nameof(points));
        }

        _x = new int[points.Count];
        _y = new int[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0 && points[i].X <= points[i - 1].X)
            {
                throw new ArgumentException("The control points' inputs must rise.", nameof(points));
            }

            _x[i] = points[i].X;
            _y[i] = points[i].Y;
        }
    }

    /// <summary>The output at an input.</summary>
    /// <param name="x">The input.</param>
    public int Evaluate(int x)
    {
        var last = _x.Length - 1;
        if (x <= _x[0])
        {
            return _y[0];
        }

        if (x >= _x[last])
        {
            return _y[last];
        }

        var i = 0;
        while (x > _x[i + 1])
        {
            i++;
        }

        var t = (int)((long)(x - _x[i]) * IntMath.One / (_x[i + 1] - _x[i]));
        return IntMath.CatmullRom(_y[Math.Max(i - 1, 0)], _y[i], _y[i + 1], _y[Math.Min(i + 2, last)], t);
    }
}
