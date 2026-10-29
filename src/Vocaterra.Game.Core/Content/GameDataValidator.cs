using System.Globalization;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Nlp.Core.Taxonomy;

namespace Vocaterra.Game.Core.Content;

/// <summary>
/// The checks between the game's content files (CONTENT_PLAN 3.7, C02): what no single loader can see, because it needs another file. Every problem is
/// reported, not only the first, each naming the file and the id at fault, so one run of the host's <c>--validate-content</c> lists all there is to fix.
/// </summary>
/// <remarks>
/// <para>What is checked:</para>
/// <list type="bullet">
/// <item>the support language (the locale, C03): every string the content or the code asks for is there (the interface words, the outcomes, the NPCs'
/// roles, the objective's nouns, each phrase's explanation, each NPC line's gloss), and every string it holds is asked for (no orphans);</item>
/// <item>the phrase sheet: every prompt a row names is a bundle prompt, and every recording a row names is a line of the bundle's dialogue;</item>
/// <item>the check battery: every item's phrase is a row of the sheet, a comprehension item plays a speakable line, and a production item is graded at a
/// bundle prompt on a phrase the learner says;</item>
/// <item>the target-language words (C05): the key words and the river are catalogue nouns, the near future's verb is in the lexicon, and the greeting is a
/// phrase of the sheet;</item>
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
        CheckLocale(data, problems);
        CheckPhrases(data, problems);
        CheckChecks(data, problems);
        CheckScene(data, problems);
        CheckWords(data, problems);
        CheckNpcs(data, problems);
        return problems;
    }

    private static void CheckLocale(GameData data, List<string> problems)
    {
        var locale = data.Locale;
        var file = $"locale {locale.Code}";
        var required = new List<string>();
        required.AddRange(LocaleText.UiKeys.Select(static key => "ui." + key));
        required.AddRange(LocaleText.OutcomeKeys.Select(static key => "feedback.outcome." + key));
        required.Add("feedback.unknownError");
        required.Add("objective." + LocaleText.CampObjective + ".title");
        required.AddRange(data.Npcs.Ids.Select(static npc => "npc." + npc + ".role"));
        required.AddRange(Objective.Camp().Steps.Select(static step => "noun." + step.Noun));
        required.AddRange(data.Phrases.Rows.Select(static row => PhraseSheet.LocaleId(row.Id, "explain")));
        required.AddRange(data.Speech.Where(static line => line.Kind is SpeechKind.Dialogue or SpeechKind.Prompt).Select(static line => "line." + line.LineId));
        foreach (var id in required.Where(id => !locale.Has(id)))
        {
            problems.Add($"{file}: the string '{id}' is missing.");
        }

        // An orphan is a string the locale holds that no content or code asks for: a renamed id leaves one behind.
        var allowed = new HashSet<string>(required, StringComparer.Ordinal);
        allowed.UnionWith(ErrorTags.All.Select(static tag => "feedback.error." + tag));
        allowed.UnionWith(data.Nouns.Nouns.Select(static noun => "noun." + noun.Lemma));
        allowed.UnionWith(data.Phrases.Rows.SelectMany(static row => new[] { PhraseSheet.LocaleId(row.Id, "meaning"), PhraseSheet.LocaleId(row.Id, "card") }));
        allowed.UnionWith(data.Tips.Select(static tip => "tip." + tip.Id));
        allowed.UnionWith(data.Controls.SelectMany(static control => new[] { "control." + control.Id + ".keys", "control." + control.Id + ".text" }));
        foreach (var lesson in data.Lessons.Lessons)
        {
            allowed.UnionWith(new[] { "title", "text", "summary", "objective" }.Select(field => LessonBook.LessonId(lesson.Id, field)));
            foreach (var step in lesson.Steps)
            {
                allowed.UnionWith(new[] { "text", "translation", "help", "retry", "personRetry", "tenseRetry", "presentNote", "hud" }.Select(field => LessonBook.StepId(lesson.Id, step.Id, field)));
                allowed.UnionWith(Enumerable.Range(1, LessonBook.MaxOptions).Select(n => LessonBook.StepId(lesson.Id, step.Id, "option." + n.ToString(CultureInfo.InvariantCulture))));
            }
        }

        foreach (var item in data.Checks)
        {
            allowed.UnionWith(new[] { "question", "situation", "option.1", "option.2", "option.3" }.Select(field => CheckBank.LocaleId(item.Id, field)));
        }

        foreach (var id in locale.Ids.Where(id => !allowed.Contains(id) && !id.StartsWith("verb.", StringComparison.Ordinal)))
        {
            problems.Add($"{file}: the string '{id}' is asked for by no content or code (an orphan).");
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

    private static void CheckWords(GameData data, List<string> problems)
    {
        const string File = "game/words.json";
        foreach (var lemma in data.Words.KeyWords.Where(lemma => data.Nouns.ForLemma(lemma) is null))
        {
            problems.Add($"{File}: the key word '{lemma}' is not a catalogue noun.");
        }

        foreach (var role in GameWords.NounRoles.Where(role => data.Nouns.ForLemma(data.Words.Noun(role)) is null))
        {
            problems.Add($"{File}: the {role} noun '{data.Words.Noun(role)}' is not a catalogue noun.");
        }

        if (!data.Bundle.Lexicon.ContainsLemma(data.Words.NearFuture.Verb))
        {
            problems.Add($"{File}: the near future's verb '{data.Words.NearFuture.Verb}' is not in the lexicon.");
        }

        if (data.Phrases.Find(data.Words.Greeting) is null)
        {
            problems.Add($"{File}: the greeting '{data.Words.Greeting}' is not a phrase of the sheet (its id is the greeting).");
        }
    }

    private static void CheckNpcs(GameData data, List<string> problems)
    {
        // C08: the NPCs of game/npcs.json are the core's agents, their focus is the phrases and prompts of the bundle, and every NPC a tip or a host event names
        // is one of them (the lesson loader checks the lessons' own).
        foreach (var npc in data.Npcs.All)
        {
            var file = NpcRoster.Folder + npc.Id + ".json";
            if (!data.Bundle.Agents.ContainsKey(npc.Id))
            {
                problems.Add($"{file}: the NPC has no agent (agents/{npc.Id}.json).");
            }

            foreach (var (prompt, phrases) in npc.Focus)
            {
                if (!data.Bundle.Prompts.TryGetValue(prompt, out var spec) || !string.Equals(spec.AgentId ?? NpcService.GuideAgent, npc.Id, StringComparison.Ordinal))
                {
                    problems.Add($"{file}: the focus names the prompt '{prompt}', which is not one of this NPC's prompts.");
                }

                foreach (var phrase in phrases.Where(phrase => data.Phrases.Find(phrase) is not { IsLearnerPhrase: true }))
                {
                    problems.Add($"{file}: the focus at '{prompt}' names '{phrase}', which is not a phrase the learner says.");
                }
            }
        }

        foreach (var agent in data.Bundle.Agents.Keys.Where(agent => data.Npcs.Find(agent) is null).Order(StringComparer.Ordinal))
        {
            problems.Add($"{NpcRoster.IndexPath}: the agent '{agent}' has no NPC file.");
        }

        foreach (var tip in data.Tips.Where(tip => tip.Trigger.Npc is { } npc && data.Npcs.Find(npc) is null))
        {
            problems.Add($"{TipBook.TipsPath}: the tip '{tip.Id}' waits at the NPC '{tip.Trigger.Npc}', which is not one of the NPCs.");
        }

        foreach (var (npc, hostEvent) in data.HostEvents.Mapped().Where(entry => data.Npcs.Find(entry.Npc) is null))
        {
            problems.Add($"{HostEventTable.RelativePath}: '{hostEvent}' is raised by '{npc}', which is not one of the NPCs.");
        }

        foreach (var (npc, hostEvent) in data.HostEvents.Mapped())
        {
            if (data.HostEvents.Of(npc, hostEvent) is { Kind: NpcEffectKind.PointAtNoun, Noun: var noun } && data.Nouns.ForLemma(noun) is null)
            {
                problems.Add($"{HostEventTable.RelativePath}: {npc}'s '{hostEvent}' points at '{noun}', which is not a catalogue noun.");
            }
        }
    }

    private static void CheckScene(GameData data, List<string> problems)
    {
        const string File = "game/scene.json";
        var placed = data.Scene.Npcs.Select(static npc => npc.Name).ToList();
        foreach (var name in placed.Where(name => !data.Npcs.Ids.Contains(name, StringComparer.Ordinal)))
        {
            problems.Add($"{File}: the scene places the NPC '{name}', which is not one of the NPCs ({string.Join(", ", data.Npcs.Ids)}).");
        }

        foreach (var name in placed.GroupBy(static name => name, StringComparer.Ordinal).Where(static group => group.Count() > 1).Select(static group => group.Key))
        {
            problems.Add($"{File}: the scene places the NPC '{name}' more than once.");
        }

        foreach (var agent in data.Npcs.Ids.Where(agent => !placed.Contains(agent, StringComparer.Ordinal)))
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
