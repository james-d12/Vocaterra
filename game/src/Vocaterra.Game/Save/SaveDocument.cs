using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game;

/// <summary>
/// One saved game (format <c>vocaterra-save</c>, version 1), written as canonical JSON by <see cref="SaveGame"/>. It holds what a run needs to
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
/// <param name="Session">The play session the save was taken in. A load runs in the next one.</param>
/// <param name="Tick">The simulation's tick.</param>
/// <param name="StateDigest">The simulation's state digest at the save (a load checks the restored world against it).</param>
/// <param name="SelectedSlot">The selected hotbar slot.</param>
/// <param name="Gather">The gathering in progress (the target, its kind and the ticks spent), or no target.</param>
/// <param name="NoBottleHint">The water cell the "no bottle" event was last raised for.</param>
/// <param name="Player">The body (fixed-point position, velocity, on-ground, yaw and pitch).</param>
/// <param name="Inventory">The slots, in order.</param>
/// <param name="Objective">The count of each step of the camp objective, in checklist order.</param>
/// <param name="Words">The word records of every noun the learner has met, in ordinal lemma order.</param>
/// <param name="Phrases">The phrase records, one per sheet row, in the sheet's order.</param>
/// <param name="Carded">The nouns whose word card was shown, in ordinal order.</param>
/// <param name="KeyWordsTaught">Whether the stall's key words were taught (the shopkeeper's first panel opening).</param>
/// <param name="RiverPointed">Whether an NPC has pointed the player at the river.</param>
/// <param name="Pointer">The arrow on screen (not a review question), or <see langword="null"/>.</param>
/// <param name="DeferredSkills">Learner-model steps of shown cards that were waiting for the next collect.</param>
/// <param name="LastRepairs">The repair each NPC's last answered turn ended with.</param>
/// <param name="NextTurnId">The id the next submitted turn gets.</param>
/// <param name="Npcs">Each NPC's conversation state, in agent order (guide, shopkeeper, villager).</param>
/// <param name="Chunks">The scene diff: the chunks that differ from the fresh scene, in chunk order.</param>
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
    IReadOnlyList<ChunkEdit> Chunks);

/// <summary>The gathering in progress: the block being gathered (or none), its kind and the ticks of holding spent on it.</summary>
/// <param name="Target">The block, or <see langword="null"/> when nothing is being gathered.</param>
/// <param name="Block">The kind of the block.</param>
/// <param name="Progress">The ticks spent.</param>
public sealed record GatherSave(BlockPos? Target, BlockId Block, int Progress);

/// <summary>One inventory slot: the item and how many (0 for an empty slot).</summary>
/// <param name="Item">The item kind.</param>
/// <param name="Count">The count.</param>
public sealed record SlotSave(ItemId Item, int Count);

/// <summary>The count of one step of the camp objective.</summary>
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
