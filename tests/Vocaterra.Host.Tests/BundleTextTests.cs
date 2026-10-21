namespace Vocaterra.Host.Tests;

/// <summary>The game bundle's prompt text, and the text loop's line for a blacksmith prompt.</summary>
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
    public void TheTextLoopShowsTheBlacksmithsLineBeforeItsTurn()
    {
        var prompt = GameContent.Bundle.Prompts["buy-item-open"];
        using var writer = new StringWriter();

        VerdictReport.WritePrompt(writer, prompt);

        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            new[] { "[buy-item-open] Open, agent blacksmith, intents buy_item", "  agent: ¿Quieres comprar algo?" },
            lines);
    }
}
