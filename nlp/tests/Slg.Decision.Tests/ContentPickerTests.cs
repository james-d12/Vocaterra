using Slg.Core.Determinism;
using Slg.Decision.Learning;
using Slg.Decision.Scheduling;

namespace Slg.Decision.Tests;

/// <summary>Tests for the content picker.</summary>
public class ContentPickerTests
{
    private static readonly StoredTimestamp Now = new(1_700_000_000);

    private static ContentItem Item(string id, params string[] skills)
    {
        return new ContentItem(id, skills);
    }

    private static ReviewCard Card(string id, long due)
    {
        return new ReviewCard(id, 4.0, 5.0, new StoredTimestamp(due - 86_400), new StoredTimestamp(due), 2, 0);
    }

    private static LearnerModel ModelWith(params (string Skill, double PKnown)[] skills)
    {
        return new LearnerModel(
            BktParameters.Default,
            skills.Select(s => new SkillMastery(s.Skill, s.PKnown, 1, 0)).ToArray());
    }

    [Fact]
    public void DueCardBeatsWeakerSkill()
    {
        var model = ModelWith(("weak", 0.05), ("strong", 0.95));
        var items = new[] { Item("weak-item", "weak"), Item("strong-item", "strong") };
        var cards = new[] { Card("strong-item", Now.UnixSeconds - 10) };

        Assert.Equal("strong-item", ContentPicker.Next(model, items, cards, Now));
    }

    [Fact]
    public void EarliestDueWinsAndTiesBreakByOrdinalId()
    {
        var model = LearnerModel.New();
        var items = new[] { Item("b"), Item("a"), Item("c"), Item("Z") };
        var cards = new[]
        {
            Card("c", Now.UnixSeconds - 5),
            Card("b", Now.UnixSeconds - 100),
            Card("a", Now.UnixSeconds - 100),
            Card("Z", Now.UnixSeconds - 50),
        };

        Assert.Equal("a", ContentPicker.Next(model, items, cards, Now));
    }

    [Fact]
    public void OrdinalTieBreakPutsUppercaseBeforeLowercase()
    {
        var items = new[] { Item("a"), Item("B") };
        var cards = new[] { Card("a", Now.UnixSeconds), Card("B", Now.UnixSeconds) };

        Assert.Equal("B", ContentPicker.Next(LearnerModel.New(), items, cards, Now));
    }

    [Fact]
    public void CardDueExactlyNowCountsAsDue()
    {
        var items = new[] { Item("x", "s"), Item("y", "t") };
        var model = ModelWith(("s", 0.9), ("t", 0.1));

        Assert.Equal("x", ContentPicker.Next(model, items, [Card("x", Now.UnixSeconds)], Now));
    }

    [Fact]
    public void FutureCardsAreIgnored()
    {
        var model = ModelWith(("s", 0.9), ("t", 0.1));
        var items = new[] { Item("x", "s"), Item("y", "t") };

        Assert.Equal("y", ContentPicker.Next(model, items, [Card("x", Now.UnixSeconds + 1)], Now));
    }

    [Fact]
    public void DueCardsOutsideTheCandidateItemsAreIgnored()
    {
        var model = ModelWith(("s", 0.9), ("t", 0.1));
        var items = new[] { Item("x", "s"), Item("y", "t") };

        Assert.Equal("y", ContentPicker.Next(model, items, [Card("elsewhere", Now.UnixSeconds - 1000)], Now));
    }

    [Fact]
    public void WithoutDueCardsTheLowestWeakestSkillWins()
    {
        var model = ModelWith(("a", 0.8), ("b", 0.2), ("c", 0.5));
        var items = new[] { Item("one", "a", "c"), Item("two", "a", "b"), Item("three", "c") };

        // weakest per item: one = 0.5, two = 0.2, three = 0.5
        Assert.Equal("two", ContentPicker.Next(model, items, [], Now));
    }

    [Fact]
    public void ItemMasteryIsItsWeakestSkillNotTheAverage()
    {
        var model = ModelWith(("a", 0.9), ("b", 0.1), ("c", 0.4), ("d", 0.4));
        var items = new[] { Item("mixed", "a", "b"), Item("flat", "c", "d") };

        // mixed: weakest 0.1 (average 0.5); flat: weakest 0.4.
        Assert.Equal("mixed", ContentPicker.Next(model, items, [], Now));
    }

    [Fact]
    public void MasteryTiesBreakByOrdinalItemId()
    {
        var model = ModelWith(("s", 0.4));
        var items = new[] { Item("m", "s"), Item("k", "s"), Item("z", "s") };

        Assert.Equal("k", ContentPicker.Next(model, items, [], Now));
    }

    [Fact]
    public void UnseenSkillsUseThePrior()
    {
        // Seen skill at 0.29 is weaker than the 0.3 prior of an unseen skill.
        var model = ModelWith(("seen", 0.29));
        var items = new[] { Item("fresh", "unseen"), Item("practised", "seen") };

        Assert.Equal("practised", ContentPicker.Next(model, items, [], Now));
    }

    [Fact]
    public void ItemsWithoutSkillsComeLast()
    {
        var model = ModelWith(("s", 0.95));
        var items = new[] { Item("a-no-skills"), Item("b", "s") };

        Assert.Equal("b", ContentPicker.Next(model, items, [], Now));
    }

    [Fact]
    public void MasteryDifferencesBelowTheQuantizationFallBackToOrdinalOrder()
    {
        var model = ModelWith(("s1", 0.4), ("s2", 0.4 + 1e-12));
        var items = new[] { Item("b", "s1"), Item("a", "s2") };

        Assert.Equal("a", ContentPicker.Next(model, items, [], Now));
    }

    [Fact]
    public void ResultDoesNotDependOnInputOrder()
    {
        var model = ModelWith(("p", 0.3), ("q", 0.3));
        var forward = new[] { Item("i1", "p"), Item("i2", "q"), Item("i3", "p") };
        var backward = forward.Reverse().ToArray();
        var cards = new[] { Card("i3", Now.UnixSeconds - 1), Card("i2", Now.UnixSeconds - 1) };

        Assert.Equal(
            ContentPicker.Next(model, forward, cards, Now),
            ContentPicker.Next(model, backward, cards.Reverse().ToArray(), Now));
        Assert.Equal("i2", ContentPicker.Next(model, forward, cards, Now));
    }

    [Fact]
    public void EmptyItemListIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ContentPicker.Next(LearnerModel.New(), [], [], Now));
    }
}
