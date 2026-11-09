using System.Text.Json.Nodes;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The NPCs as data (content-plan 3.6, C08): <c>game/npcs.json</c>, one file per NPC, and <c>game/host-events.json</c>. They were tables in the code
/// (<c>NpcService.AgentIds</c>, <c>NpcNames</c>, <c>PhraseIds.FocusAt</c> and <c>HomeOf</c>, <c>NpcHostEvents</c>'s table); the data gives the same answers,
/// and the validator checks it against the core's agents and prompts. Since schema 2 (village-plan V01) the file also holds the prompt of each state, the
/// opening, the voice, the key words and the pool prefix, which were switches in <c>NpcService</c>, the renderer and <c>SpeechSpeakers</c>.
/// </summary>
public sealed class NpcRosterTests
{
    private static NpcRoster Npcs => TestGame.Data.Npcs;

    private static string NpcFile(string id) => File.ReadAllText(Repo.Path("content", "game-v1", "game", "npcs", id + ".json"));

    // The shipped host-event table with one entry's field set to another value.
    private static string HostEvents(Action<JsonArray>? change = null)
    {
        var root = JsonNode.Parse(File.ReadAllText(Repo.Path("content", "game-v1", "game", "host-events.json")))!;
        change?.Invoke(root["events"]!.AsArray());
        return root.ToJsonString();
    }

    private static JsonNode Entry(JsonArray events, string hostEvent) => events.First(e => e!["event"]!.GetValue<string>() == hostEvent)!;

    [Fact]
    public void TheShippedNpcsAreTheOnesTheCodeHeld()
    {
        // The three the code held, and since village-plan V10 the fisher Mar, V11 the baker Luz and V12 the smith Domingo and the farmer León, at the end (the saves
        // keep the NPCs in this order).
        Assert.Equal(["guide", "shopkeeper", "villager", "fisher", "baker", "smith", "farmer"], Npcs.Ids);
        Assert.Equal(["Guía", "Tendero", "Aldeano", "Pescador", "Panadera", "Herrero", "Granjero"], Npcs.All.Select(static n => n.Label));
        Assert.Equal(["Luna", "Tendero", "Aldeano", "Mar", "Luz", "Domingo", "León"], Npcs.All.Select(static n => n.Called));
        Assert.Equal(["guide", "shopkeeper", "villager", "fisher", "baker", "smith", "farmer"], Npcs.All.Select(static n => n.Figure));
        Assert.Equal("otro", Npcs.CalledOf("otro"));
    }

