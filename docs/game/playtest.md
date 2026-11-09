# Playtest: the facilitator's script

The facilitator's checklist for the prototype's playtests (S12, `docs/game/design/prototype-brief.md`, `docs/game/scenario-plan.md` section 4 "S12"; refreshed
at S18 for the opening at the cliff camp, the guide's lesson, the longer run, the 31-item checks, the book and the keys F5, F6 and F8; refreshed at S28 for the book as a book, the lessons of sessions 2 and 3 and "Practise again"; refreshed at V09 for the village of sessions 4 to 6,
`docs/game/village-plan.md` phase A, which a pilot does not reach; and at V09 again for phase B, sessions 7 to 10, which a pilot does not reach either:
section 14). Read it
end to end before the first tester. It says what to recruit, what is logged, how a session runs, what to watch, how the next-day recall
works, how the two arms are run, which evidence answers which decision gate, and the owner's own pre-flight. Nothing here is a verdict on
learning: a small pilot gives directional evidence only (the brief).

**Provisional content trust (D-game-12).** Until a native Spanish speaker has checked the phrase sheet and the recordings, the content-trust
gate is **provisional**. The Spanish was reviewed by an LLM pass (`content/PHRASES.md`, `reviewer: llm-opus (provisional)`) and the owner
listened to the recordings (`docs/game/speech-checklist.md`). **Tell every tester:** "The Spanish in this game has not been checked by a native
speaker yet." Put that sentence in the consent script too. Do not claim the Spanish is correct to a tester.

**The review chain (D-game-12, S23).** Before a build goes to a tester the content runs lint `--strict`, `cefr-check` (A1 and A2), the grammar
check (LanguageTool over every Spanish string, from the committed cache, `docs/game/content.md`), then the Opus pass. Each step is supplementary: a clean
grammar run is not native-speaker approval, and `reviewer` in `content/PHRASES.md` does not change because of it.

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
- **Reports (F8, S21, D-game-17).** If you press F8, the game asks you what the problem or the wish is, and saves a report to the session
  folder: a Markdown file and a picture of the screen as you saw it. The report also holds the game's state (the build's commit, the content bundle's hash, the seed, where you stood, your
  objective and lesson step, the settings), **the last ten things you said or typed** (the words as the game heard or read them, so your voice
  is not kept, only what it turned into), and the last 200 lines of the session log. Nothing is sent: the files stay in the folder until you
  give it to the facilitator. Tell the tester: "Press F8 at any time to report a bug or ask for a feature; Esc cancels and keeps nothing."
- **The book (J, or the book item on the hotbar, S20, S28).** The book is an open two-page book with a ribbon for each page. It shows the nouns, verbs and phrases the game has seen you use, the lessons and
  what you said; each verb has a conjugation table of the forms you said and the forms the lessons taught. A lesson you finished has a "Practise again" button. It keeps **every line you said or typed** in the run (as the game heard or read it, not your voice), and the words those lines
  used; the save (`playtest.save`) keeps the newest 500 of those lines. Nothing is sent.
- You can stop at any time (Esc closes a dialogue; you can also say so and the facilitator ends the run). You can say no to giving the folder
  to the facilitator; if you say no, the facilitator does not look at it, and you can delete it.
- The Spanish has **not been checked by a native speaker yet**.

Record consent in the observer notes (yes or no, the time, the code). A tester who does not consent does not play.

## 3. Setup

**The build folder** (the usual route for testers; a private linux-x64 folder):

    scripts/playtest-build.sh <a new folder outside the repository>

