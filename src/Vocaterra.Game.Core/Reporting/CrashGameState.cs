namespace Vocaterra.Game.Core.Reporting;

/// <summary>The state of the game a crash report can show: the session and tick, the rows of the state table, and a note when it is partial.</summary>
/// <param name="Session">The play session, or <see langword="null"/>.</param>
/// <param name="Tick">The simulation tick, or <see langword="null"/>.</param>
/// <param name="Rows">The state's name and value rows.</param>
/// <param name="Note">Why the state is partial, or <see langword="null"/>.</param>
public sealed record CrashGameState(int? Session, long? Tick, IReadOnlyList<(string Item, string Value)> Rows, string? Note);
