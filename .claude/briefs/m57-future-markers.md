You are implementing the core milestone **M57, the later future markers in the fuzz generator and `pasado mañana` after a plural noun**, of the Vocaterra repository (C#, .NET 10). The language core is the `Vocaterra.Nlp` engine (`src/Vocaterra.Nlp`), the Spanish pack `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` test and tool projects; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the core.** The game's milestone V17 (session 9, "Mañana en el pueblo", the simple future) waits on parts (a) and (b) before the bundle's `state_action` grammar may take the later markers (`la semana que viene`, `el próximo día`, `pasado mañana`).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the existing git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` itself or any other worktree. It is on branch `feature/village` (at the HEAD `git log -1` shows, `71573ba`).
- Create your branch with `git checkout -b M57-future-markers feature/village`.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes `scripts/test.sh fast`, with the prefix `M57: `.
- Push after every commit: `git push -u origin M57-future-markers` the first time, then `git push origin M57-future-markers`.
- Never push `main` or `feature/village`, and do not merge.

## The milestone (its row in `docs/nlp/IMPLEMENTATION_PLAN.md` §5, M57)
Found by the game's V10 (2026-10-30):
- **(a)** The fuzz suite's Spanish sentence generator knows only `mañana` as a future time marker (`tests/Vocaterra.Nlp.Fuzzing.Tests/Generation/LexiconCatalog.cs`, `FutureTimeAdverbs`; used by `SentenceRealizer`). So a bundle grammar with `la semana que viene` or `el próximo …` makes it produce sentences it counts as correct (a preterite after the marker) which the core rightly flags `conj.tense`. Teach the generator every future marker `docs/nlp/API.md` "Tense errors" lists (`mañana`, `pasado mañana`, `próximo/a` before a time noun, a time noun followed by `que viene`), and the past markers likewise if the same gap is there (`ayer`, `anoche`, `anteayer`, a time noun with `pasado`), so that its oracle's idea of a correct sentence agrees with the core's tense check, from the same list. Multi-word markers are phrases, not words: the generator must recognise them in a grammar's literal words as phrases.
- **(b)** `pasado mañana` after a plural noun is read as an adjective of the noun: `compro panes pasado mañana` gives `noun.number` (`pasado` taken as agreeing with `panes`). Read `pasado mañana` as the time phrase, wherever it stands; `la semana pasada` and `el año pasado` keep their past reading. A regression test in the owning module (the Spanish pack's analysis, `src/Vocaterra.Nlp.Es/Analysis/`, where `SpanishAgreementChecker` finds the markers), with the neighbours: `compro panes pasado mañana`, `pasado mañana compro panes`, `pasado mañana compré panes` (`conj.tense`), `compré panes la semana pasada` (clean), `el pan pasado` (the adjective, unchanged).
- **The game's side, for reference (do not edit it):** V16 gave the game's lesson runner the later markers as phrases (`content/game-v1/game/words.json`, `nearFuture.timeWords`: `semana que viene`, `próxima semana`, `próximo día`, `mes que viene`, `próximo mes`, `año que viene`, `próximo año`, `luego`, `después`, `pasado mañana`; matched by `NearFutureWords.Mark`). The core's list stays the source of truth: report any marker the game has that the core does not know, or the reverse, so the game can follow.
- **(c), do it first.** `FuzzLanguageStreamTests`' two stream digests depend on `content/game-v1` and have been re-recorded at V10, V11 and V12 (and V13, V14 and V16), generators unchanged: every content milestone pays a re-recording that tells nothing about the generator. Pin them to a fixed fixture (a small bundle under the fuzz test project, or a frozen copy of the inputs they read) so that a content edit no longer moves them and a generator change still does; the test's comment and the fuzz README say what the digest guards. Only if pinning cannot keep what the digests guard, document them as content-moved goldens with the rule for re-recording, and say why. Part (a) then changes the generator, so the pinned digests move once more, in their own commit: say so in it.

## Read first
- `CLAUDE.md`.
- `docs/nlp/IMPLEMENTATION_PLAN.md` §3, §4, §6, the M56 to M61 rows of §5 and the last entries of §5b (M60, M61 are the models of a core milestone found by the game).
- `docs/nlp/CONTRIBUTING.md`, `docs/nlp/API.md` ("Tense errors"), `docs/nlp/FUZZING_PLAN.md`, `tests/Vocaterra.Nlp.Fuzzing.Tests/README.md` (bugs found, known limits, narrowed oracles).
- `docs/nlp/adr/0009-core-and-game-boundary.md` and ADR 0012 (the engine and the Spanish pack; `LayeringTests`).
- The code: `tests/Vocaterra.Nlp.Fuzzing.Tests/Generation/LexiconCatalog.cs`, `SentenceRealizer.cs`, `CorrectSentenceFuzzTests.cs`, `FuzzLanguageStreamTests.cs`; `src/Vocaterra.Nlp.Es/Analysis/SpanishAgreementChecker.cs` (the future and past markers), `SpanishAnalysisTables.cs`, and whatever reads `pasado` as an adjective in the agreement check.

## Rules
- **Try the change on correct learner lines, not only the brief's** (M59's first build flagged `Luz quiero pan`): before reporting, run the CLI on a few natural, correct lines with the markers at the NPCs' prompts (`mañana voy a comprar pan`, `la semana que viene compro pan`, `el próximo día iré al río`, `pasado mañana compraré pan`) and show they are clean.
- **A game test that pins the old gap** (its comment names M57 or "a core gap") may be updated to the core's new grade, that test file only, in a commit of its own, in the file's own style (M56's `323890f` is the model); if the game then behaves in any other way, change nothing in the game's `src/` and report it.
- A core milestone edits only the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, their tests, `tests/golden` and `docs/nlp/`. It does not edit the game's projects, `content/game-v1`'s lessons, intents or grammars, `content/locale`, or `docs/game/`. (If a core test needs a bundle file changed, stop and report it.)
- Fix bugs at the source, with a regression test in the owning module. **Never weaken or narrow a fuzz oracle**; widen it or leave it. If the fuzz suite finds a new real bug, fix it or report it, and add it to the fuzz README's list.
- The marker lists live in one place if they can: if the core keeps them in a table the generator can read without breaking the layering (a test project may read the pack's public or internal-visible tables), use it, so that the generator and the checker cannot drift apart again; otherwise say why not.
- Determinism: no `System.Random`, no clock; the fuzz suite's seeded generators only.
- Do not edit either `IMPLEMENTATION_PLAN.md` or either `DECISIONS.md`; keep `docs/nlp/API.md` true if behaviour visible there changes (the "Tense errors" list).
- Never create or edit anything under `.github/workflows/`.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`).
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh project <test project>` or `scripts/test.sh fast`; the fuzz suite is `Speed=Slow`, so run its project alone when you change it. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files; prefer `grep -n` to find a place, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/` and print at most 30 lines of it (the summary, `grep` for failures).
- Wait for a long command by running it in the foreground through `scripts/limited.sh`, not by polling a log.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/` for a CLI `eval` run.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.

## Gate (from the worktree root, each through scripts/limited.sh)
- `dotnet build Vocaterra.sln -warnaserror -m:4`, with 0 warnings.
- `dotnet test Vocaterra.sln -m:2` all passing. It was 6642 passed and 3 skipped; report the new count.
- `dotnet format Vocaterra.sln --verify-no-changes`.
- `bash scripts/license-scan.sh --offline` PASSED.
- The core demo: `dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1` still yields exactly `conj.person` and `noun.number` (through the wrapper).
- The CLI on `compro panes pasado mañana` (clean), `pasado mañana compré panes` (`conj.tense`) and `la semana que viene compré pan` (`conj.tense`) at a prompt that takes `state_action`.
- If any golden moved, re-record it (`VOCATERRA_RECORD_GOLDEN=1`) in its own commit and explain why it moved.
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyPc3eAsDgGyBVKv6
```

## Report (under 400 words)
- The branch and the final commit SHA, pushed.
- What was built for (a), (b) and (c), and the choice for (c) with the reason.
- Where the marker lists live now, and whether the generator and the checker share them.
- The regression tests and what each failed with before the fix.
- Fuzz findings, if any; oracles changed (only widened) or not.
- The test count before and after; goldens or digests re-recorded, and why.
- What V17 may now put in the bundle's `state_action` (which markers are safe for the generator and the checker), and anything still unsafe.
- Anything left undone, and why.
