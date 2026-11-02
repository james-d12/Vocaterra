# Handover: the owner's decisions after the follow-ups (2026-11-02)

**For a fresh orchestrator session.** Start this work only after the owner has merged the pull request of `feature/village-followups` into
`main`. Then create a new feature branch from `origin/main` (for example `feature/village-decisions`), check it out in the worktree
`.claude/worktrees/village`, and gate every milestone with `GATE_TARGET=<that branch> scripts/merge-gate.sh "<label>:<branch>"`. Never push
`main`.

Read first:
- `.claude/agents/orchestrator.md`: the roles, the resources, the per-milestone loop, and the gotchas in "Handover (2026-11-01)".
- The milestone entries that this work builds on, in their findings logs (`grep -n '^- \*\*M72\|^- \*\*M73'
  docs/nlp/IMPLEMENTATION_PLAN.md`, `grep -n '^- \*\*V2[078]' docs/game/IMPLEMENTATION_PLAN.md`).
- `docs/game/README.md`, "Open points after the follow-ups".

The used briefs in `.claude/briefs/` (M72, M73, V27, V28) are the models for the new ones.

## 1. What the owner decided (2026-11-02)

| # | Item | The owner's word | Status |
|---|---|---|---|
| 1 | The weather lines | "fix them by adding a weather intent" | Decided; build it |
| 2 | Spoken lines that the prompt does not take | "fix the spoken off-topic lines" | Decided; needs a design study first (section 3) |
| 3 | A reply to `¿cómo estás?` | "add that too" | Decided; build it |
| 4 | M67's near-miss rule | "go with your recommendations": a near miss must name its word (section 5) | Decided; build it |
| 5 | The replies to weather lines and to `¿cómo estás?` | "go with your recommendations" (sections 2 and 4) | Decided; build them |
| 6 | Listening to the voices; deleting the merged remote branches | "I will do later" | The owner's; not for agents |

Items 1 to 5 are recorded in D-game-22 (`docs/game/DECISIONS.md`, 2026-11-02).

## 2. The weather intent (core, then game)

**The lines today** (after M73):

| Line | What it is read as | Why |
|---|---|---|
| `¿qué tiempo hace?` | `ask_activity` | M72's keyword rule: `qué` with `hacer` |
| `hace calor`, `hoy hace sol` | `state_action` | a lesson's grammar `[VERB:hacer] [NOUN:item]` |
| `hace frío`, `hoy hace buen tiempo` | `unknown`, not understood | no intent of the catalog takes them since M72 and M73 |

M73 reported that the fix needs either a weather intent or a way to rule a lemma out of a grammar. The owner chose the intent.

**Core milestone (M74, Opus 5.5 at xhigh):**
- In the intent catalog (`content/game-v1/intents/intents.json`), add a statement intent (for example `state_weather`: `hace frío`, `hace
  calor`, `hace sol`, `hace buen tiempo`, `hace mal tiempo`, `hoy hace …`, `llueve`, `está nublado`) and a question intent (for example
  `ask_weather`: `¿qué tiempo hace?`, `¿hace frío?`).
- Give them a slot for the weather word if a reply will echo it.
- Keep `hacer` with a weather noun out of `state_action`, `ask_activity` and `ask_craft`, by the resolver's order or the grammars, not by a
  hack. `¿qué haces?`, `hago pan` and the lessons' craft questions keep what they have.
- Fuzz: O7 and O3 widened for the new intents, never narrowed. Add the new lines to the grading tests by topic (M76 turned `scripts/try-lines/` into C# tests) and
  re-record the identity baseline, as M72 and M73 did.
- Check every weather word against the A2 ceiling (`cefr-check`).

**Game milestone (V29, Sonnet 5.5):**
- Allow the new intents at the NPCs' open prompts (as V20 did for the lessons' intents).
- Give each NPC a reply in its voice (the orchestrator's proposal, accepted by the owner on 2026-11-02):
  - a statement gets «Sí, hace frío.», echoing the learner's weather word (a template with a slot), or «¡Sí!» when there is no word;
  - `¿qué tiempo hace?` gets «Hoy hace sol.», or whatever the world's weather is if the game has one (it does not today).
- The book credits the line as free talk does (V20 (b)).
- The PR #28 review's item 14: the acknowledgement and «Sí, es verdad.» handling matches pool names in code (`^[a-z]+_nice\.\d+$`,
  `answer_accepted.`, `NpcTurnRules` about line 268), and `ListenerReply` always takes the first variant. Declare these pools in the NPC
  files (`content/game-v1/game/npcs/*.json`) before adding the weather and `¿cómo estás?` pools, so no new name is written in code.

