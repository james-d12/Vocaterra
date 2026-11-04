# Spoken lines that a constrained prompt does not take: a design study (2026-11-04)

> A read-only study (Opus 5.5, `Plan` agent) for the owner's decision of 2026-11-02, "fix the spoken off-topic lines" (D-game-22; the
> after-followups handover, section 3). Nothing was built or run for it. The questions in section 7 are the owner's.

Every number below is marked: **measured** means a recorded measurement or a count taken from the repository, with its source; **target**
means a design goal nobody has measured; **estimated** means the study's own reckoning.

## 1. The problem

- **What the recognizer is given.** At a `Constrained` prompt, `UtterancePipeline.BuildRequest`
  (`src/Vocaterra.Nlp/Pipeline/UtterancePipeline.cs:226-231`) hands the recognizer only the answer set's phrases plus `[unk]`.
  `VoskWordRecognizer.CreateRecognizer` (`src/Vocaterra.Nlp/Asr/VoskWordRecognizer.cs:132-143`) builds the Vosk grammar from them.
- **What happens to the `[unk]` parts.** `CleanTranscript` (`UtterancePipeline.cs:194`) deletes them before the line is matched and
  analysed. So a line outside the set reaches the core in one of two forms: an empty line, or the answer phrase nearest to the sound. The
  off-prompt pass of M72 (`IntentResolver.cs:103`, `VerdictAssembler.cs:88-90`) therefore never sees the words the learner said.
- **How the game answers.** `NpcService.Take` (`src/Vocaterra.Game.Core/Npc/NpcService.cs:620`) gives the off-prompt reply «Te entiendo…»
  only when `Verdict.OffPromptIntent` is set. A spoken off-topic line falls into the uncertain path instead (`NpcService.cs:685`): «No te
  entendí», then the repair ladder.
- **A forced answer can count as understood.** The constrained pass does not always fail visibly; sometimes it confidently returns a wrong
  answer phrase. Measured: V14's run heard «Ellos fueron al río.» as «fuimos al río» at the constrained lesson step, and the open
  `villager-open` prompt heard «fueron» as said (`docs/game/implementation-plan.md:1794`;
  `tests/Vocaterra.Game.Core.Tests/Language/Speech/YesterdaySpokenTests.cs:54-67`). No fix built on the first pass can catch a line that
  the first pass has already counted as understood.
- **Finding 1: seven prompts are affected, not four.** The handover lists the four NPC open prompts. Free talk also waits on three more
  constrained prompts: the shopkeeper's `stall-need` (his idle prompt) and `stall-offer`, and the guide's `guide-offer`
  (`content/game-v1/game/npcs/shopkeeper.json`, `guide.json`; `content/game-v1/prompts/prompts.json:2-50`). V27 gave the shopkeeper and the
  guide «Te entiendo» lines as well.
- **Finding 2: the loss is wider than the off-prompt case.** A spoken line whose intent the prompt *does* allow, but whose words are not
  among the answer rows, is also lost. For example, `mañana voy a comprar pan` at `smith-open` (`state_action` is allowed there) is
  understood when typed and is «No te entendí» when spoken. The typed path reads the text with the same pipeline
  (`TypedUtteranceEvaluator.cs:48`). Any fix that sees the learner's words fixes both cases.

## 2. Facts every option depends on

**Time.**

| What | Value | Kind and source |
|---|---|---|
| One Vosk decode of a clip | 0.16 to 0.37 s | Measured at P5 on Kokoro clips with the small model and a single best hypothesis (`docs/game/implementation-plan.md:610`); the record does not say which recognition mode |
| Vosk model load | 0.25 to 0.39 s | Measured, same record |
| Evaluation of a line | 15 to 20 ms | Measured, same record |
| Decodes per spoken turn today | 1 | From the code: the game asks for one hypothesis (`SpokenUtteranceEvaluator.cs:31`), and `VoskWordRecognizer.Recognize` decodes a second time only for two or more (`VoskWordRecognizer.cs:96-100`) |
| Recognition time the original design aimed for | under 150 ms constrained; under 400 ms open for a 3 s clip | Target only, never confirmed (`docs/archive/nlp/history/high-level-docs.md:335-336`); the measured single decode is already over the constrained target |
| Longest wait before the game offers typing | 5 s | From the code: the "thinking" watchdog (`TurnWatchdog.cs:11`), the only time limit the game enforces |
| Longest clip | 8 s | From the code: clips are cut there (`AudioClips.cs:18`) |

**Determinism.**
- Repeated decodes are bit-identical in both modes (`tests/Vocaterra.Nlp.Tests/Asr/VoskTests.cs:63-80`), because M9 turned Vosk's dither
  off (`docs/nlp/implementation-plan.md:493-495`).
