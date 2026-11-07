namespace Vocaterra.Game.Core.Sound;

/// <summary>The mixer's categories (S24, D-game-18): each has its own volume, and the mixer's rules (ducking, the microphone) apply by category.</summary>
public enum SoundCategory
{
    /// <summary>The NPCs' pre-rendered Spanish (S9). Played by <c>NpcVoice</c>; the mixer only knows when one is on, to duck the others.</summary>
    Voice,

    /// <summary>The effects of the world: gathering, placing, picking up, crafting, the fire. Ducks under a voice; silent while the microphone records.</summary>
    Sfx,

    /// <summary>The interface and progress sounds: panels, buttons, chimes, the push-to-talk beeps. Silent while the microphone records.</summary>
    Ui,

    /// <summary>The ambient beds (S25). Ducks under a voice; silent while the microphone records.</summary>
    Ambient,
}

/// <summary>
/// Every one-shot cue the game plays (S24). The name of a cue is its wire name in the sound log (<see cref="SoundCues.NameOf(SoundCue)"/>); the category says which
/// volume and which mixer rules apply (<see cref="SoundCues.CategoryOf"/>).
/// </summary>
public enum SoundCue
{
    // ---- interaction (sfx) ----

    /// <summary>A hit on a wooden block (a trunk, planks) while gathering.</summary>
    GatherWood,

    /// <summary>A hit on a stone block while gathering.</summary>
    GatherStone,

    /// <summary>A hit on leaves, a bush or undergrowth while gathering.</summary>
    GatherLeaf,

    /// <summary>A hit on soft ground (dirt, grass, sand) while gathering.</summary>
    GatherSoft,

    /// <summary>A wooden block broke.</summary>
    BreakWood,

    /// <summary>A stone block broke.</summary>
    BreakStone,

    /// <summary>Leaves or a bush came away.</summary>
    BreakLeaf,

    /// <summary>A soft block broke.</summary>
    BreakSoft,

    /// <summary>A block was placed.</summary>
    PlaceBlock,

    /// <summary>An item went into the inventory.</summary>
    PickUp,

    /// <summary>The recipe was crafted.</summary>
    CraftDone,

    /// <summary>The bottle was filled at the water.</summary>
    BottleFill,

    /// <summary>The fire was lit (a whoosh; S25 adds the crackle).</summary>
    FireLight,

    // ---- interaction (ui) ----

    /// <summary>The inventory opened.</summary>
    InventoryOpen,

    /// <summary>The inventory closed.</summary>
    InventoryClose,

    /// <summary>The crafting panel opened.</summary>
    CraftingOpen,

    /// <summary>The crafting panel closed.</summary>
    CraftingClose,

    /// <summary>The book opened.</summary>
    BookOpen,

    /// <summary>The book closed.</summary>
    BookClose,

    /// <summary>The book's page turned.</summary>
    BookPage,

    /// <summary>A craft was refused (soft: nothing is wrong, the ingredients are not there yet).</summary>
    CraftRefused,

    /// <summary>The dialogue panel opened (a short attention cue).</summary>
    DialogueOpen,

    /// <summary>The dialogue panel closed.</summary>
    DialogueClose,

    /// <summary>The push-to-talk start beep (played just before the recording starts).</summary>
    PushToTalkStart,

    /// <summary>The push-to-talk stop beep (played just after the recording ends).</summary>
    PushToTalkStop,

    /// <summary>A button was clicked.</summary>
    UiClick,

    /// <summary>A tab was clicked.</summary>
    UiTab,

    /// <summary>The F8 report was saved.</summary>
    ReportSaved,

    // ---- progress (ui) ----

    /// <summary>A lesson step was done (a soft chime).</summary>
    StepDone,

    /// <summary>A lesson unit was complete (a brighter chime).</summary>
    UnitDone,

    /// <summary>The lesson was complete (a short flourish).</summary>
    LessonDone,

    /// <summary>An answer was understood (positive).</summary>
    AnswerUnderstood,

    /// <summary>An answer was not understood, or "try again", or "no sé": neutral, never punishing.</summary>
    AnswerNeutral,

    /// <summary>A new word was learnt (a sparkle).</summary>
    WordLearnt,

    /// <summary>A step of the camp's checklist was done.</summary>
    ObjectiveStep,

    /// <summary>The camp's checklist was complete.</summary>
    ObjectiveDone,

