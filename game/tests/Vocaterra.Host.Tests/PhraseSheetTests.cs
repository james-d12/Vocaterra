using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Host.Tests;

/// <summary>
/// <c>content/PHRASES.md</c> is the source of truth for the prototype's Spanish (D-game-10). These tests read the sheet and hold it to the
/// bundle <c>game-v1</c>: the two are one set. A row that drifts from the bundle fails here, whichever side was edited.
/// </summary>
public sealed class PhraseSheetTests
{
    private static readonly StoredTimestamp Now = new(1_760_000_000);

    private static readonly string[] ExpectedIds =
    [
        "que-necesitas", "pedir-agua", "quieres-pan", "si-por-favor", "no-gracias", "donde-esta-el-rio",
        "hola", "gracias", "adios", "no-se", "ayuda",
        "me-llamo", "es-madera", "si", "no", "que-es-esto", "quieres-fruta", "de-nada", "otra-vez", "mas-despacio", "necesitas-madera",
        "que-quieres", "quiero-madera", "tienes-madera", "tengo-madera", "necesito-agua",
        "que-quiere-luna", "ella-quiere-fruta", "necesitamos-agua", "que-necesita-usted", "necesito-pan", "tengo-botella", "que-quieren-ustedes", "queremos-pan",
    ];

    private static readonly Lazy<IReadOnlyList<PhraseRow>> LoadedRows = new(ReadRows);

    private static IReadOnlyList<PhraseRow> Rows => LoadedRows.Value;

    private static IEnumerable<PhraseRow> LearnerRows => Rows.Where(static r => r.Says is "says" or "both");

    private static IEnumerable<PhraseRow> HeardRows => Rows.Where(static r => r.Says is "hears" or "both");

    [Fact]
    public void TheSheetHoldsExactlyTheThirtyFourPhrasesInOrder()
    {
        Assert.Equal(ExpectedIds, Rows.Select(static r => r.Id));
        foreach (var row in Rows)
        {
            Assert.Matches("^[a-z]+(-[a-z]+)*$", row.Id);
            Assert.Contains(row.Says, new[] { "says", "hears", "both" });
            Assert.False(string.IsNullOrWhiteSpace(row.English), $"{row.Id} has no English.");
            Assert.False(string.IsNullOrWhiteSpace(row.Card), $"{row.Id} has no support card.");
            Assert.False(string.IsNullOrWhiteSpace(row.Situation), $"{row.Id} has no NPC and situation.");
            Assert.True(row.Reviewer == "pending" || row.Reviewer.StartsWith("llm-opus (provisional)", StringComparison.Ordinal) || row.Reviewer.StartsWith("native:", StringComparison.Ordinal),
                $"{row.Id}: reviewer '{row.Reviewer}'.");
            Assert.True(DateOnly.TryParseExact(row.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _), $"{row.Id}: date '{row.Date}'.");
        }
    }

    [Fact]
    public void TheSpanishKeepsItsAccentsAndInvertedMarks()
    {
        Assert.Equal("¿Qué necesitas?", Rows.Single(static r => r.Id == "que-necesitas").Spanish);
        Assert.Equal("Quiero agua, por favor.", Rows.Single(static r => r.Id == "pedir-agua").Spanish);
        Assert.Equal("¿Quieres pan?", Rows.Single(static r => r.Id == "quieres-pan").Spanish);
        Assert.Equal("Sí, por favor.", Rows.Single(static r => r.Id == "si-por-favor").Spanish);
        Assert.Equal("No, gracias.", Rows.Single(static r => r.Id == "no-gracias").Spanish);
        Assert.Equal("¿Dónde está el río?", Rows.Single(static r => r.Id == "donde-esta-el-rio").Spanish);
        Assert.Equal("Adiós.", Rows.Single(static r => r.Id == "adios").Spanish);
        Assert.Equal("No sé.", Rows.Single(static r => r.Id == "no-se").Spanish);
        Assert.All(Rows, static row => Assert.True(row.Spanish.IsNormalized(NormalizationForm.FormC), $"{row.Id} is not in NFC."));
    }

