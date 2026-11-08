using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Language.Phrases;

/// <summary>
/// A lesson's phrases in free talk (village-plan V20, handover 9.3 D (b) and (c)). Free talk credited only the rows of an NPC's <c>focus</c>, so a phase-B line said again
/// counted no use (<c>Mañana compraré pan.</c> to Luz, r23); it now credits any phrase of a lesson the learner has begun or done, at any NPC. And an NPC showed its focus
/// cards from the first talk, before their lesson (Luz <c>Vendo madera.</c>, session 10, in session 5; León his A2 lines); a card now waits until its lesson is the current
/// one (unlocked) or done.
/// </summary>
public sealed class LessonPhrasesInFreeTalkTests
{
    private static VoxelSession TalkingTo(string npc, int day)
    {
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, day);
        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalkForClass(npc);
        Assert.False(session.Lessons.Engaged, $"{npc} opened a lesson in session {day}, not free talk");
        return session;
    }

    // Luz's lesson of session 10 is on offer when the learner walks up; its steps skipped, the talk is free talk and the lesson begun (credited).
    private static VoxelSession AtTheBakery()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 10);
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalkForClass("baker");
        for (var i = 0; session.Lessons.Engaged && i < 50; i++)
        {
            _ = session.Lessons.Skip();
        }

        Assert.False(session.Lessons.Engaged);
        return session;
    }

    private static IReadOnlyList<PhraseUse> Uses(VoxelSession session, string line)
    {
        var before = session.Events.OfType<PhraseUse>().Count();
        var turn = session.Say(line);
        Assert.True(turn.Communicated, $"'{line}' was not understood ({turn.Intent})");
        return session.Events.OfType<PhraseUse>().Skip(before).ToList();
    }

    private static IReadOnlyList<string> LastSkills(VoxelSession session) =>
        session.Conversation.Records.Select(static r => r.Utterance).OfType<ReplayEnvelope>().Last().Context.ExtraSkills ?? [];

    [Theory]
    [InlineData("smith", "Mañana compraré pan.", "manana-comprare-pan")] // Luz's lesson, said to Domingo
    [InlineData("smith", "Domingo, mañana compraré pan.", "manana-comprare-pan")] // his name as a vocative, read as he reads it
    [InlineData("smith", "hola, compraré pan mañana", "manana-comprare-pan")] // a greeting first, and one of the row's variants
    [InlineData("fisher", "Luz hará pan.", "luz-hara-pan")] // his own lesson's, which his focus does not name
    [InlineData("farmer", "Me gustan las manzanas.", "me-gustan-las-manzanas")]
    public void APhraseOfALessonDoneIsCreditedAtAnyNpc(string npc, string line, string phrase)
    {
        var session = TalkingTo(npc, 10); // sessions 1 to 9 done

        var use = Assert.Single(Uses(session, line));

        Assert.Equal((phrase, PhraseUseKind.Unaided, npc), (use.PhraseId, use.Kind, use.Npc));
    }

    [Theory]
    [InlineData("fisher", "Luz hará pan.", "luz-hara-pan")]
    [InlineData("fisher", "Mar, luz hará pan.", "luz-hara-pan")]
    [InlineData("farmer", "hola, me gustan las manzanas", "me-gustan-las-manzanas")]
    public void TheLearnerModelIsToldTheSamePhrase(string npc, string line, string phrase)
    {
        // NpcService tells the core's learner model the phrase of a line said without a mistake (its answer set has these rows); the session credits it (above).
        var session = TalkingTo(npc, 10);

        Assert.Equal([phrase], Uses(session, line).Select(static u => u.PhraseId));
        Assert.Contains("phrase." + phrase, LastSkills(session));
    }

    [Fact]
    public void ARequestForAnotherItemIsNoUseOfTheRowForBread()
    {
        // V26: `Quiero dos manzanas.` to Luz is refused («Solo vendo pan.») and was logged as a use of quiero-dos-panes, detail manzana: the row names bread.
        var session = AtTheBakery();

        Assert.DoesNotContain(Uses(session, "quiero dos manzanas"), static u => u.PhraseId == "quiero-dos-panes");
        Assert.DoesNotContain("phrase.quiero-dos-panes", LastSkills(session));
    }

    [Theory]
    [InlineData("quiero dos panes")]
    [InlineData("quiero tres panes")] // the number is a slot of the row: another count of the same item is still a use
    public void ARequestForBreadIsAUseOfTheRowForBread(string line)
    {
        var session = AtTheBakery();

        Assert.Contains(Uses(session, line), static u => u.PhraseId == "quiero-dos-panes");
    }

    [Fact]
    public void APhraseOfALessonNotBegunIsNotCredited()
    {
        var session = TalkingTo("smith", 9); // Mañana en el pueblo is the session's lesson, not begun

        Assert.DoesNotContain(Uses(session, "Mañana compraré pan."), static u => u.PhraseId == "manana-comprare-pan");
        Assert.DoesNotContain("phrase.manana-comprare-pan", LastSkills(session));
    }

    [Fact]
    public void TheFocusStillComesFirst()
    {
        // A line that is the focus's row is that row, though a lesson's row has the same words: the stall's «quiero agua» is pedir-agua, not first-words' necesito-agua.
        var session = TalkingTo("shopkeeper", 10);

        Assert.Equal(["pedir-agua"], Uses(session, "quiero agua").Select(static u => u.PhraseId));
    }

    private static readonly string[] AllFarmerCards = ["como-te-llamas", "que-haces", "quiero-tres-manzanas-rojas", "cuanto-es-todo", "puedo-pagar-con-fruta"];

    public static TheoryData<int, string[]> FarmerCards => new()
    {
        { 5, AllFarmerCards }, // every scaffold phrase has its card from rung 1, whether or not a class taught it (L01)
        { 6, AllFarmerCards },
        { 8, AllFarmerCards },
        { 10, AllFarmerCards },
    };

    [Theory]
    [MemberData(nameof(FarmerCards))]
    public void AnNpcShowsEveryCardOfItsFocusFromTheFirstTalkWhateverTheDay(int day, string[] cards)
    {
        var session = TalkingTo("farmer", day);

        Assert.Equal(cards, session.Support.Cards.Select(static c => c.PhraseId));
    }

    [Fact]
    public void LuzShowsTheCardOfTheMarketToo()
    {
        var session = TalkingTo("baker", 6); // the market class is four sessions away, and its phrase has its card all the same (L01)

        Assert.Equal(["quiero-dos-panes", "cuanto-cuesta", "hay-pan", "vendo-madera"], session.Support.Cards.Select(static c => c.PhraseId));
    }
}
