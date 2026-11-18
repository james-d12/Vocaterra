using System.Text.Json.Serialization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;
using Vocaterra.Nlp.Language.Intent;

namespace Vocaterra.Game.Core.Save;

/// <summary>
/// One saved game (format <c>vocaterra-save</c>, version 3; versions 1 and 2 still load), written as canonical JSON by <see cref="SaveGame"/>. It holds what a run needs to
/// continue and nothing derived from the clock: the world seed and the bundle hash that the world was made from; the scene diff (the chunks
/// edited against the fresh scene); the player, the hotbar, the gathering, the inventory and the objective; the word and phrase progress; the
/// NPCs' conversation states and host decisions; the carded nouns and the flags of the story; the stored time (start, ticks, skipped seconds),
/// the session number and the digest of the world the save was taken from (a load checks it).
/// </summary>
/// <param name="Format">The format name (<see cref="SaveGame.FormatName"/>).</param>
/// <param name="Version">The format version (<see cref="SaveGame.CurrentVersion"/>).</param>
/// <param name="WorldSeed">The world seed: the scene's trees and the conversation's random draws come from it.</param>
/// <param name="SceneName">The scene layout's name (for the reader; the bundle hash fixes the layout).</param>
/// <param name="BundleHash">The content bundle's hash. A load refuses a save made from another bundle.</param>
/// <param name="StartedAt">The stored time at tick 0, in Unix seconds.</param>
/// <param name="SkippedSeconds">The seconds added by skips (the stored time is <c>StartedAt + Tick / 60 + SkippedSeconds</c>).</param>
/// <param name="StoredTime">The stored time at the save (checked on load).</param>
/// <param name="Session">The day of the village the save was taken in (V12c; named the play session before V25). A load runs in the same day, and only a sleep moves it.</param>
/// <param name="Tick">The simulation's tick.</param>
/// <param name="StateDigest">The simulation's state digest at the save (a load checks the restored world against it).</param>
/// <param name="SelectedSlot">The selected hotbar slot.</param>
/// <param name="Gather">The gathering in progress (the target, its kind and the ticks spent), or no target.</param>
/// <param name="NoBottleHint">The water cell the "no bottle" event was last raised for.</param>
/// <param name="Player">The body (fixed-point position, velocity, on-ground, yaw and pitch).</param>
/// <param name="Inventory">The slots, in order.</param>
/// <param name="Objective">The count of each step the save keeps, keyed <c>&lt;objective&gt;.&lt;step&gt;</c> (<c>camp.wood</c>, version 5; before it the camp checklist's step alone), in the session's order.</param>
/// <param name="Words">The word records of every noun the learner has met, in ordinal lemma order.</param>
/// <param name="Phrases">The phrase records, one per sheet row, in the sheet's order.</param>
/// <param name="Carded">The nouns whose word card was shown, in ordinal order.</param>
/// <param name="KeyWordsTaught">Whether any NPC's key words were taught. Kept for older readers; an older save's <see langword="true"/> means the shopkeeper's (see <see cref="KeyWordsTaughtBy"/>).</param>
/// <param name="RiverPointed">Whether an NPC has pointed the player at the river.</param>
/// <param name="Pointer">The arrow on screen (not a review question), or <see langword="null"/>.</param>
/// <param name="DeferredSkills">Learner-model steps of shown cards that were waiting for the next collect.</param>
/// <param name="LastRepairs">The repair each NPC's last answered turn ended with.</param>
/// <param name="NextTurnId">The id the next submitted turn gets.</param>
/// <param name="Npcs">Each NPC's conversation state, in the order of <c>game/npcs.json</c> (guide, shopkeeper, villager, fisher, baker, smith, farmer).</param>
/// <param name="Chunks">The world edits: the chunks that differ from the world as generated (<see cref="VoxelChunk.IsChanged"/>), sorted by chunk coordinate.</param>
/// <param name="Lessons">The progress of the lessons (S16): where each one stands and which is active. A save from before S16 has none.</param>
/// <param name="Tips">The first-time tips (S16): which were shown, which wait and which is on screen. A save from before S16 has none.</param>
/// <param name="Learner">The learner's record (S19): the newest history entries and the word rows. A version 1 save has none (an empty history). A version 2 save's rows counted the check answers (S19b); they are taken out on the load.</param>
/// <param name="BookGranted">Whether the book item has been granted (S22): a save written since S20 says so; a save from before S20 does not, and the load gives the learner the book once.</param>
/// <param name="Settings">The player's settings (S24: the sound volumes), or <see langword="null"/> in a save from before S24 (the defaults then apply).</param>
/// <param name="SkippedTo">The play session a test run was skipped to (<c>--skip-to-session</c>, V12c): the sessions before it were counted as done, not played, so the save is
/// no learner's. <see langword="null"/>, and left out of the file, for every other game.</param>
/// <param name="Sitting">The sitting the save was taken in (V25): the play session of the phrase-credit spacing rule, which a launch, a load and a sleep each begin. A load runs in the
/// next one. <see langword="null"/> in a save from before V25, whose <see cref="Session"/> is then the sitting it continues from.</param>
/// <param name="KeyWordsTaughtBy">The ids of the NPCs whose key words were taught (V23a). <see langword="null"/> in a save from before: its <see cref="KeyWordsTaught"/> then means the shopkeeper's.</param>
/// <param name="Systems">The systems that keep state of their own (version 7, N01): the player's needs now, the procedures later. <see langword="null"/> in a save from before version 7, which starts with full needs.</param>
/// <param name="MetNpcs">The ids of the NPCs the player has talked to (L01): a class opens on having met its teacher. <see langword="null"/> in a save from before L01: from the second day on, every NPC then counts as met, as the days used to open them.</param>
/// <param name="Air">The player's air in ticks (version 7, water-plan H03 and H06); <see langword="null"/>, and left out of the file, while the breath is full.</param>
/// <param name="Flow">The cells waiting for a flow update, in queue order (version 7, water-plan H06); <see langword="null"/>, and left out of the file, while none wait.</param>
/// <param name="DialoguePages">Each NPC's conversation lines (U08), so the panel scrolls back over earlier sessions: one page per NPC with lines, at most <see cref="Dialogue.DialogueModel.MaxLines"/> lines each. <see langword="null"/>, and left out of the file, while no NPC has a line; a save without it loads with empty pages. Optional, so it needs no save version.</param>
/// <param name="GivenOrders">The orders the player has taken on (issue #54: a «sí» to the NPC's offer, or the step of the lesson that tells it), ordinal. <see langword="null"/>, and left out of the file, while none is; a save without it has none, and the NPCs offer them again. Optional, so it needs no save version.</param>
/// <param name="PassOpen">True once the pass through the east ring has opened (B08b: every required lesson complete, or the cheat setting); it never closes again. Written only when true; the rockfall's removal is in the world's changed chunks. Optional, so it needs no save version.</param>
public sealed record SaveDocument(
    string Format,
    int Version,
    ulong WorldSeed,
    string SceneName,
    string BundleHash,
    long StartedAt,
    long SkippedSeconds,
    long StoredTime,
    int Session,
    long Tick,
    string StateDigest,
    int SelectedSlot,
    GatherSave Gather,
    BlockPos? NoBottleHint,
    PlayerBodyState Player,
    IReadOnlyList<SlotSave> Inventory,
    IReadOnlyList<ObjectiveSave> Objective,
    IReadOnlyList<WordSave> Words,
    IReadOnlyList<PhraseSave> Phrases,
    IReadOnlyList<string> Carded,
    bool KeyWordsTaught,
    bool RiverPointed,
    PointerSave? Pointer,
    IReadOnlyList<DeferredSkillSave> DeferredSkills,
    IReadOnlyList<LastRepairSave> LastRepairs,
    long NextTurnId,
    IReadOnlyList<NpcSave> Npcs,
    IReadOnlyList<ChunkEdit> Chunks,
    LessonsSave? Lessons = null,
    TipsSave? Tips = null,
    LearnerRecordSave? Learner = null,
    bool BookGranted = false,
    SaveSettings? Settings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SkippedTo = null,
    int? Sitting = null,
    IReadOnlyList<string>? KeyWordsTaughtBy = null,
    SystemsSave? Systems = null,
    IReadOnlyList<string>? MetNpcs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Air = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<FlowEntry>? Flow = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DialoguePageSave>? DialoguePages = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? GivenOrders = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool PassOpen = false);

