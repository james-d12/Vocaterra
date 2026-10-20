using System.Collections;
using Slg.Core.Stages;

namespace Slg.Nlp.Lexicon;

/// <summary>The readings found for a surface form, as a read-only list plus a flag for accent-insensitive fallback matches.</summary>
public sealed class LexiconLookupResult : IReadOnlyList<LexicalFeatures>
{
    private readonly LexicalFeatures[] _readings;

    internal LexiconLookupResult(string form, LexicalFeatures[] readings, bool accentInsensitive)
    {
        Form = form;
        _readings = readings;
        IsAccentInsensitive = accentInsensitive;
    }

    /// <summary>The lookup form after NFC and lowercase normalization.</summary>
    public string Form { get; }

    /// <summary>True when no entry matched exactly and the readings come from the accent-stripped fallback.</summary>
    public bool IsAccentInsensitive { get; }

    /// <summary>Number of readings; zero means the word is unknown.</summary>
    public int Count => _readings.Length;

    /// <summary>The reading at the given index; readings are in ordinal entry order.</summary>
    public LexicalFeatures this[int index] => _readings[index];

    /// <summary>Enumerates the readings in order.</summary>
    public IEnumerator<LexicalFeatures> GetEnumerator()
    {
        return ((IEnumerable<LexicalFeatures>)_readings).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
