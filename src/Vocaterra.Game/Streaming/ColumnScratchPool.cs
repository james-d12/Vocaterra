using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// A bounded stack of <see cref="ColumnScratch"/> that a caller owns and its worker threads share (B40): a thread rents one for a column's generation and gives it back, so the window arrays (about 170 KB for each column made) are
/// reused instead of allocated and collected. When none is free a new one is made; one given back beyond the bound is dropped. Nothing here is static: a pool belongs to the residency or the ring that made it.
/// </summary>
public sealed class ColumnScratchPool
{
    private readonly Stack<ColumnScratch> _free = [];
    private readonly Func<ColumnScratch> _make;
    private readonly int _max;

    /// <summary>Creates a pool.</summary>
    /// <param name="make">Makes a scratch (<see cref="ProceduralChunkSource.CreateScratch"/>).</param>
    /// <param name="max">How many idle scratches to keep (the worker threads' number is enough).</param>
    public ColumnScratchPool(Func<ColumnScratch> make, int max = 16)
    {
        _make = make;
        _max = Math.Max(1, max);
    }

    /// <summary>How many idle scratches the pool holds.</summary>
    public int Idle
    {
        get
        {
            lock (_free)
            {
                return _free.Count;
            }
        }
    }

    /// <summary>Takes a scratch.</summary>
    public ColumnScratch Rent()
    {
        lock (_free)
        {
            if (_free.Count > 0)
            {
                return _free.Pop();
            }
        }

        return _make();
    }

    /// <summary>Gives a scratch back.</summary>
    /// <param name="scratch">The scratch.</param>
    public void Return(ColumnScratch scratch)
    {
        lock (_free)
        {
            if (_free.Count < _max)
            {
                _free.Push(scratch);
            }
        }
    }
}
