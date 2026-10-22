using Vocaterra.Game;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The pre-rendered NPC voices (S9): which lines are speakable, who speaks them, the text-to-line index and the committed speech folder (every
/// file there, two per line, readable as mono WAV, none missing).
/// </summary>
public sealed class SpeechLinesTests
{
    private static readonly Lazy<IReadOnlyList<SpeechLine>> LoadedLines = new(() =>
        SpeechLines.Enumerate(TestGame.Bundle, TestGame.Nouns, PhraseSheet.Load(TestGame.Bundle)));

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
        Assert.Equal(SpeechSpeakers.Shopkeeper, Line("shop_need.1").Speaker);
        Assert.Equal(SpeechSpeakers.Villager, Line("villager_hello.1").Speaker);
        Assert.Equal(SpeechSpeakers.Guide, Line("guide_greet.1").Speaker);
        Assert.Equal(SpeechSpeakers.Shopkeeper, Line("prompt.stall-need").Speaker);
        Assert.Equal(SpeechSpeakers.Villager, Line("prompt.villager-open").Speaker);
        Assert.Equal("¿Qué necesitas?", Line("prompt.stall-need").Text);
        Assert.Equal("Quiero agua, por favor.", Line("phrase.pedir-agua").Text);
    }

    [Fact]
    public void TheWordCardsAreTheNounsThatComeInAnItem()
    {
        var cards = Lines.Where(static l => l.Kind == SpeechKind.Word).ToDictionary(static l => l.LineId, static l => l.Text, StringComparer.Ordinal);
        Assert.Equal(
            ["card.agua", "card.antorcha", "card.botella", "card.fruta", "card.madera", "card.pan"],
            cards.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("la madera", cards["card.madera"]);
        Assert.Equal("la fruta", cards["card.fruta"]);
        Assert.Equal("la antorcha", cards["card.antorcha"]);
        Assert.Equal("la botella", cards["card.botella"]);
        Assert.Equal("el agua", cards["card.agua"]);
        Assert.Equal("el pan", cards["card.pan"]);
        Assert.All(cards.Values, static text => Assert.Equal(SpeechSpeakers.Guide, Lines.Single(l => l.Text == text && l.Kind == SpeechKind.Word).Speaker));
    }

    [Fact]
    public void TheModelAnswersAreTheLearnerPhrasesAndNoHearsRow()
    {
        var sheet = PhraseSheet.Load(TestGame.Bundle);
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

        Assert.Equal("villager_hello.1", index.Find(SpeechSpeakers.Villager, "Hola.")!.LineId);
        Assert.Equal("phrase.hola", index.Find(SpeechSpeakers.Guide, "Hola.")!.LineId);
        Assert.Equal("shop_need.1", index.Find(SpeechSpeakers.Shopkeeper, "¿Qué necesitas?")!.LineId);
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
        var store = new StaticLineStore(SpeechDirectory);
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
}
