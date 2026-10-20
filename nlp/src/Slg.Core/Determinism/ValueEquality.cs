namespace Slg.Core.Determinism;

/// <summary>Helpers that give records holding lists and arrays structural equality.</summary>
internal static class ValueEquality
{
    public static bool ListEquals<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < left.Count; i++)
        {
            if (!comparer.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ArrayEquals<T>(T[]? left, T[]? right)
        where T : IEquatable<T>
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null && right is not null && left.AsSpan().SequenceEqual(right);
    }

    public static void AddList<T>(ref HashCode hash, IReadOnlyList<T>? list)
    {
        if (list is null)
        {
            hash.Add(-1);
            return;
        }

        hash.Add(list.Count);
        foreach (var item in list)
        {
            hash.Add(item);
        }
    }
}