The script validates the bundle, publishes the game self-contained (no .NET install for the tester), copies the bundle with its 1 382 speech
WAVs (300 at S18; 680 after phase A of the village plan, which brought the four new NPCs' lines and the lessons of sessions 4 to 6; 902 after phase B, which brought
the lessons of sessions 7 to 10; 1 062 after the follow-ups, the feedback lines in each NPC's voice and the free talk's replies; 1 382 on 2026-11-09, the count in `speech-checklist.md`), copies the pinned Vosk model from the shared models folder (`VOCATERRA_MODELS_DIR`, default `~/.local/share/vocaterra/models`; fetch it with
`scripts/fetch-models.sh` first) and its manifest, copies the tester's README and the launcher, then checks the folder: no sherpa-onnx or
ONNX Runtime native library, no espeak-ng, no sherpa model file, the Vosk model matching its pinned SHA-256 sums, and every speech WAV the
bundle manifest lists. The build must end with `all checks passed`. Keep its output with the build folder.

The build also stamps its commit (`VOCATERRA_BUILD_COMMIT`, set by the script to `git rev-parse HEAD`): every F8 report names it in its game
state table as "Build commit". A build made from a tree with uncommitted changes names the commit it was made from, not those changes; the
facilitator records the build folder's commit in the observer notes.

The tester runs `./playtest.sh` in that folder. The launcher sets `VOCATERRA_MODELS_DIR` to the folder's own `models/`. If the folder has no
game in it (not built), the launcher says so and asks for `scripts/playtest-build.sh` first; it does not start.

**A checkout** (for the owner or a developer): `dotnet run --project src/Vocaterra.Game -- --playtest` from the repository root, with
`VOCATERRA_MODELS_DIR` set. The developer build also carries Kokoro (`--speak`), which the playtest build does not.

**Fresh clone.** A clone needs the .NET 10 SDK, the speech models (`scripts/fetch-models.sh`) and `dotnet build Vocaterra.sln`. The
README's build steps are the reference; S12's fresh-clone check is in `docs/game/building-and-testing.md`.

**Before the tester sits down:** the microphone works (the mic check will tell you), the screen is at least 1280 x 720, and the session folder
root is empty of old runs you do not want mixed in (each run makes its own folder anyway).

**Freeze the bundle first.** A playtest run is **strict** (X01: `--playtest` implies `--strict-save`): its save refuses another bundle hash, with a message that
names both hashes, and does not load. Outside a playtest a save of an older bundle is carried into the new one (progress kept where its ids exist, the rest
dropped and logged), which is right for a player and wrong for a recall check: the recall battery assumes the bundle the tester learned from. Do not load a
tester's save without `--playtest` to get round the freeze; the recall session must be `--playtest --load`, on the same bundle. Before the first tester, note the bundle hash
from `content/game-v1/manifest.json` (`bundleHash`) and do not change `content/game-v1` until the last next-day recall is done. The
hash recorded at S18 (the refresh, 2026-10-22) is `393548666329ed76cac52e5d37dbb249285a37b6e12314b23a0df735fdfdeb98` (bundle `game-v1`,
300 speech files, 20-item battery). The owner's freeze is the hash in the owner's observer notes; if `manifest.json` shows another hash when
you build, that is a content change since S18 and the notes say so. Write the hash you froze in your observer notes header. A content change
means a new build and no recall for testers who played the old one.

**The S18 build** (recorded 2026-10-22, `scripts/playtest-build.sh`, all checks passed): 602 files, 199.7 MiB (S12: 519 files, 193 MiB; the
difference is the S15 to S21 content and the 74 further speech WAVs). Its F8 reports name the build commit.

## 4. The session

Plan 45 to 55 minutes in total: consent (3), the mic check (1), the pre-check (about 7, 31 items), the run (about 15 to 25, the guide's lesson
included), the post-check (about 7, 31 items), the thank-you and the debrief (10). These hold for a pilot that plays the first session after the
village grew (village-plan phase A): the first session's lesson and route are unchanged, and the scripted playtest (`docs/game/scripted-runs.md`, r10)
took the same ticks and asked the same 31 items before and after it (V09).

1. **Start** the build (`./playtest.sh`, or `./playtest.sh --greyscale` for arm B, see section 9). Confirm the session folder is named with
   today's date. Say: "Nothing here is a test of you; the game is being tested." Say: "Press F8 at any time to report a bug or ask for a
   feature; Esc cancels and keeps nothing."
