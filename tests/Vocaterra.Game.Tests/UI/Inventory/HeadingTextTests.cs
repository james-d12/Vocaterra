using Vocaterra.Game.UI.Inventory;

namespace Vocaterra.Game.Tests.UI.Inventory;

/// <summary>I04b: what a doll column's heading says as the room shrinks.</summary>
public sealed class HeadingTextTests
{
    private static float Seven(string text) => text.Length * 7f;

    [Fact]
    public void Given_RoomForTheArticle_When_TheHeadingIsPicked_Then_ItIsTheNounWithItsArticle()
    {
        Assert.Equal("la ropa", HeadingText.Pick("la ropa", "ropa", 64, Seven));
    }

    [Fact]
    public void Given_RoomForTheNounOnly_When_TheHeadingIsPicked_Then_ItIsTheNounAlone()
    {
        Assert.Equal("armadura", HeadingText.Pick("la armadura", "armadura", 64, Seven));
    }

    [Fact]
    public void Given_RoomForNeither_When_TheHeadingIsPicked_Then_ItIsTheShortMark()
    {
        Assert.Equal("apa", HeadingText.Pick("la apariencia", "apariencia", 36, Seven));
        Assert.Equal("ro", HeadingText.Mark("ro"));
        Assert.Equal("arm", HeadingText.Mark("armadura"));
    }
}
