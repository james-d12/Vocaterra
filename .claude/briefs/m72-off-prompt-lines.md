You are implementing the core milestone **M72, a correct line that the prompt does not take**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** The owner chose this design on 2026-11-02 (D-game-22, "Owner's answers, 2026-11-02 (the off-prompt lines)"); the game milestone V27 builds on it afterwards.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, then `git checkout -b M72-off-prompt-lines @@BASE@@` (the tip of `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra` or any other worktree. Other agents may run at the same time; every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes its tests, with the prefix `M72: `. Push after every commit: `git push -u origin M72-off-prompt-lines` the first time, then `git push origin M72-off-prompt-lines`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.
- **Disk is short (about 12 GB free).** Build only the projects you need: `tests/Vocaterra.Nlp.Tests`, `tests/Vocaterra.Nlp.Es.Tests`, `tests/Vocaterra.Nlp.Golden.Tests`, `tests/Vocaterra.Nlp.Fuzzing.Tests`, `tools/Vocaterra.Nlp.Cli`, `src/Vocaterra.Host` and its tests, and `tests/Vocaterra.Game.Core.Tests` only if a game test pinned the old score. Build the whole solution once at the end.

## The problem (measured by the orchestrator through the host on 2026-11-02)
At an NPC's constrained open prompt, a correct line whose intent the prompt does not take is graded as an error:
```
farmer-open   "la tienda está en el pueblo"  intent=unknown communicated=false correctness=0.4  error vocab.wrong: expected '', heard 'tienda'
farmer-open   "tengo un hacha"               intent=unknown communicated=false correctness=0.4  error vocab.wrong: expected '', heard 'tengo'
fisher-open   "tengo dos manzanas"           intent=unknown communicated=false correctness=0.4  error vocab.wrong: expected '', heard 'tengo'
smith-open    "me gusta el río"              intent=unknown communicated=false correctness=0.4  error vocab.wrong: expected '', heard 'gusta'
villager-open "me gusta el río"              intent=unknown communicated=false correctness=1    (no error: an Open prompt)
baker-open    "el río es grande"             intent=unknown communicated=false correctness=0.4  error vocab.wrong: expected '', heard 'río'
smith-open    "la panadería está cerca"      intent=unknown communicated=false correctness=0.4  error vocab.wrong: expected '', heard 'panadería'
smith-open    "tengo un hacha"               intent=state_have  communicated=true correctness=1
```
**The cause** (a design study's reading, verified by the orchestrator):
- `IntentResolver` (`src/Vocaterra.Nlp/Language/Intent/IntentResolver.cs`, about lines 130 to 148) considers only the prompt's `allowedIntentIds`, so `state_like` at `smith-open` falls to `unknown`.
- `VerdictAssembler.DeriveVocabWrong` (`src/Vocaterra.Nlp/Evaluation/VerdictAssembler.cs`, about lines 251 to 268, M67's rule) then adds `vocab.wrong` on the first content word: no match, a Constrained prompt, and an unknown intent.
- Its expected word is empty because every answer set in `game-v1` has more than one canonical answer (the help and «no sé» rows). Its weight 0.6 (`EvaluationConfig.cs`, about line 17) leaves correctness 0.4.

## The milestone
Put the milestone's lines in `scripts/try-lines/m72.txt` (`prompt|line`, no spaces around `|`): the table above, the clean neighbours and the near misses. Run `scripts/try-lines.sh --host` on them before your first edit, so the report can show before and after.

### (a) The derived `vocab.wrong` only where it explains a miss
Derive it only when it can name the word, or when the line is a near miss of an answer:
- "Name the word" means a one-answer set, or a quiz.
- A near miss is M67's `IsNearMiss`, as with `cuál es esto`.

A correct line that is simply not among the prompt's answers gets no error and keeps `communicated=false`. Keep M67's cases:
- a near miss keeps its tag;
- an understood line still never gets one.

Check that a lesson step's miss (a line that answers another question) is still not communicated; the game already retries it (V14).

### (b) The intent the prompt does not take
When the allowed pass gives `unknown` at a prompt that has allowed intents, run a second pass of the resolver over the whole catalog. Use the grammar and keyword rungs only, with no embedding: the resolver matches its candidates' grammars itself, so no second analysis is needed.
- Expose the result as a new optional member `IntentResolution.OffPromptIntent` and `Verdict.OffPromptIntent` (`IntentId?`).
- Leave it out of the JSON when null, so earlier verdicts and the goldens serialize as before.
- `Intent` and `Communicated` stay exactly as they are. Lesson steps, quests, credit and the repair ladder are unchanged in the core; the game decides the reply (V27).
- A quiz prompt with no intents skips the pass.
- `Verdict.Equals` and `GetHashCode` are written by hand: add the member to both.
- Show it in the CLI's `eval` output.
- Update `docs/nlp/API.md` (the M67 paragraph on the derived tag, and the new member).
- Add a dated note to `docs/nlp/adr/0009-core-and-game-boundary.md` in the style of M70's: one optional public member, null-omitted. Old replay logs with off-topic lines show a difference, as after M67.

### (c) `hace frío` is not a question about work
At `smith-open`, `hace frío` resolves to `ask_activity` because a keyword rule fires on `hacer` alone (the intent catalog in `content/game-v1`; `grep -rn '"ask_activity"' content/game-v1 | head`), so Domingo answers a remark about the weather with his job. Fix it at the source:
- Tighten the rule so `hacer` alone does not fire it (a question word or `qué` with `hacer`, for example).
- Keep every `ask_activity` row of the lessons and the lint's confusion check passing.
- If the fix is in `content/game-v1`, run `scripts/content-loop.sh --grammar --speech` directly (never under `scripts/limited.sh`), with `--record` if the goldens move and `--tier full` once. This is the one content edit this milestone may make.
- Tests: `hace frío`, `hace calor`, `hoy hace sol` are not `ask_activity` anywhere; `¿qué haces?` still is.

## Proof
- **`scripts/try-lines.sh --check` over every line file.** About 32 host rows with `comm=False vocab.wrong` will lose the tag. Every changed row must be an off-topic correct line (or gain `OffPromptIntent`), and every near miss keeps its tag. Update the `.expected` files in their own commit and list each change by kind in the report.
- **The identity baseline will move** (the grade and the pipeline stages). Re-record it with the golden project alone, in its own commit, and list each change:
  - first `VOCATERRA_RECORD_GOLDEN=1 VOCATERRA_RECORD_IDENTITY=1 scripts/test.sh project tests/Vocaterra.Nlp.Golden.Tests`;
  - then the same command again without the variables.
- No golden is expected to move; say if one does.
- **A game test that pinned the 0.4 or the tag** may be changed, as at M56, M62 and M67: change only the expected value, with a one-line comment naming M72. List each in the report, and change nothing else in the game.

## Fuzz (a widening, never a narrowing)
- **O10** (`tests/Vocaterra.Nlp.Fuzzing.Tests/VerdictAssemblyFuzzTests.cs`, about lines 174 to 204) models M67's rule. Make its model and generator follow the new rule; that follows a documented rule change and is not a narrowing.
- **Widen O7:** `OffPromptIntent` is null whenever the intent is known. When it is set, it is an intent the prompt does not allow, and it equals the resolver's answer with every intent allowed.
- **Widen O3 with a verdict leg:** every realised correct sentence, graded with `TextGrader` at an NPC's Constrained prompt that does not take its intent, gets no error that the sentence does not contain. Decide what "contains" means with care (a pronunciation tag is not grammar), and say how.
- Read `docs/nlp/FUZZING_PLAN.md` by `grep -n` for the oracles' numbering, and update it and the fuzz project's README for the widenings.
- A failure on one of the recorded seeds alone (`2tpLGUOSqj41` with `fSMdah3kOSd7`, `8yc522XLB_W2`) is rerun once after checking the seed. Any other failure is a finding: fix it if it is this milestone's, otherwise report it.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/game/DECISIONS.md`: D-game-22's two "Owner's answers, 2026-11-02" paragraphs (grep them).
- `docs/nlp/IMPLEMENTATION_PLAN.md`: §3, §6, the M67 and M72 rows of §5, M67's entry in §5b (`grep -n '^- \*\*M67'`).
- `docs/nlp/API.md` (`grep -n '^## \|^### '`): the verdict, the derived `vocab.wrong`, intent resolution.
- The code: `IntentResolver.cs`, `IntentResolution.cs`, `VerdictAssembler.cs`, `Verdict.cs`, `TextGrader.cs`, `UtterancePipeline.cs`, and their tests.

## Rules
- A core milestone edits only these:
  - the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects and their tests, `tests/golden`, `docs/nlp/` and `scripts/try-lines/`;
  - (c)'s one content edit;
  - a game test's pinned value, as above.
- It does not edit the game's code, `docs/game/` or `docs/qa-reports/`.
- Fix at the source, with regression tests in the owning module, each failing before the fix, and the clean neighbours as tests too. **Never weaken or narrow a fuzz oracle.**
- **Never flag correct Spanish.** Try a dozen natural, correct lines of your own at the NPCs' open prompts and at lesson prompts (names, greetings, `usted`/`ustedes`, the weather, likes, places), and list them.
- Determinism: no `System.Random`, no clock, ordinal comparisons.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files; prefer `grep -n`, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/m72/` and print at most 30 lines of it.
- Wait for a long command by running it in the foreground, not by polling a log. **Never leave a wait loop or a background process behind.**

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- **Never run `scripts/test.sh`, `scripts/try-lines.sh` or `scripts/content-loop.sh` under `scripts/limited.sh`:** they call it themselves. Run them directly.
- While iterating, use `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/m72/` for those (a CLI `eval` run); `scripts/try-lines.sh` holds them inside it.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Your checks before reporting (from the worktree root)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings; `scripts/test.sh full`, all passing (it was @@COUNT@@ passed and 3 skipped).
- The fuzz project three times at the default depth and once at five times (`VOCATERRA_FUZZ_ITERATIONS=15000 VOCATERRA_FUZZ_ITERATIONS_GRAMMAR=10000 scripts/test.sh project tests/Vocaterra.Nlp.Fuzzing.Tests`).
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- The core demo: `yo quiere dos espada` at `villager-open` still yields exactly `conj.person` and `noun.number`.
- `scripts/try-lines.sh --check` as in "Proof".
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 450 words)
- The branch and the final commit SHA, pushed.
- (a), (b) and (c): what was built and where, the public member, and the tests, with what each gave before.
- The `try-lines` rows that changed, by kind; your dozen correct lines.
- The identity entries and goldens that moved; the game tests changed (if any).
- Fuzz: O10's model, the O7 and O3 widenings, the four runs (time and result), and new findings.
- The test count before and after; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|limited.sh|content-loop'`, kill only your own) and that the worktree is clean and pushed.