2. **Mic check** (shown first). The tester says "hola" when asked. If it does not pass, the tester may retry or skip (Esc). Skipping is fine;
   note it. The mic check never blocks the game.
3. **Pre-check** (automatic after the mic check; "Check: before the game"). **Thirty-one items**: sixteen hear a Spanish line and choose one of three
   English meanings (1, 2, 3; R hears it again); fifteen say a Spanish phrase for an English situation (type with W, or hold V to speak, Enter
   sends, N means "no sé"). The bank has 87 items since the village lessons came (eight for each of sessions 4 to 10, village-plan V10 to V15), but a battery asks only the items of the
   lessons its play session has unlocked, and always those of sessions 1 to 3 (`coreSessions: 3` in `game/checks.json`, V07): the pre-check, the
   post-check and the next-day recall of a pilot (play sessions 1 and 2) are the same 31 items as before. The 56 items of sessions 4 to 10 (24 of phase A, 32 of phase B) are not in them. Say before it starts: "Some items are hard on purpose. Guess, or say 'no sé'. There is no score for you." Do not
   help with an item. Esc ends the check (the rest is skipped; note it).
4. **The opening, at the player's own camp on the cliff.** The tester wakes alone there; no quiz. The English tips come one at a time: look,
   walk, jump, a note at the tent (its one Spanish word, `camino`, says the guide's camp is down the trail), collecting and the inventory. The
   objective reads `Go down the trail and find the guide`, and the beacon points down the trail to her camp. Say only: "Please go on as you
   would in a game. Talk out loud if you can." Do not explain the controls: the tips do that. Note whether the tester reads them.
5. **The guide's lesson** (about 5 to 8.5 minutes, the walk down the trail included). Luna, the guide, starts the lesson by herself when the
   tester comes within five blocks of her; Esc closes the panel and the lesson waits. Seven units, 37 steps: hello; yes and no; what is this;
   please and thank you (she offers fruit: the tester picks it from a bush); getting unstuck (`No sé`, `Otra vez, por favor`, `Más despacio,
   por favor`); the camp's task. The English under her lines thins out as phrases come back. The lesson ends with a recap that opens the
   book's phrases page. Note the minute the lesson ends and whether the tester could say what the recap showed.
6. **The run** (about 10 to 15 minutes after the lesson). The objective is the camp: five wood, three fruit, a torch (made at the workbench and
   placed on the rim of the guide's fire ring) and water in the bottle (filled at the river; the beacon points there). The people are a
   shopkeeper at the stall (`¿Qué necesitas?`), a villager on the path, and the guide, who after the camp makes her transfer offer
   (`¿Quieres agua?`). Four more stand in the world since the village grew (village-plan phase A): the fisher on the bridge's east landing, on the
   road to the village, the baker at the bakery, the smith at the smithy and the farmer at the field. They answer when talked to, but their lessons
   and orders start in sessions 4 to 7 (sessions 8 to 10 have lessons and no order): the objective never points at them and nothing in the first session needs them (a tester who asks the baker
   for bread hears `Necesitas más monedas.`: there are no coins before session 4). Note it if a tester spends time with them. The tip `Press E to talk to someone.` may now show on the road, when the crosshair passes the fisher, rather than at the stall. The ending: the tester takes the spare torch back up the trail and places it on the rim of the fire ring at their own
   camp; the fire lights, and the screen says `Session complete`. Say only: "Please go on as you would in a game." Observe (section 5). Note each time the tester asks you something; answer the game questions only with "I cannot help with the
   Spanish, but you can ask the game for help: press H, or N for 'no sé'".
7. **The rescue rule.** The game never needs you. If the tester is stuck on the **same step for more than 5 minutes** (no progress in the
   objective, the journal, or the dialogue, and no attempt), you may help: first with the place (point at the next thing, "the river is
   that way"), then, if still stuck after one more minute, with a Spanish word or phrase from the card on screen. Write `rescue` and the
   minute in the notes. Why 5 minutes: the run is 15 to 25 minutes, so five minutes on one step is a large part of it; a shorter rule would
   rescue the struggle the study is about, and a longer one would let a stuck player sit for the whole session. A rescue lowers the Playable
   evidence, and the report counts it only in your notes.
8. **The post-check.** It starts by itself once the camp is complete and the guide has made her offer ("¿Quieres agua?"), and the tester has
   closed her panel. If the tester never gets there and the time is up, press **F10** ("end the run (facilitator)", top left, clear of the dialogue). Write
   `F10` in the notes with the time. The post-check is **thirty-one items** of the same kinds as the pre-check (sixteen and fifteen), in another order.
9. **Save and thank-you.** The post-check ends with "Thank you for playing", the line "Saved: playtest.save in the session folder ...", and
   "Esc: close the game". The tester presses Esc. The game is then closed, and the folder holds `session.jsonl`, `replay.jsonl` and
   `playtest.save`, and `reports/` when the tester pressed F8 (each report is a `report-s<session>-t<tick>-<bug|feature>.md` with its
   `.png`). If the tester pressed F6 during the run, `playtest.save` is overwritten by the save at the end (F6 writes the session folder's
   `playtest.save`, not a file in the build folder). If the run ended before the post-check (F10 or closing the window), the save is still
   written at the quit. During the run the game also writes `playtest.save` by itself (X05, the autosave) at each lesson step, objective step, lesson end
   and closed dialogue, and every five minutes of play, so a crash in the middle loses at most those minutes (section 10, crash reports).
10. **Collect the reports** (see section 10): after each session, with the tester's agreement, run `scripts/playtest-report.sh --copy <collection
    folder> <session folder>`. Read each report's kind and title in the printed summary; note them in the observer notes.
11. **Debrief** (see section 6). Ask the questions in the notes template before you show the report.

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
- **The opening and the lesson:** whether the tester read the tips; how long the walk to the guide took; the minute the lesson started and
  ended; whether the tester used the English under her lines or only the audio.
- **The book:** whether the tester opened it (J, or the book item on the hotbar), which page (the ribbons at the top turn the pages), and whether they used the phrases page after the
  lesson's recap. Whether they found a verb's table on the Verbs page (the highlighted forms are the ones they said). Note the words they named.
- **Reports:** whether they pressed F8, what they wrote, and whether it was a bug or a feature. Do not read a report aloud to them.
- **Keys they did not find:** F5 (the view over the shoulder), F6 (save), F8 (report), and J (book), in addition to the controls listed above.

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
Bundle hash (frozen):   393548666329...   Build commit: ____ (arm A colour / arm B --greyscale)
Consent (yes/no, time): ____            Beginner screener (answer, no names): ____________
Microphone: works / no mic (typed)   Mic check: passed / retried / skipped
Pre-check (31 items): done / Esc at item __   Post-check (31 items): automatic / F10 at minute __
Opening: tips read yes/no; lesson started at minute __, ended at minute __ (walk included)
Camp complete: yes/no, at minute __     Rescues: __ (minute, what was given)
Stuck (more than 5 min on one step): ____________________________
Book: opened yes/no, page ____ (J or the book item)   Keys not found: ______
Friction: ______________________________________________________
Retries and help used: ______________________________________________
Quotes (with minute): _____________________________________________
Desire to continue (their words): __________________________________
Why Spanish was needed (their words): _______________________________
Enjoyable (1 to 5, the observer's): __  Would play another part? yes/no/unsure
Reports (F8, the tester's): __ (kind and title, from playtest-report.sh)
Notes after the session: _____________________________________________
```

## 8. The next-day recall

The recall needs no time skip: the game counts a new play session on load, which lifts the spacing gaps, and the battery is outside the world.
Run it the next day, or later, on the same build:

    ./playtest.sh --load <session folder>/playtest.save

The playtest profile implies `--check recall` on a load: the mic check (skip if you wish), the recall battery (thirty-one items, the same kinds as
the pre- and post-checks, another order), then the thank-you screen, and the game ends on Esc. It saves nothing. Its log goes into a new
session folder (`~/.local/share/vocaterra/sessions/...`). Use the same build folder and the same bundle: a save made against another bundle
is refused (the playtest is strict, X01; a plain `--load` would migrate it instead, and a migrated save is not a recall of what the tester learned).

From a checkout: `dotnet run --project src/Vocaterra.Game -- --playtest --load <session folder>/playtest.save`.

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

It needs bash and Python 3 (standard library). It prints, per folder and in total, the profile and arm, completion, **the measured durations**
(whole session, lesson, walk, each check; read them here for the section 4 and 6 estimates), retries, support used,
ASR latency and failures, the pre, post and recall checks by item kind, transfer uses, "found without Spanish", and one evidence line per
decision gate. Under each folder it lists that session's bug and feature reports (the kind and the title). Paste its output into the session's
notes, next to the observer notes. Keep the report and the folders together.

To collect what the facilitator takes away, add `--copy <dir>`:

    scripts/playtest-report.sh --copy <a collection folder> <session folder> [<session folder> ...]

It writes `<collection folder>/<session folder name>/summary.txt` (the printed summary of that folder) and `reports/` with every report and its
picture. Give the collection folder with the observer notes; the session folders stay with the tester's machine unless the tester agreed.

### Crash reports (X05, for the facilitator)

If the game stops with an error, it does not just close. The game writes a Markdown **crash report** into the session folder's `reports/`, beside the
F8 reports, named `crash-s<session>-t<tick>.md` (or `crash-<date and time>.md` when the game could not read its state). It holds the error and its stack,
the game's state when it stopped (the bundle, the seed, where the player stood, the objective and the lesson step, whether a dialogue was open), the
newest 200 lines of the session log, and the build's commit. It also tries a **last-chance save**, `crash.save` in the same folder, when the game was at
rest. The autosave (`playtest.save`) and any save the tester made are never overwritten by it. The console then prints both paths and the game exits with
code 1.

- A crash report contains **no audio**. Its log lines hold what the tester typed or what the recogniser heard, as the session log does.
  Nothing is sent: the files stay in the folder until you collect them.
- `scripts/playtest-report.sh` lists the crash reports under each folder with the F8 reports (kind `Crash`), and `--copy` takes them too.
- What to do: note the time, what the tester was doing, and the report's name in the observer notes. Keep `crash.save` with the folder. A crash during a
  dialogue leaves no last-chance save, so the autosave (from the last closed dialogue or lesson step) is the one that holds the progress. A playtest
  `--load` of a save is the next-day recall (section 8), not a resume: do not use it to carry on a run that crashed; note the crash instead.

## 11. The decision gates and the evidence that answers them

From the brief (`docs/game/design/prototype-brief.md`). The report gives evidence lines; the decision is yours and the owner's, with the notes.

| Gate | Criterion (the brief) | Evidence that answers it | Where |
| --- | --- | --- | --- |
| Playable | Players finish without developer rescue; ASR failure never traps progress | Camp complete without a rescue (your notes, step 4.5); failed turns and timeouts; that no mic failure stopped the objective | Report "Completion", "failures"; notes "Rescues" |
| Enjoyable | Players understand why they needed Spanish and show interest in another scenario | The debrief answers (questions 3 and 5); observer notes "Why Spanish was needed" and "Would play another part"; comments | Observer notes only: the script cannot measure it |
| Learning signal | Players independently recall or understand several phrases after support is reduced | Post-check against pre-check, by kind (production and comprehension); the recall check the next day; support used (cards, Repetir, Ayuda, No sé) during the run | Report "Checks pre/post/recall", "Support used" |
| Transfer | At least some target language works with a different NPC or changed context; a next-day check is preferable | Transfer uses (the guide's "¿Quieres agua?" answered after the camp); the villager and shopkeeper phrases in the post-check; the next-day recall | Report "Transfer uses", "Found without Spanish", the recall lines |
| Content trust | Every prototype phrase, translation, accepted response and recording is checked together before testing | Provisional: the LLM review and the owner's listening pass (`content/PHRASES.md`, `docs/game/speech-checklist.md`); a native speaker's check is not done | Status stays **provisional** (D-game-12) until a speaker checks it |

Read the gates separately, as the brief asks: a recall check that is weak with a fun game means improve the learning loop before the world;
a fun game with weak recall means change how the practice is built into the objective. Transcription accuracy is not learning.

## 12. The owner's pre-flight

Before any tester (the owner, once, end to end):

1. Build the folder with `scripts/playtest-build.sh`; keep the output; it must end with `all checks passed`.
2. Play one full session in the build folder as a beginner would: `./playtest.sh`, the mic check (or skip), the pre-check (31 items), the
   opening at the cliff camp, the guide's lesson (about 5 to 8.5 minutes), the camp, the stall, the villager, the river, the guide's offer, the
   ending (the torch at your own fire ring), the post-check (31 items, or F10), the thank-you (Esc). Also press J (the book), F5 (the view),
   F6 (save) and F8 (a report, Ctrl+Enter to save). Note any control you had to guess. The game measures the time of each part; do not time
   it by hand: `scripts/playtest-report.sh <folder>` prints, under "Durations", the whole session, the lesson, the walk (from the end of the
   lesson to the camp's completion) and each check, from the wall-clock times of the session log (the `lesson_started`, `lesson_completed`,
   `check_start`, `check_end` and `session_end` lines). The first real run replaces the estimates in sections 4 and 6 with these numbers.
3. Run the recall on the session's `playtest.save`: `./playtest.sh --load <folder>/playtest.save`. Check that the thank-you says "Thank you".
4. Run `scripts/playtest-report.sh` on the two folders and read every line; then collect the owner's own F8 report with
   `scripts/playtest-report.sh --copy <collection folder> <folder>` and check its "Build commit" line.
5. **Listen to every speech WAV** (1 382 on 2026-11-09; no row is marked yet) and mark each row of `docs/game/speech-checklist.md` `ok`, `cut-off` or
   `mismatch` (D-game-12, step 4). The four village voices are provisional (D-game-22 (d)): the voice trial that compared them was deleted on 2026-11-03
   and is in git history (`git log -- docs/game/voice-trial`); the
   checklist's preface says how a rejected voice is recorded again. A tester in the first session hears those NPCs only if they talk to them.
   The owner's rows decide whether a file is played to a tester; a `mismatch` or `cut-off` is a bundle change and a new build.
6. Check that the thank-you and the pre- and post-check text still match this document (the English is for the tester and the facilitator).
7. Freeze the bundle (section 3) and note its hash.

## 13. Known limits (to read out or to note)

- The microphone path and the real keys (E, V, Esc, F5, F6, F8, F10, J) were exercised in the scripted runs only; the S18 smoke runs (a
  scripted route through the built folder, with the pre-check, the lesson, the camp and the post-check answered by script) reached session
  complete and saved, but a scripted run loads no recogniser, so the speech path is not checked by them. Real use with a microphone and a
  keyboard is the owner's step 2 above.
- The lessons of sessions 2 to 10 (S26 and S27: "Personas" at the villager, "Ayer y mañana" at the villager and the shopkeeper; village-plan V10 to V12:
  "El pueblo" at the fisher, "Números y pan" at the baker, "Gente del pueblo" at the villager, the smith and the farmer; phase B, V13 to V15: "¿Cómo es?" at
  the farmer, "Ayer en el pueblo" at the villager, "Mañana en el pueblo" and "El mercado" at the baker) are in the build, but a
  `--playtest` load runs the recall battery and then ends (section 8), so a tester does not reach them in a playtest. Only the first session's lesson, the
  guide's, is part of the playtest, and the village lessons' check items are outside the pilot's battery (section 4, step 3). The book's Lessons page lists
  all ten, and a finished lesson has a "Practise again" button.
- The playtest build has no Kokoro: the NPCs speak with the pre-rendered WAVs; a live spoken line of the facilitator's (`--speak`) is a
  developer option only.
- The build is private. Its native code is the one of core D2 (sherpa-onnx's native library is left out; the managed wrapper stays). Do not
  pass the folder on.
- The Spanish is not checked by a native speaker (the provisional note at the top of this document).

## 14. A playtest of sessions 7 to 10 (what it would need)

Not planned: written at V09 after phase B of the village plan and checked at V24 after the follow-ups, so the owner can weigh one. A pilot plays the first session only (section 13).


**What changed for a playtest since phase B** (the follow-ups, V20 to V25; the playtest's own build is the same):

- Free talk takes the lessons' lines: a learner who says a line of the lesson to its NPC gets a reply, and the phrase counts as a use (V20).
- The Lessons page opens at the `next:` lesson, so the page a tester reaches first names the lesson to talk to (V21b).
- Two session counters: the day (lessons, objectives, the HUD, the book) and the sitting (the spacing rule, the review deck, the session log); a load
  keeps the day and sets the sitting on (V25). A tester who saves in session 5 and loads sees session 5 again.
- The accent note: a verb typed without the accent of its tense (`ayer compre pan`) is accepted, and a note under the reply names the accented form, in free talk as in a lesson (M70, V21b).
- The name plates show only for the NPC the tester faces and the beacon's NPC (V22), and the stall and the market answer what was said (V21, V21b).
- **A way in.** `--playtest` plays one session a run and offers no sleep at the fire, and a `--load` of its save runs the recall battery and ends;
  `--skip-to-session`, the test aid that starts a later session, refuses `--playtest`. A playtest of sessions 7 to 10 needs a profile that starts a tester at
  a later session (its saves and logs marked as such, as the skip's are), or sittings that allow the sleep. That is a game milestone, not a facilitator's
  step.
- **Testers who know A1.** The four lessons are `a2` and build on sessions 1 to 6 (numbers, prices, the three times, the people and their names). Recruit
  testers who have A1 Spanish, or have them play sessions 4 to 6 first.
- **The battery.** A battery asks 63 items in session 7 and 87 in session 10 (section 4, step 3): about 14 and 20 minutes at the pilot's pace (31 items in
  about 7). That is too long beside the lessons. The 32 items of phase B (eight a lesson, by `lesson` in `game/checks.json`) need a battery of their own, or a
  smaller fixed core than `coreSessions: 3`, decided before the bundle is frozen.
- **The time.** A lesson takes about 6 to 10 minutes (the milestones' estimates: session 7 and 8 about 7 to 9, session 9 about 6 to 7, session 10 about 9 to
  10), plus the walks and the free talk: about 40 minutes for the four, before any check.
- **The voices.** None of the speech files (1 382 on 2026-11-09) has been heard (section 12, step 5). Phase B's 222 are 70 the farmer's, 54 the guide's (the lessons' models
  and cards), 30 the baker's, 22 each the smith's and the villager's, 14 the fisher's and 10 the shopkeeper's; the follow-ups' 148 (74 lines) are the
  villager's 26 files, the farmer's 26, the shopkeeper's 26, the baker's 24, the smith's 24 and the fisher's 22: the NPCs' free-talk replies and the
  feedback lines (`answer_accepted`, `not_understood`, `feedback.hint`, `hint.gender.agree`, `slow_repeat`) in each NPC's voice (V20, V21b).
- **What to watch.** At each session's start, whether the objective and the beacon are followed to the lesson's NPC (the Lessons page opens at `next:`,
  `next: talk to Luz` in session 9); the walks inside a lesson (session 7 to the river and back, 8 and 9 from the village to the river, 10 from the bakery to
  the field and the stall); the market's coins (wood sold, bread or apples bought: a tester who spends the coins elsewhere sells more wood); the free talk
  after a lesson, where a line the lesson taught is understood by its NPC (V20: all 11 in the route of V24) and a line it did not teach is understood only
  when its intent is one that NPC takes (6 of 11 in the route); the accent note under the reply (`ayer compre pan`); and the session counters: a
  load keeps the day, a sleep moves it, and a save made after a sleep loads on the new day (V25).