/// <summary>The player's settings in a save (S24). Presentation only: nothing here reaches the simulation, a turn, a digest or the replay.</summary>
/// <param name="Sound">The volume of each sound category.</param>
/// <param name="TextScale">The size of the interface text in percent (X08): one of <see cref="TextScaleLevels"/>; a save from before has 100.</param>
/// <param name="ViewEffectsOff">True when the player turned the needs' effects on the view off (N05: dim edges, a blink, red edges; the icons and the lines stay). Written only when true, so a save of the default is byte for byte what it was.</param>
/// <param name="RelaxedNeeds">True under the relaxed-needs setting (N05, design decision 17: half the rates, a faint instead of a death). Written only when true.</param>
/// <param name="NeedsRate">The drive rate multiplier (N09), 0.5 to 4 in steps of 0.25; 0 (and so no field) is the default 1. Written only when not 1.</param>
/// <param name="UnderwaterBlur">How much the view softens under water (water-plan H11): a <see cref="Water.UnderwaterBlur"/> as its number; no field is the default (low). Written only when the player chose another level. Optional, so it needs no save version.</param>
/// <param name="Quality">The graphics quality (the menu): a <see cref="Menu.QualityLevel"/> as its number; no field is the default (high). Written only when the player chose another level.</param>
/// <param name="MouseSensitivity">The mouse sensitivity in percent of the normal turn (the menu), 25 to 300; 0 (and so no field) is the default 100. Written only when not 100.</param>
/// <param name="InvertY">True when moving the mouse up looks down (the menu). Written only when true.</param>
/// <param name="FieldOfView">The vertical field of view in degrees (the menu), 60 to 110; 0 (and so no field) is the default 70. Written only when not 70.</param>
/// <param name="ViewDistance">How far the world is drawn in blocks (the menu), 48 to 576 in steps of 16; 0 (and so no field) is Auto, the distance of the graphics quality. Written only when not Auto.</param>
/// <param name="PassCheat">True when the player turned the pass cheat on (B08b: the pass through the east ring opens at once). Written only when true.</param>
/// <param name="Grass">The decorative grass's density in percent (B18, the menu), 0 to 100 in steps of 10; no field is the default (<see cref="Menu.PlayerSettings.DefaultGrass"/>), so 0 (none) is written. Written only when not the default.</param>
/// <param name="Workers">The worker threads setting (B07d): 0 is Auto, else 1 to 8; no field is Auto. Written only when not Auto.</param>
/// <param name="FarDistance">The far view's distance in km (B07d): 0 is Auto (the quality's), else 1 to 16; no field is Auto.</param>
/// <param name="FarDetail">The far view's detail in percent (B07d), 50 to 200 in steps of 25; no field is 100.</param>
/// <param name="FarTrees">The far view's single trees (B07d), as the setting counts it (see <see cref="Menu.SettingId.FarTrees"/>); no field is Auto.</param>
/// <param name="FarTextures">The far view's textured levels (B07d), 0 to 3; no field is 2.</param>
/// <param name="FarExact">How far the far view is made from real chunks (B07d), as the setting counts it (see <see cref="Menu.SettingId.FarExact"/>); no field is Auto.</param>
/// <param name="ShadersOff">True when the player turned the game's own GLSL shaders off (B17b, the menu's Shaders row: the chunks are drawn with MonoGame's effects). Written only when true, so a save of the default is byte for byte what it was.</param>
/// <param name="SunFollowsHourOff">True when the player turned the sun following the hour off (B17c; B22's sun per band and deeper dusk). Written only when true.</param>
/// <param name="ClimateColoursOff">True when the player turned the climate colours off (B17c; B22's land tint). Written only when true.</param>
/// <param name="WaterShaderOff">True when the player turned the water shader off (B17c; with B22's depth tint). Written only when true.</param>
/// <param name="HeightFogOff">True when the player turned the fog by height off (B17c). Written only when true.</param>
/// <param name="FarLightOff">True when the player turned the far view's light off (B17c). Written only when true.</param>
/// <param name="Msaa">The window's antialiasing in samples a pixel (0, 2, 4 or 8; B22, the menu's Graphics group since B17c); no field is the default (4). Written only when not 4, so 0 (none) is written.</param>
/// <param name="FlyCheat">True when the player turned the fly cheat on (no gravity; jump rises, the descend key sinks). Written only when true.</param>
/// <param name="FlySpeed">The flying speed in blocks per second (the fly cheat's slider); no field is the default (<see cref="Physics.VoxelPhysics.DefaultFlyBlocksPerSecond"/>). Written only when not the default.</param>
public sealed record SaveSettings(
    SoundVolumes Sound,
    int TextScale = 100,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ViewEffectsOff = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool RelaxedNeeds = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] double NeedsRate = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Water.UnderwaterBlur? UnderwaterBlur = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Menu.QualityLevel? Quality = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int MouseSensitivity = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool InvertY = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int FieldOfView = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ViewDistance = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool PassCheat = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Grass = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Workers = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FarDistance = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FarDetail = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FarTrees = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FarTextures = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FarExact = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ShadersOff = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool SunFollowsHourOff = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ClimateColoursOff = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool WaterShaderOff = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool HeightFogOff = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool FarLightOff = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Msaa = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool FlyCheat = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FlySpeed = null)
{
    /// <summary>The window's samples a pixel in the save's settings of a new game (B22).</summary>
    public const int DefaultMsaa = 4;

    /// <summary>The samples a pixel the window may open with (0 for none).</summary>
    public static IReadOnlyList<int> MsaaLevels { get; } = [0, 2, 4, 8];

    /// <summary>The samples a pixel this setting stands for: the saved count when it is one the window offers, otherwise the default (4).</summary>
    [JsonIgnore]
    public int EffectiveMsaa => Msaa is { } samples && MsaaLevels.Contains(samples) ? samples : DefaultMsaa;

    /// <summary>The drive rate of the default (N09); a save without the field reads 0, which stands for it.</summary>
    public const double DefaultNeedsRate = 1.0;

    /// <summary>The lowest drive rate.</summary>
    public const double MinNeedsRate = 0.5;

    /// <summary>The highest drive rate.</summary>
    public const double MaxNeedsRate = 4.0;

    /// <summary>The step of the drive rate slider.</summary>
    public const double NeedsRateStep = 0.25;

    /// <summary>The rate a value stands for: 0 (no field) is the default, anything else is clamped to 0.5 to 4 and snapped to a step of 0.25 (a hand-edited value, NaN included).</summary>
    /// <param name="rate">The value read.</param>
    public static double SnapNeedsRate(double rate)
    {
        if (rate == 0 || double.IsNaN(rate))
        {
            return DefaultNeedsRate;
        }

        return Math.Round(Math.Clamp(rate, MinNeedsRate, MaxNeedsRate) / NeedsRateStep) * NeedsRateStep;
    }

    /// <summary>The drive rate this setting stands for (the default when the field is absent).</summary>
    [JsonIgnore]
    public double EffectiveNeedsRate => SnapNeedsRate(NeedsRate);

    /// <summary>The blur level this setting stands for: the saved level when it is a known one, otherwise the default (low).</summary>
    [JsonIgnore]
    public Water.UnderwaterBlur EffectiveUnderwaterBlur =>
        UnderwaterBlur is { } level && Enum.IsDefined(level) ? level : Water.UnderwaterBlurSettings.Default;

    /// <summary>The graphics quality this setting stands for: the saved level when it is a known one, otherwise the default (high).</summary>
    [JsonIgnore]
    public Menu.QualityLevel EffectiveQuality => Quality is { } level && Enum.IsDefined(level) ? level : Menu.QualityLevel.High;

    /// <summary>The mouse sensitivity in percent this setting stands for (the default 100 when the field is absent).</summary>
    [JsonIgnore]
    public int EffectiveMouseSensitivity => MouseSensitivity == 0 ? Menu.PlayerSettings.Spec(Menu.SettingId.MouseSensitivity).Default : Menu.PlayerSettings.Spec(Menu.SettingId.MouseSensitivity).Snap(MouseSensitivity);

    /// <summary>The field of view in degrees this setting stands for (the default 70 when the field is absent).</summary>
    [JsonIgnore]
    public int EffectiveFieldOfView => FieldOfView == 0 ? Menu.PlayerSettings.Spec(Menu.SettingId.FieldOfView).Default : Menu.PlayerSettings.Spec(Menu.SettingId.FieldOfView).Snap(FieldOfView);

    /// <summary>The draw distance in blocks this setting stands for: 0 for Auto (the quality's own), otherwise the saved distance within the menu's range and steps.</summary>
    [JsonIgnore]
    public int EffectiveViewDistance
    {
        get
        {
            var spec = Menu.PlayerSettings.Spec(Menu.SettingId.ViewDistance);
            var snapped = spec.Snap(ViewDistance);
            return snapped <= spec.Min ? 0 : snapped;
        }
    }

    /// <summary>The grass density in percent this setting stands for (the default when the field is absent), within the menu's range and steps.</summary>
    [JsonIgnore]
    public int EffectiveGrass => Grass is { } value ? Menu.PlayerSettings.Spec(Menu.SettingId.Grass).Snap(value) : Menu.PlayerSettings.DefaultGrass;

    /// <summary>What a saved value of a setting stands for: the setting's default when the field is absent, otherwise the value snapped to the setting's range and steps (B07d: the worker count and the far view's settings).</summary>
    /// <param name="id">The setting.</param>
    public int EffectiveOf(Menu.SettingId id)
    {
        var spec = Menu.PlayerSettings.Spec(id);
        var saved = id switch
        {
            Menu.SettingId.Workers => Workers,
            Menu.SettingId.FarDistance => FarDistance,
            Menu.SettingId.FarDetail => FarDetail,
            Menu.SettingId.FarTrees => FarTrees,
            Menu.SettingId.FarTextures => FarTextures,
            Menu.SettingId.FarExact => FarExact,
            Menu.SettingId.FlySpeed => FlySpeed,
            _ => null,
        };
        return saved is { } value ? spec.Snap(value) : spec.Default;
    }

    /// <summary>The field to write for a setting's value: <see langword="null"/> (no field) when it is the setting's default.</summary>
    /// <param name="id">The setting.</param>
    /// <param name="value">The value.</param>
    public static int? OmitDefault(Menu.SettingId id, int value)
    {
        var spec = Menu.PlayerSettings.Spec(id);
        var snapped = spec.Snap(value);
        return snapped == spec.Default ? null : snapped;
    }

    /// <summary>The text sizes the player can choose, in percent (X08): the normal size and two steps up.</summary>
    public static IReadOnlyList<int> TextScaleLevels { get; } = [100, 125, 150];

    /// <summary>The level nearest to a value (a value that is no level in a hand-edited save is snapped to the closest one; ties go to the smaller).</summary>
    /// <param name="percent">The value read.</param>
    public static int SnapTextScale(int percent) => TextScaleLevels.OrderBy(level => Math.Abs(level - percent)).First();

    /// <summary>The same settings with the volumes limited to 0 to 1, the text scale on a level, and every other value in its range (a default value is left out, as the file leaves it).</summary>
    public SaveSettings Clamped() => new(
        Sound.Clamped(),
        SnapTextScale(TextScale),
        ViewEffectsOff,
        RelaxedNeeds,
        SnapNeedsRate(NeedsRate) == DefaultNeedsRate ? 0 : SnapNeedsRate(NeedsRate),
        EffectiveUnderwaterBlur == Water.UnderwaterBlurSettings.Default ? null : EffectiveUnderwaterBlur,
        EffectiveQuality == Menu.QualityLevel.High ? null : EffectiveQuality,
        EffectiveMouseSensitivity == Menu.PlayerSettings.Spec(Menu.SettingId.MouseSensitivity).Default ? 0 : EffectiveMouseSensitivity,
        InvertY,
        EffectiveFieldOfView == Menu.PlayerSettings.Spec(Menu.SettingId.FieldOfView).Default ? 0 : EffectiveFieldOfView,
        EffectiveViewDistance,
        PassCheat,
        EffectiveGrass == Menu.PlayerSettings.DefaultGrass ? null : EffectiveGrass,
        OmitDefault(Menu.SettingId.Workers, EffectiveOf(Menu.SettingId.Workers)),
        OmitDefault(Menu.SettingId.FarDistance, EffectiveOf(Menu.SettingId.FarDistance)),
        OmitDefault(Menu.SettingId.FarDetail, EffectiveOf(Menu.SettingId.FarDetail)),
        OmitDefault(Menu.SettingId.FarTrees, EffectiveOf(Menu.SettingId.FarTrees)),
        OmitDefault(Menu.SettingId.FarTextures, EffectiveOf(Menu.SettingId.FarTextures)),
        OmitDefault(Menu.SettingId.FarExact, EffectiveOf(Menu.SettingId.FarExact)),
        ShadersOff,
        SunFollowsHourOff,
        ClimateColoursOff,
        WaterShaderOff,
        HeightFogOff,
        FarLightOff,
        EffectiveMsaa == DefaultMsaa ? null : EffectiveMsaa,
        FlyCheat,
        OmitDefault(Menu.SettingId.FlySpeed, EffectiveOf(Menu.SettingId.FlySpeed)));
}

