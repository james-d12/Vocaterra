using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Reporting;

/// <summary>
/// The game's state for a report's table (S21): the build, the bundle, the seed, where the player stands and looks, the objective and the lesson,
/// what was open, the settings and the speech.
/// </summary>
public static class ReportStateBuilder
{
    /// <summary>The state of <paramref name="session"/> now.</summary>
    /// <param name="session">The session.</param>
    /// <param name="look">The look's yaw and pitch, in angle steps.</param>
    /// <param name="openPanel">What was open when the report was asked for.</param>
    /// <param name="quality">The name of the quality preset.</param>
    /// <param name="greyscale">Whether <c>--greyscale</c> is on.</param>
    /// <param name="noSpeech">Whether <c>--no-speech</c> is on.</param>
    /// <param name="microphoneFound">Whether a microphone was opened.</param>
    /// <param name="usesMicrophone">Whether the run uses a microphone at all.</param>
    public static ReportGameState Build(VoxelSession session, (double YawSteps, double PitchSteps) look, string openPanel, string quality, bool greyscale, bool noSpeech, bool microphoneFound, bool usesMicrophone)
    {
        var body = session.Simulation.Player;
        var worker = session.Worker;
        var speech = noSpeech
            ? "off (--no-speech)"
            : worker is null || !worker.IsReady
                ? "models still loading"
                : worker.CanHear
                    ? "models found, speech ready"
                    : worker.Load?.Problem is { } problem
                        ? $"models not loaded: {problem}"
                        : "models not loaded in this run";
        var microphone = microphoneFound ? "found" : usesMicrophone ? "not found" : "not used in this run";
        var lesson = session.Lessons.Active is { } active
            ? $"{active.Id}, step {session.Lessons.StepNumber} of {active.Steps.Count}"
            : "none";
        return new ReportGameState(
            BuildCommit.Read(),
            session.Bundle.Manifest.BundleId,
            session.Bundle.ContentHash,
            session.WorldSeed,
            body.CenterX / (double)VoxelPhysics.UnitsPerBlock,
            body.FeetY / (double)VoxelPhysics.UnitsPerBlock,
            body.CenterZ / (double)VoxelPhysics.UnitsPerBlock,
            look.YawSteps * 360.0 / Angles.StepsPerTurn,
            look.PitchSteps * 360.0 / Angles.StepsPerTurn,
            session.Objective.Summary,
            session.Objective.Steps.Select(static step => (step.Text, step.Done)).ToList(),
            lesson,
            openPanel,
            quality,
            greyscale,
            speech,
            microphone);
    }
}
