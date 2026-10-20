using System.Text.Json;
using CliProgram = Slg.Cli.Program;

namespace Slg.Pipeline.Tests;

/// <summary>Tests for the demo host, driven through <see cref="CliProgram.Run(string[], TextWriter, TextWriter)"/>.</summary>
public class CliTests
{
    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliProgram.Run(args, output, error);
        return (code, Normalize(output.ToString()), Normalize(error.ToString()));
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string[] Eval(params string[] extra)
    {
        return ["eval", "--transcript", TestSupport.WorkedExample, "--prompt", TestSupport.OfferPrompt, "--content", TestSupport.BundleDir, .. extra];
    }

    [Fact]
    public void EvalPrintsTheVerdictJsonAndTheResponseLines()
    {
        var (code, output, error) = Run(Eval());

        Assert.Equal(CliProgram.ExitOk, code);
        Assert.Equal(string.Empty, error);
        var lines = output.TrimEnd('\n').Split('\n');
        using var verdict = JsonDocument.Parse(lines[0]);
        Assert.Equal("offer_item", verdict.RootElement.GetProperty("intent").GetString());
        Assert.Equal("espada", verdict.RootElement.GetProperty("slots").GetProperty("item").GetString());
        Assert.Equal("2", verdict.RootElement.GetProperty("slots").GetProperty("qty").GetString());
        Assert.True(verdict.RootElement.GetProperty("communicated").GetBoolean());
        var tags = verdict.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("tag").GetString()!).ToArray();
        Assert.Equal(["conj.person", "noun.number"], tags);
        Assert.Contains(lines, static line => line.StartsWith("npc: ", StringComparison.Ordinal) && line.Contains("tienes dos espadas", StringComparison.Ordinal));
    }

    [Fact]
    public void EvalStartsTheNpcInItsInitialStateAndAnswersTheTradeWithoutAnNpcStateOption()
    {
        var (code, output, _) = Run(Eval());

        Assert.Equal(CliProgram.ExitOk, code);
        var replies = output.Split('\n').Where(static line => line.StartsWith("npc: ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, replies.Length);
        Assert.Equal("npc: Ah, ¿tienes dos espadas? ¡Muy bien!", replies[0]);
        Assert.Contains(
            replies[1],
            new[] { "npc: ¡Dos espadas! Trato hecho.", "npc: Vale, dos espadas. Gracias, amigo.", "npc: Muy bien: dos espadas. Aquí tienes tu dinero." });
        Assert.DoesNotContain("qué dices", output, StringComparison.Ordinal);
    }

    [Fact]
    public void EvalOfAnExactQuizAnswerIsCommunicatedAndAcknowledged()
    {
        var (code, output, _) = Run("eval", "--transcript", "espada", "--prompt", "quiz-item-name", "--content", TestSupport.BundleDir);

        Assert.Equal(CliProgram.ExitOk, code);
        var lines = output.TrimEnd('\n').Split('\n');
        using var verdict = JsonDocument.Parse(lines[0]);
        Assert.True(verdict.RootElement.GetProperty("communicated").GetBoolean());
        Assert.Equal("unknown", verdict.RootElement.GetProperty("intent").GetString());
        Assert.Equal("Exact", verdict.RootElement.GetProperty("resolvedBy").GetString());
        Assert.Contains(lines, static line => line is "npc: ¡Correcto!" or "npc: ¡Muy bien!" or "npc: ¡Exacto!");
        Assert.DoesNotContain(lines, static line => line.StartsWith("repair:", StringComparison.Ordinal));
    }

    [Fact]
    public void EvalOfABareSpellNameCastsTheSpellAtFullPower()
    {
        var (code, output, _) = Run("eval", "--transcript", "fuego", "--prompt", "spell-cast-constrained", "--content", TestSupport.BundleDir);

        Assert.Equal(CliProgram.ExitOk, code);
        var lines = output.TrimEnd('\n').Split('\n');
        using var verdict = JsonDocument.Parse(lines[0]);
        Assert.Equal("cast_spell", verdict.RootElement.GetProperty("intent").GetString());
        Assert.Equal("fuego", verdict.RootElement.GetProperty("slots").GetProperty("spell").GetString());
        Assert.True(verdict.RootElement.GetProperty("communicated").GetBoolean());
        Assert.Contains("effect: cast_spell_power spell_power 1", lines);
    }

    [Fact]
    public void EvalIsDeterministicAcrossRuns()
    {
        var first = Run(Eval("--seed", "7", "--counter", "2"));
        var second = Run(Eval("--seed", "7", "--counter", "2"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void EvalHonoursTheNpcStateAndRepeatCounts()
    {
        var (code, output, _) = Run(Eval("--npc-state", "trading", "--repeat", "conj.person=2"));

        Assert.Equal(CliProgram.ExitOk, code);
        Assert.Contains("npc: Mmm, ¿seguro?", output, StringComparison.Ordinal);
        Assert.DoesNotContain("tienes dos espadas", output, StringComparison.Ordinal);
    }

    [Fact]
    public void EvalPrintsTheRepairStepWhenThePlayerIsNotUnderstood()
    {
        var (code, output, _) = Run("eval", "--transcript", "xyzzy plugh", "--prompt", TestSupport.OfferPrompt, "--content", TestSupport.BundleDir);

        Assert.Equal(CliProgram.ExitOk, code);
        Assert.Contains("\"communicated\":false", output, StringComparison.Ordinal);
        Assert.Contains("repair: AskAgain ¿Cómo?", output, StringComparison.Ordinal);
    }

    [Fact]
    public void EvalPrintsGameEffects()
    {
        var (_, output, _) = Run("eval", "--transcript", "quiero comprar una espada", "--prompt", "buy-item-open", "--content", TestSupport.BundleDir);

        Assert.Contains("effect: buy_item_price trade_price 1", output, StringComparison.Ordinal);
    }

    [Fact]
    public void EvalReportsAnUnknownPromptAndListsTheKnownOnes()
    {
        var (code, output, error) = Run("eval", "--transcript", "hola", "--prompt", "nope", "--content", TestSupport.BundleDir);

        Assert.Equal(CliProgram.ExitFailed, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("unknown prompt 'nope'", error, StringComparison.Ordinal);
        Assert.Contains(TestSupport.OfferPrompt, error, StringComparison.Ordinal);
    }

    [Fact]
    public void EvalReportsAnInvalidBundle()
    {
        var (code, _, error) = Run("eval", "--transcript", "hola", "--prompt", "greet-open", "--content", Path.Combine(AppContext.BaseDirectory, "no-such-bundle"));

        Assert.Equal(CliProgram.ExitFailed, code);
        Assert.StartsWith("error:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void EvalExplainsMissingOptions()
    {
        Assert.Equal(CliProgram.ExitUsage, Run("eval", "--prompt", TestSupport.OfferPrompt, "--content", TestSupport.BundleDir).Code);
        Assert.Equal(CliProgram.ExitUsage, Run("eval", "--transcript", "hola", "--content", TestSupport.BundleDir).Code);
        Assert.Equal(CliProgram.ExitUsage, Run("eval", "--transcript", "hola", "--prompt", "greet-open", "--content").Code);
        Assert.Equal(CliProgram.ExitUsage, Run(Eval("--bogus", "1")).Code);
        Assert.Equal(CliProgram.ExitUsage, Run(Eval("--seed", "many")).Code);
        Assert.Equal(CliProgram.ExitUsage, Run(Eval("--repeat", "no-count")).Code);
        var withModelsButNoAudio = Run(Eval("--models", AppContext.BaseDirectory));
        Assert.Equal(CliProgram.ExitUsage, withModelsButNoAudio.Code);
        Assert.Contains("--wav", withModelsButNoAudio.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void WithModelsButNoSpeechModelTheFailureIsReported()
    {
        var wav = Path.Combine(Path.GetTempPath(), "slg-cli-" + Guid.NewGuid().ToString("N") + ".wav");
        Slg.Audio.WavFile.Write(wav, new Slg.Audio.WavData(16000, 1, new short[1600]));
        try
        {
            var (code, _, error) = Run("eval", "--wav", wav, "--prompt", "greet-open", "--content", TestSupport.BundleDir, "--models", Path.GetTempPath());

            Assert.Equal(CliProgram.ExitFailed, code);
            Assert.Contains("No speech model found", error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(wav);
        }
    }

    [Fact]
    public void UnknownCommandsAndNoArgumentsShowUsage()
    {
        var none = Run();
        var unknown = Run("frobnicate");
        var help = Run("help");

        Assert.Equal(CliProgram.ExitUsage, none.Code);
        Assert.Contains("Usage:", none.Error, StringComparison.Ordinal);
        Assert.Equal(CliProgram.ExitUsage, unknown.Code);
        Assert.Contains("Unknown command 'frobnicate'", unknown.Error, StringComparison.Ordinal);
        Assert.Equal(CliProgram.ExitOk, help.Code);
        Assert.Contains("Usage:", help.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayOutThenReplayPrintsOk()
    {
        using var temp = new TempFile();

        var eval = Run(Eval("--replay-out", temp.Path, "--seed", "9", "--counter", "4"));
        var evalAgain = Run(Eval("--replay-out", temp.Path, "--npc-state", "idle"));
        var replay = Run("replay", temp.Path, "--content", TestSupport.BundleDir);

        Assert.Equal(CliProgram.ExitOk, eval.Code);
        Assert.Equal(CliProgram.ExitOk, evalAgain.Code);
        Assert.Equal(2, File.ReadAllLines(temp.Path).Length);
        Assert.Equal((CliProgram.ExitOk, "OK\n", string.Empty), replay);
    }

    [Fact]
    public void ReplayReportsTheDifferingPathAndExitsWithOne()
    {
        using var temp = new TempFile();
        Run(Eval("--replay-out", temp.Path));
        var tampered = File.ReadAllText(temp.Path).Replace("\"qty\":\"2\"", "\"qty\":\"3\"", StringComparison.Ordinal);
        File.WriteAllText(temp.Path, tampered);

        var (code, output, _) = Run("replay", temp.Path, "--content", TestSupport.BundleDir);

        Assert.Equal(CliProgram.ExitFailed, code);
        Assert.StartsWith("DIFF entry 1:", output, StringComparison.Ordinal);
        Assert.Contains("/verdict/slots/qty", output, StringComparison.Ordinal);
        Assert.DoesNotContain("OK", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayRejectsBadInput()
    {
        using var temp = new TempFile();
        File.WriteAllText(temp.Path, "not json\n");

        Assert.Equal(CliProgram.ExitUsage, Run("replay", "--content", TestSupport.BundleDir).Code);
        Assert.Equal(CliProgram.ExitUsage, Run("replay", temp.Path).Code);
        var (code, _, error) = Run("replay", temp.Path, "--content", TestSupport.BundleDir);
        Assert.Equal(CliProgram.ExitFailed, code);
        Assert.Contains("line 1", error, StringComparison.Ordinal);
        Assert.Equal(CliProgram.ExitFailed, Run("replay", temp.Path + ".missing", "--content", TestSupport.BundleDir).Code);
    }

    private sealed class TempFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "slg-replay-" + Guid.NewGuid().ToString("N") + ".jsonl");

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