## 3. Spoken lines that the prompt does not take (design first, then core and game)

**The cause:**
- At a constrained prompt, the recognizer decodes only the answer set's phrases.
  - `UtterancePipeline.BuildRequest` (`src/Vocaterra.Nlp/Pipeline/UtterancePipeline.cs`, about line 228) builds
    `RecognitionRequest.Constrained(answerSet.GrammarPhrases …)`.
  - `VoskWordRecognizer` (`src/Vocaterra.Nlp/Asr/VoskWordRecognizer.cs`, about line 132) gives Vosk that grammar.
- So a spoken line outside the set comes out as the nearest answer phrase or as `[unk]`. The core never sees the learner's words, and
  M72's `Verdict.OffPromptIntent` is never set: the NPC says «No te entendí» where a typed line gets «Te entiendo.» (V27).
- These prompts are constrained:
  - the NPCs' open prompts `smith-open`, `baker-open`, `fisher-open` and `farmer-open`, because V20 gave them their lessons' rows as
    answers, graded and heard when spoken;
  - every lesson prompt.
- `villager-open` and `guide-open` are open, and decode freely.

**First, a design study** (an Opus `Plan` agent, read only, as for M72):
- **The options** to compare:
  1. A second, free-vocabulary pass when the constrained pass is poor (no answer matched, `[unk]`, or confidence under the threshold),
     in free talk only.
  2. Decode freely and against the grammar at once, and choose between them.
  3. Widen the grammar with the catalog's grammars' phrases.
  4. Make the NPCs' open prompts open, and keep their lessons' rows another way.
