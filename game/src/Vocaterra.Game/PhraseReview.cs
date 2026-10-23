using Vocaterra.Nlp.Content;

namespace Vocaterra.Game;

/// <summary>
/// The guide's review of a due phrase in context (S8): she asks it with a request line instead of the pointing question of a word review, and the
/// player's answer is graded as an unaided use unless the card was shown again with Ayuda. The answer is evaluated on <see cref="Prompt"/>, the
/// prompt the phrase was taught at, so the core recognizes its intent.
/// </summary>
/// <param name="PhraseId">The phrase asked for (<c>pedir-agua</c>).</param>
/// <param name="Prompt">The prompt the answer is evaluated on (<c>stall-need</c>).</param>
/// <param name="Request">What the guide says: the bundle's line <see cref="PhraseReviews.PoolOf"/> when it has one, otherwise a line of an existing pool (<see cref="FromBundle"/> is then false).</param>
/// <param name="FromBundle">Whether the request is the bundle's dedicated review line rather than the placeholder.</param>
public sealed record PhraseReview(string PhraseId, string Prompt, string Request, bool FromBundle);

/// <summary>
/// How the guide asks a due phrase (S8). The bundle's pool <see cref="PoolOf"/> holds the request line when the content has it (for example
/// <c>¿Qué necesitas?</c>); until then the placeholder is a line the bundle already has that makes the same situation: the stall's own question for
/// <c>pedir-agua</c> (the guide asks what the player needs, so the transfer is to a different speaker), the guide's offer for <c>sí, por favor</c>
/// and <c>no, gracias</c>, and the villager's greeting question for the river question.
/// </summary>
public static class PhraseReviews
{
    private static readonly Dictionary<string, string> Placeholders = new(StringComparer.Ordinal)
    {
        [PhraseIds.RequestWater] = "shop_need.1",
        [PhraseIds.YesPlease] = "guide_offer.1",
        [PhraseIds.NoThanks] = "guide_offer.1",
        [PhraseIds.WhereIsTheRiver] = "villager_greet.1",
        [PhraseIds.Hello] = "guide.lesson.hello.1", // S17: the lesson's phrases (Rosa says "¡Hola!", offers fruit) are asked again with the lines of the lesson
        [PhraseIds.Yes] = "guide.lesson.offer.1",
        [PhraseIds.No] = "guide.lesson.offer.1",
        [PhraseIds.Thanks] = "guide.lesson.have.1", // X06: the guide hands over the fruit ("¡Muy bien! Tienes una fruta."), as in the lesson, and the learner thanks her
        ["necesito-agua"] = "guide.lesson.need.1", // the lesson's own question ("¿Qué necesitas?"), asked again
    };

    /// <summary>The bundle pool that holds the request line of a phrase's review: <c>guide.review.phrase.pedir_agua</c> (hyphens become underscores).</summary>
    /// <param name="phraseId">The phrase sheet's id.</param>
    public static string PoolOf(string phraseId)
    {
        ArgumentNullException.ThrowIfNull(phraseId);
        return "guide.review.phrase." + phraseId.Replace('-', '_');
    }

    /// <summary>The review of a phrase, or <see langword="null"/> when it cannot be asked (a phrase with no scaffold, or no request line anywhere).</summary>
    /// <param name="phraseId">The phrase sheet's id.</param>
    /// <param name="sheet">The phrase sheet.</param>
    /// <param name="bundle">The content bundle.</param>
    public static PhraseReview? Create(string phraseId, PhraseSheet sheet, ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(phraseId);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(bundle);
        if (sheet.Find(phraseId) is not { IsLearnerPhrase: true, Prompt: { } prompt })
        {
            return null;
        }

        if (bundle.Dialogue.TryGetPoolById(PoolOf(phraseId), out var pool) && pool.Variants.Count > 0)
        {
            return new PhraseReview(phraseId, prompt, pool.Variants[0].Text, true);
        }

        if (Placeholders.TryGetValue(phraseId, out var lineId) && TextOf(bundle, lineId) is { } text)
        {
            return new PhraseReview(phraseId, prompt, text, false);
        }

        // X06: a phrase of a lesson is asked with the line its lesson asks it with, which the phrase sheet names as the audio of the row the guide says for the same prompt
        // (for example "quiero madera" with the guide's "¿Qué quieres?"). Only a line of the guide: the review is hers. No new Spanish.
        foreach (var heard in sheet.Rows.Where(row => string.Equals(row.Says, "hears", StringComparison.Ordinal) && string.Equals(row.Prompt, prompt, StringComparison.Ordinal)))
        {
            foreach (var audio in heard.AudioLineIds)
            {
                if (FindLine(bundle, audio) is { } found && string.Equals(SpeechSpeakers.OfPool(found.PoolId), SpeechSpeakers.Guide, StringComparison.Ordinal))
                {
                    return new PhraseReview(phraseId, prompt, found.Text, false);
                }
            }
        }

        return null;
    }

    private static (string PoolId, string Text)? FindLine(ContentBundle bundle, string lineId)
    {
        foreach (var pool in bundle.Dialogue.Pools.Values)
        {
            foreach (var variant in pool.Variants)
            {
                if (string.Equals(variant.LineId, lineId, StringComparison.Ordinal))
                {
                    return (pool.PoolId, variant.Text);
                }
            }
        }

        return null;
    }

    private static string? TextOf(ContentBundle bundle, string lineId)
    {
        foreach (var pool in bundle.Dialogue.Pools.Values)
        {
            foreach (var variant in pool.Variants)
            {
                if (string.Equals(variant.LineId, lineId, StringComparison.Ordinal))
                {
                    return variant.Text;
                }
            }
        }

        return null;
    }
}
