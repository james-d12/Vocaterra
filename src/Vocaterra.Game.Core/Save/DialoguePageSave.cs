using Vocaterra.Game.Core.Dialogue;

namespace Vocaterra.Game.Core.Save;

/// <summary>One line of an NPC's conversation page in a save (U08): its speaker kind as a stable name and its text.</summary>
/// <param name="Speaker">The speaker kind's name (see <see cref="DialoguePageSave.SpeakerName"/>); a name this build does not know is skipped on load.</param>
/// <param name="Text">The line's text.</param>
public sealed record DialogueLineSave(string Speaker, string Text);

/// <summary>One NPC's conversation page in a save (U08): the NPC's id and its lines, oldest first, at most <see cref="DialogueModel.MaxLines"/>.</summary>
/// <param name="Npc">The NPC's id.</param>
/// <param name="Lines">The page's lines.</param>
public sealed record DialoguePageSave(string Npc, IReadOnlyList<DialogueLineSave> Lines)
{
    /// <summary>The stable name a speaker kind has in a save, or <see langword="null"/> for a kind that is never stored (the coaching line).</summary>
    /// <param name="speaker">The speaker kind.</param>
    public static string? SpeakerName(DialogueSpeaker speaker) => speaker switch
    {
        DialogueSpeaker.Player => "player",
        DialogueSpeaker.PlayerSpoken => "player-spoken",
        DialogueSpeaker.Note => "note",
        DialogueSpeaker.Correction => "correction",
        DialogueSpeaker.Accent => "accent",
        DialogueSpeaker.Guide => "guide",
        DialogueSpeaker.World => "world",
        DialogueSpeaker.Hint => "hint",
        DialogueSpeaker.English => "english",
        _ => null,
    };

    /// <summary>The speaker kind of a saved name, or <see langword="null"/> for a name this build does not know.</summary>
    /// <param name="name">The saved name.</param>
    public static DialogueSpeaker? SpeakerOf(string name) => name switch
    {
        "player" => DialogueSpeaker.Player,
        "player-spoken" => DialogueSpeaker.PlayerSpoken,
        "note" => DialogueSpeaker.Note,
        "correction" => DialogueSpeaker.Correction,
        "accent" => DialogueSpeaker.Accent,
        "guide" => DialogueSpeaker.Guide,
        "world" => DialogueSpeaker.World,
        "hint" => DialogueSpeaker.Hint,
        "english" => DialogueSpeaker.English,
        _ => null,
    };

    /// <summary>The pages of a dialogue model as they go into a save: the NPCs with lines, in id order, each page's newest <see cref="DialogueModel.MaxLines"/> lines; <see langword="null"/> when no page has a line.</summary>
    /// <param name="pages">The model's pages.</param>
    public static IReadOnlyList<DialoguePageSave>? Capture(IReadOnlyDictionary<string, List<DialogueLine>> pages)
    {
        var saved = new List<DialoguePageSave>();
        foreach (var (npc, lines) in pages.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            var kept = lines
                .Where(static l => SpeakerName(l.Speaker) is not null)
                .TakeLast(DialogueModel.MaxLines)
                .Select(static l => new DialogueLineSave(SpeakerName(l.Speaker)!, l.Text))
                .ToArray();
            if (kept.Length > 0)
            {
                saved.Add(new DialoguePageSave(npc, kept));
            }
        }

        return saved.Count == 0 ? null : saved;
    }
}
