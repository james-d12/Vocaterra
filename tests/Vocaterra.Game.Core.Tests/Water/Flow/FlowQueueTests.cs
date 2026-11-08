using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;

namespace Vocaterra.Game.Core.Tests.Water.Flow;

/// <summary>The queue serves cells in one fixed order: due tick, then y, then z, then x.</summary>
public sealed class FlowQueueTests
{
    private static List<BlockPos> Drain(FlowQueue queue, long tick)
    {
        var served = new List<BlockPos>();
        while (queue.TryDequeue(tick, out var entry))
        {
            served.Add(entry.Position);
        }

        return served;
    }

    [Fact]
    public void Given_CellsScheduledInAnyOrder_When_Served_Then_TheOrderIsDueThenYThenZThenX()
    {
        var queue = new FlowQueue();
        queue.Schedule(new BlockPos(5, 3, 2), 30);
        queue.Schedule(new BlockPos(9, 2, 7), 30);
        queue.Schedule(new BlockPos(1, 3, 2), 30);
        queue.Schedule(new BlockPos(5, 3, 1), 30);
        queue.Schedule(new BlockPos(8, 8, 8), 15);
        queue.Schedule(new BlockPos(0, 9, 0), 45);

        var served = Drain(queue, 45);

        Assert.Equal(
            [new BlockPos(8, 8, 8), new BlockPos(9, 2, 7), new BlockPos(5, 3, 1), new BlockPos(1, 3, 2), new BlockPos(5, 3, 2), new BlockPos(0, 9, 0)],
            served);
    }

    [Fact]
    public void Given_TheSameCellsInAnotherOrder_When_Served_Then_TheOrderIsTheSame()
    {
        var cells = Enumerable.Range(0, 60).Select(i => new BlockPos((i * 7) % 11, (i * 5) % 4, (i * 3) % 13)).Distinct().ToList();
        var forward = new FlowQueue();
        var backward = new FlowQueue();
        foreach (var cell in cells)
        {
            forward.Schedule(cell, 15);
        }

        foreach (var cell in Enumerable.Reverse(cells))
        {
            backward.Schedule(cell, 15);
        }

        Assert.Equal(Drain(forward, 15), Drain(backward, 15));
    }

    [Fact]
    public void Given_ACellNotDueYet_When_Served_Then_ItWaits()
    {
        var queue = new FlowQueue();
        queue.Schedule(new BlockPos(1, 1, 1), 30);

        Assert.Empty(Drain(queue, 29));
        Assert.Equal(1, queue.Count);
        Assert.Single(Drain(queue, 30));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Given_ACellScheduledTwice_When_Served_Then_ItIsServedOnceAtTheEarlierTick()
    {
        var queue = new FlowQueue();
        var cell = new BlockPos(4, 4, 4);

        Assert.True(queue.Schedule(cell, 30));
        Assert.False(queue.Schedule(cell, 45)); // later: ignored
        Assert.True(queue.Schedule(cell, 15)); // earlier: moves it up

        Assert.Equal(1, queue.Count);
        Assert.Single(Drain(queue, 15));
        Assert.Empty(Drain(queue, 100));
    }

    [Fact]
    public void Given_AQueue_When_SnapshotAndRestored_Then_ItServesTheSameCellsInTheSameOrder()
    {
        var queue = new FlowQueue();
        queue.Schedule(new BlockPos(3, 1, 1), 45);
        queue.Schedule(new BlockPos(1, 1, 1), 30);
        queue.Schedule(new BlockPos(2, 0, 9), 30);
        var copy = new FlowQueue();
        copy.Schedule(new BlockPos(99, 99, 99), 1); // dropped by the restore

        copy.Restore(queue.Snapshot());

        Assert.Equal(queue.Snapshot(), copy.Snapshot());
        Assert.Equal(Drain(queue, 100), Drain(copy, 100));
    }
}
