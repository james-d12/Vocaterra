namespace Vocaterra.Nlp.Response.Templates;

/// <summary>How a template reads a slot, as far as its filters show (see <see cref="TemplateEngine.SlotKinds"/>).</summary>
public enum TemplateSlotKind
{
    /// <summary>Plain text or a noun; no filter needs more.</summary>
    Text,

    /// <summary>A number (digits or a Spanish number word).</summary>
    Number,

    /// <summary>A verb lemma.</summary>
    Verb,
}
