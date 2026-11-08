using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Language.Speech;

/// <summary>
/// The pre-rendered NPC voices (S9): which lines are speakable, who speaks them, the text-to-line index and the committed speech folder (every
/// file there, two per line, readable as mono WAV, none missing).
/// </summary>
public sealed class SpeechLinesTests
{
    private static readonly Lazy<IReadOnlyList<SpeechLine>> LoadedLines = new(() =>
        SpeechLines.Enumerate(TestGame.Bundle, TestGame.Nouns, RouteScene.DataOf(TestGame.Bundle).Phrases, TestGame.Data.Npcs));

    private static IReadOnlyList<SpeechLine> Lines => LoadedLines.Value;

    private static string SpeechDirectory => Path.Combine(GameBundle.ResolveDirectory(), SpeechLines.FolderName);

    private static SpeechLine Line(string id) => Lines.Single(l => string.Equals(l.LineId, id, StringComparison.Ordinal));

    [Fact]
    public void EveryLineIdIsAPlainFileNameAndNoIdIsUsedTwice()
    {
        Assert.True(Lines.Count > 0);
        Assert.All(Lines, static l => Assert.True(SpeechFiles.IsPlainId(l.LineId), l.LineId));
        Assert.Equal(Lines.Count, Lines.Select(static l => l.LineId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void OnlyTemplatesWithoutSlotsAreSpeakable()
    {
        // The guide's world lines ({place}, {count}, {direction}...) have no voice: none of their rendered forms is in the list.
        Assert.All(Lines, static l => Assert.DoesNotContain('{', l.Text));
        Assert.DoesNotContain(Lines, static l => l.LineId.StartsWith("guide.point.found", StringComparison.Ordinal));
        Assert.DoesNotContain(Lines, static l => l.LineId.StartsWith("guide.craft.recipe", StringComparison.Ordinal));
        Assert.DoesNotContain(Lines, static l => l.LineId.StartsWith("feedback.recast", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSpeakerIsTheAgentOfTheLine()
    {
        Assert.Equal(NpcService.ShopkeeperAgent, Line("shop_need.1").Speaker);
        Assert.Equal(NpcService.VillagerAgent, Line("villager_hello.1").Speaker);
        Assert.Equal(SpeechSpeakers.Guide, Line("guide_greet.1").Speaker);
        Assert.Equal(NpcService.ShopkeeperAgent, Line("prompt.stall-need").Speaker);
        Assert.Equal(NpcService.VillagerAgent, Line("prompt.villager-open").Speaker);
        Assert.Equal("¿Qué necesitas?", Line("prompt.stall-need").Text);
        Assert.Equal("Quiero agua, por favor.", Line("phrase.pedir-agua").Text);
    }

    [Fact]
    public void EveryPoolOfTheShippedBundleKeepsTheSpeakerItHadBeforeV01()
    {
        // The rule V01 replaced, as it was in SpeechSpeakers.OfPool: the speaker of a pool now comes from the NPC files (<id>_* and guide.lesson.<id>.*, the
        // shopkeeper's pools by his poolPrefix "shop"), and no pool of the shipped bundle may change voice: the recordings stay as they are.
        // The fisher's pools (village-plan V10), the baker's (V11), the smith's and the farmer's (V12) and the farmer's lesson lines (V13) are new: theirs by the same rule.
        static string Before(string pool) =>
            pool.StartsWith("guide.lesson.villager.", StringComparison.Ordinal) || pool.StartsWith("villager_", StringComparison.Ordinal) ? "villager"
            : pool.StartsWith("guide.lesson.shop.", StringComparison.Ordinal) || pool.StartsWith("shop_", StringComparison.Ordinal) ? "shopkeeper"
            : pool.StartsWith("guide.lesson.fisher.", StringComparison.Ordinal) || pool.StartsWith("fisher_", StringComparison.Ordinal) ? "fisher"
            : pool.StartsWith("guide.lesson.baker.", StringComparison.Ordinal) || pool.StartsWith("baker_", StringComparison.Ordinal) ? "baker"
            : pool.StartsWith("guide.lesson.smith.", StringComparison.Ordinal) || pool.StartsWith("smith_", StringComparison.Ordinal) ? "smith"
            : pool.StartsWith("guide.lesson.farmer.", StringComparison.Ordinal) || pool.StartsWith("farmer_", StringComparison.Ordinal) ? "farmer"
            : "guide";

        var pools = TestGame.Bundle.Dialogue.Pools.Keys.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(337, pools.Count); // W10 added Luz's thirteen (the nine instructions, «Todavía no.», two lesson questions and the plan line) and the stall's eight (flour sold, bread bought, the misses; the stall's first seven wait for the bread and the flour); W08 added the baker's seven (the offer to bake, her «¡Vamos!», «Escucha.», «Te ayudo.», the ingredients, «Ya tienes todo.», «Hoy no tengo más.»); N05 added the three gift_used pools (the stall, the villager, the guide); L01 added five class-offer pools (guide.lesson.<npc>.offer) and the player's «No tengo sueño.» (guide.sleep.not_sleepy); // V29 added four pools to each of the seven NPCs (_yes, _weather, _sun, _well); V28 added «Sí, es verdad.» for the baker, the smith and the fisher; V27 added the NPCs' off-prompt pools, one each; V21b added farmer_unit; V02 added villager_unknown_place; V10 the fisher's 21 and the villager's signpost line; V11 the baker's 20 and the stall's 3; V12 the smith's 10, the farmer's 7 and the villager's 10; V13 the farmer's 15 and the stall's 2; V14 the villager's 8, the smith's 2 and the fisher's 3; V17 the baker's 2, the smith's 2 and the fisher's 2; V15 the baker's 10, the farmer's 10, the smith's 3 and the stall's 1; V19 the baker's, the farmer's and the smith's no_room and none_offered; V20 the free-talk replies, the villager's 2, the stall's 2, the fisher's, the baker's, the smith's 2 and the farmer's 2; V21 the stall's and the villager's wont_sell and the baker's wait
        Assert.All(pools, pool => Assert.Equal((pool, Before(pool)), (pool, SpeechSpeakers.OfPool(pool, TestGame.Data.Npcs))));
        Assert.Equal([("baker", 63), ("farmer", 43), ("fisher", 34), ("guide", 78), ("shopkeeper", 43), ("smith", 27), ("villager", 49)], pools.GroupBy(pool => SpeechSpeakers.OfPool(pool, TestGame.Data.Npcs)).Select(static g => (g.Key, g.Count())).OrderBy(static g => g.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void TheLinesEveryNpcSaysAreRecordedInEachNpcsOwnVoiceAndTheSpeakerWinsTheLookup()
    {
        // V21b: «¡Muy bien!» (answer_accepted), «No te entendí» (not_understood) and the core's hints played in Luna's voice for every NPC.
        // #22: the host's «No te entendí bien.» (guide.uncertain) when the recognizer was not sure, too.
        var shared = new[] { "answer_accepted.1", "answer_accepted.2", "answer_accepted.3", "not_understood.1", "not_understood.2", "feedback.hint.1", "feedback.hint.2", "feedback.hint.gender.agree.1", "feedback.slow_repeat.1", "feedback.slow_repeat.2", "guide.uncertain.1", "guide.uncertain.2" };
        var npcs = TestGame.Data.Npcs.All.Select(static n => n.Id).Where(static id => id != SpeechSpeakers.Guide).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(6, npcs.Count);
        var copies = Lines.Where(static l => l.Kind == SpeechKind.NpcCopy && !l.LineId.StartsWith("card.", StringComparison.Ordinal)).ToList();
        Assert.Equal(npcs.Count * shared.Length, copies.Count);
        foreach (var id in shared)
        {
            foreach (var npc in npcs)
            {
                var copy = Line($"{id}.{npc}");
                Assert.Equal((npc, SpeechKind.NpcCopy, Line(id).Text), (copy.Speaker, copy.Kind, copy.Text));
            }
        }

        var index = new SpeechIndex(Lines);
        Assert.Equal("answer_accepted.1.baker", index.Find("baker", "¡Muy bien!")!.LineId);
        Assert.Equal("answer_accepted.1", index.Find(SpeechSpeakers.Guide, "¡Muy bien!")!.LineId);
        Assert.Equal("not_understood.1.shopkeeper", index.Find("shopkeeper", Line("not_understood.1").Text)!.LineId);
        Assert.Equal("guide.uncertain.2.baker", index.Find("baker", "No te entendí bien. ¿Me lo dices otra vez?")!.LineId); // #22: the baker's own voice
        Assert.Equal("guide.uncertain.2", index.Find(SpeechSpeakers.Guide, "No te entendí bien. ¿Me lo dices otra vez?")!.LineId);
    }

    [Fact]
    public void TheKeyWordsAnNpcTeachesAreRecordedInItsOwnVoice()
    {
        // #36: the shopkeeper's key words («el agua», «el pan») are said before his question, by him: copies of their word cards in his folder, and the lookup finds
        // his copy for him and the guide's card for her.
        var keyWords = Lines.Where(static l => l.Kind == SpeechKind.NpcCopy && l.LineId.StartsWith("card.", StringComparison.Ordinal)).ToList();
        var expected = TestGame.Data.Npcs.All.Where(static n => n.Id != SpeechSpeakers.Guide)
            .SelectMany(static n => n.KeyWords.Select(lemma => (SpeechFiles.CardId(lemma) + "." + n.Id, n.Id, TestGame.Nouns.ForLemma(lemma)!.WithDefinite)))
            .Order().ToList();
        Assert.Equal(expected, keyWords.Select(static l => (l.LineId, l.Speaker, l.Text)).Order().ToList());
        Assert.Contains(("card.agua.shopkeeper", NpcService.ShopkeeperAgent, "el agua"), expected);
        Assert.Equal("shopkeeper/card.agua.shopkeeper.wav", SpeechFiles.RelativePath(Line("card.agua.shopkeeper"), slow: false));

        var index = new SpeechIndex(Lines);
        Assert.Equal("card.agua.shopkeeper", index.Find(NpcService.ShopkeeperAgent, "el agua")!.LineId);
        Assert.Equal("card.pan.shopkeeper", index.Find(NpcService.ShopkeeperAgent, "el pan")!.LineId);
        Assert.Equal("card.agua", index.Find(SpeechSpeakers.Guide, "el agua")!.LineId);
    }

    [Fact]
    public void TheWordCardsAreEveryCatalogueNounS20b()
    {
        // Every catalogue noun has a card (S20b: the book's Nouns page plays it); the file name folds the accent (card.rio for río).
        var cards = Lines.Where(static l => l.Kind == SpeechKind.Word).ToDictionary(static l => l.LineId, static l => l.Text, StringComparer.Ordinal);
        Assert.Equal(
            TestGame.Nouns.Nouns.Select(static n => SpeechFiles.CardId(n.Lemma)).Order(StringComparer.Ordinal),
            cards.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(29, cards.Count); // W10 harina, bol, cuchara, masa, horno, mano; V03 added moneda, manzana and hacha, N07 fuego
        Assert.Equal("el hacha", cards["card.hacha"]); // a feminine noun with a stressed a takes el
        Assert.Equal("el río", cards["card.rio"]);
        Assert.Equal("el árbol", cards["card.arbol"]);
        Assert.Equal("la madera", cards["card.madera"]);
        Assert.Equal("la fruta", cards["card.fruta"]);
        Assert.Equal("la antorcha", cards["card.antorcha"]);
        Assert.Equal("la botella", cards["card.botella"]);
        Assert.Equal("el agua", cards["card.agua"]);
        Assert.Equal("el pan", cards["card.pan"]);
        Assert.Equal("el libro", cards["card.libro"]); // the learner's book (S20)
        Assert.All(cards.Values, static text => Assert.Equal(SpeechSpeakers.Guide, Lines.Single(l => l.Text == text && l.Kind == SpeechKind.Word).Speaker));
    }

    [Fact]
    public void TheModelAnswersAreTheLearnerPhrasesAndNoHearsRow()
    {
        var sheet = RouteScene.DataOf(TestGame.Bundle).Phrases;
        var learner = sheet.Rows.Where(static r => r.IsLearnerPhrase).ToList();
        var phrases = Lines.Where(static l => l.Kind == SpeechKind.Phrase).ToList();
        Assert.Equal(learner.Count, phrases.Count);
        Assert.All(learner, row => Assert.Contains(phrases, p => p.LineId == "phrase." + row.Id && p.Text == row.Spanish));
        Assert.DoesNotContain(phrases, static p => p.LineId == "phrase.que-necesitas");
    }

    [Fact]
    public void TheIndexFindsTheSpeakersOwnLine()
    {
        var index = new SpeechIndex(Lines);

        Assert.Equal("villager_hello.1", index.Find(NpcService.VillagerAgent, "Hola.")!.LineId);
        Assert.Equal("phrase.hola", index.Find(SpeechSpeakers.Guide, "Hola.")!.LineId);
        Assert.Equal("shop_need.1", index.Find(NpcService.ShopkeeperAgent, "¿Qué necesitas?")!.LineId);
        Assert.Null(index.Find(SpeechSpeakers.Guide, "El río está allí, a la izquierda, a sesenta bloques."));
    }

    [Fact]
    public void TheCommittedSpeechFolderHasEveryFileAndNoneIsMissing()
    {
        var summary = SpeechSummary.Of(Lines, SpeechDirectory);

        Assert.Equal(Lines.Count, summary.Lines);
        Assert.Equal(2 * Lines.Count, summary.Files);
        Assert.Equal(2 * PendingSpeech.Ids.Count, summary.Missing); // exactly the lines S17 phase 1 added and phase 2 renders: nothing else is missing
        Assert.Equal($"speech: {Lines.Count} lines, {2 * Lines.Count} files, {2 * PendingSpeech.Ids.Count} missing", summary.ToString());
        Assert.All(PendingSpeech.Ids, id => Assert.Contains(Lines, l => string.Equals(l.LineId, id, StringComparison.Ordinal)));
    }

    [Fact]
    public void AFolderWithoutFilesMissesEveryFile()
    {
        var empty = Path.Combine(Path.GetTempPath(), "vocaterra-s9-empty-" + Guid.NewGuid().ToString("N"));
        var summary = SpeechSummary.Of(Lines, empty);

        Assert.Equal(2 * Lines.Count, summary.Missing);
    }

    [Fact]
    public void EveryCommittedFileIsAReadableMonoWavOfKokoroRate()
    {
        var store = new SpeechStore(SpeechDirectory, Lines);
        foreach (var line in Lines.Where(static l => !PendingSpeech.Contains(l.LineId)))
        {
            foreach (var id in new[] { line.LineId, SpeechFiles.SlowId(line.LineId) })
            {
                Assert.True(store.TryGet(id, out var clip), id);
                Assert.Contains(clip.SampleRate, new[] { 16000, 24000 });
                Assert.True(clip.Samples.Length > 0, id);
            }
        }
    }

    [Fact]
    public void EveryFileIsInItsSpeakersFolderOrInCardsAndNoneIsLeftLoose()
    {
        // V28: speech/<speaker>/<id>.wav (the word cards' in speech/cards/), the slow file beside its normal one, nothing in the speech folder itself.
        Assert.Empty(Directory.GetFiles(SpeechDirectory));
        foreach (var line in Lines.Where(static l => !PendingSpeech.Contains(l.LineId)))
        {
            Assert.Equal(line.Kind == SpeechKind.Word ? "cards" : line.Speaker, SpeechFiles.FolderOf(line));
            Assert.True(File.Exists(Path.Combine(SpeechDirectory, SpeechFiles.RelativePath(line, false))), SpeechFiles.RelativePath(line, false));
            Assert.True(File.Exists(Path.Combine(SpeechDirectory, SpeechFiles.RelativePath(line, true))), SpeechFiles.RelativePath(line, true));
        }

        Assert.Equal(
            ["baker", "cards", "farmer", "fisher", "guide", "shopkeeper", "smith", "villager"],
            Directory.GetDirectories(SpeechDirectory).Select(static d => Path.GetFileName(d)).Order(StringComparer.Ordinal));
        Assert.Equal(2 * Lines.Count - (2 * PendingSpeech.Ids.Count), Directory.GetFiles(SpeechDirectory, "*.wav", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public void AStoreFindsAFileByItsLineIdThroughTheLinesFolder()
    {
        var store = new SpeechStore(SpeechDirectory, Lines);
        var card = Lines.First(static l => l.Kind == SpeechKind.Word);

        Assert.True(store.Contains(card.LineId));
        Assert.True(store.Contains(SpeechFiles.SlowId(card.LineId)));
        Assert.False(store.Contains("no_such_line.1"));
        Assert.False(store.Contains(SpeechFiles.SlowId("no_such_line.1")));
    }
}
