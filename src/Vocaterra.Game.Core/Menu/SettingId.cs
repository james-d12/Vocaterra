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

    /// <summary>The decorative grass on the grass blocks' tops (B18, D-game-34 item 34): its density in percent, 0 (none: the look of before) to 100, in steps of 10.</summary>
    Grass,
    /// <summary>The worker threads that make chunks, meshes and far tiles beside the game loop (B07d, D-game-34 item 42): 0 is Auto (the processor count less four, at least 1, at most 8), then 1 to 8. A change takes effect at once, without losing the work in the queues.</summary>
    Workers,

    /// <summary>How far the far view's land is drawn (B07d): 0 is Auto (the graphics quality's own, 4, 8 or 16 km), then 1 to 16 km.</summary>
    FarDistance,

    /// <summary>How much detail the far view keeps at a distance (B07d): the bands the finer levels last for, in percent of the usual, 50 to 200 (100 is the picture of before; more is finer and dearer).</summary>
    FarDetail,

    /// <summary>How far every tree is drawn as a tree of its own in the far view (B07d): 0 Auto (the quality's own), 1 none, then to 256 m, 512 m, 1 km, 2 km, 4 km (a forest is a canopy layer beyond).</summary>
    FarTrees,

    /// <summary>How many of the far view's finest levels draw each face with its block's texture (B07d): 0 none (flat colours), 1, 2 (the default) or 3.</summary>
    FarTextures,

    /// <summary>How far from the eye the far view is made from the real chunks the generator makes (B07d): 0 Auto (the quality's own), 1 none (the model alone), then 500 m, 1 km, 1.5 km, 2 km.</summary>
    FarExact,

    /// <summary>The game's own GLSL shaders for the chunks (B17b, D-game-34 item 43): 0 off (MonoGame's effects draw the chunks), 1 on (the default; the effects draw anyway when the context cannot run the program). The graphics effects of B17c join it, each its own toggle.</summary>
    Shaders,

    /// <summary>The sun follows the hour (B17c, B22's effect; item 43): 1 on (the default: the faces are shaded by the sky's sun through the day, and the dusk is the deeper one), 0 off (the sun stays where the Day band had it and the dusk is the lighter, older one).</summary>
    SunFollowsHour,

    /// <summary>The climate colours (B17c, B22's effect): 1 on (the default: the grass and the oak leaves take the colour of their climate), 0 off (the colour map's middle everywhere).</summary>
    ClimateColours,

    /// <summary>The water shader (B17c, with B22's depth tint): 1 on (the default: the tint by depth, a gentle movement of the surface and a brighter edge at grazing angles), 0 off (plain water).</summary>
    WaterShader,

    /// <summary>The fog by height (B17c): 1 on (the default: the haze lies thicker in the low land, in the same colours as the far view's), 0 off (the distance fog alone).</summary>
    HeightFog,

    /// <summary>The far view's light (B17c): 1 on (the default: the far view is drawn through the same program as the chunks, with the same sun and fog), 0 off (the far view is drawn as before, in the day's baked light).</summary>
    FarLight,

    /// <summary>The window's antialiasing (B22, moved into the Graphics group by B17c): the index of 0, 2, 4 or 8 samples a pixel (the default 4); read when the window opens, so a change takes effect at the next start.</summary>
    Msaa,

    /// <summary>The fly cheat: 0 off, 1 on. On, the player flies: no gravity, the jump key rises and the descend key sinks, the sprint key flies faster.</summary>
    Fly,

    /// <summary>The flying speed in blocks per second, 5 to 100 in steps of 5 (the default 30, about seven times the walk).</summary>
    FlySpeed,
}
