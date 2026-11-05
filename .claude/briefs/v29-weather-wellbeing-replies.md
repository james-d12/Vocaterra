You are implementing the game milestone **V29, the weather and «¿cómo estás?» replies**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game** (`src/Vocaterra.Game.Core`, `src/Vocaterra.Host`, their tests, and `content/game-v1`). The owner decided it on 2026-11-02 (D-game-22, items 1, 3 and 5 of `docs/handovers/2026-11-02-after-followups.md`); the core's M74 has built its half.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/v29`, already on the branch `V29-weather-wellbeing-replies` (from `feature/village-decisions` at 048c4048, with M74 merged). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- Commit after each item that builds and passes its tests, with the prefix `V29: `. Push after every commit: `git push -u origin V29-weather-wellbeing-replies` the first time.
- Never push `main` or `feature/village-decisions`, and do not merge. When you finish, leave the worktree on your branch, clean.

## What the core gives you (M74)
- `state_weather` (slot `weather` = the words after `hace`, e.g. `frío`, `buen tiempo`; also `llueve`, `nieva`, `está nublado`, `¡qué frío!`, past and future forms), `ask_weather` (`¿qué tiempo hace / hará / hacía?`, `¿cómo está el tiempo?`), and `ask_wellbeing` (`¿cómo estás?`, `¿cómo está usted?`, `¿cómo están ustedes?`, `¿cómo estáis?`, `¿qué tal?`, also after `hola` / `buenos días`), taken out of `greet`.
- `ask_wellbeing` is already allowed at all 8 prompts that allow `greet`. M74 added an `ask_wellbeing` twin to each `greet` transition in `content/game-v1/agents/*.json`, so today the NPCs answer «¿cómo estás?» with their greeting: you replace that with the real reply.
- The weather intents are allowed nowhere yet, so at the NPCs' prompts a weather line is unknown with `Verdict.OffPromptIntent` = `state_weather` / `ask_weather` (V27 then gives «Te entiendo. …»). At `smith-open` and `farmer-open`, `¿qué tiempo hace?` is `ask_activity` by a keyword rule until `ask_weather` is allowed there; once allowed, its grammar wins by the existing rung order (check it).
- `¿hace frío?` is `state_weather` (a spoken transcript has no question mark). An off-prompt weather verdict still carries an analysis slot `item=calor` (existing merge behaviour): do not credit `calor` as an item noun from it unless it is a lexicon noun the book already tracks.
- Read M74's §5b entry (`grep -n '^- \*\*M74' docs/nlp/implementation-plan.md`) and `docs/nlp/api.md` on the new intents (`grep -n 'state_weather\|ask_wellbeing' docs/nlp/api.md`).

## The items
1. **Declare the acknowledgement pools first** (the PR #28 review's item 14). `NpcTurnRules` matches pool names in code: `^[a-z]+_nice\.\d+$` (about lines 232 and 293) and `answer_accepted.`, and `ListenerReply` (about line 288) always takes the first variant. Declare these pools in the NPC files (`content/game-v1/game/npcs/*.json`, the shape `NpcEntry` reads; the schema in `content/schemas/` if there is one, with `GameDataValidator`) so that no pool name is written in code, and let `ListenerReply` choose among the variants deterministically (the host's repeat counts or a seeded index, never `System.Random` or the clock). Tests for both.
2. **Allow the new intents** at the NPCs' open prompts (`content/game-v1/prompts/prompts.json`: `smith-open`, `baker-open`, `fisher-open`, `farmer-open`, `villager-open`, `guide-open`, and the shopkeeper's `stall-need`): `state_weather` and `ask_weather`, as V20 did for the lessons' intents. Lesson prompts stay as they are.
3. **The replies**, one pool per kind per NPC, declared in the NPC files (item 1's mechanism), chosen in free talk only:
   - a weather statement: «Sí, hace frío.» echoing the learner's weather word through a template with a slot (`hace` + the slot; for `llueve` / `está nublado` / a word the template cannot take, «¡Sí!»). The core's response templates take slots: read how V21's or the guide's templated lines fill a slot before adding code.
   - `¿qué tiempo hace?`: «Hoy hace sol.» (the game has no weather; if it ever does, the reply follows it: leave one seam, no more).
   - `¿cómo estás?`: «Bien, gracias.» Do **not** add «¿Y tú?».
   Seven NPCs (Domingo, Luz, León, Mar, the villager, Luna, the shopkeeper). Each fixed line gets a normal and a slow WAV in its NPC's voice through the content loop (the template's variants: one WAV per weather word the template can produce from the A2 weather words, or say what you chose). Keep the texts exactly; if the lint or `cefr-check` refuses a word, stop that line and report it. Remove M74's `greet` twins for `ask_wellbeing` from `content/game-v1/agents/*.json` once the real reply works.
