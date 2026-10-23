using System.Collections.Concurrent;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Game;

/// <summary>Where the audio of a submitted turn comes from.</summary>
public enum TurnSource
{
    /// <summary>The player typed the line.</summary>
    Typed,

    /// <summary>A recorded clip (the microphone), already converted to 16 kHz and trimmed (<see cref="AudioClips.Prepare"/>).</summary>
    Clip,

    /// <summary>A WAV file the worker reads, converts and trims (<c>--wav</c>).</summary>
    Wav,

    /// <summary>A line the worker synthesizes with Kokoro and then recognizes like a recorded one (<c>--speak</c>).</summary>
    Speak,

    /// <summary>A choice the repair ladder offered, picked with a button: sent as a line, marked as a choice (S7).</summary>
    Choice,

    /// <summary>A speech button's line (<c>Ayuda</c> sends "ayuda", <c>No sé</c> sends "no sé"): sent as a line, with no credit (S7).</summary>
    Button,

    /// <summary>A recorded clip for the mic check: the worker recognizes it and reports what it heard; it is no turn (S7).</summary>
    Hear,

    /// <summary>A line the worker synthesizes with Kokoro and then hears for the mic check, as <see cref="Hear"/> (the scripted mic check, <c>--mic-check</c> without a microphone).</summary>
    HearSpeak,
}

/// <summary>
/// A flag the player sets to cancel a turn that is still being evaluated (Esc while "Pensando…"). The worker looks at it before it starts the turn
/// and again when the turn is done: a cancelled turn is rolled back (<see cref="NpcService.Rollback"/>) and its outcome carries no turn, so it
/// never reaches the world.
/// </summary>
public sealed class TurnCancellation
{
    private volatile bool _cancelled;

    /// <summary>Whether the turn was cancelled.</summary>
    public bool IsCancelled => _cancelled;

    internal bool Cancel()
    {
        if (_cancelled)
        {
            return false;
        }

        _cancelled = true;
        return true;
    }
}

/// <summary>
/// One line submitted to the worker with every input of its evaluation fixed at the moment of submission: the scene (a copy of the world
/// around the player, a copy of the inventory), the stored time and the host state hash. The worker reads nothing else, so the turn does
/// not depend on when it gets to run.
/// </summary>
/// <param name="Id">The number of the submission, from 1.</param>
/// <param name="Source">Where the line comes from.</param>
/// <param name="Text">For <see cref="TurnSource.Typed"/> the line, for <see cref="TurnSource.Speak"/> the text to speak, otherwise <see langword="null"/>.</param>
/// <param name="Clip">For <see cref="TurnSource.Clip"/>, the prepared clip.</param>
/// <param name="WavPath">For <see cref="TurnSource.Wav"/>, the file.</param>
/// <param name="Scene">The moment of the turn (see <see cref="GuideScene"/>).</param>
/// <param name="Now">The stored time of the turn, captured when it was submitted.</param>
/// <param name="HostStateHash">The simulation's state digest, captured when it was submitted.</param>
/// <param name="Review">The review question that was open when the line was submitted, or <see langword="null"/>.</param>
/// <param name="Tick">The simulation tick at submission (for reports only, and the log's time of the turn).</param>
/// <param name="Npc">The NPC the line is said to (<c>guide</c> by default).</param>
/// <param name="PhraseReview">The guide's phrase review that was open when the line was submitted (S8), or <see langword="null"/>.</param>
/// <param name="Support">The phrases in play and the help on offer when the line was submitted (S8), or <see langword="null"/>.</param>
/// <param name="Check">For a check answer (S11), the production item it answers: the worker grades it in a throwaway context and makes no turn; otherwise <see langword="null"/>.</param>
public sealed record TurnRequest(
    long Id,
    TurnSource Source,
    string? Text,
    PcmClip? Clip,
    string? WavPath,
    GuideScene Scene,
    StoredTimestamp Now,
    string HostStateHash,
    GuideReview? Review,
    long Tick,
    string Npc = NpcService.GuideAgent,
    PhraseReview? PhraseReview = null,
    SupportSnapshot? Support = null,
    CheckItem? Check = null)
{
    /// <summary>Whether the request is a mic check (<see cref="TurnSource.Hear"/> or <see cref="TurnSource.HearSpeak"/>): it hears a clip and makes no turn.</summary>
    public bool IsMicCheck => Source is TurnSource.Hear or TurnSource.HearSpeak;

    /// <summary>The flag that cancels this request while it is on the worker (see <see cref="TurnWorker.CancelPending"/>).</summary>
    public TurnCancellation Cancellation { get; } = new();

    /// <summary>How the line arrives: typed, sent by a button (a choice, <c>Ayuda</c>, <c>No sé</c>), or spoken (a clip, a file or a synthesized line).</summary>
    public InputKind Kind => Source switch
    {
        TurnSource.Typed => InputKind.Typed,
        TurnSource.Choice or TurnSource.Button => InputKind.Button,
        _ => InputKind.Spoken,
    };
}

