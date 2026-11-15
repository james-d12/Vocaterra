namespace Vocaterra.Game.Core.Menu;

/// <summary>The player's settings the menu offers and the save keeps (the menu issue): each is one number the menu changes in steps and <see cref="PlayerSettings"/> clamps.</summary>
public enum SettingId
{
    /// <summary>The ambient beds' volume, 0 to 100.</summary>
    AmbientVolume,

    /// <summary>The weather's own sounds (the rain), 0 to 100: its own slider, not the ambient one (E09).</summary>
    WeatherVolume,

    /// <summary>The NPCs' voices' volume, 0 to 100.</summary>
    VoiceVolume,

    /// <summary>The effects' volume (gathering, placing, crafting, the fire), 0 to 100.</summary>
    SfxVolume,

    /// <summary>The interface and progress sounds' volume, 0 to 100.</summary>
    UiVolume,

    /// <summary>The graphics quality: 0 low, 1 medium, 2 high.</summary>
    Quality,

    /// <summary>How far the world is drawn, in blocks: the lowest value is Auto (the quality's own distance), then 48 up in steps of 16.</summary>
    ViewDistance,

    /// <summary>The interface text size: the index of 100, 125 or 150 percent.</summary>
    TextSize,

    /// <summary>The underwater blur: 0 off, 1 low, 2 medium, 3 high.</summary>
    Blur,

    /// <summary>The needs' effects on the view (dimmed edges, a blink): 0 off, 1 on.</summary>
    ViewEffects,

    /// <summary>The mouse sensitivity in percent of the normal turn.</summary>
    MouseSensitivity,

    /// <summary>Whether moving the mouse up looks down: 0 off, 1 on.</summary>
    InvertY,

    /// <summary>The vertical field of view in degrees.</summary>
    FieldOfView,

    /// <summary>The drive rate (how fast hunger, thirst and tiredness grow) in percent: 50 to 400.</summary>
    NeedsRate,

    /// <summary>The relaxed needs (half the rates, a faint instead of a death): 0 off, 1 on.</summary>
    RelaxedNeeds,

    /// <summary>The pass cheat (B08b): 0 off, 1 on. On, the pass through the east ring opens at once (and stays open when it is turned off again).</summary>
    PassCheat,
}