/// <summary>
/// The volume of each sound category, 0 to 1 (S24, D-game-18): the NPCs' voices, the effects of the world, the interface and progress sounds, and the ambient beds
/// (S25), and the weather's own beds (E09). A value outside 0 to 1 is clamped by <see cref="Clamped"/>; a save from a hand-edited file never carries the mixer past full scale.
/// </summary>
/// <param name="Voice">The NPCs' pre-rendered voices.</param>
/// <param name="Sfx">The effects: gathering, placing, picking up, crafting, the fire.</param>
/// <param name="Ui">The interface and progress sounds: panels, buttons, chimes, the push-to-talk beeps.</param>
/// <param name="Ambient">The ambient beds.</param>
/// <param name="Weather">The weather's own beds (the rain, E09).</param>
public sealed record SoundVolumes(double Voice = 1.0, double Sfx = 0.8, double Ui = 0.8, double Ambient = 0.6, double Weather = 0.6)
{
    /// <summary>The volumes of a new game.</summary>
    public static SoundVolumes Default { get; } = new();

    /// <summary>The same volumes with every value limited to 0 to 1 (a value that is not a number counts as 0).</summary>
    public SoundVolumes Clamped() => new(Limit(Voice), Limit(Sfx), Limit(Ui), Limit(Ambient), Limit(Weather));

