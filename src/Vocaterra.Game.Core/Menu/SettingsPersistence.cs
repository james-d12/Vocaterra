using Vocaterra.Game.Core.Save;

namespace Vocaterra.Game.Core.Menu;

/// <summary>
/// Keeps <see cref="PlayerSettings"/> in the global settings file (Q01): a change marks the settings dirty, and the front-end's <see cref="Pump"/> (once a frame) writes them when the
/// last change is <see cref="DelayMilliseconds"/> old, so a slider that is dragged is written once it rests, and <see cref="Flush"/> writes at once (a page closing, the game ending). A write
/// that fails is said and tried again later; the game goes on.
/// </summary>
public sealed class SettingsPersistence
{
    /// <summary>How long after the last change the settings are written.</summary>
    public const long DelayMilliseconds = 400;

    private readonly string _path;
    private readonly PlayerSettings _settings;
    private readonly Func<long> _clock;
    private readonly Action<string> _log;
    private bool _dirty;
    private long _changedAt;

    /// <summary>Starts keeping <paramref name="settings"/> in <paramref name="path"/>.</summary>
    /// <param name="path">The settings file.</param>
    /// <param name="settings">The settings; a change to any of them is kept.</param>
    /// <param name="clock">Milliseconds on a steady clock.</param>
    /// <param name="log">Where a line about a failed write goes.</param>
    public SettingsPersistence(string path, PlayerSettings settings, Func<long> clock, Action<string> log)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(log);
        _path = path;
        _settings = settings;
        _clock = clock;
        _log = log;
        settings.Changed += OnChanged;
    }

    /// <summary>Whether a change is waiting to be written.</summary>
    public bool Dirty => _dirty;

    /// <summary>Writes the settings when a change has rested for <see cref="DelayMilliseconds"/>.</summary>
    public void Pump()
    {
        if (_dirty && _clock() - _changedAt >= DelayMilliseconds)
        {
            Flush();
        }
    }

    /// <summary>Writes the settings now when a change is waiting.</summary>
    public void Flush()
    {
        if (!_dirty)
        {
            return;
        }

        try
        {
            SettingsFile.Write(_path, _settings.ToSave());
            _dirty = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _changedAt = _clock();
            _log($"settings: not written to {_path}: {ex.Message}");
        }
    }

    /// <summary>Stops keeping the settings (the owner is done with them): a waiting change is written first.</summary>
    public void Close()
    {
        _settings.Changed -= OnChanged;
        Flush();
    }

    private void OnChanged(SettingId id)
    {
        _dirty = true;
        _changedAt = _clock();
    }
}
