using Vocaterra.Game.UI.Inventory;

namespace Vocaterra.Game.Tests.UI.Inventory;

/// <summary>I04b, I04c: what a doll column's heading says as the room shrinks.</summary>
public sealed class HeadingTextTests
{
    private static float Seven(string text) => text.Length * 7f;

    [Fact]
    public void Given_RoomForTheNoun_When_TheHeadingIsPicked_Then_ItIsTheBareNounWithoutAnArticle()
    {
        Assert.Equal("ropa", HeadingText.Pick("ropa", 64, Seven));
        Assert.Equal("apariencia", HeadingText.Pick("apariencia", 70, Seven));
    }

    [Fact]
    public void Given_RoomForNothingLonger_When_TheHeadingIsPicked_Then_ItIsTheShortMark()
    {
        Assert.Equal("apa", HeadingText.Pick("apariencia", 36, Seven));
        Assert.Equal("ro", HeadingText.Mark("ro"));
        Assert.Equal("arm", HeadingText.Mark("armadura"));
    }
}
