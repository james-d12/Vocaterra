You are implementing the core milestone **M62, two adjective checks that are silent where a learner errs**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** The owner asked on 2026-10-31 for M62 to land before the game's V15 (session 10, "El mercado"), whose `es muy caro` and `más barato que` are predicate adjectives a learner will get wrong. It builds on M56 (agreement with a subject after its verb, `PostverbalSubjectTests`), which is merged.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `@@HEAD@@`).
- Create your branch with `git checkout -b M62-adjectives feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `M62: `. Push after every commit: `git push -u origin M62-adjectives` the first time, then `git push origin M62-adjectives`.
- Never push `main` or `feature/village`, and do not merge.

## The milestone (its row in `docs/nlp/IMPLEMENTATION_PLAN.md` §5, M62)
Found by the game's V13 (2026-10-31), each tried on the CLI at a prompt of the game bundle:
- **(a) A predicate adjective is not agreed with its subject across the copula.** `la manzana es rojo` (expected `roja`) and `el pan es grandes` (expected `grande`) give no `gender.agree` or `noun.number`, where the attributive `quiero tres manzanas rojos` gives `gender.agree` correctly. Agree an adjective after `ser` and `estar` with the subject, before or after the verb, with an adverb between (`muy`, `más`, `menos`, `tan`) and in a comparison (`más barato que`, `tan caro como`):
  - flagged: `la manzana es rojo`, `el pan es grandes`, `las manzanas son caro` (expected `caras`; report the tags the core's attributive check gives for the same double slip), `la manzana es muy caro`, `la manzana es más barato que el pan` (expected `barata`), `la sopa está frío`, `nosotros somos alto` (expected `altos`);
  - clean: `la manzana es roja`, `las manzanas son caras`, `el pan es muy caro`, `los panes son más baratos que las manzanas`, `es caro` (no subject), `es muy caro` at a prompt about an item (the core must not guess a subject it does not have), `yo soy alto` and `yo soy alta` (the speaker's gender is unknown), `la casa es de Luz`, `el pan es para Luz`, and M56's `es lejos` (`ser_estar`, unchanged);
  - say what the check does when the subject is a pronoun with a gender (`ella es alto`) and when it is a name (`Luz es alto`: the core may not know a name's gender; do not guess one).
- **(b) An adjective before its noun is not understood.** `quiero tres rojas manzanas` and `quiero una roja manzana` resolve no intent, and with a grammar that matches them give no `word_order` (expected `manzanas rojas`). Colour adjectives, which do not go before the noun in neutral Spanish, are the case to flag; `una gran casa`, `un buen pan`, `la primera casa`, `muchas manzanas`, `otra casa` stay clean (they go before the noun). Decide, and say why, whether the intent should still resolve with the `word_order` error (the learner was understood) as the core does for its other order slips; follow the core's existing `word_order` design (`docs/nlp/API.md`).
- Regression tests in the owning module (the Spanish pack's analysis, `src/Vocaterra.Nlp.Es/Analysis/`), each failing before the fix, with the clean neighbours above as tests too. The fuzz oracles unchanged or widened, never narrowed: if the generator produces predicate adjectives or pre-nominal adjectives, its idea of a correct sentence must agree with the new checks.
- `docs/nlp/API.md` stays true: where it lists what `gender.agree`, `noun.number` and `word_order` report, add the new cases.

## Read first (the sections named, not whole files)
- `CLAUDE.md`.
- `docs/nlp/IMPLEMENTATION_PLAN.md` §3, §4, §6, the M56 to M62 rows of §5 and the M60 and M61 entries of §5b (models of a core milestone found by the game; `grep -n` for them).
- `docs/nlp/CONTRIBUTING.md`, `docs/nlp/API.md` (the error tags `ser_estar`, `conj.person`; "Tense errors" for the style), `docs/nlp/FUZZING_PLAN.md` (the oracles), `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` (bugs found, known limits, narrowed oracles).
- `docs/nlp/adr/0009-core-and-game-boundary.md` and ADR 0012 (the engine and the Spanish pack; `LayeringTests`).
- The code: `src/Vocaterra.Nlp.Es/Analysis/SpanishAgreementChecker.cs` (the attributive agreement, and what M56 added for postverbal subjects), `SpanishAnalysisTables.cs`, whatever reports `word_order`, the lexicon's adjective entries (which adjectives go before the noun: `gran`, `buen`, `primer`, ordinals, quantifiers), and their tests.
- The game's notes on these slips, so you know the sentences the content wants graded: `grep -n 'M62\|meanwhile' content/PHRASES.md` (do not read it whole; do not edit it).

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
- The CLI at the game's prompts: `la manzana es rojo` at the farmer's prompt gives `gender.agree`; `el pan es muy caro` at `baker-open` stays clean and `la manzana es muy caro` gives `gender.agree`; `quiero tres rojas manzanas` at the farmer's prompt gives `word_order` (and the intent, if you chose so); `quiero tres manzanas rojas` and `me gustan las manzanas` stay clean. (If a prompt id differs, find it with `grep`.)
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
- What was built for (a) and (b); the choices for pronoun and name subjects and for the intent with `word_order`, and why.
- The regression tests and what each gave before the fix.
- The CLI results of the gate's lines.
- Fuzz findings, if any; oracles changed (only widened) or not.
- The test count before and after; goldens re-recorded and why each entry moved.
- Which of the game's "meanwhile" examples in `content/PHRASES.md` the core now grades, so the game can restore them.
- Anything left undone, and why.
