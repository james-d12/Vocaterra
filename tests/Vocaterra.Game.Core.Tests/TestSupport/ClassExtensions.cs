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
        _ = session.HandOverGoods(); // the goods of an order the NPC asks for are handed over first, as a learner who answers right does (issue #42)
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

    /// <summary>
    /// Marks NPCs as met without a talk, as an earlier visit would have (issue #55: the first talk with an NPC that has an order tells the order and takes no goods,
    /// so a test of the hand-over, a sale or a class there starts from a later talk). With no ids, every NPC an order is brought to.
    /// </summary>
    public static void MeetOrderNpcs(this VoxelSession session, params string[] npcs)
    {
        var ids = npcs.Length > 0 ? npcs : session.Objectives.Where(static o => o.Spec.IsOrder).Select(static o => o.Spec.DeliverTo!).Distinct().ToArray();
        foreach (var npc in ids)
        {
            session.MarkMet(npc);
        }
    }

    /// <summary>
    /// When the NPC the panel is open on asks for the goods of an order (issue #42, <see cref="GuideSession.HandOverOpen"/>), says the line of the card its receiving prompt
    /// practises (<c>Aquí tienes cinco bloques de madera.</c>), as a learner who answers right does, and returns that turn; otherwise <see langword="null"/>.
    /// </summary>
    public static GuideTurn? HandOverGoods(this VoxelSession session)
    {
        if (!session.HandOverOpen)
        {
            return null;
        }

        var prompt = session.Conversation.PromptOf(session.CurrentNpc);
        var row = session.Data.Npcs.FocusAt(prompt).Select(session.Data.Phrases.Find).First(static row => row is { IsLearnerPhrase: true })!;
        return session.Say(row.Spanish);
    }
}
