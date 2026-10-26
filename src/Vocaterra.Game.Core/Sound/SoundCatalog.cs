namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// How one cue sounds (S24): the takes (files under <c>assets/audio</c>, or none for a synthesized cue, which has one take), its gain before the category's volume
/// and the size of its random pitch change.
/// </summary>
/// <param name="Cue">The cue.</param>
/// <param name="Files">The files of the takes, relative to the audio folder; empty for a synthesized cue.</param>
/// <param name="Gain">The cue's own level, 0 to 1, before the category volume and the duck.</param>
/// <param name="PitchJitter">The most the pitch may change either way, in octaves (0.08 is about one semitone); 0 for a cue that is a note.</param>
/// <param name="SynthTakes">How many takes a synthesized cue has (S25: the footsteps, the birds and the crackle have several); one by default.</param>
public sealed record SoundDefinition(SoundCue Cue, IReadOnlyList<string> Files, double Gain, double PitchJitter, int SynthTakes = 1)
{
    /// <summary>How many takes the cue has: the files, or the synthesized takes.</summary>
    public int Takes => Files.Count == 0 ? SynthTakes : Files.Count;
}

/// <summary>
/// The sounds of the game (S24): every cue with its files (Kenney's CC0 packs, listed in <c>assets/audio/SOURCES.md</c>) or its synthesis
/// (<see cref="SoundSynth"/>). The provenance test reads this table against the folder and the list.
/// </summary>
public static class SoundCatalog
{
    private static readonly Dictionary<SoundCue, SoundDefinition> Table = Build();

    /// <summary>Every definition, one per cue.</summary>
    public static IReadOnlyCollection<SoundDefinition> All => Table.Values;

    /// <summary>The definition of a cue.</summary>
    /// <param name="cue">The cue.</param>
    public static SoundDefinition Of(SoundCue cue) => Table[cue];

