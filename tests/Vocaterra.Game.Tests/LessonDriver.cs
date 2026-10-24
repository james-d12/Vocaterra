using System.Globalization;
using Vocaterra.Game.Language.Lessons;
using Vocaterra.Game.Npc;

namespace Vocaterra.Game.Tests;

/// <summary>
/// Drives the shipped lesson the way a learner who does everything right would, step by step, for the tests of the whole lesson (S17): the runner's own calls
/// (<see cref="LessonRunner.Continue"/>, <see cref="LessonRunner.Pick"/>), a typed line for a repeat or answer step, and the world event a <c>do</c> step waits for.
/// </summary>
internal static class LessonDriver
{
    /// <summary>One step as the driver did it: the step's id and type, the line the learner sent (if any) and how the step ended.</summary>
    /// <param name="Id">The step's id.</param>
    /// <param name="Type">The wire name of its type.</param>
    /// <param name="Sent">What the learner typed, or <see langword="null"/>.</param>
    /// <param name="Outcome">How it ended.</param>
    /// <param name="Shown">The lines the guide showed for the turn (her reply, any repeat), in order.</param>
    public sealed record Done(string Id, string Type, string? Sent, StepOutcome Outcome, IReadOnlyList<string> Shown, GuideTurn? Turn = null);

    /// <summary>The line the learner types for a step: the phrase's Spanish for a repeat or answer step.</summary>
    /// <param name="session">The session.</param>
    /// <param name="step">The step.</param>
    public static string LineFor(VoxelSession session, LessonStep step) =>
        session.Phrases.Find(step.Phrase!)!.Spanish.TrimEnd('.');

    /// <summary>Does the step in progress the way a learner who gets it right would, and returns what happened.</summary>
    /// <param name="session">The session, with the panel open on the guide and the lesson engaged.</param>
    public static Done DoStep(VoxelSession session)
    {
        var runner = session.Lessons;
        var step = runner.Current ?? throw new InvalidOperationException("No lesson step is in progress.");
        var lessonId = runner.Active!.Id;
        var before = runner.OutcomesOf(lessonId).Count;
        string? sent = null;
        IReadOnlyList<string> shown = [];
        GuideTurn? turn = null;
        switch (step.Type)
        {
            case LessonStepType.Listen:
                Assert.True(runner.Pick(step.Correct));
                Assert.True(runner.Continue());
                break;
            case LessonStepType.Repeat:
            case LessonStepType.Answer:
                sent = LineFor(session, step);
                turn = session.Say(sent);
                shown = turn.GuideLines;
                break;
            case LessonStepType.Do:
                session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, step.Event!, null, step.Detail));
                break;
            default:
                Assert.True(runner.Continue());
                break;
        }

        var outcomes = runner.OutcomesOf(lessonId);
        Assert.Equal(before + 1, outcomes.Count);
        return new Done(step.Id, LessonStepTypes.Of(step.Type), sent, outcomes[^1].Outcome, shown, turn);
    }

    /// <summary>Does every step until the lesson is completed (or a limit is reached) and returns each step as it went.</summary>
    /// <param name="session">The session, with the panel open on the guide.</param>
    /// <param name="lessonId">The lesson to run.</param>
    public static IReadOnlyList<Done> CompleteAll(VoxelSession session, string lessonId = "first-words")
    {
        if (session.Lessons.StatusOf(lessonId) != LessonStatus.InProgress)
        {
            Assert.True(session.Lessons.Start(lessonId));
        }

        var steps = new List<Done>();
        for (var guard = 0; session.Lessons.StatusOf(lessonId) == LessonStatus.InProgress && guard < 200; guard++)
        {
            steps.Add(DoStep(session));
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(lessonId));
        Assert.True(steps.Count < 200, string.Create(CultureInfo.InvariantCulture, $"the lesson did not end after {steps.Count} steps"));
        return steps;
    }
}
