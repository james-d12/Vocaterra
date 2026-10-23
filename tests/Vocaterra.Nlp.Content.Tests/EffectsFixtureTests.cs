using Vocaterra.Nlp.Response.Effects;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// Effects and curves on a fixture. The repository's bundle (<c>content/game-v1</c>) has an empty effect table, because the game derives its effects from
/// its own host files, so the core's effect table, the curve interpolation and the loader's cross-checks keep their coverage here: a copy of the bundle gets a
/// small table written inside the test (no second bundle on disk). The effect kinds are opaque to the core; these are made up.
/// </summary>
public class EffectsFixtureTests
{
    private const string Table =
        """
        {
          "effects": [
            {
              "effectId": "fx_price",
              "intent": "request_item",
              "kind": "price_factor",
              "curve": [ { "correctness": 0, "value": 1.3 }, { "correctness": 1, "value": 1 } ],
              "requiresCommunicated": true
            },
            {
              "effectId": "fx_power",
              "intent": "state_action",
              "kind": "power",
              "curve": [ { "correctness": 0, "value": 0 }, { "correctness": 0.5, "value": 0.5 }, { "correctness": 1, "value": 1 } ],
              "requiresCommunicated": true
            },
            {
              "effectId": "fx_always",
              "intent": "greet",
              "kind": "mood",
              "curve": [ { "correctness": 0, "value": 2 }, { "correctness": 1, "value": 2 } ],
              "requiresCommunicated": false
            }
          ]
        }
        """;

    private static ContentBundle BundleWithEffects()
    {
        using var copy = new TempBundle();
        copy.Write("effects/effects.json", Table);
        return copy.Load();
    }

    [Fact]
    public void TheBundleItselfCarriesNoEffectsAndTheFixtureLoadsTheTableSorted()
    {
        Assert.Empty(TestSupport.Bundle.Effects.Effects);

        var bundle = BundleWithEffects();

        Assert.Equal(["fx_always", "fx_power", "fx_price"], bundle.Effects.Effects.Select(effect => effect.EffectId));
        var power = bundle.Effects.Effects.Single(effect => effect.Intent == "state_action");
        Assert.Equal("power", power.Kind);
        Assert.Equal([new CurvePoint(0, 0), new CurvePoint(0.5, 0.5), new CurvePoint(1, 1)], power.Curve);
    }

    [Fact]
    public void ACurveIsInterpolatedBetweenItsPointsAndClampedAtItsEnds()
    {
        var bundle = BundleWithEffects();

        var power = EffectResolver.Resolve(TestSupport.MakeVerdict("state_action", correctness: 0.25), bundle.Effects);
        var price = EffectResolver.Resolve(TestSupport.MakeVerdict("request_item", correctness: 0.5), bundle.Effects);

        Assert.Equal(0.25, Assert.Single(power).Value, 12);
        Assert.Equal("power", Assert.Single(power).Kind);
        Assert.Equal(1.15, Assert.Single(price).Value, 12);
        Assert.Equal(1.3, Assert.Single(EffectResolver.Resolve(TestSupport.MakeVerdict("request_item", correctness: -0.5), bundle.Effects)).Value, 12);
        Assert.Equal(1.0, Assert.Single(EffectResolver.Resolve(TestSupport.MakeVerdict("request_item", correctness: 1.5), bundle.Effects)).Value, 12);
    }

    [Fact]
    public void AnEffectThatRequiresCommunicationIsDroppedForAVerdictThatDidNotCommunicate()
    {
        var bundle = BundleWithEffects();

        Assert.Empty(EffectResolver.Resolve(TestSupport.MakeVerdict("request_item", communicated: false, correctness: 1), bundle.Effects));
        Assert.Equal(2.0, Assert.Single(EffectResolver.Resolve(TestSupport.MakeVerdict("greet", communicated: false, correctness: 0), bundle.Effects)).Value, 12);
        Assert.Empty(EffectResolver.Resolve(TestSupport.MakeVerdict("farewell"), bundle.Effects));
    }

    [Fact]
    public void TheTableIsPartOfTheBundleHashAndRoundTrips()
    {
        using var copy = new TempBundle();
        var before = copy.Rehash().BundleHash;
        copy.Write("effects/effects.json", Table);

        var after = copy.Rehash().BundleHash;
        var bundle = copy.Load(verifyHashes: true);

        Assert.NotEqual(before, after);
        Assert.Equal(after, bundle.ContentHash);
        Assert.Equal(bundle.Effects, EffectTable.FromJson(bundle.Effects.ToJson()));
    }
}
