using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>The outline of a plot of land (Y10): posts on the four corners and an edge line on the ground's own height, a broken edge for the pending plot and a solid one with flags for the owned one.</summary>
public sealed class PlotOutlineTests
{
    private static readonly Plot Pending = new(20, 30, 10, 10);

    // A hill: the ground rises with x, so a post's height says which column it stands on.
    private static float Surface(int x, int z) => 10 + ((x - 20) * 0.5f) + (z % 2 == 0 ? 0f : 1f);

    private static IEnumerable<MeshVertex> Vertices(MeshBuffer mesh) => mesh.Vertices.ToArray();

    [Fact]
    public void Given_APendingPlot_When_TheOutlineIsMade_Then_FourPostsStandOnTheCornerColumnsAtTheGroundsHeight()
    {
        var mesh = OverlayMeshes.PlotOutline(Pending, Surface, owned: false);

        // A post is a box of six faces; find the vertices that reach the post's top.
        foreach (var (cx, cz) in new[] { (20, 30), (29, 30), (20, 39), (29, 39) })
        {
            var ground = Surface(cx, cz);
            var top = ground + OverlayMeshes.PostHeight;
            var inColumn = Vertices(mesh).Where(v => v.X >= cx && v.X <= cx + 1 && v.Z >= cz && v.Z <= cz + 1 && Math.Abs(v.Y - top) < 0.001f).ToList();
            Assert.Equal(12, inColumn.Count); // the post's top face (4) and the upper edge of its four sides (8)
            Assert.Contains(Vertices(mesh), v => v.X >= cx && v.X <= cx + 1 && v.Z >= cz && v.Z <= cz + 1 && Math.Abs(v.Y - ground) < 0.001f); // its foot
        }
    }

    [Fact]
    public void Given_APlot_When_TheOutlineIsMade_Then_EverythingStaysInsideTheColumnsOfThePlotAndFollowsTheGround()
    {
        var mesh = OverlayMeshes.PlotOutline(Pending, Surface, owned: false);

        Assert.All(Vertices(mesh), v =>
        {
            Assert.InRange(v.X, 20f, 30f);
            Assert.InRange(v.Z, 30f, 40f);
            // A vertex on the border of two columns may belong to either: it is never below the lower of the ground of the columns it touches.
            var touched = from cx in new[] { (int)Math.Floor(v.X - 0.001f), (int)Math.Floor(v.X + 0.001f) }
                          from cz in new[] { (int)Math.Floor(v.Z - 0.001f), (int)Math.Floor(v.Z + 0.001f) }
                          where cx is >= 20 and <= 29 && cz is >= 30 and <= 39
                          select Surface(cx, cz);
            Assert.True(v.Y >= touched.Min() - 0.001f);
        });
    }

    [Fact]
    public void Given_PendingAndOwned_When_TheOutlinesAreMade_Then_TheOwnedOneIsSolidWithFlagsAndThePendingOneIsBroken()
    {
        var pending = OverlayMeshes.PlotOutline(Pending, Surface, owned: false);
        var owned = OverlayMeshes.PlotOutline(Pending, Surface, owned: true);

        // The posts are boxes of six faces; an edge strip is one top face. Owned: every column of the four sides (10 + 10 + 8 + 8 = 36) and a flag box on each post.
        // Pending: every second column of a side (5 + 5 + 4 + 4 = 18) and bare posts.
        Assert.Equal((4 * 6) + 18, pending.FaceCount);
        Assert.Equal((4 * 6) + 36 + (4 * 6), owned.FaceCount);
        Assert.True(owned.FaceCount > pending.FaceCount);
    }

    [Fact]
    public void Given_ADistance_When_TheOutlineIsAskedFor_Then_ItIsDrawnOnlyNearThePlot()
    {
        Assert.True(OverlayMeshes.PlotOutlineVisible(Pending, 25f, 35f)); // inside
        Assert.True(OverlayMeshes.PlotOutlineVisible(Pending, 25f, 35f + OverlayMeshes.PlotOutlineReach - 20f));
        Assert.False(OverlayMeshes.PlotOutlineVisible(Pending, 25f, 40f + OverlayMeshes.PlotOutlineReach + 1f));
        Assert.False(OverlayMeshes.PlotOutlineVisible(Pending, 285f, 206f));
    }

    [Fact]
    public void TheOutlineOfAPlotStaysUnderTheDynamicMeshLimit()
    {
        var mesh = OverlayMeshes.PlotOutline(new Plot(0, 0, 40, 40), static (_, _) => 5f, owned: true);

        Assert.True(mesh.VertexCount <= 4096, $"{mesh.VertexCount} vertices");
    }
}