    private static double Limit(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
}

/// <summary>The learner's record in a save (S19): the next entry's number, the newest history entries (oldest first) and the word rows, which are a fold of the whole history.</summary>
/// <param name="NextId">The number the next history entry takes.</param>
/// <param name="History">The newest entries of the history, oldest first (at most <see cref="LearnerRecord.SavedEntries"/>).</param>
/// <param name="Words">The word rows (<see cref="LearnerWords"/>), sorted by lemma then part of speech.</param>
public sealed record LearnerRecordSave(long NextId, IReadOnlyList<InputEntry> History, IReadOnlyList<WordRow> Words);

/// <summary>The first-time tips (S16): the ids shown so far, the ids waiting to be shown (in order), and the one on screen with the tick it came up at.</summary>
/// <param name="Shown">The ids shown so far, in ordinal order (the one on screen is among them).</param>
/// <param name="Queued">The ids waiting, in the order they will be shown.</param>
/// <param name="Current">The id of the tip on screen, or <see langword="null"/>.</param>
/// <param name="Since">The tick the tip on screen came up at (0 when none).</param>
/// <param name="Ready">For each id in <paramref name="Queued"/>, the tick it may come up at (S17: the opening tips wait their delay); <see langword="null"/> in a save from before, which then has them ready at once.</param>
public sealed record TipsSave(IReadOnlyList<string> Shown, IReadOnlyList<string> Queued, string? Current, long Since, IReadOnlyList<long>? Ready = null);

/// <summary>The progress of the lessons (S16): each lesson's state, and the id of the one that has begun and not finished.</summary>
/// <param name="Lessons">One entry per lesson of the game, in the file's order.</param>
/// <param name="Active">The active lesson's id, or <see langword="null"/>.</param>
/// <param name="Replay">The lesson the learner asked to practise again from the book, which starts at its NPC (S26; null for none, and in a save before version 4).</param>
public sealed record LessonsSave(IReadOnlyList<LessonProgressSave> Lessons, string? Active, string? Replay = null);

/// <summary>Where one lesson stands: its status, the step in progress, how the finished steps ended, and the tries and help used on the step in progress.</summary>
/// <param name="Id">The lesson's id.</param>
/// <param name="Status">Not started, in progress or completed.</param>
/// <param name="Step">The index of the step in progress (0-based).</param>
/// <param name="Outcomes">How the finished steps ended, in order.</param>
/// <param name="Attempts">The tries used on the step in progress.</param>
/// <param name="Helped">Whether the help path was used on the step in progress.</param>
/// <param name="Turns">The turns taken at the lesson's turn steps (S19; 0 in a version 1 save).</param>
/// <param name="HelpUses">The help-path uses (S19; 0 in a version 1 save).</param>
/// <param name="DontKnowUses">The "no sé" uses (S19; 0 in a version 1 save).</param>
/// <param name="RepeatUses">The requests to say the line again (S19; 0 in a version 1 save).</param>
/// <param name="CompletedSession">The session the lesson was last completed in (S19; null when never, or unknown in a version 1 save).</param>
/// <param name="EverCompleted">Whether the lesson was ever completed, which stays true while a replay of it runs (S26; null in a save before version 4: then a completed lesson was done).</param>
public sealed record LessonProgressSave(
    string Id,
    LessonStatus Status,
    int Step,
    IReadOnlyList<StepOutcomeSave> Outcomes,
    int Attempts,
    bool Helped,
    int Turns = 0,
    int HelpUses = 0,
    int DontKnowUses = 0,
    int RepeatUses = 0,
    int? CompletedSession = null,
    bool? EverCompleted = null);

/// <summary>How one finished lesson step ended.</summary>
/// <param name="StepId">The step's id.</param>
/// <param name="Outcome">Done, skipped or moved on.</param>
public sealed record StepOutcomeSave(string StepId, StepOutcome Outcome);

/// <summary>The gathering in progress: the block being gathered (or none), its kind and the ticks of holding spent on it.</summary>
/// <param name="Target">The block, or <see langword="null"/> when nothing is being gathered.</param>
/// <param name="Block">The kind of the block.</param>
/// <param name="Progress">The ticks spent.</param>
public sealed record GatherSave(BlockPos? Target, BlockId Block, int Progress);

/// <summary>One inventory slot: the item and how many (0 for an empty slot).</summary>
/// <param name="Item">The item kind.</param>
/// <param name="Count">The count.</param>
public sealed record SlotSave(ItemId Item, int Count);

/// <summary>The count of one step of an objective (its key is <c>&lt;objective&gt;.&lt;step&gt;</c> since version 5).</summary>
/// <param name="Key">The step's key (<c>wood</c>, <c>berry</c>, <c>torch</c>, <c>water</c>).</param>
/// <param name="Count">The count so far.</param>
public sealed record ObjectiveSave(string Key, int Count);

/// <summary>The word record of one catalogue noun (every count the learner has, and the spacing of its credited uses).</summary>
/// <param name="Lemma">The noun.</param>
/// <param name="Heard">Times it was heard: in a guide turn, or on its word card.</param>
/// <param name="Read">Times its label was read in the journal.</param>
/// <param name="Spoken">Communicated turns in which the learner used it.</param>
/// <param name="SpokenCorrect">Of those, the correct ones.</param>
/// <param name="CreditUnits">The credit earned, in units of a use.</param>
/// <param name="CorrectTurns">The turns that earned credit.</param>
/// <param name="LastCreditedAt">The stored time of the last credited use.</param>
/// <param name="LastCreditedSession">The play session of the last credited use.</param>
/// <param name="Lapses">How often the word was revealed after a wrong name.</param>
public sealed record WordSave(
    string Lemma,
    int Heard,
    int Read,
    int Spoken,
    int SpokenCorrect,
    int CreditUnits,
    int CorrectTurns,
    long LastCreditedAt,
    int LastCreditedSession,
    int Lapses);

/// <summary>The progress record of one phrase of the sheet (the counts, the support level and the spacing of its uses).</summary>
/// <param name="Id">The sheet's id.</param>
/// <param name="Level">The support level now.</param>
/// <param name="CardShown">How often its card appeared.</param>
/// <param name="Supported">Credit of the supported uses (units).</param>
/// <param name="Unaided">Credit of the unaided uses (units).</param>
/// <param name="ChoiceOnly">Choice uses.</param>
/// <param name="DontKnow">"No sé" uses.</param>
/// <param name="Transfer">Credit of the transfer uses (units).</param>
/// <param name="FailForward">Fail-forward handovers.</param>
/// <param name="Slips">Communicated uses with a mistake.</param>
/// <param name="Heard">Times the NPC asked it (the phrase's prompt was heard).</param>
/// <param name="Answered">Times the learner answered the NPC's question.</param>
/// <param name="StepUnits">Credit units toward the next support step.</param>
/// <param name="CreditedUses">Credited uses so far.</param>
/// <param name="LastCreditedAt">The stored time of the last credited use.</param>
/// <param name="LastCreditedSession">The play session of the last credited use.</param>
/// <param name="LastUseAt">The stored time of the last use of any kind.</param>
/// <param name="LastUseSession">The play session of the last use of any kind.</param>
public sealed record PhraseSave(
    string Id,
    SupportLevel Level,
    int CardShown,
    int Supported,
    int Unaided,
    int ChoiceOnly,
    int DontKnow,
    int Transfer,
    int FailForward,
    int Slips,
    int Heard,
    int Answered,
    int StepUnits,
    int CreditedUses,
    long LastCreditedAt,
    int LastCreditedSession,
    long LastUseAt,
    int LastUseSession);

/// <summary>The arrow on screen: the noun it points at and the block.</summary>
/// <param name="Place">The noun.</param>
/// <param name="Target">The block.</param>
public sealed record PointerSave(string Place, GuideCell Target);

/// <summary>A learner-model step waiting for the next collect (a card shown, for an NPC).</summary>
/// <param name="Npc">The NPC.</param>
/// <param name="Skill">The skill id (<c>phrase.pedir-agua</c>).</param>
public sealed record DeferredSkillSave(string Npc, string Skill);

/// <summary>The repair an NPC's last answered turn ended with.</summary>
/// <param name="Npc">The NPC.</param>
/// <param name="Repair">The repair kind.</param>
public sealed record LastRepairSave(string Npc, RepairKind Repair);

/// <summary>One NPC's conversation: the context pieces the host owns, and the host decisions NpcService remembers.</summary>
/// <param name="Agent">The agent id.</param>
/// <param name="StateId">The agent's state.</param>
/// <param name="InteractionCounter">How many interactions the agent has had (it seeds its random draws).</param>
/// <param name="Now">The stored time of the agent's last turn (the context's <c>Now</c>).</param>
/// <param name="HostStateHash">The context's digest of the host state at its last turn.</param>
/// <param name="Learner">The learner model of this agent.</param>
/// <param name="RepeatCounts">The repeat counts before its next turn, in ordinal tag order.</param>
/// <param name="Visits">How often the player has opened the talk.</param>
/// <param name="Unproductive">The lines at the stall that asked for nothing (the fail-forward count).</param>
/// <param name="Uncertain">The uncertain recognitions so far.</param>
/// <param name="Helped">Whether the player asked for help at the stall.</param>
/// <param name="BottleGiven">Whether the bottle was handed over.</param>
/// <param name="Offered">Whether the guide made her transfer offer.</param>
/// <param name="LastRepair">The repair of the agent's last turn.</param>
public sealed record NpcSave(
    string Agent,
    string StateId,
    long InteractionCounter,
    long Now,
    string HostStateHash,
    LearnerSave Learner,
    IReadOnlyList<RepeatSave> RepeatCounts,
    int Visits,
    int Unproductive,
    int Uncertain,
    bool Helped,
    bool BottleGiven,
    bool Offered,
    RepairKind? LastRepair);

/// <summary>A learner model: its BKT parameters and every skill it tracks, in ordinal skill order.</summary>
/// <param name="PriorKnown">The prior probability of a known skill.</param>
/// <param name="Learn">The learn probability.</param>
/// <param name="Slip">The slip probability.</param>
/// <param name="Guess">The guess probability.</param>
/// <param name="Skills">The tracked skills.</param>
public sealed record LearnerSave(double PriorKnown, double Learn, double Slip, double Guess, IReadOnlyList<SkillSave> Skills);

/// <summary>One learner skill: its probability of being known, the attempts and the errors.</summary>
/// <param name="SkillId">The skill id (<c>word.agua</c>, <c>phrase.pedir-agua</c>, <c>intent.request_item</c>, an error family).</param>
/// <param name="PKnown">The probability the skill is known.</param>
/// <param name="Attempts">The observations.</param>
/// <param name="Errors">The incorrect observations.</param>
public sealed record SkillSave(string SkillId, double PKnown, int Attempts, int Errors);

/// <summary>One repeat count: an error tag (or <c>not_communicated</c>) and how many times in a row it was seen.</summary>
/// <param name="Tag">The tag.</param>
/// <param name="Count">The count.</param>
public sealed record RepeatSave(string Tag, int Count);

/// <summary>
/// The game's systems that keep state of their own (save version 7, N01; version 8 adds <c>sleep</c>): a map from a system's name to its entry, so a system joins the save without another format change.
/// <c>needs</c> is the player's needs; <c>chest</c> the belongings a death left (N04); W06 adds <c>procedures</c>. An entry a save lacks (every save before version 7) is the system as a new game has it.
/// </summary>
/// <param name="Needs">The player's needs (<see cref="Vocaterra.Game.Core.Needs.PlayerNeeds"/>), or <see langword="null"/> for full needs.</param>
/// <param name="Chest">The belongings chest a death left standing (its cell and what is in it), or <see langword="null"/> for none. The chest block itself is in the world's block edits.</param>
/// <param name="Bushes">The cells of the berry bushes picked by hand since the last night (N06): each is a plain bush in the world's block edits until a night grows it back; <see langword="null"/> for none.</param>
/// <param name="Gifts">The gifts given today (N06, <c>GiftRule.Key</c>: <c>shopkeeper:bread</c>), which the next night makes available again; <see langword="null"/> for none.</param>
/// <param name="Planted">The berry bushes planted at the camp that stand (N08): each is also in <paramref name="Bushes"/> until a night grows it; <see langword="null"/> for none.</param>
/// <param name="Procedures">The procedures (W06): what the learner knows, their records and the run in progress; <see langword="null"/> with none, which starts with none.</param>
/// <param name="Sleep">The sleep places' state (Y02, version 8): the lodging given, the place last slept at and the places made in play; <see langword="null"/> for no lodging, no last place and no places made in play.</param>
/// <param name="Land">The player's land (Y09, <c>systems.land</c>): <c>{"pending": {x, z, width, depth}, "owned": {x, z, width, depth}}</c>, each part only when the plot exists; <see langword="null"/> for no plot. The rules (the protected regions) are not saved: they come from the scene and the bundle. No version bump: a save without the entry has no plot.</param>
/// <param name="Atmosphere">The day clock's offset and held hour (E01) and the weather's state (E04), <c>systems.atmosphere</c>: <c>{"offset", "frozenHour", "weather"}</c>, written only when it is not a new game's (the offset is 0, no hour is held and the weather is still in its first spell); <see langword="null"/> starts both as a new game has them.</param>
public sealed record SystemsSave(
    Vocaterra.Game.Core.Needs.NeedsSave? Needs = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ChestSave? Chest = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] BushSave[]? Bushes = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string[]? Gifts = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] BushSave[]? Planted = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ProceduresSave? Procedures = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] SleepSave? Sleep = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Vocaterra.Game.Core.Atmosphere.AtmosphereState? Atmosphere = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Vocaterra.Game.Core.Land.LandSave? Land = null);