    [Fact]
    public void TheShippedNpcsBehaveAsTheCodeDid()
    {
        var guide = Npcs.Find("guide")!;
        var shopkeeper = Npcs.Find("shopkeeper")!;
        var villager = Npcs.Find("villager")!;

        // NpcService.PromptAfter's switch: the guide's offer and review, the stall's offer in "offering", and every other state the initial state's prompt.
        Assert.Equal(("guide-open", "guide-offer", "guide-review", "guide-open"), (guide.PromptIn("idle", "idle"), guide.PromptIn("offering", "idle"), guide.PromptIn("offering", "idle", review: true), guide.PromptIn("pointing", "idle")));
        Assert.Equal(("stall-need", "stall-need", "stall-offer", "stall-need"), (shopkeeper.PromptIn("idle", "idle"), shopkeeper.PromptIn("asking", "idle"), shopkeeper.PromptIn("offering", "idle"), shopkeeper.PromptIn("done", "idle", review: true)));
        Assert.Equal(("villager-open", "villager-open"), (villager.PromptIn("idle", "idle"), villager.PromptIn("pointing", "idle")));

        // NpcService.OpeningEvent's switch: the guide never (her transfer offer stays code), the stall's question at the first talk, the villager's greeting always.
        Assert.Equal(NpcOpeningRule.Never, guide.Opening);
        Assert.Equal(new NpcOpeningRule("shopkeeper.ask", OpeningWhen.First), shopkeeper.Opening);
        Assert.Equal(new NpcOpeningRule("villager.greet", OpeningWhen.Always), villager.Opening);

        // The renderer's table (docs/nlp/api.md section 6), the key words that were words.json's, and the pools' names.
        // V10: the fisher's provisional voice is the villager's at 0.9 (D-game-22), his pools his own. V11: the baker's is the Italian if_sara (V06's trial), brought
        // down to the Spanish voices' level (gain 0.32: Kokoro renders it about 10 dB louder). V12: the smith's is the Italian im_nicola (gain 0.6: about 4 dB
        // louder in the trial), the farmer's the Portuguese pm_santa (as loud as the Spanish voices).
        Assert.Equal(
            [new NpcVoiceSpec(28, "ef_dora", 1.0f), new NpcVoiceSpec(29, "em_alex", 1.0f), new NpcVoiceSpec(53, "em_santa", 1.0f), new NpcVoiceSpec(53, "em_santa", 0.9f), new NpcVoiceSpec(35, "if_sara", 1.0f, 0.32f),
                new NpcVoiceSpec(36, "im_nicola", 1.0f, 0.6f), new NpcVoiceSpec(44, "pm_santa", 1.0f)],
            Npcs.All.Select(static n => n.Voice));
        Assert.Equal(["agua", "pan"], shopkeeper.KeyWords);
        Assert.Empty(guide.KeyWords.Concat(villager.KeyWords));
        Assert.Equal(["guide", "shop", "villager", "fisher", "baker", "smith", "farmer"], Npcs.All.Select(static n => n.PoolPrefix));
        var fisher = Npcs.Find("fisher")!;
        Assert.Equal(("fisher-open", "fisher-open"), (fisher.PromptIn("idle", "idle"), fisher.PromptIn("pointing", "idle")));
        Assert.Equal(new NpcOpeningRule("fisher.greet", OpeningWhen.Always), fisher.Opening);
        Assert.Empty(fisher.KeyWords);
        var baker = Npcs.Find("baker")!;
        Assert.Equal("baker-open", baker.PromptIn("idle", "idle"));
        Assert.Equal(new NpcOpeningRule("baker.greet", OpeningWhen.Always), baker.Opening);
        var smith = Npcs.Find("smith")!;
        var farmer = Npcs.Find("farmer")!;
        Assert.Equal(("smith-open", "farmer-open"), (smith.PromptIn("idle", "idle"), farmer.PromptIn("idle", "idle")));
        Assert.Equal((new NpcOpeningRule("smith.greet", OpeningWhen.Always), new NpcOpeningRule("farmer.greet", OpeningWhen.Always)), (smith.Opening, farmer.Opening));
        Assert.Empty(smith.KeyWords.Concat(farmer.KeyWords));
    }

    [Fact]
    public void ASchemaTwoFileGivesDefaultsForWhatItLeavesOut()
    {
        var bare = NpcRoster.Parse("game/npcs/miller.json", """{ "schemaVersion": 2, "id": "miller", "label": "Molinero", "figure": "miller", "voice": { "speaker": 53, "name": " " } }""", "miller");

        Assert.Empty(bare.Prompts); // the validator says so: an NPC with no prompts
        Assert.Null(bare.ReviewPrompt);
        Assert.Equal(NpcOpeningRule.Never, bare.Opening);
        Assert.Equal(new NpcVoiceSpec(53, string.Empty, 1.0f), bare.Voice); // no name: the validator says so too
        Assert.Equal("miller", bare.PoolPrefix);
        Assert.Null(bare.PromptIn("idle", "idle"));
        Assert.True(bare.OwnsPool("miller_hello") && bare.OwnsPool("guide.lesson.miller.flour") && !bare.OwnsPool("millers_hello") && !bare.OwnsPool("guide.lesson.end"));
    }