    [Fact]
    public void EveryLearnerPhraseAndVariantIsAnExampleOfItsIntent()
    {
        var bundle = GameContent.Bundle;
        foreach (var row in LearnerRows)
        {
            var intent = bundle.Intents.Intents.SingleOrDefault(i => i.Id.Value == row.Intent);
            Assert.True(intent is not null, $"{row.Id}: the bundle has no intent '{row.Intent}'.");
            var examples = intent.Examples.Select(Plain).ToHashSet(StringComparer.Ordinal);
            foreach (var text in row.Texts)
            {
                Assert.True(examples.Contains(Plain(text)), $"{row.Id}: '{text}' is not an example of intent {row.Intent} in intents.json.");
            }
        }
    }

    [Fact]
    public void EveryLearnerPhraseAndVariantResolvesToItsIntentAtItsPromptWithNoError()
    {
        foreach (var row in LearnerRows)
        {
            var prompt = GameContent.Bundle.Prompts[row.Prompt];
            Assert.Contains(row.Intent, prompt.AllowedIntentIds);
            foreach (var text in row.Texts)
            {
                var result = new ConversationSession(GameContent.Bundle).Evaluate(text, row.Prompt, Now);

                Assert.True(result.Verdict.Intent.Value == row.Intent, $"{row.Id}: '{text}' resolved to {result.Verdict.Intent.Value}, not {row.Intent}.");
                Assert.True(result.Verdict.Communicated, $"{row.Id}: '{text}' was not communicated.");
                Assert.True(result.Verdict.Errors.Count == 0,
                    $"{row.Id}: '{text}' has errors {string.Join(", ", result.Verdict.Errors.Select(static e => e.Tag))}.");
            }
        }
    }

