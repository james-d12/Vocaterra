namespace Vocaterra.Nlp.Response.Tests.Effects;

using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Effects;

public class EffectTests
{
    private const double Tolerance = 1e-12;

    private static EffectTable Table()
    {
        return new EffectTable(
        [
            new EffectDefinition("trade.price", "buy_item", "trade_price", [new CurvePoint(0, 1.3), new CurvePoint(1, 1.0)]),
            new EffectDefinition("spell.fireball", "cast_spell", "spell_power", [new CurvePoint(0, 0), new CurvePoint(1, 1)]),
            new EffectDefinition("spell.heal", "cast_spell", "spell_power", [new CurvePoint(0, 0), new CurvePoint(0.5, 0.2), new CurvePoint(0.8, 1), new CurvePoint(1, 1)]),
            new EffectDefinition("quest.greeted", "greet", "flag", [new CurvePoint(0, 0), new CurvePoint(0.7, 1)], RequiresCommunicated: false),
            new EffectDefinition("a.marker", "cast_spell", "none", [new CurvePoint(0, 0)]),
        ]);
    }

    private static Verdict MakeVerdict(string intent, double correctness, bool communicated = true)
    {
        return TestSupport.MakeVerdict(intent, communicated, correctness);
    }

    [Fact]
    public void TheOutcomeKindIsCarriedThroughAndTheValueFollowsTheLinearCurve()
    {
        var effects = EffectResolver.Resolve(MakeVerdict("cast_spell", 0.6), Table());
        var fireball = Assert.Single(effects, static e => e.EffectId == "spell.fireball");
        Assert.Equal("spell_power", fireball.Kind);
        Assert.Equal(0.6, fireball.Value, Tolerance);
    }

    [Fact]
    public void AFactorCurveInterpolatesDownward()
    {
        var effects = EffectResolver.Resolve(MakeVerdict("buy_item", 0.5), Table());
        var price = Assert.Single(effects);
        Assert.Equal("trade.price", price.EffectId);
        Assert.Equal(1.15, price.Value, 1e-9);
        Assert.Equal(1.3, Assert.Single(EffectResolver.Resolve(MakeVerdict("buy_item", 0.0), Table())).Value, Tolerance);
        Assert.Equal(1.0, Assert.Single(EffectResolver.Resolve(MakeVerdict("buy_item", 1.0), Table())).Value, Tolerance);
    }

    [Fact]
    public void MultiPointCurvesInterpolatePerSegment()
    {
        double Heal(double correctness)
        {
            return EffectResolver.Resolve(MakeVerdict("cast_spell", correctness), Table()).Single(static e => e.EffectId == "spell.heal").Value;
        }

        Assert.Equal(0.0, Heal(0.0), Tolerance);
        Assert.Equal(0.1, Heal(0.25), 1e-9);
        Assert.Equal(0.2, Heal(0.5), Tolerance);
        Assert.Equal(0.6, Heal(0.65), 1e-9);
        Assert.Equal(1.0, Heal(0.8), Tolerance);
        Assert.Equal(1.0, Heal(0.95), Tolerance);
    }

    [Fact]
    public void CorrectnessIsClampedToTheUnitRange()
    {
        double Power(double correctness)
        {
            return EffectResolver.Resolve(MakeVerdict("cast_spell", correctness), Table()).Single(static e => e.EffectId == "spell.fireball").Value;
        }

        Assert.Equal(0.0, Power(-3), Tolerance);
        Assert.Equal(1.0, Power(7), Tolerance);
        Assert.Equal(0.0, Power(double.NaN), Tolerance);
    }

    [Fact]
    public void EffectsRequiringCommunicationAreGated()
    {
        Assert.Empty(EffectResolver.Resolve(MakeVerdict("cast_spell", 0.9, communicated: false), Table()));
        Assert.Empty(EffectResolver.Resolve(MakeVerdict("buy_item", 0.9, communicated: false), Table()));
        var greet = Assert.Single(EffectResolver.Resolve(MakeVerdict("greet", 0.35, communicated: false), Table()));
        Assert.Equal("quest.greeted", greet.EffectId);
        Assert.Equal(0.5, greet.Value, Tolerance);
    }

    [Fact]
    public void EffectsComeOutInOrdinalEffectIdOrder()
    {
        var ids = EffectResolver.Resolve(MakeVerdict("cast_spell", 0.6), Table()).Select(static e => e.EffectId).ToList();
        Assert.Equal(["a.marker", "spell.fireball", "spell.heal"], ids);
        Assert.Equal(["a.marker", "quest.greeted", "spell.fireball", "spell.heal", "trade.price"], Table().Effects.Select(static e => e.EffectId));
    }

    [Fact]
    public void OnlyEffectsOfTheVerdictIntentAreReturned()
    {
        Assert.Empty(EffectResolver.Resolve(MakeVerdict("sell_item", 1.0), Table()));
        Assert.Empty(EffectResolver.Resolve(MakeVerdict("unknown", 1.0), Table()));
    }