    [Theory]
    [InlineData("\"when\": \"first\"", "\"when\": \"sometimes\"", "not always, first or never")]
    [InlineData("\"event\": \"shopkeeper.ask\",", "", "'event'")]
    [InlineData("\"speaker\": 29", "\"speaker\": -1", "must be 0 or more")]
    [InlineData("\"speed\": 1.0", "\"speed\": 0", "more than 0")]
    [InlineData("\"speaker\": 29", "\"speaker\": \"29\"", "is malformed")]
    public void AnOpeningOrAVoiceOfTheWrongShapeIsRefused(string from, string to, string problem)
    {
        var json = NpcFile("shopkeeper");
        Assert.Contains(from, json, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => NpcRoster.Parse("game/npcs/shopkeeper.json", json.Replace(from, to, StringComparison.Ordinal), "shopkeeper"));

        Assert.Contains(problem, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnNpcWhoseBehaviourDoesNotFitTheBundleIsAProblem()
    {
        // V01 (village-plan 2.8): an NPC the code did not know used to wait on the villager's prompt with no error.
        var shopkeeper = Npcs.Find("shopkeeper")!;
        var villager = Npcs.Find("villager")!;
        var guide = Npcs.Find("guide")! with { Voice = null, KeyWords = ["queso"] };
        var wrongShop = shopkeeper with
        {
            Prompts = new Dictionary<string, string>(StringComparer.Ordinal) { ["asking"] = "stall-ask", ["selling"] = "stall-offer", ["done"] = "villager-open" },
            Opening = new NpcOpeningRule("shopkeeper.hello", OpeningWhen.First),
            Voice = new NpcVoiceSpec(29, string.Empty, 1.0f),
            PoolPrefix = "villager",
        };
        var mute = villager with { Prompts = new Dictionary<string, string>(StringComparer.Ordinal) };

        var problems = GameDataValidator.Validate(TestGame.Data.WithNpcs(NpcRoster.Of([guide, wrongShop, mute])));

        Assert.Contains("game/npcs/villager.json: the NPC has no prompts (prompts: the prompt it waits on in each state of its agent).", problems);
        Assert.Contains("game/npcs/shopkeeper.json: the prompts name 'stall-ask' at 'asking', which is not a prompt of the bundle.", problems);
        Assert.Contains("game/npcs/shopkeeper.json: the prompts name 'villager-open' at 'done', which is the prompt of 'villager', not of this NPC.", problems);
        Assert.Contains("game/npcs/shopkeeper.json: the prompts name the state 'selling', which agents/shopkeeper.json does not have.", problems);
        Assert.Contains("game/npcs/shopkeeper.json: the prompts name none for the initial state 'idle', which a state with none of its own waits on.", problems);
        Assert.Contains("game/npcs/shopkeeper.json: the opening event 'shopkeeper.hello' is taken by no state of agents/shopkeeper.json (onHostEvent), so the talk would open with the prompt's text.", problems);
        Assert.Contains("game/npcs/shopkeeper.json: the voice (speaker 29) has no name.", problems);
        Assert.Contains("game/npcs/guide.json: the NPC has no voice (voice: the Kokoro speaker and name its lines are recorded with).", problems);
        Assert.Contains("game/npcs/guide.json: the key word 'queso' is not a catalogue noun.", problems);
        Assert.Contains("game/npcs/villager.json: the pool prefix 'villager' is also shopkeeper's, so its pools would speak with one voice.", problems);
    }

    [Fact]
    public void TheFocusIsThePhrasesThePromptsPractise()
    {
        Assert.Equal(["pedir-agua"], Npcs.FocusAt("stall-need"));
        Assert.Equal(["si-por-favor", "no-gracias"], Npcs.FocusAt("stall-offer"));
        Assert.Equal(["donde-esta-el-rio", "que-tal", "muy-bien"], Npcs.FocusAt("villager-open")); // PR #48: and the small talk
        Assert.Empty(Npcs.FocusAt("guide-offer"));
        Assert.Empty(Npcs.FocusAt("nowhere"));

        // V12b: the village's NPCs, each at its open prompt, from the rows whose home it is; the farmer practises the smith's two and, since V13, his apples.
        Assert.Equal(["donde-esta-la-panaderia", "donde-esta-la-herreria"], Npcs.FocusAt("fisher-open"));
        Assert.Equal(["quiero-dos-panes", "cuanto-cuesta", "hay-pan", "vendo-madera"], Npcs.FocusAt("baker-open"));
        Assert.Equal(["como-te-llamas", "que-haces"], Npcs.FocusAt("smith-open"));
        Assert.Equal(["como-te-llamas", "que-haces", "quiero-tres-manzanas-rojas", "cuanto-es-todo", "puedo-pagar-con-fruta"], Npcs.FocusAt("farmer-open"));

        Assert.Equal(
            ["aqui-tienes-cinco-bloques", "aqui-tienes-el-agua", "aqui-tienes-la-madera-y-las-monedas", "aqui-tienes-tres-frutas", "como-te-llamas", "cuanto-cuesta", "cuanto-es-todo", "donde-esta-el-rio", "donde-esta-la-herreria", "donde-esta-la-panaderia", "hay-pan", "muy-bien", "no-gracias", "pedir-agua", "puedo-pagar-con-fruta", "que-haces", "que-tal", "quiero-dos-panes", "quiero-tres-manzanas-rojas", "si-por-favor", "vendo-madera"],
            Npcs.ScaffoldPhrases.Order(StringComparer.Ordinal));
        Assert.Equal("shopkeeper", Npcs.HomeOf("pedir-agua"));
        Assert.Equal("villager", Npcs.HomeOf("donde-esta-el-rio"));
        Assert.Equal("smith", Npcs.HomeOf("como-te-llamas")); // the first of its two homes
        Assert.True(Npcs.IsHomeOf("farmer", "como-te-llamas") && Npcs.IsHomeOf("smith", "como-te-llamas"));
        Assert.False(Npcs.IsHomeOf("guide", "como-te-llamas") || Npcs.IsHomeOf("baker", "pedir-agua") || Npcs.IsHomeOf("nobody", "pedir-agua"));
        Assert.Null(Npcs.HomeOf("hola"));
        Assert.False(Npcs.HasScaffold("gracias"));
    }

    [Fact]
    public void APromptThatIsTheHomeOfPhrasesNeedsAFocusNamingOneOfThem()
    {
        // V12b: the four village NPCs shipped with no focus, so free talk at their prompts showed no card and the baker's request was credited as the stall's.
        // Each keeps the card of its hand-over (D-game-27), which the validator also asks for.
        var baker = Npcs.Find("baker")! with { Focus = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["baker-receive"] = ["aqui-tienes-cinco-bloques"] } };
        var smith = Npcs.Find("smith")! with { Focus = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["smith-open"] = ["donde-esta-el-rio"], ["smith-receive"] = ["aqui-tienes-la-madera-y-las-monedas"] } };
        var farmer = Npcs.Find("farmer")! with { Focus = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["farmer-receive"] = ["aqui-tienes-el-agua"] } }; // his prompt is the home of his apples (V13), and the market's total and payment (V15)
        var npcs = Npcs.All.Select(npc => npc.Id switch { "baker" => baker, "smith" => smith, "farmer" => farmer, _ => npc }).ToList();