    [Fact]
    public void EveryLearnerErrorExampleGivesExactlyTheCodesTheSheetNames()
    {
        foreach (var row in LearnerRows)
        {
            Assert.True(row.ErrorText is not null && row.ErrorCodes.Count > 0, $"{row.Id} has no learner error example.");
            var result = new ConversationSession(GameContent.Bundle).Evaluate(row.ErrorText!, row.Prompt, Now);

            Assert.Equal(row.ErrorCodes.Order(StringComparer.Ordinal), result.Verdict.Errors.Select(static e => e.Tag).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            Assert.True(result.Verdict.Intent.Value == row.Intent, $"{row.Id}: '{row.ErrorText}' resolved to {result.Verdict.Intent.Value}, not {row.Intent}.");
        }
    }

    [Fact]
    public void EveryHeardPhraseIsTheTextOfItsPromptOrOfADialogueLine()
    {
        foreach (var row in HeardRows)
        {
            var prompt = GameContent.Bundle.Prompts[row.Prompt];
            var lineTexts = row.AudioLineIds.Select(LineText).ToList();
            Assert.True(
                Plain(prompt.Text ?? string.Empty) == Plain(row.Spanish) || lineTexts.Any(t => Plain(t) == Plain(row.Spanish)),
                $"{row.Id}: '{row.Spanish}' is neither the text of prompt {row.Prompt} nor of a line in {string.Join(", ", row.AudioLineIds)}.");
            Assert.True(row.AudioLineIds.Count > 0, $"{row.Id} is heard but names no audio line.");
            Assert.True(lineTexts.Any(t => Plain(t) == Plain(row.Spanish)), $"{row.Id}: no audio line says exactly '{row.Spanish}'.");
        }
    }

    [Fact]
    public void EveryAudioLineIdExistsInTheBundleAndSaysItsPhrase()
    {
        foreach (var row in Rows)
        {
            foreach (var id in row.AudioLineIds)
            {
                var text = LineText(id);
                Assert.Contains(Plain(row.Spanish), Plain(text), StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void EveryAudioLineIsSlotFree()
    {
        foreach (var id in Rows.SelectMany(static r => r.AudioLineIds))
        {
            Assert.DoesNotContain('{', LineText(id));
        }
    }

    [Fact]
    public void TheRuntimeCopyOfTheSheetAgreesWithThisFileRowByRow()
    {
        // game/phrases.json is the machine-readable copy the game reads (S7, S8); content/PHRASES.md stays the source of truth.
        var runtime = Vocaterra.Game.PhraseSheet.Load(GameContent.Bundle);

        Assert.Equal(Rows.Select(static r => r.Id), runtime.Rows.Select(static r => r.Id));
        foreach (var row in Rows)
        {
            var copy = runtime.Find(row.Id)!;
            Assert.True(copy.Says == row.Says, $"{row.Id}: says '{copy.Says}' in phrases.json, '{row.Says}' in PHRASES.md.");
            Assert.True(copy.Spanish == row.Spanish, $"{row.Id}: Spanish '{copy.Spanish}' in phrases.json, '{row.Spanish}' in PHRASES.md.");
            Assert.True(copy.English == row.English, $"{row.Id}: English '{copy.English}' in phrases.json, '{row.English}' in PHRASES.md.");
            Assert.True(copy.Intent == (row.Intent == "-" ? null : row.Intent), $"{row.Id}: intent '{copy.Intent}' in phrases.json, '{row.Intent}' in PHRASES.md.");
            Assert.True(copy.Prompt == (row.Prompt == "-" ? null : row.Prompt), $"{row.Id}: prompt '{copy.Prompt}' in phrases.json, '{row.Prompt}' in PHRASES.md.");
            Assert.True(copy.Variants.SequenceEqual(row.Variants), $"{row.Id}: variants differ: [{string.Join(" ; ", copy.Variants)}] against [{string.Join(" ; ", row.Variants)}].");
            Assert.True(copy.Card == row.Card, $"{row.Id}: card '{copy.Card}' in phrases.json, '{row.Card}' in PHRASES.md.");
            Assert.True(copy.AudioLineIds.SequenceEqual(row.AudioLineIds), $"{row.Id}: audio line ids differ: [{string.Join(" ; ", copy.AudioLineIds)}] against [{string.Join(" ; ", row.AudioLineIds)}].");
        }
    }

    [Fact]
    public void ThePhraseOfEachCardIsThePhraseAndTheRuntimeFileIsListedAsHandAuthoredInTheProvenance()
    {
        var runtime = Vocaterra.Game.PhraseSheet.Load(GameContent.Bundle);
        Assert.All(runtime.Rows, row => Assert.Equal(row.Spanish, row.CardPhrase));

        var provenance = File.ReadAllText(Path.Combine(GameContent.BundleDirectory(), "provenance.json"));
        Assert.Contains("\"path\": \"game/phrases.json\", \"generatedBy\": \"hand-authored\"", provenance, StringComparison.Ordinal);
    }

    private static string LineText(string lineId)
    {
        foreach (var pool in GameContent.Bundle.Dialogue.Pools.Values)
        {
            foreach (var variant in pool.Variants)
            {
                if (variant.LineId == lineId)
                {
                    return variant.Text;
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"The bundle has no dialogue line '{lineId}'.");
    }

    // Case, punctuation and the inverted marks do not matter when a sheet text meets a bundle text; accents do.
    private static string Plain(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormC).ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string SheetPath()
    {
        var content = Directory.GetParent(GameContent.BundleDirectory())!.FullName;
        return Path.Combine(content, "PHRASES.md");
    }

    private static IReadOnlyList<PhraseRow> ReadRows()
    {
        var rows = new List<PhraseRow>();
        var inTable = false;
        foreach (var line in File.ReadAllLines(SheetPath(), Encoding.UTF8))
        {
            if (!line.StartsWith('|'))
            {
                if (inTable && rows.Count > 0)
                {
                    break;
                }

                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(static c => c.Trim()).ToArray();
            if (cells[0] == "id")
            {
                Assert.Equal(13, cells.Length);
                inTable = true;
                continue;
            }

            if (!inTable || cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.Equal(13, cells.Length);
            rows.Add(new PhraseRow(
                cells[0], cells[1], cells[2], cells[3], cells[4], cells[5], List(cells[6]), cells[7], cells[8], cells[9], List(cells[10]), cells[11], cells[12]));
        }

        return rows;
    }

    private static IReadOnlyList<string> List(string cell)
    {
        return cell == "-" ? [] : cell.Split(" ; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private sealed record PhraseRow(
        string Id,
        string Says,
        string Spanish,
        string English,
        string Intent,
        string Prompt,
        IReadOnlyList<string> Variants,
        string LearnerError,
        string Situation,
        string Card,
        IReadOnlyList<string> AudioLineIds,
        string Reviewer,
        string Date)
    {
        /// <summary>The phrase and its accepted variants.</summary>
        public IEnumerable<string> Texts => Variants.Prepend(Spanish);

        /// <summary>The text of the learner error example: <c>text (code, code)</c>.</summary>
        public string? ErrorText => LearnerError == "-" ? null : LearnerError[..LearnerError.LastIndexOf('(')].Trim();

        /// <summary>The error codes of the learner error example.</summary>
        public IReadOnlyList<string> ErrorCodes => LearnerError == "-"
            ? []
            : LearnerError[(LearnerError.LastIndexOf('(') + 1)..].TrimEnd(')').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
