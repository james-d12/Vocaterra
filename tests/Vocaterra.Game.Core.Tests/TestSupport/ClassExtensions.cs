using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>What the tests of the lessons ask of the runner since L01: the class that is open (its teacher met, its earlier lesson done) and not begun.</summary>
internal static class ClassExtensions
{
    /// <summary>The newest-session class of a later session that is open and not begun, or <see langword="null"/>.</summary>
    public static LessonDefinition? OpenClass(this LessonRunner runner) =>
        runner.Book.Lessons.Where(l => l.Session > 1 && runner.StatusOf(l.Id) == LessonStatus.NotStarted && runner.IsUnlocked(l)).OrderByDescending(static l => l.Session).FirstOrDefault();

    /// <summary>
    /// Opens the panel on an NPC and takes the class on offer there whose session the test's day has reached, as saying Sí to the teacher does (L01: before, every lesson started by itself
    /// at the first talk of its day). A talk that has no such class is only a talk.
    /// </summary>
    public static NpcOpening OpenTalkForClass(this VoxelSession session, string npc)
    {
        var opening = session.OpenTalk(npc);
        if (!session.Lessons.Engaged && session.Lessons.OfferFor(npc) is not null)
        {
            // The class of the day the test sets (the newest whose session has come, as the day used to pick it), else the one on offer.
            var runner = session.Lessons;
            var wanted = runner.Book.Lessons
                .Where(l => string.Equals(l.Npc, npc, StringComparison.Ordinal) && l.Session <= session.Day && runner.StatusOf(l.Id) != LessonStatus.Completed && runner.IsUnlocked(l))
                .OrderByDescending(static l => l.Session)
                .FirstOrDefault();
            var id = runner.Active?.Id ?? wanted?.Id;
            if (id is not null)
            {
                _ = runner.Start(id);
            }
        }

        return opening;
    }
}
