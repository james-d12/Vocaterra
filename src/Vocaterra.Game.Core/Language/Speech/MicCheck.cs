using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Session;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Game.Core.Language.Speech;

/// <summary>
/// The recognition half of the mic check (D-game-9, S7): the player says <c>hola</c> into the microphone and the game shows what it heard. The
/// clip is decoded twice by the recognizer, on the turn worker: once in Constrained mode over the one word, which tolerates a poor microphone
/// and an accent, and, when that does not hear it, once in Open mode so that the screen can still show what was heard («gola»). The check passes
/// when either hears «hola». It changes no conversation, learner or world state, and it never blocks the game: the player can always skip it.
/// </summary>
public static class MicCheck
{
    /// <summary>The word the player is asked to say.</summary>
    public const string Word = "hola";

    /// <summary>The milliseconds of one window of the level (the meter and the clip's level).</summary>
    public const int WindowMilliseconds = 50;

    /// <summary>
    /// Recognizes the clip and reports what was heard. A <see langword="null"/> clip (silence) is a failed check that heard nothing. Call it on
    /// the thread that owns the recognizer.
    /// </summary>
    /// <param name="recognizer">The word recognizer (Vosk), owned by the calling thread.</param>
    /// <param name="clip">The prepared clip, 16 kHz mono, or <see langword="null"/>.</param>
    public static MicCheckResult Run(IWordRecognizer recognizer, PcmClip? clip)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        if (clip is null)
        {
            return new MicCheckResult(string.Empty, false, 0);
        }

        var level = Level(clip);
        var narrow = Clean(recognizer.Recognize(clip, RecognitionRequest.Constrained([Word], 1)).Transcript);
        if (Says(narrow))
        {
            return new MicCheckResult(narrow, true, level);
        }

        var open = Clean(recognizer.Recognize(clip, RecognitionRequest.Open(1)).Transcript);
        return new MicCheckResult(open, Says(open), level);
    }

    /// <summary>The loudest 50 ms window of the clip as an RMS level from 0 to 1 (full scale is 1).</summary>
    /// <param name="clip">The clip.</param>
    public static double Level(PcmClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return Level(clip.Samples, clip.SampleRate);
    }

    /// <summary>The loudest 50 ms window of the samples as an RMS level from 0 to 1.</summary>
    /// <param name="samples">Mono 16-bit samples.</param>
    /// <param name="sampleRate">Their rate in hertz.</param>
    public static double Level(IReadOnlyList<short> samples, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var window = Math.Max(1, sampleRate * WindowMilliseconds / 1000);
        var loudest = 0.0;
        for (var start = 0; start < samples.Count; start += window)
        {
            var end = Math.Min(samples.Count, start + window);
            var sum = 0.0;
            for (var i = start; i < end; i++)
            {
                sum += (double)samples[i] * samples[i];
            }

            loudest = Math.Max(loudest, Math.Sqrt(sum / (end - start)) / short.MaxValue);
        }

        return Math.Min(1.0, loudest);
    }

    private static string Clean(string transcript) =>
        string.Join(' ', transcript.Replace(GrammarBuilder.UnknownToken, " ", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static bool Says(string heard) =>
        PhraseSheet.Plain(heard).Split(' ').Contains(Word, StringComparer.Ordinal);
}
