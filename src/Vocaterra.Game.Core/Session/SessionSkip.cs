using System.Globalization;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Es.Normalization;

namespace Vocaterra.Game.Core.Session;

/// <summary>
/// The test aid <c>--skip-to-session &lt;n&gt;</c> (V12c): a new game is put where a learner who played sessions 1 to n-1 and did everything would stand, so a playtester can
/// try a later session's lesson at once (<c>--session n</c> only raises the session number, and a lesson that comes <c>after</c> another waits for it). Built from the data alone,
/// session by session, so a later session needs no code: the lessons of each earlier session are counted as completed in their own session; session 1's ending is reached
/// (the camp done and the room at the inn given); each earlier session's order is delivered, its goods brought (the coins it asks for paid from the bag, V13) and its
/// reward paid by the NPC's own thanks; and each lesson's purchases are made (the two loaves of <c>numeros-y-pan</c>), so the coins and the items in hand follow the
/// plan's path. No practice is faked: no turn is taken, and the word and phrase records stay as a new game's. The session then begins as play session n
/// (<see cref="VoxelSession.BeginPlaySession"/>), and the session and its saves are marked (<see cref="VoxelSession.SkippedTo"/>, the world event
/// <see cref="WorldEvents.SessionSkipped"/>).
/// </summary>
/// <remarks>
/// Gathered things are not carried over: the camp's wood and fruit were used in the play, and the next session's lesson and order point at the trees and the bushes for
/// what they need. What only an NPC gives (the coins, the bread bought, the axe) is in the bag, and so is the shopkeeper's bottle of session 1, full of the river's water the
/// camp's checklist counted (V13: nothing in the game empties a bottle, and the farmer's order of session 7 takes it); the shopkeeper counts it as given and his key words as
/// taught (V09), so a later visit is not a first one.
/// </remarks>
public static class SessionSkip
{
    /// <summary>A purchase a lesson makes: the NPC, the item, how many and the price of one, in coins.</summary>
    /// <param name="Npc">The NPC that sells.</param>
    /// <param name="Item">The item bought.</param>
    /// <param name="Count">How many.</param>
    /// <param name="Price">The price of one, in coins.</param>
    public sealed record Purchase(string Npc, ItemId Item, int Count, int Price);

    /// <summary>The last play session the skip goes to: 10, the market (V15). The bakery class of session 11 (W10) is optional and needs the bread the bake makes, so it is reached by playing.</summary>
    public const int MarketSession = 10;

    /// <summary>The last play session that has a lesson and that the skip goes to (<see cref="MarketSession"/> at most).</summary>
    /// <param name="lessons">The game's lessons.</param>
    public static int LastSession(LessonBook lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        return Math.Min(MarketSession, lessons.Lessons.Max(static l => l.Session));
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

        if (session.Day != 1 || session.Simulation.Tick != 0 || session.Lessons.Book.Lessons.Any(l => session.Lessons.StatusOf(l.Id) != LessonStatus.NotStarted))
        {
            throw new InvalidOperationException("Only a new game can be skipped to a later session.");
        }

        var words = session.Words.Records.Values.ToList();
        var carded = session.CardedLemmas();
        for (var played = 1; played < target; played++)
        {
            session.Day = played;
            foreach (var lesson in session.Lessons.Book.Lessons.Where(l => l.Session == played))
            {
                session.Lessons.CountAsCompleted(lesson.Id, played);
                session.MarkMet(lesson.Npc); // L01: a class opens on having met its teacher, and the skip counts the classes before the target as done
            }

            if (played == 1)
            {
                session.SkipToEnding();
            }

            foreach (var order in session.Objectives.Where(o => o.Spec.IsOrder && o.Spec.Session == played && !o.Complete))
            {
                session.MarkMet(order.Spec.DeliverTo!);
                session.GiveOrder(order.Spec.DeliverTo!, order.Id, "skip"); // an order counts once taken on (issue #54); its session only says when the skip delivers it
                session.SkipDeliver(order);
            }

            foreach (var purchase in session.Lessons.Book.Lessons.Where(l => l.Session == played).SelectMany(l => Purchases(l, session.Data)))
            {
                session.SkipBuy(purchase);
            }
        }

        foreach (var lesson in session.Lessons.Book.Lessons.Where(l => l.Session <= target))
        {
            session.MarkMet(lesson.Npc); // the target session's own teachers count as met too, so its class is open at once (a test aid)
        }

        session.ForgetSkippedWords(words, carded);
        session.Day = target;
        session.Sitting = target; // a skipped-to game is a different play session, as the sessions before it were
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
                .Select(effect => effect.Kind == NpcEffectKind.Sell ? effect.Priced(noun) ?? (effect.Prices is null ? effect : null) : null) // W04: a price list sells the noun at its own price
                .FirstOrDefault(effect => effect is not null && noun.Items.Contains(effect.Item));
            if (sale is not null)
            {
                purchases.Add(new Purchase(npc, sale.Item, RequestedCount(lesson, i, npc, noun, data), sale.Price));
            }
        }

        return purchases;
    }

    // How many items the lesson's request at an NPC asks for: the first number in the phrase of the last step before `before` that is said at that NPC and asks for an
    // item (intent request_item), as a sale gives it (no cap since V19). A lesson with no such step (the market's do step has none, V23a) is read from the first later step whose
    // phrase names the item and a number ("Compré tres manzanas."); one when there is neither.
    private static int RequestedCount(LessonDefinition lesson, int before, string npc, NounEntry noun, GameData data)
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
                    return number;
                }
            }

            return 1;
        }

        for (var i = before + 1; i < lesson.Steps.Count; i++)
        {
            if (lesson.Steps[i].Phrase is { } phrase && data.Phrases.Find(phrase) is { } row && NumberIn(row.Spanish, noun) is { } number)
            {
                return number;
            }
        }

        return 1;
    }

    // The number that goes with the item in a phrase's Spanish (V30): the item as a whole word, its singular or its plural, and the number written just before it ("tres manzanas" in
    // "Compré tres manzanas y dos panes" is 3 for the apple, "dos panes" is 2 for the bread); null when the phrase does not name the item with a number.
    internal static int? NumberIn(string spanish, NounEntry noun)
    {
        var tokens = spanish.ToLowerInvariant().Split([' ', ',', '.', '¿', '?', '¡', '!'], StringSplitOptions.RemoveEmptyEntries);
        for (var t = 0; t < tokens.Length; t++)
        {
            if (!string.Equals(tokens[t], noun.Lemma, StringComparison.OrdinalIgnoreCase) && !string.Equals(tokens[t], noun.Plural, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (var start = Math.Max(0, t - 4); start < t; start++) // the longest run first: "treinta y dos" is 32, not 2
            {
                if (SpanishNumbers.TryParsePrefix(tokens, start, out var number) == t - start && number > 0)
                {
                    return number;
                }
            }
        }

        return null;
    }
}
