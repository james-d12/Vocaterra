using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.UI;

/// <summary>A scripted spoken line waiting to be sent (<c>--wav</c>, <c>--speak</c>): the game submits it to the worker between ticks.</summary>
/// <param name="Source">A WAV file or a line to synthesize.</param>
/// <param name="Value">The file path, or the text to speak.</param>
public sealed record SpeechRequest(TurnSource Source, string Value);
