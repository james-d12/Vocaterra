using System.Collections.Immutable;
using Slg.Content;
using Slg.Core.Stages;
using Slg.Core.Verdicts;

namespace Slg.Pipeline;

/// <summary>The models and content an <see cref="UtterancePipeline"/> runs on, injected through the constructor.</summary>
public sealed class PipelineServices
{
    /// <summary>The key of the word recognizer in <see cref="VersionStamp.ModelHashes"/>.</summary>
    public const string AsrKey = "asr";

    /// <summary>The key of the phoneme recognizer in <see cref="VersionStamp.ModelHashes"/>.</summary>
    public const string PhonemeKey = "phoneme";

    /// <summary>The key of the embedding model in <see cref="VersionStamp.ModelHashes"/>.</summary>
    public const string EmbeddingKey = "embedding";

    /// <summary>The key of the speech synthesizer in <see cref="VersionStamp.ModelHashes"/>.</summary>
    public const string TtsKey = "tts";

    /// <summary>Creates the services; the optional models may be null, in which case their stage is skipped.</summary>
    public PipelineServices(
        IWordRecognizer wordRecognizer,
        IPhonemeRecognizer? phonemeRecognizer,
        IEmbeddingModel? embeddings,
        ISpeechSynthesizer? synthesizer,
        ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(wordRecognizer);
        ArgumentNullException.ThrowIfNull(content);
        WordRecognizer = wordRecognizer;
        PhonemeRecognizer = phonemeRecognizer;
        Embeddings = embeddings;
        Synthesizer = synthesizer;
        Content = content;
        Versions = BuildVersions();
    }

    /// <summary>The word recognizer (ASR).</summary>
    public IWordRecognizer WordRecognizer { get; }

    /// <summary>The phoneme recognizer, or null when pronunciation scoring is off.</summary>
    public IPhonemeRecognizer? PhonemeRecognizer { get; }

    /// <summary>The embedding model for the third intent rung, or null to skip that rung.</summary>
    public IEmbeddingModel? Embeddings { get; }

    /// <summary>The speech synthesizer used by <see cref="UtterancePipeline.Synthesize"/>, or null.</summary>
    public ISpeechSynthesizer? Synthesizer { get; }

    /// <summary>The loaded content bundle.</summary>
    public ContentBundle Content { get; }

    /// <summary>The content hash plus the model hash of every provided model; copied into every verdict.</summary>
    public VersionStamp Versions { get; }

    /// <summary>Returns the same services with the recognizers replaced; used to replay recorded recognition.</summary>
    public PipelineServices WithRecognizers(IWordRecognizer wordRecognizer, IPhonemeRecognizer? phonemeRecognizer)
    {
        return new PipelineServices(wordRecognizer, phonemeRecognizer, Embeddings, Synthesizer, Content);
    }

    private VersionStamp BuildVersions()
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        builder[AsrKey] = WordRecognizer.ModelHash;
        if (PhonemeRecognizer is not null)
        {
            builder[PhonemeKey] = PhonemeRecognizer.ModelHash;
        }

        if (Embeddings is not null)
        {
            builder[EmbeddingKey] = Embeddings.ModelHash;
        }

        if (Synthesizer is not null)
        {
            builder[TtsKey] = Synthesizer.ModelHash;
        }

        return new VersionStamp(Content.ContentHash, builder.ToImmutable());
    }
}