- Each call creates its own native recognizer on the one loaded model, with or without a grammar (`VoskWordRecognizer.cs:118, 140, 143`).
  A second decode needs no second model load.
- The game already decodes one clip twice: the mic check decodes with a grammar first and freely only if that fails (`MicCheck.cs:38-46`).
- Two decodes at the same time are not supported as the code stands: the API asks for one `VoskWordRecognizer` per thread
  (`docs/nlp/api.md:1063`), and the game's turn worker is a single thread by design.
- Replay serves the recorded recognition whatever the request asks for (`ReplayWordRecognizer.cs:58`).

**Accuracy.**
- **Why constrained decoding exists.** Strong recognizers smooth learner errors away, and decoding against a grammar is "the
  error-preserving path" (`docs/research/2026-10-20-scaling-approaches.md:228-236`; the HLD risk table, `high-level-docs.md:365`).
- **What the brief says.** The prototype brief's default is "ASR only for intent in a constrained phrase set" (D-game-9;
  `docs/game/design/prototype-brief.md:42`). `villager-open` and `guide-open` already depart from it.
- **The only comparison between modes is on synthetic speech.** At the stall, both modes gave the same intents for every scripted Kokoro
  line (S7, measured, `docs/game/implementation-plan.md:746`).
- **Nothing is measured on learner speech.** `tests/golden/audio/` is empty; M24 waits for the owner's recordings
  (`docs/nlp/known-gaps.md:9-11`).
- **Parakeet is not a candidate for the free decode.** It is 641 MB (measured, `docs/nlp/implementation-plan.md:1224`), it sometimes gave
  different results for the same clip (M26g), it carries GPL code through sherpa-onnx (D2), and the game does not load it.

**The uncertain path.**
- `UncertaintyRule` (`SpeechRules.cs:32-60`) averages Vosk's per-word confidences, leaving out `[unk]`. A line is uncertain when it is not
  understood, or when that mean is under 0.55.
- The 0.55 threshold is measured on Kokoro speech only: correct phrases 0.77 to 1.00, a misheard «hola» 0.36.
- V27 already refuses the off-prompt reply when the mean is under 0.55 (`NpcService.cs:620`).

**The boundary.** ASR and evaluation are the core's (ADR 0009, section 1). The game captures and keeps the clip for the whole turn
(`NpcService.cs:603-605`), so the audio is there for a second pass.

## 3. The options

**Option 1: a second, free decode when the constrained pass is poor (free talk only).**
- **Cost.** One more decode, only on lines that fail today: estimated 0.3 to 0.75 s of recognition on that path, from the measured single
  decode; well inside the 5 s watchdog, and nothing changes on lines that work.
- **Accuracy risk.** Confined to lines that are «No te entendí» today. The danger is a false take: an accented attempt at an answer row that
  the constrained pass rejected, decoded freely into words that some intent accepts. The catalog has 15 one-word keyword rules with no
  required slot, among them `no`, `sí`, `ir`, `eso` and `esto` (counted in `intents.json`). The game's 0.55 confidence rule is the guard;
  it must be measured on free decodes.
- **What it cannot do.** It never catches a forced answer that was counted as understood (the «fueron» case).
- **Determinism.** Kept: the passes run in a fixed order on one thread, and replay stays exact (section 5).
- **Uncertain path.** Unchanged when neither pass is understood.
- **Size.** Medium in the core, small to medium in the game.

**Option 2: decode both ways at once and choose.**
- **Cost.** Two decodes on every spoken turn: estimated +0.16 to 0.37 s on each. In parallel it would need a second model instance: a
  second load of 0.25 to 0.39 s (measured) and estimated tens of MB more memory.
- **Accuracy risk.** Its only gain over option 1 is overruling a constrained result that was counted as understood. Doing so safely needs a
  choosing rule calibrated on learner speech, which does not exist; calibrated on Kokoro, it would overrule correct accented answers, the
  loss constrained decoding is there to prevent.
- **Determinism.** Kept if run in sequence.
- **Size.** Medium to large, and the choosing rule cannot be checked today.

**Option 3: widen the grammar with the catalog's phrases.**
- **What the catalog holds.** The grammars are patterns over the lexicon, not phrases (`[ART]` 65 times, `[NOUN:item]` 36, `[NUM:qty]` 28;
  counted). Expanding them over 40,263 lexicon entries (measured, `docs/nlp/tools.md:130`) is combinatorial. Only the 541 example texts of
  the 30 intents are literal phrases (counted).
