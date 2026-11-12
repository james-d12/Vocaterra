using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>What the HUD shows of the needs (N05, adaptation design 9.9), as plain values: no drawing, no window.</summary>
public sealed class NeedsHudTests
{
    private static (VoxelSession Session, NeedsHud Hud) Fresh()
    {
        var session = TestGame.NewSession();
        var hud = new NeedsHud(session);
        hud.Update(0);
        _ = hud.TakeSounds();
        return (session, hud);
    }

    [Fact]
    public void WithHealthyDrivesAndFullStaminaOnlyTheHealthBarShows()
    {
        var (_, hud) = Fresh();
        Assert.True(hud.Visible);
        Assert.Equal(1.0, hud.Health);
        Assert.Empty(hud.Icons);
        Assert.False(hud.StaminaVisible);
        Assert.Null(hud.Announcement);
        Assert.Equal(default, hud.Effects(500));
    }

    [Theory]
    [InlineData(NeedMeter.Hunger, Drive.Hunger)]
    [InlineData(NeedMeter.Thirst, Drive.Thirst)]
    [InlineData(NeedMeter.Sleep, Drive.Sleep)]
    public void ADriveShowsAnIconWithOneNotchPerStageAndNothingAtOrAbove75(NeedMeter meter, Drive drive)
    {
        var (session, hud) = Fresh();
        foreach (var (value, notches) in new[] { (74.0, 1), (49.0, 2), (24.0, 3), (0.0, 4) })
        {
            session.SetNeed(meter, value);
            var icon = Assert.Single(hud.Icons);
            Assert.Equal(new DriveIcon(drive, notches), icon);
        }

        session.SetNeed(meter, 75);
        Assert.Empty(hud.Icons); // a recovery to exactly 75 takes the icon away
    }

    [Fact]
    public void TheIconsKeepTheOrderHungerThirstSleep()
    {
        var (session, hud) = Fresh();
        session.SetNeed(NeedMeter.Sleep, 60);
        session.SetNeed(NeedMeter.Hunger, 10);
        Assert.Equal([Drive.Hunger, Drive.Sleep], hud.Icons.Select(static i => i.Drive));
    }

    [Fact]
    public void StaminaShowsOnlyBelowItsMaximumAndTheDrivesCutTheMaximum()
    {
        var (session, hud) = Fresh();
        session.SetNeed(NeedMeter.Stamina, 60);
        Assert.True(hud.StaminaVisible);
        Assert.Equal(0.6, hud.Stamina, 6);
        Assert.Equal(1.0, hud.StaminaMaximum, 6);

        session.SetNeed(NeedMeter.Stamina, 100);
        Assert.False(hud.StaminaVisible);

        session.SetNeed(NeedMeter.Thirst, 10); // stage 3: the maximum is cut by 35
        Assert.Equal(0.65, hud.StaminaMaximum, 6);
        session.SetNeed(NeedMeter.Stamina, 65);
        Assert.False(hud.StaminaVisible); // at the cut maximum it is at its maximum
        session.SetNeed(NeedMeter.Stamina, 40);
        Assert.True(hud.StaminaVisible);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(50, 0)]
    [InlineData(49, 1)]
    [InlineData(25, 1)]
    [InlineData(24, 2)]
    [InlineData(0.5, 2)]
    public void HealthStagesFollowTheWarningsBelow50And25(double health, int stage)
    {
        var (session, hud) = Fresh();
        session.SetNeed(NeedMeter.Health, health);
        Assert.Equal(stage, hud.HealthStage);
        Assert.Equal(health / 100, hud.Health, 6);
        Assert.Equal(stage >= 1, hud.HeartPulse(15) > 0 || hud.HeartPulse(0) > 0);
    }

    [Fact]
    public void AStageChangeShowsItsLineOnceWithItsEnglishAndAsksForItsSoundOnce()
    {
        var (session, hud) = Fresh();
        session.Needs.Set(NeedMeter.Thirst, 24.9); // a hair under 25: stage 3 comes on
        hud.Update(100);
        var line = hud.Announcement;
        Assert.NotNull(line);
        Assert.Equal("Tengo mucha sed.", line.Spanish);
        Assert.Equal("I'm very thirsty.", line.English);
        Assert.Equal(1, hud.TakeSounds());

        hud.Update(101);
        hud.Update(102);
        Assert.Equal(0, hud.TakeSounds()); // once
        Assert.Same(line, hud.Announcement);

        hud.Update(100 + NeedsHud.DisplayTicks);
        Assert.Null(hud.Announcement); // and it goes
        Assert.Equal(0, hud.TakeSounds());
    }

