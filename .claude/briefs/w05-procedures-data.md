You are implementing the game milestone **W05, procedures, stations and actions as data; the `does` rows of the phrase sheet; the validator's rules**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It is data formats, loaders and validation only: no runner (W06), no UI (W07), no bread content (W10).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/w05`, already on the branch `W05-procedures-data` (from `feature/bread-pilot` at its tip). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`.claude/worktrees/adaptive` is the owner's playtest checkout: never touch it); do not switch branches.
- Commit after each item that builds and passes its tests, with the prefix `W05: `. **Do not push**: your branch stays local and the gate merges it from there (owner, 2026-11-05). Never push or merge `feature/bread-pilot`, `feature/adaptive_design` or `main`. When you finish, leave the worktree on your branch, clean.

## The specification
- `docs/game/activities-plan.md`: section 4.1 (`sed -n 97,106p`), section 6's paragraph **W05** (`sed -n 248,258p`), section 7's table (`sed -n 418,434p`), section 8's rows for `Activities/` and `Language/` (`sed -n 435,450p`), and line 469 (the validator's reuse report: optional, do it if cheap).
- The owner's design document, extracted one paragraph per line at `/tmp/claude-1000/orch/adaptation-design.txt`: **section 10.7 "The data" (`sed -n 793,833p`)** is the shape of the four file kinds (the `pan.json` example, `stations.json`, `actions.json`); section 10.16 (`sed -n 1002,1033p`) describes the `does` rows. Read only those ranges; do not edit the `.docx`. Where the design and the code disagree on a file or a name, the code wins; say so in the report.
- The owner adopted the design's section 18.2 recommendations provisionally (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"). List every value you choose where the design leaves one open under "values chosen" in the report.

## What to build
1. **Schemas** in `content/schema/`: `procedure.schema.json` (one `game/procedures/<id>.json`: `schemaVersion`, `id`, `station`, `teacher`, `level` `a1`|`a2`, `makes` with optional `bonus`, `uses` with optional `leaves`, `steps` with `id`, `row`, `do`, `what` and one of `where`/`from`/`with`, optional `anyOrder`; `chunks`; `goal`), `procedures.schema.json` (the list of procedure ids, as `lessons.json` lists lessons), `stations.schema.json` (`id`, `prop`, `npc`, `fixtures` of `noun` and `icon`), `actions.schema.json` (`use`, `on`, `verbs`). Follow the existing schemas' style (`lessons.schema.json`, `recipes.schema.json`). The `item` enum in `nouns.schema.json` is the list of item names: reuse it for `makes`, `uses` and `leaves`.
2. **The bundle**: `content/game-v1/game/procedures.json` (empty list), `stations.json` (empty), `actions.json` (empty), and no `procedures/` files, so the game loads and behaves exactly as before. Any file listing of the bundle (manifest inputs, the content README's file table) names them.
3. **The `does` kind** of the phrase sheet: `content/PHRASES.md` documents it (an instruction the NPC gives: Spanish, English, the NPC and situation, the support card, the audio line ids; no `intent`, no `prompt`, no learner error, `-` in those columns), `phrases.schema.json` accepts it, `PhraseSheet` reads it and the sheet's own tests (the rules that every `says` row has a graded learner-error example and so on) treat `does` rows correctly. Add **no** `does` rows to the real sheet: they are W10's.
4. **Loaders** in a new `src/Vocaterra.Game.Core/Activities/` folder: `ProcedureBook` (records `ProcedureDefinition`, `ProcedureStep`, `ProcedureChunk`, …), `StationBook`, `ActionTable` (lookup of the verbs for a `use`/`on` pair), modelled on `Language/Lessons/LessonBook.cs` and `Items/Crafting/RecipeBook.cs`; `Content/GameData.cs` loads them in its one load.
5. **Validator rules** in `Content/GameDataValidator.cs`, each with a clear message naming the file and the field: every `what`, `where`, `from` and `with` is a catalogue noun (a noun that names an item for what is held; a station fixture's noun for a station's things); every step's verb is in `actions.json` for its `use`/`on` pair; every verb lemma is in the lexicon with its tú imperative (use what the game already reads from the lexicon, e.g. `Language/Words/VerbReading.cs`; if the core gives no way to get a tú imperative, report it as a core gap and check only that the lemma exists); every step's, chunk's and goal's `row` is a `does` row whose line is in the bundle and recorded (the speech index, as lessons' lines are checked); chunks cover contiguous steps and no step belongs to two chunks; the station's `prop` exists in the scene; the `teacher` and the station's `npc` are NPCs; the procedure's `station` exists; `makes`/`uses` items exist; the procedure's Spanish is held to its `level` as lessons are (V08). Wire the rules into the host's `--validate-content` as the lesson rules are.
6. **Tests** (`tests/Vocaterra.Game.Core.Tests`, plus the host's if `--validate-content` changes): each loader on a small JSON string, including the design's `pan.json` example; each validator rule with one passing and one failing case on small in-memory data (no edit of the real bundle to make a case fail); the real bundle still loads and validates clean.

Out of scope: `ProcedureRunner`, saves, events, the station view, flour, any Spanish line, any WAV, any lexicon entry.

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/w05/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md` (grep its rows).
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 14 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload; the owner is playing the game on this machine)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; the full suite once before the report.
- Exit code 137 or 143 means a cap killed the command: lower the parallelism or rerun; never raise a cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/w05/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/w05/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
- After any edit of `content/game-v1` (step 2 is one): `scripts/content-loop.sh --speech` (it refreshes the hash, the save fixtures, the LanguageTool cache and the WAVs; `--record` when the goldens move; `--tier full` once before the report). Content outside game-v1's hash: `content/locale/`, `content/schema/`. How to edit content: `content/README.md`, "The edit loop".

## Rules
- **What this milestone edits:** the game's code and tests (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tests/Vocaterra.Game*.Tests`, `tests/Vocaterra.Host.Tests`), `content/game-v1`, `content/PHRASES.md`, `content/schema`, `content/README.md` and `docs/game/` (not the plans or the decisions).
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched; a core test that fails after a content edit is wanted feedback: report it.
- A regression check is a C# test, never a script with text baselines.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`). Logic in Core (`docs/process/code-guidelines.md`, `docs/game/architecture.md`).
- Do not edit either `implementation-plan.md`, `docs/game/activities-plan.md`, `docs/game/village-plan.md`, either `decisions.md`, `scripts/merge-gate.sh`, the design `.docx`, or anything under `.claude/` or `.github/workflows/`.
- Determinism: no `System.Random` and no wall clock. Assets CC0 only (D-game-5). Every save version from 1 still loads.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/content-loop.sh --speech --tier full`, all passing: 9586 passed before you; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; the lint at A2 clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`); the host's `--validate-content` clean.
- **The scripted runs** (`docs/game/scripted-runs.md`): `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`, one at a time through `scripts/limited.sh`. No baseline should move; explain any difference.
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder); report any wall-clock test within four times of its budget.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
```

## Report (one page, under 500 words)
- The branch and the final commit SHA (local, not pushed).
- Each deliverable: what you built (file and type), its tests.
- **Values chosen** where the design left one open, and every place the design and the code disagreed.
- Core gaps (the tú imperative lookup, if missing).
- The scripted runs' differences; the test count before and after; CI's case; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs'`, kill only your own).
