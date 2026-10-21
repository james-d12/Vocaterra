namespace Vocaterra.Nlp.Pipeline;

/// <summary>Element-wise equality and hashing helpers for the list-holding records of this assembly.</summary>
internal static class ListEquality
{
    public static bool Equal<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
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

    public static void Add<T>(ref HashCode hash, IReadOnlyList<T>? list)
    {
        if (list is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(list.Count);
        foreach (var item in list)
        {
            hash.Add(item);
        }
    }
}
