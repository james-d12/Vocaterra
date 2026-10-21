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
/// <param name="Tick">The simulation tick at submission (for reports only).</param>
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
    long Tick)
{
    /// <summary>How the line arrives: typed, or spoken (a clip, a file or a synthesized line).</summary>
    public InputKind Kind => Source == TurnSource.Typed ? InputKind.Typed : InputKind.Spoken;
}

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
public sealed record TurnOutcome(TurnRequest Request, GuideTurn? Turn, string? Problem, TurnTimings Timings);

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
/// API section 8) and the conversation (<see cref="GuideService"/>), so the frame never waits for a decode or an evaluation. The game thread
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
    private readonly GuideService _guide;
    private readonly ContentBundle _bundle;
    private readonly Func<double>? _clock;
    private readonly bool _loadSpeech;
    private readonly BlockingCollection<TurnRequest> _queue = [];
    private readonly ConcurrentQueue<TurnOutcome> _outcomes = new();
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Thread _thread;
    private long _submitted;
    private long _taken;
    private volatile SpeechLoad? _load;
    private volatile bool _canHear;
    private bool _started;

    /// <summary>Creates the worker; it does nothing until <see cref="Start"/>.</summary>
    /// <param name="guide">The conversation. After <see cref="Start"/> only the worker thread may call its <c>Say</c> methods.</param>
    /// <param name="bundle">The content bundle (for the spoken-line evaluator).</param>
    /// <param name="clockMilliseconds">A clock for the timings only; <see langword="null"/> gives zeros.</param>
    /// <param name="loadSpeech">Whether to load the speech models (false for a typed-only worker: no models, no Vosk).</param>
    public TurnWorker(GuideService guide, ContentBundle bundle, Func<double>? clockMilliseconds = null, bool loadSpeech = true)
    {
        ArgumentNullException.ThrowIfNull(guide);
        ArgumentNullException.ThrowIfNull(bundle);
        _guide = guide;
        _bundle = bundle;
        _clock = clockMilliseconds;
        _loadSpeech = loadSpeech;
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
        Interlocked.Increment(ref _submitted);
        _queue.Add(request);
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
                _outcomes.Enqueue(Handle(request, engine));
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
                _guide.AttachSpeech(new SpokenUtteranceEvaluator(_bundle, engine.Recognizer));
                var warmStart = Now();
                _guide.WarmUpSpeech();
                warmMs = Now() - warmStart;
                _canHear = true;
            }
        }
        else
        {
            problem = "Sin voz: el reconocedor no se ha cargado.";
        }

        _load = new SpeechLoad(guideDone - begin, speechMs, warmMs, Now() - begin, problem, engine?.CanSpeak ?? false);
        _ready.Set();
        return engine;
    }

    private TurnOutcome Handle(TurnRequest request, SpeechEngine? engine)
    {
        var begin = Now();
        double synthMs = 0;
        var cached = false;
        try
        {
            GuideTurn turn;
            if (request.Source == TurnSource.Typed)
            {
                turn = _guide.Say(request.Text!, request.Scene, request.Now, request.HostStateHash);
                return new TurnOutcome(request, turn, null, new TurnTimings(0, 0, Now() - begin, false));
            }

            if (engine is null || !_canHear)
            {
                return Failed(request, begin, _load?.Problem ?? "No hay reconocedor de voz.");
            }

            PcmClip? clip;
            switch (request.Source)
            {
                case TurnSource.Clip:
                    clip = request.Clip;
                    break;
                case TurnSource.Wav:
                    var (samples, rate) = AudioClips.ReadWav(File.ReadAllBytes(request.WavPath!));
                    clip = AudioClips.Prepare(samples, rate);
                    break;
                default:
                    if (!engine.CanSpeak)
                    {
                        return Failed(request, begin, $"Falta el modelo {SpeechEngine.KokoroModelId}: no se puede sintetizar la frase.");
                    }

                    var synthStart = Now();
                    clip = engine.Synthesize(request.Text!, out cached);
                    synthMs = Now() - synthStart;
                    break;
            }

            if (clip is null)
            {
                return Failed(request, begin, "No se oyó nada.");
            }

            turn = _guide.SaySpoken(clip, request.Scene, request.Now, request.HostStateHash);
            return new TurnOutcome(request, turn, null, new TurnTimings(synthMs, engine.LastRecognizeMilliseconds, Now() - begin, cached));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or KeyNotFoundException or DllNotFoundException or TypeInitializationException)
        {
            return Failed(request, begin, ex.Message);
        }
    }

    private TurnOutcome Failed(TurnRequest request, double begin, string problem) =>
        new(request, null, problem, new TurnTimings(0, 0, Now() - begin, false));
}
