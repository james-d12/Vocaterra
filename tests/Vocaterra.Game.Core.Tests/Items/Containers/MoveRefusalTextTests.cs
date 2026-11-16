using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Containers;

/// <summary>Which line says why a move was refused (I04): every reason either has a line in the locale bundle or none is needed.</summary>
public sealed class MoveRefusalTextTests
{
    [Theory]
    [InlineData(MoveReason.NoRoom, EquipResult.Done, ContainerIds.Bag, "refuseNoRoom")]
    [InlineData(MoveReason.DifferentItem, EquipResult.Done, ContainerIds.Bag, "refuseDifferent")]
    [InlineData(MoveReason.DifferentItem, EquipResult.Done, ContainerIds.Trash, "refuseTrashDifferent")]
    [InlineData(MoveReason.SlotFull, EquipResult.Done, ContainerIds.Bag, "refuseSlotFull")]
    [InlineData(MoveReason.StackTooLarge, EquipResult.Done, ContainerIds.Bag, "refuseStackTooLarge")]
    [InlineData(MoveReason.SlotRefused, EquipResult.WrongSlot, ContainerIds.Equipment, "refuseWrongSlot")]
    [InlineData(MoveReason.SlotRefused, EquipResult.WrongKind, ContainerIds.Equipment, "refuseWrongKind")]
    [InlineData(MoveReason.SlotRefused, EquipResult.Duplicate, ContainerIds.Equipment, "refuseDuplicate")]
    [InlineData(MoveReason.SlotRefused, EquipResult.StackTooLarge, ContainerIds.Equipment, "refuseWearOne")]
    [InlineData(MoveReason.SlotRefused, EquipResult.NotWearable, ContainerIds.Equipment, "refuseNotWearable")]
    [InlineData(MoveReason.SlotRefused, EquipResult.BagNotEmpty, ContainerIds.Equipment, "refuseBagNotEmpty")]
    [InlineData(MoveReason.SlotRefused, EquipResult.BagFull, ContainerIds.Equipment, "refuseNoRoom")]
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
        Assert.All(keys, key => Assert.True(locale.Has("ui." + key), $"ui.{key} is missing from the locale bundle"));
    }
}