        var problems = GameDataValidator.Validate(TestGame.Data.WithNpcs(NpcRoster.Of(npcs)));

        Assert.Equal(
            [
                "game/npcs/baker.json: the prompt 'baker-open' is the home of quiero-dos-panes, cuanto-cuesta, hay-pan, vendo-madera (game/phrases.json), and the focus names none of them there.",
                "game/npcs/smith.json: the prompt 'smith-open' is the home of como-te-llamas, que-haces (game/phrases.json), and the focus names none of them there.",
                "game/npcs/farmer.json: the prompt 'farmer-open' is the home of quiero-tres-manzanas-rojas, cuanto-es-todo, puedo-pagar-con-fruta (game/phrases.json), and the focus names none of them there.",
            ],
            problems);
        Assert.Empty(GameDataValidator.Validate(TestGame.Data)); // the shipped seven: the guide's prompts are no row's home, the villager names the river question and, since PR #48, the small talk
    }

    [Fact]
    public void TheReviewLinesAreThePhraseRowsOwn()
    {
        var rows = TestGame.Data.Phrases.Rows.Where(static r => r.ReviewLine is not null).ToDictionary(static r => r.Id, static r => r.ReviewLine);

        Assert.Equal(12, rows.Count);
        Assert.Equal("shop_need.1", rows["pedir-agua"]);
        Assert.Equal("guide_offer.1", rows["no-gracias"]);
        Assert.Equal("villager_greet.1", rows["donde-esta-el-rio"]);
        Assert.Equal("guide.lesson.hello.1", rows["hola"]);
        Assert.Equal("guide.lesson.hello.1", rows["que-tal"]); // PR #48: ¿qué tal? after the greeting (muy bien has its own pool, guide.review.phrase.muy_bien)
        Assert.Equal("guide.lesson.want.1", rows["quiero-madera"]); // X06: the warm-up asks the first lesson's phrases with the guide's own lines
    }

    [Theory]
    [InlineData("\"id\": \"shopkeeper\"", "\"id\": \"tendero\"", "its file's name")]
    [InlineData("\"label\": \"Tendero\",", "", "'label'")]
    [InlineData("\"figure\": \"shopkeeper\"", "\"figure\": \"\"", "'figure'")]
    [InlineData("\"schemaVersion\": 2", "\"schemaVersion\": 1", "schemaVersion 2")]
    public void AnNpcFileThatDoesNotHoldTogetherIsRefused(string from, string to, string problem)
    {
        var json = NpcFile("shopkeeper");
        Assert.Contains(from, json, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => NpcRoster.Parse("game/npcs/shopkeeper.json", json.Replace(from, to, StringComparison.Ordinal), "shopkeeper"));

        Assert.Contains(problem, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHostEventsActionsAreReadAndAnUnknownOneIsRefused()
    {
        var json = HostEvents();
        Assert.Equal(ItemId.Bottle, HostEventTable.Parse(json).Of("shopkeeper", "stall.give_water").Item);
        Assert.Equal(NpcEffectKind.None, HostEventTable.Parse(json).Of("shopkeeper", "shopkeeper.ask").Kind);

        Assert.Contains("is not one of give_item, point_at_noun, point_at_place, point_at_slot, take_item, sell", Refused(HostEvents(events => Entry(events, "stall.give_water")["action"]!["kind"] = "dance")), StringComparison.Ordinal);
        Assert.Contains("unknown item 'botella'", Refused(HostEvents(events => Entry(events, "stall.give_water")["action"]!["item"] = "botella")), StringComparison.Ordinal);
        Assert.Contains("given twice", Refused(HostEvents(events => Entry(events, "stall.give_bread")["event"] = "stall.give_water")), StringComparison.Ordinal);

        static string Refused(string text) => Assert.Throws<InvalidDataException>(() => HostEventTable.Parse(text)).Message;
    }

    [Fact]
    public void AnNpcThatDoesNotFitTheBundleIsAProblem()
    {
        var shopkeeper = Npcs.Find("shopkeeper")!;
        var miller = new NpcEntry("miller", "Molinero", null, "villager", new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
        var wrongFocus = shopkeeper with
        {
            Focus = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["villager-open"] = ["pedir-agua"], ["stall-need"] = ["que-necesitas"] },
        };
        var data = TestGame.Data.WithNpcs(NpcRoster.Of([Npcs.Find("guide")!, wrongFocus, miller]));

        var problems = GameDataValidator.Validate(data);

        Assert.Contains("game/npcs/miller.json: the NPC has no agent (agents/miller.json).", problems);
        Assert.Contains("game/npcs.json: the agent 'villager' has no NPC file.", problems);
        Assert.Contains("game/npcs/shopkeeper.json: the focus names the prompt 'villager-open', which is not one of this NPC's prompts.", problems);
        Assert.Contains("game/npcs/shopkeeper.json: the focus at 'stall-need' names 'que-necesitas', which is not a phrase the learner says.", problems);
    }

    [Fact]
    public void AHostEventOrATipOfAnUnknownNpcOrAPointerAtNoNounIsAProblem()
    {
        var json = HostEvents(events =>
        {
            Entry(events, "villager.give")["npc"] = "miller";
            Entry(events, "villager.point")["action"] = new JsonObject { ["kind"] = "point_at_noun", ["noun"] = "mar" };
        });
        var tips = TestGame.Data.Tips.Append(new TipSpec("mill", new Trigger(WorldEvents.TalkOpened, Npc: "miller"), "x", 0)).ToList();

        var problems = GameDataValidator.Validate(TestGame.Data.WithHostEvents(HostEventTable.Parse(json)).WithTips(tips));

        Assert.Contains("game/host-events.json: 'villager.give' is raised by 'miller', which is not one of the NPCs.", problems);
        Assert.Contains("game/host-events.json: villager's 'villager.point' points at 'mar', which is not a catalogue noun.", problems);
        Assert.Contains("game/tips.json: the tip 'mill' waits at the NPC 'miller', which is not one of the NPCs.", problems);
    }

    [Fact]
    public void TheShippedNpcsHaveNoProblems() =>
        Assert.DoesNotContain(GameDataValidator.Validate(TestGame.Data), static p => p.StartsWith("game/npcs", StringComparison.Ordinal) || p.StartsWith("game/host-events", StringComparison.Ordinal));
}
