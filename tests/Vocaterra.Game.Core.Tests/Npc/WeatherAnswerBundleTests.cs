using System.Text.Json.Nodes;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The NPCs' answer to «¿Qué tiempo hace?» by data alone (E07): a copy of the bundle with a fourth weather kind, <c>mist</c>, whose dialogue key and pool exist only here
/// (the baker's <c>baker.mist</c> transition and the pool it names), is answered with no code change; and the validator's rule that every kind has a <c>say</c> and every NPC
/// that answers the weather has a pool for every key. The copies leave out the speech folder, and the shipped bundle is not touched.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class WeatherAnswerBundleTests
{
    private static readonly Lazy<GameData> Complete = new(() => Build(wirePool: true, say: true));

    [Fact]
    public void AFourthKindWithItsOwnKeyAndPoolIsAnsweredWithNoCodeChange()
    {
        var npcs = new NpcService(Complete.Value, new NoFacts(), TestGame.Seed, new StoredTimestamp(GuideSession.DefaultStartedAt));

        var turn = npcs.Say("baker", "¿qué tiempo hace?", new GuideScene(new GuideCell(0, 0, 0), WeatherKey: "mist"), new StoredTimestamp(GuideSession.DefaultStartedAt + 5), "hash");

        Assert.Equal(["Hay niebla."], turn.Replies);
        Assert.DoesNotContain(GameDataValidator.Validate(Complete.Value), static p => p.StartsWith("game/npcs/baker.json", StringComparison.Ordinal) || p.StartsWith("game/weather.json", StringComparison.Ordinal)); // the other six NPCs have no pool for mist: the rule names them
    }

    [Fact]
    public void AKindWhoseKeyHasNoPoolAtAnNpcThatAnswersTheWeatherIsAProblemNamingTheEventToAdd()
    {
        var problems = GameDataValidator.Validate(Build(wirePool: false, say: true));

        Assert.Contains(problems, static p => p.Contains("agents/baker.json", StringComparison.Ordinal) && p.Contains("'baker.mist'", StringComparison.Ordinal) && p.Contains("'mist'", StringComparison.Ordinal));
    }

    [Fact]
    public void AKindWithNoSayIsAProblemWhileAnNpcAnswersTheWeather()
    {
        var problems = GameDataValidator.Validate(Build(wirePool: true, say: false));

        Assert.Contains(problems, static p => p.StartsWith("game/weather.json: the kind 'mist' has no 'say'", StringComparison.Ordinal));
    }

    // The bundle copied without the speech folder, with a mist kind (reachable from cloudy) and, when asked, the baker's pool and transition for its key.
    private static GameData Build(bool wirePool, bool say)
    {
        var source = GameBundle.ResolveDirectory();
        var copy = Path.Combine(Path.GetTempPath(), "vocaterra-e07-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                if (!relative.StartsWith(SpeechLines.FolderName + "/", StringComparison.Ordinal))
                {
                    var target = Path.Combine(copy, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, overwrite: true);
                }
            }

            Edit(copy, "game/weather.json", root =>
            {
                var kinds = root["kinds"]!.AsArray();
                var mist = new JsonObject
                {
                    ["id"] = "mist",
                    ["cloudCover"] = 0.6,
                    ["sunlight"] = 0.8,
                    ["fog"] = 0.95,
                    ["wind"] = 0.05,
                    ["minSeconds"] = 100,
                    ["maxSeconds"] = 200,
                    ["next"] = new JsonObject { ["sunny"] = 1 },
                    ["morningWeight"] = 2,
                };
                if (say)
                {
                    mist["say"] = new JsonObject { ["day"] = "mist", ["night"] = "mist" };
                }

                kinds.Add(mist);
                kinds.First(static k => k!["id"]!.GetValue<string>() == "cloudy")!["next"]!.AsObject().Add("mist", 4);
            });
            if (wirePool)
            {
                Edit(copy, "dialogue/dialogue.json", root => root["pools"]!.AsObject().Add("baker_mist", new JsonObject
                {
                    ["poolId"] = "baker_mist",
                    ["variants"] = new JsonArray(new JsonObject { ["lineId"] = "baker_mist.1", ["text"] = "Hay niebla." }),
                }));
                Edit(copy, "agents/baker.json", root =>
                {
                    root["variantPools"]!.AsArray().Add(new JsonObject { ["id"] = "baker_mist", ["count"] = 1 });
                    var idle = root["states"]!.AsArray().First(static s => s!["id"]!.GetValue<string>() == "idle")!;
                    idle["transitions"]!.AsArray().Add(JsonNode.Parse("""{ "onHostEvent": "baker.mist", "to": "idle", "actions": [{ "dialogueIntent": "baker_mist", "variantPoolId": "baker_mist" }] }"""));
                });
            }

            Edit(copy, "provenance.json", root =>
            {
                var items = root["items"]!.AsArray();
                foreach (var speech in items.Where(static item => item!["path"]!.GetValue<string>().StartsWith(SpeechLines.FolderName + "/", StringComparison.Ordinal)).ToList())
                {
                    items.Remove(speech);
                }
            });
            File.WriteAllText(Path.Combine(copy, ContentPaths.Manifest), ContentManifest.Compute(copy).ToJson());

            return GameData.Read(GameBundle.Load(copy), TestGame.Locale);
        }
        finally
        {
            if (Directory.Exists(copy))
            {
                Directory.Delete(copy, recursive: true);
            }
        }
    }

    private static void Edit(string bundle, string path, Action<JsonNode> change)
    {
        var file = Path.Combine(bundle, path);
        var root = JsonNode.Parse(File.ReadAllText(file))!;
        change(root);
        File.WriteAllText(file, root.ToJsonString());
    }

    // The baker's turn asks nothing of the world.
    private sealed class NoFacts : IGuideFacts
    {
        public GuideNearest? NearestOfNoun(GuideViewpoint from, string lemma) => null;

        public GuideNearest? NearestOfNounJoinedTo(GuideViewpoint from, string lemma, GuideCell body) => null;

        public GuideNearest Toward(GuideViewpoint from, GuideCell cell) => new(cell, 0, string.Empty, 0);

        public IReadOnlyList<string> NounsAt(GuideCell cell) => [];
    }
}
