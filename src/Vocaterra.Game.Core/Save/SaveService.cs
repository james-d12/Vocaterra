using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Save;

/// <summary>
/// The game's saves (S10, X05): one capture of the session, for F6, the <c>--save</c> at the end of a run, the playtest's save, the autosave and the crash's
/// last-chance save. A save lists the chunks that differ from the world as generated; the grid knows them itself (B05), so no second copy of the world is built or kept.
/// </summary>
public sealed class SaveService
{
    private readonly VoxelSession _session;
    private readonly Func<SaveSettings> _settings;
    private readonly Action<string> _log;
    private readonly DialogueModel? _dialogue;

    /// <summary>Creates the saves of a session.</summary>
    /// <param name="session">The session to save.</param>
    /// <param name="settings">The settings that ride in the save: the sound volumes (S24) and the text size (X08).</param>
    /// <param name="log">Where a line about a save goes (the console).</param>
    /// <param name="dialogue">The dialogue panel whose NPC pages ride in the save (U08), or <see langword="null"/> for none.</param>
    public SaveService(VoxelSession session, Func<SaveSettings> settings, Action<string> log, DialogueModel? dialogue = null)
    {
        _dialogue = dialogue;
        _session = session;
        _settings = settings;
        _log = log;
    }

    /// <summary>The save of the session as it is now. The session must be at rest (<see cref="GuideSession.CanSave"/>).</summary>
    public SaveDocument Capture() => SaveGame.Capture(_session, _settings(), _dialogue is null ? null : DialoguePageSave.Capture(_dialogue.Pages));

    /// <summary>
    /// Writes the save of the session to <paramref name="path"/>, or says why not: a session that is not at rest, or that holds a stack the full bag cannot take back (I04), is not saved, and that is said,
    /// not thrown. Returns whether the file was written.
    /// </summary>
    /// <param name="path">The save file.</param>
    /// <param name="why">When the save is taken, for the line (<c>on F6</c>, <c>at the end of the run</c>).</param>
    public bool Write(string path, string why)
    {
        if (!_session.CanSave)
        {
            _log($"save: not written {why}: the guide is still thinking, or the key words are on show");
            return false;
        }

        if (!_session.Simulation.ReturnHeld())
        {
            _log($"save: not written {why}: a stack is held at the cursor and the bag has no room for it"); // I04: a quit or an F6 with the inventory screen open must not throw
            return false;
        }

        var document = Capture();
        var text = SaveGame.Serialize(document);
        SaveFile.Write(path, text);
        _log($"save: {path} written {why} (session {document.Session}, tick {document.Tick}, stored time {document.StoredTime}, world digest {document.StateDigest[..16]}, {System.Text.Encoding.UTF8.GetByteCount(text)} bytes)");
        return true;
    }
}
