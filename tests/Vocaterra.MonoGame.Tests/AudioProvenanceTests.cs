using System.Text.RegularExpressions;
using Vocaterra.MonoGame.Sound;
using Vocaterra.MonoGame.Utilities;

namespace Vocaterra.MonoGame.Tests;

/// <summary>
/// The audio assets of S24 (D-game-5, D-game-18): every file is CC0 and listed in <c>assets/audio/SOURCES.md</c> with its source and edit, the catalogue and the folder
/// agree, the files are the format the engine plays, the size stays in the budget, and the synthesized cues are the same every time they are made.
/// </summary>
public sealed class AudioProvenanceTests
{
    /// <summary>The audio budget of the playtest build for S24 and S25 together (D-game-18's plan: at most 15 MB).</summary>
    private const long BudgetBytes = 15L * 1024 * 1024;

    private static string AudioFolder => Repo.Path("assets", "audio");

    private static IReadOnlyList<string> Files() => Directory.EnumerateFiles(AudioFolder, "*", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileName).OfType<string>().Where(static n => !string.Equals(n, "SOURCES.md", StringComparison.Ordinal)).OrderBy(static n => n, StringComparer.Ordinal).ToList();

    private sealed record Row(string File, string Source, string Pack, string Author, string Licence, string Edit);

    private static IReadOnlyList<Row> Rows()
    {
        var rows = new List<Row>();
        foreach (var line in File.ReadAllLines(Path.Combine(AudioFolder, "SOURCES.md")))
        {
            var match = Regex.Match(line, "^\\| `([^`]+)` \\|(.*)$");
            if (!match.Success)
            {
                continue;
            }

            var cells = match.Groups[2].Value.Split('|', StringSplitOptions.TrimEntries).Where(static c => c.Length > 0).ToArray();
            Assert.True(cells.Length >= 6, $"{match.Groups[1].Value}: the row needs a source, pack, author, licence, edit and date.");
            rows.Add(new Row(match.Groups[1].Value, cells[0], cells[1], cells[2], cells[3], cells[4]));
        }

        return rows;
    }

    [Fact]
    public void EveryAudioFileIsListedWithItsSourceAndEveryListedFileExists()
    {
        var files = Files();
        var rows = Rows();

        Assert.NotEmpty(files);
        var unlisted = files.Except(rows.Select(static r => r.File), StringComparer.Ordinal).ToList();
        Assert.True(unlisted.Count == 0, $"Audio files not listed in assets/audio/SOURCES.md: {string.Join(", ", unlisted)}");
        var missing = rows.Select(static r => r.File).Except(files, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, $"Rows in assets/audio/SOURCES.md for files that are not there: {string.Join(", ", missing)}");
        Assert.Equal(rows.Count, rows.Select(static r => r.File).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryRowIsCc0WithAnHttpsSourceAPackAnAuthorAndAnEdit()
    {
        foreach (var row in Rows())
        {
            Assert.StartsWith("CC0", row.Licence, StringComparison.Ordinal);
            Assert.Contains("https://", row.Source, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(row.Pack), row.File);
            Assert.False(string.IsNullOrWhiteSpace(row.Author), row.File);
            Assert.False(string.IsNullOrWhiteSpace(row.Edit), row.File);
            Assert.EndsWith(".wav", row.File, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheFilesAre16BitPcmMonoWavAt22Or44KilohertzAndShort()
    {
        foreach (var name in Files())
        {
            var wav = WavFile.Parse(File.ReadAllBytes(Path.Combine(AudioFolder, name)));

            Assert.Equal(16, wav.BitsPerSample);
            Assert.Equal(1, wav.Channels);
            Assert.Contains(wav.SampleRate, new[] { 22050, 44100 });
            Assert.InRange(wav.Seconds, 0.01, 3.0);
        }
    }

    [Fact]
    public void TheAudioStaysWithinTheBudgetOfFifteenMegabytesForS24AndS25Together()
    {
        var total = Files().Sum(static n => new FileInfo(Path.Combine(AudioFolder, n)).Length);

        Assert.True(total <= BudgetBytes, $"The audio is {total / 1024.0 / 1024:0.0} MB; the budget is 15 MB for S24 and S25 together.");
        Assert.True(total <= 8L * 1024 * 1024, "S24 aims at about 8 MB so that S25's beds have room."); // S24 alone
    }

    [Fact]
    public void TheCatalogueAndTheFolderAgreeAndEverySynthesizedCueHasNoFile()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cue in SoundCues.All)
        {
            var definition = SoundCatalog.Of(cue);
            Assert.InRange(definition.Gain, 0.05, 1.0);
            Assert.InRange(definition.PitchJitter, 0.0, 0.1);
            Assert.Equal(SoundSynth.IsSynthesized(cue), definition.Files.Count == 0);
            foreach (var file in definition.Files)
            {
                Assert.True(File.Exists(Path.Combine(AudioFolder, file)), $"{cue}: {file} is not in assets/audio.");
                _ = used.Add(file);
            }
        }

        Assert.Equal(SoundCues.All.Count, SoundCatalog.All.Count);
        var unused = Files().Except(used, StringComparer.Ordinal).ToList();
        Assert.True(unused.Count == 0, $"Audio files no cue plays (delete them, the build ships every file): {string.Join(", ", unused)}");
    }

    [Fact]
    public void TheSynthesizedCuesAreTheSameEveryTimeTheyAreMadeAndStayInRange()
    {
        foreach (var cue in SoundCues.All.Where(SoundSynth.IsSynthesized))
        {
            var first = SoundSynth.Render(cue);
            var second = SoundSynth.Render(cue);

            Assert.Equal(first, second);
            Assert.InRange(first.Length / (double)SoundSynth.SampleRate, 0.05, 3.0);
            var peak = first.Max(static s => Math.Abs((int)s));
            Assert.InRange(peak, 0.7 * short.MaxValue, 0.85 * short.MaxValue);
            Assert.Equal(0, first[0]); // no click at the start
            Assert.Equal(0, first[^1]); // and none at the end
        }

        Assert.NotEqual(SoundSynth.Render(SoundCue.PushToTalkStart), SoundSynth.Render(SoundCue.PushToTalkStop));
        Assert.Equal(SoundSynth.StartBeepSeconds, SoundSynth.Render(SoundCue.PushToTalkStart).Length / (double)SoundSynth.SampleRate, 2);
        Assert.Throws<ArgumentException>(() => SoundSynth.Render(SoundCue.PlaceBlock));
    }

    [Fact]
    public void TheProjectCopiesTheAudioFolderNextToTheExecutable()
    {
        var project = File.ReadAllText(Repo.Path("src", "Vocaterra.MonoGame", "Vocaterra.MonoGame.csproj"));

        Assert.Contains("assets/audio/*.wav", project, StringComparison.Ordinal);
        Assert.Contains(AssetPaths.AudioRelative, project, StringComparison.Ordinal);
    }
}
