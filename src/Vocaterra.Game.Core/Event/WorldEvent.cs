namespace Vocaterra.Game.Core.Event;

/// <summary>A thing that happened in the world: an item given, the beacon set, the bottle filled, a step done, the river found without Spanish.</summary>
/// <param name="Tick">The simulation tick.</param>
/// <param name="Time">The stored time.</param>
/// <param name="Name">What happened, one of <see cref="WorldEvents"/>.</param>
/// <param name="Npc">The NPC it is about, or <see langword="null"/>.</param>
/// <param name="Detail">What it is about (<c>botella</c>, <c>río</c>), or <see langword="null"/>.</param>
public sealed record WorldEvent(long Tick, long Time, string Name, string? Npc = null, string? Detail = null) : GameEvent(Tick, Time)
{
    /// <inheritdoc />
    public override string Family => "world";

    /// <inheritdoc />
    public override string Text => $"world {Name}{(Npc is null ? string.Empty : $" at {Npc}")}{(Detail is null ? string.Empty : $" ({Detail})")}";
}

/// <summary>The names of the world events (<see cref="WorldEvent.Name"/>).</summary>
public static class WorldEvents
{
    /// <summary>The panel opened on an NPC.</summary>
    public const string TalkOpened = "talk_opened";

    /// <summary>The panel closed.</summary>
    public const string TalkClosed = "talk_closed";

    /// <summary>An NPC put an item in the inventory (the detail is the item's noun and how it came: request, offer or fail_forward).</summary>
    public const string ItemGiven = "item_given";

    /// <summary>The villager's answer set the beacon on the nearest river block.</summary>
    public const string BeaconSet = "beacon_set";

    /// <summary>The player filled the bottle at the river.</summary>
    public const string BottleFilled = "bottle_filled";

    /// <summary>The player worked on water without a bottle: a hint was shown, nothing was blocked.</summary>
    public const string WaterWithoutBottle = "water_without_bottle";

    /// <summary>The player filled the bottle without ever having been pointed at the river by an NPC: they found it without Spanish.</summary>
    public const string FoundWithoutSpanish = "found_without_spanish";

    /// <summary>A step of the camp objective was done (the detail is the checklist's text of the step).</summary>
    public const string ObjectiveStep = "objective_step";

    /// <summary>The whole camp objective is complete.</summary>
    public const string ObjectiveComplete = "objective_complete";

    /// <summary>The guide made the transfer offer ("¿Quieres agua?") after the objective was complete.</summary>
    public const string TransferOffered = "transfer_offered";

    /// <summary>The key words of the stall (<c>agua</c>, <c>pan</c>) were shown as word cards before the shopkeeper's first question (the detail lists them).</summary>
    public const string KeyWordsShown = "key_words_shown";

    /// <summary>The guide's review asked a due phrase in context (the detail is the phrase id).</summary>
    public const string PhraseReviewAsked = "phrase_review_asked";

    /// <summary>A phrase said "no sé" to is asked once more in this session (X04, R02; the detail is the phrase id).</summary>
    public const string SecondTryAsked = "second_try_asked";

    /// <summary>The second try was answered (X04, R02; the detail is the phrase id and <c>recovered</c> when it was said correctly without help, else <c>not_yet</c>).</summary>
    public const string SecondTryAnswered = "second_try_answered";

    /// <summary>A phrase's support card appeared (the detail is the phrase id and how: <c>level</c> or <c>help</c>).</summary>
    public const string SupportCardShown = "support_card_shown";

    /// <summary>An item entered the inventory by a pickup (S16: a lesson's <c>do</c> step waits for it; the detail is the item's noun, or its name when it has none).</summary>
    public const string ItemPickedUp = "item_picked_up";

    /// <summary>A first-time tip was shown (S16; the detail is the tip's id).</summary>
    public const string TipShown = "tip_shown";

    /// <summary>The player met the guide (S17): the first time the panel opened on her, or the first step of the camp's checklist, whichever came first. The objective line then stops sending the learner down the trail.</summary>
    public const string GuideMet = "guide_met";

    /// <summary>A torch was set on the rim of the fire ring at the player's own camp (S17): the fire is lit.</summary>
    public const string FireLit = "fire_lit";

    /// <summary>The session's ending (S17): the camp checklist is complete and the player's own fire is lit. The front-end shows the closing moment.</summary>
    public const string SessionComplete = "session_complete";

    /// <summary>The camp's checklist was complete and the player was given a spare torch for their own fire (S17; the detail says how, or that the bag was full).</summary>
    public const string SpareTorch = "spare_torch";

    /// <summary>
    /// A torch was placed at a fire ring (CONTENT_PLAN C06): in the three-by-three square around a ring stone, on its layer or on top of a stone. The detail is
    /// the ring's prop name (<c>firering</c> at the guide's camp, <c>firering.player</c> at the player's own); in a scene that names no camp ring, every ring
    /// that is not the player's counts as <c>firering</c>. The camp's checklist step waits for it.
    /// </summary>
    public const string TorchAtRing = "torch_at_ring";

    /// <summary>The guide offered the review deck's warm-up at the first talk of a session (X06, R06; the detail lists the phrase ids).</summary>
    public const string WarmupOffered = "warmup_offered";

    /// <summary>The warm-up began: the learner accepted the offer, or asked for it from the book (X06; the detail lists the phrase ids).</summary>
    public const string WarmupStarted = "warmup_started";

    /// <summary>The warm-up ended (X06; the detail says how many of its items were answered).</summary>
    public const string WarmupEnded = "warmup_ended";

    /// <summary>The learner skipped the warm-up offer (X06).</summary>
    public const string WarmupSkipped = "warmup_skipped";

    /// <summary>The learner skipped one item of the warm-up (X06; the detail is the phrase id).</summary>
    public const string ReviewSkipped = "review_skipped";

    /// <summary>A phrase of an earlier session was reviewed and answered, in the warm-up or in a lesson's review step (X06; the detail is the phrase id and <c>warm-up</c> or <c>lesson</c>).</summary>
    public const string ReviewAnswered = "review_answered";

    /// <summary>Every world event name, in the order above (the tips, the lessons and the objectives check the names they use against it).</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        TalkOpened, TalkClosed, ItemGiven, BeaconSet, BottleFilled, WaterWithoutBottle, FoundWithoutSpanish, ObjectiveStep, ObjectiveComplete, TransferOffered,
        KeyWordsShown, PhraseReviewAsked, SupportCardShown, ItemPickedUp, TipShown, GuideMet, FireLit, SessionComplete, SpareTorch, SecondTryAsked, SecondTryAnswered,
        TorchAtRing,
        WarmupOffered, WarmupStarted, WarmupEnded, WarmupSkipped, ReviewSkipped, ReviewAnswered,
    ];
}
