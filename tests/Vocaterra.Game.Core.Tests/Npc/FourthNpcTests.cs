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
/// A fourth NPC by data alone (VILLAGE_PLAN V01): a copy of the bundle with a fisher added the way <c>content/README.md</c> says ("Adding an NPC": the core's
/// agent, its prompts and pools, <c>game/npcs/fisher.json</c> and its id at the end of <c>game/npcs.json</c>) and no code. Before V01 he would have waited on
/// the villager's prompt, opened with the villager's greeting and spoken with the guide's voice; now his file decides all three. The copy leaves out the
/// speech folder (no recording is needed to talk), and the shipped bundle is not touched.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class FourthNpcTests
{
    private const string FisherFile = """
        {
          "schemaVersion": 2,
          "id": "fisher",
          "label": "Pescador",
          "name": "Mar",
          "figure": "fisher",
          "prompts": { "idle": "fisher-open", "pointing": "fisher-more" },
          "opening": { "event": "fisher.greet", "when": "first" },
          "voice": { "speaker": 53, "name": "em_santa", "speed": 0.9 }
        }
        """;

    private const string FisherAgent = """
        {
          "agentId": "fisher",
          "initialState": "idle",
          "variantPools": [ { "id": "fisher_hello", "count": 1 }, { "id": "fisher_point", "count": 1 }, { "id": "not_understood", "count": 2 } ],
          "states": [
            {
              "id": "idle",
              "transitions": [
                { "onHostEvent": "fisher.greet", "to": "idle", "actions": [{ "dialogueIntent": "fisher_hello", "variantPoolId": "fisher_hello" }] },
                { "on": {"intent": "ask_directions", "communicated": true}, "to": "pointing", "actions": [{ "dialogueIntent": "fisher_point", "variantPoolId": "fisher_point" }] }
              ],
              "fallback": { "actions": [{ "dialogueIntent": "not_understood", "variantPoolId": "not_understood" }] }
            },
            {
              "id": "pointing",
              "transitions": [
                { "onHostEvent": "fisher.greet", "to": "pointing", "actions": [{ "dialogueIntent": "fisher_hello", "variantPoolId": "fisher_hello" }] },
                { "on": {"intent": "farewell", "communicated": true}, "to": "idle", "actions": [{ "dialogueIntent": "fisher_hello", "variantPoolId": "fisher_hello" }] }
              ],
              "fallback": { "actions": [{ "dialogueIntent": "not_understood", "variantPoolId": "not_understood" }] }
            }
          ]
        }
        """;

    private static readonly Lazy<GameData> Fisher = new(BuildFisherBundle);

    [Fact]
    public void AFourthNpcInTheDataHoldsTogetherWithTheNpcChecks()
    {
        var npc = Fisher.Value.Npcs.Find("fisher")!;

        Assert.Equal(["guide", "shopkeeper", "villager", "fisher"], Fisher.Value.Npcs.Ids);
        Assert.Equal(new NpcVoiceSpec(53, "em_santa", 0.9f), npc.Voice);
        Assert.Equal(new NpcOpeningRule("fisher.greet", OpeningWhen.First), npc.Opening);

        // The copy has no scene placement or glosses for him (those are the scene's and the locale's); what his NPC file says is all in order.
        Assert.DoesNotContain(GameDataValidator.Validate(Fisher.Value), static p => p.StartsWith("game/npcs", StringComparison.Ordinal));
    }

    [Fact]
    public void AFourthNpcGetsItsOwnPromptInEachStateAndItsOwnOpening()
    {
        var npcs = new NpcService(Fisher.Value, new NoFacts(), TestGame.Seed, new StoredTimestamp(GuideSession.DefaultStartedAt));
        Assert.Equal("fisher-open", npcs.PromptOf("fisher"));

        var first = npcs.Open("fisher", objectiveComplete: false, new StoredTimestamp(GuideSession.DefaultStartedAt), "hash");
        Assert.True(first.HostTurn); // his own opening event (the villager's would be villager.greet; before V01 he would have raised it)
        Assert.Equal(["¡Hola! Me llamo Mar."], first.Lines);
        Assert.Equal("fisher-open", first.Prompt);

        var turn = npcs.Say("fisher", "¿dónde está el río?", new GuideScene(new GuideCell(0, 0, 0)), new StoredTimestamp(GuideSession.DefaultStartedAt + 5), "hash");
        Assert.Equal(("ask_directions", "pointing", "fisher-open"), (turn.Intent, turn.AgentState, turn.Prompt));
        Assert.Equal(["El río está allí."], turn.Replies);
        Assert.Equal("fisher-more", npcs.PromptOf("fisher")); // the prompt of his second state, not the villager's villager-open
        Assert.Equal("fisher-open", npcs.PromptAfter("fisher", "idle"));

        var again = npcs.Open("fisher", objectiveComplete: false, new StoredTimestamp(GuideSession.DefaultStartedAt + 10), "hash");
        Assert.False(again.HostTurn); // "first": the second talk opens with the text of the prompt he waits on
        Assert.Equal(["¿Algo más?"], again.Lines);
    }

    [Fact]
    public void AFourthNpcsPoolsAndPromptsSpeakWithItsOwnVoice()
    {
        var speech = Fisher.Value.Speech.ToDictionary(static line => line.LineId, StringComparer.Ordinal);

        Assert.Equal("fisher", SpeechSpeakers.OfPool("fisher_hello", Fisher.Value.Npcs));
        Assert.Equal("fisher", speech["fisher_point.1"].Speaker);
        Assert.Equal("fisher", speech["guide.lesson.fisher.near.1"].Speaker); // a lesson line named guide.lesson.<id>.* is his too
        Assert.Equal("fisher", speech["prompt.fisher-open"].Speaker);
        Assert.Equal(SpeechSpeakers.Guide, speech["guide.lesson.end.1"].Speaker);
    }

    // The bundle with the fisher: copied without the speech folder, the five files edited or added, the manifest computed again, loaded by the core and read by
    // the game without the checks between the files (the copy lacks his scene placement and glosses, which this test is not about).
    private static GameData BuildFisherBundle()
    {
        var source = GameBundle.ResolveDirectory();
        var copy = Path.Combine(Path.GetTempPath(), "vocaterra-v01-fisher-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
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

            File.WriteAllText(Path.Combine(copy, "agents", "fisher.json"), FisherAgent);
            File.WriteAllText(Path.Combine(copy, "game", "npcs", "fisher.json"), FisherFile);
            Edit(copy, "game/npcs.json", root => root["npcs"]!.AsArray().Add("fisher"));
            Edit(copy, "prompts/prompts.json", root =>
            {
                root.AsArray().Add(Prompt("fisher-open", "¿Qué buscas?"));
                root.AsArray().Add(Prompt("fisher-more", "¿Algo más?"));
            });
            Edit(copy, "dialogue/dialogue.json", root =>
            {
                var pools = root["pools"]!.AsObject();
                pools.Add("fisher_hello", Pool("fisher_hello", "¡Hola! Me llamo Mar."));
                pools.Add("fisher_point", Pool("fisher_point", "El río está allí."));
                pools.Add("guide.lesson.fisher.near", Pool("guide.lesson.fisher.near", "La tienda está cerca."));
            });
            Edit(copy, "provenance.json", root =>
            {
                var items = root["items"]!.AsArray();
                foreach (var speech in items.Where(static item => item!["path"]!.GetValue<string>().StartsWith(SpeechLines.FolderName + "/", StringComparison.Ordinal)).ToList())
                {
                    items.Remove(speech);
                }

                foreach (var path in new[] { "agents/fisher.json", "game/npcs/fisher.json" })
                {
                    items.Add(new JsonObject { ["path"] = path, ["generatedBy"] = "hand-authored", ["approvedBy"] = null, ["approvedOn"] = null });
                }
            });
            File.WriteAllText(Path.Combine(copy, ContentPaths.Manifest), ContentManifest.Compute(copy).ToJson());

            var bundle = GameBundle.Load(copy);
            return GameData.Read(bundle, TestGame.Locale);
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

    private static JsonObject Prompt(string id, string text) => new()
    {
        ["promptId"] = id,
        ["mode"] = "Open",
        ["difficulty"] = "Beginner",
        ["answerSetId"] = "places-basic",
        ["allowedIntentIds"] = new JsonArray("ask_directions", "greet", "thank", "farewell", "ask_help", "dont_know"),
        ["agentId"] = "fisher",
        ["slotGrammarIds"] = new JsonArray(),
        ["text"] = text,
    };

    private static JsonObject Pool(string id, string text) => new()
    {
        ["poolId"] = id,
        ["variants"] = new JsonArray(new JsonObject { ["lineId"] = id + ".1", ["text"] = text }),
    };

    // The fisher's turns ask nothing of the world (only the guide's lines do).
    private sealed class NoFacts : IGuideFacts
    {
        public GuideNearest? NearestOfNoun(GuideViewpoint from, string lemma) => null;

        public GuideNearest? NearestOfNounJoinedTo(GuideViewpoint from, string lemma, GuideCell body) => null;

        public IReadOnlyList<string> NounsAt(GuideCell cell) => [];
    }
}