4. **The book** credits the line as free talk does (V20 (b), V27's item 3): a use of its nouns and of any phrase of a lesson begun or done. No NPC action, no item, no order, no host event.
5. **`cómo hago un hacha` at `smith-open`** is off the prompt (`ask_craft`), so Domingo answers «Te entiendo. Yo hago hachas.». Let `smith-open` take `ask_craft` with a reply in Domingo's voice, if the lessons allow it (check the smith's lessons' steps: a lesson that teaches the craft question must not be bypassed); otherwise leave it and say why.
6. **One test through both paths:** a miss and an off-prompt line through both the host (`src/Vocaterra.Host/ConversationSession.cs`) and the game (`NpcService`), asserting the same reply (V30 shared their code; no test proves it).
7. **The host** prints the new replies as its `agent:` line; the grading tests and any host baselines that move are updated and listed.
8. **The README**, `docs/game/village-plan.md` section 5, "Open points after the follow-ups" (`grep -n 'Open points after the follow-ups' docs/game/village-plan.md`): drop the points V29 closes, add any it leaves, and update the counts it states for lines and speech files. Edit only that subsection of the village plan.

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/process/code-guidelines.md`; `docs/game/architecture.md` (by `grep -n`); `content/README.md` (how to edit content).
- `docs/handovers/2026-11-02-after-followups.md` sections 2, 4 and 8.
- V20's, V21's, V27's and V30's entries in `docs/game/implementation-plan.md` section 5 (`grep -n '^- \*\*V2[017]\|^- \*\*V30'`, about 25 lines each) and the brief `.claude/briefs/v27-off-prompt-replies.md` (your model).
- `src/Vocaterra.Game.Core/Npc/NpcService.cs` about lines 600 to 740 (the turn), `src/Vocaterra.Game.Core/Npc/NpcTurnRules.cs`.
- Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md` (grep the rows you need).

## The content loop (V18)
After every content edit run `scripts/content-loop.sh --grammar --speech` directly (never under `scripts/limited.sh`: it refuses, exit 3). It refreshes the hash, the fixtures, the LanguageTool cache and the WAVs. Add `--record` when the goldens move, and run it once with `--tier full` before the report (your full-suite run). After a grammar refresh, check the cache rebuilt from nothing equals the committed one: `scripts/languagetool.sh start`; `rm content/grammar/game-v1-cache.json`; `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --strict`; `git diff --exit-code content/grammar/`; stop the server. (M74 found LanguageTool unreachable under `limited.sh`; run the grammar check directly if so, and say so.)

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/v29/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind.**
- **Disk:** about 13 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly.
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/v29/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/v29/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll --transcript "<line>" --prompt <id>`.

## Rules
- **What this milestone edits:** the game's code and tests, `content/game-v1` (the pools, the NPCs' data, the prompts' allowed intents, the agents' transitions, the WAVs and what the content loop refreshes), and the one subsection of `docs/game/village-plan.md` named in item 8.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched; a core test that fails after a content edit is wanted feedback: report it (a core grading test whose expected intent moves only because a prompt now allows `state_weather` may be updated, each listed).
- A regression check is a C# test, never a script with text baselines.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`).
- Do not edit either `implementation-plan.md`, either `decisions.md`, `scripts/merge-gate.sh`, or anything under `.claude/` or `.github/workflows/`.
- No `System.Random` and no clock in the evaluation path.

## Behaviour checks (before the report; show the lines)
- The game: `--talk smith --say "hace frío"` gives «Sí, hace frío.» in Domingo's voice; `--talk baker --say "¿qué tiempo hace?"` gives «Hoy hace sol.»; `--talk fisher --say "¿cómo estás?"` gives «Bien, gracias.»; `--talk smith --say "hola"` still greets; `--talk farmer --say "llueve"` gives «¡Sí!»; one lesson step with a weather line unchanged. Look at one screenshot.
- The host: each of these lines at its prompt, before and after, and `hola luz quiero pan`, `buenos días domingo`, `¿cómo está usted?` at three prompts.
- **The scripted runs:** `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`, one at a time through `scripts/limited.sh`. Explain every difference.
- **The learner's route:** a new world, then free talk once with each NPC with a weather line and «¿cómo estás?».
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder); report any wall-clock test within four times of its budget.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/content-loop.sh --grammar --speech --tier full`, all passing (9019 passed and 3 skipped after M75; report the new count).
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; the lint at A2 clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`).
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean and the branch is pushed.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf
```

## Report (one page, under 450 words)
- The branch and the final commit SHA, pushed.
- Each item: what you changed (file and member), the test; the pool ids and their lines; the WAV count; the new bundle hash.
- The behaviour checks' lines; the scripted runs' differences; the grading or host cases that moved.
- The test count before and after; CI's case; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs'`, kill only your own).
