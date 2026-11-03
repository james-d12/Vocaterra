# X00: a ranked improvement backlog (2026-10-23)

Research for the X series on `feature/experiment` (`docs/game/implementation-plan.md` section 4.5). Read-only: nothing was built, run or
changed. The evidence is the plans, the findings log (`implementation-plan.md` section 5), the decisions (`docs/game/decisions.md`,
`docs/nlp/decisions.md`), the playtest and README documents, the code (file and line where it matters) and earlier agents' screenshots
under `/tmp/claude-1000/` (s22, s20b, s28). Nothing was learned from a real tester: no tester session exists yet (the owner's pre-flight in
`docs/playtest.md` section 12 is still open), so every "effect" below is a hypothesis a playtest can check, not a measurement.

Limits kept: Spanish speech with English support text (D-game-13), offline with local reports (D8 open, D-game-17), CC0 assets
(D-game-5), MonoGame (D-game-1), no German (D14), no automated CEFR placement or global score (D-game-10), no gating behind language or
voice (D-game-8, D-game-9).

## 1. The game as it stands

**What it is.** A first-person voxel game on MonoGame (about 45,000 lines of C# in `src`; 4,709 tests passing on `main`). A 384 x 384 x
128 generated world with a player's cliff camp, a trail, a village (stall, three houses, signposts), a river and a waterfall. Three NPCs
(Luna the guide, the villager, the shopkeeper), 19 noun lemmas (`content/game-v1/game/nouns.json`), 3 lessons, 31 check items. All Spanish
is Latin American neutral, A1, hand-authored in `content/PHRASES.md` (about 60 rows, all `llm-opus (provisional)`; the native-speaker check
is deferred, D-game-12). NPC voices are pre-rendered Kokoro WAVs; the learner speaks by push-to-talk (Vosk small), types, or picks buttons.

**The sessions.**

| Session | Content | Length (estimates, never measured with a person) |
| --- | --- | --- |
| 1 | The opening at the cliff camp (English tips), the guide's lesson `first-words` (7 units, 37 steps: hello, yes/no, "¿Qué es esto?", please and thanks, the yo/tú verb unit, getting unstuck), then the camp task (wood, fruit, a torch at the workbench), the stall, the villager, the river, the fire | lesson 5 to 8.5 min, whole run 15 to 25 min |
| 2 | "Personas" (villager then shopkeeper, 20 steps): él, ella, usted, nosotros, ustedes, ellos | about 6 to 8 min |
| 3 | "Ayer y mañana" (about 19 steps): ir, ir a + infinitive, comí/bebí/fui, ayer, hoy, mañana | about 5 to 8 min |

A session is a load of the save (`--load`), so a "session" is a relaunch. In a playtest the pre-check (31 items, about 7 min) comes first,
the post-check last, and the recall check on a later load (`docs/playtest.md` sections 2, 3 and 8; 45 to 55 minutes in all).

**The systems.** The language core (`Vocaterra.Nlp.*`): utterance to verdict, error codes, word readings, conversation steps. In the game: the lesson
runner with fail-forward and person and tense checks (`LessonRunner.cs`); fading English scaffolds per phrase (S8); spaced credit for words
and phrases with growing gaps and the guide's review question (`GuideSession.BeginTalk`, about line 405; `WordProgress.DueWords`,
`PhraseProgress.DuePhrases`); the learner's record (S19, save version 4); the book with Nouns, Verbs (conjugation tables), Phrases, Lessons
and What I said pages, replayable audio and "Practise again" (S20, S20b, S28); mic check, cancel, watchdog and the "heard / expected" line
(S7); an audio mixer with ducking and silence while recording, ambient beds and footsteps (S24, S25); F8 local bug reports (S21); a session
JSONL log and `scripts/playtest-report.sh`; an F6 manual save, written atomically (`SaveFile.cs` lines 21 and 22: a temporary file, then a move).