/// <summary>What the worker heard for a mic check (S7).</summary>
/// <param name="Heard">What the recognizer heard, without its <c>[unk]</c> marks; empty when nothing was heard.</param>
/// <param name="Passed">Whether it heard "hola".</param>
/// <param name="Level">The loudest moment of the clip, 0 to 1 (the RMS of 50 ms windows).</param>
public sealed record MicCheckResult(string Heard, bool Passed, double Level);

/// <summary>How long the stages of a turn took on the worker, in milliseconds of the clock the front-end gave it (diagnostics only).</summary>
/// <param name="SynthesizeMilliseconds">Kokoro (zero for a cache hit or when nothing was spoken).</param>
/// <param name="RecognizeMilliseconds">Vosk's decode of the clip (zero for a typed line).</param>
/// <param name="TotalMilliseconds">The whole turn on the worker: synthesis, recognition and both evaluations.</param>
/// <param name="Cached">Whether a synthesized line came from the cache.</param>
public sealed record TurnTimings(double SynthesizeMilliseconds, double RecognizeMilliseconds, double TotalMilliseconds, bool Cached);

/// <summary>What the worker made of one request.</summary>
/// <param name="Request">The request.</param>
/// <param name="Turn">The turn, or <see langword="null"/> when <paramref name="Problem"/> says why there is none.</param>
/// <param name="Problem">Why no turn was made (no recognizer, no such file, an unreadable clip), in Spanish for the player.</param>
/// <param name="Timings">The stage timings.</param>
/// <param name="Cancelled">Whether the player cancelled the request: the turn (if it ran) was rolled back and nothing of it is applied.</param>
/// <param name="MicCheck">For a mic-check request (<see cref="TurnSource.Hear"/>), what was heard; otherwise <see langword="null"/>.</param>
/// <param name="Grade">For a check answer (<see cref="TurnRequest.Check"/>), what the core made of it; otherwise <see langword="null"/>.</param>
/// <param name="ReplayIndex">For a turn with an utterance record, its index in <see cref="NpcService.Records"/> (the session log numbers the records); otherwise <see langword="null"/>.</param>
public sealed record TurnOutcome(
    TurnRequest Request,
    GuideTurn? Turn,
    string? Problem,
    TurnTimings Timings,
    bool Cancelled = false,
    MicCheckResult? MicCheck = null,
    CheckGrade? Grade = null,
    int? ReplayIndex = null);

/// <summary>What loading did, for the loading screen and the report.</summary>
/// <param name="GuideMilliseconds">The first typed evaluation (JIT and lazy tables).</param>
/// <param name="SpeechMilliseconds">Vosk's model load.</param>
/// <param name="WarmUpMilliseconds">The warm-up spoken evaluation.</param>
/// <param name="TotalMilliseconds">All of it.</param>
/// <param name="Problem">Why speech is not available, or <see langword="null"/>.</param>
/// <param name="CanSpeak">Whether Kokoro's model is in the models folder.</param>
public sealed record SpeechLoad(double GuideMilliseconds, double SpeechMilliseconds, double WarmUpMilliseconds, double TotalMilliseconds, string? Problem, bool CanSpeak);

