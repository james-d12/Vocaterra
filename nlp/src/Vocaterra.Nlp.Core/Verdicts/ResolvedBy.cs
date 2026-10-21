namespace Vocaterra.Nlp.Core.Verdicts;

/// <summary>Which resolution stage produced the verdict; the declaration order is fixed and stable.</summary>
public enum ResolvedBy
{
    /// <summary>Exact answer match.</summary>
    Exact,

    /// <summary>Alias answer match.</summary>
    Alias,

    /// <summary>Fuzzy answer match.</summary>
    Fuzzy,

    /// <summary>Slot grammar match.</summary>
    Grammar,

    /// <summary>Rule-based intent resolution.</summary>
    Rules,

    /// <summary>Embedding-based intent resolution.</summary>
    Embedding,

    /// <summary>No stage could resolve the input.</summary>
    Unresolved,
}
