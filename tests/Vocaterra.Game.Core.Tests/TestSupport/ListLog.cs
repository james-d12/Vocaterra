using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>Collects what the mixer logs, and what it plays, without a sound device.</summary>
internal sealed class ListLog : ISoundLog
{
    public List<string> Lines { get; } = [];

    public void Write(string line) => Lines.Add(line);
}
