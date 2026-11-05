namespace Vocaterra.Game.Core.Scripted;

/// <summary>The kinds of step of the scripted run: the body's steps and the talk steps.</summary>
public enum ScriptStepKind
{
    /// <summary>A step of the body (<see cref="ScriptStep.Step"/>): look, walk, interact, place, select, craft.</summary>
    Move,

    /// <summary>Walk up to the NPC named in <see cref="ScriptStep.Value"/>, stop at the stand-off distance facing it and open the dialogue.</summary>
    Talk,

    /// <summary>Type and send the line in <see cref="ScriptStep.Value"/> to the NPC the dialogue is open on.</summary>
    Say,

    /// <summary>Synthesize the line in <see cref="ScriptStep.Value"/> with Kokoro and say it to the NPC the dialogue is open on as if spoken (S7).</summary>
    Speak,

    /// <summary>Say the WAV file in <see cref="ScriptStep.Value"/> to the NPC the dialogue is open on as if spoken (S7).</summary>
    Wav,

    /// <summary>Close the dialogue.</summary>
    Leave,

    /// <summary>Save the screen to the file in <see cref="ScriptStep.Value"/> and go on.</summary>
    Shot,

    /// <summary>Answer the check's comprehension item with the option (1 to 3, as shown) in <see cref="ScriptStep.Value"/> (S11).</summary>
    Pick,

    /// <summary>Open the report panel, type the text in <see cref="ScriptStep.Value"/> and save the report; the script waits until it is saved (S21).</summary>
    Report,

    /// <summary>Open the report panel, type the text in <see cref="ScriptStep.Value"/> and leave it open for the screenshot; the script goes on (S21).</summary>
    ReportOpen,

    /// <summary>Let the game run for the seconds in <see cref="ScriptStep.Value"/> (S8).</summary>
    Wait,

    /// <summary>Move the stored time on by the seconds in <see cref="ScriptStep.Value"/> without stepping the world (S8).</summary>
    Skip,

    /// <summary>Start the lesson whose id is in <see cref="ScriptStep.Value"/> at the NPC it is attached to: walk up, open the dialogue and begin it (S16).</summary>
    LessonStart,

    /// <summary>The lesson strip's Continue (S16).</summary>
    LessonContinue,

    /// <summary>Skip the lesson step in progress (S16).</summary>
    LessonSkip,

    /// <summary>The lesson step's help path (S16).</summary>
    LessonHelp,

    /// <summary>Pick the option (1 to 4) in <see cref="ScriptStep.Value"/> of a listen step (S16).</summary>
    LessonPick,

    /// <summary>Opens the book on the page of the target in <see cref="ScriptStep.Value"/> (a noun, or <c>phrase:&lt;id&gt;</c>) and plays its recording, as Enter does (S20b).</summary>
    BookPlay,

    /// <summary>As <see cref="BookPlay"/>, the slow recording, as Shift+Enter does (S20b).</summary>
    BookPlaySlow,

    /// <summary>Opens the book on its Lessons page and presses "Practise again" on the lesson whose id is in <see cref="ScriptStep.Value"/> (S26).</summary>
    BookPractise,

    /// <summary>
    /// Run the whole lesson whose id is in <see cref="ScriptStep.Value"/> the way a learner who gets everything right would (S17): walk up to its NPC, then for each
    /// step continue, pick the right option, type the phrase, or (a <c>do</c> step) close the panel, walk to the beacon and pick the thing up and walk back. Every step
    /// and its outcome is printed by the game's event lines.
    /// </summary>
    LessonRun,

    /// <summary>
    /// Sleep at the player's own fire as if the player had chosen to (V12c), wherever they stand (a script needs no walk): the panels close first, and the next play session
    /// begins as a load of the save would begin it. Refused, with a line, when the player may not sleep (the fire not lit, a lesson step waiting, a playtest).
    /// </summary>
    Sleep,

    /// <summary>
    /// Eat or drink the item named in <see cref="ScriptStep.Value"/> (<c>--eat pan</c>, N02): one is given if the bag has none (a test aid, like <c>--set-need</c>), then it is used as a right
    /// click with it selected uses it. The result is the game's event lines (<c>meal_eaten</c>, <c>water_drunk</c>) and the meters.
    /// </summary>
    Eat,

    /// <summary>A drink by hand at the water the crosshair is on, within reach (<c>--drink</c>, N02): the walk and the look come first. A line says when there is no water to drink.</summary>
    Drink,
}
