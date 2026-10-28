using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests.Reporting;

/// <summary>The report's table of the game's state: where the player stands and looks, what was open, and the speech and the microphone in words.</summary>
public sealed class ReportStateBuilderTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    [Fact]
    public void TheTableHoldsThePlaceTheLookAndWhatWasOpen()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        var state = ReportStateBuilder.Build(session, (Angles.StepsPerTurn / 4.0, -Angles.StepsPerTurn / 36.0), "book", "low", greyscale: true, noSpeech: false, microphoneFound: false, usesMicrophone: false);
        var body = session.Simulation.Player;
        Assert.Equal(body.CenterX / (double)VoxelPhysics.UnitsPerBlock, state.X, 6);
        Assert.Equal(90.0, state.YawDegrees, 6);
        Assert.Equal(-10.0, state.PitchDegrees, 6);
        Assert.Equal("book", state.OpenPanel);
        Assert.Equal("low", state.Quality);
        Assert.True(state.Greyscale);
        Assert.Equal(42UL, state.Seed);
        Assert.Equal("none", state.Lesson);
        Assert.Equal(session.Objective.Summary, state.Objective);
    }

    [Theory]
    [InlineData(true, false, false, "off (--no-speech)", "not used in this run")]
    [InlineData(false, false, true, "models still loading", "not found")]
    [InlineData(false, true, true, "models still loading", "found")]
    public void TheSpeechAndTheMicrophoneAreSaidInWords(bool noSpeech, bool found, bool uses, string speech, string microphone)
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value); // no worker started: the models are still loading
        var state = ReportStateBuilder.Build(session, (0, 0), "none (the world)", "high", greyscale: false, noSpeech, found, uses);
        Assert.Equal(speech, state.Speech);
        Assert.Equal(microphone, state.Microphone);
    }
}
