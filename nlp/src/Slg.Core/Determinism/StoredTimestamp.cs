namespace Slg.Core.Determinism;

/// <summary>A point in time as Unix seconds read from the save file, never from the wall clock.</summary>
public readonly record struct StoredTimestamp(long UnixSeconds) : IComparable<StoredTimestamp>
{
    /// <summary>Returns a timestamp the given number of seconds later (earlier when negative).</summary>
    public StoredTimestamp AddSeconds(long seconds)
    {
        return new StoredTimestamp(checked(UnixSeconds + seconds));
    }

    /// <summary>Returns the seconds from this timestamp until <paramref name="other"/> (negative if it is earlier).</summary>
    public long SecondsUntil(StoredTimestamp other)
    {
        return checked(other.UnixSeconds - UnixSeconds);
    }

    /// <summary>Compares by Unix seconds.</summary>
    public int CompareTo(StoredTimestamp other)
    {
        return UnixSeconds.CompareTo(other.UnixSeconds);
    }

    /// <summary>Returns true when the left timestamp is earlier.</summary>
    public static bool operator <(StoredTimestamp left, StoredTimestamp right) => left.UnixSeconds < right.UnixSeconds;

    /// <summary>Returns true when the left timestamp is later.</summary>
    public static bool operator >(StoredTimestamp left, StoredTimestamp right) => left.UnixSeconds > right.UnixSeconds;

    /// <summary>Returns true when the left timestamp is earlier or equal.</summary>
    public static bool operator <=(StoredTimestamp left, StoredTimestamp right) => left.UnixSeconds <= right.UnixSeconds;

    /// <summary>Returns true when the left timestamp is later or equal.</summary>
    public static bool operator >=(StoredTimestamp left, StoredTimestamp right) => left.UnixSeconds >= right.UnixSeconds;
}
