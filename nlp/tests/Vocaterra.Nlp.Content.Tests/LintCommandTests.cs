using System.Text.Json;
using Vocaterra.Nlp.ContentBuilder;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Tests of the <c>lint</c> command and of what <c>validate</c> prints from the lint, through <see cref="Program.Run(string[], TextWriter, TextWriter)"/>.</summary>
public class LintCommandTests
{
    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static LintFixture Broken()
    {
        // An example no grammar covers: a lint error, while the bundle itself stays valid.
        return new LintFixture().Intent(
            "{\"intentId\":\"fx_buy\",\"requiredSlots\":[\"item\"],\"slotGrammars\":[\"[VERB:comprar] [ART]? [NOUN:item]\"],\"examples\":[\"compro una espada\",\"necesito un escudo\"]}");
    }

    [Fact]
    public void LintOfTheSamplePrintsOneLinePerFindingAndTheSummaryAndExitsZero()
    {
        var (code, output, error) = Run("lint", TestSupport.BundleDir);

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("lint: 0 errors, 2 warnings, ", lines[^1], StringComparison.Ordinal);
        Assert.Contains(lines, line => line.StartsWith("info confusion.catalog intents/intents.json [thank, cast_spell] ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("warning coverage.rule-only intents/intents.json [thank] ", StringComparison.Ordinal));
    }

    [Fact]
    public void StrictMakesWarningsFailTheCommand()
    {
        Assert.Equal(Program.ExitInvalid, Run("lint", TestSupport.BundleDir, "--strict").Code);
    }

    [Fact]
    public void ErrorsFailTheCommandWithoutStrict()
    {
        using var fixture = Broken();
        fixture.Load();

        var (code, output, _) = Run("lint", fixture.Directory);

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("error coverage.uncovered intents/intents.json [fx_buy] ", output, StringComparison.Ordinal);
        Assert.Contains("Suggested grammar", output, StringComparison.Ordinal);
    }

    [Fact]
    public void LintDoesNotVerifyHashesSoItRunsBeforeHash()
    {
        using var fixture = Broken();
        fixture.Load();

        // The fixture's manifest is stale (the intents file changed), and validate says so while lint does not.
        Assert.Equal(Program.ExitInvalid, Run("validate", fixture.Directory).Code);
        Assert.Contains("lint:", Run("lint", fixture.Directory).Output, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonPrintsTheReportAsJson()
    {
        var (code, output, _) = Run("lint", TestSupport.BundleDir, "--json");

        Assert.Equal(Program.ExitOk, code);
        using var document = JsonDocument.Parse(output);
        Assert.Equal(0, document.RootElement.GetProperty("summary").GetProperty("errors").GetInt32());
        Assert.True(document.RootElement.GetProperty("findings").GetArrayLength() > 0);
        Assert.Equal(output, ContentLinter.Lint(TestSupport.Bundle).ToJson() + "\n");
    }

    [Fact]
    public void LevelAddsCefrFindingsAndFailOnLevelMakesThemFail()
    {
        var warn = Run("lint", TestSupport.BundleDir, "--level", "a1");
        var fail = Run("lint", TestSupport.BundleDir, "--level", "A1", "--fail-on-level");

        Assert.Equal(Program.ExitOk, warn.Code);
        Assert.Contains("warning cefr.dialogue dialogue/dialogue.json ", warn.Output, StringComparison.Ordinal);
        Assert.Equal(Program.ExitInvalid, fail.Code);
        Assert.Contains("error cefr.prompt prompts/prompts.json ", fail.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void PromptsAndHostPoolsSelectWhatReachabilityIsMeasuredFrom()
    {
        var (code, output, _) = Run("lint", TestSupport.BundleDir, "--prompts", "spell-cast-open,quiz-spell-name", "--host-pools", "greet_*,trade_done");

        Assert.Equal(Program.ExitOk, code);
        Assert.Contains("reach.pool dialogue/dialogue.json [ask_price_reply, blacksmith]", output, StringComparison.Ordinal);
        Assert.DoesNotContain("[greet_reply", output, StringComparison.Ordinal);
        Assert.DoesNotContain("[trade_done", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownPromptIsReportedAsInvalidInputNotAsUsage()
    {
        var (code, _, error) = Run("lint", TestSupport.BundleDir, "--prompts", "nope");

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("'nope'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingBundleExitsWithOne()
    {
        var (code, _, error) = Run("lint", Path.Combine(Path.GetTempPath(), "vocaterra-lint-missing-" + Guid.NewGuid().ToString("N")));

        Assert.Equal(Program.ExitInvalid, code);
        Assert.Contains("does not exist", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("lint")]
    [InlineData("lint a b")]
    [InlineData("lint a --level b2")]
    [InlineData("lint a --fail-on-level")]
    [InlineData("lint a --verbose")]
    [InlineData("lint a --strict --strict")]
    [InlineData("lint a --level")]
    [InlineData("validate a --json")]
    public void WrongCommandLinesExitWithTwo(string line)
    {
        var (code, _, error) = Run(line.Split(' '));

        Assert.Equal(Program.ExitUsage, code);
        Assert.Contains("lint <bundleDir>", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatePrintsTheLintWarningsAfterTheOkLineAndStillSucceeds()
    {
        var (code, output, error) = Run("validate", TestSupport.BundleDir);

        Assert.Equal(Program.ExitOk, code);
        Assert.Equal(string.Empty, error);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal($"OK: bundle 'v1' 1.0.0, {TestSupport.Bundle.Manifest.Files.Count} files, hash {TestSupport.Bundle.ContentHash}", lines[0]);
        Assert.Equal(2, lines.Count(static line => line.StartsWith("warning coverage.rule-only ", StringComparison.Ordinal)));
        Assert.DoesNotContain(lines, static line => line.StartsWith("info ", StringComparison.Ordinal));
        Assert.StartsWith("lint: 0 errors, 2 warnings, ", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateNeverFailsOnLintExceptWithStrict()
    {
        using var fixture = Broken();
        fixture.Load();

        // Re-hash the fixture (a temporary copy) so that validate itself passes, then compare plain and strict.
        File.WriteAllText(Path.Combine(fixture.Directory, "manifest.json"), ContentManifest.Compute(fixture.Directory).ToJson());

        var plain = Run("validate", fixture.Directory);
        var strict = Run("validate", fixture.Directory, "--strict");

        Assert.Equal(Program.ExitOk, plain.Code);
        Assert.Contains("error coverage.uncovered", plain.Output, StringComparison.Ordinal);
        Assert.Equal(Program.ExitInvalid, strict.Code);
        Assert.Contains("validate --strict", strict.Error, StringComparison.Ordinal);
    }
}
