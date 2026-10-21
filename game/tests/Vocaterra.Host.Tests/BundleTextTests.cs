namespace Vocaterra.Host.Tests;

/// <summary>The game bundle's prompt text, and the text loop's line for a shopkeeper prompt.</summary>
public sealed class BundleTextTests
{
    [Fact]
    public void EveryPromptInTheGameBundleHasNonBlankText()
    {
        var prompts = GameContent.Bundle.Prompts;

        Assert.NotEmpty(prompts);
        var blank = prompts.Values
            .Where(static prompt => string.IsNullOrWhiteSpace(prompt.Text))
            .Select(static prompt => prompt.PromptId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(blank);
    }

    [Fact]
    public void TheTextLoopShowsTheShopkeepersLineBeforeItsTurn()
    {
        var prompt = GameContent.Bundle.Prompts["stall-need"];
        using var writer = new StringWriter();

        VerdictReport.WritePrompt(writer, prompt);

        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            new[] { "[stall-need] Constrained, agent shopkeeper, intents request_item, ask_help, dont_know", "  agent: ¿Qué necesitas?" },
            lines);
    }
}
