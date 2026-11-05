using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Language.Lessons;

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

    /// <summary>The page of the strip's English now (X08): a step's English longer than the strip has room for goes over pages, and the More button (M) turns them; a new step starts at the first.</summary>
    public int EnglishPage { get; private set; }

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
        PumpDeck();
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
        EnglishPage = 0;
        if (session.Lessons.Current is { Line: { } line })
        {
            dialogue.AddNpcLine(line.Text);
        }
    }

    // The review deck's part of a frame (X06): the guide's request for the next item of the warm-up goes on the panel, and a note (the warm-up is done) goes to the game.
    private void PumpDeck()
    {
        foreach (var line in session.Deck.TakeLines())
        {
            if (dialogue.IsOpen)
            {
                dialogue.AddNpcLine(line);
            }
        }

        if (dialogue.IsOpen)
        {
            foreach (var line in session.Lessons.TakeLines())
            {
                dialogue.AddNpcLine(line); // a teacher's offer of a class (L01): kept until the panel is open
            }
        }

        foreach (var note in session.Deck.TakeNotes())
        {
            log($"warm-up: {note}");
            OnNote?.Invoke(note);
        }
    }

    /// <summary>Called with an English note the game shows as a toast (the warm-up is done, X06). <see langword="null"/> for none (the tests').</summary>
    public Action<string>? OnNote { get; set; }

    /// <summary>
    /// The lesson strip's state now: the step in progress, or the lesson on offer at the NPC the panel is open on. The review deck's warm-up (X06) shows in the same
    /// strip: its offer in place of a lesson's, and its item in progress in place of a step.
    /// </summary>
    /// <returns>The step and the offer; at most one of them is set.</returns>
    public (LessonView? View, LessonOffer? Offer) State()
    {
        if (!dialogue.IsOpen)
        {
            return (null, null);
        }

        var view = AtLessonsNpc ? session.Lessons.View : null;
        if (view is not null)
        {
            return (view, null);
        }

        if (session.Deck.ViewFor(dialogue.Npc) is { } warmup)
        {
            return (warmup, null);
        }

        return (null, session.Deck.OfferFor(dialogue.Npc) ?? session.Lessons.OfferFor(dialogue.Npc));
    }

    // The panel is open on the NPC the engaged lesson is attached to: a lesson that stays engaged (autoStart) is not drawn over another NPC's panel.
    // S26: a lesson may move from one NPC to another, so it is the step's NPC that counts.
    private bool AtLessonsNpc => session.Lessons.CurrentNpc is { } npc && string.Equals(npc, dialogue.Npc, StringComparison.Ordinal);

    /// <summary>Whether a lesson key means something now (a lesson is engaged or on offer, and the text box is not typing): the game then does not give the key to the panel.</summary>
    /// <param name="key">The key.</param>
    public bool Handles(LessonKey key)
    {
        if (!dialogue.IsOpen || dialogue.IsTyping)
        {
            return false;
        }

        var (view, offer) = State();
        return LessonActions.ForKey(key) switch
        {
            LessonAction.Start => offer is not null,
            LessonAction.Continue => view is { CanContinue: true },
            LessonAction.Skip => view is not null || offer is { Warmup: true },
            LessonAction.More => view is not null,
            LessonAction.Option1 or LessonAction.Option2 or LessonAction.Option3 or LessonAction.Option4 => view is { Type: LessonStepType.Listen, CanContinue: false },
            _ => false,
        };
    }

    /// <summary>A key of the strip was pressed. Returns whether it did something.</summary>
    /// <param name="key">The key.</param>
    public bool PressKey(LessonKey key) => Handles(key) && LessonActions.ForKey(key) is { } action && Perform(action);

    /// <summary>Carries out a press of the strip (key or mouse). Returns whether it did something.</summary>
    /// <param name="action">What was pressed.</param>
    public bool Perform(LessonAction action)
    {
        if (!dialogue.IsOpen)
        {
            return false;
        }

        var runner = session.Lessons;
        var (state, shown) = State();
        var warmup = shown is { Warmup: true } ? shown : null;
        var running = state is not null && string.Equals(state.LessonId, ReviewDeck.StripId, StringComparison.Ordinal);
        bool done;
        switch (action)
        {
            case LessonAction.Start when warmup is not null:
                done = session.Deck.Accept(); // the offered warm-up begins (X06)
                break;
            case LessonAction.Skip when warmup is not null:
                done = session.Deck.SkipAll();
                break;
            case LessonAction.Skip when running:
                done = session.Deck.SkipItem();
                break;
            case LessonAction.Help when running:
                _ = session.SubmitHelp(null); // the panel's own Ayuda: the card goes up and the next line is the answer
                dialogue.BeginLine(session.Data.Words.Buttons.Help);
                done = true;
                break;
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
            case LessonAction.More:
                EnglishPage++; // the strip wraps the page round
                return state is not null;
            default:
                done = LessonActions.OptionIndex(action) is { } index && runner.Pick(index);
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
