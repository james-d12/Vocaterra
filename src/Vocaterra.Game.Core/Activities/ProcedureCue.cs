namespace Vocaterra.Game.Core.Activities;

/// <summary>What a teacher's answer at a station asks the station to show or play (W08): the runner has counted it already; the panel presents it.</summary>
public enum ProcedureCue
{
    /// <summary>Play the instruction again (<c>procedure_repeat</c>).</summary>
    Repeat,

    /// <summary>Play the instruction slowly (<c>procedure_slower</c>).</summary>
    Slower,

    /// <summary>Show the help: the English, the highlight, the thing and the target selected (<c>procedure_help</c>).</summary>
    Help,
}
