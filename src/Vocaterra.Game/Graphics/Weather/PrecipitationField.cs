using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Simulation;

namespace Vocaterra.Game.Graphics.Weather;

/// <summary>
/// The drops of the rain (E06), as streaks, with no engine type. They live in a box that wraps round the camera, so the field never ends and costs the same anywhere: each drop has a fixed
/// place in a pattern that repeats every <see cref="BoxWidth"/> blocks east-west and north-south and every <see cref="BoxHeight"/> up (the box reaches further below the eye than above it, <see cref="BoxLowering"/>), from <see cref="PresentationRng"/> (no clock, no
/// <see cref="System.Random"/>), and falls at the style's speed along a line the wind slants. The same tick and camera give the same drops, and a camera moved by the box's size sees the
/// same pattern. A drop is not drawn below the cover of its column (<see cref="ColumnCover"/>): the rain stops at the eaves, and a drop that reaches a roof or the ground is shortened into it.
/// The number alive is the style's share of the quality's budget times the intensity, one drop after another (a drop at the edge fades in), so a change of intensity thickens and
/// thins the rain with no pop.
/// </summary>
public sealed class PrecipitationField
{
    /// <summary>The width of the box, east-west and north-south, in blocks. The pattern repeats at this size.</summary>
    public const float BoxWidth = 36f;

    /// <summary>The height of the box, in blocks.</summary>
    public const float BoxHeight = 24f;

    /// <summary>How far the middle of the box sits below the eye, in blocks: most of the rain is seen below the horizon (on the ground in front), so the box reaches further down than up.</summary>
    public const float BoxLowering = 3f;

    /// <summary>The most drops the field can give (the pattern is made for this many).</summary>
    public const int MaxDrops = 6000;

    /// <summary>The seed of the pattern: the rain looks the same in every run.</summary>
    public const ulong Seed = 0x5241_494E_4649_454CUL;

    /// <summary>The horizontal direction the wind blows towards (a unit vector, east and a little south, the way the clouds drift).</summary>
    public static readonly (float X, float Z) WindHeading = (0.94f, 0.342f);

    // How near the box's wall the drops fade out (a share of the half width / half height), and how near the eye they fade (blocks): a drop pops neither at the wrap nor in the face (a streak a block from the lens would be a bar across the picture).
    private const float EdgeFade = 0.3f;
    private const float RoofFade = 0.2f;
    private const float NearFadeFrom = 1.0f;
    private const float NearFadeTo = 4.5f;

    private readonly ColumnCover _cover;
    private readonly Action<string>? _log;
    private readonly float[] _x = new float[MaxDrops];
    private readonly float[] _z = new float[MaxDrops];
    private readonly float[] _y = new float[MaxDrops];
    private readonly float[] _speed = new float[MaxDrops];
    private readonly float[] _length = new float[MaxDrops];
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

    /// <summary>Creates the field over the cover of a world.</summary>
    /// <param name="cover">The cover heights of the columns.</param>
    /// <param name="log">Where an unknown precipitation kind is reported, once, or <see langword="null"/>.</param>
    public PrecipitationField(ColumnCover cover, Action<string>? log = null)
    {
        _cover = cover ?? throw new ArgumentNullException(nameof(cover));
        _log = log;
        var rng = new PresentationRng(Seed);
        for (var i = 0; i < MaxDrops; i++)
        {
            _x[i] = (float)(rng.NextDouble() * BoxWidth);
            _z[i] = (float)(rng.NextDouble() * BoxWidth);
            _y[i] = (float)(rng.NextDouble() * BoxHeight);
            _speed[i] = (float)rng.NextBetween(0.85, 1.15);
            _length[i] = (float)rng.NextBetween(0.75, 1.25);
        }
    }

    /// <summary>How many drops are alive (fully or fading in) at an intensity, for a budget and a style's share: the budget times the share times the intensity, rounded up, and none at 0.</summary>
    /// <param name="budget">The quality's drop budget.</param>
    /// <param name="share">The style's share of the budget at full intensity, 0 to 1.</param>
    /// <param name="intensity">How hard it falls, 0 to 1.</param>
    public static int AliveCount(int budget, float share, double intensity)
    {
        var full = Math.Clamp(budget, 0, MaxDrops) * Math.Clamp(share, 0f, 1f);
        var i = Math.Clamp(intensity, 0, 1);
        return i <= 0 ? 0 : (int)Math.Ceiling(full * i);
    }

