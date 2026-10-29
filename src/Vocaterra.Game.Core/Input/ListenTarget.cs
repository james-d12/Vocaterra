using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Core.Input;

/// <summary>Who a recording is for: what to do with the clip when it ends, and with the level while it runs (the mic check's meter).</summary>
/// <param name="Heard">Takes the clip when the recording ends (the key went up or the recording hit its cap), or <see langword="null"/> when nothing usable was heard.</param>
/// <param name="Level">Takes the level of what was collected and the frame's seconds, once a frame while the recording runs; <see langword="null"/> when nobody shows it.</param>
public sealed record ListenTarget(Action<PcmClip?> Heard, Action<double, double>? Level = null);
