using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The front-end's part of the lessons (S16), as plain code the game calls once per frame and for each key or click, and the tests call the same way: it puts the
/// NPC's line of a new lesson step on the panel (its pre-rendered WAV is played by the session's cues, <see cref="SupportFrontend"/>), carries out the presses of the
/// lesson strip (Start, Continue, Skip, Help and the options of a listen step) and answers the keys for them. The rules are in <see cref="LessonRunner"/>; nothing
/// here decides what a step means.
/// </summary>
/// <param name="session">The running game.</param>
/// <param name="dialogue">The panel.</param>
/// <param name="log">Where the console lines go.</param>
public sealed class LessonFrontend(GuideSession session, DialogueModel dialogue, Action<string> log)
{
    private string? _stepKey;

    /// <summary>
    /// Called when the recap step ends and the lesson is complete (S17): the game closes the panel and opens the journal on its phrases page, where the phrases the
    /// lesson taught are listed with the help each has now and its counts. <see langword="null"/> for none (the tests').
    /// </summary>
    public Action? OnRecapDone { get; set; }

    /// <summary>
    /// One frame: when a step began since the last frame and it has a line (a model, a listen or an answer step), the line is added to the panel as the NPC's, so it
    /// is shown, glossed in English, and can be repeated like any other of the NPC's lines. A step the panel was closed for is shown when the panel opens again.
    /// </summary>
    public void Pump()
    {
        if (!dialogue.IsOpen || !AtLessonsNpc || session.Lessons.View is not { } view)
        {
            if (session.Lessons.View is null)
            {
                _stepKey = null;
            }

            return;
        }

        var key = $"{view.LessonId}:{view.StepNumber}";
        if (string.Equals(key, _stepKey, StringComparison.Ordinal))
        {
            return;
        }

        _stepKey = key;
        if (session.Lessons.Current is { Line: { } line })
        {
            dialogue.AddNpcLine(line.Text);
        }
    }

    /// <summary>The lesson strip's state now: the step in progress, or the lesson on offer at the NPC the panel is open on.</summary>
    /// <returns>The step and the offer; at most one of them is set.</returns>
    public (LessonView? View, LessonOffer? Offer) State()
    {
        if (!dialogue.IsOpen)
        {
            return (null, null);
        }

        var view = AtLessonsNpc ? session.Lessons.View : null;
        return view is not null ? (view, null) : (null, session.Lessons.OfferFor(dialogue.Npc));
    }

    // The panel is open on the NPC the engaged lesson is attached to: a lesson that stays engaged (autoStart) is not drawn over another NPC's panel.
    private bool AtLessonsNpc => session.Lessons.Active is { } lesson && string.Equals(lesson.Npc, dialogue.Npc, StringComparison.Ordinal);

    /// <summary>Whether a lesson key means something now (a lesson is engaged or on offer, and the text box is not typing): the game then does not give the key to the panel.</summary>
    /// <param name="key">The key.</param>
    public bool Handles(LessonKey key)
    {
        if (!dialogue.IsOpen || dialogue.IsTyping)
        {
            return false;
        }

        var (view, offer) = State();
        return LessonStripLayout.ForKey(key) switch
        {
            LessonAction.Start => offer is not null,
            LessonAction.Continue => view is { CanContinue: true },
            LessonAction.Skip => view is not null,
            LessonAction.Option1 or LessonAction.Option2 or LessonAction.Option3 or LessonAction.Option4 => view is { Type: LessonStepType.Listen, CanContinue: false },
            _ => false,
        };
    }

    /// <summary>A key of the strip was pressed. Returns whether it did something.</summary>
    /// <param name="key">The key.</param>
    public bool PressKey(LessonKey key) => Handles(key) && LessonStripLayout.ForKey(key) is { } action && Perform(action);

    /// <summary>Carries out a press of the strip (key or mouse). Returns whether it did something.</summary>
    /// <param name="action">What was pressed.</param>
    public bool Perform(LessonAction action)
    {
        if (!dialogue.IsOpen)
        {
            return false;
        }

        var runner = session.Lessons;
        bool done;
        switch (action)
        {
            case LessonAction.Start:
                done = runner.OfferFor(dialogue.Npc) is { } offer && runner.Start(offer.LessonId);
                break;
            case LessonAction.Continue:
                var lessonId = runner.Active?.Id;
                var recap = runner.Current is { Type: LessonStepType.Recap };
                done = runner.Continue();
                if (done && recap && lessonId is not null && runner.StatusOf(lessonId) == LessonStatus.Completed)
                {
                    log($"lesson: the recap is done; the journal opens on its phrases page");
                    OnRecapDone?.Invoke();
                }

                break;
            case LessonAction.Skip:
                done = runner.Skip();
                break;
            case LessonAction.Help:
                session.LogVoice(VoiceEventKind.Hint);
                done = runner.Help();
                break;
            default:
                done = LessonStripLayout.OptionIndex(action) is { } index && runner.Pick(index);
                break;
        }

        if (done)
        {
            Pump();
            log($"lesson: {action} -> {(runner.View is { } view ? $"step {view.StepNumber} of {view.StepCount}, {view.Heading}" : "no step in progress")}");
        }

        return done;
    }
}
