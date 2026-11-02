You are implementing the core milestone **M66, the grading gaps a learner of sessions 7 to 10 meets**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** It is a follow-up after PR #20's merge (`docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 B; read it), and folds in the filed M63. The game's free-talk milestone V20 waits on it.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m65` (M65's worktree, reused so its build is reused): cd there first, then `git checkout -b M66-grading-gaps b45684c` (M65's final commit, which brings `scripts/try-lines.sh` and the fuzz narrowings; it is being merged into `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra`, the worktree `.claude/worktrees/village` or any other worktree. **Other agents run at the same time**; every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes its tests, with the prefix `M66: `. Push after every commit: `git push -u origin M66-grading-gaps` the first time, then `git push origin M66-grading-gaps`.
- Never push `main` or `feature/village-followups`, and do not merge.
- **Disk is short (about 11 GB free; each core project's build output is about 400 MB).** Build only the projects you need: `tests/Vocaterra.Nlp.Es.Tests`, `tests/Vocaterra.Nlp.Tests`, `tests/Vocaterra.Nlp.Golden.Tests`, `tests/Vocaterra.Nlp.Fuzzing.Tests`, `tools/Vocaterra.Nlp.Cli`, `src/Vocaterra.Host`, and `tests/Vocaterra.Game.Core.Tests` for the save fixture. Never the whole solution: the orchestrator's gate builds and tests it afterwards.

## The milestone (each fix with regression tests in `tests/Vocaterra.Nlp.Es.Tests`, failing before the fix, with the clean and still-flagged neighbours as tests too)
**Measure on the host** (`scripts/try-lines.sh --host`, the game's path, which takes out a greeting and the listener's name first) as well as the CLI: the QA pass found they differ on such lines, and the learner meets the host's. Try every line with `scripts/try-lines.sh` first (put them in a new line file `scripts/try-lines/m66.txt`, `prompt|line`, at the prompts a learner would say them: `market-*`, `baker-open`, `smith-open`, `lesson-*`; grep `content/game-v1/prompts/prompts.json` for the ids).

### False alarms first (correct Spanish flagged)
1. **`¿puede usted vender manzanas?` gives `conj.person` (expects `vende`).** Cause: `CheckSubjectVerb`'s bare-infinitive rule (`yo comprar` gives `compro`, `SpanishAgreementChecker.cs`) fires on a subject pronoun after its verb. A subject pronoun right after a finite verb is that verb's subject (an inverted question), and the infinitive after it belongs to the verb. Clean: `¿puede usted vender manzanas?`, `puede usted vender manzanas` (transcripts have no punctuation), `¿quiere usted comprar pan?`, `¿va usted a comprar fruta?`, `¿pueden ustedes venir mañana?`. Still flagged: `usted vender manzanas`, `yo comprar pan`. This matters most: `poder` is session 10's verb and `usted` its register.
2. **`ni tú ni yo fuimos` and `tú o yo vamos` are flagged** (M64 left them). Extend M64's coordination (`docs/nlp/API.md`, "Person and copula errors"): `ni … ni` takes the person rule of `y` (plural); `o` accepts the plural by that rule and also the form of the member nearest the verb. Clean: `ni tú ni yo fuimos`, `ni luz ni mar fueron al río`, `tú o yo vamos`, `tú o yo voy`, `luz o mar va`, `luz o mar van`. Keep M64's clause coordination unchanged.

### Missed errors a learner of these lessons makes
3. **A finite verb after a modal:** `puedo pago con fruta`, `quiero vendo madera`. After `poder`, `querer`, `deber`, `necesitar` and `tener que`, a finite form of a lexicon verb is reported with its infinitive as `expected` (`pago` gives `pagar`), with the tag and shape `ir a` with a finite verb already gives (`voy a como` gives `comer`; `docs/nlp/API.md`, "Tense errors", line about 462), so the learner meets one kind of message. Flagged: `puedo pago con fruta` (`pagar`), `quiero vendo madera` (`vender`), `tengo que voy al río` (`ir`), `necesito compro pan` (`comprar`), `debo como más fruta` (`comer`). **Homographs stay clean** where the word has a reading the verb can take as an object: `quiero vino`, `necesito pago`, `quiero pan`, `quiero dos panes`, `debo dinero`; and a clause after `que` (`quiero que vengas`) is not this rule. `poder` and `tener que` take no noun object, so after them a finite reading may win; say in the report how you decided each homograph.
4. **`¿cuánto cuestan todo?` gives no `conj.person`.** M56's price check agrees `costar` with the noun phrase after it; extend it to `todo`, `eso` and `esto`, which are singular: `cuestan todo` gives `cuesta`, `¿cuánto son todo?` gives `es`. Clean: `¿cuánto cuesta todo?`, `¿cuánto es todo?`, `¿cuánto cuesta eso?`, `¿cuánto cuestan las manzanas?`, `¿cuánto son las manzanas?`. (The game's grammar for `¿cuánto son todo?`, intent `unknown` today, is V20's.)
4b. **`las manzanas cuesta una moneda` is praised** (the QA pass, `docs/qa-reports/2026-11-01-language.md` QA-language-3, reproduced by the orchestrator through the host at `farmer-open`: `ask_price`, correctness 0.4 from the `vocab.wrong` M67 removes, no agreement tag, the NPC says «las manzanas cuesta una moneda». ¡Muy bien!). A plural noun subject before a singular `costar` (and `valer`) gives `conj.person` `cuestan`; `un pan cuestan dos monedas` already gives `cuesta`, so find why the plural subject is missed (M59's person check after a noun subject; M56's price check). Flagged: `las manzanas cuesta una moneda`, `los panes cuesta dos monedas`, `las manzanas es roja` (already). Clean: `las manzanas cuestan una moneda`, `la manzana cuesta una moneda`, `cada manzana cuesta una moneda`, `dos manzanas cuestan dos monedas`.
5. **`los panes costa`** (M58 left it): the slip reading of `costa` after a phrase that opens the line holds only for the singular (`el pan costa` gives `cuesta`). Extend it to the plural, giving `cuestan`. Clean: `vivo en la costa`, `la costa es bonita`, `los panes cuestan dos monedas`.
6. **`azuls`** (M58 left it): the plural-slip rule (M58, `TryGetPluralSlip`) covers nouns only. Extend it to adjectives that end in a consonant (`azuls` gives `azules`, `las casas azuls`), with the same tag as the noun slip. Clean: `azules`, `las casas azules`.
7. **`ti` is not in the lexicon** (`para ti` gives `vocab.unknown`): add it as a prepositional pronoun, following `docs/nlp/LEXICON.md` ("Merge policy", "Words the content uses and the lexicon lacked") and the way `mí` is held. The lexicon is `content/game-v1/lexicon/`, so this is a bundle edit: see "The bundle" below. Clean: `es para ti`, `tengo pan para ti`.
8. **M63, as filed** (`docs/nlp/IMPLEMENTATION_PLAN.md` §5, the M63 row): a subject pronoun before a question word is lost to M56's check. `ellas dónde están` gives `conj.person` `está`. Keep a subject pronoun before the question word as the subject. Clean: `ellos dónde están`, `ellas adónde van`, `ella qué quiere`, `tú qué quieres`. Still flagged: `ellas dónde está`, `ella qué quieres`. Seeds `8zKtIhOY3951` (O5) and `c5SlpISRmTZ3` (O3) must pass.

### Fuzz
Widen where the realiser can model it, never narrow: for example let the realiser build `ni … ni` and `o` subjects (the `skip:coordinated-subject` re-roll), a pronoun before a question word, and a mutation that turns the infinitive after a modal into a finite form (a new O4/O5 case, like `ir a`'s). Say what you widened and what you could not, and why. The skip budget test (M65) must keep passing. A failure on one of M65's recorded seeds alone, (d) `2tpLGUOSqj41` or the older `8yc522XLB_W2` (O4, `word_order`, found by M65 and recorded in the README), is rerun once after checking the seed; any other failure is a finding.

### The bundle (for `ti` only; nothing else in `content/` changes)
- After the lexicon edit, run `scripts/content-loop.sh --tier none` (directly, never under `limited.sh`; read its header, lines 1 to 45). Steps 1 to 4 refresh the manifest and hash, run the content checks and put the new hash into the save fixtures (`tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json`, the one game file this milestone may change). **Do not pass `--record` or another tier:** those build the whole solution (step 5), which the disk cannot take in this worktree.
- The end-to-end goldens may move (for `ti` and for the other fixes): re-record them with the golden project alone, `VOCATERRA_RECORD_GOLDEN=1 VOCATERRA_RECORD_IDENTITY=1 scripts/test.sh project tests/Vocaterra.Nlp.Golden.Tests`, then run it again without the variables; commit them in their own commit, and list in the report every entry that moved and why it is now right.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 B (`grep -n '^\*\*B\. ' docs/HANDOVER_VILLAGE_PHASE_B.md`, about 45 lines).
- `docs/nlp/IMPLEMENTATION_PLAN.md`: §3, §6, the M56 to M66 rows of §5 (`grep -n '^| M5[6-9]\|^| M6'`), and the M64 entry of §5b.
- `docs/nlp/API.md`: "Tense errors", "Person and copula errors", "Irregular forms, plurals and unknown words" and "Adjectives" (`grep -n '^### '`).
- `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md`: grep for `coordinated-subject`, `M64`, `M65` and the narrowed oracles. Do not read it whole.
- The code: `src/Vocaterra.Nlp.Es/Analysis/SpanishAgreementChecker*.cs` (`CheckSubjectVerb`, the coordination M64 added, the price check of M56), the `ir a` + finite check (grep `voy a como` or the tense checker), M58's `TryGetPluralSlip`, and their tests.

## Rules
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, their tests, `tests/golden` and `docs/nlp/`; this one also adds `scripts/try-lines/m66.txt` (and updates `scripts/try-lines/*.expected` for the lines it fixes), edits `content/game-v1/lexicon/` for `ti` only, and takes the new hash into `content/game-v1/manifest.json` and the save fixture. It does not edit the game's projects, any other part of `content/`, `docs/game/` or `docs/qa-reports/`.
- Fix bugs at the source, with a regression test in the owning module. **Never weaken or narrow a fuzz oracle.** A new real bug the fuzz suite finds: fix it if small, or record it in the README with its seed and report it.
- **Never flag correct Spanish.** Before reporting, run `scripts/try-lines.sh --check` (every line file in `scripts/try-lines/` against its `.expected` and `.host.expected`, both modes; read the script's header); only the lines this milestone fixes may change from their `.expected`, apart from the known `vocab.wrong` at constrained prompts (M67's). Then try a dozen natural, correct lines of your own at the NPCs' prompts (names, greetings, `usted`/`ustedes`, modals with infinitives, questions with inverted subjects) and list them.
- Determinism: no `System.Random`, no clock, ordinal comparisons.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- Update `docs/nlp/API.md` where it lists what each tag reports, and add a dated note to `docs/nlp/adr/0009-core-and-game-boundary.md` only if the public surface changes.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files; prefer `grep -n`, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/m66/` and print at most 30 lines of it.
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log.
- Run the fuzz project (`scripts/test.sh project tests/Vocaterra.Nlp.Fuzzing.Tests`) as soon as the checker changes, not only at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` and every `scripts/content.sh` call through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`); `scripts/content-loop.sh` runs directly. Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh project <test project>`. Do not run `scripts/test.sh fast` or `full` (they build the whole solution).
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`; `scripts/try-lines.sh` and `scripts/content-loop.sh` hold those words inside them.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Your checks before reporting (from the worktree root, each through scripts/limited.sh)
- Each project above builds with `-warnaserror`, 0 warnings, and its tests pass (`scripts/test.sh project <name>`): Es, Nlp, Golden, Fuzzing (three runs at the default depth and one at five times, `VOCATERRA_FUZZ_ITERATIONS=15000 VOCATERRA_FUZZ_ITERATIONS_GRAMMAR=10000`), Game.Core.
- `dotnet format --verify-no-changes` on each project you changed.
- The core demo: `yo quiere dos espada` at `villager-open` still yields exactly `conj.person` and `noun.number`.
- `scripts/try-lines.sh` as in "Rules"; `scripts/content.sh validate`.
- Do not commit `bin/`, `obj/` or `artifacts/`. The orchestrator's gate then runs the whole solution's build and tests, format, the content gate, grammar and the licence scan.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- For 1 to 8 and 4b: what was built, the rule's limits (the homographs of 3), the test, the result before and after.
- Fuzz: what was widened, the four runs (time, result), new findings.
- `try-lines`: the lines that changed, each explained; your dozen correct lines.
- The goldens that moved and why; the new bundle hash.
- The test counts of the projects you ran, before and after.
- Anything left undone, and why.
