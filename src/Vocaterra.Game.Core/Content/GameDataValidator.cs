using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Response.Templates;

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
/// <item>the target-language words (C05): the river is a catalogue noun, the near future's verb is in the lexicon, and the greeting is a phrase of the
/// sheet;</item>
/// <item>the NPCs (C08, schema 2 since VILLAGE_PLAN V01): each is an agent of the bundle; its focus, its prompts by state and its review prompt are prompts of
/// that agent, the states it names are the agent's, and its initial state has a prompt; its opening event is one a state of the agent takes; it has a voice
/// with a name; its key words are catalogue nouns; no two share a pool prefix;</item>
/// <item>the scene: every NPC it places is an agent the game drives, and every agent is placed once; every NPC a lesson is said or waits at is placed;</item>
/// <item>the host actions (VILLAGE_PLAN V02, V03): every place a host event (<c>point_at_place</c>) or a lesson's <c>do</c> step (<c>pointPlace</c>) sends the beacon
/// to is a point of the scene; an NPC whose directions name the place of the learner's line (<c>point_at_slot</c>) has an agent that can say it does not know one
/// (<c>&lt;npc&gt;.unknown_place</c>), an NPC that sells has one that can say the coins are too few (<c>&lt;npc&gt;.cant_pay</c>) and one that can say it sells another item (V15,
/// <c>&lt;npc&gt;.wont_sell</c>), and an NPC that buys (V15) one that can
/// say the learner has none to sell (<c>&lt;npc&gt;.cant_sell</c>) and one that can say it buys another item (<c>&lt;npc&gt;.wont_buy</c>); and (V19) an NPC that sells or
/// buys one that can say the bag is full (<c>&lt;npc&gt;.no_room</c>), and one that can answer a request for none (<c>&lt;npc&gt;.none_asked</c>) or an offer of none
/// (<c>&lt;npc&gt;.none_offered</c>);</item>
/// <item>the orders (V03): the NPC an order is brought to is one of the NPCs, and every state of its agent takes its thanks (<c>&lt;npc&gt;.delivered</c>), so the
/// goods are never brought in vain; the lesson an order names as the one that tells it (V13) is a lesson with a <c>do</c> step that waits for that delivery;</item>
/// <item>the lessons' levels (VILLAGE_PLAN 3.9, V08, D-game-22 (b)): the Spanish a lesson teaches and says (its phrases, the lines its steps name, the text of
/// the prompts its turns are evaluated at) has no lemma above its level, <c>a1</c> or <c>a2</c>, in the bundle's cumulative CEFR lists (<c>cefr/a1.txt</c>,
/// <c>cefr/a2.txt</c>), and its phrases' variants none above the bundle's ceiling (<see cref="LessonLevels.Ceiling"/>), lemmatized by the core's
/// <see cref="CefrChecker"/> as the content lint does; and the tenses its verbs and steps name are its level's (<see cref="LessonLevels.TensesAt"/>: no
/// <c>fut</c> in an <c>a1</c> lesson).</item>
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
        CheckHostActions(data, problems);
        CheckObjectives(data, problems);
        CheckLessonLevels(data, problems);
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
        required.AddRange(data.Objectives.Objectives.Select(static o => "objective." + o.Id + (o.IsChecklist ? ".title" : ".hud")));
        required.AddRange(data.Npcs.Ids.Select(static npc => "npc." + npc + ".role"));
        required.AddRange(data.Objectives.Objectives.Where(static o => o.IsChecklist).SelectMany(static o => o.Steps).Select(static step => "noun." + step.Noun));
        required.AddRange(data.Phrases.Rows.Select(static row => PhraseSheet.LocaleId(row.Id, "explain")));
        required.AddRange(data.Speech.Where(static line => line.Kind is SpeechKind.Dialogue or SpeechKind.Prompt).Select(static line => "line." + line.LineId));
        required.AddRange(InputKeys.Defaults.Values.Distinct().Select(static key => "key." + key.ToString())); // C04: {key:…} spells a command's key with these
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
                allowed.UnionWith(new[] { "text", "translation", "help", "retry", "personRetry", "tenseRetry", "presentNote", "futureNote", "hud" }.Select(field => LessonBook.StepId(lesson.Id, step.Id, field)));
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

        // C04: a {npc:…} names an NPC of game/npcs.json and a {key:…} a command of InputKeys, or the text would show the placeholder itself.
        foreach (var id in locale.Ids.Where(static id => !id.StartsWith("verb.", StringComparison.Ordinal)))
        {
            foreach (var (kind, name) in LocaleText.NamePlaceholders(locale.Raw(id) ?? string.Empty))
            {
                if (kind == "npc" ? data.Npcs.Find(name) is null : !InputKeys.Defaults.ContainsKey(name))
                {
                    problems.Add($"{file}: the string '{id}' names {{{kind}:{name}}}, which is not {(kind == "npc" ? "one of the NPCs" : "a command with a key")}.");
                }
            }
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

            if (item.Lesson is { } lesson && data.Lessons.Find(lesson) is null)
            {
                problems.Add($"{File}: item '{item.Id}' belongs to the lesson '{lesson}', which is not a lesson ({string.Join(", ", data.Lessons.Lessons.Select(static l => l.Id))}).");
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

    private static void CheckObjectives(GameData data, List<string> problems)
    {
        // C07: the hooks an objective names are the code's, the objectives it is also done by exist, its triggers' NPCs are the roster's, and the session's closing
        // pool and beacon start are the bundle's and the scene's.
        var book = data.Objectives;
        foreach (var objective in book.Objectives)
        {
            var file = ObjectiveBook.Folder + objective.Id + ".json";
            foreach (var hook in objective.OnComplete.Where(static hook => !ObjectiveBook.CompleteHooks.Contains(hook, StringComparer.Ordinal)))
            {
                problems.Add($"{file}: onComplete names the hook '{hook}', which the game does not have ({string.Join(", ", ObjectiveBook.CompleteHooks)}).");
            }

            foreach (var step in objective.Steps)
            {
                if (step.Restore is { } hook && !ObjectiveBook.RestoreHooks.Contains(hook, StringComparer.Ordinal))
                {
                    problems.Add($"{file}: the step '{step.Id}' names the restore hook '{hook}', which the game does not have ({string.Join(", ", ObjectiveBook.RestoreHooks)}).");
                }

                if (step.Trigger.Npc is { } npc && data.Npcs.Find(npc) is null && !string.Equals(npc, objective.DeliverTo, StringComparison.Ordinal))
                {
                    problems.Add($"{file}: the step '{step.Id}' waits at the NPC '{npc}', which is not one of the NPCs.");
                }
            }

            CheckOrder(data, objective, file, problems);

            foreach (var other in objective.AlsoDoneBy.Where(other => string.Equals(other, objective.Id, StringComparison.Ordinal) || book.Find(other) is null))
            {
                problems.Add($"{file}: alsoDoneBy names '{other}', which is not another objective of the session.");
            }
        }

        if (!data.Bundle.Dialogue.TryGetPoolById(book.ClosingPool, out var pool) || pool.Variants.Count == 0)
        {
            problems.Add($"{ObjectiveBook.SessionPath}: the closing pool '{book.ClosingPool}' is not a dialogue pool with a line.");
        }

        if (!data.Scene.Sites.Any(site => site.Name == book.BeaconStart) && !data.Scene.Points.Any(point => point.Name == book.BeaconStart))
        {
            problems.Add($"{ObjectiveBook.SessionPath}: the beacon starts on '{book.BeaconStart}', which is not a site or a point of the scene.");
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

            // V12b: a prompt the NPC waits on that is the home of phrases the learner practises (their row's prompt) names one of them in the focus there, or free talk
            // there shows no card and gives none of them a scaffold (the four village NPCs shipped so, and the baker's request was credited as the stall's).
            foreach (var prompt in npc.Prompts.Values.Distinct(StringComparer.Ordinal))
            {
                var homes = data.Phrases.Rows.Where(row => string.Equals(row.Prompt, prompt, StringComparison.Ordinal) && data.Phrases.IsPractisable(row.Id)).Select(static row => row.Id).ToList();
                if (homes.Count > 0 && !(npc.Focus.TryGetValue(prompt, out var focus) && focus.Any(homes.Contains)))
                {
                    problems.Add($"{file}: the prompt '{prompt}' is the home of {string.Join(", ", homes)} ({PhraseSheet.RelativePath}), and the focus names none of them there.");
                }
            }

            CheckNpcBehaviour(data, npc, file, problems);
        }

        foreach (var group in data.Npcs.All.GroupBy(static npc => npc.PoolPrefix, StringComparer.Ordinal).Where(static group => group.Count() > 1))
        {
            problems.Add($"{NpcRoster.Folder}{group.Last().Id}.json: the pool prefix '{group.Key}' is also {group.First().Id}'s, so its pools would speak with one voice.");
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

    private static void CheckHostActions(GameData data, List<string> problems)
    {
        var places = data.Scene.PointNames.ToHashSet(StringComparer.Ordinal);
        foreach (var (npc, hostEvent) in data.HostEvents.Mapped())
        {
            var effect = data.HostEvents.Of(npc, hostEvent);
            if (effect.Kind == NpcEffectKind.PointAtPlace && !places.Contains(effect.Place))
            {
                problems.Add($"{HostEventTable.RelativePath}: {npc}'s '{hostEvent}' points at the place '{effect.Place}', which is not a point of the scene.");
            }

            if (effect.Kind == NpcEffectKind.PointAtSlot && data.Bundle.Agents.ContainsKey(npc) && !TakenBySomeState(data, npc, NpcHostEvents.Of(npc, NpcHostEvents.UnknownPlace)))
            {
                problems.Add($"{HostEventTable.RelativePath}: {npc}'s '{hostEvent}' points at the place the learner names, and no state of agents/{npc}.json takes '{NpcHostEvents.Of(npc, NpcHostEvents.UnknownPlace)}' to say it does not know one.");
            }

            if (effect.Kind == NpcEffectKind.Sell && data.Bundle.Agents.ContainsKey(npc) && !TakenBySomeState(data, npc, NpcHostEvents.Of(npc, NpcHostEvents.CantPay)))
            {
                problems.Add($"{HostEventTable.RelativePath}: {npc}'s '{hostEvent}' sells, and no state of agents/{npc}.json takes '{NpcHostEvents.Of(npc, NpcHostEvents.CantPay)}' to say the coins are too few.");
            }

            if (effect.Kind == NpcEffectKind.Sell && data.Bundle.Agents.ContainsKey(npc) && !TakenBySomeState(data, npc, NpcHostEvents.Of(npc, NpcHostEvents.WontSell)))
            {
                problems.Add($"{HostEventTable.RelativePath}: {npc}'s '{hostEvent}' sells, and no state of agents/{npc}.json takes '{NpcHostEvents.Of(npc, NpcHostEvents.WontSell)}' to say it sells another item.");
            }

            if (effect.Kind == NpcEffectKind.Sell && data.Bundle.Agents.ContainsKey(npc) && !TakenBySomeState(data, npc, NpcHostEvents.Of(npc, NpcHostEvents.NoneAsked)))
            {
                problems.Add($"{HostEventTable.RelativePath}: {npc}'s '{hostEvent}' sells, and no state of agents/{npc}.json takes '{NpcHostEvents.Of(npc, NpcHostEvents.NoneAsked)}' to answer a request for none.");
            }

            if (effect.Kind is NpcEffectKind.Sell or NpcEffectKind.Buy && data.Bundle.Agents.ContainsKey(npc) && !TakenBySomeState(data, npc, NpcHostEvents.Of(npc, NpcHostEvents.NoRoom)))
            {
                problems.Add($"{HostEventTable.RelativePath}: {npc}'s '{hostEvent}' {(effect.Kind == NpcEffectKind.Sell ? "sells" : "buys")}, and no state of agents/{npc}.json takes '{NpcHostEvents.Of(npc, NpcHostEvents.NoRoom)}' to say the bag is full.");
            }

            if (effect.Kind == NpcEffectKind.Buy && data.Bundle.Agents.ContainsKey(npc))
            {
                if (!TakenBySomeState(data, npc, NpcHostEvents.Of(npc, NpcHostEvents.CantSell)))
                {
                    problems.Add($"{HostEventTable.RelativePath}: {npc}'s '{hostEvent}' buys, and no state of agents/{npc}.json takes '{NpcHostEvents.Of(npc, NpcHostEvents.CantSell)}' to say the learner has none to sell.");
                }

                if (!TakenBySomeState(data, npc, NpcHostEvents.Of(npc, NpcHostEvents.WontBuy)))
                {
                    problems.Add($"{HostEventTable.RelativePath}: {npc}'s '{hostEvent}' buys, and no state of agents/{npc}.json takes '{NpcHostEvents.Of(npc, NpcHostEvents.WontBuy)}' to say it does not buy another item.");
                }

                if (!TakenBySomeState(data, npc, NpcHostEvents.Of(npc, NpcHostEvents.NoneOffered)))
                {
                    problems.Add($"{HostEventTable.RelativePath}: {npc}'s '{hostEvent}' buys, and no state of agents/{npc}.json takes '{NpcHostEvents.Of(npc, NpcHostEvents.NoneOffered)}' to answer an offer of none.");
                }
            }
        }

        foreach (var lesson in data.Lessons.Lessons)
        {
            foreach (var step in lesson.Steps.Where(step => step.PointPlace is { } place && !places.Contains(place)))
            {
                problems.Add($"{LessonBook.Folder}{lesson.Id}.json: the step '{step.Id}' points at the place '{step.PointPlace}', which is not a point of the scene.");
            }
        }
    }

    // VILLAGE_PLAN 3.9, V08 (D-game-22 (b)): the bundle's ceiling is A2 (the gate's lint), and each lesson promises its own level. The lists hold lemmas, so a
    // form's tense is invisible to them: the tenses the lesson's verbs and steps name are checked against the level too.
    private static void CheckLessonLevels(GameData data, List<string> problems)
    {
        foreach (var lesson in data.Lessons.Lessons)
        {
            var file = LessonBook.Folder + lesson.Id + ".json";
            var tenses = LessonLevels.TensesAt(lesson.Level);
            var allowed = string.Join(", ", tenses);
            foreach (var form in lesson.TaughtVerbs.Where(form => !tenses.Contains(form.Tense, StringComparer.Ordinal)))
            {
                problems.Add($"{file}: the {lesson.Level} lesson declares the verb form {form.Lemma} {form.Person} {form.Number} {form.Tense}; an {lesson.Level} lesson teaches {allowed}.");
            }

            foreach (var step in lesson.Steps.Where(step => step.Tense is { } tense && !tenses.Contains(tense, StringComparer.Ordinal)))
            {
                problems.Add($"{file}: the {lesson.Level} lesson's step '{step.Id}' expects the tense {step.Tense}; an {lesson.Level} lesson teaches {allowed}.");
            }

            if (!data.Bundle.Cefr.ContainsKey(lesson.Level))
            {
                problems.Add($"{file}: the lesson is {lesson.Level}, and the bundle has no list of that level (cefr/{lesson.Level}.txt).");
                continue;
            }

            problems.AddRange(WordsAboveLevel(data, lesson, file));
        }
    }

    // The lemmas of a lesson's Spanish above the level each text is held to, one problem for each text and lemma, in the order of the steps (the prompts last).
    // The phrases, the lines and the prompts' text are what the lesson teaches and says, held to its level; a phrase's variants are what the learner may say
    // instead, which the lesson does not teach, so they are held to the bundle's ceiling, as the core's lint holds an answer set's aliases to none. The texts are
    // lemmatized by the core's CefrChecker exactly as the lint lemmatizes the dialogue and the prompts' text: the phrases, the variants and the lines as the
    // lines of a pool of plain text, the prompts as prompts.
    private static List<string> WordsAboveLevel(GameData data, LessonDefinition lesson, string file)
    {
        var bundle = data.Bundle;
        var texts = new List<(string What, string Text, string Level)>();
        var prompts = new SortedDictionary<string, PromptSpec>(StringComparer.Ordinal);
        void Add(string what, string text, string level)
        {
            var known = texts.FindIndex(known => string.Equals(known.Text, text, StringComparison.Ordinal));
            if (known < 0)
            {
                texts.Add((what, text, level));
            }
            else if (string.CompareOrdinal(level, texts[known].Level) < 0)
            {
                texts[known] = (what, text, level); // the same words said by the lesson and allowed as a variant: the stricter level
            }
        }

        foreach (var step in lesson.Steps)
        {
            var asked = step.Candidates is { } candidates
                ? candidates.Select(static c => (Phrase: (string?)c.Phrase, Line: (SpeechLine?)c.Line, Prompt: (string?)c.Prompt))
                : [(step.Phrase, step.Line, step.Prompt)];
            foreach (var (phrase, line, prompt) in asked)
            {
                if (phrase is not null && data.Phrases.Find(phrase) is { } row)
                {
                    Add($"the phrase '{row.Id}'", row.Spanish, lesson.Level);
                    foreach (var variant in row.Variants)
                    {
                        Add($"a variant of the phrase '{row.Id}'", variant, LessonLevels.Ceiling);
                    }
                }

                if (line is not null)
                {
                    Add($"the line '{line.LineId}'", line.Text, lesson.Level);
                }

                if (prompt is not null && bundle.Prompts.TryGetValue(prompt, out var spec) && !string.IsNullOrWhiteSpace(spec.Text))
                {
                    prompts.TryAdd(prompt, spec);
                }
            }
        }

        var found = new List<(int Order, string Problem)>();
        foreach (var level in texts.Select(static t => t.Level).Append(lesson.Level).Distinct(StringComparer.Ordinal).Where(bundle.Cefr.ContainsKey))
        {
            var held = texts.Select(static (text, index) => (text.Text, text.Level, Index: index)).Where(t => string.Equals(t.Level, level, StringComparison.Ordinal)).ToList();
            var catalog = held.Count == 0
                ? DialogueCatalog.Empty
                : new DialogueCatalog([new(lesson.Id, new VariantPool(lesson.Id, held.Select(static t => new DialogueTemplate(t.Index.ToString(CultureInfo.InvariantCulture), t.Text)).ToList()))]);
            var atLessonLevel = string.Equals(level, lesson.Level, StringComparison.Ordinal);
            var report = CefrChecker.Check(catalog, bundle.Lexicon, bundle.Language, CefrChecker.AllowedLemmas(bundle.Cefr, level), level, atLessonLevel ? prompts : null);
            var lists = bundle.Cefr.Keys.Where(key => string.CompareOrdinal(key, level) <= 0).Order(StringComparer.Ordinal).Select(static key => $"cefr/{key}.txt").ToList();
            var notListed = lists.Count == 1 ? $"not in {lists[0]}" : $"in none of {string.Join(", ", lists)}";
            string Says(string text, string what, CefrMissingLemma missing) =>
                $"{file}: the {lesson.Level} lesson says “{text}” ({what}), and '{missing.Lemma}'{(missing.InLexicon ? string.Empty : " (not in the lexicon)")} is above {level.ToUpperInvariant()} ({notListed}).";

            foreach (var missing in report.Missing)
            {
                foreach (var index in missing.LineIds.Select(static id => int.Parse(id, CultureInfo.InvariantCulture)))
                {
                    found.Add((index, Says(texts[index].Text, texts[index].What, missing)));
                }
            }

            var promptOrder = prompts.Keys.ToList();
            foreach (var missing in report.Prompts?.Missing ?? [])
            {
                foreach (var prompt in missing.LineIds)
                {
                    found.Add((texts.Count + promptOrder.IndexOf(prompt), Says(prompts[prompt].Text!, $"the prompt '{prompt}'", missing)));
                }
            }
        }

        return found.OrderBy(static f => f.Order).ThenBy(static f => f.Problem, StringComparer.Ordinal).Select(static f => f.Problem).ToList();
    }

    // V03: an order is brought to one of the NPCs, and every state of its agent thanks the player for it (the opening is <npc>.delivered whatever the state; a state
    // that did not take it would leave the goods with the player and the order open).
    private static void CheckOrder(GameData data, ObjectiveSpec objective, string file, List<string> problems)
    {
        if (objective.DeliverTo is not { } npc)
        {
            return;
        }

        if (data.Npcs.Find(npc) is null || !data.Bundle.Agents.TryGetValue(npc, out var machine))
        {
            problems.Add($"{file}: the order is brought to '{npc}', which is not one of the NPCs.");
            return;
        }

        var delivered = NpcHostEvents.Of(npc, NpcHostEvents.Delivered);
        foreach (var state in machine.Definition.States.Where(state => !state.Transitions.Any(t => string.Equals(t.OnHostEvent, delivered, StringComparison.Ordinal))))
        {
            problems.Add($"{file}: the order is brought to '{npc}', and the state '{state.Id}' of agents/{npc}.json does not take '{delivered}' to thank the player.");
        }

        // V13: the lesson an order names as the one that tells it keeps the order back until its step that waits for the delivery, so it must have that step.
        if (objective.Lesson is { } lesson)
        {
            if (data.Lessons.Find(lesson) is not { } told)
            {
                problems.Add($"{file}: the order names the lesson '{lesson}', which is not one of the lessons.");
            }
            else if (!told.Steps.Any(step => step is { Type: LessonStepType.Do, Trigger: { Event: WorldEvents.OrderDelivered } trigger }
                && string.Equals(trigger.Npc, npc, StringComparison.Ordinal) && string.Equals(trigger.Detail, objective.Id, StringComparison.Ordinal)))
            {
                problems.Add($"{file}: the order names the lesson '{lesson}', which has no do step that waits for its delivery (order_delivered at '{npc}' with the detail '{objective.Id}').");
            }
        }
    }

    // Whether a state of the NPC's agent has a transition on the host event (onHostEvent).
    private static bool TakenBySomeState(GameData data, string npc, string hostEvent) =>
        data.Bundle.Agents.TryGetValue(npc, out var machine)
        && machine.Definition.States.SelectMany(static state => state.Transitions).Any(t => string.Equals(t.OnHostEvent, hostEvent, StringComparison.Ordinal));

    // Schema 2 (VILLAGE_PLAN V01): what the NPC file says the NPC does. Before it, an NPC the code did not know waited on the villager's prompt and opened with his
    // greeting, with no error; now a missing or unknown prompt, an opening event no state takes and a voice with no name are problems.
    private static void CheckNpcBehaviour(GameData data, NpcEntry npc, string file, List<string> problems)
    {
        data.Bundle.Agents.TryGetValue(npc.Id, out var machine);
        var states = machine?.Definition.States.Select(static state => state.Id).ToHashSet(StringComparer.Ordinal);
        if (npc.Prompts.Count == 0)
        {
            problems.Add($"{file}: the NPC has no prompts (prompts: the prompt it waits on in each state of its agent).");
        }

        var named = npc.Prompts.Select(static pair => (State: pair.Key, Prompt: pair.Value)).ToList();
        if (npc.ReviewPrompt is { } review)
        {
            named.Add((NpcEntry.ReviewKey, review));
        }

        foreach (var (state, prompt) in named)
        {
            if (!data.Bundle.Prompts.TryGetValue(prompt, out var spec))
            {
                problems.Add($"{file}: the prompts name '{prompt}' at '{state}', which is not a prompt of the bundle.");
            }
            else if (!string.Equals(spec.AgentId ?? NpcService.GuideAgent, npc.Id, StringComparison.Ordinal))
            {
                problems.Add($"{file}: the prompts name '{prompt}' at '{state}', which is the prompt of '{spec.AgentId ?? NpcService.GuideAgent}', not of this NPC.");
            }

            if (states is not null && !string.Equals(state, NpcEntry.ReviewKey, StringComparison.Ordinal) && !states.Contains(state))
            {
                problems.Add($"{file}: the prompts name the state '{state}', which agents/{npc.Id}.json does not have.");
            }
        }

        if (machine is not null && npc.Prompts.Count > 0 && !npc.Prompts.ContainsKey(machine.Definition.InitialState))
        {
            problems.Add($"{file}: the prompts name none for the initial state '{machine.Definition.InitialState}', which a state with none of its own waits on.");
        }

        var takes = machine?.Definition.States.SelectMany(static state => state.Transitions).Select(static t => t.OnHostEvent).OfType<string>().ToHashSet(StringComparer.Ordinal);
        if (npc.Opening.Event is { } opening && takes is not null && !takes.Contains(opening))
        {
            problems.Add($"{file}: the opening event '{opening}' is taken by no state of agents/{npc.Id}.json (onHostEvent), so the talk would open with the prompt's text.");
        }

        if (npc.Voice is not { } voice)
        {
            problems.Add($"{file}: the NPC has no voice (voice: the Kokoro speaker and name its lines are recorded with).");
        }
        else if (voice.Name.Length == 0)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"{file}: the voice (speaker {voice.Speaker}) has no name."));
        }

        foreach (var lemma in npc.KeyWords.Where(lemma => data.Nouns.ForLemma(lemma) is null))
        {
            problems.Add($"{file}: the key word '{lemma}' is not a catalogue noun.");
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
