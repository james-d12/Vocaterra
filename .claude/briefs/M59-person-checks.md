You are implementing the core milestone **M59, the person and tense checks the third person needs**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** The owner asked on 2026-10-31 for M56 and M59 to land before the game's V14 (session 8, the preterite for every person, with named villagers as subjects) and M58 before V15 (session 10, the market). M56 (agreement with a postverbal subject, `ser` with places) has just landed: build on it.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `@@HEAD@@`).
- Create your branch with `git checkout -b M59-person-checks feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `M59: `. Push after every commit: `git push -u origin M59-person-checks` the first time, then `git push origin M59-person-checks`.
- Never push `main` or `feature/village`, and do not merge.

## The milestone (its row in `docs/nlp/IMPLEMENTATION_PLAN.md` §5, M59)
Found by the game's V12 (2026-10-31), each tried on the CLI at a prompt of the game bundle:
- **(a) `conj.person` looks only after a subject pronoun.** A name or noun subject before the verb is not checked: `Domingo trabajo en la herrería` and `Domingo voy a hacer un hacha` give nothing, where `él trabajo …` gives `conj.person`. Check a noun or name subject against its verb, in every tense (`Luz hice pan`, `Mar pescaste en el río`, `Luz y Domingo comió pan`, `los niños come pan`), and `ser` with a noun subject, which V13 found silent: `las manzanas es roja` (expected `son`), `la manzana son roja` (expected `es`). The game's villagers are named with nouns (`domingo`, `luz`, `mar`, `león`: all third person singular in the lexicon); a name the lexicon lacks binds no reading and has nothing to say. Keep clean: `Domingo trabaja en la herrería`, `Luz hizo pan`, `Mar pescó en el río`, `Luz y Domingo comieron pan`, a noun that is not the subject (`el pan quiero` if the core reads it so; `en la herrería trabaja Domingo`), a vocative (`Domingo, ¿qué haces?`), and a noun object before a clitic (`el pan lo compré`).
- **(b) A reflexive clitic is not matched to its verb's person:** `cómo te llama` and `cómo se llamas` give nothing (expected `llamas` and `llama`). `me llamo`, `te llamas`, `se llama`, `nos llamamos` and `se llaman` are clean; a clitic that is an object of another person (`te veo`, `me ves`) is not a reflexive and stays clean.
- **(c) `ir a` followed by a finite present is not flagged:** `va a hace un hacha` gives nothing, where `va a hizo` gives `conj.tense` (expected `hacer`). `voy a como` is already flagged (`docs/nlp/API.md`, "A conjugated verb after `ir a`"); find why `hace` escapes (it is also read as `hace` + a time, part (d)) and flag it; a noun phrase after `a` stays clean.
- **(d) A literal word in a slot grammar binds its first reading,** so `hace` in `qué hace` is read as the preposition-like `hace` of `hace dos días`, and the verb is hidden from the host's person check (the game reads the words' readings). Make the bound reading the one the sentence needs (the verb, when no time noun follows); show it with a test on a grammar with a literal `hace`.
- Regression tests in the owning modules (the Spanish pack's analysis for (a) to (c), the engine's slot grammar or reading resolver for (d)), each failing before the fix, with the clean neighbours above as tests too. The fuzz oracles unchanged or widened, never narrowed: (a) widens what the correct-sentence oracle may expect to be flagged, so check the generator does not produce a noun subject with a verb of another person and count it correct.
- `docs/nlp/API.md` stays true: where it says what `conj.person` and `conj.tense` report, add the new cases.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/nlp/IMPLEMENTATION_PLAN.md` §3, §4, §6, the M56 to M61 rows of §5 and the M60 and M61 entries of §5b (models of a core milestone found by the game; `grep -n` for them).
- `docs/nlp/CONTRIBUTING.md`, `docs/nlp/API.md` (the error tags `conj.person`, `conj.tense`; "Tense errors" for the style), `docs/nlp/FUZZING_PLAN.md` (the oracles), `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` (bugs found, known limits, narrowed oracles).
- `docs/nlp/adr/0009-core-and-game-boundary.md` and ADR 0012 (the engine and the Spanish pack; `LayeringTests`).
- The code: `src/Vocaterra.Nlp.Es/Analysis/SpanishAgreementChecker.cs`, `SpanishAnalysisTables.cs` and whatever else reports `conj.person` and `conj.tense`; `src/Vocaterra.Nlp` where a slot grammar's literal word binds its reading (`Language/SlotGrammar` and the reading resolver); their tests.
- The game's notes on these slips, so you know the sentences the content wants graded: `grep -n 'M59\|meanwhile\|pronoun' content/PHRASES.md` (do not read it whole; do not edit it).

## Rules
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, their tests, `tests/golden` and `docs/nlp/`. It does not edit the game's projects, `content/game-v1`, `content/locale`, `content/PHRASES.md` or `docs/game/`. If a core test on `content/game-v1` breaks because the content relied on the old grade, stop and report it.
- Fix bugs at the source, with a regression test in the owning module. **Never weaken or narrow a fuzz oracle**; widen it or leave it. If the fuzz suite finds a new real bug, fix it or report it, and add it to the fuzz README's list.
- Determinism: no `System.Random`, no clock.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- The end-to-end goldens may move because the core now grades a learner line differently: re-record them (`VOCATERRA_RECORD_GOLDEN=1`, `tests/Vocaterra.Nlp.Golden.Tests`) in their own commit, and list in the report every entry that moved and why it is now right.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files; prefer `grep -n` to find a place, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/` and print at most 30 lines of it (the summary, `grep` for failures).
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log.
- Run the fuzz suite's project (`scripts/test.sh project tests/Vocaterra.Nlp.Fuzzing.Tests`) as soon as the checker changes, not only at the full run.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh project <test project>` or `scripts/test.sh fast`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/` for a CLI `eval` run (`dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "<text>" --prompt <prompt> --content content/game-v1`).
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing (it was @@COUNT@@); report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `bash scripts/license-scan.sh --offline` PASSED.
- The core demo: `dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1` still yields exactly `conj.person` and `noun.number` (through the wrapper).
- The CLI at the game's prompts: `Domingo trabajo en la herrería` at `lesson-works` and `Luz hice pan` at a prompt that takes `state_action` give `conj.person`; `cómo te llama` at `smith-open` gives `conj.person`; `va a hace un hacha` at `lesson-will-make` gives `conj.tense`; `Domingo hizo un hacha` at `lesson-made`, `¿cómo te llamas?` at `smith-open` and `¿qué haces?` at `farmer-open` stay clean, and the host's `--transcript "¿qué hace Domingo?" --prompt <a prompt that takes ask_activity>` shows `hace` read as the verb. (If a prompt id differs, find it with `grep`.)
- `scripts/content.sh validate` passes (the bundle is unchanged).
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyPc3eAsDgGyBVKv6
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- What was built for (a) to (d), and where each fix lives.
- The regression tests and what each gave before the fix.
- The CLI results of the gate's lines.
- Fuzz findings, if any; oracles changed (only widened) or not.
- The test count before and after; goldens re-recorded and why each entry moved.
- Which of the game's "meanwhile" examples in `content/PHRASES.md` the core now grades, so the game can restore them.
- Anything left undone, and why.
