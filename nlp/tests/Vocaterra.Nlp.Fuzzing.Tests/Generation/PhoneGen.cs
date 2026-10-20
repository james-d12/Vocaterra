using CsCheck;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pronunciation;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>How the fake phoneme recognizer lays a phone sequence out in frames.</summary>
/// <param name="FramesPerPhone">Frames each phone holds (1 to 6).</param>
/// <param name="BlankFramesBetween">Blank frames between two phones (0 to 3); equal adjacent phones need at least one to survive CTC collapse.</param>
/// <param name="EdgeBlankFrames">Blank frames before the first and after the last phone (0 to 3).</param>
public sealed record FrameShape(int FramesPerPhone, int BlankFramesBetween, int EdgeBlankFrames);

/// <summary>One frame of a phone mumbled: the intended phone keeps <see cref="TargetMass"/> and <see cref="Alternative"/> takes the rest.</summary>
/// <param name="Index">Index of the mumbled phone in the spoken sequence.</param>
/// <param name="Alternative">The phone that takes the rest of the probability.</param>
/// <param name="TargetMass">Probability kept by the intended phone, strictly between 0 and 1.</param>
public sealed record Mumble(int Index, string Alternative, double TargetMass);

/// <summary>
/// A phone sequence straight from the inventory (1 to 20 phones, any order: the shapes a scorer meets that no real word produces), spoken through
/// the fake recognizer with an optional systematic confusion from the predicted-error table, an optional inserted <c>h</c> and an optional mumble.
/// </summary>
/// <param name="Phones">The reference phones.</param>
/// <param name="Shape">The frame layout.</param>
/// <param name="Confusion">0 none, 1 <c>ɾ</c> to <c>ɹ</c>, 2 <c>r</c> to <c>ɹ</c>, 3 <c>a</c> to <c>ə</c>, 4 <c>e</c> to <c>eɪ</c>, 5 <c>o</c> to <c>oʊ</c>, 6 insert <c>h</c>.</param>
/// <param name="InsertAt">Where the <c>h</c> goes for confusion 6.</param>
/// <param name="Mumble">The mumbled frame, or null.</param>
/// <param name="Mode">The scorer to run.</param>
/// <param name="Difficulty">The prompt difficulty.</param>
public sealed record PhoneSequenceCase(
    IReadOnlyList<string> Phones,
    FrameShape Shape,
    int Confusion,
    int InsertAt,
    Mumble? Mumble,
    ScoringMode Mode,
    Difficulty Difficulty)
{
    /// <summary>The reference, hand built: one syllable over all the phones, the way a caller without a G2P result would describe it.</summary>
    public G2pResult Reference
    {
        get
        {
            var syllable = new SyllableAlignment(new Vocaterra.Nlp.Core.Verdicts.TextSpan(0, Phones.Count), 0, Phones.Count, true);
            return new G2pResult(Phones, [syllable], 0, DialectConfig.LatinAmericanNeutral);
        }
    }

    /// <summary>The phones the recognizer is told to hear (the reference with the confusion applied).</summary>
    public IReadOnlyList<string> Spoken
    {
        get
        {
            if (Confusion == 6)
            {
                var spoken = new List<string>(Phones);
                spoken.Insert(Math.Clamp(InsertAt, 0, spoken.Count), PhoneInventory.H);
                return spoken;
            }

            return [.. Phones.Select(Replace)];
        }
    }

    /// <summary>Whether the recognizer hears the reference exactly: no confusion, no mumble, and no two equal phones that would collapse.</summary>
    public bool IsPerfect => Confusion == 0 && Mumble is null && !CollapsesEqualNeighbours;

    /// <summary>True when two equal adjacent phones sit with no blank frame between them (CTC collapses them into one).</summary>
    public bool CollapsesEqualNeighbours => Shape.BlankFramesBetween == 0 && Enumerable.Range(1, Phones.Count - 1).Any(i => Phones[i] == Phones[i - 1]);

    /// <summary>The posteriors the fake recognizer returns for this case.</summary>
    public Vocaterra.Nlp.Core.Stages.PhonePosteriors Posteriors(FakePhonemeRecognizer fake)
    {
        var spoken = Spoken;
        if (Mumble is { } mumble)
        {
            var index = mumble.Index % spoken.Count;
            var alternative = mumble.Alternative == spoken[index] ? PhoneInventory.All.First(p => p != spoken[index]) : mumble.Alternative;
            return fake.FromMumbled(spoken, index, alternative, mumble.TargetMass, Shape.FramesPerPhone, Shape.BlankFramesBetween, Shape.EdgeBlankFrames);
        }

        return fake.FromPhones(spoken, Shape.FramesPerPhone, Shape.BlankFramesBetween, edgeBlankFrames: Shape.EdgeBlankFrames);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"phones=[{string.Join(' ', Phones)}] shape={Shape} confusion={Confusion}@{InsertAt} mumble={Mumble} mode={Mode} difficulty={Difficulty}";
    }

    private string Replace(string phone)
    {
        return (Confusion, phone) switch
        {
            (1, PhoneInventory.Tap) => PhoneInventory.EnglishR,
            (2, PhoneInventory.Trill) => PhoneInventory.EnglishR,
            (3, PhoneInventory.A) => PhoneInventory.Schwa,
            (4, PhoneInventory.E) => PhoneInventory.EnglishEi,
            (5, PhoneInventory.O) => PhoneInventory.EnglishOu,
            _ => phone,
        };
    }
}

