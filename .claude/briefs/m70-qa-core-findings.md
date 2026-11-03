You are implementing the core milestone **M70, the core findings of the QA pass on the village**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** Its findings come from `docs/qa-reports/2026-11-01-language.md` (read QA-language-2, 4, 6, 7 and 9, and the observations), reproduced by the orchestrator through the host, and from the owner's answers of 2026-11-01 (`docs/game/DECISIONS.md`, D-game-22, "Owner's answers, 2026-11-01 (on the QA passes …)", items 2 and 3).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m65` (reused; it holds a full build): cd there first, then `git checkout -b M70-qa-core-findings @@BASE@@` (the tip of `feature/village-followups` after M67). Never edit `/home/user/Dev/Vocaterra`, the worktree `.claude/worktrees/village` or any other worktree. **Other agents run at the same time** (V20, a game milestone, in the `village` worktree); every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs.
- **The owner wants work pushed often.** Commit after each item that builds and passes its tests, with the prefix `M70: `. Push after every commit: `git push -u origin M70-qa-core-findings` the first time, then `git push origin M70-qa-core-findings`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.
- **Disk is short (about 7 GB free).** Do not build the whole solution more than once; iterate with the test projects you change.

## The items (each with regression tests in the owning module, failing before the fix, with the clean neighbours as tests too; one commit each)
Measure every line on the host as well as the CLI (`scripts/try-lines.sh` and `--host`, the game's path); put them in `scripts/try-lines/m70.txt` and run them before your first edit.

1. **Typed `n` for `ñ` (the owner's answer, item 2).** `manana compraré pan` at `lesson-will-buy` gets `vocab.unknown` (expected `mañana`, correctness 0.6), and on the CLI `manana venderé fruta` loses its intent. A word typed with `n` for `ñ` is accepted as a dropped accent is (M26b, `docs/nlp/LEXICON.md`, "Words written without their accent"), **only when the spelling without `ñ` is not itself a word of the lexicon**. Accepted: `manana`, `pequeno`, `senor`, `espanol`, `nino`. Graded as today, because the plain spelling is a word: `ano`/`año`, `pena`/`peña`, `cana`/`caña`, `una`/`uña`, `mono`/`moño`, `suena`/`sueña`. Say how the lexicon is asked and where the fold lives.
2. **An accent that marks a verb's tense or person (the owner's answer, item 3).** `compre` is taken as `compré`, and `compro` as `compró`, on typed input, and stays accepted with no deduction; **add a note** naming the accented form (`compré, con tilde: es pasado`, or what fits the core's feedback text), so the learner sees why the accent matters. This is a public addition: decide its shape (a zero-weight note on the verdict, or a field the feedback can read), document it in `docs/nlp/API.md`, and add a dated note to `docs/nlp/adr/0009-core-and-game-boundary.md`. The game shows it later (V21); say in the report exactly what V21 must read.
3. **`gustar` (QA-language-4).** `yo gusto el pan` and `me gusto el pan` get correctness 1 at `lesson-like-bread` and from the farmer. `gustar` agrees with the thing liked, and the person who likes is a dative pronoun: `me gusto el pan` gives `conj.person` `gusta`; `me gusta las manzanas` gives `gustan` (check what it gives today); `yo gusto el pan` is flagged with `me gusta` expected (choose the tag and say why). Clean: `me gusta el pan`, `me gustan las manzanas`, `te gusta el pan`, `a mí me gusta el pan`, `no me gusta`, `¿te gusta?`, `nos gusta la fruta`, and `me gustas tú` (correct: `tú` is liked). The same for `encantar` if the lexicon has it.
4. **An unknown word blamed on its neighbour (QA-language-6).** `compré three manzanas` at `lesson-market` gives `vocab.unknown` with expected `compré`, heard `three`, and the NPC recasts «compré compré manzanas»; `manana iré al río` gives expected `al`, recast «al iré al río». The error must name the word that is unknown, with the expected word of the same position, so a recast never repeats a word. Find where the alignment between the line and the answer is made (the core builds the expected text; check whether the recast is the core's or the game's and say so). After item 1, `manana` is no longer unknown; keep a test with another unknown word in that place.
5. **The preterite's spelling changes (QA-language-9).** `buscé`, `pescé`, `llegé`, `empezé`, `pagé`, `jugé`, `tocé` are read as unknown words (`vocab.unknown`, sometimes a wrong expected word). They are the learner's slip for `busqué`, `pesqué`, `llegué`, `empecé`, `pagué`, `jugué`, `toqué` (the `c`/`qu`, `g`/`gu`, `z`/`c` changes before `e`). Grade them as the verb's slip with M58's machinery (`TryGetRegularizationOfWord`, the tag `conj.irregular` that `podo` gets), with the right form as expected. Clean: the right forms, and words that end in `-cé` or `-gé` legitimately.
6. **A learner's own name (QA-language-2).** `me llamo ana` at `farmer-open` gets "No te entendí" (`vocab.unknown` on `ana`, `vocab.wrong` on `llamo`); `me llamo domingo` is recast as «me llamo» (the name dropped). Find the cause: the name slot of the `me llamo` grammar (a `[WORD]` slot should bind any word of letters) or the vocabulary check of an unknown word inside a bound slot. If the cause is in the core, fix it; if it is the bundle's grammar (`content/game-v1/intents/intents.json`), do not edit it: report the exact change for the game's V21.
7. **`heard '2'`** (an observation): `dos panes es caro` at `farmer-open` reports `heard '2'` for the learner's `dos`. An error reports the learner's own word, not its normalised number.

## Fuzz
Widen where the realiser can model it (an `ñ` dropped from a word whose plain spelling is no word; `gustar` with its dative pronoun; a preterite spelling slip as a mutation), never narrow. A failure on a recorded seed alone (`2tpLGUOSqj41`, `8yc522XLB_W2`) is rerun once after checking the seed. Two analyzer findings came from M67b's runs on a widened O4 that is not merged: O4 `3isZGy3IeUc8` (`vaya sentir` read as `ir` plus a bare infinitive) and O5 `deXmFwx4GzL4` (the recast `miedo un tu una exposición` keeps a gender error); if either reproduces on your branch, record it in the fuzz README for M69, do not fix it here.

## Proof
- `scripts/try-lines.sh --check`: only the lines this milestone fixes change; update the `.expected` files in their own commit and list every changed line.
- The goldens and the identity baseline: re-record them with the golden project alone (`VOCATERRA_RECORD_GOLDEN=1 VOCATERRA_RECORD_IDENTITY=1 scripts/test.sh project tests/Vocaterra.Nlp.Golden.Tests`, then again without the variables), in their own commit, each change listed.
- **Never flag correct Spanish:** a dozen natural, correct lines of your own at the lessons' and NPCs' prompts (with `ñ`, accents, `gustar`, names), listed.
- The core demo: `yo quiere dos espada` at `villager-open` still yields exactly `conj.person` and `noun.number`.
- The fuzz project three times at the default depth and once at five times (`VOCATERRA_FUZZ_ITERATIONS=15000 VOCATERRA_FUZZ_ITERATIONS_GRAMMAR=10000`).

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/qa-reports/2026-11-01-language.md`: QA-language-2, 4, 6, 7, 9 and the observations.
- `docs/game/DECISIONS.md`: D-game-22's "Owner's answers, 2026-11-01 (on the QA passes …)".
- `docs/nlp/IMPLEMENTATION_PLAN.md`: §3, §6, the M70 row of §5, and the M58, M66 and M67 entries of §5b (`grep -n '^- \*\*M58\|^- \*\*M66\|^- \*\*M67'`).
- `docs/nlp/API.md` and `docs/nlp/LEXICON.md`: the sections on accents, unknown words, irregular forms and the person checks (`grep -n '^## \|^### '`).
- `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md`: grep, never whole.

## Rules
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, their tests, `tests/golden`, `docs/nlp/` and `scripts/try-lines/`. It does not edit `content/`, `docs/game/` or `docs/qa-reports/`; a content change it needs is reported for the game's V21.
- Fix at the source with regression tests in the owning module. **Never weaken or narrow a fuzz oracle.**
- Determinism: no `System.Random`, no clock, ordinal comparisons.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.

## Output, context and resources
- Read the sections named, not whole files; prefer `grep -n`, and keep `sed -n` ranges under about 150 lines. Heavy output to log files under `/tmp/claude-1000/m70/`, at most 30 lines printed.
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). `scripts/test.sh` and `scripts/content-loop.sh` run directly (they call it themselves). Never start two heavy commands at once: no `&`, no background builds. Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`. The sandbox refuses a command line containing `eval` or `hash`. Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Your checks before reporting
- The projects you changed build with `-warnaserror`, 0 warnings; their tests pass (`scripts/test.sh project <name>`), with `tests/Vocaterra.Game.Core.Tests` and `tests/Vocaterra.Host.Tests` run once at the end (a game test that pinned an old grade is reported, not changed).
- `dotnet format --verify-no-changes` on each project you changed.
- The proof above. The orchestrator's gate then runs the whole solution.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 450 words)
- The branch and the final commit SHA, pushed.
- For 1 to 7: what was built, where, the rule's limits, the tests and the result before and after; for 2, exactly what V21 must read; for 6, the cause and, if it is content, the change for V21.
- The `try-lines` lines that changed; your dozen correct lines.
- The goldens and identity entries that moved.
- Fuzz: what was widened, the four runs, findings.
- The test counts before and after.
- Anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|limited.sh'`, kill only your own) and that the worktree is clean and pushed.