/// <summary>
/// What the sleep places keep across a sleep and a load (Y02): whether the innkeeper has given the room, the place the player last slept at and the places made while the game runs.
/// Each part is optional: no lodging is <see langword="false"/> (left out), no last place and no places made in play are <see langword="null"/>.
/// </summary>
/// <param name="LodgingGiven">Whether the player has been given lodging, which meets a sleep place whose access is <c>lodging</c>; left out when false.</param>
/// <param name="Last">The place last slept at, or <see langword="null"/> for none.</param>
/// <param name="Places">The places made in play (<c>SleepPlaces.Add</c>), or <see langword="null"/> for none. The authored places are in the bundle, not here.</param>
/// <param name="Camp">The camp the player set up with the camp kit (Y12): where its footprint lies and which way it faces; its two places are in <paramref name="Places"/>. <see langword="null"/> for none.</param>
/// <param name="KitOwed">Whether a camp whose blocks were gone was dropped by a load and the camp kit could not go back into a full bag yet (Y12); left out when false.</param>
public sealed record SleepSave(
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool LodgingGiven = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] LastSleepSave? Last = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] SleepPlaceSave[]? Places = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Vocaterra.Game.Core.Land.CampSave? Camp = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool KitOwed = false);

/// <summary>The place last slept at: its id and, for a place made in play, its cell.</summary>
/// <param name="Id">The place's id.</param>
/// <param name="X">The cell's X, or <see langword="null"/> for an authored place.</param>
/// <param name="Y">The cell's Y, or <see langword="null"/> for an authored place.</param>
/// <param name="Z">The cell's Z, or <see langword="null"/> for an authored place.</param>
public sealed record LastSleepSave(
    string Id,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? X = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Y = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Z = null);