- **What to measure:** the time a second decode costs (the game's speech budget), the accuracy lost to free decoding of a learner's
  accent (the reason constrained decoding exists), determinism (model-gated tests, `VOCATERRA_MODELS_DIR`), and what the uncertain
  path does (`UncertaintyRule`, D-game-9).
- Lesson steps stay constrained unless the study shows a reason.
- Bring its recommendation to the owner, then plan a core milestone (Opus 5.5 at xhigh) and the game's half (Sonnet 5.5).

## 4. A reply to `¿cómo estás?` (core, then game)

**Today:** since M73, `cómo estás`, `cómo está usted` and the plurals are `greet`. The NPCs answer them as they answer `hola`, for
example «¡Hola! ¿Qué buscas?» (the villager) or «¡Hola! Esta es mi herrería.» (Domingo).

**The fix:**
- **Core:** a separate intent for the question, for example `ask_wellbeing`, taken out of `greet`, so that the game can tell it from
  `hola`.
  - It fits in M74 beside the weather intents: both are catalog edits.
  - `cómo estás usted` keeps its `conj.person` (M73's example).
- **Game (V29):** allow the new intent wherever `greet` is allowed, and give each NPC «Bien, gracias.» in its voice: 7 lines, 14 WAVs.
  - The orchestrator proposed this line; the owner said "add that too".
  - Do not add «¿Y tú?», unless the learner's `bien` and `muy bien` are understood at every open prompt.

## 5. M67's near-miss rule (decided: a near miss must name its word)

**What it does** (`VerdictAssembler.DeriveVocabWrong`, `ExplainsAMiss` and `IsNearMiss`, `src/Vocaterra.Nlp/Evaluation/VerdictAssembler.cs`):
- At a constrained prompt with no answer matched, a line gets `vocab.wrong` (weight 0.6, so a score of 0.4) when it meets two
  conditions:
  - it resolved no intent, or its intent came from a keyword rule or the embedding rung rather than a slot grammar;
  - it is within the fuzzy budget of the nearest answer phrase, taken for the line's own length.
- Its expected word is empty when the answer set has several answers, which is every NPC's open prompt.
- **The case it was made for:** `cuál es esto` for `qué es esto` at `lesson-what`.

**The trouble:** it tags correct Spanish.
- `quiero hacer un hacha` at `smith-open` is `state_want` by a keyword rule. It scores 0.4 with an empty expected word, while Domingo
  praises it: «Claro, «quiero hacer un hacha». Muy bien.»
- M72 found `rápido por favor` and `pasado mañana iré al río con Mar` too.
- M72's O3 verdict leg has to admit these tags and count them (about 1 in 2,000).

**The rule to build** (the orchestrator's recommendation; the owner chose it on 2026-11-02): a near miss must name its word.
- Keep the tag only when the line and the nearest answer phrase have the same number of words and differ in exactly one.
- The two differing words must also be a slip pair: either close in spelling (within that word's own fuzzy budget), or both of one closed
  class (question words, articles, prepositions, pronouns).
- Report the answer's word as `Expected`, so the expected word is never empty.
- **The effect:**
  - `cuál es esto` keeps its tag, now naming `qué`;
  - `quiero hacer un hacha` (an added word), `pasado mañana iré al río con Mar` and `rápido por favor` (content words far apart) lose it;
  - the praise and the score agree again.
- **The milestone:** a core milestone (Opus 5.5 at xhigh).
  - Also the PR #28 review's item 8: a constrained prompt with no answers gives the matcher distance 0, so every unresolved line is a
    "near miss" (`VerdictAssembler.IsNearMiss`). The new rule needs an answer to name, so it holds no such case; test it.
  - O10's model and generator follow the rule.
  - O3's verdict leg becomes strict again (a widening).
  - The grading tests (M76) and the identity baseline move. List every changed case by kind.

**The alternatives the owner did not choose** (kept for the record):
- **(b) Keyword lines out:** drop the keyword-rule case, so only a line with no intent can be a near miss. It is simpler, but check first
  whether `cuál es esto` (`ask_what`, communicated) then loses its tag.
- **(c) A note, not a mistake:** keep the tag at weight 0.
- **(d) Remove the near-miss case entirely.** The case for a prompt that names the word stays.
- M67b (an answer's verb forms, parked) is a separate question; keep it parked unless the owner says otherwise.

## 6. A suggested order

The machine allows one agent at a time, with no second full-build worktree (disk).

1. **M74 (core):** the weather intents and `ask_wellbeing`, in the catalog.
2. **M75 (core): the near-miss rule** (section 5), Opus 5.5 at xhigh.
3. **V29 (game):** the weather and `¿cómo estás?` replies, and the prompts' allowed intents.
4. **The design study for spoken lines,** with the owner's choice, then its core and game milestones.
5. **A pull request** of the new branch into `main`.

M68 (CI timing) runs only if CI fails a timed test. The owner reports CI failures, because this token cannot read CI results.

## 7. A known random failure that can fail a gate or CI

- **The fuzz family `2tpLGUOSqj41`** (O5, `RecastLoopFuzzTests.Recast_RestoresTheCorrectedSentence_AndReAnalyzesClean`): a `word_order`
  mutation moves an adjective before its noun, and a grammar's optional `[WORD]` slot rightly binds the moved adjective as a name, so the
  recast cannot restore the sentence.
  - Its seeds so far: `2tpLGUOSqj41`, `fSMdah3kOSd7`, `3-VvCuMLFEA2`, and `8ogzm5GU5h-8` (V28's run without models, `magos relacionados
    compañero` under `([NOUN:item] [ADJ]|[WORD:qty] [NOUN:spell]) [NOUN:thing]`; it replays with `CsCheck_Seed`).
  - The owner kept it recorded, not narrowed (D-game-22, 2026-11-01, the answer (d)). M73 found that the only fix narrows the mutation.
- **What to do:** a gate or CI run that fails on this family alone is rerun once, after checking with `CsCheck_Seed` that the seed is
  this family. If it fails often, bring the owner the choice again: keep it recorded, or narrow the `word_order` mutation where a
  `[WORD]` slot can bind the moved word (a guard with a test that what it leaves out is correct Spanish, as M65's).
