using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Physics;

namespace Vocaterra.Game.Core.Sound;

/// <summary>
/// The ambient sound of a tick (S25): it gives the mixer the beds' targets for the player's place, logs a line when the place's weights change, and schedules the
/// scheduled sounds over their beds: the birds' chirps (in clusters, by the forest bed) and the fire's crackles (by the fire bed). Every time is in ticks and every
/// draw is from the presentation generator, so the same route gives the same schedule. Nothing here touches the simulation.
/// </summary>
public sealed class AmbientSound
{
    private const double MinChirpGapSeconds = 1.5;
    private const double MaxChirpGapSeconds = 5.0;
    private const double ChirpSpacingSeconds = 0.28;
    private const double MinCrackleGapSeconds = 0.15;
    private const double MaxCrackleGapSeconds = 0.9;
    private const double Silent = 0.02;

    private readonly SoundMixer _mixer;
    private readonly AmbientPlaces _places;
    private readonly List<(long Tick, double Pan)> _chirps = [];
    private long _nextChirpCluster = -1;
    private long _nextCrackle = -1;
    private double _firePan;
    private string _lastNote = string.Empty;

    /// <summary>Creates the ambient sound over the mixer.</summary>
    /// <param name="mixer">The mixer.</param>
    /// <param name="places">The scene's places.</param>
    public AmbientSound(SoundMixer mixer, AmbientPlaces places)
    {
        _mixer = mixer ?? throw new ArgumentNullException(nameof(mixer));
        _places = places ?? throw new ArgumentNullException(nameof(places));
    }

    /// <summary>The beds' weights for a listener (the same as <see cref="AmbientBeds.At"/>).</summary>
    /// <param name="listener">The listener.</param>
    /// <param name="atmosphere">The atmosphere now (the daylight sets the birds and the insects).</param>
    /// <param name="fireLit">Whether the player's own fire is lit.</param>
    /// <param name="highestBlock">Reads the highest block of a column of the world (the rain is lower under cover), or <see langword="null"/> for open ground.</param>
    public IReadOnlyList<BedInput> WeightsAt(Listener listener, AtmosphereSnapshot atmosphere, bool fireLit, Func<int, int, int>? highestBlock = null) => AmbientBeds.At(_places, listener, atmosphere, fireLit, highestBlock);

    /// <summary>
    /// One tick: the beds' targets are set for the listener, a line is logged when the place or its weights change (rounded to a tenth), and the chirps and crackles
    /// due this tick play, at the bed's weight.
    /// </summary>
    /// <param name="tick">The tick.</param>
    /// <param name="listener">The listener.</param>
    /// <param name="atmosphere">The atmosphere now, as the simulation reports it.</param>
    /// <param name="fireLit">Whether the player's own fire is lit.</param>
    /// <param name="highestBlock">Reads the highest block of a column of the world (E06: the rain is lower under cover); read only, never written.</param>
    public void Observe(long tick, Listener listener, AtmosphereSnapshot atmosphere, bool fireLit, Func<int, int, int>? highestBlock = null)
    {
        _mixer.SetTick(tick);
        var inputs = WeightsAt(listener, atmosphere, fireLit, highestBlock);
        _mixer.SetBeds(inputs);
        var weights = inputs.ToDictionary(static b => b.Bed, static b => b.Weight, StringComparer.Ordinal);
        _firePan = inputs.First(static b => b.Bed == AmbientBeds.Fire).Pan;

        var place = AmbientBeds.PlaceOf(_places, listener);
        var heard = string.Join(' ', AmbientBeds.All.Where(bed => Math.Round(weights[bed], 1) > 0).Select(bed => $"{bed}={weights[bed]:0.0}"));
        var note = $"{place} {heard}";
        if (note != _lastNote)
        {
            _lastNote = note;
            _mixer.Mark($"bed at {place}: {(heard.Length == 0 ? "none" : heard)}");
        }

        PlayChirps(tick, weights[AmbientBeds.Birds]);
        if (tick >= _nextCrackle)
        {
            var weight = weights[AmbientBeds.Fire];
            if (weight > Silent && !_mixer.Recording)
            {
                _mixer.Play(SoundCue.FireCrackle, weight, _firePan);
            }

            _nextCrackle = tick + Ticks(_mixer.Rng.NextBetween(MinCrackleGapSeconds, MaxCrackleGapSeconds));
        }
    }

    private void PlayChirps(long tick, double weight)
    {
        if (_nextChirpCluster < 0)
        {
            _nextChirpCluster = tick + Ticks(_mixer.Rng.NextBetween(MinChirpGapSeconds, MaxChirpGapSeconds));
        }

        if (tick >= _nextChirpCluster)
        {
            var rng = _mixer.Rng;
            var count = 1 + rng.NextInt(3);
            for (var k = 0; k < count; k++)
            {
                _chirps.Add((tick + Ticks(k * ChirpSpacingSeconds), rng.NextBetween(-0.6, 0.6)));
            }

            _nextChirpCluster = tick + Ticks(rng.NextBetween(MinChirpGapSeconds, MaxChirpGapSeconds));
        }

        for (var i = _chirps.Count - 1; i >= 0; i--)
        {
            if (tick < _chirps[i].Tick)
            {
                continue;
            }

            if (weight > Silent && !_mixer.Recording)
            {
                _mixer.Play(SoundCue.BirdChirp, weight, _chirps[i].Pan);
            }

            _chirps.RemoveAt(i);
        }
    }

    private static long Ticks(double seconds) => (long)Math.Round(seconds * VoxelPhysics.TicksPerSecond);
}
