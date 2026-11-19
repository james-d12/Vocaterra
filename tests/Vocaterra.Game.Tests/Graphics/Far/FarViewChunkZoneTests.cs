using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>
/// The owner's video of 2026-11-19: for two frames, flat grey and green boxes stood over a near mountain's ridge and the trees under it. The far pass is drawn before the chunks and its depth is cleared for them, so any far ground under the
/// chunks that stands higher than the blocks shows against the sky above them. A column of the zone that the chunks drew stopped being drawn whole for a frame (a chunk the view newly wanted in it); its leaf got a level-4 stand-in, and the
/// stand-in kept its ground under every other drawn column of the zone too (its "backdrop"). Now the far view draws no ground where the chunks show it, and a column that is only waiting a moment for a new chunk is given no stand-in.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FarViewChunkZoneTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    private static readonly FarViewSettings Wide = new(2048, MaxTiles: 600, TreeLevels: 1, RealRadius: 192);
    private const int Vox = 192;
    private const double Eye = 200;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_TheZoneDrawnAndItsStandInsOnTheCard_When_AColumnUnderTheEyeIsNotDrawnForAMoment_Then_NoFarGroundIsDrawnOverTheChunksUntilTheGraceIsOut(bool gl)
    {
        using var pool = new JobPool(0);
        var view = Make(pool, Wide);
        _ = view.UsePath(gl, static _ => { });
        var zoneDrawn = false;
        (int X, int Z)? waiting = null;
        bool Ready(int cx, int cz)
        {
            var (dx, dz) = ((cx * 16) + 8 - Eye, (cz * 16) + 8 - Eye);
            return zoneDrawn && (dx * dx) + (dz * dz) <= (double)Vox * Vox && waiting != (cx, cz);
        }

        view.SetNearReady(Ready);

        // Nothing of the zone drawn yet: the leaves inside it wait for their columns, and their level-4 stand-ins are made and go on the card. One update only: a second one after the grace (the first can take longer) would make the leaves'
        // own tiles, and the leaf under the eye would then be drawn from its own tile, not stood in for.
        Frame(view);
        TakeToTheCard(view);
        Assert.Contains(view.Cache.Values, static t => t.OnCard && t.Key.Level == 4 && Contains(t.Key, Eye, Eye));
        var leaf = FarTileSelector.Select(Wide, Eye, Eye, Vox, includeInner: true).Single(l => Contains(l, Eye, Eye));
        Assert.False(view.Cache.TryGetValue(leaf, out var own) && own.IsBuilt, $"the leaf under the eye {leaf} has no tile of its own");

        zoneDrawn = true;
        Frame(view);
        AssertNothingOverTheChunks(view, Ready, "the zone drawn");

        // The column under the eye waits for a chunk the view newly wants: its other chunks are still drawn, so no far ground goes over it, nor over the rest of the zone.
        var column = ((int)Eye >> 4, (int)Eye >> 4);
        waiting = column;
        Frame(view);
        AssertNothingOverTheChunks(view, Ready, "a column waiting a moment");
        Assert.DoesNotContain(Rectangles(view), r => Overlaps(r, leaf));
        Assert.Equal(0, view.Uncovered);

        waiting = null;
        Frame(view);
        AssertNothingOverTheChunks(view, Ready, "the column drawn again");

        // A column that stays undrawn past the grace (a jump, a slow machine) is filled by the far land as before.
        waiting = column;
        Frame(view);
        Thread.Sleep(800);
        Frame(view);
        Frame(view);
        AssertNothingOverTheChunks(view, Ready, "a column undrawn past the grace");
        Assert.Contains(Rectangles(view), r => Overlaps(r, leaf));
        Output.WriteLine($"leaf under the eye {leaf}; draw list {view.Draw.Count} tiles, {view.FallbackCount} stand-ins");
    }

    // A frame of the renderer: what the workers made goes on the card, then the view is updated (every missing tile built now).
    private static void Frame(FarView view)
    {
        TakeToTheCard(view);
        view.Update(Eye, Eye, Vox, static _ => { }, null);
    }

    private static void TakeToTheCard(FarView view)
    {
        foreach (var tile in view.PendingTiles.Where(static t => t.Pending is not null).ToList()) // the list is made by Update: a tile taken since is skipped
        {
            tile.Adopt(tile.Pending!);
            tile.Pending = null;
            tile.OnCard = true;
        }
    }

    // The ground each tile of the draw list draws: a stand-in's windows, or a tile's footprint.
    private static List<(int MinX, int MinZ, int MaxX, int MaxZ)> Rectangles(FarView view)
    {
        var rects = new List<(int, int, int, int)>();
        for (var i = 0; i < view.Draw.Count; i++)
        {
            var tile = view.Draw[i];
            if (i < view.FallbackCount && tile.Windows is { } windows)
            {
                rects.AddRange(windows.Select(static w => (w.MinX, w.MinZ, w.MaxX, w.MaxZ)));
            }
            else
            {
                var (x0, z0, size) = (tile.Key.MinX(Wide), tile.Key.MinZ(Wide), Wide.SizeOf(tile.Key.Level));
                rects.Add((x0, z0, x0 + size, z0 + size));
            }
        }

        return rects;
    }

    // No tile of the draw list draws ground over a leaf whose columns the chunks draw (it lies wholly inside the zone, and every column of it is drawn).
    private static void AssertNothingOverTheChunks(FarView view, Func<int, int, bool> ready, string when)
    {
        var rects = Rectangles(view);
        foreach (var leaf in FarTileSelector.Select(Wide, Eye, Eye, Vox, includeInner: true))
        {
            var (x0, z0, size) = (leaf.MinX(Wide), leaf.MinZ(Wide), Wide.SizeOf(leaf.Level));
            if (!ChunksShow(x0, z0, x0 + size, z0 + size, ready))
            {
                continue;
            }

            foreach (var r in rects)
            {
                Assert.False(Overlaps(r, leaf), $"{when}: far ground {r} is drawn over {leaf}, whose columns the chunks draw");
            }
        }

        Assert.True(rects.Count > 0, $"{when}: the far view draws something");
    }

    private static bool Contains(FarTileKey leaf, double x, double z) =>
        x >= leaf.MinX(Wide) && x < leaf.MinX(Wide) + Wide.SizeOf(leaf.Level) && z >= leaf.MinZ(Wide) && z < leaf.MinZ(Wide) + Wide.SizeOf(leaf.Level);

    private static bool Overlaps((int MinX, int MinZ, int MaxX, int MaxZ) r, FarTileKey leaf)
    {
        var (x0, z0, size) = (leaf.MinX(Wide), leaf.MinZ(Wide), Wide.SizeOf(leaf.Level));
        return r.MinX < x0 + size && x0 < r.MaxX && r.MinZ < z0 + size && z0 < r.MaxZ;
    }

    // Whether the chunks show a rectangle of ground, as the far view decides it for a leaf (IsNeeded): its farthest corner is inside the inner radius and every chunk column of it is drawn.
    private static bool ChunksShow(int x0, int z0, int x1, int z1, Func<int, int, bool> ready)
    {
        var (dx, dz) = (Math.Max(Math.Abs(Eye - x0), Math.Abs(Eye - x1)), Math.Max(Math.Abs(Eye - z0), Math.Abs(Eye - z1)));
        if (Math.Sqrt((dx * dx) + (dz * dz)) >= Wide.InnerFraction * Vox)
        {
            return false;
        }

        for (var cz = z0 >> 4; cz <= (z1 - 1) >> 4; cz++)
        {
            for (var cx = x0 >> 4; cx <= (x1 - 1) >> 4; cx++)
            {
                if (!ready(cx, cz))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