    /// <summary>
    /// Fills <paramref name="streaks"/> with the drops to draw for a camera, a tick and what falls, and gives the style they use. A drop under cover, or one whose whole streak is under the
    /// cover of its column, is left out; a drop entering the cover is cut short. Nothing is allocated. An unknown kind gives nothing and is reported once.
    /// </summary>
    /// <param name="streaks">The room for the streaks (at most the alive count is used).</param>
    /// <param name="cameraX">The eye's east-west position, in blocks.</param>
    /// <param name="cameraY">The eye's height.</param>
    /// <param name="cameraZ">The eye's north-south position.</param>
    /// <param name="tick">The simulation's tick (the drops fall with it, 60 ticks a second).</param>
    /// <param name="state">What falls: the kind, the intensity and the wind.</param>
    /// <param name="budget">The quality's drop budget (<c>QualityPreset.RainDrops</c>).</param>
    /// <param name="style">The style used, or <see langword="null"/> when nothing is drawn.</param>
    /// <returns>How many streaks were written.</returns>
    public int Fill(Span<Streak> streaks, double cameraX, double cameraY, double cameraZ, long tick, in PrecipitationState state, int budget, out PrecipitationStyle? style)
    {
        style = null;
        if (string.IsNullOrEmpty(state.Kind) || state.Intensity <= 0)
        {
            return 0;
        }

        if (!PrecipitationStyles.TryFor(state.Kind, out var found))
        {
            if (_reported.Add(state.Kind))
            {
                _log?.Invoke($"weather: no precipitation style for the kind '{state.Kind}', nothing is drawn for it");
            }

            return 0;
        }

        style = found;
        var cap = Math.Min(AliveCount(budget, found.CountShare, state.Intensity), streaks.Length);
        if (cap == 0)
        {
            return 0;
        }

        _cover.Refresh(cameraX, cameraZ, (BoxWidth / 2) + 2);
        var full = Math.Clamp(budget, 0, MaxDrops) * Math.Clamp(found.CountShare, 0f, 1f) * Math.Clamp(state.Intensity, 0, 1); // fractional: the last alive drop fades in by its fraction
        var slant = found.WindSlant * (float)Math.Clamp(state.Wind, 0, 1);
        var dx = -WindHeading.X * slant; // the tail is up the line the drop fell along: against the wind
        var dz = -WindHeading.Z * slant;
        var norm = 1f / MathF.Sqrt((dx * dx) + 1f + (dz * dz));
        var (ux, uy, uz) = (dx * norm, norm, dz * norm); // the unit vector from a head up its streak
        var seconds = tick / (double)VoxelSimulation.TicksPerSecond;
        var half = BoxWidth / 2;
        var baseX = cameraX - half;
        var baseZ = cameraZ - half;
        var baseY = cameraY - BoxLowering - (BoxHeight / 2);
        var count = 0;
        for (var i = 0; i < cap; i++)
        {
            var y = baseY + Mod(_y[i] - (found.FallSpeed * _speed[i] * seconds) - baseY, BoxHeight);
            var lateral = slant * (y - baseY); // blocks of drift with the height: a line slanted by the wind
            var x = baseX + Mod(_x[i] - (WindHeading.X * lateral) - baseX, BoxWidth);
            var z = baseZ + Mod(_z[i] - (WindHeading.Z * lateral) - baseZ, BoxWidth);
            var length = found.Length * _length[i];
            var top = _cover.TopAt((int)Math.Floor(x), (int)Math.Floor(z));
            var headY = y;
            var run = 0f; // how far the head is moved up its line to the cover's top
            if (y < top)
            {
                run = (float)((top - y) / uy);
                if (run >= length - 0.05f)
                {
                    continue; // the whole streak is under the cover
                }

                headY = top;
            }

            var alpha = found.Opacity * Fade(x - cameraX, y - cameraY, z - cameraZ, half) * (float)Math.Min(1.0, full - i);
            if (alpha <= 0.002f)
            {
                continue;
            }

            var hx = (float)x + (ux * run);
            var hz = (float)z + (uz * run);
            var tailLength = length - run;
            streaks[count++] = new Streak(hx, (float)headY, hz, hx + (ux * tailLength), (float)headY + (uy * tailLength), hz + (uz * tailLength), alpha);
        }

        return count;
    }

    // A drop fades out towards the walls and the roof and floor of the box (where it wraps) and in front of the eye.
    private static float Fade(double dx, double dy, double dz, float half)
    {
        var side = 1 - (Math.Max(Math.Abs(dx), Math.Abs(dz)) / half);
        var vertical = 1 - (Math.Abs(dy + BoxLowering) / (BoxHeight / 2));
        var near = (Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)) - NearFadeFrom) / (NearFadeTo - NearFadeFrom);
        return (float)(Smooth(side / EdgeFade) * Smooth(vertical / RoofFade) * Smooth(near));
    }

    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static double Mod(double value, double period)
    {
        var r = value % period;
        return r < 0 ? r + period : r;
    }
}
