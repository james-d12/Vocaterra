using Slg.Core.Verdicts;

namespace Slg.Core.Tests;

/// <summary>Tests for <see cref="SlotMap"/>.</summary>
public class SlotMapTests
{
    [Fact]
    public void EnumerationIsOrdinalSortedRegardlessOfInsertionOrder()
    {
        var forward = SlotMap.From([new("item", "espada"), new("qty", "2"), new("Zed", "z")]);
        var backward = SlotMap.From([new("qty", "2"), new("Zed", "z"), new("item", "espada")]);
        var built = SlotMap.Empty.With("qty", "2").With("item", "espada").With("Zed", "z");

        string[] expected = ["Zed", "item", "qty"];
        Assert.Equal(expected, forward.Keys);
        Assert.Equal(expected, backward.Keys);
        Assert.Equal(expected, built.Keys);
        Assert.Equal(["z", "espada", "2"], forward.Values);
        Assert.Equal(forward.Select(p => p.Key), backward.Select(p => p.Key));
    }

    [Fact]
    public void EqualityIsByValue()
    {
        var a = SlotMap.From([new("a", "1"), new("b", "2")]);
        var b = SlotMap.Empty.With("b", "2").With("a", "1");
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a == a.With("a", "other"));
        Assert.True(a != SlotMap.Empty);
        Assert.Equal(SlotMap.Empty, SlotMap.From([]));
    }

    [Fact]
    public void WithReplacesExistingKeyWithoutMutatingOriginal()
    {
        var original = SlotMap.Empty.With("a", "1");
        var changed = original.With("a", "2");
        Assert.Equal("1", original["a"]);
        Assert.Equal("2", changed["a"]);
        Assert.Single(changed);
    }

    [Fact]
    public void DictionaryContractWorks()
    {
        IReadOnlyDictionary<string, string> map = SlotMap.From([new("item", "espada")]);
        Assert.Single(map);
        Assert.True(map.ContainsKey("item"));
        Assert.False(map.ContainsKey("Item"));
        Assert.True(map.TryGetValue("item", out var value));
        Assert.Equal("espada", value);
        Assert.False(map.TryGetValue("qty", out _));
        Assert.Throws<KeyNotFoundException>(() => map["qty"]);
    }

    [Fact]
    public void DuplicateKeysAreRejected()
    {
        Assert.Throws<ArgumentException>(() => SlotMap.From([new("a", "1"), new("a", "2")]));
    }

    [Fact]
    public void ToStringListsPairsInOrder()
    {
        Assert.Equal("{item=espada, qty=2}", SlotMap.Empty.With("qty", "2").With("item", "espada").ToString());
    }
}
