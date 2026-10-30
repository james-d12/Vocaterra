# The voice trial (VILLAGE_PLAN V06)

The Kokoro model (`kokoro-multi-lang-v1_0`) has three Spanish voices, and the guide, the shopkeeper and the villager have them. The four NPCs of the
village (D-game-22 (c)) take provisional voices (D-game-22 (d)): their lines are recorded in them when the NPCs' files come (V10 to V12), and **the owner
listens to this trial afterwards**. Every file here says one line in one candidate voice, the female candidates "Hola. Me llamo Luz. Tengo diez panes.
¿Cuántos quieres?" and the male ones "Hola. Me llamo Mar. La panadería está a la izquierda de la torre.", all through the Spanish phonemizer (the
renderer's language is `es` whatever the voice). The speaker index is the voice's place in the model's own list (`model.onnx`, its `speaker2id`).

| File | Voice | Speaker index | Speed | Provisional choice for |
| --- | --- | --- | --- | --- |
| `if_sara-1.0.wav` | `if_sara` (Italian, female) | 35 | 1.0 | the baker, Luz |
| `im_nicola-1.0.wav` | `im_nicola` (Italian, male) | 36 | 1.0 | the smith, Domingo |
| `pf_dora-1.0.wav` | `pf_dora` (Portuguese, female) | 42 | 1.0 | |
| `pm_alex-1.0.wav` | `pm_alex` (Portuguese, male) | 43 | 1.0 | |
| `pm_santa-1.0.wav` | `pm_santa` (Portuguese, male) | 44 | 1.0 | the farmer, León |
| `ff_siwis-1.0.wav` | `ff_siwis` (French, female) | 30 | 1.0 | |
| `ef_dora-0.9.wav` | `ef_dora` (Spanish, female; the guide's) | 28 | 0.9 | |
| `ef_dora-1.1.wav` | `ef_dora` | 28 | 1.1 | |
| `em_alex-0.9.wav` | `em_alex` (Spanish, male; the shopkeeper's) | 29 | 0.9 | |
| `em_alex-1.1.wav` | `em_alex` | 29 | 1.1 | |
| `em_santa-0.9.wav` | `em_santa` (Spanish, male; the villager's) | 53 | 0.9 | the fisher, Mar |
| `em_santa-1.1.wav` | `em_santa` | 53 | 1.1 | |

The files are 16-bit mono WAV at 24 kHz, Kokoro's own rate (12 files, about 2.5 MB). Each NPC's slow recordings ("Más despacio") are 0.8 of its speed;
the trial has none. Measured when rendered: `if_sara` is about three times as loud as the Spanish voices (RMS 6 400 against about 2 000, peaks near full
scale), and `ff_siwis` and `im_nicola` about 1.7 times; the others are level with the Spanish three.

**Changing a voice.** A voice the owner rejects is changed in the NPC's file, `content/game-v1/game/npcs/<id>.json`, `voice` (`speaker`, `name`, `speed`),
and its lines are rendered again with `scripts/prerender-speech.sh`, which records every line of the NPC with its file's voice and skips the rest. Until the
NPC's file exists, its provisional voice is the row above (and the `npc` of its entry in `trial.json`); `docs/game/SPEECH_CHECKLIST.md` lists the
provisional voices with the speakers.

**Rendering the trial again.** `trial.json` holds the two lines and the twelve recordings (a file, a voice, its speaker index, a speed, the line, and for a
provisional choice the NPC and its name). Edit it and run, from the repository root:

    scripts/limited.sh dotnet run --project tools/Vocaterra.SpeechRender -- --trial docs/game/voice-trial

It needs the models folder (`VOCATERRA_MODELS_DIR`, default `~/.local/share/vocaterra/models`) and writes only the files `trial.json` names, here.
These files are not in the bundle and no test reads them.