    [Fact]
    public void ARecoveryHidesTheIconAndShowsNoLine()
    {
        var (session, hud) = Fresh();
        session.SetNeed(NeedMeter.Hunger, 40);
        hud.Update(10);
        Assert.Equal(1, hud.TakeSounds());
        hud.Update(10 + NeedsHud.DisplayTicks);
        Assert.Null(hud.Announcement);

        session.SetNeed(NeedMeter.Hunger, 100);
        hud.Update(20 + NeedsHud.DisplayTicks);
        Assert.Empty(hud.Icons);
        Assert.Null(hud.Announcement);
        Assert.Equal(0, hud.TakeSounds());
    }

    [Fact]
    public void SeveralWorseningsAtOnceAreShownOneAfterTheOther()
    {
        var (session, hud) = Fresh();
        session.SetNeed(NeedMeter.Hunger, 60);
        session.SetNeed(NeedMeter.Thirst, 60);
        hud.Update(10);
        Assert.Equal("Tengo un poco de hambre.", hud.Announcement!.Spanish);
        Assert.Equal(1, hud.TakeSounds());

        hud.Update(10 + NeedsHud.DisplayTicks);
        Assert.Equal("Tengo un poco de sed.", hud.Announcement!.Spanish);
        Assert.Equal(1, hud.TakeSounds());
    }

    [Fact]
    public void AStageTheFirstLookFindsIsToldOnceAndWithTheNeedsOffNothingIs()
    {
        var session = TestGame.NewSession();
        session.SetNeed(NeedMeter.Thirst, 20);
        var hud = new NeedsHud(session);
        hud.Update(5);
        Assert.Equal("Tengo mucha sed.", hud.Announcement!.Spanish);

        var off = TestGame.NewSession();
        off.NeedsEnabled = false;
        off.SetNeed(NeedMeter.Thirst, 20);
        var quiet = new NeedsHud(off);
        quiet.Update(5);
        Assert.False(quiet.Visible);
        Assert.Null(quiet.Announcement);
        Assert.Equal(0, quiet.TakeSounds());
        Assert.Equal(default, quiet.Effects(5));
    }

    [Fact]
    public void EveryStageLineHasItsSpanishAndEnglish()
    {
        var data = TestGame.Data;
        foreach (var key in GameDataValidator.HudLineKeys(data))
        {
            Assert.NotNull(data.Words.HudLine(key));
            Assert.True(data.Locale.Has(key), key);
        }

        Assert.Equal("Tengo un poco de hambre.", data.Words.HudLine("need.hunger.1"));
        Assert.Equal("Me muero de sueño.", data.Words.HudLine("need.sleep.4"));
        Assert.Equal("Estoy muy débil.", data.Words.HudLine("need.health.2"));
        Assert.Equal("Estás de vuelta.", data.Words.HudLine("hud.camp"));
        Assert.Equal("tus cosas", data.Words.HudLine("hud.belongings"));
    }

    [Fact]
    public void TheValidatorRefusesAStageLineWithNoSpanish()
    {
        var data = TestGame.Data;
        var kept = data.Words.HudLines.Where(static pair => pair.Key != "need.thirst.3").ToDictionary(static pair => pair.Key, static pair => pair.Value);
        var broken = data.WithWords(GameWordsFor(data, kept));
        var problems = GameDataValidator.Validate(broken);
        Assert.Contains(problems, static p => p.Contains("no Spanish for 'need.thirst.3'", StringComparison.Ordinal));
        Assert.DoesNotContain(GameDataValidator.Validate(data), static p => p.Contains("hudLines", StringComparison.Ordinal));
    }

