using Vocaterra.Game.Graphics.Sky;

namespace Vocaterra.Game.Tests.Graphics.Sky;

/// <summary>The clouds' cover (E05): at the fair cover the field is the one the game always had, a higher cover only adds clouds, and a cloud near the threshold fades by alpha.</summary>
public sealed class CloudFieldCoverTests
{
    private static List<CloudBox> Field(double cover, float x = 200f, float z = 200f, long tick = 0)
    {
        var boxes = new List<CloudBox>();
        CloudField.Generate(x, z, tick, boxes, cover);
        return boxes;
    }

    [Fact]
    public void Given_TheFairCover_When_TheFieldIsGenerated_Then_ItIsTodaysFieldWithEveryBoxFullyShown()
    {
        var today = new List<CloudBox>();
        CloudField.Generate(200f, 200f, 0, today);

        Assert.Equal(today, Field(0.42));
        Assert.Equal(today, Field(CloudField.Cover / 100.0));
        Assert.All(today, box => Assert.Equal(1f, box.Alpha));
        Assert.NotEmpty(today);
        Assert.Equal(Field(0.42, 531f, -77f, 9000), Field(0.42, 531f, -77f, 9000));
    }

    [Fact]
    public void Given_AHigherCover_When_TheFieldIsGenerated_Then_EveryCloudOfALowerCoverIsStillThereUnmovedAndAtLeastAsShown()
    {
        var previous = Field(0.42);
        for (var cover = 0.45; cover <= 1.0001; cover += 0.03)
        {
            var field = Field(cover);
            foreach (var box in previous)
            {
                var same = field.FirstOrDefault(b => b.X == box.X && b.Y == box.Y && b.Z == box.Z && b.Width == box.Width && b.Depth == box.Depth && b.Thickness == box.Thickness);
                Assert.True(same != default, $"the cloud at {box} is gone at cover {cover}");
                Assert.True(same.Alpha >= box.Alpha, $"the cloud at {box} fades out as the cover rises to {cover}");
            }

            Assert.True(field.Count >= previous.Count);
            previous = field;
        }
    }

    [Fact]
    public void Given_AFullCover_When_TheFieldIsGenerated_Then_EveryCellHasItsCloudShown()
    {
        var full = Field(1.0);

        Assert.All(full, box => Assert.Equal(1f, box.Alpha));
        var cells = (2 * CloudField.Reach + 1) * (2 * CloudField.Reach + 1);
        Assert.InRange(full.Count, cells * 2, cells * 3);
    }

    [Fact]
    public void Given_ARisingCover_When_ACellsAlphaIsRead_Then_ItNeverFallsAndRunsContinuouslyFromNothingToOne()
    {
        foreach (var (rank, fair) in new[] { (3.2f, true), (41.9f, true), (42.0f, false), (60.5f, false), (99.9f, false) })
        {
            var last = 0f;
            var steps = 0;
            for (var cover = 0.0; cover <= 1.0; cover += 0.001)
            {
                var alpha = CloudField.CellAlpha(rank, fair, cover);
                Assert.InRange(alpha, last, 1f);
                Assert.True(alpha - last < 0.05f, $"a step of {alpha - last} at cover {cover} for rank {rank}");
                last = alpha;
                steps += alpha is > 0f and < 1f ? 1 : 0;
            }

            Assert.Equal(1f, CloudField.CellAlpha(rank, fair, 1.0));
            Assert.True(steps > 20, $"rank {rank} fades over {steps} thousandths of cover");
        }
    }

    [Fact]
    public void Given_ACoverBelowFair_When_TheFieldIsGenerated_Then_ItHoldsFewerCloudsAndNoneAtZero()
    {
        Assert.Empty(Field(0.0));
        Assert.True(Field(0.2).Count < Field(0.42).Count);
        Assert.All(Field(0.3), box => Assert.InRange(box.Alpha, 0.0001f, 1f));
    }
}
