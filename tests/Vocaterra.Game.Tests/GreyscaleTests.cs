using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI;
using Vocaterra.Game.Utilities;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The greyscale A/B (D-game-6 as amended, <c>--greyscale</c>) without a GPU: the grey copies of the atlas, the mesher's choice of copy, the
/// chunks due when the grey set changes, the grey set from the progress and the new-word toast, and the launch option.
/// </summary>
public sealed class GreyscaleTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    // The grey copies sit in the rows below the colour cells: a vertex with a V at or above this is drawn grey.
    private const float GreyFrom = BlockAtlasLayout.ColourRows / (float)BlockAtlasLayout.Rows;

    private static ChunkMeshes Mesh(MapBlocks blocks, ulong greyKinds = 0) => ChunkMesher.Build(blocks, 0, 0, 0, greyKinds);

    private static bool AllVertices(MeshBuffer buffer, Func<MeshVertex, bool> test)
    {
        var vertices = buffer.Vertices;
        for (var i = 0; i < vertices.Length; i++)
        {
            if (!test(vertices[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsGreyVertex(MeshVertex vertex) => vertex.V >= GreyFrom;

    private static byte[] Crop(byte[] atlas, (int X, int Y, int Width, int Height) rectangle)
    {
        const int Size = BlockAtlasLayout.CellSize;
        var result = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            Array.Copy(atlas, (((rectangle.Y + y) * BlockAtlasLayout.Width) + rectangle.X) * 4, result, y * Size * 4, Size * 4);
        }

        return result;
    }

    [Fact]
    public void TheGreyCopyOfEveryCellIsTheLuminanceOfItsColourCellWithTheAlphaKept()
    {
        var atlas = BlockArt.AtlasPixels();
        foreach (var cell in Enum.GetValues<BlockCell>())
        {
            var colour = Crop(atlas, BlockAtlasLayout.PixelRectangle(cell));
            var grey = Crop(atlas, BlockAtlasLayout.GreyPixelRectangle(cell));
            Assert.Equal(BlockArt.Pixels(cell), colour);
            Assert.Equal(Greyscale.ToLuma(colour), grey);
            Assert.Equal(BlockArt.GreyPixels(cell), grey);
            for (var i = 0; i < grey.Length; i += 4)
            {
                Assert.Equal(grey[i], grey[i + 1]);
                Assert.Equal(grey[i], grey[i + 2]);
                Assert.Equal(colour[i + 3], grey[i + 3]);
            }
        }
    }

    [Fact]
    public void TheBooksNounIconIsGreyUntilTheWordIsKnownAndOnlyInGreyscale()
    {
        Assert.Equal(0.0, NounIcons.Saturation(greyscale: true, known: false)); // the grey copy
        Assert.Equal(1.0, NounIcons.Saturation(greyscale: true, known: true));
        Assert.Equal(1.0, NounIcons.Saturation(greyscale: false, known: false)); // arm A: every icon in colour
        Assert.Equal(1.0, NounIcons.Saturation(greyscale: false, known: true));

        // Every noun that has an item has an icon cell whose grey copy is a different picture from its colour cell (the berry, the torch, the bottle ...).
        var atlas = BlockArt.AtlasPixels();
        var coloured = 0;
        foreach (var noun in Nouns.Value.Nouns.Where(static n => n.Items.Count > 0))
        {
            var cell = BlockAtlasLayout.IconFor(noun.Items[0]);
            Assert.NotNull(cell);
            var colour = Crop(atlas, BlockAtlasLayout.PixelRectangle(cell.Value));
            var grey = Crop(atlas, BlockAtlasLayout.GreyPixelRectangle(cell.Value));
            if (!colour.SequenceEqual(grey))
            {
                coloured++;
            }
        }

        Assert.True(coloured >= 5, $"only {coloured} noun icons differ between colour and grey");
    }

    [Fact]
    public void EveryBlockFaceHasAGreyCopyBelowItsColourCellInTheSameColumn()
    {
        foreach (var definition in BlockDefinition.All.Where(static d => d.Id != BlockId.Air))
        {
            foreach (var face in Enum.GetValues<BlockFace>())
            {
                var cell = BlockAtlasLayout.CellFor(definition.Id, face);
                Assert.Equal(Greyscale.ToLuma(BlockArt.Pixels(cell)), BlockArt.GreyPixels(cell));
                var (u0, _, u1, v1) = BlockAtlasLayout.Uv(cell);
                var (g0, gv0, g1, _) = BlockAtlasLayout.Uv(cell, grey: true);
                Assert.True(v1 < GreyFrom, $"{definition.Id} {face}: the colour cell is not in the colour rows");
                Assert.True(gv0 > GreyFrom, $"{definition.Id} {face}: the grey copy is not in the grey rows");
                Assert.Equal(u0, g0);
                Assert.Equal(u1, g1);
            }
        }
    }

    [Fact]
    public void TheLumaUsesTheRec601WeightsAndRoundsToTheNearestLevel()
    {
        Assert.Equal((byte)76, Greyscale.Luma(255, 0, 0));
        Assert.Equal((byte)150, Greyscale.Luma(0, 255, 0));
        Assert.Equal((byte)29, Greyscale.Luma(0, 0, 255));
        Assert.Equal((byte)255, Greyscale.Luma(255, 255, 255));
        Assert.Throws<ArgumentException>(() => Greyscale.ToLuma([1, 2, 3]));
    }

    [Fact]
    public void EveryBlockKindFitsTheKindsMask()
    {
        Assert.All(Enum.GetValues<BlockId>(), id => Assert.True((int)id < 64, $"{id} does not fit a 64-bit mask"));
    }

    [Fact]
    public void TheMesherPicksTheGreyCopyOnlyForTheKindsInTheGreySet()
    {
        var grassOnly = new MapBlocks().Set(2, 2, 2, BlockId.Grass);
        var stoneOnly = new MapBlocks().Set(2, 2, 2, BlockId.Stone);
        var both = new MapBlocks().Set(2, 2, 2, BlockId.Grass).Set(9, 2, 9, BlockId.Stone);
        var greyGrass = ChunkMesher.KindBit(BlockId.Grass);

        Assert.True(AllVertices(Mesh(grassOnly, greyGrass).Opaque, IsGreyVertex));
        Assert.True(AllVertices(Mesh(stoneOnly, greyGrass).Opaque, v => !IsGreyVertex(v)));

        // The grass block lies at x = 2, the stone at x = 9: the faces of each are on their own side of x = 6.
        var mixed = Mesh(both, greyGrass).Opaque;
        Assert.True(mixed.FaceCount > 0);
        for (var i = 0; i < mixed.VertexCount; i++)
        {
            var vertex = mixed.Vertices[i];
            Assert.Equal(vertex.X < 6, IsGreyVertex(vertex));
        }
    }

    [Fact]
    public void TheTranslucentWaterIsGreyOnlyWhenItsKindIsInTheGreySet()
    {
        var water = new MapBlocks().Set(3, 3, 3, BlockId.Water);
        var alphaGrey = Mesh(water, ChunkMesher.KindBit(BlockId.Water)).Alpha;
        Assert.True(alphaGrey.FaceCount > 0);
        Assert.True(AllVertices(alphaGrey, IsGreyVertex));
        Assert.True(AllVertices(Mesh(water).Alpha, v => !IsGreyVertex(v)));
    }

    [Fact]
    public void DefaultModeNeverPicksAGreyCopy()
    {
        foreach (var definition in BlockDefinition.All.Where(static d => d.Id != BlockId.Air))
        {
            var meshes = Mesh(new MapBlocks().Set(4, 4, 4, definition.Id).Set(5, 4, 4, definition.Id).Set(4, 5, 4, definition.Id));
            Assert.True(AllVertices(meshes.Opaque, v => !IsGreyVertex(v)), $"{definition.Id} is grey by default");
            Assert.True(AllVertices(meshes.Alpha, v => !IsGreyVertex(v)), $"{definition.Id} is grey by default");
        }

        var scene = SceneBuilder.Build(SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json")), 42);
        var source = new GridBlockSource(scene.Grid);
        foreach (var chunk in scene.Grid.Chunks)
        {
            var meshes = ChunkMesher.Build(source, chunk.Coord.X, chunk.Coord.Y, chunk.Coord.Z);
            Assert.True(AllVertices(meshes.Opaque, v => !IsGreyVertex(v)));
            Assert.True(AllVertices(meshes.Alpha, v => !IsGreyVertex(v)));
        }
    }

    [Fact]
    public void AChunkReportsTheBlockKindsItHolds()
    {
        var meshes = Mesh(new MapBlocks().Set(1, 1, 1, BlockId.Grass).Set(2, 1, 1, BlockId.Stone));
        Assert.Equal(ChunkMesher.KindBit(BlockId.Grass) | ChunkMesher.KindBit(BlockId.Stone), meshes.Kinds);
        Assert.Equal(0UL, Mesh(new MapBlocks()).Kinds);
    }

    [Fact]
    public void ChangingTheGreySetMakesDueOnlyTheChunksThatHoldAChangedKind()
    {
        var scene = SceneBuilder.Build(SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json")), 42);
        var source = new GridBlockSource(scene.Grid);
        var tracker = new ChunkVersionTracker(scene.Grid);
        var trunk = ChunkMesher.KindBit(BlockId.Trunk);
        var holdingTrunk = new List<(int X, int Y, int Z)>();
        foreach (var chunk in scene.Grid.Chunks)
        {
            var coord = chunk.Coord;
            var meshes = ChunkMesher.Build(source, coord.X, coord.Y, coord.Z, tracker.GreyKinds);
            tracker.MarkMeshed(coord, meshes.Kinds);
            if ((meshes.Kinds & trunk) != 0)
            {
                holdingTrunk.Add((coord.X, coord.Y, coord.Z));
            }
        }

        Assert.Empty(tracker.Due());
        Assert.NotEmpty(holdingTrunk);
        Assert.True(holdingTrunk.Count < scene.Grid.Chunks.Count);

        Assert.Equal(0, tracker.SetGreyKinds(0)); // the same set changes nothing
        Assert.Equal(holdingTrunk.Count, tracker.SetGreyKinds(trunk));
        Assert.Equal(Sorted(holdingTrunk), Sorted(tracker.Due().Select(static c => (c.X, c.Y, c.Z))));

        foreach (var coord in tracker.Due().ToList())
        {
            tracker.MarkMeshed(coord, ChunkMesher.Build(source, coord.X, coord.Y, coord.Z, tracker.GreyKinds).Kinds);
        }

        Assert.Empty(tracker.Due());
        Assert.Equal(0, tracker.SetGreyKinds(trunk));
        Assert.Equal(holdingTrunk.Count, tracker.SetGreyKinds(0)); // back to colour: the same chunks again
        Assert.Equal(Sorted(holdingTrunk), Sorted(tracker.Due().Select(static c => (c.X, c.Y, c.Z))));
    }

    private static List<(int X, int Y, int Z)> Sorted(IEnumerable<(int X, int Y, int Z)> chunks) => chunks.OrderBy(static c => c).ToList();

    [Fact]
    public void TheGreySetIsTheBlocksWhoseNounIsNotKnownAndBlocksNoNounNamesStayInColour()
    {
        var words = new WordProgress(Nouns.Value);
        var calls = new List<ulong>();
        var world = new GreyscaleWorld(true, Nouns.Value, words, new ToastQueue(), calls.Add);
        world.Refresh();

        Assert.Single(calls);
        var grey = calls[0];
        Assert.Equal(grey, world.GreyKinds);
        Assert.True(ChunkMesher.IsGrey(BlockId.Water, grey)); // agua is not known
        Assert.True(ChunkMesher.IsGrey(BlockId.Trunk, grey)); // árbol is not known
        Assert.True(ChunkMesher.IsGrey(BlockId.Stone, grey)); // piedra is not known
        Assert.False(ChunkMesher.IsGrey(BlockId.Bush, grey)); // no noun names a bush
        Assert.False(ChunkMesher.IsGrey(BlockId.Air, grey));
    }

    [Fact]
    public void WhenANounBecomesKnownItsBlocksRegainColourAndTheToastNamesIt()
    {
        var words = new WordProgress(Nouns.Value);
        var toasts = new ToastQueue();
        var calls = new List<ulong>();
        var world = new GreyscaleWorld(true, Nouns.Value, words, toasts, calls.Add);
        world.Refresh();
        Assert.Null(toasts.Current(0));

        words.Restore([new WordRecord("agua", CreditUnits: WordProgress.UnitsToKnow, CorrectTurns: WordProgress.UsesToKnow)]);
        world.Refresh();

        Assert.Equal(2, calls.Count);
        Assert.False(ChunkMesher.IsGrey(BlockId.Water, calls[1]));
        Assert.True(ChunkMesher.IsGrey(BlockId.Trunk, calls[1]));
        Assert.Equal(ChunkMesher.KindBit(BlockId.Water), calls[0] & ~calls[1]); // exactly the water changed
        Assert.Equal("New word: el agua!", toasts.Current(0)?.Text);

        world.Refresh(); // nothing changed since: no call, no second toast
        Assert.Equal(2, calls.Count);
        Assert.Equal(0, toasts.Waiting);
    }

    [Fact]
    public void AWaterBlockNamedByTwoNounsRegainsColourWhenEitherIsKnown()
    {
        // S12, the orchestrator's water rule: agua and río both name the water block; one known name colours it.
        Assert.Equal(new[] { "agua", "río" }, Nouns.Value.NounsOfBlock(BlockId.Water).Select(static noun => noun.Lemma).Order(StringComparer.Ordinal).ToArray());

        var none = WordColours.GreyKinds(Nouns.Value, new WordProgress(Nouns.Value));
        Assert.True(ChunkMesher.IsGrey(BlockId.Water, none));

        var riverKnown = new WordProgress(Nouns.Value);
        riverKnown.Restore([new WordRecord("río", CreditUnits: WordProgress.UnitsToKnow, CorrectTurns: WordProgress.UsesToKnow)]);
        Assert.False(ChunkMesher.IsGrey(BlockId.Water, WordColours.GreyKinds(Nouns.Value, riverKnown)));

        var waterKnown = new WordProgress(Nouns.Value);
        waterKnown.Restore([new WordRecord("agua", CreditUnits: WordProgress.UnitsToKnow, CorrectTurns: WordProgress.UsesToKnow)]);
        Assert.False(ChunkMesher.IsGrey(BlockId.Water, WordColours.GreyKinds(Nouns.Value, waterKnown)));

        // A single-noun block is unchanged: the trunk is grey until árbol is known, whatever else is known.
        Assert.True(ChunkMesher.IsGrey(BlockId.Trunk, WordColours.GreyKinds(Nouns.Value, riverKnown)));
    }

    [Fact]
    public void WithTheSwitchOffNothingIsGreyAndNothingIsToasted()
    {
        var words = new WordProgress(Nouns.Value);
        var toasts = new ToastQueue();
        var calls = new List<ulong>();
        var world = new GreyscaleWorld(false, Nouns.Value, words, toasts, calls.Add);
        world.Refresh();
        words.Restore([new WordRecord("agua", CreditUnits: WordProgress.UnitsToKnow)]);
        world.Refresh();

        Assert.False(world.Enabled);
        Assert.Empty(calls);
        Assert.Equal(0UL, world.GreyKinds);
        Assert.Null(toasts.Current(0));
    }

    [Fact]
    public void NounsAssumedKnownAtTheStartAreInColourAndRaiseNoToast()
    {
        var words = new WordProgress(Nouns.Value);
        var toasts = new ToastQueue();
        var calls = new List<ulong>();
        var world = new GreyscaleWorld(true, Nouns.Value, words, toasts, calls.Add, ["agua"]);
        world.Refresh();

        Assert.True(words.RecordOf("agua").Known);
        Assert.False(ChunkMesher.IsGrey(BlockId.Water, calls[0]));
        Assert.Null(toasts.Current(0));
    }

    [Fact]
    public void AssumeKnownKeepsOtherRecordsAndSkipsWhatIsNoNoun()
    {
        var words = new WordProgress(Nouns.Value);
        words.Restore([new WordRecord("madera", Heard: 2, CreditUnits: 6, CorrectTurns: 1)]);

        var skipped = WordColours.AssumeKnown(words, ["agua", "zzz"]);

        Assert.Equal(new[] { "zzz" }, skipped);
        Assert.True(words.RecordOf("agua").Known);
        Assert.Equal(6, words.RecordOf("madera").CreditUnits);
        Assert.Equal(2, words.RecordOf("madera").Heard);
    }

    [Fact]
    public void TheLaunchOptionIsTheSwitchAndAssumeKnownTakesCommaSeparatedNouns()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "x.png", "--greyscale", "--assume-known", "madera, agua"], out var options, out var error), error);
        Assert.True(options.Greyscale);
        Assert.Equal(new[] { "madera", "agua" }, options.AssumeKnown);

        Assert.True(LaunchOptions.TryParse(["--screenshot", "x.png"], out var plain, out _));
        Assert.False(plain.Greyscale);
        Assert.Empty(plain.AssumeKnown);

        Assert.False(LaunchOptions.TryParse(["--screenshot", "x.png", "--assume-known", " , "], out _, out var empty));
        Assert.Contains("assume-known", empty);
    }
}
