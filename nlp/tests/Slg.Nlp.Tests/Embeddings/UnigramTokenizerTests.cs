namespace Slg.Nlp.Tests.Embeddings;

using Slg.Nlp.Embeddings;

public class UnigramTokenizerTests
{
    // ids: 0 <s>, 1 <pad>, 2 </s>, 3 <unk>, then the pieces below in order.
    private const string TokenizerJson = """
        {
          "version": "1.0",
          "added_tokens": [
            { "id": 0, "content": "<s>", "special": true },
            { "id": 1, "content": "<pad>", "special": true },
            { "id": 2, "content": "</s>", "special": true },
            { "id": 3, "content": "<unk>", "special": true }
          ],
          "model": {
            "type": "Unigram",
            "unk_id": 3,
            "vocab": [
              ["<s>", 0.0], ["<pad>", 0.0], ["</s>", 0.0], ["<unk>", 0.0],
              ["▁hola", -4.0], ["▁ho", -5.0], ["la", -3.0], ["▁", -2.0],
              ["h", -6.0], ["o", -6.0], ["l", -6.0], ["a", -6.0],
              ["▁mundo", -5.0], ["▁mun", -5.0], ["do", -4.5],
              ["▁lana", -6.0], ["▁la", -2.0], ["na", -2.0],
              ["ñ", -7.0], ["▁es", -4.0]
            ]
          }
        }
        """;

    private static readonly UnigramTokenizer Tokenizer = UnigramTokenizer.FromJson(TokenizerJson);

    [Fact]
    public void WrapsTheBestSegmentationInBeginAndEndTokens()
    {
        Assert.Equal([0, 4, 2], Tokenizer.Encode("hola"));
        Assert.Equal([0, 4, 12, 2], Tokenizer.Encode("hola mundo"));
        Assert.Equal(["<s>", "▁hola", "▁mundo", "</s>"], Tokenizer.PiecesOf(Tokenizer.Encode("hola mundo")));
    }

    [Fact]
    public void ViterbiPrefersTheHigherTotalScore()
    {
        // "▁lana" scores -6.0 but "▁la" + "na" scores -4.0.
        Assert.Equal([0, 16, 17, 2], Tokenizer.Encode("lana"));
        // "▁mundo" -5.0 beats "▁mun" + "do" -9.5.
        Assert.Equal([0, 12, 2], Tokenizer.Encode("mundo"));
    }

    [Fact]
    public void WhitespaceCaseOfFormAndCompatibilityCharactersAreFolded()
    {
        var expected = Tokenizer.Encode("hola mundo");

        Assert.Equal(expected, Tokenizer.Encode("  hola \t\n mundo  "));
        Assert.Equal(expected, Tokenizer.Encode("ｈｏｌａ　ｍｕｎｄｏ"));
    }

    [Fact]
    public void EmptyTextGivesOnlyTheSpecialTokens()
    {
        Assert.Equal([0, 2], Tokenizer.Encode(string.Empty));
        Assert.Equal([0, 2], Tokenizer.Encode("   "));
    }

    [Fact]
    public void UnknownCharactersBecomeOneFusedUnknownToken()
    {
        Assert.Equal([0, 7, 3, 2], Tokenizer.Encode("¿"));
        Assert.Equal([0, 7, 3, 2], Tokenizer.Encode("¿¡"));
        Assert.Equal([0, 7, 3, 2], Tokenizer.Encode("\U0001F600"));
    }

    [Fact]
    public void KnownPiecesSurroundUnknownCharacters()
    {
        // "▁" "¿" (unk) "a"
        Assert.Equal([0, 7, 3, 11, 2], Tokenizer.Encode("¿a"));
    }

    [Fact]
    public void LongInputIsTruncatedKeepingTheEndToken()
    {
        var capped3 = UnigramTokenizer.FromJson(TokenizerJson, maxLength: 3);
        var capped4 = UnigramTokenizer.FromJson(TokenizerJson, maxLength: 4);

        Assert.Equal([0, 4, 2], capped3.Encode("hola mundo hola"));
        Assert.Equal([0, 4, 12, 2], capped4.Encode("hola mundo hola"));
    }

    [Fact]
    public void EncodingIsDeterministic()
    {
        var other = UnigramTokenizer.FromJson(TokenizerJson);

        Assert.Equal(Tokenizer.Encode("hola mundo lana"), other.Encode("hola mundo lana"));
        Assert.Equal(Tokenizer.Encode("hola mundo lana"), Tokenizer.Encode("hola mundo lana"));
    }

    [Fact]
    public void ReportsVocabularySize()
    {
        Assert.Equal(20, Tokenizer.VocabularySize);
    }

    [Fact]
    public void LoadsFromFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "slg-unigram-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, TokenizerJson);
        try
        {
            Assert.Equal([0, 4, 2], UnigramTokenizer.FromFile(path).Encode("hola"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NonUnigramModelsAreNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => UnigramTokenizer.FromJson("{\"model\":{\"type\":\"WordPiece\",\"vocab\":{}}}"));
    }

    [Fact]
    public void MissingSpecialTokensAreRejected()
    {
        const string json = "{\"model\":{\"type\":\"Unigram\",\"unk_id\":0,\"vocab\":[[\"<unk>\",0.0],[\"a\",-1.0]]}}";

        Assert.Throws<FormatException>(() => UnigramTokenizer.FromJson(json));
        Assert.Throws<FormatException>(() => UnigramTokenizer.FromJson("{}"));
    }
}