/// <summary>
/// A real phrase (lexicon words through the G2P, so the reference carries syllables, stress and grapheme spans) spoken through the fake
/// recognizer, optionally with one predicted learner error from the predicted-error table, and optionally a mumble.
/// </summary>
/// <param name="Text">The phrase.</param>
/// <param name="SpokenDialect">Index into <see cref="DialectConfig.AllShipped"/> (modulo its length) of the dialect the speaker uses.</param>
/// <param name="ErrorIndex">Selects one non-stress predicted error of the spoken variant (modulo their number), or -1 for none.</param>
/// <param name="Shape">The frame layout.</param>
/// <param name="Mumble">The mumbled frame, or null; the index is taken modulo the spoken length.</param>
/// <param name="Mode">The scorer to run.</param>
/// <param name="Difficulty">The prompt difficulty.</param>
public sealed record SpokenCase(string Text, int SpokenDialect, int ErrorIndex, FrameShape Shape, Mumble? Mumble, ScoringMode Mode, Difficulty Difficulty)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return $"text=\"{Text}\" dialect={SpokenDialect} error={ErrorIndex} shape={Shape} mumble={Mumble} mode={Mode} difficulty={Difficulty}";
    }
}

/// <summary>
/// Phone generators: inventory sequences of one to twenty phones with posterior matrices from <see cref="FakePhonemeRecognizer.FromPhones"/>,
/// confusions drawn from the predicted-error table (<c>ɾ→ɹ</c>, an inserted <c>h</c>, <c>a→ə</c>, <c>e→eɪ</c>, <c>o→oʊ</c>), mumbled frames, and
/// phrases from the lexicon for the cases where the grapheme span and tag of an error matter.
/// </summary>
public static class PhoneGen
{
    /// <summary>The longest inventory sequence (plan section 4).</summary>
    public const int MaxSequenceLength = 20;

    /// <summary>The longest spoken phrase, in phones; the scorer's cost grows with the cube of the length (see the README).</summary>
    public const int MaxSpokenPhones = 40;

    /// <summary>A dialect: one of the shipped presets two times in three, otherwise any combination of the four switches.</summary>
    public static Gen<DialectConfig> Dialect { get; } = Gen.Frequency(
        (2, (IGen<DialectConfig>)Gen.OneOfConst([.. DialectConfig.AllShipped])),
        (1, (IGen<DialectConfig>)Gen.Select(
            Gen.Bool,
            Gen.Bool,
            Gen.Enum<LlSound>(),
            Gen.Bool,
            static (seseo, yeismo, ll, aspiration) => new DialectConfig(seseo, yeismo, ll, aspiration))));

