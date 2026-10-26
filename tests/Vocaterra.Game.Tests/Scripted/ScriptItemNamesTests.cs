using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Scripted;

namespace Vocaterra.Game.Tests.Scripted;

/// <summary>A scripted run names an item as a catalogue noun or as the item's key; a waiting <c>--shot</c> is taken once.</summary>
public sealed class ScriptItemNamesTests
{
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(GameBundle.Load()));

    [Theory]
    [InlineData("madera", ItemId.Wood)]
    [InlineData("wood", ItemId.Wood)]
    [InlineData("WOOD", ItemId.Wood)]
    [InlineData("antorcha", ItemId.Torch)]
    public void AnItemIsNamedByItsNounOrItsKey(string name, ItemId expected) => Assert.Equal(expected, ScriptItemNames.Resolve(Nouns.Value, name));

    [Fact]
    public void ANameThatIsNeitherIsNoItem() => Assert.Null(ScriptItemNames.Resolve(Nouns.Value, "zzz"));

    [Fact]
    public void AWaitingShotIsTakenOnce()
    {
        var shots = new ShotRequest();
        Assert.False(shots.Pending);
        Assert.Null(shots.Take());
        shots.Request("/tmp/a.png");
        Assert.True(shots.Pending);
        Assert.Equal("/tmp/a.png", shots.Take());
        Assert.False(shots.Pending);
    }
}
