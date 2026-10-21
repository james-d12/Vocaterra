namespace Vocaterra.Nlp.Core.Verdicts;

/// <summary>Pronunciation score for one expected phone aligned to a grapheme span.</summary>
public sealed record PhoneScore(TextSpan GraphemeSpan, string Expected, string Actual, double Score);