/// <summary>A sleep place made in play: what <c>SleepPlaces.Add</c> needs.</summary>
/// <param name="Id">The place's id.</param>
/// <param name="X">The cell's X.</param>
/// <param name="Y">The cell's Y.</param>
/// <param name="Z">The cell's Z.</param>
/// <param name="Kind"><c>fire</c> or <c>bed</c>.</param>
/// <param name="RadiusBlocks">How far across, in blocks, the player may stand to sleep there.</param>
/// <param name="HeightBlocks">How far in height, in blocks.</param>
/// <param name="Access"><c>always</c> or <c>lodging</c>.</param>
/// <param name="Point">The scene point it was made at, or <see langword="null"/>.</param>
/// <param name="Wake">The cell the player wakes on after a death (the camp's places, Y12), or <see langword="null"/>.</param>
public sealed record SleepPlaceSave(
    string Id,
    int X,
    int Y,
    int Z,
    string Kind,
    int RadiusBlocks,
    int HeightBlocks,
    string Access,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Point = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] CellSave? Wake = null);

/// <summary>A block cell in a save.</summary>
/// <param name="X">The cell's X.</param>
/// <param name="Y">The cell's Y.</param>
/// <param name="Z">The cell's Z.</param>
public sealed record CellSave(int X, int Y, int Z);

