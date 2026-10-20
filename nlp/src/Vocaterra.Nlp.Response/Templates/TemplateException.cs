namespace Vocaterra.Nlp.Response.Templates;

/// <summary>Raised when a template is malformed or cannot be rendered with the given slots.</summary>
public sealed class TemplateException : Exception
{
    /// <summary>Creates an exception for a placeholder (the slot name for a missing slot, otherwise the raw placeholder text).</summary>
    public TemplateException(string placeholder, string message)
        : base(message)
    {
        Placeholder = placeholder ?? throw new ArgumentNullException(nameof(placeholder));
    }

    /// <summary>The slot name when a slot is missing, otherwise the raw placeholder text including its braces.</summary>
    public string Placeholder { get; }
}
