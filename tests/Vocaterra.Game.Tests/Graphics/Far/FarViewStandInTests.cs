using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>B35: on a fresh world the fine leaves of the inner ring get their level-4 ancestor asked for at once and ahead of the fine tiles, so the ring is covered within seconds.</summary>
public sealed class FarViewStandInTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    private static readonly FarViewSettings Wide = new(2048, MaxTiles: 400, TreeLevels: 1);

    private static bool Covered(FarView view, FarTileKey leaf, int top)
    {
        var key = leaf;
        for (var step = 0; step <= 4; step++)
        {
            if (view.Requested.Contains(key) || (view.Cache.TryGetValue(key, out var tile) && tile.IsBuilt))
            {
                return true;
            }

            if (key.Level >= top)
            {
                break;
            }

            key = key.GetParent();
        }

        return false;
    }

    [Fact]
    public void Given_AFreshView_When_TheFirstFrameRuns_Then_EveryLeafIsCoveredByItsOwnTileOrARequestedAncestor()
    {
        using var pool = new JobPool(0);
        var view = Make(pool, Wide);

        view.Update(192, 192, Voxel, Release, 0);

        var leaves = FarTileSelector.Select(Wide, 192, 192, Voxel);
        Assert.Contains(leaves, static l => l.Level < 4);
        foreach (var leaf in leaves)
        {
            Assert.True(Covered(view, leaf, Wide.TopLevel), $"{leaf} has neither a tile nor a requested ancestor");
        }
    }

    [Fact]
    public void Given_AFreshView_When_TilesAreBuilt_Then_TheStandInsComeBeforeTheFirstFineTileAndNothingIsBuiltTwice()
    {
        using var pool = new JobPool(0);
        var view = Make(pool, Wide);
        var order = new List<FarTileKey>();
        var seen = new HashSet<FarTileKey>();
        for (var frame = 0; frame < 2000; frame++)
        {
            view.Update(192, 192, Voxel, Release, 0);
            foreach (var key in view.Cache.Keys)
            {
                if (seen.Add(key))
                {
                    order.Add(key);
                }
            }

            if (view.Missing == 0 && view.Requested.Count == 0)
            {
                break;
            }
        }

        var leaves = FarTileSelector.Select(Wide, 192, 192, Voxel);
        Assert.Equal(0, view.Missing);
        var firstFine = order.FindIndex(static k => k.Level < 4);
        Assert.True(firstFine > 0);
        var standIns = order.Where(k => k.Level == 4 && !leaves.Contains(k)).ToList();
        Assert.NotEmpty(standIns);
        Assert.All(standIns, k => Assert.True(order.IndexOf(k) < firstFine, $"{k} came after a fine tile"));
        Assert.Equal(view.Cache.Count, view.Stats.Built); // every tile built once: no key was asked for twice
    }
}
