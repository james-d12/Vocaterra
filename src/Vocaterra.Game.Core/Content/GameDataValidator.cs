using System.Globalization;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Npc;

namespace Vocaterra.Game.Core.Content;

/// <summary>
/// The checks between the game's content files (CONTENT_PLAN 3.7, C02): what no single loader can see, because it needs another file. Every problem is
/// reported, not only the first, each naming the file and the id at fault, so one run of the host's <c>--validate-content</c> lists all there is to fix.
/// </summary>
/// <remarks>
/// <para>What is checked:</para>
/// <list type="bullet">
/// <item>the English text: every explained phrase is a row of the sheet, every noun it glosses (and every noun of the objective) is a catalogue noun,
/// and every NPC it names is one of the game's agents;</item>
/// <item>the phrase sheet: every prompt a row names is a bundle prompt, and every recording a row names is a line of the bundle's dialogue;</item>
/// <item>the check battery: every item's phrase is a row of the sheet, a comprehension item plays a speakable line, and a production item is graded at a
/// bundle prompt on a phrase the learner says;</item>
/// <item>the scene: every NPC it places is an agent the game drives, and every agent is placed once; every NPC a lesson is said or waits at is placed.</item>
/// </list>
/// <para>Plain values, ordinal comparisons; the order of the problems is the order of the files above.</para>
/// </remarks>
public static class GameDataValidator
{
    /// <summary>Every problem between the files, in a fixed order; empty when the content holds together.</summary>
    /// <param name="data">The content, read without the checks (<see cref="GameData.Read"/>) or loaded.</param>
    public static IReadOnlyList<string> Validate(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var problems = new List<string>();
        CheckEnglish(data, problems);
        CheckPhrases(data, problems);
        CheckChecks(data, problems);
        CheckScene(data, problems);
        return problems;
    }

    private static void CheckEnglish(GameData data, List<string> problems)
    {
        const string File = "game/english.json";
        foreach (var id in data.English.ExplainedPhrases.Where(id => data.Phrases.Find(id) is null))
        {
            problems.Add($"{File}: the explanation names the phrase '{id}', which is not in the phrase sheet.");
        }

        foreach (var lemma in data.English.GlossedNouns.Where(lemma => data.Nouns.ForLemma(lemma) is null))
        {
            problems.Add($"{File}: the noun '{lemma}' has English but is not a catalogue noun.");
        }

        foreach (var (step, lemma) in data.English.ObjectiveNouns.Where(entry => data.Nouns.ForLemma(entry.Noun) is null))
        {
            problems.Add($"{File}: the objective step '{step}' names the noun '{lemma}', which is not a catalogue noun.");
        }

        foreach (var npc in data.English.NamedNpcs.Where(npc => !NpcService.AgentIds.Contains(npc, StringComparer.Ordinal)))
        {
            problems.Add($"{File}: npcNames names '{npc}', which is not one of the NPCs ({string.Join(", ", NpcService.AgentIds)}).");
        }
    }

    private static void CheckPhrases(GameData data, List<string> problems)
    {
        const string File = "game/phrases.json";
        var dialogueLines = data.Bundle.Dialogue.Pools.Values.SelectMany(static pool => pool.Variants).Select(static v => v.LineId).ToHashSet(StringComparer.Ordinal);
        foreach (var row in data.Phrases.Rows)
        {
            if (row.Prompt is { } prompt && !data.Bundle.Prompts.ContainsKey(prompt))
            {
                problems.Add($"{File}: the phrase '{row.Id}' belongs to the prompt '{prompt}', which the bundle does not have.");
            }

            foreach (var line in row.AudioLineIds.Where(line => !dialogueLines.Contains(line)))
            {
                problems.Add($"{File}: the phrase '{row.Id}' is spoken by the line '{line}', which is not a line of the bundle's dialogue.");
            }
        }
    }

    private static void CheckChecks(GameData data, List<string> problems)
    {
        const string File = "game/checks.json";
        var speech = data.Speech.Select(static line => line.LineId).ToHashSet(StringComparer.Ordinal);
        foreach (var item in data.Checks)
        {
            var row = data.Phrases.Find(item.Phrase);
            if (row is null)
            {
                problems.Add($"{File}: item '{item.Id}' names the phrase '{item.Phrase}', which is not in the phrase sheet.");
            }

            switch (item.Kind)
            {
                case CheckItemKind.Comprehension when !speech.Contains(item.Line):
                    problems.Add($"{File}: comprehension item '{item.Id}' plays the line '{item.Line}', which has no pre-rendered speech line in the bundle.");
                    break;
                case CheckItemKind.Production:
                    if (!data.Bundle.Prompts.ContainsKey(item.Prompt))
                    {
                        problems.Add($"{File}: production item '{item.Id}' is graded at the prompt '{item.Prompt}', which the bundle does not have.");
                    }

                    if (row is { IsLearnerPhrase: false })
                    {
                        problems.Add($"{File}: production item '{item.Id}' names '{item.Phrase}', a phrase the learner does not say.");
                    }

                    break;
                default:
                    break;
            }
        }
    }

    private static void CheckScene(GameData data, List<string> problems)
    {
        const string File = "game/scene.json";
        var placed = data.Scene.Npcs.Select(static npc => npc.Name).ToList();
        foreach (var name in placed.Where(name => !NpcService.AgentIds.Contains(name, StringComparer.Ordinal)))
        {
            problems.Add($"{File}: the scene places the NPC '{name}', which is not one of the NPCs ({string.Join(", ", NpcService.AgentIds)}).");
        }

        foreach (var name in placed.GroupBy(static name => name, StringComparer.Ordinal).Where(static group => group.Count() > 1).Select(static group => group.Key))
        {
            problems.Add($"{File}: the scene places the NPC '{name}' more than once.");
        }

        foreach (var agent in NpcService.AgentIds.Where(agent => !placed.Contains(agent, StringComparer.Ordinal)))
        {
            problems.Add($"{File}: the scene does not place the NPC '{agent}'.");
        }

        foreach (var lesson in data.Lessons.Lessons)
        {
            foreach (var npc in lesson.Steps.SelectMany(static step => new[] { step.Npc, step.At }).Prepend(lesson.Npc).OfType<string>().Distinct(StringComparer.Ordinal))
            {
                if (!placed.Contains(npc, StringComparer.Ordinal))
                {
                    problems.Add(string.Create(CultureInfo.InvariantCulture, $"game/lessons.json: the lesson '{lesson.Id}' is said at '{npc}', whom the scene does not place."));
                }
            }
        }
    }
}
