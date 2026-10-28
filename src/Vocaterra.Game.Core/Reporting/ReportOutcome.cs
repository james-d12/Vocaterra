namespace Vocaterra.Game.Core.Reporting;

/// <summary>What a key or a click asks of the game (S21): nothing, save the report, or drop it.</summary>
public enum ReportOutcome
{
    /// <summary>The panel only changed its own state.</summary>
    None,

    /// <summary>The report is to be saved. The game writes it, then calls <see cref="ReportPanel.Hide"/>.</summary>
    Save,

    /// <summary>The report is dropped. The game calls <see cref="ReportPanel.Hide"/> and keeps nothing.</summary>
    Cancel,
}