**Known gaps** (from the plan's "Not fixed" and "Seen, not fixed" lines, and the code):

- No person has played the current build. The pre-flight is open, live push-to-talk with a real microphone is untried (plan, Handover 2026-10-22) and scripted runs never exercise Vosk (S18 "Not fixed").
- Every performance number comes from software GL (llvmpipe): frame medians 32 to 49 ms (plan about line 830), draw median 47.5 ms at the camp (about line 858). The target is a real GPU (D-game-14) and has not been measured.
- Saves are manual (F6, `VoxelGame.cs` about line 978) and a newer bundle refuses an older save (X01 fixes the refusal). `Program.cs` runs `game.Run()` outside the `try` (after line 77's catch), and the game has no unhandled-exception handler, so a crash loses the session.
- No title screen, no "continue", no settings screen: the volumes ride in the save and are set by flags (`README.md` lines 663 to 672); the next session needs `--load <path>` (`README.md` line 346). No UI scale, no gamepad.
- The review runs only when the player walks to the guide and opens her panel, one item at a time; the book prints "due for review" on most phrase rows but nothing sends the player to it (`GuideSession.cs` about lines 405 to 440; screenshot `s20b-shots/play-final.png`).
- Thin content: 19 nouns, one world, three NPCs; the same few nouns (madera, fruta, agua, pan) carry every verb exercise; nothing teaches numbers or prices.
- Small known issues: the card tag `Puedes decir` is Spanish (S17 "Seen, not fixed"; still in `s22-shots/playtest-dialogue.png`); M28b (`quiero pan` at the stall also gets the bottle line); in-flight one-shots are cut hard when recording starts (S24); lamps stand in for torches (S25); the right book page does not scroll and narrow rows drop their note (S28, X03).
- The documents drift from the bundle: the check count was wrong in the README at S26 ("20 items, now 27"), in PLAYTEST at S27 ("still 20, there are 31"), and was fixed by hand each time.

**Planned next (4.5).** X01 saves survive content updates; X02 core M51 (`voy comer`); X03 book polish (scrolling right page, `vamos`, ser and
estar glosses). This backlog takes them as given and notes below where an item touches the same files.

## 2. Ranked backlog

Ranked by learning value (or player value) per unit of effort, with robustness items placed where a loss of data would otherwise destroy
the evidence. Effort: S under a day of agent work, M a milestone, L several. Model tier follows the owner's policy: Haiku 5.5 by default,
Sonnet 5.5 for design-heavy work.

| Rank | ID | Title | Effort | Model | Owner decision |
| --- | --- | --- | --- | --- | --- |
| 1 | R01 | A session-end summary from the learner's record | S | Haiku | no |
| 2 | R02 | A second try after "No sé", in the same session | S to M | Haiku | no |
| 3 | R03 | The mic lab: measure the real microphone path | S, then M | Haiku, then Sonnet | no |
| 4 | R04 | Autosave and a crash handler | S to M | Haiku | no |
| 5 | R05 | The pre-check stops early for a lost beginner | S | Haiku | **yes** |
| 6 | R06 | A review deck: a session warm-up and "Review now" | M | Sonnet | no |
| 7 | R07 | Earlier phrases come back inside later lessons | M | Sonnet (step type), Haiku (content) | no |
| 8 | R08 | Audit and improve the corrective feedback | M | Sonnet | no |
| 9 | R09 | A UI scale and readable text | M | Sonnet | no |
| 10 | R10 | Playtest tooling: per-step evidence, doc counts under test | S to M | Haiku | no |
| 11 | R11 | A title screen: New, Continue, Settings | M | Sonnet | no |
| 12 | R12 | Adaptive pacing from the learner's record | M to L | Sonnet | **yes** |
| 13 | R13 | Measure on a real GPU | S | Haiku | no (needs the owner's machine) |
| 14 | R14 | Numbers and prices: a fourth lesson | M to L | Sonnet, Haiku | **yes** |
| 15 | R15 | Transfer across speakers: more NPCs and varied answers | M to L | Sonnet, Haiku | **yes** |
| 16 | R16 | Technical debt: split `VoxelGame`, deflake a test | M | Sonnet | no |
| 17 | R17 | A sweep of the small known issues | S | Haiku | no |
| 18 | R18 | A second objective loop: orders (defer) | L | Sonnet | **yes** |

### R01. A session-end summary from the learner's record

- **Problem.** The session ends with the `Session complete` card and Luna's closing line (`README.md` line 495), then, in a playtest, the post-check. The learner is told nothing about what they did, though the data exists (S19's `LearnerRecord`; the book header already shows "Without help: 0", `s20b-shots/play-final.png`). The brief's comparable-game section names visible progress among the things reviewers praise (`PROTOTYPE_BRIEF.md`).
- **Change.** On `session_complete`, one card in English: phrases heard, said with help and said without help this session; the verbs used, with the form; "next time Luna will ask you about: ..." (the due list). Counts only, no score, no CEFR label (D-game-10), the same in both arms.
- **Effect.** A reason to return, and a name for what is due. Playtest: the debrief question "which words or phrases did you remember" (`playtest.md` section 6, question 2) should name more items; watch whether the tester reads the card.
- **Effort / model.** S, Haiku. An engine-free model in `Vocaterra.Game`, one layer in MonoGame, tests.
- **Risks.** It reads like a score and discourages a weak session: phrase it as things done, never failures. It must not touch the evaluation path (presentation only).
- **Owner decision.** No.

### R02. A second try after "No sé", in the same session

- **Problem.** The "No sé" path shows the model phrase and moves on (D-game-9; `PHRASES.md` row `no-se`). The count is kept (the book shows "don't know 1"), but the item is not asked again until the spaced gap (10 minutes, then an hour, or a new session). The brief asks for "a model answer and retry without shame" and revisiting "after another activity" (issue 3).
- **Change.** After a `dont_know` or a Miss at a phrase, queue it for a retry later in the same session, after an intervening unit or world action, with the scaffold one rung lower than before; a correct retry logs a `recovered` use. It never blocks anything (fail-forward stays).
- **Effect.** The cheapest failure signal becomes retrieval practice. Playtest: in `session.jsonl`, the share of `dont_know` items said correctly and unaided later in the same session (the report can print it, R10).
- **Effort / model.** S to M, Haiku with a precise brief (`GuideSession.BeginPhraseReview` and `PhraseReviews.Create` are the hooks).
- **Risks.** Repetition fatigue (brief issue 9): cap at 2 per unit. The retry must not earn colour or credit before its gap (D-game-6): log it like an early use.
- **Owner decision.** No. It extends D-game-9 and D-game-6 without changing them.

### R03. The mic lab: measure the real microphone path, then tune it

- **Problem.** The premise rests on speech working (brief issue 4; Noun Town reviews mention speech or microphone about 63 times in negative and 120 in positive reviews, `PROTOTYPE_BRIEF.md`). The game uses Vosk small es, asked for one hypothesis so the core's confidence gate is off (CR-022, plan Handover), and nobody has used push-to-talk with a real microphone (plan Handover 2026-10-22, item 2). Scripted runs never exercise Vosk (S18).
- **Change.** A tool, `--mic-lab`, run once by a person: it prompts each of the 31 check phrases plus the likely confusions (`sí`/`si`, `esto`/`eso`, `quiero`/`quieres`, `voy`/`vas`) and writes a table of recognised text, accepted or not, latency and failure reason. A second milestone then decides the fix from the table: a restricted vocabulary or grammar for Vosk if the core exposes one (unverified: check `Vocaterra.Nlp.Asr` first; a core milestone if not, ADR 0009), a confusion list so a known A1 near-miss is accepted with a "heard / expected" line, or a louder "type instead" offer after two failures.
- **Effect.** The number the brief's "voice reliability" test needs ("Test beginner comprehension, voice reliability and independent transfer separately"). Playtest: the report's `voice` failure rate per tester.
- **Effort / model.** S for the lab (Haiku); M for the tuning (Sonnet).
- **Risks.** A tolerance list can let wrong Spanish pass: every accepted near-miss must still show "heard / expected" and count as supported, never unaided. Needs a human voice and microphone.
- **Owner decision.** No for the lab. Pronunciation scoring stays out (core D13 is open; not raised).

### R04. Autosave and a crash handler

- **Problem.** Saves are F6 only (`VoxelGame.cs` about lines 978 and 2422) and there is no unhandled-exception handler (`Program.cs`, `game.Run()` after the `try`). The learner record, lesson progress and spaced-credit times are both the learner's progress and the research data. A crash 20 minutes in loses the session. The playtest save is written only at the end or on F10 (`playtest.md` section 4).
- **Change.** Autosave to a rotating `autosave.save` in the session folder at each lesson unit boundary, on closing a dialogue, and every 5 minutes of play time (the stored time, not the clock). A try around `game.Run()` and an `AppDomain.UnhandledException` hook that write the last save and a crash report in the F8 format (Markdown plus the log tail, local only, D-game-17), then exit with a message. `--load` of an autosave works like any save. Keep the atomic move.
- **Effect.** No lost sessions; a tester's crash arrives as a report. Playtest: no session missing its post-check because of a crash.
- **Effort / model.** S to M, Haiku. After X01.
- **Risks.** The open dialogue is not saved (`README.md` lines 284 to 285): save only at unit boundaries and closed dialogues. Write on a worker thread to avoid a hitch.
- **Owner decision.** No.

### R05. The pre-check stops early for a lost beginner

- **Problem.** In a playtest the first thing a true beginner meets is the mic check and a 31-item pre-check of about 7 minutes before the world (`playtest.md` sections 3 and 4). The brief says "no quiz at spawn" and warns against punishing beginners (issues 1 and 2). A tester who answers "no sé" fifteen times learns little from item 16 and may quit before seeing the game.
- **Change.** After 8 consecutive `dont_know` or empty answers, offer "Skip the rest"; the unanswered items are logged as `skipped_after_dont_know` and the report counts them as not known. Optionally order items easy to hard.
- **Effect.** Fewer testers who give up in the first 10 minutes. Playtest: the minute the tester first reaches the world, and how many leave in the pre-check.
- **Effort / model.** S, Haiku.
- **Risks.** It changes the measuring instrument: the post-check keeps all 31 items, and the report must treat skipped as unknown, not wrong.
- **Owner decision.** **Yes.** It touches the brief's gates and D-game-10's pre-check. The alternative is to keep 31 items and only reorder them easy to hard. Do not assume the answer.

### R06. A review deck: a session warm-up and "Review now"

- **Problem.** The spaced credit is built (D-game-6 amended, S8) but fires only when the player opens the guide's panel and one item is due (`GuideSession.BeginTalk`: a phrase review first, "the due word follows the next time"). A returning learner (session 2 or 3) starts at the camp with an objective for the next lesson and nothing about last session's phrases. The book's Phrases page prints "due for review" on most rows right after session 1 (`s20b-shots/play-final.png`) with no action to take. The recall check exists only in the playtest. The brief's delayed-recall goal (scenario step 6) has no player-facing form.
- **Change.** (a) At the start of a session with due items, Luna runs a 3 to 5 item warm-up in the world, reusing `PhraseReviews.Create` and the check cards: listen, then say, support off, the most overdue and the most "no sé" first. (b) A `Review now` button on the book's Phrases page starts the same deck from anywhere (`BookPractise.cs`'s "Practise again" is the pattern). Credit rules unchanged.
- **Effect.** The first player-facing use of spacing across sessions. Playtest: the next-day recall check's unaided rate for items that were in a warm-up versus those that were not (same tester); minutes in the warm-up; whether testers press Review now unprompted.
- **Effort / model.** M, Sonnet. Run after X03 (it edits `BookLayer.cs` and `BookPages.cs`).
- **Risks.** A quiz at every session start is homework (brief issue 1): 3 to 5 items, Esc skips, never gating, not called a test.
- **Owner decision.** No. It extends D-game-6 and D-game-10.

### R07. Earlier phrases come back inside later lessons

- **Problem.** Sessions 2 and 3 teach new forms but do not ask for lesson 1's phrases or yo/tú forms except by accident. The step types are explain, model, listen, repeat, answer, do and recap (`lessons.json` "about"); none draws from an earlier lesson.
- **Change.** A `review` step that names a phrase id from an earlier lesson, drawn at run time from the due list when one is due (else skipped). Two or three per later lesson between units, one in the recap.
- **Effect.** Interleaving and delayed use of session 1's verb forms inside sessions 2 and 3. Playtest: the Verbs tables (S28) show the `yo` and `tú` forms of querer and tener used again in sessions 2 and 3.
- **Effort / model.** M: the step type is Sonnet-sized (runner, `LessonBook` validation, English text), the lesson edits are Haiku.
- **Risks.** Step counts are asserted by tests and repeated in the playtest docs (37, 20, 19; see R10). A review step must not count against a person or tense check (S26, S27).
- **Owner decision.** No.

### R08. Audit and improve the corrective feedback

- **Problem.** The shipped screenshot shows "English: Good. The shopkeeper understood you." (`s22-shots/playtest-dialogue.png`): outcome feedback, not language feedback. The core returns error codes (`PHRASES.md` column "learner error": `gender.agree`, `conj.person`, `noun.number`, `vocab.wrong`) and S26 and S27 added retries that name the ending, but no plan entry checks what a learner sees after each code in the free dialogues (stall, villager, guide). The log also prints a developer line (`request_item, understood, 100 %, 14.4 ms`) in the scripted screenshot (S17 "Seen, not fixed"; S18 was to check `--playtest` hides it).
- **Change.** An audit: a table in `docs/` of what the player sees for each code the check bank and phrase table produce; then one short English line per common code ("`la río`: río goes with `el`"), the correct form recast once, on the help rung, never red; hide the debug line outside `--debug`.
- **Effect.** The learner knows why. Playtest: after a slip, does the next attempt fix it (retry success after a feedback line versus a plain Miss, from the log).
- **Effort / model.** M, Sonnet (core feedback API and `EnglishText.cs`).
- **Risks.** Over-explaining a beginner: use the pattern, not the term, as S26 does ("-o is I"). New English lines need an LLM pass.
- **Owner decision.** No.

### R09. A UI scale and readable text

- **Problem.** No text-size or contrast setting exists (a search for `UiScale` and `TextScale` found nothing; one font, `FontProvider.cs`). In the screenshots the dialogue log is small grey text on dark, the English help lines the dimmest (`s22-shots/playtest-dialogue.png`), and the Phrases page is dense with small grey sub-lines (`s20b-shots/play-final.png`). Layouts were tested at 1280 x 720 and 800 x 600 only (S22). English is the beginner's lifeline (D-game-13), so unreadable English defeats it.
- **Change.** A UI scale of 100, 125, 150 and 200 percent (draw the 2D layers into a virtual resolution of window size divided by scale, so the engine-free layouts keep working), a brighter text option for the dim lines (at least 4.5:1 against the panel), and a pass for colour-only signals (the book's highlight, the heard / expected colours, the beacon). The Spanish is already shown as well as spoken, which helps a player who cannot hear well; each progress sound (S24) should have a visual twin (a toast), which most do.
- **Effect.** Small displays, low vision, distance from the screen. Playtest: add "could you read all the text?" to the debrief.
- **Effort / model.** M, Sonnet.
- **Risks.** Clipping at 200 percent (as S28's book at 800 x 600, X03); mouse picking must use scaled coordinates; 3D labels are separate.
- **Owner decision.** No.

### R10. Playtest tooling: per-step evidence, and doc counts under test

- **Problem.** The report script prints the gate lines (`scripts/playtest-report.sh`) but not where a learner stalled; time per unit, skipped steps, microphone failure rate and hint use need someone to read JSONL. The notes template is on paper (`playtest.md` section 7). The documents' counts drifted three times (S26, S27, S28; see section 1).
- **Change.** (a) The report adds time per lesson unit and step, retries, the help rung used, the `voice` failure rate, items recovered after `dont_know` (R02), the skipped count (R05), and a CSV. (b) A test that reads the counts in `docs/playtest.md`, `README.md` and `playtest/tester-readme.md` ("31 items", "37 steps", "7 units") from fixed markers and compares them to the bundle.
- **Effect.** The first batch of testers yields numbers the owner can read in ten minutes and the three hypotheses (gameplay, learning, transfer) can be answered without opening a log.
- **Effort / model.** S to M, Haiku. Python standard library only, `python3 -I`.
- **Risks.** Nothing is sent (D-game-17); per-tester data is kept by code, not name.
- **Owner decision.** No.

### R11. A title screen: New, Continue, Settings

- **Problem.** There is no front door. Session 2 needs `./playtest.sh --load <folder>/playtest.save` (`README.md` line 346, `playtest/playtest.sh`), a non-playtest player can only continue by flag, and volumes and the mic check live in flags and the save (`README.md` lines 663 to 672). The brief: "keep controls and tutorial clear, settings stable, saving reliable".
- **Change.** A minimal screen at launch: `New game`, `Continue` (the newest save in the default folder), `Settings` (volumes, UI scale from R09, mic check again) and `Quit`, drawn in code in the book's style. `--playtest` keeps its own flow.
- **Effect.** A learner can come back the next day without a facilitator, which makes the brief's recall session a player action. Playtest: a tester starts session 2 alone.
- **Effort / model.** M, Sonnet. After X01 and R04; keep it a class apart from `VoxelGame` (R16).
- **Risks.** Duplicating `LaunchOptions` logic (946 lines); the strict-bundle refusal of X01 must remain for the playtest.
- **Owner decision.** No (inside the prototype's own polish rule).

### R12. Adaptive pacing from the learner's record

- **Problem.** Every learner walks the same 37 steps. Someone who says "hola" and "gracias" already (the screener allows it, `playtest.md` section 1) repeats the hello unit; someone who misses four steps in a row gets the same next unit. The record that could steer this exists (S19) and so does the per-step attempt count (`LessonRunner.cs` about line 928).
- **Change.** Two local rules, no global level: after two unaided correct answers in a unit, offer "You know this. Skip the rest of this unit?"; after two misses, insert one extra supported repeat of the missed phrase. Both logged as `adapted`.
- **Effect.** Fewer bored and fewer lost players. Playtest: the spread of lesson duration (the 5 to 8.5 minute estimate has never been measured, S18), and how many use the skip.
- **Effort / model.** M to L, Sonnet (a 1,052-line runner with person and tense checks; tests assert exact step counts).
- **Risks.** A skipped unit has no credited use for S8's fading; a lesson that differs per person blurs the pre and post comparison.
- **Owner decision.** **Yes.** It bears on D-game-10 (no placement, no global score): per-unit skipping is not placement, but whether the study arm may adapt, or must be fixed for a clean comparison, is the owner's call. Do not assume.

### R13. Measure on a real GPU

- **Problem.** All figures are from software GL (draw median 47.5 ms at the camp, 20 to 30 fps, plan about lines 830 and 858). The target is a real GPU (D-game-14) with `--quality` presets, yet no number exists for it, nothing records the chunk-meshing hitch when walking into new chunks, and an intermittent allocation test (`VillageFrontendTests.DrawingTheFiguresTheArmAndTheShoulderCameraAllocatesNothingOnceWarm`, S25 entry) shows the frame budget is only loosely guarded. Fades run on frame time (S25).
- **Change.** A `--bench-route` that walks a fixed route (camp, trail, village, river, waterfall) at each preset and writes p50, p95, p99 and worst frame time, GC count and chunk-mesh time to a text file. The owner runs it once on the real machine and the file goes in the plan. If p99 exceeds 20 ms, a follow-up decides the fix (meshing on a worker, view distance per preset).
- **Effect.** A janky first minute in first person loses a tester. Playtest: a debrief question ("did it run smoothly?") plus the log's frame stats.
- **Effort / model.** S for the bench (Haiku); the fixes are unknown.
- **Risks.** An agent must not run it on this machine; the script is written, not run.
- **Owner decision.** No, but it needs the owner's time on a GPU machine.

### R14. Numbers and prices: a fourth lesson

- **Problem.** Content is thin and narrow: 19 nouns, and `madera`, `fruta`, `agua` and `pan` carry every verb exercise. The brief's functions are "request, answer, ask directions and clarify" (signals table); quantities and prices are not taught at all, and the stall hands over one bottle and one bread, so `quiero dos panes` has no meaning in the game. The lexicon has numerals and question words (`game-v1/lexicon/seed.json`) but no phrase row uses them (`PHRASES.md` has no `cuánto`).
- **Change.** Session 4, "En la tienda": one to ten, `Quiero dos panes`, `¿Cuánto cuesta?`, `Cuesta tres monedas`, the plural of nouns already known; a coin item and a price at the stall. Game words stay in `content/game-v1`; check the core's intents for quantities first and file a core milestone if one is missing. Opus pass, lint `--strict --level a1`, speech, and a larger check bank.
- **Effect.** Practical, transferable vocabulary with an obvious use (brief issue 7). Playtest: the transfer check with the villager buying.
- **Effort / model.** M to L, Sonnet (design, stall logic), Haiku (rows, WAVs).
- **Risks.** More unreviewed Spanish (D-game-12 deferral grows; the sheet is already about 60 rows against the brief's 5); 40 or more new WAVs for the owner to hear; a changed check bank changes the frozen bundle for any recall test.
- **Owner decision.** **Yes.** Scope (more content before the first tester data, against the brief's "improve the learning loop before expanding the world") and the native-speaker review (D-game-12). Do not assume.

### R15. Transfer across speakers: more NPCs and varied answers

- **Problem.** The brief's transfer hypothesis wants "a different NPC or changed context". There are three NPCs and the transfer moment is the guide's offer (S6). Each NPC has a small fixed line pool (`guide.lesson.villager.*`, `guide.lesson.shop.*`); hearing the same voice say the same line rewards remembering the audio, not the phrase (brief issue 9).
- **Change.** Two more NPCs (a baker, a fisher at the river) with different Kokoro speakers, reusing the learned intents (`greet`, `request_item`, `ask_directions`, `thank`) with 3 to 4 authored reply variants per state. The transfer check asks one old phrase of a new NPC.
- **Effect.** Real transfer evidence and a less empty village. Playtest: unaided success of a phrase at a new NPC versus the original.
- **Effort / model.** M to L, Sonnet (agents, `NpcService`, placement in `SceneBuilder`), Haiku (rows, WAVs).
- **Risks.** WAV count and the owner's listening; new text unreviewed; do one of R14 and R15 first.
- **Owner decision.** **Yes** for the voices (the owner listens, S9) and the added unreviewed text. Do not assume.

### R16. Technical debt: split `VoxelGame`, deflake a test

- **Problem.** `VoxelGame.cs` is 2,725 lines, one class, no `partial`; `LaunchOptions.cs` 946 (flags, scripts and help text), `GuideSession.cs` 1,286, `LessonRunner.cs` 1,052, `SceneBuilder.cs` 1,389. Every milestone adds to `VoxelGame` (S21, S24, S25, S28). `ScenarioScript.cs` (510) and `VoxelScript.cs` (475) both drive scripted runs, and S20 to S28 each added a flag and a step (`--book-practise`, `--book-verb`, `--size`). Every item above lands in the same files, so parallel agents conflict.
- **Change.** A refactor with no behaviour change: extract the input router, report, save, sound and dialogue-flow code from `VoxelGame` into classes the architecture tests allow; group the flags by area; fold the scripts' shared steps; fix the allocation test by measuring up to three times and taking the least (the fix the plan already named). Gate: the 4,709 tests and the seeded digests stay identical.
- **Effect.** Faster, safer later work and fewer merge conflicts.
- **Effort / model.** M, Sonnet.
- **Risks.** It conflicts with every other in-flight milestone: do it between batches, never beside one. Rendering and input have fewer tests than the simulation; screenshots are the check.
- **Owner decision.** No.

### R17. A sweep of the small known issues

- **Problem.** Items recorded and left: the Spanish `Puedes decir` tag (S17), in-flight one-shots cut at recording and a scripted chime held back (S24), M28b, the pergola posts (S15), lamps for torches (S25), `--greyscale` on the book not checked by eye (S20), a trimmed check still counted in old version 2 saves (S19b), a distance-based pace (S25).
- **Change.** One milestone with a test or a screenshot for each, in order of what a first-time player sees: the tag, the stall's item-dependent reply (a core milestone if needed), the sound cut.
- **Effect.** Polish a tester notices in the first ten minutes. Playtest: friction notes at the card.
- **Effort / model.** S, Haiku.
- **Risks.** Low; the stall reply may need the core.
- **Owner decision.** No.

### R18. A second objective loop: orders (defer)

- **Problem.** After `session_complete` nothing is left but the next lesson. The brief wants "interest in another scenario" and "a small game, not a language quiz in a 3D scene" (Purpose and the Enjoyable gate). One camp task is the whole loop.
- **Change.** Villagers post small orders (bring three planks; ask the baker for bread) that need a phrase and an item and pay a cosmetic reward, with no penalty, reusing intents, items and the book's record. Builds on R14 and R15.
- **Effect.** A reason to keep playing and practise beyond the lesson. Playtest: the answer to "would you play the next part?" and the minutes of voluntary play.
- **Effort / model.** L, Sonnet.
- **Risks.** Scope growth before any tester has played; the brief says to improve the learning loop before expanding the world.
- **Owner decision.** **Yes**, and it is better to wait for the first pilot's data.

## 3. How X01 to X03 fit

- **X01 (saves survive content updates)** is the base for R04 (autosave), R11 (Continue) and R14 (a new lesson changes the bundle): it goes first. Its strict mode stays for the playtest's frozen bundle; R05 and R14 both change the instrument, so they need the owner's answer before a freeze.
- **X02 (core M51, `ir` + a bare infinitive)** closes the session-3 gap from S27 (`voy comer pan` passes the core clean) and overlaps this list only through R14's tense work.
- **X03 (book polish)** edits `BookLayer.cs`, `BookPages.cs` and the glosses. R06's `Review now` button and any "skipped" mark live on the Phrases and Lessons pages, so they run after X03.

## 4. A proposed first batch

Order: X04 and X05 can run side by side (disjoint folders), X06 after X01, X07 after X03 and X04, X08 alone (it touches every UI layer; no other UI work beside it). Rows are in the plan's style.

| ID | Experiment | Done criteria | Model | Depends on |
| --- | --- | --- | --- | --- |
| X04 | The session summary card (R01) and the second try after "No sé" (R02): an engine-free `SessionSummary` and `RetryQueue` in `Vocaterra.Game`, one card layer, the runner's hook, `recovered` uses logged | The card appears after `session_complete` with counts from the record (a test: a fixed record gives fixed text, no failure counts); a `dont_know` item is asked once more later in the same session, never credited or coloured before its gap, never blocking; `--lesson-run first-words` still completes; the gate passes | Haiku 5.5 | S28 |
| X05 | Autosave and the crash report (R04) | Autosave at unit boundaries, on dialogue close and every 5 minutes of play time into the session folder; an injected exception (a test hook) leaves a loadable save and a Markdown crash report with the log tail; `--load` of the autosave resumes at the objective and lesson step; none while a dialogue is open; no clock in the evaluation path (ArchitectureRulesTests); the gate passes | Haiku 5.5 | X01 |
| X06 | The review deck (R06) with the `review` step type (R07) | A 3 to 5 item warm-up at a session start with due items (Esc skips, never gating); `Review now` on the Phrases page; a `review` step in Personas and Ayer y mañana asks an earlier phrase when one is due; tests for the due order and the credit rules; the recall check on a saved game credits the warm-up items by the spaced gaps; the gate passes | Sonnet 5.5 | X03, X04 |
| X07 | The mic lab (R03, tool only) and the real-GPU bench (R13, script only) | `--mic-lab` and `--bench-route` exist and build, with unit tests for their output tables (no hardware in tests); README has the commands; the plan lists them as the owner's two human steps with the output files named; the agent runs neither | Haiku 5.5 | X05 |
| X08 | UI scale and readable text (R09) | Four scale steps work at 1280 x 720 and 1920 x 1080 with layout tests at each (no overlap, no clipped title, hit-boxes agree); the dim English lines reach 4.5:1 (a test on the colour constants); screenshots at 150 percent for the dialogue, the book and the check card; the setting is kept in the save settings; the lesson strip and check card have no colour-only signal (listed in the entry); the gate passes | Sonnet 5.5 | X03 |

R05, R12, R14, R15 and R18 wait for the owner's answers. R08, R10 and R17 are good second-batch rows; R16 goes between batches.

## 5. Open questions for the owner

1. **The pre-check (R05).** May a beginner skip the rest of the 31-item pre-check after about 8 "no sé" answers, with the rest logged as unknown, or must the pre-check stay a fixed instrument (D-game-10), shortened only by ordering easy to hard?
2. **Adapting a lesson (R12).** Is per-unit skipping and extra practice acceptable in the study arm, or must every tester get the same lesson so pre and post compare cleanly? (D-game-10 forbids placement and a global score; this is neither, but it changes what each tester lived through.)
3. **More content before the pilot (R14, R15, R18).** The brief says to test the learning loop before expanding the world. Do you want numbers and prices, more NPCs and orders now, or only after the first pilot's recall data?
4. **Voices and review for new text (R14, R15).** Which Kokoro speakers for new NPCs? Is the LLM review plus your listening still the interim for each new row, with the native-speaker check still deferred (D-game-12), though the unreviewed set (about 60 rows) keeps growing?
5. **The human hardware steps.** Can you run the mic lab (R03) and the real-GPU bench (R13) once each, and the open pre-flight (`playtest.md` section 12), and when? They are better done before more content.
6. **The first pilot.** Is there a date or a set of testers? Most "effect" lines above are hypotheses only a session folder can confirm, so R10's report should be ready first.
7. **A front door (R11).** Is a title screen with Continue in scope for the prototype, or do `--load` and the launcher stay the way back in until after the pilot?
8. **Decision log.** R01, R02 and R06 extend D-game-6, D-game-9 and D-game-10 without changing them. Shall they get one D-game number, or only a plan entry?