    /// <summary>An item of a check was answered (whatever the answer).</summary>
    CheckAnswered,

    /// <summary>The session is complete: the closing cue at the fire.</summary>
    SessionDone,

    // ---- movement (sfx, S25): appended, so the values of the cues above never change ----

    /// <summary>A step on grass (or leaves, undergrowth and bushes, which the player stands in or beside).</summary>
    FootGrass,

    /// <summary>A step on dirt or farmland.</summary>
    FootDirt,

    /// <summary>A step on the gravel path or the village square.</summary>
    FootGravel,

    /// <summary>A step on stone, cobble, mossy stone, the fire ring or a house wall.</summary>
    FootStone,

    /// <summary>A step on wood: planks, a bridge, timber, a trunk, the stall, a crate.</summary>
    FootWood,

    /// <summary>A step on sand.</summary>
    FootSand,

    /// <summary>A step on snow (the high peaks).</summary>
    FootSnow,

    /// <summary>A step in water: a splash (the feet are in the water).</summary>
    FootWater,

    /// <summary>The player jumped (a short effort, not a step).</summary>
    Jump,

    /// <summary>The player landed after the air (a heavier thud; a splash when the landing is in water).</summary>
    Land,

    // ---- ambient (S25): the scheduled one-shots over the beds ----

    /// <summary>A bird's chirp, scheduled by the presentation generator over the forest bed; its level follows the bed's weight.</summary>
    BirdChirp,

    /// <summary>A crackle of the fire, scheduled over the fire bed; its level follows the fire's weight.</summary>
    FireCrackle,

    // ---- water (H10, sfx): appended, so the values of the cues above never change ----

    /// <summary>The player went into water fast (a fall or a jump in): a splash. A wade in makes none (<see cref="WaterEntry"/>).</summary>
    WaterSplash,

    /// <summary>A bubble rising while the breath runs down under water: one for each bubble the meter loses (<see cref="BreathBubbles"/>).</summary>
    UnderwaterBubbles,
}

/// <summary>The category and the wire name of each cue.</summary>
public static class SoundCues
{
    /// <summary>Every cue, in declaration order.</summary>
    public static IReadOnlyList<SoundCue> All { get; } = Enum.GetValues<SoundCue>();

    /// <summary>The category of the cue.</summary>
    /// <param name="cue">The cue.</param>
    public static SoundCategory CategoryOf(SoundCue cue) => cue switch
    {
        SoundCue.GatherWood or SoundCue.GatherStone or SoundCue.GatherLeaf or SoundCue.GatherSoft
            or SoundCue.BreakWood or SoundCue.BreakStone or SoundCue.BreakLeaf or SoundCue.BreakSoft
            or SoundCue.PlaceBlock or SoundCue.PickUp or SoundCue.CraftDone or SoundCue.BottleFill or SoundCue.FireLight => SoundCategory.Sfx,
        SoundCue.FootGrass or SoundCue.FootDirt or SoundCue.FootGravel or SoundCue.FootStone or SoundCue.FootWood or SoundCue.FootSand or SoundCue.FootSnow
            or SoundCue.FootWater or SoundCue.Jump or SoundCue.Land => SoundCategory.Sfx,
        SoundCue.WaterSplash or SoundCue.UnderwaterBubbles => SoundCategory.Sfx,
        SoundCue.BirdChirp or SoundCue.FireCrackle => SoundCategory.Ambient,
        _ => SoundCategory.Ui,
    };

    /// <summary>The wire name of the cue: lowercase snake case, as the sound log writes it (<c>gather_wood</c>).</summary>
    /// <param name="cue">The cue.</param>
    public static string NameOf(SoundCue cue)
    {
        var text = cue.ToString();
        var builder = new System.Text.StringBuilder(text.Length + 4);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(text[i]));
        }

        return builder.ToString();
    }

    /// <summary>The wire name of a category: <c>voice</c>, <c>sfx</c>, <c>ui</c> or <c>ambient</c>.</summary>
    /// <param name="category">The category.</param>
    public static string NameOf(SoundCategory category) => category switch
    {
        SoundCategory.Voice => "voice",
        SoundCategory.Sfx => "sfx",
        SoundCategory.Ui => "ui",
        SoundCategory.Ambient => "ambient",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown sound category."),
    };
}
