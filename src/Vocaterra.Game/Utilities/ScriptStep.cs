namespace Vocaterra.Game.Utilities;

/// <summary>One step of the scripted run, in the order given on the command line.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Step">For <see cref="ScriptStepKind.Move"/>: the body's step.</param>
/// <param name="Value">For the talk steps: the NPC, the line or the file path.</param>
public sealed record ScriptStep(ScriptStepKind Kind, VoxelStep? Step = null, string Value = "");