/// <summary>A cell of a picked berry bush (N06, design 9.6) that a night grows back.</summary>
/// <param name="X">The cell's X.</param>
/// <param name="Y">The cell's Y.</param>
/// <param name="Z">The cell's Z.</param>
public sealed record BushSave(int X, int Y, int Z);

/// <summary>The belongings chest of a death (N04, design 9.7): the cell of its block and the stacks it holds.</summary>
/// <param name="X">The cell's X.</param>
/// <param name="Y">The cell's Y.</param>
/// <param name="Z">The cell's Z.</param>
/// <param name="Stacks">What is in it, one entry per item kind.</param>
public sealed record ChestSave(int X, int Y, int Z, SlotSave[] Stacks);

/// <summary>The procedures of a save (W06, <c>systems.procedures</c>, design 10.13): the records, the evidence of acts, and the run in progress.</summary>
/// <param name="Version">The entry's version (1).</param>
/// <param name="Known">The procedures the learner has started.</param>
/// <param name="Progress">The records, one per procedure started.</param>
/// <param name="Evidence">The right acts per instruction row, heard and read apart.</param>
/// <param name="Run">The run in progress, or <see langword="null"/>.</param>
/// <param name="OfferedToday">The NPCs whose offer to bake was made since the last night (W08, design 10.14: once per in-game day); <see langword="null"/> for none.</param>
/// <param name="GrantedToday">The procedures whose ingredients a teacher gave since the last night (W08, design decision 20: once per in-game day after the first bake); <see langword="null"/> for none, and in a save made before W08.</param>
public sealed record ProceduresSave(
    int Version,
    string[] Known,
    ProcedureProgressSave[] Progress,
    ActEvidenceSave[] Evidence,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ProcedureRunSave? Run = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string[]? GrantedToday = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string[]? OfferedToday = null);

