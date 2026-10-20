namespace Vocaterra.Nlp.Core.Verdicts;

/// <summary>A detected learner error: a stable tag, where it occurred, and the expected versus actual text.</summary>
public sealed record ErrorTag(string Tag, TextSpan Span, string Expected, string Actual);
