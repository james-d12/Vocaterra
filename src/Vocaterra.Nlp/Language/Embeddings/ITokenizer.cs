namespace Vocaterra.Nlp.Language.Embeddings;

/// <summary>Turns text into the model's input token ids, including the special begin and end tokens the model expects.</summary>
public interface ITokenizer
{
    /// <summary>Encodes the text; the result is deterministic and never empty.</summary>
    int[] Encode(string text);
}
