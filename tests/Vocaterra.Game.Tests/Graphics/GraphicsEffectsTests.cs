using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Graphics;

/// <summary>B17c: the graphics effects of the settings page (D-game-34 item 43): each is read from the settings, set by the quality level, a launch option and applied by the drawer to the renderer.</summary>
public sealed class GraphicsEffectsTests
{
    private static readonly (SettingId Id, string Flag, Func<GraphicsEffects, bool> Read)[] Effects =
    [
        (SettingId.SunFollowsHour, "--sun-follows-hour", static e => e.SunFollowsHour),
        (SettingId.ClimateColours, "--climate-colours", static e => e.ClimateColours),
        (SettingId.WaterShader, "--water-shader", static e => e.WaterShader),
        (SettingId.HeightFog, "--height-fog", static e => e.HeightFog),
        (SettingId.FarLight, "--far-light", static e => e.FarLight),
    ];

    [Fact]
    public void Given_ANewGame_When_TheEffectsAreRead_Then_AllAreOnAndEachRowSwitchesOnlyItsOwn()
    {
        var settings = new PlayerSettings();
        Assert.Equal(GraphicsEffects.All, GraphicsEffects.Of(settings));
        foreach (var (id, _, read) in Effects)
        {
            var one = new PlayerSettings();
            one.Set(id, 0);
            var effects = GraphicsEffects.Of(one);
            Assert.False(read(effects), $"{id} off");
            Assert.Equal(Effects.Length - 1, Effects.Count(e => e.Read(effects)));
        }

        Assert.Equal(GraphicsEffects.None, GraphicsEffects.Of(PlayerSettings.Resolve(null, new LaunchSettings(SunFollowsHour: false, ClimateColours: false, WaterShader: false, HeightFog: false, FarLight: false))));
    }

    [Fact]
    public void Given_TheQualityLevels_When_TheirEffectsAreRead_Then_LowTurnsTheWaterShaderAndTheHeightFogOffAndTheOthersAreAllOn()
    {
        Assert.Equal(GraphicsEffects.All, GraphicsEffects.Of(QualityPreset.For(QualityLevel.High)));
        Assert.Equal(GraphicsEffects.All, GraphicsEffects.Of(QualityPreset.For(QualityLevel.Medium)));
        var low = GraphicsEffects.Of(QualityPreset.For(QualityLevel.Low));
        Assert.False(low.WaterShader);
        Assert.False(low.HeightFog);
        Assert.True(low.SunFollowsHour && low.ClimateColours && low.FarLight);

        var settings = new PlayerSettings();
        low.WriteTo(settings);
        Assert.Equal(low, GraphicsEffects.Of(settings));
        GraphicsEffects.All.WriteTo(settings);
        Assert.Equal(GraphicsEffects.All, GraphicsEffects.Of(settings));
    }

    [Fact]
    public void Given_ALaunchFlagForAnEffect_When_ItIsParsed_Then_OnOffOrBareAreRead()
    {
        foreach (var (_, flag, _) in Effects)
        {
            Assert.Contains(flag + " [on|off]", LaunchOptions.Usage, StringComparison.Ordinal);
            Assert.True(LaunchOptions.TryParse([], out var none, out var error), error);
            Assert.Null(Value(none, flag));
            Assert.True(LaunchOptions.TryParse([flag], out var bare, out error), error);
            Assert.True(Value(bare, flag));
            Assert.True(LaunchOptions.TryParse([flag, "on"], out var on, out error), error);
            Assert.True(Value(on, flag));
            Assert.True(LaunchOptions.TryParse([flag, "off", "--quality", "low"], out var off, out error), error);
            Assert.False(Value(off, flag));
            Assert.Equal(QualityLevel.Low, off.Quality);
        }
    }

    private static bool? Value(LaunchOptions options, string flag) => flag switch
    {
        "--sun-follows-hour" => options.SunFollowsHour,
        "--climate-colours" => options.ClimateColours,
        "--water-shader" => options.WaterShader,
        "--height-fog" => options.HeightFog,
        _ => options.FarLight,
    };

    [Fact]
    public void Given_ARowChanged_When_TheGameApplyesIt_Then_TheDrawerHandsTheEffectsToTheRendererAndTheFarViewsColours()
    {
        // The wiring is in two places and says so in source: the game applies a changed row (and a level's set) to the drawer, the drawer to the renderer and the far view's host.
        var game = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "VoxelGame.cs"));
        Assert.Contains("case SettingId.SunFollowsHour or SettingId.ClimateColours or SettingId.WaterShader or SettingId.HeightFog or SettingId.FarLight:", game, StringComparison.Ordinal);
        Assert.Contains("_drawer.ApplyEffects(GraphicsEffects.Of(_settings));", game, StringComparison.Ordinal);
        Assert.Contains("GraphicsEffects.Of(_drawer.Preset).WriteTo(_settings);", game, StringComparison.Ordinal);
        var drawer = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "SceneDrawer.cs"));
        Assert.Contains("_renderer?.SetEffects(effects);", drawer, StringComparison.Ordinal);
        Assert.Contains("_streaming?.SetClimateColours(effects.ClimateColours, VoxelRenderer.ReleaseFarTile);", drawer, StringComparison.Ordinal);
        var renderer = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "World", "VoxelRenderer.cs"));
        Assert.Contains("_effects.WaterShader", renderer, StringComparison.Ordinal); // the uniform, and for the effects the baked tint
        Assert.Contains("_effects.HeightFog", renderer, StringComparison.Ordinal);
        Assert.Contains("_effects.FarLight", renderer, StringComparison.Ordinal);
        Assert.Contains("_effects.SunFollowsHour", renderer, StringComparison.Ordinal);
        Assert.Contains("_effects.ClimateColours ? _ground : null", renderer, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_EachEffectOffInASavedGame_When_TheSettingsAreResolved_Then_TheEffectsTheDrawerIsGivenAreTheSaved()
    {
        var saved = new SaveSettings(SoundVolumes.Default, SunFollowsHourOff: true, WaterShaderOff: true);
        var effects = GraphicsEffects.Of(PlayerSettings.Resolve(saved));
        Assert.False(effects.SunFollowsHour);
        Assert.False(effects.WaterShader);
        Assert.True(effects.ClimateColours && effects.HeightFog && effects.FarLight);
    }
}
