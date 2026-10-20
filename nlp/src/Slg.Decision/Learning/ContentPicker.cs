using Slg.Core.Determinism;
using Slg.Decision.Scheduling;

namespace Slg.Decision.Learning;

/// <summary>A piece of practice content and the skills it exercises.</summary>
public sealed record ContentItem(string ItemId, IReadOnlyList<string> SkillIds)
{
    /// <summary>Structural equality including the skill list.</summary>
    public bool Equals(ContentItem? other)
    {
        return other is not null
            && string.Equals(ItemId, other.ItemId, StringComparison.Ordinal)
            && ListEquality.Equal(SkillIds, other.SkillIds);
    }

    /// <summary>Hash consistent with <see cref="Equals(ContentItem)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(ItemId, StringComparer.Ordinal);
        ListEquality.Add(ref hash, SkillIds);
        return hash.ToHashCode();
    }
}

/// <summary>Chooses the next content item: due reviews first, otherwise the item practising the learner's weakest skill.</summary>
public static class ContentPicker
{
    private const int MasteryDecimals = 9;

    /// <summary>
    /// Returns the id of the next item among <paramref name="items"/>. Cards that are due (<c>Due &lt;= now</c>) and belong to an
    /// available item win: earliest due first, ties by ordinal item id. Otherwise the item whose weakest skill has the lowest
    /// mastery (quantized to 9 decimals; unseen skills use the prior; an item without skills counts as fully mastered) wins, ties by ordinal item id.
    /// </summary>
    public static string Next(LearnerModel model, IReadOnlyList<ContentItem> items, IReadOnlyList<ReviewCard> cards, StoredTimestamp now)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(cards);
        if (items.Count == 0)
        {
            throw new ArgumentException("At least one content item is required.", nameof(items));
        }

        var available = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            available.Add(item.ItemId);
        }

        ReviewCard? bestCard = null;
        foreach (var card in cards)
        {
            if (card.Due > now || !available.Contains(card.ItemId))
            {
                continue;
            }

            if (bestCard is null
                || card.Due < bestCard.Due
                || (card.Due == bestCard.Due && string.CompareOrdinal(card.ItemId, bestCard.ItemId) < 0))
            {
                bestCard = card;
            }
        }

        if (bestCard is not null)
        {
            return bestCard.ItemId;
        }

        string? bestId = null;
        var bestMastery = double.PositiveInfinity;
        foreach (var item in items)
        {
            var mastery = WeakestMastery(model, item);
            if (bestId is null
                || mastery < bestMastery
                || (mastery == bestMastery && string.CompareOrdinal(item.ItemId, bestId) < 0))
            {
                bestId = item.ItemId;
                bestMastery = mastery;
            }
        }

        return bestId!;
    }

    private static double WeakestMastery(LearnerModel model, ContentItem item)
    {
        var weakest = 1.0;
        foreach (var skillId in item.SkillIds)
        {
            weakest = Math.Min(weakest, model.PKnown(skillId));
        }

        return Math.Round(weakest, MasteryDecimals, MidpointRounding.AwayFromZero);
    }
}
