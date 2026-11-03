You are implementing the game milestone **V28, the small game points after the follow-ups**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game** (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tools/Vocaterra.SpeechRender`, their tests, `content/game-v1`, `scripts/` for the speech scripts, and `docs/game/`). The owner asked on 2026-11-02 for the small open points to be addressed, and for the speech folder to be organised into subfolders. Each item is one commit.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` or any other worktree. Create your branch with `git checkout -b V28-small-game-points @@BASE@@` (the tip of `feature/village-followups`). Do not switch branches once you have started.
- **The owner wants work pushed often.** Commit after each item that builds and passes its tests, with the prefix `V28: `. Push after every commit: `git push -u origin V28-small-game-points` the first time, then `git push origin V28-small-game-points`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The items
1. **A statement about the listener gets «Sí, es verdad.»** Free-talk replies match on intent only (V20), so Luz says «¡Qué bien!» to `Tienes veinte panes.` A statement whose subject is the listener (a second-person verb, `tú` or `usted`: `Tienes veinte panes.`, `Usted tiene muchas manzanas.`, `Eres el herrero.`) gets «Sí, es verdad.» instead; a statement about the speaker or a third person keeps today's reply. Read how V20's per-NPC pools are chosen (`NpcTurnRules.Compose`, `NpcEntry.FreeTalkPools`; `grep -n '^- \*\*V20' docs/game/IMPLEMENTATION_PLAN.md`, then about 25 lines). Only the shopkeeper, the villager and the farmer have a `<npc>_true` pool today (`grep -n '_true' content/game-v1/dialogue/dialogue.json`); add «Sí, es verdad.» for the NPCs that lack it, each with a normal and a slow WAV in its own voice through the content loop. Decide the listener from the core's analysis (the verb's person), not from the text; say how. Test the three cases. The host shares `NpcTurnRules`: check it there too.
2. **The name plates behind a building.** V22 hides a plate whose NPC is behind something solid (a depth test). Add the missing test: the beacon's NPC behind a building has no plate drawn (`tests/Vocaterra.Game.Tests/UI/Hud/NamePlatesTests.cs`, `src/Vocaterra.Game/UI/Hud/NamePlates.cs`). If the test fails, fix the code.
3. **The `<npc>.understood` transitions.** V27 added an `onHostEvent: "<npc>.understood"` transition to every state of every agent file (`content/game-v1/agents/*.json`) only so that the lint counts the `<npc>_understood` pools as used; the game says the pool directly (`NpcTurnRules.OffPromptReply`) and nothing fires the event. Keep one mechanism, not both: either the game fires the event and the agent's transition says the line (as V21's exchange lines `<npc>.cant_sell` are said: `grep -rn 'CantSell' src/Vocaterra.Game.Core --include=*.cs`), or the transitions go and the lint is told these pools are said by the host (find how the lint waives a pool: `grep -rn 'host-pools\|hostPools\|waive' src/Vocaterra.Nlp/Content --include=*.cs | head`, and the gate's `--host-pools 'guide.*'`). Prefer the one that changes no turn's state or counter; report which and why. The lint stays clean at A2.
4. **The speech folder in subfolders** (the owner: `content/game-v1/speech` holds 1 076 WAVs in one folder). Put each speaker's files in `speech/<speaker>/` for the seven speakers of `content/speech-index.json` (its keys are `speech/<file>`, its field `Speaker`): the guide, the villager, the farmer, the baker, the shopkeeper, the fisher and the smith. The guide's 44 word-card files (`card.*`) may go in `speech/cards/` if that reads better; say what you chose. Each slow file stays beside its normal one.
   - Move the files with `git mv` so their history is kept (a script under `/tmp/claude-1000/v28/` that reads the index; never a shell glob over 1 000 files on one command line).
   - Everything that writes or reads the paths follows: the renderer (`tools/Vocaterra.SpeechRender/Program.cs`), `scripts/prerender-speech.sh`, the game's loaders (`src/Vocaterra.Game.Core/Language/Speech/SpeechLines.cs`, `src/Vocaterra.Game.Core/Sound/NpcVoice.cs`), the host's `GrammarStringsExtractor` if it reads paths, `scripts/playtest-build.sh` (its check about line 152, `startswith("speech/")`), `content/speech-index.json`, `docs/game/SPEECH_CHECKLIST.md` (its file names only: edit it with a script, never read the whole file), and any doc that names a speech path (`grep -rn 'speech/' docs/game content/README.md scripts --include=*.md --include=*.sh | head -30`).
   - The core's `ContentManifest` hashes the bundle's files recursively (`src/Vocaterra.Nlp/Content/ContentManifest.cs`, about line 234), so the subfolders are hashed with no core change; the bundle hash moves through the content loop. If anything in the core turns out to need the flat folder, stop that part and report it: never edit the core.
   - Proof: the game's start line still says `speech: 538 lines, 1076 files, 0 missing` (more if item 1 added lines); a re-render of one line (`scripts/prerender-speech.sh` for one id, if it allows) writes into the right subfolder; the playtest build's check passes; a game run plays a line (the session log names the file).
5. **The README's open points** (`docs/game/README.md`, "Open points after the follow-ups", `grep -n`): drop the points fixed since V24: `buenas tardes` (M69), `cuántas cuesta todo` (M73), «¡Qué bien!» to a statement about the listener and the name plates (this milestone); in the fuzz families keep `2tpLGUOSqj41` with `fSMdah3kOSd7` and `3-VvCuMLFEA2` (recorded, M73 reported that the only fix would narrow the mutation) and drop `8yc522XLB_W2` (fixed by M73). Add any point this milestone leaves, and update the counts of lines and speech files it states. Do not edit the plans or the decisions.

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/code_guidelines.md`; `docs/game/ARCHITECTURE.md` (by `grep -n`); `content/README.md` (how to edit content).
- V20's, V22's and V27's entries in `docs/game/IMPLEMENTATION_PLAN.md` section 5 (`grep -n '^- \*\*V2[027]'`, about 25 lines each).
- Never read `docs/game/SPEECH_CHECKLIST.md`, `manifest.json`, `content/speech-index.json` or the whole of `content/PHRASES.md`; grep them or read them with a script.

## The content loop (V18)
After every content edit run `scripts/content-loop.sh --grammar --speech` directly (never under `scripts/limited.sh`: it refuses, exit 3). It refreshes the hash, the fixtures, the LanguageTool cache and the WAVs. Add `--record` when the goldens move, and run it once with `--tier full` before the report (that is your full-suite run). After a grammar refresh, check that the cache rebuilt from nothing equals the committed one: `scripts/languagetool.sh start`; `rm content/grammar/game-v1-cache.json`; `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --strict`; `git diff --exit-code content/grammar/`; stop the server. Check that `scripts/content-loop.sh --speech` still finds every WAV after the move (it should report 0 to render).

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/v28/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` to find the place first.
- Wait for a long command by running it in the foreground, not by polling a log. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 11 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call, every speech prerender and every game run through `scripts/limited.sh` (`scripts/limited.sh dotnet build <project> -m:4`; tests with `-m:2`). `scripts/test.sh`, `scripts/try-lines.sh` and `scripts/content-loop.sh` run directly and are **never** wrapped in `scripts/limited.sh`.
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating use `scripts/test.sh fast` or `scripts/test.sh project <test project>`.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/v28/` for those.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`; give each its own `--session-dir` under `/tmp/claude-1000/v28/`, and scripted steps need `--screenshot`. Run the game from `src/Vocaterra.Game/bin/Debug/net10.0` (`dotnet Vocaterra.Game.dll --seed 42 --mute --session-dir <dir> --content <repo>/content/game-v1 --skip-to-session 10 --talk baker --say "Tienes veinte panes." --leave --screenshot <png> --frames 10`). The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll --transcript "<line>" --prompt <id>`.

## Rules
- **What this milestone edits:** the game's code and tests, `tools/Vocaterra.SpeechRender`, `content/game-v1` (the dialogue pools, the agents' and NPCs' files, the speech folder, and what the content loop refreshes), `content/speech-index.json`, `scripts/prerender-speech.sh`, `scripts/playtest-build.sh`, `docs/game/README.md`, `docs/game/SPEECH_CHECKLIST.md` (paths only), other `docs/game/` files that name a speech path, and the try-lines baselines it moves.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched; a core test that fails after a content edit is wanted feedback: report it.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`).
- Do not edit either `IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, either `DECISIONS.md`, `docs/HANDOVER_VILLAGE_PHASE_B.md`, `scripts/merge-gate.sh`, or anything under `.claude/` or `.github/workflows/`.
- No `System.Random` and no clock in the evaluation path.

## Behaviour checks (before the report; show the lines)
- The game at session 10: `--talk baker --say "Tienes veinte panes."` gives «Sí, es verdad.»; `--say "Tengo veinte panes."` keeps today's reply; `--talk smith --say "me gusta el río"` still gives «Te entiendo. Yo hago hachas.»; the start line's speech count with 0 missing. Look at one screenshot.
- The host: the same lines at `baker-open` and `smith-open`.
- `scripts/try-lines.sh --check` and `--host --check`: list the rows that moved and why.
- **The scripted runs:** `scripts/scripted-runs.sh` before your first edit and after, with **the same work folder** for both captures (so paths do not differ), compared with `python3 -I scripts/compare-runs.py`; run them one at a time through `scripts/limited.sh`. Explain every difference.
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder); report any failure and any wall-clock test within four times of its budget.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/content-loop.sh --grammar --speech --tier full`, all passing. It was 8265 passed and 3 skipped after M73; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; the lint at A2 clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`); `scripts/try-lines.sh --check` clean (after any re-record you justify).
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean and the branch is pushed.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 450 words)
- The branch and the final commit SHA, pushed.
- Each item: what you changed (file and member), the test; for item 1 how the listener is decided and the new pools; for item 3 which mechanism and why; for item 4 the folder layout, the files moved, everything that followed, and the new bundle hash.
- The behaviour checks' lines; the scripted runs' differences; the try-lines rows re-recorded.
- The test count before and after; CI's case; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs|languagetool'`, kill only your own).
