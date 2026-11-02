using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>V30 (review): a word the core read as its accented form carries that form's features, not the written word's.</summary>
public sealed class AccentNoteWordTests
{
    [Fact]
    public void AnAccentFixTakesTheFeaturesOfTheNotesReading()
    {
        var session = TestGame.NewSession();

        var turn = session.Conversation.Say("fisher", "luz compro pescado", new GuideScene(TestGame.ViewpointOf(session), PromptOverride: "lesson-luz-bought"), session.StoredTime, "hash");

        var word = Assert.Single(turn.Words!, static w => w.Lemma == "comprar");
        Assert.Equal("compró", word.Text);
        Assert.Equal("3", word.Person);
        Assert.Equal("pret", word.Tense);
    }
}
