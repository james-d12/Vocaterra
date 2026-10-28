using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Sound;
using Vocaterra.Game.Utilities;

namespace Vocaterra.Game.Flow;

/// <summary>
/// The turns the worker finished, applied in order (S5, S7, S9): the panel (through <see cref="TurnPump"/>), the answer's cue, the NPC's lines
/// spoken, the mic check's result, and the console's account of each turn. A check answer is the battery's (it grades it when the turn is
/// applied); a cancelled turn changes nothing.
/// </summary>
public sealed class TurnPresenter
{
    private readonly TurnPump _pump;
    private readonly SoundFrontend _sound;
    private readonly NpcVoice _voice;
    private readonly DialogueModel _dialogue;
    private readonly MicCheckModel? _micCheck;
    private readonly Action<string> _console;

    /// <summary>Creates the presenter.</summary>
    /// <param name="pump">Applies the finished turns to the session and the panel.</param>
    /// <param name="sound">Plays an answer's cue.</param>
    /// <param name="voice">Says the NPCs' lines.</param>
    /// <param name="dialogue">The panel (what it shows of a turn is in the console too).</param>
    /// <param name="micCheck">The mic check, which a mic check's turn completes, or <see langword="null"/>.</param>
    /// <param name="console">Where the turns' lines go.</param>
    public TurnPresenter(TurnPump pump, SoundFrontend sound, NpcVoice voice, DialogueModel dialogue, MicCheckModel? micCheck, Action<string> console)
    {
        _pump = pump;
        _sound = sound;
        _voice = voice;
        _dialogue = dialogue;
        _micCheck = micCheck;
        _console = console;
    }

    /// <summary>Applies the turns the worker has finished since the last call, in order.</summary>
    public void Apply()
    {
        foreach (var applied in _pump.Apply())
        {
            var turn = applied.Turn;
            if (applied.Outcome.Request.Check is not null)
            {
                continue; // a check answer: the battery took it when it was applied
            }

            if (applied.Outcome.Cancelled)
            {
                if (!applied.Outcome.Request.IsMicCheck)
                {
                    _console("turn cancelled: the player pressed Esc while it was thinking; nothing of it was applied");
                }

                continue;
            }

            if (applied.Outcome.Request.IsMicCheck)
            {
                // The sound for a mic check's result comes with its voice event (passed or not), in the session's events (S24).
                var result = applied.Outcome.MicCheck ?? new MicCheckResult(string.Empty, false, 0);
                _console($"mic check: heard \"{result.Heard}\", level {result.Level:0.00}, {(result.Passed ? "passed" : "not passed")}{(applied.Outcome.Problem is null ? string.Empty : $" ({applied.Outcome.Problem})")}");
                _micCheck?.Complete(result);
                continue;
            }

            if (turn is null)
            {
                _console($"turn failed: {applied.Outcome.Problem}");
                continue;
            }

            _sound.ObserveTurn(turn, applied.Words); // the answer's cue: understood, or the neutral one, and a new known word (S24)
            Print(turn, applied.Outcome.Timings.TotalMilliseconds);
            Speak(turn);
        }
    }

    // The console's account of a turn: what was heard or typed, the verdict, the NPC's lines, and what the panel shows of it.
    private void Print(GuideTurn turn, double milliseconds)
    {
        var detail = turn.Input == InputKind.Spoken
            ? $"heard \"{turn.Line}\" (confidence {(int)Math.Round((turn.Confidence ?? 0) * 100)} %){(turn.Uncertain ? ", UNCERTAIN" : string.Empty)} -> {turn.Summary}, {milliseconds:0} ms"
            : $"\"{turn.Line}\" -> {turn.Summary}, {milliseconds:0.0} ms";
        _console($"turn {_dialogue.TurnCount} ({turn.Npc}, {turn.Prompt}): {(turn.Input == InputKind.Spoken ? "spoken " : string.Empty)}{detail}, state {turn.AgentState}{(turn.HostEvents.Count > 0 ? $", host events {string.Join(" ", turn.HostEvents)}" : string.Empty)}{(turn.FailForward ? ", FAIL-FORWARD" : string.Empty)}");
        foreach (var line in turn.GuideLines)
        {
            _console($"  {turn.Npc}: {line}");
        }
    }

    // The NPC's lines, in the order the panel shows them (S9); "Más despacio, por favor" (S17): her reply, then her last line again from the slow
    // recordings. Then the rest of the console's account: the panel's heard line, the choices and the arrow.
    private void Speak(GuideTurn turn)
    {
        if (turn.SlowFollowUp && turn.FollowUp is { Count: > 0 } again)
        {
            _voice.Say(turn.Npc, turn.GuideLines.Take(turn.GuideLines.Count - again.Count));
            foreach (var line in again)
            {
                _voice.Append(turn.Npc, line, slow: true);
            }
        }
        else
        {
            _voice.Say(turn.Npc, turn.GuideLines);
        }

        if (_dialogue.Heard is { } heard && turn.Input == InputKind.Spoken)
        {
            _console($"  panel: {heard.Text}");
        }

        if (turn.Choices is { Count: > 0 })
        {
            _console($"  choices: {string.Join(" | ", _dialogue.Choices.Select(static c => c.Label))}");
        }

        if (turn.Pointer is { } arrow)
        {
            _console($"  arrow: {arrow.Place} at {arrow.Target.X},{arrow.Target.Y},{arrow.Target.Z} ({arrow.Blocks} blocks, {arrow.Direction})");
        }
    }
}
