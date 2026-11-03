namespace Vocaterra.Game.Core.Npc;

/// <summary>The voice the speech renderer records an NPC's lines with (schema 2, village-plan V01; it was a table in the renderer).</summary>
/// <param name="Speaker">The Kokoro speaker id (<c>29</c>; the core's voice table, <c>docs/nlp/api.md</c> section 6).</param>
/// <param name="Name">The voice's name in the model's table (<c>em_alex</c>), written into the speech index and the provenance; empty when the file gives none (a validation error).</param>
/// <param name="Speed">The speed of the normal recordings (1.0); the slow ones are a fraction of it.</param>
/// <param name="Gain">
/// The factor the renderer scales the voice's samples by (village-plan V11), 1 by default: a voice the model renders louder than the Spanish ones is brought to
/// their level without touching the other NPCs' recordings (the baker's <c>if_sara</c> is about three times louder, so 0.32).
/// </param>
public sealed record NpcVoiceSpec(int Speaker, string Name, float Speed, float Gain = 1.0f);
