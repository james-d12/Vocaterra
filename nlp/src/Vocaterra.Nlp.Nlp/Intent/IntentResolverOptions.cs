using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Nlp.Intent;

using Vocaterra.Nlp.Nlp.Normalization;

/// <summary>Thresholds and settings of <see cref="IntentResolver"/>; the defaults are the values from the plan.</summary>
public sealed record IntentResolverOptions
{
    /// <summary>The default options.</summary>
    public static IntentResolverOptions Default { get; } = new();

    /// <summary>The embedding rung needs the best example score to be accepted by this margin (default threshold 0.80, epsilon 0.02).</summary>
    public Margin EmbeddingTop1 { get; init; } = new(0.80, 0.02);

    /// <summary>The embedding rung needs the gap between the best two intents to be accepted by this margin (default 0.05, epsilon 0.01).</summary>
    public Margin EmbeddingGap { get; init; } = new(0.05, 0.01);

    /// <summary>The confidence reported when a keyword rule decides the intent.</summary>
    public double RulesConfidence { get; init; } = 0.9;

    /// <summary>How example utterances are normalized before they are embedded; indefinite articles stay articles by default.</summary>
    public NormalizationOptions ExampleNormalization { get; init; } = new(ConvertIndefiniteArticles: false);
}