    /// <summary>A frame layout; mostly the generous default so most cases are scoreable, sometimes the tightest layouts the recognizer can produce.</summary>
    public static Gen<FrameShape> Shape { get; } = Gen.Select(
        Gen.Frequency((5, (IGen<int>)Gen.Const(3)), (3, (IGen<int>)Gen.Int[1, 6])),
        Gen.Frequency((5, (IGen<int>)Gen.Const(1)), (3, (IGen<int>)Gen.Int[0, 3])),
        Gen.Frequency((5, (IGen<int>)Gen.Const(0)), (2, (IGen<int>)Gen.Int[0, 3])),
        static (frames, blanks, edges) => new FrameShape(frames, blanks, edges));

    /// <summary>A mumble that makes the alternative win the argmax most of the time and sometimes leaves the intended phone ahead.</summary>
    public static Gen<Mumble?> OptionalMumble { get; } = Gen.Frequency(
        (5, (IGen<Mumble?>)Gen.Const((Mumble?)null)),
        (2, (IGen<Mumble?>)Gen.Select(
            Gen.Int[0, 1000],
            Gen.OneOfConst([.. PhoneInventory.All]),
            Gen.Frequency((1, (IGen<double>)Gen.Double[0.02, 0.48]), (1, (IGen<double>)Gen.Double[0.52, 0.98]), (1, (IGen<double>)Gen.OneOfConst(0.5, 0.49999, 0.50001))),
            static (index, alternative, mass) => (Mumble?)new Mumble(index, alternative, mass))));

    /// <summary>Phones of a sequence: from the fixed edge sequences one time in four, otherwise drawn uniformly from the inventory.</summary>
    public static Gen<IReadOnlyList<string>> Sequence { get; } = Gen.Frequency(
        (FuzzOptions.BenignWeight, (IGen<IReadOnlyList<string>>)Gen.OneOfConst([.. PhoneInventory.All]).List[1, MaxSequenceLength].Select(static list => (IReadOnlyList<string>)list)),
        (FuzzOptions.AdversarialWeight, (IGen<IReadOnlyList<string>>)Gen.OneOfConst([.. AdversarialCorpus.PhoneSequences])));

    /// <summary>An inventory sequence case.</summary>
    public static Gen<PhoneSequenceCase> SequenceCase { get; } = Gen.Select(
        Sequence,
        Shape,
        Gen.Frequency((5, (IGen<int>)Gen.Const(0)), (3, (IGen<int>)Gen.Int[1, 6])),
        Gen.Int[0, MaxSequenceLength],
        OptionalMumble,
        Gen.Enum<ScoringMode>(),
        Gen.Enum<Difficulty>(),
        static (phones, shape, confusion, insertAt, mumble, mode, difficulty) => new PhoneSequenceCase(phones, shape, confusion, insertAt, mumble, mode, difficulty));

    /// <summary>
    /// A phrase of one to three lexicon words, spoken with or without one predicted error. One phrase in three starts with a word that has a silent
    /// <c>h</c>, so the rarest error kind (a pronounced silent h) is reached as often as the others.
    /// </summary>
    public static Gen<SpokenCase> SpokenCase(PipelineHarness harness)
    {
        var silentH = Gen.OneOfConst([.. harness.Words.AllForms.Where(static f => f.Contains('h', StringComparison.Ordinal) && !f.Contains("ch", StringComparison.Ordinal) && !f.Contains(' ', StringComparison.Ordinal))]);
        var withSilentH = Gen.Select(silentH, SpanishTextGen.Phrase(harness.Words, 0, 2), static (first, rest) => rest.Length == 0 ? first : first + " " + rest);
        var phrase = Gen.Frequency(
            (2, (IGen<string>)SpanishTextGen.Phrase(harness.Words, 1, 3)),
            (1, (IGen<string>)withSilentH));
        return Gen.Select(
            phrase,
            Gen.Int[0, DialectConfig.AllShipped.Count - 1],
            Gen.Frequency((2, (IGen<int>)Gen.Const(-1)), (3, (IGen<int>)Gen.Int[0, 1000])),
            Shape,
            OptionalMumble,
            Gen.Enum<ScoringMode>(),
            Gen.Enum<Difficulty>(),
            static (text, dialect, error, shape, mumble, mode, difficulty) => new SpokenCase(text, dialect, error, shape, mumble, mode, difficulty));
    }
}
