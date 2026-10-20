namespace Vocaterra.Nlp.Decision;

/// <summary>Element-wise equality helpers so records holding lists compare by value.</summary>
internal static class ListEquality
{
    public static bool Equal<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
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

    public static void Add<T>(ref HashCode hash, IReadOnlyList<T> list)
    {
        hash.Add(list.Count);
        foreach (var item in list)
        {
            hash.Add(item);
        }
    }
}