/// <summary>
/// The one thread that evaluates turns. It owns the Vosk recognizer (one instance, loaded and used and disposed on this thread only: core
/// API section 8) and the conversation (<see cref="NpcService"/>), so the frame never waits for a decode or an evaluation. The game thread
/// <see cref="Submit"/>s requests with their inputs fixed, the worker runs them in order, and the game thread <see cref="TryTake"/>s the
/// outcomes in the same order and applies them (<see cref="GuideSession.Collect"/>). Starting it loads the models (the loading screen waits
/// for <see cref="IsReady"/>).
/// </summary>
/// <remarks>
/// Determinism: the worker adds no input of its own. What a turn evaluates (line or clip, scene, stored time, state hash) is in the request;
/// the order is the order of submission; nothing here reads the clock or a random number (the optional clock only measures).
/// </remarks>
public sealed class TurnWorker : IDisposable
{
    private readonly NpcService _guide;
    private readonly ContentBundle _bundle;
    private readonly Func<double>? _clock;
    private readonly bool _loadSpeech;
    private readonly BlockingCollection<TurnRequest> _queue = [];
    private readonly ConcurrentQueue<TurnOutcome> _outcomes = new();
    private readonly object _gate = new();
    private readonly List<TurnRequest> _open = [];
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Thread _thread;
    private long _submitted;
    private long _taken;
    private volatile SpeechLoad? _load;
    private readonly CheckGrader _checks;
    private volatile bool _canHear;
    private SpokenUtteranceEvaluator? _spoken;
    private bool _started;

    /// <summary>Creates the worker; it does nothing until <see cref="Start"/>.</summary>
    /// <param name="guide">The conversations with the NPCs. After <see cref="Start"/> only the worker thread may call their <c>Say</c> methods.</param>
    /// <param name="bundle">The content bundle (for the spoken-line evaluator).</param>
    /// <param name="clockMilliseconds">A clock for the timings only; <see langword="null"/> gives zeros.</param>
    /// <param name="loadSpeech">Whether to load the speech models (false for a typed-only worker: no models, no Vosk).</param>
    /// <param name="worldSeed">The world seed of the session, for the throwaway contexts of the check answers (S11).</param>
    public TurnWorker(NpcService guide, ContentBundle bundle, Func<double>? clockMilliseconds = null, bool loadSpeech = true, ulong worldSeed = 0)
    {
        ArgumentNullException.ThrowIfNull(guide);
        ArgumentNullException.ThrowIfNull(bundle);
        _guide = guide;
        _bundle = bundle;
        _clock = clockMilliseconds;
        _loadSpeech = loadSpeech;
        _checks = new CheckGrader(bundle, worldSeed);
        _thread = new Thread(Run) { Name = "Vocaterra turn worker", IsBackground = true };
    }

    /// <summary>Whether the models are loaded and the first turn can be taken without a stall. The loading screen shows until it is.</summary>
    public bool IsReady => _ready.IsSet;

    /// <summary>What loading did, or <see langword="null"/> while it is still going.</summary>
    public SpeechLoad? Load => _load;

    /// <summary>Whether spoken lines can be evaluated (the Vosk model loaded).</summary>
    public bool CanHear => _canHear;

    /// <summary>Submitted turns whose outcome has not been taken yet.</summary>
    public int Pending => (int)(Interlocked.Read(ref _submitted) - Interlocked.Read(ref _taken));

    /// <summary>Starts the thread, which loads the models and then waits for requests. Call it once.</summary>
    public void Start()
    {
        if (_started)
        {
            throw new InvalidOperationException("The worker is already started.");
        }

        _started = true;
        _thread.Start();
    }

    /// <summary>Waits until loading has finished (for headless callers and tests; the front-end polls <see cref="IsReady"/> instead).</summary>
    /// <param name="timeoutMilliseconds">How long to wait at most.</param>
    /// <returns>Whether the worker is ready.</returns>
    public bool WaitUntilReady(int timeoutMilliseconds) => _ready.Wait(timeoutMilliseconds);

    /// <summary>Queues a request; it runs after the ones before it.</summary>
    public void Submit(TurnRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            _open.Add(request);
        }

