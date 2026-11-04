You are implementing the core milestone **M74, the weather intents and `ask_wellbeing`**, for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the core and the intent catalog of the shared bundle `content/game-v1`.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m74`, already on the branch `M74-weather-intents` (from `feature/village-decisions` at 45305998). cd there first. Commit after each coherent step that builds and passes its tests, with the prefix `M74: `, and push after every commit (`git push -u origin M74-weather-intents` the first time). Never push or merge `feature/village-decisions` or `main`. When you finish, leave the worktree on your branch, clean.

## Resources (the machine has crashed from overload)
Run every dotnet restore, build, test, format and run, every scripts/content.sh call, speech prerender and screenshot run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly (content-loop refuses limited.sh). Never start two heavy commands at once (no `&`, no background builds), and write no shell wait loop on `pgrep -f`. While iterating run `scripts/test.sh fast` or `scripts/test.sh project <test project>`; run `scripts/test.sh full` once before reporting. Exit 137 means the 6 GB cap killed it: lower the parallelism. Disk is about 17 GB free: build only what you need while iterating.

## Environment
Prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may reject a bare `export`, and it refuses a command line containing the words `eval` or `hash`: for `scripts/content.sh hash` or a CLI `eval` run, write a wrapper script under /tmp/claude-1000/ and run it). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

## Read first
CLAUDE.md; docs/nlp/implementation-plan.md §3, §4 and your §5 row (M74); `docs/handovers/2026-11-02-after-followups.md` sections 2, 4 and 7 (the spec; the owner decided it); the §5b entries of M72 and M73 (`grep -n '^- \*\*M7[23]' docs/nlp/implementation-plan.md`) and their briefs `.claude/briefs/m72-off-prompt-lines.md`, `m73-small-core-points.md`; docs/nlp/changing-the-core.md (the catalog's DSL, the lint, re-hashing); docs/nlp/api.md; `content/game-v1/intents/intents.json`, `prompts/prompts.json`; `src/Vocaterra.Nlp/Language/Intent/IntentResolver.cs`; the grading tests M76 made (search `tests/` for the topic-grouped grading tests that replaced `scripts/try-lines/`) and the identity baseline M72 and M73 re-recorded. Read the sections named, never whole long files.

## The milestone (decided by the owner, 2026-11-02)
1. **Weather statement intent** (for example `state_weather`): `hace frío`, `hace calor`, `hace sol`, `hace buen tiempo`, `hace mal tiempo`, `hoy hace …`, `llueve`, `está nublado`. Give it a slot for the weather word so V29's reply can echo it («Sí, hace frío.»).
2. **Weather question intent** (for example `ask_weather`): `¿qué tiempo hace?`, `¿hace frío?`.
3. **`ask_wellbeing`**: `¿cómo estás?`, `¿cómo está usted?`, `¿cómo están ustedes?`, `¿cómo estáis?`, taken out of `greet` so the game can tell it from `hola`. `cómo estás usted` keeps its `conj.person` (M73's example). `hola, ¿cómo estás?` — decide and document which intent wins (the game answers one line; prefer `ask_wellbeing`, since the reply «Bien, gracias.» also greets).
4. Keep `hacer` with a weather noun out of `state_action`, `ask_activity` and `ask_craft` **by the resolver's order or the grammars, not by a hack**. `¿qué haces?`, `hago pan` and the lessons' craft questions keep what they have today. `hace dos días compré pan` at `smith-open` must be neither weather nor `state_action` (`hace` + time = "ago"); test it with the weather lines.
5. **Prompts:** wherever `greet` is allowed today, allow `ask_wellbeing` too, so no prompt loses a line it took. Allowing the weather intents at the NPCs' prompts, and every reply, is V29's (the game), not yours. If a game test fails only because `¿cómo estás?` is no longer `greet`, you may add the narrowest game-side mapping (treat `ask_wellbeing` as `greet` until V29) in one commit named for it, and report it; touch nothing else in the game.
6. **Fuzz:** widen O7 and O3 for the new intents, never narrow. Add the new lines (and their clean neighbours and near misses) to the grading tests by topic (C# tests, never line files) and re-record the identity baseline as M72 and M73 did; list every moved case by kind in the report.
7. **CEFR:** every new weather word within A2 (`cefr-check`; the lint at `--level a2`).
8. Re-hash the bundle after the catalog edit, run the content loop (`scripts/content-loop.sh --grammar --speech`; `--record` if the goldens move), put the new bundle hash into `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json`, and run the whole suite.
9. docs/nlp: update the intent catalog's documentation and api.md if a public type changes.

## Rules
A core milestone edits only the Vocaterra.Nlp projects, their tests, tests/golden, docs/nlp/ and content/game-v1 where the core needs it (the catalog and the prompts' allowed intents above), never the game's lessons or dialogue (except item 5's one mapping). Fix bugs at the source with a regression test in the owning module; never weaken a fuzz oracle. A regression check is a C# test. Nothing under .github/workflows/; do not edit either implementation-plan.md or either decisions.md. No System.Random and no clock in the evaluation path. Before reporting, run the CLI on a few natural, correct lines at the NPCs' prompts (names, greetings, usted/ustedes, `hola luz quiero pan`, `buenos días domingo`), not only the brief's lines; none may newly fail. The known random fuzz family `2tpLGUOSqj41` (handover section 7) may fail once: check the seed with `CsCheck_Seed` and rerun once.

## Gate (from the worktree root)
scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full (8817 passed before you, with models); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; the A2 lint (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`) clean; bash scripts/license-scan.sh --offline PASSED; the core demo (`dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open --content content/game-v1`) still gives exactly `conj.person` and `noun.number`. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf

## Report, one page
Branch, commit SHA, worktree path; what was built (the intents, their grammars, the resolver change); test counts; the moved grading cases by kind; a table of the weather and wellbeing lines at `villager-open`, `smith-open` and `baker-open` with intent, communicated and correctness; bugs found and fixed (where, symptom, fix, test); known-not-fixed with reasoning; files touched outside the core (the bundle, any game mapping); anything left undone and why. Leave out: replies, WAVs and NPC dialogue (V29), the near-miss rule (M75).
