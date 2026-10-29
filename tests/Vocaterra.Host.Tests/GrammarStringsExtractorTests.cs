using System.Text;
using System.Text.Json;
using Vocaterra.Game.Core.Language.Phrases;

namespace Vocaterra.Host.Tests;

/// <summary>
/// The grammar check's strings (S23): the host's extractor covers every Spanish source of game-v1 and the game's sheets, with stable unique ids, the
/// right kinds and the learner-error tags of the phrase sheet. The committed strings file must be what the extractor writes now.
/// </summary>
public sealed class GrammarStringsExtractorTests
{
    private static readonly Lazy<IReadOnlyList<GrammarStringLine>> Extracted =
        new(() => GrammarStringsExtractor.Extract(GameContent.Data, GameContent.BundleDirectory()));

    private static IReadOnlyList<GrammarStringLine> All => Extracted.Value;

    private static string ContentDirectory => Directory.GetParent(GameContent.BundleDirectory())!.FullName;

    private static string StringsPath => Path.Combine(ContentDirectory, "grammar", "game-v1-strings.jsonl");

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedLearnerErrors = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["pedir-agua"] = ["conj.person"],
        ["si-por-favor"] = ["vocab.unknown"],
        ["no-gracias"] = ["gender.agree", "noun.number"],
        ["donde-esta-el-rio"] = ["gender.agree"],
        ["hola"] = ["noun.number"],
        ["gracias"] = ["gender.agree", "noun.number"],
        ["adios"] = ["conj.person"],
        ["no-se"] = ["conj.irregular"],
        ["ayuda"] = ["conj.person"],
        ["si"] = ["gender.agree"],
        ["no"] = ["gender.agree"],
        ["que-es-esto"] = ["vocab.wrong"],
        ["otra-vez"] = ["gender.agree"],
        ["mas-despacio"] = ["vocab.unknown"],
        ["quiero-madera"] = ["conj.person"],
        ["tengo-fruta"] = ["conj.irregular"],
        ["necesito-agua"] = ["conj.person"],
        ["ella-quiere-fruta"] = ["conj.person"],
        ["necesitamos-agua"] = ["conj.person"],
        ["necesito-pan"] = ["conj.person"],
        ["tengo-botella"] = ["conj.person"],
        ["queremos-pan"] = ["conj.person"],
        ["voy-a-comer-pan"] = ["conj.tense"],
        ["vamos-a-comer-fruta"] = ["conj.tense"],
        ["ayer-comi-fruta"] = ["conj.tense"],
        ["ayer-fui-al-rio"] = ["conj.tense"],
        ["ayer-bebi-agua"] = ["conj.tense"],
        ["voy-a-ir-al-rio"] = ["conj.tense"],
    };

    [Fact]
    public void IdsAreUniqueAndTheExtractionIsStable()
    {
        var ids = All.Select(static line => line.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, static id => Assert.Matches("^[^:\\s]+:\\S+$", id));

        var again = GrammarStringsExtractor.Extract(GameContent.Data, GameContent.BundleDirectory());
        Assert.Equal(GrammarStringsExtractor.ToJsonl(All), GrammarStringsExtractor.ToJsonl(again));
    }

    [Fact]
    public void TheCommittedStringsFileIsWhatTheExtractorWritesNow()
    {
        var committed = File.ReadAllText(StringsPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Assert.True(
            committed == GrammarStringsExtractor.ToJsonl(All),
            "content/grammar/game-v1-strings.jsonl is out of date: run dotnet run --project src/Vocaterra.Host -- --content content/game-v1 --grammar-strings content/grammar/game-v1-strings.jsonl");
    }

    [Fact]
    public void EveryBundleSourceFileContributesItsStrings()
    {
        var bundle = GameContent.Bundle;
        var ids = All.Select(static line => line.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var pool in bundle.Dialogue.Pools.Values)
        {
            foreach (var variant in pool.Variants)
            {
                Assert.Contains("dialogue:" + variant.LineId, ids);
            }
        }

        foreach (var prompt in bundle.Prompts.Values.Where(static prompt => prompt.Text is not null))
        {
            Assert.Contains("prompts:" + prompt.PromptId, ids);
        }

        foreach (var set in bundle.AnswerSets.Values)
        {
            Assert.True(File.Exists(Path.Combine(GameContent.BundleDirectory(), "answer-sets", set.AnswerSetId + ".json")), $"{set.AnswerSetId} is not a file.");
            foreach (var answer in set.Answers)
            {
                Assert.Contains($"answer-sets/{set.AnswerSetId}:{answer.AnswerId}", ids);
            }
        }

        foreach (var intent in bundle.Intents.Intents)
        {
            for (var i = 0; i < intent.ExampleSpecs.Count; i++)
            {
                Assert.Contains($"intents:{intent.Id.Value}.ex{i}", ids);
            }
        }

        // Each game file the strings come from has at least one line in the file.
        foreach (var prefix in new[] { "english.json:", "checks.json:", "PHRASES.md:", "speech:" })
        {
            Assert.Contains(All, line => line.Id.StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void EverySpeechLineTextIsChecked()
    {
        var texts = All.Select(static line => line.Text).ToHashSet(StringComparer.Ordinal);
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(ContentDirectory, "speech-index.json"), Encoding.UTF8));
        foreach (var file in index.RootElement.GetProperty("Files").EnumerateObject())
        {
            var text = file.Value.GetProperty("Text").GetString()!.Normalize(NormalizationForm.FormC);
            Assert.Contains(text, texts);
        }
    }

    [Fact]
    public void EveryLessonLineIsADialogueLineSoItIsCheckedWithTheDialogue()
    {
        // lessons.json names its spoken lines by dialogue id; their text is checked once, as the dialogue line (no second copy in the strings file).
        var lineIds = GameContent.Bundle.Dialogue.Pools.Values.SelectMany(static pool => pool.Variants).Select(static variant => variant.LineId).ToHashSet(StringComparer.Ordinal);
        using var lessons = JsonDocument.Parse(File.ReadAllText(Path.Combine(GameContent.BundleDirectory(), "game", "lessons.json"), Encoding.UTF8));
        var spoken = 0;
        foreach (var lesson in lessons.RootElement.GetProperty("lessons").EnumerateArray())
        {
            foreach (var step in lesson.GetProperty("steps").EnumerateArray())
            {
                if (step.TryGetProperty("line", out var line))
                {
                    spoken++;
                    Assert.Contains(line.GetString()!, lineIds);
                }
            }
        }

        Assert.True(spoken > 0, "lessons.json names no spoken line.");
    }

    [Fact]
    public void TheKindsFollowTheirSource()
    {
        foreach (var line in All)
        {
            if (line.Kind == GrammarStringsExtractor.Template)
            {
                Assert.Contains('{', line.Text);
            }
            else
            {
                Assert.DoesNotContain('{', line.Text);
            }
        }

        Assert.All(All.Where(static line => line.Id.StartsWith("answer-sets/", StringComparison.Ordinal)), static line => Assert.Equal("learner", line.Kind));
        Assert.All(All.Where(static line => line.Id.StartsWith("prompts:", StringComparison.Ordinal)), static line => Assert.Equal("npc", line.Kind));
        Assert.All(All.Where(static line => line.Id.StartsWith("intents:", StringComparison.Ordinal)), static line => Assert.Equal("learner", line.Kind));

        var byId = All.ToDictionary(static line => line.Id, StringComparer.Ordinal);
        Assert.Equal("npc", byId["PHRASES.md:quieres-pan.text"].Kind);
        Assert.Equal("learner", byId["PHRASES.md:pedir-agua.text"].Kind);
        Assert.Equal("learner", byId["PHRASES.md:pedir-agua.variant1"].Kind);
        Assert.Equal("npc", byId["english.json:lines[0]"].Kind);
    }

    [Fact]
    public void EveryLearnerErrorExampleCarriesItsTags()
    {
        var errors = All.Where(static line => line.Id.StartsWith("PHRASES.md:", StringComparison.Ordinal) && line.Id.EndsWith(".error", StringComparison.Ordinal)).ToList();
        Assert.Equal(ExpectedLearnerErrors.Count, errors.Count);
        foreach (var (id, tags) in ExpectedLearnerErrors)
        {
            var line = errors.Single(e => e.Id == "PHRASES.md:" + id + ".error");
            Assert.Equal("learner", line.Kind);
            Assert.Equal(tags, line.Errors!.ToArray());
        }
    }

    [Fact]
    public void EveryIntentExampleCarriesItsExpectedTags()
    {
        // An example with no tags is an oracle example that expects no error (an empty list), so every example has an errors array.
        foreach (var intent in GameContent.Bundle.Intents.Intents)
        {
            for (var i = 0; i < intent.ExampleSpecs.Count; i++)
            {
                var line = All.Single(l => l.Id == $"intents:{intent.Id.Value}.ex{i}");
                Assert.Equal(intent.ExampleSpecs[i].ExpectedErrors ?? [], line.Errors ?? []);
                Assert.NotNull(line.Errors);
            }
        }
    }

    [Fact]
    public void ThePhraseSheetLinesAreTheRuntimePhrasesAndVariants()
    {
        var runtime = RouteScene.DataOf(GameContent.Bundle).Phrases;
        var byId = All.ToDictionary(static line => line.Id, StringComparer.Ordinal);
        foreach (var row in runtime.Rows)
        {
            var text = byId["PHRASES.md:" + row.Id + ".text"];
            Assert.Equal(row.Spanish.Normalize(NormalizationForm.FormC), text.Text);
            for (var i = 0; i < row.Variants.Count; i++)
            {
                Assert.Equal(row.Variants[i].Normalize(NormalizationForm.FormC), byId[$"PHRASES.md:{row.Id}.variant{i + 1}"].Text);
            }
        }
    }

    [Fact]
    public void TheWaiverFileOnlyNamesStringsThatExist()
    {
        // A waiver for an id the strings file no longer has would be reported unused by the core; this keeps the file honest in the test run too.
        var ids = All.Select(static line => line.Id).ToHashSet(StringComparer.Ordinal);
        using var waivers = JsonDocument.Parse(File.ReadAllText(Path.Combine(ContentDirectory, "grammar", "game-v1-waivers.json"), Encoding.UTF8));
        foreach (var waiver in waivers.RootElement.GetProperty("waivers").EnumerateArray())
        {
            Assert.Contains(waiver.GetProperty("id").GetString()!, ids);
            Assert.False(string.IsNullOrWhiteSpace(waiver.GetProperty("reason").GetString()));
        }
    }

    [Fact]
    public void TheHostTakesTheGrammarStringsOption()
    {
        Assert.Equal("out.jsonl", HostOptions.Parse(["--grammar-strings", "out.jsonl"]).GrammarStringsOut);
        Assert.Null(HostOptions.Parse([]).GrammarStringsOut);
    }
}