- **What it would do.** A larger grammar makes the answer rows easier to confuse, eroding the accent tolerance; the nearest-phrase forcing
  would pick catalog examples, so a misheard answer row would get «Te entiendo». It still misses every line nobody wrote down, and it
  changes grading for every prompt it touches.
- **Cost and determinism.** No extra decode; deterministic.
- **Verdict.** Rejected.

**Option 4: make the NPCs' open prompts open.**
- **How "keep their rows another way" works.** The answer set is matched against any transcript (`UtterancePipeline.cs:99`), so the rows
  stay graded in open mode; only the constrained hearing of the rows in free talk goes.
- **Size and cost.** A content edit to four `mode` values and a new bundle hash; no extra time.
- **Side effect on typed grading.** The derived `vocab.wrong` and M75's near-miss rule apply only at constrained prompts
  (`VerdictAssembler.cs:265`). Typed grading at those prompts would change too, and the goldens and the identity baseline would move.
- **Accuracy risk.** Falls on every line said to those NPCs, rows included; unmeasured for accents.
- **Coverage.** It leaves the three stall and offer prompts as they are, and departs further from the brief's default.

**Option 5 (from the code): option 1 done in the game alone.**
- The mic check's two-pass pattern and `SpokenUtteranceEvaluator.AnalyzeRecognized` (`:81`) would let the game do option 1 with no core
  change: the smallest change.
- **Why not.** It puts a policy for choosing a recognition in the game, against ADR 0009; the core's tests, fuzz suite and replay checks
  would not cover it, and any other host would have to write it again.
- It remains a fallback if the owner wants the fix sooner than a core milestone allows.

## 4. Recommendation

**Option 1, built in the core, switched on by the host for free talk only.**
- **Why.** It is the only option that cannot make worse any spoken line that works today: a second pass runs only when the first is not
  taken, and the first result is kept unless the second is taken. It keeps the rows' constrained hearing, costs time only on the failing
  path, and stays deterministic and replay-exact.
- **It also closes Finding 2.** A spoken line then reads as its own transcript would read typed: the parity V27 gives typed lines.
- **What it leaves.** Forced answers counted as understood (the «fueron» case) stay as they are. Option 2's overrule waits for learner
  recordings (M24); the measurement below reports how often forced answers occur.
- **Lesson steps stay constrained.** No reason was found to change them.

## 5. Core milestone (proposed: M80, Opus 5.5 at xhigh)

**Scope.**
- **The switch.** `PipelineOptions.OpenRecognitionFallback` (a `bool`, default `false`), an additive host option rather than a prompt
  field, so that "free talk only" stays the game's decision.
- **When the second pass runs.** In `UtterancePipeline.Analyze`, only when all four hold: the option is on; the prompt is `Constrained`;
  the first recognition's `Mode` is `Constrained`; the first reading is **not taken** (not `Communicated` and no `OffPromptIntent`).
- **What the second pass is.** Decode once more with `RecognitionRequest.Open(MaxAlternatives)` and read that recognition through the same
  stages. Factor the stages after recognition into one private method used by both passes. The phoneme posteriors are computed once.
- **Which pass wins.** The second, only if it is taken; otherwise the first, which is then byte-identical to today. The core adds no
  confidence gate of its own: the game's `UncertaintyRule` reads the chosen `Asr`.
- **What is exposed.** `UtteranceAnalysis.Asr` is the chosen recognition (`Mode` = `Open` marks a line heard freely). A new optional
  `UtteranceAnalysis.OtherPass` is the recognition that was not chosen.
- **Replay needs no format change.** When the free recognition was chosen, it is recorded with `Mode` = `Open`, so the trigger never fires
  on replay. When the constrained one was kept, replay serves it again for the second request; it is not taken either, so the first is
  kept and the verdict is the same.
- **Test helper.** An additive constructor of `ScriptedWordRecognizer` that answers by mode.
- **Docs.** `docs/nlp/api.md` (sections 3, 5 and 8), `docs/nlp/pipeline.md` (stages 0 and 1), `docs/nlp/replay.md`, and a note in
  ADR 0009.

**Files.** `src/Vocaterra.Nlp/Pipeline/UtterancePipeline.cs`, `PipelineOptions.cs`, `UtteranceAnalysis.cs`,
`src/Vocaterra.Nlp/Asr/ScriptedWordRecognizer.cs`, `tools/Vocaterra.Nlp.Bench/Program.cs`, and the tests under
`tests/Vocaterra.Nlp.Tests/Pipeline/`.

