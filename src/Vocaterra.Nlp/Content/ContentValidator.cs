using System.Globalization;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Content;

/// <summary>Cross-reference and consistency checks over the parsed parts of a bundle; every problem is appended to the list.</summary>
internal static class ContentValidator
{
    private const string AttributionLicenseMarker = "CC BY-SA";

    public static void Validate(LoadedParts parts, BundleFiles files, List<string> problems)
    {
        ValidateProvenance(parts, files, problems);
        ValidateAttribution(parts, files, problems);
        ValidatePrompts(parts, problems);
        ValidateAgents(parts, problems);
        ValidateEffects(parts, problems);
        ValidateDialogue(parts, problems);
    }

    private static HashSet<string> IntentIds(LoadedParts parts)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (parts.Intents is { } intents)
        {
            foreach (var intent in intents.Intents)
            {
                ids.Add(intent.Id.Value);
            }
        }

        return ids;
    }

    private static void ValidateProvenance(LoadedParts parts, BundleFiles files, List<string> problems)
    {
        if (parts.Provenance is not { } provenance)
        {
            return;
        }

        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in provenance.Items)
        {
            var where = $"{ContentPaths.Provenance}: '{item.Path}'";
            if (!covered.Add(item.Path))
            {
                problems.Add($"{where} is listed twice.");
                continue;
            }

            if (!files.Contains(item.Path))
            {
                problems.Add($"{where} does not exist in the bundle.");
            }

            if (!GeneratedByKinds.All.Contains(item.GeneratedBy))
            {
                problems.Add($"{where} has unknown generatedBy '{item.GeneratedBy}'.");
            }

            var approvedBy = string.IsNullOrWhiteSpace(item.ApprovedBy) ? null : item.ApprovedBy;
            if (approvedBy is null != item.ApprovedOn is null)
            {
                problems.Add($"{where} must set approvedBy and approvedOn together.");
            }
            else if (item.ApprovedOn is { } date
                && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                problems.Add($"{where} has approvedOn '{date}'; expected YYYY-MM-DD.");
            }

            if (approvedBy is null && string.Equals(item.GeneratedBy, GeneratedByKinds.LlmDraft, StringComparison.Ordinal))
            {
                problems.Add($"{where} is a language-model draft and must be approved by a person before it ships.");
            }

            ValidateModelFields(item, where, problems);
        }

        foreach (var path in files.Paths)
        {
            if (!string.Equals(path, ContentPaths.Provenance, StringComparison.Ordinal) && !covered.Contains(path))
            {
                problems.Add($"{ContentPaths.Provenance}: no provenance item for '{path}'.");
            }
        }

        ValidateLexiconProvenance(parts, files, provenance, problems);
    }

    // A model output is a host-owned file rendered from text a person has reviewed, so it names the model and may not be language data: the loader
    // parses that, and its provenance is what the approval rules protect. Every other kind must leave the model fields out.
    private static void ValidateModelFields(ProvenanceItem item, string where, List<string> problems)
    {
        if (!string.Equals(item.GeneratedBy, GeneratedByKinds.ModelOutput, StringComparison.Ordinal))
        {
            if (item.Model is not null || item.Voice is not null || item.Speaker is not null || item.Source is not null)
            {
                problems.Add($"{where} sets model, voice, speaker or source, which only a '{GeneratedByKinds.ModelOutput}' item may set.");
            }

            return;
        }

        if (ContentPaths.IsLoaderOwned(item.Path))
        {
            problems.Add($"{where} is '{GeneratedByKinds.ModelOutput}', which only host-owned files may be; this file is language data the loader parses, so give it a human-reviewed kind instead.");
        }

        if (item.Model is null || string.IsNullOrWhiteSpace(item.Model))
        {
            problems.Add($"{where} is '{GeneratedByKinds.ModelOutput}' and needs a model: the id of the model that rendered the file, for example 'kokoro-multi-lang-v1_0'.");
        }
        else if (!string.Equals(item.Model, item.Model.Trim(), StringComparison.Ordinal))
        {
            problems.Add($"{where} has a model with leading or trailing whitespace.");
        }

        CheckOptionalModelField(where, "voice", item.Voice, problems);
        CheckOptionalModelField(where, "speaker", item.Speaker, problems);
        CheckOptionalModelField(where, "source", item.Source, problems);
    }

    private static void CheckOptionalModelField(string where, string name, string? value, List<string> problems)
    {
        if (value is not null && (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            problems.Add($"{where} has a {name} that is empty or has leading or trailing whitespace; leave the field out instead.");
        }
    }

    // A lexicon file marks its own origin with "generated_by"; the mark and provenance.json must tell the same story, and every imported file
    // carries the sidecar that makes the import reproducible.
    private static void ValidateLexiconProvenance(LoadedParts parts, BundleFiles files, ContentProvenance provenance, List<string> problems)
    {
        var declared = provenance.Items
            .GroupBy(static item => item.Path, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First().GeneratedBy, StringComparer.Ordinal);
        foreach (var (path, marker) in parts.LexiconGeneratedBy)
        {
            var imported = string.Equals(marker, GeneratedByKinds.WiktionaryImport, StringComparison.Ordinal);
            var declaredImport = declared.TryGetValue(path, out var kind) && string.Equals(kind, GeneratedByKinds.WiktionaryImport, StringComparison.Ordinal);
            if (imported != declaredImport)
            {
                problems.Add($"{path}: generated_by is '{marker ?? "(none)"}' but {ContentPaths.Provenance} says '{(declared.TryGetValue(path, out var said) ? said : "(no item)")}'; an imported lexicon must be marked '{GeneratedByKinds.WiktionaryImport}' in both places.");
            }

            if (imported && !files.Contains(path[..^".json".Length] + LexiconSourceInfo.FileSuffix))
            {
                problems.Add($"{path}: an imported lexicon needs its sidecar '{LexiconSourceInfo.SidecarNameFor(Path.GetFileName(path))}' (source URLs, hashes, importer version).");
            }
        }
    }

    private static void ValidateAttribution(LoadedParts parts, BundleFiles files, List<string> problems)
    {
        if (!files.Contains(ContentPaths.Attribution))
        {
            problems.Add($"{ContentPaths.Attribution}: required file is missing.");
            return;
        }

        var text = files.Text(ContentPaths.Attribution);
        if (string.IsNullOrWhiteSpace(text))
        {
            problems.Add($"{ContentPaths.Attribution}: the notice is empty or not valid UTF-8 text.");
            return;
        }

        var importsWiktionary = parts.Provenance?.Items.Any(static item =>
            string.Equals(item.GeneratedBy, GeneratedByKinds.WiktionaryImport, StringComparison.Ordinal)) ?? false;
        if (importsWiktionary && !text.Contains(AttributionLicenseMarker, StringComparison.Ordinal))
        {
            problems.Add($"{ContentPaths.Attribution}: Wiktionary data is imported, so the notice must state its {AttributionLicenseMarker} license.");
        }
    }

    private static void ValidatePrompts(LoadedParts parts, List<string> problems)
    {
        var intentIds = IntentIds(parts);
        var grammarIds = new HashSet<string>(StringComparer.Ordinal);
        if (parts.Intents is { } intents)
        {
            foreach (var grammar in intents.Grammars)
            {
                grammarIds.Add(grammar.Id);
            }
        }

        foreach (var prompt in parts.Prompts.Values)
        {
            var where = $"{ContentPaths.Prompts}: prompt '{prompt.PromptId}'";
            if (!Enum.IsDefined(prompt.Mode) || !Enum.IsDefined(prompt.Difficulty))
            {
                problems.Add($"{where} has an unknown mode or difficulty.");
            }

            if (!parts.AnswerSets.ContainsKey(prompt.AnswerSetId))
            {
                problems.Add($"{where} references unknown answer set '{prompt.AnswerSetId}'.");
            }

            if (parts.Intents is not null)
            {
                foreach (var intentId in prompt.AllowedIntentIds)
                {
                    if (!intentIds.Contains(intentId))
                    {
                        problems.Add($"{where} allows unknown intent '{intentId}'.");
                    }
                }

                foreach (var grammarId in prompt.SlotGrammarIds)
                {
                    if (!grammarIds.Contains(grammarId))
                    {
                        problems.Add($"{where} references unknown slot grammar '{grammarId}'.");
                    }
                }
            }

            if (prompt.AgentId is { } agentId && !parts.Agents.ContainsKey(agentId))
            {
                problems.Add($"{where} references unknown agent '{agentId}'.");
            }

            // The prompt text is optional, but when it is present it is the line the agent says: it must not be empty, and it is written without
            // leading or trailing whitespace so that every host shows the same text.
            if (prompt.Text is { } text && (string.IsNullOrWhiteSpace(text) || !string.Equals(text, text.Trim(), StringComparison.Ordinal)))
            {
                problems.Add($"{where} has a text that is empty or has leading or trailing whitespace.");
            }
        }
    }

    private static void ValidateAgents(LoadedParts parts, List<string> problems)
    {
        var intentIds = IntentIds(parts);
        foreach (var machine in parts.Agents.Values)
        {
            var definition = machine.Definition;
            var where = $"{ContentPaths.AgentFolder}{definition.AgentId}.json";
            var poolSizes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pool in definition.VariantPools ?? [])
            {
                poolSizes[pool.Id] = pool.Count;
            }

            foreach (var state in definition.States)
            {
                foreach (var transition in state.Transitions)
                {
                    if (parts.Intents is not null
                        && transition.On.Intent is { } onIntent
                        && !IntentId.Unknown.Value.Equals(onIntent, StringComparison.Ordinal)
                        && !intentIds.Contains(onIntent))
                    {
                        problems.Add($"{where}: state '{state.Id}' reacts to unknown intent '{onIntent}'.");
                    }

                    CheckActions(parts, where, state.Id, transition.Actions, poolSizes, problems);
                }

                if (state.Fallback is { } fallback)
                {
                    CheckActions(parts, where, state.Id, fallback.Actions, poolSizes, problems);
                }
            }
        }
    }

    private static void CheckActions(
        LoadedParts parts,
        string where,
        string stateId,
        IReadOnlyList<Vocaterra.Nlp.Decision.ConversationAgents.AgentActionDefinition> actions,
        Dictionary<string, int> poolSizes,
        List<string> problems)
    {
        if (parts.Dialogue is not { } dialogue)
        {
            return;
        }

        foreach (var action in actions)
        {
            if (!dialogue.TryGetPool(action.DialogueIntent, out var pool))
            {
                problems.Add($"{where}: state '{stateId}' says unknown dialogue intent '{action.DialogueIntent}'.");
                continue;
            }

            if (action.VariantPoolId is { } poolId
                && poolSizes.TryGetValue(poolId, out var count)
                && count > pool.Variants.Count)
            {
                problems.Add(
                    $"{where}: variant pool '{poolId}' has {count} variants but dialogue intent '{action.DialogueIntent}' only has {pool.Variants.Count}.");
            }
        }
    }

    private static void ValidateEffects(LoadedParts parts, List<string> problems)
    {
        if (parts.Effects is not { } effects || parts.Intents is null)
        {
            return;
        }

        var intentIds = IntentIds(parts);
        foreach (var effect in effects.Effects)
        {
            if (!intentIds.Contains(effect.Intent))
            {
                problems.Add($"{ContentPaths.Effects}: effect '{effect.EffectId}' references unknown intent '{effect.Intent}'.");
            }
        }
    }

    private static void ValidateDialogue(LoadedParts parts, List<string> problems)
    {
        if (parts.Dialogue is not { } dialogue)
        {
            return;
        }

        if (!dialogue.TryGetPool(ResponsePlanner.NotUnderstoodIntent, out _))
        {
            problems.Add($"{ContentPaths.Dialogue}: the catalog needs a '{ResponsePlanner.NotUnderstoodIntent}' pool.");
        }

        if (parts.Lexicon is null || parts.Language is null)
        {
            return;
        }

        var engine = new TemplateEngine(parts.Lexicon, parts.Language.CreateMorphology(parts.Lexicon), parts.Language);
        foreach (var pair in dialogue.Pools)
        {
            foreach (var variant in pair.Value.Variants)
            {
                try
                {
                    engine.Render(variant, SampleSlots.For(variant, parts.Language.Samples));
                }
                catch (Exception ex) when (ContentLoader.IsContentError(ex))
                {
                    problems.Add($"{ContentPaths.Dialogue}: line '{variant.LineId}' of '{pair.Key}' does not render: {ContentLoader.FirstLine(ex.Message)}");
                }
            }
        }
    }
}
