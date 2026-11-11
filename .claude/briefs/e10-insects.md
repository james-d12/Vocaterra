# Brief: E10, the insects without the ringing

Sent 2026-11-11 to a general-purpose agent, Sonnet 5.5 at medium effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/e10`,
branch `E10-insects`, cut from `feature/weather`. The shared part is `.claude/briefs/e-series-standard.md`.

```
You are implementing E10: the evening insects' sound, which the owner heard as "a high pitched constant ringing sound when I was heading
down the path to the guide".

The cause (found by the orchestrator, 2026-11-11): `AmbientLoops.Insects` (src/Vocaterra.Game.Core/Sound/AmbientLoops.cs) is two pure
sines, 4700 Hz and 3900 Hz, amplitude-pulsed at 17 and 23 Hz with no rest, plus a trace of noise: a continuous whine. Until E03 it only
played with `--dusk`; since E03 its weight is `lowland x (1 - daylight)` (AmbientBeds.cs), so it plays every dawn, dusk and night, and
it rises as the player walks down from the camp into the valley. A scripted run (`--seed 42 --time 21 --talk guide --sound-log`) shows
`insects` going from 0 to 0.5 on that path; at night it reaches 1.0.

What to build:
1. A new `Insects` synthesis that sounds like crickets in a field, not a tone:
   - several voices (four to six), each a cricket: chirps made of three or four short pulses (about 10 to 25 ms each, a soft attack and
     decay so nothing clicks) at that voice's own carrier, the carriers spread over roughly 2.8 to 4.2 kHz (lower than now: less
     piercing), a voice's carrier wandering a little (a few Hz) from chirp to chirp;
   - each voice chirps one to three times a second with its own jittered rhythm from `PresentationRng` (deterministic, seeded as now),
     with a per-chirp level variation, and real silence between chirps, so the bed never holds a steady pitch;
   - voices at different levels and so at different apparent distances; a very quiet low-passed noise bed under them (the night air),
     no louder than the present `grass` term.
   Keep the loop machinery as it is (`Render`, `Seamless`, the 10 s loop, the normalisation to `Peak`).
2. Its level: keep `AmbientBeds.Gain(Insects)` (0.3) unless the new loop's RMS differs from the old one's by more than 2 dB; if it does,
   change the gain so the RMS at the bed is within 1 dB of the old one, and say so. Measure both.
3. Tests (Game.Core.Tests, mirroring the source): (a) the loop is seamless and deterministic (if a general test already covers every
   loop, make sure it runs on the new one); (b) "no steady tone": split the loop into 20 ms windows; at least 40 percent of them are
   quiet (RMS under a tenth of the loop's peak window); (c) "no single pitch": over the whole loop, no 50 Hz-wide band between 2 and 6 kHz
   holds more than a third of the energy between 2 and 6 kHz (a plain DFT or Goertzel over the loop is fine in a test). Check that (b)
   and (c) FAIL on the old synthesis before you replace it (say so in the report), so they really guard the bug.
4. Write the old and the new loop as 16-bit mono WAV files for the owner to listen to, to
   /tmp/claude-1000/-home-james-Dev-Vocaterra/749a3cc3-e476-4312-9dfe-129a441f408b/scratchpad/e10/insects-old.wav and insects-new.wav
   (from a throwaway program or a test run you do not commit; the folder may need creating).
5. docs/game/sound-and-display.md and the Sound section of src/Vocaterra.Game.Core/README.md: one or two sentences on how the insects
   are made now.

Out of scope: when the insects play (the weights in AmbientBeds and their tests stay as they are), every other bed, the mixer, the
volumes (another agent, E09, is adding a Weather volume slider in another worktree at the same time: do not touch PlayerSettings,
SoundMixer, SoundVolumes or the menu), the content bundle.

The gate is the standard brief's (11094 passed, 3 skipped on `feature/weather`). Report: the standard one page, the old and new RMS and
the gain you kept or set, whether (b) and (c) failed on the old loop, and the two WAV paths.
```
