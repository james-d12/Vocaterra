using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>A scripted run names an item as a catalogue noun or as the item's key.</summary>
public sealed class ScriptItemNamesTests
{
    [Theory]
    [InlineData("madera", ItemId.Wood)]
    [InlineData("wood", ItemId.Wood)]
    [InlineData("WOOD", ItemId.Wood)]
    [InlineData("antorcha", ItemId.Torch)]
    public void AnItemIsNamedByItsNounOrItsKey(string name, ItemId expected) => Assert.Equal(expected, ScriptItemNames.Resolve(TestGame.Nouns, name));

    [Fact]
    public void ANameThatIsNeitherIsNoItem() => Assert.Null(ScriptItemNames.Resolve(TestGame.Nouns, "zzz"));
}
