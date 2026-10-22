using System.Text.Json;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game;

/// <summary>A save that cannot be read, is for another world or bundle, or does not restore to the digest it was taken with.</summary>
public sealed class SaveException : Exception
{
    /// <summary>Creates the exception with a message for the player (the front-end prints it).</summary>
    /// <param name="message">What is wrong with the save.</param>
    public SaveException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Saves and loads a running game (S10). <see cref="Capture"/> reads a session at rest into a <see cref="SaveDocument"/> and
/// <see cref="Serialize"/> writes it as canonical JSON (sorted keys, no whitespace, invariant numbers): the same state gives the same bytes.
/// <see cref="Load"/> reads one back into a new session: it refuses a save of another seed or bundle, rebuilds the scene from the seed and the
/// layout, applies the scene diff, restores every part, checks the world's digest, and runs the session in the next play session (which lifts
/// the spacing gaps of D-game-6). The stored time continues where the save left it: loading adds nothing to it.
/// </summary>
/// <remarks>
/// No file is read or written here: the front-end reads the text and writes the bytes. Nothing reads the clock or draws a random number.
/// </remarks>
public static class SaveGame
{
    /// <summary>The format name every save carries.</summary>
    public const string FormatName = "vocaterra-save";

    /// <summary>The format version this code writes and reads.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The canonical JSON of a save (the same document always gives the same string).</summary>
    /// <param name="document">The document.</param>
    public static string Serialize(SaveDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CanonicalJson.Serialize(document);
    }

    /// <summary>Reads a save's text. Refuses text that is not a save of this format and version.</summary>
    /// <exception cref="SaveException">The text is not a readable save of version <see cref="CurrentVersion"/>.</exception>
    public static SaveDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        SaveDocument document;
        try
        {
            document = CanonicalJson.Deserialize<SaveDocument>(json);
        }
        catch (JsonException ex)
        {
            throw new SaveException($"The file is not a readable save: {ex.Message}");
        }

        if (!string.Equals(document.Format, FormatName, StringComparison.Ordinal))
        {
            throw new SaveException($"The file is not a {FormatName} save (its format is '{document.Format}').");
        }

        if (document.Version != CurrentVersion)
        {
            throw new SaveException($"The save is format version {document.Version}; this game reads version {CurrentVersion}.");
        }

        return document;
    }

    /// <summary>
    /// Checks that a save can be loaded here: the bundle is the one it was made from, and (when given) the world seed is the one asked for.
    /// </summary>
    /// <param name="document">The save.</param>
    /// <param name="bundle">The content bundle of this run.</param>
    /// <param name="requestedSeed">The seed the run asked for, or <see langword="null"/> to take the save's.</param>
    /// <exception cref="SaveException">The seed or the bundle differs.</exception>
    public static void Check(SaveDocument document, ContentBundle bundle, ulong? requestedSeed = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bundle);
        if (requestedSeed is { } seed && seed != document.WorldSeed)
        {
            throw new SaveException($"The save is for world seed {document.WorldSeed}, not {seed}. Load it without --seed, or with --seed {document.WorldSeed}.");
        }

