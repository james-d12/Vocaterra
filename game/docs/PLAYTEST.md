# Playtest: the facilitator's script

The facilitator's checklist for the prototype's playtests (S12, `docs/PROTOTYPE_BRIEF.md`, `docs/SCENARIO_PLAN.md` section 4 "S12"). Read it
end to end before the first tester. It says what to recruit, what is logged, how a session runs, what to watch, how the next-day recall
works, how the two arms are run, which evidence answers which decision gate, and the owner's own pre-flight. Nothing here is a verdict on
learning: a small pilot gives directional evidence only (the brief).

**Provisional content trust (D-game-12).** Until a native Spanish speaker has checked the phrase sheet and the recordings, the content-trust
gate is **provisional**. The Spanish was reviewed by an LLM pass (`content/PHRASES.md`, `reviewer: llm-opus (provisional)`) and the owner
listened to the recordings (`docs/SPEECH_CHECKLIST.md`). **Tell every tester:** "The Spanish in this game has not been checked by a native
speaker yet." Put that sentence in the consent script too. Do not claim the Spanish is correct to a tester.

## 1. Who to recruit

- **Beginners with little or no Spanish.** This is the audience the prototype promises to help (the brief's audience decision). Ask in the
  screener: "Which Spanish words or phrases do you know?" Someone who already says "¿Dónde está…?", "quiero…" or "¿Cómo estás?" with confidence
  is **not** a beginner for this study: their success does not validate the beginner promise. "Hola", "gracias" and "sí" alone are fine.
- Aim for people who have played few games of this kind and have not used a Spanish course in the last year, if you can find them. Note
  each tester's answer to the screener (a number, not a name) in the observer notes.
- Ask whether they have a Linux computer with a screen, keyboard and mouse. A microphone is wanted but not required: a tester without a
  microphone types every answer, and that is a valid session.
- Give each tester a code (P01, P02, ...). Notes and the session folder use the code only.

## 2. Consent and what is logged

Read this to the tester, in your own words, before the session:

- The game records what you do and the answers you give, **on this computer**, in a folder named by the date and the time. Nothing is sent
  anywhere, and nobody can read the folder unless you give it to the facilitator.
- The log holds: the text you typed, or the text the speech recogniser heard from what you said (the **microphone audio is not written to
  disk**); what the characters said back; how long each answer took; the quiz answers; the steps of the objective; and whether you asked for
  help, repeated a line, or said "no sé". The game also saves its state at the end (`playtest.save`).
- You can stop at any time (Esc closes a dialogue; you can also say so and the facilitator ends the run). You can say no to giving the folder
  to the facilitator; if you say no, the facilitator does not look at it, and you can delete it.
- The Spanish has **not been checked by a native speaker yet**.

Record consent in the observer notes (yes or no, the time, the code). A tester who does not consent does not play.

## 3. Setup

**The build folder** (the usual route for testers; a private linux-x64 folder):

    scripts/playtest-build.sh <a new folder outside the repository>

The script validates the bundle, publishes the game self-contained (no .NET install for the tester), copies the bundle with its 226 speech
WAVs, copies the pinned Vosk model from the shared models folder (`VOCATERRA_MODELS_DIR`, default `~/.local/share/vocaterra/models`; fetch it with
`nlp/scripts/fetch-models.sh` first) and its manifest, copies the tester's README and the launcher, then checks the folder: no sherpa-onnx or
ONNX Runtime native library, no espeak-ng, no sherpa model file, the Vosk model matching its pinned SHA-256 sums, 226 speech files. The build
must end with `all checks passed`. Keep its output with the build folder.

The tester runs `./playtest.sh` in that folder. The launcher sets `VOCATERRA_MODELS_DIR` to the folder's own `models/`.

**A checkout** (for the owner or a developer): `dotnet run --project game/src/Vocaterra.MonoGame -- --playtest` from the repository root, with
`VOCATERRA_MODELS_DIR` set. The developer build also carries Kokoro (`--speak`), which the playtest build does not.

**Fresh clone.** A clone needs the .NET 10 SDK, the speech models (`nlp/scripts/fetch-models.sh`) and `dotnet build Vocaterra.sln`. The
README's build steps are the reference; S12's fresh-clone check is in `game/README.md`.

**Before the tester sits down:** the microphone works (the mic check will tell you), the screen is at least 1280 x 720, and the session folder
root is empty of old runs you do not want mixed in (each run makes its own folder anyway).

**Freeze the bundle first.** A save refuses another bundle hash (the save would not load). Before the first tester, note the bundle hash
from `game/content/game-v1/manifest.json` (`bundleHash`) and do not change `game/content/game-v1` until the last next-day recall is done. The
hash at the time of writing is `cd63077b9a8b0ee34a21d4cf963aa2afa8fa6ee6866827224b847cefd7c94f88` (bundle `game-v1`, 226 speech files). Write the
hash you froze in your observer notes header. A content change means a new build and no recall for testers who played the old one.

## 4. The session

Plan 35 to 45 minutes in total: consent (3), the mic check (1), the pre-check (about 4), the run (10 to 15), the post-check (about 4), the
thank-you and the debrief (10).

1. **Start** the build (`./playtest.sh`, or `./playtest.sh --greyscale` for arm B, see section 9). Confirm the session folder is named with
   today's date. Say: "Nothing here is a test of you; the game is being tested."
2. **Mic check** (shown first). The tester says "hola" when asked. If it does not pass, the tester may retry or skip (Esc). Skipping is fine;
   note it. The mic check never blocks the game.
3. **Pre-check** (automatic after the mic check; "Check: before the game"). Ten items: six hear a Spanish line and choose one of three English
   meanings (1, 2, 3; R hears it again); four say a Spanish phrase for an English situation (type with W, or hold V to speak, Enter sends,
   N means "no sé"). Say before it starts: "Some items are hard on purpose. Guess, or say 'no sé'. There is no score for you." Do not help
   with an item. Esc ends the check (the rest is skipped; note it).
4. **The run** (10 to 15 minutes). The world: a forest clearing with a workbench and a fire ring, a path, a village with a stall and houses,
   a river with a bridge. The objective is the camp: five wood, three berries, one torch at the fire ring, water in the bottle. The people are
   a shopkeeper at the stall, a villager on the path, and the guide at the camp. Say only: "Please go on as you would in a game. Talk out loud
   if you can." Observe (section 5). Note each time the tester asks you something; answer the game questions only with "I cannot help with
   the Spanish, but you can ask the game for help: press H, or N for 'no sé'".
5. **The rescue rule.** The game never needs you. If the tester is stuck on the **same step for more than 5 minutes** (no progress in the
   objective, the journal, or the dialogue, and no attempt), you may help: first with the place (point at the next thing, "the river is
   that way"), then, if still stuck after one more minute, with a Spanish word or phrase from the card on screen. Write `rescue` and the
   minute in the notes. Why 5 minutes: the run is 10 to 15 minutes, so five minutes on one step is a third of it; a shorter rule would rescue
   the struggle the study is about, and a longer one would let a stuck player sit for the whole session. A rescue lowers the Playable
   evidence, and the report counts it only in your notes.
6. **The post-check.** It starts by itself once the camp is complete and the guide has made her offer ("¿Quieres agua?"), and the tester has
   closed her panel. If the tester never gets there and the time is up, press **F10** ("end the run (facilitator)", bottom left). Write
   `F10` in the notes with the time. The post-check is the same kind of ten items as the pre-check, in another order.
7. **Save and thank-you.** The post-check ends with "Thank you for playing", the line "Saved: playtest.save in the session folder ...", and
   "Esc: close the game". The tester presses Esc. The game is then closed, and the folder holds `session.jsonl`, `replay.jsonl` and
   `playtest.save`. If the run ended before the post-check (F10 or closing the window), the save is still written at the quit.
8. **Debrief** (see section 6). Ask the questions in the notes template before you show the report.

## 5. What to observe

Write notes as you go (template in section 7). For each session:

- **Completion without rescue:** did the camp get completed; at what minute; how many rescues (step 4.5).
- **Friction:** where the tester stopped, looked lost, or asked "what now?"; controls they did not find (E to talk, V to speak, Esc, C for
  the workbench); the mic check's experience.
- **Retries:** how many times they said or typed the same thing again; whether they used R, S, H or N; whether they ignored the offered
  choices.
- **Comments:** what they said aloud, unprompted. Quote the words, with the minute.
- **Desire to continue:** ask at the end, "Would you play the next part of this?" and "What would you do next?". Write the answer as said.
- **Understanding of why Spanish was needed:** ask, "Why did you need Spanish in that part?" The Enjoyable gate asks for this.
- **Voice:** what the game heard (the "Heard / Expected" line) and whether the tester understood the difference.

Do not coach, do not praise a correct answer in Spanish, and do not correct a wrong one. Say "thank you" and move on.

## 6. The debrief questions

Ask after the thank-you, before any report is read:

1. What were you trying to do in the last five minutes?
2. Which words or phrases did you remember afterwards? (Let them name them.)
3. Did any of the Spanish feel like part of the game, or like a separate quiz? Why?
4. Was there a moment you wanted to give up? What happened next?
5. Would you play another part of this game? What would make you want to?

## 7. The observer notes template

Copy this into a file for each session (one file per tester; the code, not the name):

```
Tester code: P__        Date: ____        Session folder: ____________________
Bundle hash (frozen):   cd63077b...   Build: ____ (arm A colour / arm B --greyscale)
Consent (yes/no, time): ____            Beginner screener (answer, no names): ____________
Microphone: works / no mic (typed)   Mic check: passed / retried / skipped
Pre-check: done / Esc at item __        Post-check: automatic / F10 at minute __
Camp complete: yes/no, at minute __     Rescues: __ (minute, what was given)
Stuck (more than 5 min on one step): ____________________________
Friction: ______________________________________________________
Retries and help used: ______________________________________________
Quotes (with minute): _____________________________________________
Desire to continue (their words): __________________________________
Why Spanish was needed (their words): _______________________________
Enjoyable (1 to 5, the observer's): __  Would play another part? yes/no/unsure
Notes after the session: _____________________________________________
```

## 8. The next-day recall

The recall needs no time skip: the game counts a new play session on load, which lifts the spacing gaps, and the battery is outside the world.
Run it the next day, or later, on the same build:

    ./playtest.sh --load <session folder>/playtest.save

The playtest profile implies `--check recall` on a load: the mic check (skip if you wish), the recall battery (ten items, the same kinds as
the pre- and post-checks, another order), then the thank-you screen, and the game ends on Esc. It saves nothing. Its log goes into a new
session folder (`~/.local/share/vocaterra/sessions/...`). Use the same build folder and the same bundle: a save made against another bundle
is refused.

From a checkout: `dotnet run --project game/src/Vocaterra.MonoGame -- --playtest --load <session folder>/playtest.save`.

## 9. The A/B arm (`--greyscale`)

The world is in full colour by default (arm A). `--greyscale` (arm B) draws blocks whose noun the player does not know yet in grey; a block
named by two nouns (water: `agua` and `río`) regains colour when either is known (the orchestrator's water rule, S12). Arm B tests whether
the grey world changes what the player notices and tries.

- Alternate the arms in the order the testers come in: A, B, A, B, ... Write the arm in the notes header. The report prints the arm of each
  folder (`profile: playtest, arm B, greyscale`).
- Both arms run the same objective, quizzes and speech; the only difference is the colour of unknown blocks. The tester is not told which
  arm they have.
- Arm B is a question about attention, not about learning. Do not read it as a learning result from a few sessions.

## 10. The report

    scripts/playtest-report.sh <session folder> [<session folder> ...]

It needs bash and Python 3 (standard library). It prints, per folder and in total, the profile and arm, completion, retries, support used,
ASR latency and failures, the pre, post and recall checks by item kind, transfer uses, "found without Spanish", and one evidence line per
decision gate. Paste its output into the session's notes, next to the observer notes. Keep the report and the folders together.

## 11. The decision gates and the evidence that answers them

From the brief (`docs/PROTOTYPE_BRIEF.md`). The report gives evidence lines; the decision is yours and the owner's, with the notes.

| Gate | Criterion (the brief) | Evidence that answers it | Where |
| --- | --- | --- | --- |
| Playable | Players finish without developer rescue; ASR failure never traps progress | Camp complete without a rescue (your notes, step 4.5); failed turns and timeouts; that no mic failure stopped the objective | Report "Completion", "failures"; notes "Rescues" |
| Enjoyable | Players understand why they needed Spanish and show interest in another scenario | The debrief answers (questions 3 and 5); observer notes "Why Spanish was needed" and "Would play another part"; comments | Observer notes only: the script cannot measure it |
| Learning signal | Players independently recall or understand several phrases after support is reduced | Post-check against pre-check, by kind (production and comprehension); the recall check the next day; support used (cards, Repetir, Ayuda, No sé) during the run | Report "Checks pre/post/recall", "Support used" |
| Transfer | At least some target language works with a different NPC or changed context; a next-day check is preferable | Transfer uses (the guide's "¿Quieres agua?" answered after the camp); the villager and shopkeeper phrases in the post-check; the next-day recall | Report "Transfer uses", "Found without Spanish", the recall lines |
| Content trust | Every prototype phrase, translation, accepted response and recording is checked together before testing | Provisional: the LLM review and the owner's listening pass (`content/PHRASES.md`, `docs/SPEECH_CHECKLIST.md`); a native speaker's check is not done | Status stays **provisional** (D-game-12) until a speaker checks it |

Read the gates separately, as the brief asks: a recall check that is weak with a fun game means improve the learning loop before the world;
a fun game with weak recall means change how the practice is built into the objective. Transcription accuracy is not learning.

## 12. The owner's pre-flight

Before any tester (the owner, once, end to end):

1. Build the folder with `scripts/playtest-build.sh`; keep the output; it must end with `all checks passed`.
2. Play one full session in the build folder as a beginner would: `./playtest.sh`, the mic check (or skip), the pre-check, the camp, the
   guide's offer, the post-check (or F10), the thank-you (Esc). Note any control you had to guess.
3. Run the recall on the session's `playtest.save`: `./playtest.sh --load <folder>/playtest.save`. Check that the thank-you says "Thank you".
4. Run `scripts/playtest-report.sh` on the two folders and read every line.
5. **Listen to every speech WAV** and mark each row of `game/docs/SPEECH_CHECKLIST.md` `ok`, `cut-off` or `mismatch` (D-game-12, step 4).
   The owner's rows decide whether a file is played to a tester; a `mismatch` or `cut-off` is a bundle change and a new build.
6. Check that the thank-you and the pre- and post-check text still match this document (the English is for the tester and the facilitator).
7. Freeze the bundle (section 3) and note its hash.

## 13. Known limits (to read out or to note)

- The microphone path and the real keys (E, V, Esc, F10) were exercised in the scripted runs only; real use with a microphone and a keyboard
  is the owner's step 2 above.
- The playtest build has no Kokoro: the NPCs speak with the pre-rendered WAVs; a live spoken line of the facilitator's (`--speak`) is a
  developer option only.
- The build is private. Its native code is the one of core D2 (sherpa-onnx's native library is left out; the managed wrapper stays). Do not
  pass the folder on.
- The Spanish is not checked by a native speaker (the provisional note at the top of this document).
