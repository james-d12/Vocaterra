using Vocaterra.Game.Language.Checks;
using Vocaterra.Game.Language.Words;
using Vocaterra.Game.Npc;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Game.Tests;

/// <summary>The production answers of a check, graded by the core at the row's prompt (S11): correct, incorrect, "no sé" and typed.</summary>
public sealed class CheckGraderTests
{
    private static readonly StoredTimestamp Now = new(GuideSession.DefaultStartedAt);

    private static CheckGrader Grader => new(TestGame.Bundle, TestGame.Seed);

    private static CheckItem Item(string id) => CheckBank.Load(TestGame.Bundle).Single(i => i.Id == id);

    [Fact]
    public void ACorrectTypedAnswerIsCorrectAtTheRowsPrompt()
    {
        var grade = Grader.GradeTyped(Item("say.pedir-agua"), "Quiero agua, por favor.", InputKind.Typed, Now, "hash");

        Assert.True(grade.Correct);
        Assert.True(grade.Communicated);
        Assert.Equal("request_item", grade.Intent);
        Assert.Equal(InputKind.Typed, grade.Input);
        Assert.False(grade.DontKnow);
        Assert.Null(grade.Confidence);
        Assert.Equal("stall-need", grade.Entry.Utterance!.Record.PromptId);
        Assert.Equal("Quiero agua, por favor.", grade.Transcript);
    }

    [Fact]
    public void AnAnswerOfAnotherIntentIsNotCorrect()
    {
        var grade = Grader.GradeTyped(Item("say.pedir-agua"), "¿Dónde está el río?", InputKind.Typed, Now, "hash");

        Assert.False(grade.Correct);
        Assert.NotEqual("request_item", grade.Intent);
    }

    [Fact]
    public void NoSeIsRecordedAsDontKnowAndIsNotAFailure()
    {
        var grade = Grader.GradeTyped(Item("say.pedir-agua"), "no sé", InputKind.Button, Now, "hash");

        Assert.True(grade.DontKnow);
        Assert.Equal("dont_know", grade.Intent);
        Assert.False(grade.Correct);
        Assert.Equal(InputKind.Button, grade.Input);
    }

    [Fact]
    public void ConfirmAndDenyAtTheStallAreCorrectWhenTyped()
    {
        Assert.True(Grader.GradeTyped(Item("say.si-por-favor"), "Sí, por favor.", InputKind.Typed, Now, "hash").Correct);
        Assert.True(Grader.GradeTyped(Item("say.no-gracias"), "No, gracias.", InputKind.Typed, Now, "hash").Correct);
    }

    [Fact]
    public void AskingForTheRiverIsCorrectAtTheVillagersPrompt()
    {
        var grade = Grader.GradeTyped(Item("say.donde-esta-el-rio"), "¿Dónde está el río?", InputKind.Typed, Now, "hash");

        Assert.True(grade.Correct);
        Assert.Equal("ask_directions", grade.Intent);
        Assert.Equal("villager-open", grade.Entry.Utterance!.Record.PromptId);
    }

    [Fact]
    public void ErrorsAreRecordedAndTheRuleIsCommunicatedWithTheRowsIntent()
    {
        // The learner error the sheet lists for pedir-agua (conj.person): recorded, and the rule is the intent, not the error count.
        var grade = Grader.GradeTyped(Item("say.pedir-agua"), "yo quieres agua, por favor", InputKind.Typed, Now, "hash");

        Assert.Contains("conj.person", grade.ErrorTags);
        Assert.Equal(grade.Communicated && grade.Intent == "request_item", grade.Correct);
    }

    [Fact]
    public void TheRuleIsCommunicatedAndTheRowsIntentOnly()
    {
        Assert.True(CheckGrader.IsCorrect(communicated: true, intent: "request_item", expected: "request_item"));
        Assert.False(CheckGrader.IsCorrect(communicated: false, intent: "request_item", expected: "request_item"));
        Assert.False(CheckGrader.IsCorrect(communicated: true, intent: "dont_know", expected: "dont_know"));
        Assert.False(CheckGrader.IsCorrect(communicated: true, intent: "greet", expected: "request_item"));
    }

    [Fact]
    public void AComprehensionItemIsNotGradedByTheCore()
    {
        var comprehension = Item("hear.pedir-agua");

        Assert.Throws<ArgumentException>(() => Grader.GradeTyped(comprehension, "x", InputKind.Typed, Now, "hash"));
    }

    [Fact]
    public void TheThrowawayContextHasNoLearnerEvidenceAndNoHostFacts()
    {
        var grade = Grader.GradeTyped(Item("say.pedir-agua"), "Quiero agua, por favor.", InputKind.Typed, Now, "hash");
        var context = grade.Entry.Utterance!.Context;

        Assert.Empty(context.Learner.Skills);
        Assert.Equal("hash", context.HostStateHash);
        Assert.Equal(TestGame.Seed, context.WorldSeed);
    }
}
