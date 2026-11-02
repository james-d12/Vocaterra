using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// The content validator's sample slots come from the language (M35). A toy language's validator must not fill a template with a Spanish word; the
/// Spanish samples are the ones the validator always used.
/// </summary>
public class SampleSlotsTests
{
    private static string Sample(DialogueTemplate template, SampleWords samples, string slot)
    {
        return SampleSlots.For(template, samples)[slot];
    }

    [Fact]
    public void TheSpanishSamplesAreTheValuesTheValidatorAlwaysUsed()
    {
        var samples = SpanishLanguagePack.Instance.Samples;
        var template = new DialogueTemplate("t.1", "{item} {verb} {qty} {spell} {tag} {expected} {actual}");

        Assert.Equal("espada", Sample(template, samples, "item"));
        Assert.Equal("tener", Sample(template, samples, "verb"));
        Assert.Equal("2", Sample(template, samples, "qty"));
        Assert.Equal("fuego", Sample(template, samples, "spell"));
        Assert.Equal("gender.agree", Sample(template, samples, "tag"));
        Assert.Equal("tengo", Sample(template, samples, "expected"));
        Assert.Equal("tiene", Sample(template, samples, "actual"));
    }

    [Fact]
    public void ASlotOfAnotherLanguageTakesItsOwnSamples()
    {
        var toy = new SampleWords("ka", "mo", "ri", "nu", "ko", "ka mo", "vocab.unknown");
        var template = new DialogueTemplate("t.1", "{item} {verb} {qty} {spell} {tag} {expected} {actual} {recast}");

        Assert.Equal("ka", Sample(template, toy, "item"));
        Assert.Equal("mo", Sample(template, toy, "verb"));
        Assert.Equal("2", Sample(template, toy, "qty"));
        Assert.Equal("ri", Sample(template, toy, "spell"));
        Assert.Equal("vocab.unknown", Sample(template, toy, "tag"));
        Assert.Equal("nu", Sample(template, toy, "expected"));
        Assert.Equal("ko", Sample(template, toy, "actual"));
        Assert.Equal("ka mo", Sample(template, toy, "recast"));
    }
}
