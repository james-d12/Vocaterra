namespace Slg.Response.Templates;

/// <summary>One dialogue line: a stable line id (also the file name of its pre-rendered audio) and its template text.</summary>
public sealed record DialogueTemplate(string LineId, string Text)
{
    /// <summary>Stable identifier of the line; never empty.</summary>
    public string LineId { get; init; } = !string.IsNullOrWhiteSpace(LineId)
        ? LineId
        : throw new ArgumentException("A line id is required.", nameof(LineId));

    /// <summary>Template text with <c>{slot}</c> placeholders; never empty.</summary>
    public string Text { get; init; } = !string.IsNullOrWhiteSpace(Text)
        ? Text
        : throw new ArgumentException("Template text is required.", nameof(Text));
}