    private static Vocaterra.Game.Core.Language.Words.GameWords GameWordsFor(GameData data, Dictionary<string, string> lines)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Repo.Root, "content", "game-v1", "game", "words.json")))!.AsObject();
        var hud = new System.Text.Json.Nodes.JsonObject();
        foreach (var (key, value) in lines)
        {
            hud[key] = value;
        }

        json["hudLines"] = hud;
        _ = data;
        return Vocaterra.Game.Core.Language.Words.GameWords.Parse(json.ToJsonString());
    }

    [Fact]
    public void TheNeedsListHasTheFiveNeedsWithTheirStageInSpanishAndEnglish()
    {
        var (session, hud) = Fresh();
        session.SetNeed(NeedMeter.Thirst, 30);
        session.SetNeed(NeedMeter.Health, 40);
        var rows = hud.ListRows();
        Assert.Equal([NeedMeter.Hunger, NeedMeter.Thirst, NeedMeter.Sleep, NeedMeter.Health, NeedMeter.Stamina], rows.Select(static r => r.Meter));
        Assert.Equal(("hambre", "bien", "fine"), (rows[0].Name, rows[0].Spanish, rows[0].English));
        Assert.Equal(("sed", "Tengo sed.", "I'm thirsty."), (rows[1].Name, rows[1].Spanish, rows[1].English));
        Assert.Equal(("salud", "Estoy débil.", "I'm weak."), (rows[3].Name, rows[3].Spanish, rows[3].English));
        Assert.Equal("strength", rows[4].NameEnglish);
    }

    [Fact]
    public void TheViewEffectsComeOnAtTheirStagesAndTheOptionTurnsThemOffButNotTheIconsOrLines()
    {
        var (session, hud) = Fresh();
        session.SetNeed(NeedMeter.Thirst, 30);
        Assert.Equal(0, hud.Effects(0).DimEdges); // stage 2: none
        session.SetNeed(NeedMeter.Thirst, 20);
        Assert.True(hud.Effects(0).DimEdges > 0);
        session.SetNeed(NeedMeter.Sleep, 20);
        session.SetNeed(NeedMeter.Health, 20);
        Assert.True(hud.Effects(0).RedEdges > 0);
        Assert.True(hud.Effects(NeedsData.TicksPerSecond / 2).Blink > 0); // inside the first blink

        hud.Update(10);
        var icons = hud.Icons.Count;
        hud.EffectsOn = false;
        Assert.Equal(default, hud.Effects(NeedsData.TicksPerSecond / 2));
        Assert.Equal(icons, hud.Icons.Count);
        Assert.NotNull(hud.Announcement);
    }

    [Fact]
    public void TheViewEffectsAreGentleNeverBrighterThanTheWorldAndNeverFasterThanAboutThreeHertz()
    {
        var session = TestGame.NewSession();
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Sleep, 0);
        session.SetNeed(NeedMeter.Health, 5);
        var hud = new NeedsHud(session);
        var previous = hud.Effects(0);
        var steepest = 0.0;
        var blinked = false;
        for (long tick = 1; tick < 60L * NeedsData.TicksPerSecond; tick++)
        {
            var now = hud.Effects(tick);
            Assert.InRange(now.DimEdges, 0, ViewEffectLevels.MaxAlpha);
            Assert.InRange(now.Blink, 0, ViewEffectLevels.MaxAlpha);
            Assert.InRange(now.RedEdges, 0, ViewEffectLevels.MaxAlpha);
            steepest = Math.Max(steepest, new[] { Math.Abs(now.DimEdges - previous.DimEdges), Math.Abs(now.Blink - previous.Blink), Math.Abs(now.RedEdges - previous.RedEdges) }.Max() * NeedsData.TicksPerSecond);
            blinked |= now.Blink > 0;
            previous = now;
        }

        Assert.True(blinked);
        Assert.True(steepest <= ViewEffectLevels.MaxChangePerSecond, $"steepest change {steepest} per second");
    }

    [Fact]
    public void TheNeedStageCueIsASoftSoundEffectThatDucksUnderAVoice()
    {
        Assert.Equal(SoundCategory.Sfx, SoundCues.CategoryOf(SoundCue.NeedStage));
        Assert.Equal("need_stage", SoundCues.NameOf(SoundCue.NeedStage));
        Assert.True(SoundSynth.IsSynthesized(SoundCue.NeedStage));
        var samples = SoundSynth.Render(SoundCue.NeedStage);
        Assert.NotEmpty(samples);
        Assert.Equal(samples, SoundSynth.Render(SoundCue.NeedStage)); // the same bytes every time
    }
}
