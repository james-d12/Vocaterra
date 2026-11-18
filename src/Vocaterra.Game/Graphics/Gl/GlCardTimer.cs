using Silk.NET.OpenGL;
using Vocaterra.Game.Core.Reporting;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>
/// The card's time for the world's passes (B17b): a GL timer query (<c>GL_TIME_ELAPSED</c>) round the stretch of a frame that draws the chunks, with its result read a few frames later (the card runs behind the
/// processor), so nothing waits for it. The stretch may be cut in two in a frame (the effect path draws the figures in the middle): the pieces of one frame are added up. It changes no state MonoGame tracks (a query is not
/// part of it), so it brackets the effects' draws as well as the game's own scope: the median of both paths is comparable.
/// </summary>
public sealed class GlCardTimer : IDisposable
{
    private const int RingSize = 24;

    private readonly GL _gl;
    private readonly uint[] _queries = new uint[RingSize];
    private readonly long[] _frameOf = new long[RingSize];
    private int _head;
    private int _tail;
    private int _inFlight;
    private bool _open;
    private long _frame;
    private long _accumulatedFrame = -1;
    private double _accumulated;

    private GlCardTimer(GL gl)
    {
        _gl = gl;
        for (var i = 0; i < _queries.Length; i++)
        {
            _queries[i] = gl.GenQuery();
        }
    }

    /// <summary>The card's time for each frame whose results have come back, in milliseconds.</summary>
    public BusyFrameStats Times { get; } = new();

    /// <summary>
    /// Makes the timer on MonoGame's context, or <see langword="null"/> when the context has no timer queries (below OpenGL 3.3 and no ARB_timer_query) or the functions did not load.
    /// </summary>
    /// <param name="log">Where a reason is said.</param>
    public static GlCardTimer? TryCreate(Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (!GlLoader.TryLoad(out var gl, out var error) || gl is null)
        {
            log($"gl: no card timer: {error}");
            return null;
        }

        var info = GlInfo.Query(gl);
        if (!info.TimerQuery)
        {
            log("gl: no card timer: the context has no timer queries");
            return null;
        }

        return new GlCardTimer(gl);
    }

    /// <summary>Starts a new frame: collects the results that are ready from earlier ones.</summary>
    public void NextFrame()
    {
        _frame++;
        Collect();
    }

    /// <summary>Starts timing the card's work from here (a piece of the frame; <see cref="End"/> closes it).</summary>
    public void Begin()
    {
        if (_open || _inFlight >= RingSize)
        {
            return;
        }

        _gl.BeginQuery(QueryTarget.TimeElapsed, _queries[_head]);
        _frameOf[_head] = _frame;
        _open = true;
    }

    /// <summary>Stops timing.</summary>
    public void End()
    {
        if (!_open)
        {
            return;
        }

        _gl.EndQuery(QueryTarget.TimeElapsed);
        _head = (_head + 1) % RingSize;
        _inFlight++;
        _open = false;
    }

    // Reads, oldest first, every query whose result is there; the pieces of a frame are added up and recorded when a later frame's piece arrives.
    private void Collect()
    {
        while (_inFlight > 0)
        {
            var query = _queries[_tail];
            _gl.GetQueryObject(query, QueryObjectParameterName.ResultAvailable, out int available);
            if (available == 0)
            {
                break;
            }

            _gl.GetQueryObject(query, QueryObjectParameterName.Result, out ulong nanoseconds);
            var frame = _frameOf[_tail];
            if (frame != _accumulatedFrame)
            {
                if (_accumulatedFrame >= 0)
                {
                    Times.Record(_accumulated);
                }

                _accumulatedFrame = frame;
                _accumulated = 0;
            }

            _accumulated += nanoseconds / 1_000_000.0;
            _tail = (_tail + 1) % RingSize;
            _inFlight--;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_open)
        {
            _gl.EndQuery(QueryTarget.TimeElapsed);
            _open = false;
        }

        _gl.DeleteQueries(_queries);
    }
}
