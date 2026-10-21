using Vocaterra.Nlp.Core.Prompts;

namespace Vocaterra.Host.Tests;

/// <summary>The prompt the text loop shows before each turn: its header and the agent's line (<see cref="PromptSpec.Text"/>).</summary>
public sealed class PromptReportTests
{
    [Fact]
    public void APromptWithTextShowsTheAgentsLineUnderItsHeader()
    {
        var prompt = Prompt(text: "¡Hola! ¿Qué necesitas?");

        var lines = Write(prompt);

        Assert.Equal(
            new[] { "[greet-test] Open, agent blacksmith, intents greet", "  agent: ¡Hola! ¿Qué necesitas?" },
            lines);
    }

    [Fact]
    public void APromptWithoutTextShowsTheHeaderAlone()
    {
        var lines = Write(Prompt(text: null));

        Assert.Equal(new[] { "[greet-test] Open, agent blacksmith, intents greet" }, lines);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void APromptWithBlankTextShowsTheHeaderAlone(string text)
    {
        var lines = Write(Prompt(text));

        Assert.Single(lines);
    }

    [Fact]
    public void APromptWithNoAgentShowsThePlayerAsTheAgent()
    {
        var prompt = new PromptSpec("open-talk", RecognitionMode.Open, Difficulty.Beginner, "greetings", ["greet"], null, [], "Hola.");

        var lines = Write(prompt);

        Assert.Equal(
            new[] { "[open-talk] Open, agent player, intents greet", "  agent: Hola." },
            lines);
    }

    private static PromptSpec Prompt(string? text)
    {
        return new PromptSpec("greet-test", RecognitionMode.Open, Difficulty.Beginner, "greetings", ["greet"], "blacksmith", [], text);
    }

    private static string[] Write(PromptSpec prompt)
    {
        using var writer = new StringWriter();
        VerdictReport.WritePrompt(writer, prompt);
        return writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }
}