**Tests (scripted, no models).**
- With the option off, every golden, the identity baseline, the replay tests and the fuzz stream digests are unchanged.
- A first pass that is taken means the recognizer is called once.
- An off-prompt free pass sets `OffPromptIntent`, and `Asr.Mode` is `Open`.
- An allowed intent outside the rows becomes understood.
- A free pass that is not taken keeps the first verdict.
- Open prompts and the option-off case never decode twice.
- A chosen free recognition replays without the model, with no difference, through pipelines with the option on and off.
- A new fuzz oracle: with the option on, a line taken on the first pass gets a verdict identical to the option-off verdict.

**What to measure with the models (`VOCATERRA_MODELS_DIR`).**
- **The test.** A model-gated test that prints a report, like `ConstrainedRecognitionTests.TheDemoLinesInBothModes`, and asserts the
  invariants.
- **The corpus.** Kokoro clips over the seven free-talk constrained prompts: every answer row; about eight off-prompt lines the catalog
  takes (with M74's weather lines and `¿cómo estás?`); five lines with an allowed intent outside the rows; five lines no intent takes
  (`la tienda está en el pueblo`, `el gato duerme`); silence and noise.
- **The voices.** The Spanish voices 28, 29 and 53, at speed 1.0 and 0.8; the Italian and Portuguese voices the NPCs already use
  (`im_nicola`, `pm_santa`), a weak stand-in for an accent, reported as such; the owner's recordings, if he gives them (question 5).
- **What to report.**
  - The share of off-prompt lines whose first pass is not taken (the trigger's reach; the rest are forced answers).
  - The share understood after the free pass, with the right intent and a mean confidence of 0.55 or more.
  - **False takes:** lines no intent should take that the free pass makes taken at 0.55 or more.
  - Recognition time per pass, median and 95th percentile by clip length. Clocks are allowed only in the Bench tool, which today chooses
    Parakeet for open mode when present (`Program.cs:143`); it needs `--open-fallback` and a way to force Vosk.
  - For information: how many answers are forced but taken with a mean under 0.55 (the variant that would also send them to a second
    pass).

**Acceptance.** The invariants above hold with and without the models; every answer row's verdict is identical with the option on and off;
no false take in the corpus; the failing path's recognition stays under 1 s for clips up to 4 s (an estimated target; the measured figures
are reported); the gate passes.

## 6. Game milestone (proposed: V31, Sonnet 5.5)

**Scope.**
- **Which turns.** Free talk (`scene.PromptOverride is null`) uses an evaluator with the fallback on. Lesson steps, reviews and the mic
  check keep today's options. In practice, `SpokenUtteranceEvaluator` holds two pipelines.
- **Line reading.** `LineReading` also tries the vocative and greeting readings on `OtherPass` when the chosen reading is not taken.
- **Timings.** `TimedRecognizer` (`SpeechEngine.cs:246-258`) records only the last decode; it should sum the decodes of a turn, and
  `TurnTimings` should show the count.
- **Telemetry.** The session log and the voice event mark a line heard freely, so that the playtest can count them.
- **Credit.** As the owner answers question 3.
- **Docs.** A note under D-game-9; the speech bullet of `docs/game/architecture.md`; remove the open point in `docs/game/village-plan.md`
  section 5.

**Tests.**
- Kokoro model tests at each affected NPC: a spoken off-prompt line gets its «Te entiendo…».
- A spoken allowed line outside the rows is understood.
- A lesson step decodes once (a counting recognizer).
- `ConstrainedRecognitionTests:92` (a garbled line at the stall, three uncertain turns, then choices) still passes.
- A low-confidence free pass keeps «No te entendí bien».

## 7. Questions for the owner

1. Do you accept option 1, a second free decode only when the constrained decode is not understood and only in free talk, instead of
   opening the four NPC prompts (option 4)?
2. A spoken line that the prompt allows but that is not one of its answer rows, such as `mañana voy a comprar pan` at the smith, would be
   understood and answered as the same typed line is. Do you want that, or only the «Te entiendo» reply for lines the prompt does not take?
3. A line heard on the free decode may have had a learner's mistake smoothed away by the recognizer. Should it earn full spoken credit in
   the book (as lines at the villager's open prompt do now), half credit as a typed line does, or no phrase credit?
4. Should the fix cover all seven constrained prompts that free talk uses (the four NPC open prompts, the shopkeeper's `stall-need` and
   `stall-offer`, and the guide's `guide-offer`), or only the four NPC open prompts?
5. The accent risk can only be measured on real speech, and today there are only Kokoro's synthetic voices. Will you record about 40 short
   clips in your own voice (answer rows, off-topic lines, and nonsense) for this milestone's measurement, under the consent rules in
   `tests/golden/README.md`?
6. How often may an NPC say «Te entiendo» to a line that meant nothing (a false take)? The proposed acceptance is zero in the test corpus.
   Is that the bar, or is a small rate acceptable in exchange for understanding more real lines?