        Interlocked.Increment(ref _submitted);
        _queue.Add(request);
    }

    /// <summary>
    /// Cancels every request that has not been finished: the ones waiting in the queue are skipped, the one on the worker is rolled back when
    /// it ends. The outcomes still come back, in order, marked <see cref="TurnOutcome.Cancelled"/>, so <see cref="Pending"/> counts down as usual.
    /// A request whose turn is already finished cannot be cancelled (its outcome is applied by the next collect).
    /// </summary>
    /// <returns>How many requests were cancelled.</returns>
    public int CancelPending()
    {
        lock (_gate)
        {
            var count = 0;
            foreach (var request in _open)
            {
                if (request.Cancellation.Cancel())
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Takes the oldest finished outcome, or returns <see langword="false"/> when none is finished. Outcomes come in submission order.</summary>
    public bool TryTake(out TurnOutcome? outcome)
    {
        if (_outcomes.TryDequeue(out outcome))
        {
            Interlocked.Increment(ref _taken);
            return true;
        }

        return false;
    }

    /// <summary>Stops the worker after the request it is on, disposes the recognizer (on its own thread) and waits for the thread to end.</summary>
    public void Dispose()
    {
        _queue.CompleteAdding();
        if (_started)
        {
            _thread.Join();
        }

        _queue.Dispose();
        _ready.Dispose();
    }

    private double Now() => _clock?.Invoke() ?? 0;

    private void Run()
    {
        SpeechEngine? engine = null;
        try
        {
            engine = LoadModels();
            foreach (var request in _queue.GetConsumingEnumerable())
            {
                _outcomes.Enqueue(Finish(request, engine));
            }
        }
        finally
        {
            _ready.Set(); // never leave a loading screen waiting on a worker that died
            engine?.Dispose();
        }
    }

    private SpeechEngine? LoadModels()
    {
        var begin = Now();
        _guide.WarmUp();
        var guideDone = Now();
        SpeechEngine? engine = null;
        string? problem = null;
        double speechMs = 0;
        double warmMs = 0;
        if (_loadSpeech)
        {
            var speechStart = Now();
            engine = SpeechEngine.TryCreate(out problem, _clock);
            speechMs = Now() - speechStart;
            if (engine is not null)
            {
                _spoken = new SpokenUtteranceEvaluator(_bundle, engine.Recognizer);
                _guide.AttachSpeech(_spoken);
                var warmStart = Now();
                _guide.WarmUpSpeech();
                warmMs = Now() - warmStart;
                _canHear = true;
            }
        }
        else
        {
            problem = "Speech off: the recogniser is not loaded. Type your answer.";
        }

        _load = new SpeechLoad(guideDone - begin, speechMs, warmMs, Now() - begin, problem, engine?.CanSpeak ?? false);
        _ready.Set();
        return engine;
    }

    // Runs one request and settles it with the player's cancel flag.
    private TurnOutcome Finish(TurnRequest request, SpeechEngine? engine)
    {
        if (request.Cancellation.IsCancelled)
        {
            return Settle(request, new TurnOutcome(request, null, null, new TurnTimings(0, 0, 0, false), Cancelled: true), null);
        }

        var outcome = Handle(request, engine, out var checkpoint);
        return Settle(request, outcome, checkpoint);
    }

    // Under the gate the request leaves the open list and, if it was cancelled meanwhile, the NPC is put back as it was before the turn (the turn never
    // happened) and the outcome says so. A cancel that comes after this point finds nothing to cancel: the outcome is applied as it is.
    internal TurnOutcome Settle(TurnRequest request, TurnOutcome outcome, NpcCheckpoint? checkpoint)
    {
        lock (_gate)
        {
            _open.Remove(request);
            if (request.Cancellation.IsCancelled && !outcome.Cancelled)
            {
                if (checkpoint is not null && outcome.Turn is not null)
                {
                    _guide.Rollback(checkpoint);
                }

                return new TurnOutcome(request, null, null, outcome.Timings, Cancelled: true);
            }

            return outcome;
        }
    }

    internal TurnOutcome Handle(TurnRequest request, SpeechEngine? engine, out NpcCheckpoint? checkpoint)
    {
        checkpoint = null;
        var begin = Now();
        double synthMs = 0;
        var cached = false;
        if (request.Check is { } item)
        {
            return HandleCheck(request, item, engine, begin);
        }

        try
        {
            GuideTurn turn;
            if (request.Source is TurnSource.Typed or TurnSource.Choice or TurnSource.Button)
            {
                checkpoint = _guide.Checkpoint(request.Npc);
                var origin = request.Source switch
                {
                    TurnSource.Choice => LineOrigin.Choice,
                    TurnSource.Button => LineOrigin.Button,
                    _ => LineOrigin.Typed,
                };
                var before = _guide.RecordCount;
                turn = _guide.Say(request.Npc, request.Text!, request.Scene, request.Now, request.HostStateHash, origin);
                return new TurnOutcome(request, turn, null, new TurnTimings(0, 0, Now() - begin, false), ReplayIndex: ReplayIndexSince(before));
            }

            if (engine is null || !_canHear)
            {
                return Failed(request, begin, _load?.Problem ?? "No speech recogniser is loaded. Type your answer.");
            }

            PcmClip? clip;
            switch (request.Source)
            {
                case TurnSource.Clip:
                case TurnSource.Hear:
                    clip = request.Clip;
                    break;
                case TurnSource.Wav:
                    var (samples, rate) = AudioClips.ReadWav(File.ReadAllBytes(request.WavPath!));
                    clip = AudioClips.Prepare(samples, rate);
                    break;
                default: // Speak, HearSpeak
                    if (!engine.CanSpeak)
                    {
                        return Failed(request, begin, $"Speech model {SpeechEngine.KokoroModelId} is missing, so the line cannot be spoken. Type your answer.");
                    }

                    var synthStart = Now();
                    clip = engine.Synthesize(request.Text!, out cached);
                    synthMs = Now() - synthStart;
                    break;
            }

            if (request.Source is TurnSource.Hear or TurnSource.HearSpeak)
            {
                var heard = MicCheck.Run(engine.Recognizer, clip);
                return new TurnOutcome(request, null, null, new TurnTimings(synthMs, engine.LastRecognizeMilliseconds, Now() - begin, cached), MicCheck: heard);
            }

            if (clip is null)
            {
                return Failed(request, begin, "I didn't hear anything.");
            }

            checkpoint = _guide.Checkpoint(request.Npc);
            var recorded = _guide.RecordCount;
            turn = _guide.SaySpoken(request.Npc, clip, request.Scene, request.Now, request.HostStateHash);
            return new TurnOutcome(request, turn, null, new TurnTimings(synthMs, engine.LastRecognizeMilliseconds, Now() - begin, cached), ReplayIndex: ReplayIndexSince(recorded));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or KeyNotFoundException or DllNotFoundException or TypeInitializationException)
        {
            return Failed(request, begin, ex.Message);
        }
    }

    // The index of the utterance record a turn added to the conversations' log (none for a turn held back as unsure, or for a host turn).
    private int? ReplayIndexSince(int before) => _guide.RecordCount > before ? _guide.RecordCount - 1 : null;

    // A check answer (S11): graded by the core in a throwaway context, on this thread (the recognizer is this thread's). It changes no conversation,
    // no learner model and no world: the outcome carries the grade, and the front-end records it.
    private TurnOutcome HandleCheck(TurnRequest request, CheckItem item, SpeechEngine? engine, double begin)
    {
        var now = request.Now;
        var digest = request.HostStateHash;
        try
        {
            if (request.Source is TurnSource.Typed or TurnSource.Button)
            {
                var input = request.Source == TurnSource.Button ? InputKind.Button : InputKind.Typed;
                var typedGrade = _checks.GradeTyped(item, request.Text!, input, now, digest);
                return new TurnOutcome(request, null, null, new TurnTimings(0, 0, Now() - begin, false), Grade: typedGrade);
            }

            if (engine is null || !_canHear || _spoken is null)
            {
                return Failed(request, begin, _load?.Problem ?? "No speech recogniser is loaded. Type your answer.");
            }

            PcmClip? clip;
            double synthMs = 0;
            var cached = false;
            switch (request.Source)
            {
                case TurnSource.Wav:
                    var (samples, rate) = AudioClips.ReadWav(File.ReadAllBytes(request.WavPath!));
                    clip = AudioClips.Prepare(samples, rate);
                    break;
                case TurnSource.Speak:
                    if (!engine.CanSpeak)
                    {
                        return Failed(request, begin, $"Speech model {SpeechEngine.KokoroModelId} is missing, so the line cannot be spoken. Type your answer.");
                    }

                    var synthStart = Now();
                    clip = engine.Synthesize(request.Text!, out cached);
                    synthMs = Now() - synthStart;
                    break;
                default: // Clip
                    clip = request.Clip;
                    break;
            }

            if (clip is null)
            {
                return Failed(request, begin, "I didn't hear anything.");
            }

            var spokenGrade = _checks.GradeSpoken(_spoken, item, clip, now, digest);
            return new TurnOutcome(request, null, null, new TurnTimings(synthMs, engine.LastRecognizeMilliseconds, Now() - begin, cached), Grade: spokenGrade);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or KeyNotFoundException or DllNotFoundException or TypeInitializationException)
        {
            return Failed(request, begin, ex.Message);
        }
    }

    private TurnOutcome Failed(TurnRequest request, double begin, string problem) =>
        new(request, null, problem, new TurnTimings(0, 0, Now() - begin, false));
}
