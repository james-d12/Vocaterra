namespace Vocaterra.Game.Core.Language.Lessons;

/// <summary>
/// Why a lesson that is not begun waits, and where it starts (V12c): the book's Lessons page names it (<c>next: talk to Luna</c>, <c>opens in session 4, Mar</c>,
/// <c>after “Ayer y mañana”</c>). Plain values from <see cref="LessonRunner.WaitOf"/>.
/// </summary>
/// <param name="Kind">Unlocked, waiting for its session, or waiting for the lesson it comes after.</param>
/// <param name="Session">The play session the lesson opens in.</param>
/// <param name="Npc">The agent id of the NPC the lesson starts at.</param>
/// <param name="After">The id of the lesson it waits for (<see cref="LessonWaitKind.After"/>), or <see langword="null"/>.</param>
public sealed record LessonWait(LessonWaitKind Kind, int Session, string Npc, string? After = null);
