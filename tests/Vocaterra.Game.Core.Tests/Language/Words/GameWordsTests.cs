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
        Assert.Equal(("ayuda", "no sé", "hola"), (Words.HelpLine, Words.DontKnowLine, Words.Greeting));
        Assert.Equal(("ir", "a"), (Words.NearFuture.Verb, Words.NearFuture.Joiner));
        Assert.Equal(["mañana", "luego", "después", "pasado mañana"], Words.NearFuture.TimeWords); // VILLAGE_PLAN V10: the later markers make a present a plan too
        Assert.Equal("río", Words.Noun("river"));
    }

    [Fact]
    public void AFileThatLacksAWordIsRefused()
    {
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "words.json"));

        Assert.Throws<InvalidDataException>(() => GameWords.Parse(json.Replace("\"blockUnit\": \"bloque\",", string.Empty, StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => GameWords.Parse(System.Text.RegularExpressions.Regex.Replace(json, "\"timeWords\": \\[[^\\]]*\\]", "\"timeWords\": []")));
        Assert.Throws<InvalidDataException>(() => GameWords.Parse(json.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1", StringComparison.Ordinal))); // schema 2: the key words left for the NPC files (V01)
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
        Assert.Equal("panadería", scene.Labels["bakery"]); // VILLAGE_PLAN V04: the bakery, the smithy, the well, the tower and the field
        Assert.Equal("pescador", scene.Labels["npc.fisher"]); // V10: where the fisher stands, which El pueblo's walk back points at
        Assert.Equal(("herrero", "granjero"), (scene.Labels["npc.smith"], scene.Labels["npc.farmer"])); // V12: where the smith and the farmer stand, Gente del pueblo's walks
        Assert.Equal(25, scene.Labels.Count);
    }

    [Fact]
    public void ALabelIsAWordAndAPropNeedsANameForOne()
    {
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "scene.json"));

        Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(json.Replace("\"label\": \"lago\"", "\"label\": \" \"", StringComparison.Ordinal)));
        Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(json.Replace("\"name\": \"bridge\", \"label\"", "\"label\"", StringComparison.Ordinal)));
    }

    [Fact]
    public void ARiverThatIsNoCatalogueNounIsAProblem()
    {
        // The key words are the NPC files' since V01 (NpcRosterTests checks them).
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "words.json"))
            .Replace("\"river\": \"río\"", "\"river\": \"mar\"", StringComparison.Ordinal);
        var problems = GameDataValidator.Validate(TestGame.Data.WithWords(GameWords.Parse(json)));

        Assert.Contains("game/words.json: the river noun 'mar' is not a catalogue noun.", problems);
    }
}
