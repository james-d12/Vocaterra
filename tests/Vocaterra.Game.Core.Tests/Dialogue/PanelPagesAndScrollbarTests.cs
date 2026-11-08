using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Dialogue;

/// <summary>U06: one page of lines per NPC, and the scrollbar's thumb.</summary>
public sealed class PanelPagesAndScrollbarTests
{
    private static NpcOpening Opening(string npc, string line, bool hostTurn = false) => new(npc, "prompt", [line], hostTurn, [], false);

    private static DialogueModel Model()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        return new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases, session.Locale);
    }

    [Fact]
    public void EachNpcKeepsItsOwnPageAcrossASwitch()
    {
        var dialogue = Model();
        dialogue.Open(Opening("shopkeeper", "¿Qué necesitas?"), "Tendero");
        dialogue.AddHint("una pista");
        dialogue.Close();
        dialogue.Open(Opening("baker", "¡Hola!"), "Panadera");
        Assert.Equal(["¡Hola!"], dialogue.Lines.Select(static l => l.Text));
        dialogue.Close();
        dialogue.Open(Opening("shopkeeper", "¿Qué necesitas?"), "Tendero");
        Assert.Equal(["¿Qué necesitas?", "una pista"], dialogue.Lines.Select(static l => l.Text)); // the page came back, and the prompt is not added twice
        Assert.Equal(["baker", "shopkeeper"], dialogue.Pages.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void WhatBelongsToTheLastExchangeStillResetsOnASwitch()
    {
        var dialogue = Model();
        dialogue.Open(Opening("shopkeeper", "¿Qué necesitas?"), "Tendero");
        dialogue.ToggleExpanded();
        dialogue.ScrollOlder();
        dialogue.Close();
        dialogue.Open(Opening("baker", "¡Hola!"), "Panadera");
        Assert.False(dialogue.Expanded);
        Assert.Equal(0, dialogue.ScrollBack);
        Assert.Null(dialogue.Heard);
        Assert.Null(dialogue.CoachingLine);
    }

    [Fact]
    public void TheCapAppliesPerPage()
    {
        Assert.Equal(200, DialogueModel.MaxLines);
        var dialogue = Model();
        dialogue.Open(Opening("baker", "¡Hola!"), "Panadera");
        for (var i = 0; i < DialogueModel.MaxLines + 5; i++)
        {
            dialogue.AddNpcLine($"línea {i}");
        }

        Assert.Equal(DialogueModel.MaxLines, dialogue.Lines.Count);
        Assert.Equal("línea 204", dialogue.Lines[^1].Text);
        dialogue.Close();
        dialogue.Open(Opening("shopkeeper", "¿Qué necesitas?"), "Tendero");
        Assert.Single(dialogue.Lines);
        dialogue.Close();
        dialogue.Open(Opening("baker", "¡Hola!"), "Panadera");
        Assert.Equal(DialogueModel.MaxLines, dialogue.Lines.Count); // the baker's page was not touched by the shopkeeper's
    }

    [Fact]
    public void ThePromptIsAddedOnTheFirstTalkOnAHostTurnAndWhenThePageIsEmpty()
    {
        var dialogue = Model();
        dialogue.Open(Opening("baker", "uno"), "Panadera"); // the first talk
        dialogue.Close();
        dialogue.Open(Opening("baker", "dos"), "Panadera"); // not first, not a host turn, page not empty: nothing
        Assert.Equal(["uno"], dialogue.Lines.Select(static l => l.Text));
        dialogue.Close();
        dialogue.Open(Opening("baker", "tres", hostTurn: true), "Panadera"); // a host turn: every time
        Assert.Equal(["uno", "tres"], dialogue.Lines.Select(static l => l.Text));
        dialogue.Close();
        dialogue.Open(Opening("farmer", "cero"), "Granjero");
        dialogue.Close();
        dialogue.Open(Opening("baker", "cuatro"), "Panadera");
        Assert.Equal(["uno", "tres"], dialogue.Lines.Select(static l => l.Text)); // a switch back keeps the page, it does not re-add the prompt
    }

    private static PanelBlock[] Blocks(int count) => [.. Enumerable.Range(0, count).Select(static i => new PanelBlock(i, DialogueSpeaker.Guide, 1, 0))];

    [Fact]
    public void NoBarWhenEveryLineIsInView()
    {
        var blocks = Blocks(3);
        var fit = PanelFitter.Fit(blocks, 10, expanded: true, scrollBack: 0, hiddenBefore: 0);
        Assert.Null(PanelScrollbar.For(blocks, fit, 0, 100));
    }

    [Fact]
    public void TheCollapsedPanelWithHiddenLinesShowsTheThumbAtTheBottom()
    {
        var blocks = Blocks(2);
        var fit = PanelFitter.Fit(blocks, 2, expanded: false, scrollBack: 0, hiddenBefore: 6);
        var bar = PanelScrollbar.For(blocks, fit, 6, 80)!.Value;
        Assert.Equal(20, bar.ThumbHeight); // 2 of 8 rows
        Assert.Equal(60, bar.ThumbTop); // the newest lines: the thumb rests at the bottom of the track
    }

    [Fact]
    public void TheExpandedPageAtTheNewestLinesRestsAtTheBottomAndAtTheOldestAtTheTop()
    {
        var blocks = Blocks(8);
        var newest = PanelFitter.Fit(blocks, 4, expanded: true, scrollBack: 0, hiddenBefore: 0);
        var atNewest = PanelScrollbar.For(blocks, newest, 0, 80)!.Value;
        Assert.Equal(80, atNewest.ThumbTop + atNewest.ThumbHeight);
        Assert.True(atNewest.ThumbHeight is > 0 and < 80);

        var oldest = PanelFitter.Fit(blocks, 4, expanded: true, scrollBack: 7, hiddenBefore: 0);
        var atOldest = PanelScrollbar.For(blocks, oldest, 0, 80)!.Value;
        Assert.Equal(0, atOldest.ThumbTop);

        var middle = PanelFitter.Fit(blocks, 4, expanded: true, scrollBack: 3, hiddenBefore: 0);
        var inMiddle = PanelScrollbar.For(blocks, middle, 0, 80)!.Value;
        Assert.InRange(inMiddle.ThumbTop, 1, atNewest.ThumbTop - 1); // scrolling back moves it up
    }

    [Fact]
    public void TheBarShowsEvenWhenTheFitDropsTheHiddenLinesRow()
    {
        var blocks = Blocks(2);
        var fit = PanelFitter.Fit(blocks, 2, expanded: false, scrollBack: 0, hiddenBefore: 3);
        Assert.False(fit.ShowEarlier); // no room for the row
        Assert.NotNull(PanelScrollbar.For(blocks, fit, 3, 40));
    }

    [Theory]
    [InlineData("ui.dialogueOlder", 1, "1 earlier line hidden (scroll up or PageUp)")]
    [InlineData("ui.dialogueOlder", 3, "3 earlier lines hidden (scroll up or PageUp)")]
    [InlineData("ui.dialogueAbove", 1, "1 earlier line above (scroll or Up)")]
    [InlineData("ui.dialogueAbove", 2, "2 earlier lines above (scroll or Up)")]
    [InlineData("ui.dialogueBelow", 1, "1 later line below (scroll or Down)")]
    [InlineData("ui.dialogueBelow", 2, "2 later lines below (scroll or Down)")]
    public void TheHintStringsNameTheWheel(string key, int count, string expected) => Assert.Equal(expected, TestGame.Data.Locale.Plural(key, count));
}