        if (!string.Equals(document.BundleHash, bundle.ContentHash, StringComparison.Ordinal))
        {
            throw new SaveException(
                $"The save was made from content bundle {Short(document.BundleHash)}; this run has bundle {Short(bundle.ContentHash)}. "
                + "The game's content changed since the save, so it cannot be loaded.");
        }
    }

    /// <summary>
    /// Captures a session as a save. The session must be at rest (<see cref="GuideSession.CanSave"/>: no turn on the worker, no key words standing,
    /// no opening waiting). <paramref name="pristine"/> is a freshly built scene of the same seed and layout, which the scene diff is taken against.
    /// The session is not changed.
    /// </summary>
    /// <param name="session">The running game.</param>
    /// <param name="pristine">The scene as it was built for the session's seed (a new build, never the session's own scene).</param>
    /// <exception cref="InvalidOperationException">The session is not at rest.</exception>
    /// <exception cref="ArgumentException">The pristine scene is not this session's scene.</exception>
    public static SaveDocument Capture(VoxelSession session, Scene pristine)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(pristine);
        if (!session.CanSave)
        {
            throw new InvalidOperationException("The game is not at rest: the guide is still thinking, the key words are on show or an opening waits. Save in a moment.");
        }

        if (pristine.Seed != session.WorldSeed || !string.Equals(pristine.Name, session.Scene.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException("The pristine scene is not built from this session's seed and layout.", nameof(pristine));
        }

        var state = session.Simulation.CaptureState();
        return new SaveDocument(
            FormatName,
            CurrentVersion,
            session.WorldSeed,
            session.Scene.Name,
            session.Bundle.ContentHash,
            session.StartedAt,
            session.SkippedSeconds,
            session.StoredTime.UnixSeconds,
            session.PlaySession,
            state.Tick,
            session.Simulation.ComputeStateDigest(),
            state.SelectedSlot,
            new GatherSave(state.GatherTarget, state.GatherBlock, state.GatherProgress),
            state.NoBottleHint,
            state.Player,
            state.Slots.Select(static slot => new SlotSave(slot.Item, slot.Count)).ToArray(),
            session.Objective.Steps.Select(static step => new ObjectiveSave(step.Key, step.Count)).ToArray(),
            session.Words.Records.Values.OrderBy(static record => record.Lemma, StringComparer.Ordinal).Select(WordSaveOf).ToArray(),
            session.PhraseProgress.Records.Select(PhraseSaveOf).ToArray(),
            session.CardedLemmas(),
            session.KeyWordsTaught,
            session.RiverPointedState,
            session.Pointer is { Quiz: false } pointer ? new PointerSave(pointer.Place, pointer.Target) : null,
            session.DeferredSkills.Select(static step => new DeferredSkillSave(step.Npc, step.Skill)).ToArray(),
            session.LastRepairs().Select(static step => new LastRepairSave(step.Npc, step.Repair)).ToArray(),
            session.NextTurnId,
            NpcService.AgentIds.Select(session.Conversation.CaptureAgent).ToArray(),
            VoxelGridDiff.Capture(session.Scene.Grid, pristine.Grid),
            session.Lessons.Capture(),
            session.Tips.Capture());
    }

    /// <summary>Captures a session and serializes it: the text of the save file.</summary>
    /// <param name="session">The running game (at rest).</param>
    /// <param name="pristine">A freshly built scene of the same seed and layout.</param>
    public static string Save(VoxelSession session, Scene pristine) => Serialize(Capture(session, pristine));

    /// <summary>
    /// Reads a save's text, checks it against the bundle and the requested seed, builds its scene with <paramref name="buildPristine"/> and restores
    /// the session (see <see cref="Restore"/>).
    /// </summary>
    /// <param name="json">The save's text.</param>
    /// <param name="bundle">The content bundle of this run.</param>
    /// <param name="buildPristine">Builds the scene of a seed from the layout (a new scene each call).</param>
    /// <param name="nouns">The noun catalogue of the bundle.</param>
    /// <param name="requestedSeed">The seed the run asked for, or <see langword="null"/> to take the save's.</param>
    /// <param name="credit">The credit rules of the run (the default when <see langword="null"/>).</param>
    /// <param name="bumpSession">Whether the run is the next play session (true for a real load; the tests also restore without it).</param>
    /// <exception cref="SaveException">The save cannot be loaded.</exception>
    public static VoxelSession Load(
        string json,
        ContentBundle bundle,
        Func<ulong, Scene> buildPristine,
        NounCatalog nouns,
        ulong? requestedSeed = null,
        CreditRules? credit = null,
        bool bumpSession = true)
    {
        ArgumentNullException.ThrowIfNull(buildPristine);
        var document = Parse(json);
        Check(document, bundle, requestedSeed);
        return Restore(document, bundle, buildPristine(document.WorldSeed), nouns, credit, bumpSession);
    }

    /// <summary>
    /// Restores a session from a save and a freshly built scene of the save's seed and layout. The scene diff is applied to the scene, the session is
    /// made on it, and every part is put back. The restored world's digest must equal the save's, and the stored time must be the save's, or the
    /// save is refused. The play session is the save's plus one when <paramref name="bumpSession"/> (a load), the save's own when not.
    /// </summary>
    /// <param name="document">The save.</param>
    /// <param name="bundle">The content bundle of this run (checked against the save).</param>
    /// <param name="scene">A freshly built scene of the save's seed and layout; it is changed by the diff and then becomes the session's scene.</param>
    /// <param name="nouns">The noun catalogue of the bundle.</param>
    /// <param name="credit">The credit rules of the run; the default when <see langword="null"/>.</param>
    /// <param name="bumpSession">Whether to start the next play session.</param>
    /// <exception cref="SaveException">The save does not fit the scene, the bundle or itself, or the restored world differs from its digest.</exception>
    public static VoxelSession Restore(
        SaveDocument document,
        ContentBundle bundle,
        Scene scene,
        NounCatalog nouns,
        CreditRules? credit = null,
        bool bumpSession = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(nouns);
        Check(document, bundle);
        if (document.Session < 1)
        {
            throw new SaveException($"The save's play session is {document.Session}; sessions start at 1.");
        }

        if (scene.Seed != document.WorldSeed || !string.Equals(scene.Name, document.SceneName, StringComparison.Ordinal))
        {
            throw new SaveException("The scene was not built from this save's seed and layout.");
        }

        VoxelSession session;
        try
        {
            VoxelGridDiff.Apply(scene.Grid, document.Chunks);
            session = new VoxelSession(bundle, document.WorldSeed, document.StartedAt, nouns, scene, credit);
            RestoreParts(session, document);
        }
        catch (ArgumentException ex)
        {
            throw new SaveException($"The save does not restore: {ex.Message}");
        }

        if (session.StoredTime.UnixSeconds != document.StoredTime)
        {
            throw new SaveException($"The stored time is {session.StoredTime.UnixSeconds}, the save says {document.StoredTime}.");
        }

        var digest = session.Simulation.ComputeStateDigest();
        if (!string.Equals(digest, document.StateDigest, StringComparison.Ordinal))
        {
            throw new SaveException("The restored world does not match the save's digest: the save is damaged or was made by another build.");
        }

        session.PlaySession = bumpSession ? document.Session + 1 : document.Session;
        return session;
    }

    // Every part of the save, put back in the order the parts depend on: the simulation, the time, the progress, the NPCs.
    private static void RestoreParts(VoxelSession session, SaveDocument document)
    {
        var slots = document.Inventory.Select(static slot => new ItemStack(slot.Item, slot.Count)).ToArray();
        session.Simulation.Restore(new VoxelSimulationState(
            document.Tick,
            document.SelectedSlot,
            document.Gather.Target,
            document.Gather.Block,
            document.Gather.Progress,
            document.NoBottleHint,
            document.Player,
            slots));
        session.RestoreSkippedSeconds(document.SkippedSeconds);
        session.Words.Restore(document.Words.Select(RecordOf));
        RestorePhrases(session, document.Phrases);
        session.Objective.Restore(document.Objective.Select(static step => (step.Key, step.Count)));
        session.RestoreCarded(document.Carded);
        session.KeyWordsTaught = document.KeyWordsTaught;
        session.RiverPointedState = document.RiverPointed;
        session.RestorePointer(document.Pointer is { } pointer ? new PointerMarker(pointer.Place, pointer.Target) : null);
        session.RestoreDeferredSkills(document.DeferredSkills.Select(static step => (step.Npc, step.Skill)));
        session.RestoreLastRepairs(document.LastRepairs.Select(static step => (step.Npc, step.Repair)));
        session.NextTurnId = document.NextTurnId;
        RestoreNpcs(session, document.Npcs);
        if (document.Lessons is { } lessons)
        {
            session.Lessons.Restore(lessons);
        }

        if (document.Tips is { } tips)
        {
            session.Tips.Restore(tips);
        }

        session.RestoreStage(); // the stage of the objective area (S17) follows from the world and the progress just put back
    }

    // The phrase sheet's rows, each exactly once: a save with an unknown or missing phrase is refused.
    private static void RestorePhrases(VoxelSession session, IReadOnlyList<PhraseSave> phrases)
    {
        var rows = session.Phrases.Rows;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var phrase in phrases)
        {
            if (!rows.Any(row => string.Equals(row.Id, phrase.Id, StringComparison.Ordinal)) || !ids.Add(phrase.Id))
            {
                throw new ArgumentException($"The phrase '{phrase.Id}' is not a row of the sheet, or is given twice.", nameof(phrases));
            }
        }

        if (ids.Count != rows.Count)
        {
            throw new ArgumentException($"The save has {ids.Count} phrase records; the sheet has {rows.Count}.", nameof(phrases));
        }

        session.PhraseProgress.Restore(phrases.Select(PhraseRecordOf));
    }

    // Each NPC exactly once; a missing NPC or one the game does not have is refused.
    private static void RestoreNpcs(VoxelSession session, IReadOnlyList<NpcSave> npcs)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var npc in npcs)
        {
            if (!NpcService.AgentIds.Contains(npc.Agent, StringComparer.Ordinal) || !seen.Add(npc.Agent))
            {
                throw new ArgumentException($"The NPC '{npc.Agent}' is not one of the game's, or is given twice.", nameof(npcs));
            }
        }

        if (seen.Count != NpcService.AgentIds.Count)
        {
            throw new ArgumentException("The save does not hold every NPC.", nameof(npcs));
        }

        foreach (var npc in npcs)
        {
            session.Conversation.RestoreAgent(npc);
        }
    }

    private static WordSave WordSaveOf(WordRecord record) => new(
        record.Lemma,
        record.Heard,
        record.Read,
        record.Spoken,
        record.SpokenCorrect,
        record.CreditUnits,
        record.CorrectTurns,
        record.LastCreditedAt,
        record.LastCreditedSession,
        record.Lapses);

    private static WordRecord RecordOf(WordSave save) => new(
        save.Lemma,
        save.Heard,
        save.Read,
        save.Spoken,
        save.SpokenCorrect,
        save.CreditUnits,
        save.CorrectTurns,
        save.LastCreditedAt,
        save.LastCreditedSession,
        save.Lapses);

    private static PhraseSave PhraseSaveOf(PhraseRecord record) => new(
        record.Id,
        record.Level,
        record.CardShown,
        record.Supported,
        record.Unaided,
        record.ChoiceOnly,
        record.DontKnow,
        record.Transfer,
        record.FailForward,
        record.Slips,
        record.Heard,
        record.Answered,
        record.StepUnits,
        record.CreditedUses,
        record.LastCreditedAt,
        record.LastCreditedSession,
        record.LastUseAt,
        record.LastUseSession);

    private static PhraseRecord PhraseRecordOf(PhraseSave save) => new(
        save.Id,
        save.Level,
        save.CardShown,
        save.Supported,
        save.Unaided,
        save.ChoiceOnly,
        save.DontKnow,
        save.Transfer,
        save.FailForward,
        save.Slips,
        save.Heard,
        save.Answered,
        save.StepUnits,
        save.CreditedUses,
        save.LastCreditedAt,
        save.LastCreditedSession,
        save.LastUseAt,
        save.LastUseSession);

    // The first characters of a hash, for a message a player can read.
    private static string Short(string hash) => hash.Length > 12 ? hash[..12] + "..." : hash;
}