    [Fact]
    public void ResolvingTwiceGivesIdenticalResults()
    {
        var verdict = MakeVerdict("cast_spell", 0.37);
        Assert.Equal(EffectResolver.Resolve(verdict, Table()), EffectResolver.Resolve(verdict, Table()));
    }

    [Fact]
    public void TableRoundTripsThroughCanonicalJson()
    {
        var table = Table();
        var json = table.ToJson();
        Assert.StartsWith("{\"effects\":[{\"effectId\":\"a.marker\",\"intent\":\"cast_spell\",\"kind\":\"none\",\"curve\":[{\"correctness\":0,\"value\":0}],\"requiresCommunicated\":true}", json, StringComparison.Ordinal);
        var copy = EffectTable.FromJson(json);
        Assert.Equal(table, copy);
        Assert.Equal(json, copy.ToJson());
        Assert.Equal(json, CanonicalJson.Serialize(table));
    }

    [Fact]
    public void TableJsonFromTheSpecificationShapeParses()
    {
        const string json = """
            { "effects": [
              { "effectId": "spell.fire", "intent": "cast_spell", "kind": "spell_power",
                "curve": [ { "correctness": 0, "value": 0 }, { "correctness": 1, "value": 1 } ], "requiresCommunicated": true },
              { "effectId": "barter", "intent": "buy_item", "kind": "trade_price",
                "curve": [ { "correctness": 0, "value": 1.3 }, { "correctness": 1, "value": 1.0 } ] }
            ] }
            """;
        var table = EffectTable.FromJson(json);
        Assert.Equal(["barter", "spell.fire"], table.Effects.Select(static e => e.EffectId));
        Assert.True(table.Effects[0].RequiresCommunicated);
    }

    [Theory]
    [InlineData("{\"effects\":[{\"effectId\":\"a\",\"intent\":\"i\",\"kind\":\"\",\"curve\":[{\"correctness\":0,\"value\":0}]}]}")]
    [InlineData("{\"effects\":[{\"effectId\":\"a\",\"intent\":\"i\",\"kind\":\"none\",\"curve\":[]}]}")]
    [InlineData("{\"effects\":[{\"effectId\":\"a\",\"intent\":\"i\",\"kind\":\"none\",\"curve\":[{\"correctness\":1,\"value\":0},{\"correctness\":0,\"value\":1}]}]}")]
    [InlineData("{\"effects\":[{\"effectId\":\"a\",\"intent\":\"i\",\"kind\":\"none\",\"curve\":[{\"correctness\":0,\"value\":0},{\"correctness\":0,\"value\":1}]}]}")]
    [InlineData("{\"effects\":[{\"effectId\":\"\",\"intent\":\"i\",\"kind\":\"none\",\"curve\":[{\"correctness\":0,\"value\":0}]}]}")]
    [InlineData("{\"effects\":[{\"effectId\":\"a\",\"intent\":\"\",\"kind\":\"none\",\"curve\":[{\"correctness\":0,\"value\":0}]}]}")]
    [InlineData("{\"effects\":[{\"effectId\":\"a\",\"intent\":\"i\",\"kind\":\"none\",\"curve\":[{\"correctness\":0,\"value\":0}]},{\"effectId\":\"a\",\"intent\":\"j\",\"kind\":\"none\",\"curve\":[{\"correctness\":0,\"value\":0}]}]}")]
    [InlineData("{\"effects\":[{\"effectId\":\"a\",\"intent\":\"i\",\"kind\":\"none\"}]}")]
    public void InvalidTablesAreRejected(string json)
    {
        Assert.ThrowsAny<Exception>(() => EffectTable.FromJson(json));
    }

    [Fact]
    public void AnyNonEmptyKindIsAnOpaqueOutcomeKind()
    {
        var table = EffectTable.FromJson("{\"effects\":[{\"effectId\":\"a\",\"intent\":\"i\",\"kind\":\"bogus\",\"curve\":[{\"correctness\":0,\"value\":0}]}]}");
        Assert.Equal("bogus", Assert.Single(table.Effects).Kind);
    }

    [Fact]
    public void InterpolateHandlesSinglePointAndExactKnots()
    {
        Assert.Equal(4.0, EffectResolver.Interpolate([new CurvePoint(0.3, 4)], 0.9), Tolerance);
        Assert.Equal(4.0, EffectResolver.Interpolate([new CurvePoint(0.3, 4)], 0.0), Tolerance);
        var curve = new[] { new CurvePoint(0, 0), new CurvePoint(0.5, 10), new CurvePoint(1, 0) };
        Assert.Equal(10.0, EffectResolver.Interpolate(curve, 0.5), Tolerance);
        Assert.Equal(5.0, EffectResolver.Interpolate(curve, 0.25), Tolerance);
        Assert.Equal(5.0, EffectResolver.Interpolate(curve, 0.75), Tolerance);
    }
}
