using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>An output that records every call: the tests never open an audio device.</summary>
internal sealed class RecordingOutput : ISoundOutput
{
    public List<SoundRequest> Played { get; } = [];

    public int Stops { get; private set; }

    public List<BedLevel> Beds { get; private set; } = [];

    public bool Available => true;

    public void Play(SoundRequest request) => Played.Add(request);

    public void StopAll() => Stops++;

    public void SetBeds(IReadOnlyList<BedLevel> levels) => Beds = [.. levels];

    public void Dispose()
    {
    }
}
