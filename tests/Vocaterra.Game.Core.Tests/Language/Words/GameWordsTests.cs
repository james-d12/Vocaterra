using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Tests.Language.Words;

/// <summary>
/// The target-language words the game itself uses (CONTENT_PLAN C05): <c>game/words.json</c>, the articles from the lexicon and the beacon's place labels from
/// the scene. They were constants in the code; the words are the same, and the validator checks them against the other files.
/// </summary>
public sealed class GameWordsTests
{
    private static GameWords Words => TestGame.Data.Words;

    [Fact]
    public void TheShippedWordsAreTheOnesTheCodeHeld()
    {
        Assert.Equal(new DirectionWords("adelante", "detrás", "a la izquierda", "a la derecha"), Words.Directions);
        Assert.Equal(("bloque", "y"), (Words.BlockUnit, Words.ListJoiner));
        Assert.Equal(new ButtonWords("Repetir", "Más despacio", "Ayuda", "No sé", "Escribir"), Words.Buttons);
        Assert.Equal(("¿Escribes?", "¿?"), (Words.TypeOffer, Words.Unseen));
        Assert.Equal(new SupportWords("Puedes decir", "Escuchar la frase", "Palabras nuevas"), Words.Support);
        Assert.Equal(["yo", "tú", "él/ella/usted", "nosotros", "ustedes/ellos"], GameWords.PronounKeys.Select(key => Words.Pronoun(key[..1], key[1..])));
        Assert.Equal(["agua", "pan"], Words.KeyWords);
        Assert.Equal(("ayuda", "no sé", "hola"), (Words.HelpLine, Words.DontKnowLine, Words.Greeting));
        Assert.Equal(("ir", "a"), (Words.NearFuture.Verb, Words.NearFuture.Joiner));
        Assert.Equal(["mañana"], Words.NearFuture.TimeWords);
        Assert.Equal("río", Words.Noun("river"));
    }

    [Fact]
    public void AFileThatLacksAWordIsRefused()
    {
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "words.json"));

        Assert.Throws<InvalidDataException>(() => GameWords.Parse(json.Replace("\"blockUnit\": \"bloque\",", string.Empty, StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => GameWords.Parse(json.Replace("\"keyWords\": [\n    \"agua\",\n    \"pan\"\n  ]", "\"keyWords\": []", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => GameWords.Parse(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal)));
        Assert.Equal(Words.Directions, GameWords.Parse(json).Directions);
    }

    [Fact]
    public void TheArticlesAreTheLexiconsArtReadings()
    {
        // WordProgress counted a noun's article as part of it from a list in the code; the list is now the lexicon's art readings, the same eight forms.
        Assert.Equal(["el", "la", "las", "los", "un", "una", "unas", "unos"], TestGame.Nouns.Articles.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ThePlacesHaveTheBeaconsWordsAsTheirLabels()
    {
        var scene = TestGame.Data.Scene;

        Assert.Equal("río", scene.Labels["river"]);
        Assert.Equal("casa", scene.Labels["house2"]);
        Assert.Equal("campamento", scene.Labels["camp"]);
        Assert.Equal("fuego", scene.Labels["firering.player"]);
        Assert.Equal(17, scene.Labels.Count);
    }

    [Fact]
    public void ALabelIsAWordAndAPropNeedsANameForOne()
    {
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "scene.json"));

        Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(json.Replace("\"label\": \"lago\"", "\"label\": \" \"", StringComparison.Ordinal)));
        Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(json.Replace("\"name\": \"bridge\", \"label\"", "\"label\"", StringComparison.Ordinal)));
    }

    [Fact]
    public void AKeyWordOrARiverThatIsNoCatalogueNounIsAProblem()
    {
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "words.json"))
            .Replace("\"pan\"", "\"queso\"", StringComparison.Ordinal)
            .Replace("\"river\": \"río\"", "\"river\": \"mar\"", StringComparison.Ordinal);
        var problems = GameDataValidator.Validate(TestGame.Data.WithWords(GameWords.Parse(json)));

        Assert.Contains("game/words.json: the key word 'queso' is not a catalogue noun.", problems);
        Assert.Contains("game/words.json: the river noun 'mar' is not a catalogue noun.", problems);
    }
}
