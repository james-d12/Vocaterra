using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>The instruction strip alone (W07; the model the recipe book of W30 will show too): built from a view of the run and a row, with nothing of the station.</summary>
public sealed class InstructionStripTests
{
    private static readonly PhraseRow Row = new("pon-la-harina", "does", "Pon la harina en el bol.", "Put the flour in the bowl.", null, null, [], "card", ["x.1"]);

    private static ProcedureView View(int rung, bool english, bool text, int done = 1, ProcedureState state = ProcedureState.InRun) =>
        new(state, "pan", UnitKind.Step, "pon-la-harina", "harina", rung, english, text, null, null, done, 3, false);

    private static InstructionStrip Strip(ProcedureView view, bool audible = true) => InstructionStrip.From(view, Row, TestGame.Data.Words.Buttons, audible);

    [Fact]
    public void TheFirstRungShowsTheSpanishAndTheEnglish()
    {
        var strip = Strip(View(1, true, true));
        Assert.Equal("Pon la harina en el bol.", strip.Spanish);
        Assert.Equal("Put the flour in the bowl.", strip.English);
        Assert.False(strip.CardForced);
        Assert.Equal(1, strip.Rung);
    }

    [Fact]
    public void TheCardAndAudioRungShowsTheSpanishOnly()
    {
        var strip = Strip(View(2, false, true));
        Assert.NotNull(strip.Spanish);
        Assert.Null(strip.English);
    }

    [Fact]
    public void TheAudioOnlyRungShowsNothingWrittenUnlessTheRowCannotBeHeard()
    {
        var heard = Strip(View(3, false, false));
        Assert.Null(heard.Spanish);
        Assert.Null(heard.English);
        Assert.False(heard.CardForced);

        var muted = Strip(View(3, false, false), audible: false); // decision 19: the card shows
        Assert.Equal("Pon la harina en el bol.", muted.Spanish);
        Assert.Null(muted.English);
        Assert.True(muted.CardForced);
    }

    [Fact]
    public void HelpShowsBoth()
    {
        var strip = Strip(View(3, true, true));
        Assert.NotNull(strip.Spanish);
        Assert.NotNull(strip.English);
    }

    [Fact]
    public void ADotPerStepWithTheDoneOnesFirstAndOneCurrent()
    {
        var strip = Strip(View(1, true, true, done: 1));
        Assert.Equal([StripDotState.Done, StripDotState.Current, StripDotState.Todo], strip.Dots.Select(static d => d.State));
    }

    [Fact]
    public void TheThreeActionsAreRepetirMasDespacioAndAyudaOnRSAndH()
    {
        var strip = Strip(View(1, true, true));
        Assert.Equal(["Repetir", "Más despacio", "Ayuda"], strip.Actions.Select(static a => a.Label));
        Assert.Equal([GameKey.R, GameKey.S, GameKey.H], strip.Actions.Select(static a => a.Key));
        Assert.All(strip.Actions, static a => Assert.True(a.Enabled));
        var silent = Strip(View(1, true, true), audible: false);
        Assert.Equal([false, false, true], silent.Actions.Select(static a => a.Enabled)); // help needs no sound
    }

    [Fact]
    public void ADoneBakeAndNoRunTeachNothing()
    {
        var done = Strip(View(4, false, false, done: 3, state: ProcedureState.Done));
        Assert.Null(done.Row);
        Assert.Null(done.Spanish);
        Assert.All(done.Dots, static d => Assert.Equal(StripDotState.Done, d.State));
        Assert.All(done.Actions, static a => Assert.False(a.Enabled));
        Assert.Same(InstructionStrip.Empty, Strip(ProcedureView.None));
    }
}
