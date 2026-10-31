using System.Globalization;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Es.Normalization;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// The test aid <c>--skip-to-session &lt;n&gt;</c> (V12c): a new game is put where a learner who played sessions 1 to n-1 and did everything would stand, so a playtester can
/// try a later session's lesson at once (<c>--session n</c> only raises the session number, and a lesson that comes <c>after</c> another waits for it). Built from the data alone,
/// session by session, so a later session needs no code: the lessons of each earlier session are counted as completed in their own session; session 1's ending is reached
/// (the camp done and the player's own fire lit); each earlier session's order is delivered, its goods brought and its reward paid by the NPC's own thanks; and each lesson's
/// purchases are made (the two loaves of <c>numeros-y-pan</c>), so the coins and the items in hand follow the plan's path. No practice is faked: no turn is taken, and the
/// word and phrase records stay as a new game's. The session then begins as play session n (<see cref="VoxelSession.BeginPlaySession"/>), and the session and its saves
/// are marked (<see cref="VoxelSession.SkippedTo"/>, the world event <see cref="WorldEvents.SessionSkipped"/>).
/// </summary>
/// <remarks>
/// Gathered things are not carried over: the camp's wood and fruit and the river's water were used in the play, and the next session's lesson and order point at the trees
/// and the bushes for what they need. What only an NPC gives (the coins, the bread bought, the axe) is in the bag.
/// </remarks>
public static class SessionSkip
{
    /// <summary>A purchase a lesson makes: the NPC, the item, how many and the price of one, in coins.</summary>
    /// <param name="Npc">The NPC that sells.</param>
    /// <param name="Item">The item bought.</param>
    /// <param name="Count">How many.</param>
    /// <param name="Price">The price of one, in coins.</param>
    public sealed record Purchase(string Npc, ItemId Item, int Count, int Price);

    /// <summary>The last play session that has a lesson: the highest session the skip goes to (6 today).</summary>
    /// <param name="lessons">The game's lessons.</param>
    public static int LastSession(LessonBook lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        return lessons.Lessons.Max(static l => l.Session);
    }

    /// <summary>What the console and the session log say of a skipped run: <c>skipped to play session 4 (sessions 1 to 3 counted as done; test aid)</c>.</summary>
    /// <param name="session">The play session the run was skipped to.</param>
    public static string Line(int session) =>
        string.Create(CultureInfo.InvariantCulture, $"skipped to play session {session} (sessions 1 to {session - 1} counted as done; test aid)");

    /// <summary>Puts a new game in play session <paramref name="target"/> as a learner who did everything before it would have it (see the type's summary).</summary>
    /// <param name="session">A new game: play session 1, nothing begun.</param>
    /// <param name="target">The play session to start in, from 2 to <see cref="LastSession"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The session is not one the skip goes to.</exception>
    /// <exception cref="InvalidOperationException">The game is not new, or the data cannot be played through (an order its NPC does not take).</exception>
    public static void Apply(VoxelSession session, int target)
    {
        ArgumentNullException.ThrowIfNull(session);
        var last = LastSession(session.Lessons.Book);
        if (target < 2 || target > last)
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, $"The skip goes to a play session from 2 to {last}, the last that has a lesson.");
        }

        if (session.PlaySession != 1 || session.Simulation.Tick != 0 || session.Lessons.Book.Lessons.Any(l => session.Lessons.StatusOf(l.Id) != LessonStatus.NotStarted))
        {
            throw new InvalidOperationException("Only a new game can be skipped to a later session.");
        }

        var words = session.Words.Records.Values.ToList();
        var carded = session.CardedLemmas();
        for (var played = 1; played < target; played++)
        {
            session.PlaySession = played;
            foreach (var lesson in session.Lessons.Book.Lessons.Where(l => l.Session == played))
            {
                session.Lessons.CountAsCompleted(lesson.Id, played);
            }

            if (played == 1)
            {
                session.SkipToEnding();
            }

            foreach (var order in session.Objectives.Where(o => o.Spec.IsOrder && o.Spec.Session == played && !o.Complete))
            {
                session.SkipDeliver(order);
            }

            foreach (var purchase in session.Lessons.Book.Lessons.Where(l => l.Session == played).SelectMany(l => Purchases(l, session.Data)))
            {
                session.SkipBuy(purchase);
            }
        }

        session.ForgetSkippedWords(words, carded);
        session.PlaySession = target;
        session.SkippedTo = target;
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.SessionSkipped, null, Line(target)));
        session.BeginPlaySession(); // the new session's lesson is named and pointed at, as after a load
    }

    /// <summary>
    /// The purchases a lesson makes: each <c>do</c> step that waits for an NPC to give an item the NPC sells (<c>sell</c> in <c>game/host-events.json</c>), as many as the
    /// lesson's last request at that NPC before it names (its phrase's Spanish: <c>Quiero dos panes, por favor.</c> is two; one when it names no number), at the NPC's price.
    /// </summary>
    /// <param name="lesson">The lesson.</param>
    /// <param name="data">The game's content: the nouns, the host events and the phrase sheet.</param>
    public static IReadOnlyList<Purchase> Purchases(LessonDefinition lesson, GameData data)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(data);
        var purchases = new List<Purchase>();
        for (var i = 0; i < lesson.Steps.Count; i++)
        {
            if (lesson.Steps[i] is not { Type: LessonStepType.Do, Trigger: { Event: WorldEvents.ItemGiven, Npc: { } npc, Detail: { } detail } }
                || data.Nouns.ForLemma(detail.Split(',')[0].Trim()) is not { } noun)
            {
                continue;
            }

            var sale = data.HostEvents.Mapped()
                .Where(mapped => string.Equals(mapped.Npc, npc, StringComparison.Ordinal))
                .Select(mapped => data.HostEvents.Of(npc, mapped.HostEvent))
                .FirstOrDefault(effect => effect.Kind == NpcEffectKind.Sell && noun.Items.Contains(effect.Item));
            if (sale is not null)
            {
                purchases.Add(new Purchase(npc, sale.Item, RequestedCount(lesson, i, npc, data), sale.Price));
            }
        }

        return purchases;
    }

    // How many items the lesson's request at an NPC asks for: the first number in the phrase of the last step before `before` that is said at that NPC and asks for an
    // item (intent request_item), at most what a sale gives at once; one when there is no such step or it names no number.
    private static int RequestedCount(LessonDefinition lesson, int before, string npc, GameData data)
    {
        for (var i = before - 1; i >= 0; i--)
        {
            var step = lesson.Steps[i];
            if (step.Phrase is not { } phrase || !string.Equals(lesson.NpcOf(step), npc, StringComparison.Ordinal)
                || data.Phrases.Find(phrase) is not { Intent: "request_item" } row)
            {
                continue;
            }

            var tokens = row.Spanish.ToLowerInvariant().Split([' ', ',', '.', '¿', '?', '¡', '!'], StringSplitOptions.RemoveEmptyEntries);
            for (var t = 0; t < tokens.Length; t++)
            {
                if (SpanishNumbers.TryParsePrefix(tokens, t, out var number) > 0 && number > 0)
                {
                    return Math.Min(number, NpcEffect.MaxQuantity);
                }
            }

            return 1;
        }

        return 1;
    }
}
