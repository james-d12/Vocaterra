namespace Slg.Pronunciation;

/// <summary>The result of mapping a model vocabulary: unique output symbols, the model columns behind each, and the unmapped labels.</summary>
/// <param name="Symbols">Unique mapped symbols in order of the lowest model id that maps to them.</param>
/// <param name="Groups">For each symbol, the model columns (ascending) whose probability it adds up.</param>
/// <param name="Unmapped">Labels with no rule, sorted ordinally; they appear in <paramref name="Symbols"/> unchanged.</param>
internal sealed record VocabularyMapping(string[] Symbols, int[][] Groups, IReadOnlyList<string> Unmapped)
{
    /// <summary>Maps a label-to-id vocabulary. Ids must be exactly 0 to n-1 and the padding token must be present.</summary>
    public static VocabularyMapping Build(IReadOnlyDictionary<string, int> labels, EspeakPhoneMap map)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(map);

        var byId = new string?[labels.Count];
        foreach (var (label, id) in labels)
        {
            if (id < 0 || id >= byId.Length || byId[id] is not null)
            {
                throw new ArgumentException($"Vocabulary ids must be unique and cover 0 to {labels.Count - 1}; '{label}' has id {id}.", nameof(labels));
            }

            byId[id] = label;
        }

        var symbols = new List<string>();
        var members = new List<List<int>>();
        var unmapped = new List<string>();
        for (var id = 0; id < byId.Length; id++)
        {
            var label = byId[id]!;
            var symbol = map.Map(label);
            if (!map.IsMapped(label))
            {
                unmapped.Add(label);
            }

            var index = symbols.IndexOf(symbol);
            if (index < 0)
            {
                symbols.Add(symbol);
                members.Add([id]);
            }
            else
            {
                members[index].Add(id);
            }
        }

        if (!symbols.Contains(CtcDecoder.BlankSymbol))
        {
            throw new ArgumentException($"The vocabulary has no padding token that maps to '{CtcDecoder.BlankSymbol}'.", nameof(labels));
        }

        unmapped.Sort(StringComparer.Ordinal);
        return new VocabularyMapping(
            [.. symbols],
            members.Select(static ids => ids.ToArray()).ToArray(),
            unmapped.AsReadOnly());
    }
}