/// <summary>One procedure's record in a save.</summary>
/// <param name="Id">The procedure.</param>
/// <param name="Runs">Runs started.</param>
/// <param name="Completed">Runs finished.</param>
/// <param name="CleanSittings">The play sessions in which it was finished without help.</param>
/// <param name="FirstTimeSteps">Steps right the first time, over all runs.</param>
/// <param name="Mastered">Whether it is mastered.</param>
public sealed record ProcedureProgressSave(string Id, int Runs, int Completed, int[] CleanSittings, int FirstTimeSteps, bool Mastered);

/// <summary>The right acts on one instruction row in a save, by how it was presented.</summary>
/// <param name="Row">The row.</param>
/// <param name="Heard">Acts with the audio played.</param>
/// <param name="Read">Acts with the text shown.</param>
public sealed record ActEvidenceSave(string Row, int Heard, int Read);

/// <summary>The run in progress (design 10.13): the drawn order, the steps done, the slips, the steps helped and what is shown or folded.</summary>
/// <param name="Procedure">The procedure.</param>
/// <param name="Station">The station.</param>
/// <param name="State">The state: <c>InRun</c>, <c>ReadyToCollect</c> or <c>Done</c>.</param>
/// <param name="Teacher">Whether the teacher is present.</param>
/// <param name="Bake">The run's number (the draw's index).</param>
/// <param name="Order">The steps in the drawn order.</param>
/// <param name="Done">The steps done.</param>
/// <param name="Slips">The wrong acts per step.</param>
/// <param name="Helped">The steps done or shown with help.</param>
/// <param name="Shown">The rows whose instruction was shown this run.</param>
/// <param name="Folded">The chunks (and <c>goal</c>) shown folded this run.</param>
/// <param name="Unfolded">The chunks (and <c>goal</c>) help unfolded this run.</param>
public sealed record ProcedureRunSave(
    string Procedure,
    string Station,
    string State,
    bool Teacher,
    int Bake,
    string[] Order,
    string[] Done,
    SlipSave[] Slips,
    string[] Helped,
    string[] Shown,
    string[] Folded,
    string[] Unfolded);

/// <summary>The wrong acts on one step.</summary>
/// <param name="Step">The step's id.</param>
/// <param name="Count">How many.</param>
public sealed record SlipSave(string Step, int Count);
