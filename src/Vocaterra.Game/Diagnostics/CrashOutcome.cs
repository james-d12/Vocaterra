namespace Vocaterra.Game.Diagnostics;

/// <summary>What the handler wrote: the report's file and the last-chance save's file (each <see langword="null"/> when it was not written), and the save's note.</summary>
/// <param name="ReportPath">The Markdown report, or <see langword="null"/>.</param>
/// <param name="SavePath">The last-chance save, or <see langword="null"/>.</param>
/// <param name="SaveNote">What the last-chance save did, in words.</param>
public sealed record CrashOutcome(string? ReportPath, string? SavePath, string SaveNote);
