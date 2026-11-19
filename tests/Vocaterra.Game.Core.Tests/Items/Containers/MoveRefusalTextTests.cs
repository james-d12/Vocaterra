using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Containers;

/// <summary>Which line says why a move was refused (I04): every reason either has a line in the locale bundle or none is needed.</summary>
public sealed class MoveRefusalTextTests
{
    [Theory]
    [InlineData(MoveReason.NoRoom, EquipResult.Done, ContainerIds.Bag, "refuse.noRoom")]
    [InlineData(MoveReason.DifferentItem, EquipResult.Done, ContainerIds.Bag, "refuse.different")]
    [InlineData(MoveReason.DifferentItem, EquipResult.Done, ContainerIds.Trash, "refuse.trashDifferent")]
    [InlineData(MoveReason.SlotFull, EquipResult.Done, ContainerIds.Bag, "refuse.slotFull")]
    [InlineData(MoveReason.StackTooLarge, EquipResult.Done, ContainerIds.Bag, "refuse.stackTooLarge")]
    [InlineData(MoveReason.SlotRefused, EquipResult.WrongSlot, ContainerIds.Equipment, "refuse.wrongSlot")]
    [InlineData(MoveReason.SlotRefused, EquipResult.WrongKind, ContainerIds.Equipment, "refuse.wrongKind")]
    [InlineData(MoveReason.SlotRefused, EquipResult.Duplicate, ContainerIds.Equipment, "refuse.duplicate")]
    [InlineData(MoveReason.SlotRefused, EquipResult.StackTooLarge, ContainerIds.Equipment, "refuse.wearOne")]
    [InlineData(MoveReason.SlotRefused, EquipResult.NotWearable, ContainerIds.Equipment, "refuse.notWearable")]
    [InlineData(MoveReason.SlotRefused, EquipResult.BagNotEmpty, ContainerIds.Equipment, "refuse.bagNotEmpty")]
    [InlineData(MoveReason.SlotRefused, EquipResult.BagFull, ContainerIds.Equipment, "refuse.noRoom")]
    public void Given_AReason_When_TheKeyIsAsked_Then_ItIsTheLineThatSaysWhy(MoveReason reason, EquipResult equip, int container, string key)
    {
        Assert.Equal(key, MoveRefusalText.KeyFor(reason, equip, container));
    }

    [Theory]
    [InlineData(MoveReason.Done)]
    [InlineData(MoveReason.NothingToMove)]
    [InlineData(MoveReason.NoSuchSlot)]
    [InlineData(MoveReason.NoSuchContainer)]
    [InlineData(MoveReason.NoSuchAction)]
    public void Given_AReasonThePlayerNeedsNoLineFor_When_TheKeyIsAsked_Then_ItIsNull(MoveReason reason)
    {
        Assert.Null(MoveRefusalText.KeyFor(reason, EquipResult.Done, ContainerIds.Bag));
    }

    [Fact]
    public void Given_EveryReasonOfBothEnums_When_TheKeyIsAsked_Then_AKeyHasALineInTheLocale()
    {
        var locale = TestGame.Data.Locale;
        var keys = Enum.GetValues<MoveReason>().SelectMany(r => Enum.GetValues<EquipResult>().Select(e => MoveRefusalText.KeyFor(r, e, ContainerIds.Bag)))
            .Concat(Enum.GetValues<MoveReason>().Select(r => MoveRefusalText.KeyFor(r, EquipResult.Done, ContainerIds.Trash)))
            .Concat(Enum.GetValues<EquipResult>().Select(static e => MoveRefusalText.KeyFor(e)))
            .OfType<string>()
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.True(locale.Has(key), $"{key} is missing from the locale bundle"));
        Assert.All(keys, key => Assert.Contains(key, MoveRefusalText.AllKeys));
    }

    [Fact]
    public void Given_EveryRefusalKey_When_ItsLineIsRead_Then_TheGameSaysItInSpanishWithTheEnglishInTheLocale()
    {
        var data = TestGame.Data;

        Assert.All(MoveRefusalText.AllKeys, key =>
        {
            var spanish = data.Words.HudLine(key);
            Assert.False(string.IsNullOrWhiteSpace(spanish), $"{key} has no Spanish line in words.json");
            Assert.True(data.Locale.Has(key), $"{key} has no English");
            Assert.NotEqual(spanish, data.Locale.Text(key));
        });
        Assert.Equal(MoveRefusalText.AllKeys.Count, MoveRefusalText.AllKeys.Distinct().Count());
        Assert.All(MoveRefusalText.AllKeys, key => Assert.Contains(key, Vocaterra.Game.Core.Content.GameDataValidator.HudLineKeys(data))); // so the validator asks for both
    }
}