    private static Dictionary<SoundCue, SoundDefinition> Build()
    {
        var table = new Dictionary<SoundCue, SoundDefinition>();
        void File(SoundCue cue, double gain, double jitter, params string[] files) => table[cue] = new SoundDefinition(cue, files, gain, jitter);
        void Synth(SoundCue cue, double gain, int takes = 1) => table[cue] = new SoundDefinition(cue, [], gain, 0, takes);

        // Kenney "Impact Sounds" (kenney-impact_), "RPG Audio" (kenney-rpg_) and "Interface Sounds" (kenney-interface_): CC0, see SOURCES.md.
        File(SoundCue.GatherWood, 0.7, 0.06, "kenney-impact_impactWood_light_000.wav", "kenney-impact_impactWood_light_001.wav", "kenney-impact_impactWood_light_002.wav");
        File(SoundCue.GatherStone, 0.7, 0.06, "kenney-impact_impactMining_000.wav", "kenney-impact_impactMining_001.wav", "kenney-impact_impactMining_002.wav");
        File(SoundCue.GatherLeaf, 0.6, 0.06, "kenney-rpg_cloth1.wav", "kenney-rpg_cloth2.wav");
        File(SoundCue.GatherSoft, 0.7, 0.06, "kenney-impact_impactSoft_medium_000.wav", "kenney-impact_impactSoft_medium_001.wav", "kenney-impact_impactSoft_medium_002.wav");
        File(SoundCue.BreakWood, 0.8, 0.05, "kenney-impact_impactWood_heavy_000.wav", "kenney-impact_impactWood_heavy_001.wav", "kenney-impact_impactWood_heavy_002.wav");
        File(SoundCue.BreakStone, 0.8, 0.05, "kenney-impact_impactMining_003.wav", "kenney-impact_impactMining_004.wav");
        File(SoundCue.BreakLeaf, 0.6, 0.05, "kenney-rpg_cloth3.wav", "kenney-rpg_cloth4.wav");
        File(SoundCue.BreakSoft, 0.8, 0.05, "kenney-impact_impactSoft_heavy_000.wav", "kenney-impact_impactSoft_heavy_001.wav", "kenney-impact_impactSoft_heavy_002.wav");
        File(SoundCue.PlaceBlock, 0.8, 0.05, "kenney-impact_impactPlank_medium_000.wav", "kenney-impact_impactPlank_medium_001.wav", "kenney-impact_impactPlank_medium_002.wav");
        File(SoundCue.PickUp, 0.7, 0.05, "kenney-rpg_handleSmallLeather.wav", "kenney-rpg_handleSmallLeather2.wav");
        File(SoundCue.CraftDone, 0.8, 0.0, "kenney-rpg_metalLatch.wav");
        Synth(SoundCue.BottleFill, 0.8);
        Synth(SoundCue.FireLight, 0.8);

        File(SoundCue.InventoryOpen, 0.7, 0.03, "kenney-rpg_beltHandle1.wav");
        File(SoundCue.InventoryClose, 0.7, 0.03, "kenney-rpg_beltHandle2.wav");
        File(SoundCue.CraftingOpen, 0.7, 0.03, "kenney-rpg_clothBelt.wav");
        File(SoundCue.CraftingClose, 0.7, 0.03, "kenney-rpg_clothBelt2.wav");
        File(SoundCue.BookOpen, 0.8, 0.0, "kenney-rpg_bookOpen.wav");
        File(SoundCue.BookClose, 0.8, 0.0, "kenney-rpg_bookClose.wav");
        File(SoundCue.BookPage, 0.7, 0.04, "kenney-rpg_bookFlip1.wav", "kenney-rpg_bookFlip2.wav", "kenney-rpg_bookFlip3.wav");
        File(SoundCue.CraftRefused, 0.5, 0.0, "kenney-interface_back_001.wav");
        File(SoundCue.DialogueOpen, 0.6, 0.0, "kenney-interface_pluck_001.wav");
        File(SoundCue.DialogueClose, 0.5, 0.0, "kenney-interface_pluck_002.wav");
        Synth(SoundCue.PushToTalkStart, 0.7);
        Synth(SoundCue.PushToTalkStop, 0.7);
        File(SoundCue.UiClick, 0.6, 0.03, "kenney-interface_click_001.wav", "kenney-interface_click_002.wav", "kenney-interface_click_003.wav");
        File(SoundCue.UiTab, 0.6, 0.03, "kenney-interface_tick_001.wav", "kenney-interface_tick_002.wav");
        File(SoundCue.ReportSaved, 0.7, 0.0, "kenney-interface_confirmation_001.wav");

        Synth(SoundCue.StepDone, 0.6);
        Synth(SoundCue.UnitDone, 0.7);
        Synth(SoundCue.LessonDone, 0.8);
        Synth(SoundCue.AnswerUnderstood, 0.6);
        Synth(SoundCue.AnswerNeutral, 0.5);
        Synth(SoundCue.WordLearnt, 0.6);
        File(SoundCue.ObjectiveStep, 0.7, 0.0, "kenney-interface_confirmation_002.wav");
        Synth(SoundCue.ObjectiveDone, 0.8);
        File(SoundCue.CheckAnswered, 0.6, 0.0, "kenney-interface_select_001.wav");
        Synth(SoundCue.SessionDone, 0.8);

        // Movement (S25). Kenney "Impact Sounds" has footsteps on grass, snow, wood and concrete (kenney-impact_footstep_*); the dirt, gravel, sand and
        // splash are made in code (SoundTextures). Concrete plays for stone.
        File(SoundCue.FootGrass, 0.5, 0.05, "kenney-impact_footstep_grass_000.wav", "kenney-impact_footstep_grass_001.wav", "kenney-impact_footstep_grass_002.wav");
        File(SoundCue.FootSnow, 0.5, 0.05, "kenney-impact_footstep_snow_000.wav", "kenney-impact_footstep_snow_001.wav");
        File(SoundCue.FootWood, 0.5, 0.05, "kenney-impact_footstep_wood_000.wav", "kenney-impact_footstep_wood_001.wav");
        File(SoundCue.FootStone, 0.5, 0.05, "kenney-impact_footstep_concrete_000.wav", "kenney-impact_footstep_concrete_001.wav");
        File(SoundCue.Jump, 0.3, 0.05, "kenney-impact_impactGeneric_light_000.wav");
        File(SoundCue.Land, 0.45, 0.05, "kenney-impact_impactSoft_heavy_000.wav");
        Synth(SoundCue.FootDirt, 0.55, takes: 3);
        Synth(SoundCue.FootGravel, 0.5, takes: 3);
        Synth(SoundCue.FootSand, 0.4, takes: 3);
        Synth(SoundCue.FootWater, 0.55, takes: 3);
        Synth(SoundCue.BirdChirp, 0.3, takes: 4);
        Synth(SoundCue.FireCrackle, 0.45, takes: 4);
        return table;
    }
}
