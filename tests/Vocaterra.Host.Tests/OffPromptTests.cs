using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Host.Tests;

/// <summary>The host shows the game's off-prompt reply (V27): the NPC's one line as its agent line, and an <c>off-prompt:</c> diagnostic line.</summary>
public sealed class OffPromptTests
{
    private static readonly StoredTimestamp Now = new(1_760_000_000);

    private static string Report(string line, string prompt)
    {
        var session = new ConversationSession(GameContent.Bundle);
        var result = session.Evaluate(line, prompt, Now);
        using var writer = new StringWriter();
        VerdictReport.Write(writer, prompt, line, result, session.LastExchange);
        return writer.ToString();
    }

    private static List<string> Lines(string report, string prefix) =>
        [.. report.Split('\n').Select(static l => l.TrimEnd('\r')).Where(l => l.StartsWith(prefix, StringComparison.Ordinal)).Select(l => l[prefix.Length..])];

    [Theory]
    [InlineData("me gusta el río", "smith-open", "state_like", "Te entiendo. Yo hago hachas.")]
    [InlineData("tengo dos manzanas", "fisher-open", "state_have", "Te entiendo. ¿Buscas un lugar?")]
    [InlineData("el río es grande", "baker-open", "describe", "Te entiendo. ¿Quieres pan?")]
    public void ALineThePromptDoesNotTakeGetsTheNpcsOffPromptLineAndNothingElse(string line, string prompt, string intent, string reply)
    {
        var report = Report(line, prompt);

        Assert.Equal([reply], Lines(report, "  agent: "));
        Assert.Equal([intent], Lines(report, "  off-prompt: "));
        Assert.Empty(Lines(report, "  repair: "));
    }

    [Theory]
    [InlineData("Tienes veinte panes.", "baker-open", "Sí, es verdad.")]
    [InlineData("Usted tiene muchas manzanas.", "baker-open", "Sí, es verdad.")]
    [InlineData("Eres el herrero.", "villager-open", "Sí, es verdad.")]
    [InlineData("Tengo veinte panes.", "baker-open", "¡Qué bien!")]
    [InlineData("Ella tiene veinte panes.", "baker-open", "¡Qué bien!")]
    public void AStatementAboutTheListenerGetsSiEsVerdadInTheHostAsInTheGame(string line, string prompt, string reply)
    {
        Assert.Equal([reply], Lines(Report(line, prompt), "  agent: "));
    }

    // No intent of the catalog takes this line since M78 (the core reads «la tienda está en el pueblo» as describe, so it is no longer the unknown example).
    [Fact]
    public void ALineNoIntentOfTheCatalogTakesKeepsTheRepairAndPrintsNoOffPromptLine()
    {
        var report = Report("la tienda cierra mañana", "farmer-open");

        Assert.Empty(Lines(report, "  off-prompt: "));
        Assert.NotEmpty(Lines(report, "  repair: "));
    }

    [Fact]
    public void AnOffPromptLineDoesNotMarkTheLearnerDownInTheHostAsInTheGame()
    {
        // V30: the game keeps the learner model as it was before the line (V27); the host kept the model the core had marked down.
        var session = new ConversationSession(GameContent.Bundle);

        _ = session.Evaluate("me gusta el río", "smith-open", Now);

        Assert.NotNull(session.LastExchange!.OffPromptIntent);
        Assert.Equal(Vocaterra.Nlp.Decision.Learning.LearnerModel.New(), session.Context.Learner);
    }
}
