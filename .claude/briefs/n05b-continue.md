You are implementing the game milestone **N05, the HUD and the opening**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It builds on N01 to N04 and N06 (the needs model, eating and drinking, the night and the collapse, death and the chest, regrowth and gifts), which are engine-free and already raise every event; N05 makes them visible and audible: the bars, the icons, the stage lines, the sounds, the view effects and their option, the tip, the death screen, the relaxed-needs option, and the proof that day 1 is safe.

Do the work yourself; do not spawn subagents.

## You continue a first attempt (read this first)
A first agent wrote most of N05's code and stopped before building it (it ran out of context). The orchestrator committed its work as it
stood: the branch `N05-hud-opening` holds a commit `N05: WIP …` with the code, the tests and its content edits (the patch it saved,
applied). **None of it has been compiled.** Start by building `src/Vocaterra.Game.Core` and `src/Vocaterra.Game`, fix the compile errors,
then run the new tests, then go on with the list below. Keep what works; rewrite what does not; you own the result.

What the first attempt built (verify each; nothing is proven):
- Core: `Needs/NeedsHud.cs` (the stage-line queue and its sound request, the icons, the bars, the view-effect levels, the needs list's
  rows); `GameWords.HudLines` and `GameDataValidator.HudLineKeys` (refuses a stage line with no Spanish); `PlayerNeeds.RateFactor` and
  `VoxelSession.RelaxedNeeds` (half the drive rates; at zero health a faint, no chest, the bag kept); `VoxelSession.DeathLine`; the chest's
  beacon label «tus cosas»; `SoundCue.NeedStage` (a synthesised soft tone in the Sfx category, which ducks under the voices);
  `SaveSettings.ViewEffectsOff` and `RelaxedNeeds` (written only when true); F7, `InputKeys` "viewEffects", the `ToggleViewEffects` command.
- Game: `UI/Hud/NeedsHudLayout.cs`, `NeedIcons.cs`, `NeedsHudLayer.cs`, `ViewEffectsLayer.cs`, `DeathScreenLayer.cs`; `HotbarLayer.BoxFor`;
  `InventoryLayout` takes `needsRows` (a tight mode) and `InventoryLayer` draws the five needs; `FrameView`, `LayerStack`, `LaunchOptions`
  (`--relaxed-needs`, `--view-effects on|off`), `Program.cs`, `VoxelGame.cs` (a 4 s death-screen hold).
- Tests: `tests/Vocaterra.Game.Core.Tests/Needs/NeedsHudTests.cs`, `RelaxedNeedsTests.cs`, `DayOneSafetyTests.cs`;
  `tests/Vocaterra.Game.Tests/UI/Hud/NeedsHudLayoutTests.cs`.
- Content: `hudLines` in `content/game-v1/game/words.json` (the 14 stage lines of 9.3, `hud.camp`, `hud.belongings`, `hud.fine`,
  `hud.name.*`), the `needs` tip in `game/tips.json` (on `need_stage`), the English in `content/locale/en/` (`ui.json`, `tips.json`,
  `keys.json`), `hudLines` in `content/schema/words.schema.json`. Its own notes: drop the orphan `ui.needsTitle` from `locale/en/ui.json`;
  update the `about` text of `words.json`. The content loop has **not** run on these edits: run it (`--grammar --speech`) once it builds.
- Its values: the novice pace 2.5 times the route's time; a stage line on screen 4 s; the HUD hidden under `--needs off` (so the scripted
  runs keep their baselines); the stamina bar only below the drive-cut maximum; dim edges from thirst stage 3 and above (stage 4 has no
  `view` entry in `needs.json`). Its findings: `NeedsData.TicksPerSecond` is 60; `morir` («Me muero de …») is in neither the A1 nor the A2
  list and the lint does not read `hudLines`: report it, do not waive it.

Still to do: build and fix; the tests; N06's lines (deliverable 6: pools `shop_gift_used`, `villager_gift_used` «Hoy no tengo más pan.»,
`guide_gift_used` «Hoy ya no tengo agua.», the four `gift_used` agent states re-pointed, their English in `lines.json`); the content loop;
`docs/game/running.md` and the HUD doc; the behaviour checks; the scripted runs' after-run and comparison; the gate; CI's case; commits.

**The scripted runs' baseline is already captured** at `/tmp/claude-1000/n05/runs-base` (from the base build, before any edit; r01 to r05
from a first capture, r06 onward from a second, both on unchanged content). Do not capture it again; capture the after-run into
`/tmp/claude-1000/n05/runs-after` and compare. Run `scripts/scripted-runs.sh` in the foreground only, never in the background, and never
build or edit `content/` while it runs (it reads this worktree's binary and bundle).

**Your context is the limit that stopped the first attempt.** Never print a whole file or a whole build log: `grep -n` the errors
(`grep -E 'error|warn' log | head -30`), read files in ranges under 100 lines, and do not re-read a file you have just edited. Commit and push
after each step that builds and passes, so a stop loses little. Use no Monitor and no background command.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n05`, already on the branch `N05-hud-opening` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit and push often** (the owner's rule): commit after each item that builds and passes its tests, with the prefix `N05: `, and push after every commit (`git push -u origin N05-hud-opening` the first time).
- Never push `main` or `feature/adaptive_design`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/n05/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 13 GB free once you build. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.
- **Another agent (L01, lessons as classes) works beside you in its own worktree.** `scripts/limited.sh` runs at most two heavy commands at once machine-wide and caps them together at 8 GB; a command may wait for a slot, which is normal. A command killed with exit 137 or 143 may have met the shared cap: rerun it once, and report it if it happens twice. L01 also edits `content/game-v1` (lessons, NPC offer lines) and `content/locale/en`: keep your edits to the files your deliverables name, and do not reformat other entries.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/n05/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/n05/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
- After any edit of `content/game-v1`: `scripts/content-loop.sh --speech` (it refreshes the hash, the save fixtures, the LanguageTool cache and the WAVs; `--record` when the goldens move; `--tier full` once before the report). Content outside game-v1's hash: `content/locale/` (the English), `content/schema/`. How to edit content: `content/README.md`.

## Rules
- **What this milestone edits:** the game's code and tests (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tests/Vocaterra.Game*.Tests`, `tests/Vocaterra.Host.Tests`), `content/game-v1`, `content/locale`, `content/schema`, `assets/` and `docs/game/` (not the plans or the decisions).
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched; a core test that fails after a content edit is wanted feedback: report it.
- A regression check is a C# test, never a script with text baselines.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`). Logic in Core, drawing and devices in Game (`docs/process/code-guidelines.md`, `docs/game/architecture.md`).
- Do not edit either `implementation-plan.md`, `docs/game/activities-plan.md`, `docs/game/village-plan.md`, either `decisions.md`, `scripts/merge-gate.sh`, the design `.docx`, or anything under `.claude/` or `.github/workflows/`.
- Determinism: no `System.Random` and no wall clock in the game's logic; the host owns the stored time, the seed and the repeat counts. Assets CC0 only (D-game-5).
- Every save version from 1 loads; a save round-trips.

## The grammar cache
The LanguageTool check works from this sandbox (since 2026-11-05): after a content edit that adds Spanish, `scripts/content-loop.sh --grammar --speech` refreshes `content/grammar/` and reports new findings; never edit `content/grammar/` by hand. This milestone adds Spanish (the stage lines, the death screen, «tus cosas», two NPC lines): run the loop with `--grammar` after each such edit, read every new finding, and fix the line or say in the report why it stands.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` (or `scripts/content-loop.sh --grammar --speech --tier full`, since you edit content), all passing: 9385 passed and 3 skipped before you; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; the lint at A2 clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`).
- **The scripted runs** (`docs/game/scripted-runs.md`): `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`, one at a time through `scripts/limited.sh`. Explain every difference; re-baseline only the runs this milestone is meant to move.
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder); report any wall-clock test within four times of its budget.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean and the branch is pushed.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf
```

## Report (one page, under 500 words)
- The branch and the final commit SHA, pushed.
- Each deliverable: what you built (file and type), its tests.
- **Values chosen** where the design left one open, and every place the design and the code disagreed.
- The behaviour checks' lines; the scripted runs' differences; the save versions tested.
- The test count before and after; CI's case; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs'`, kill only your own).


## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/process/code-guidelines.md`; `docs/game/architecture.md` (by `grep -n`); `content/README.md`; `content/PHRASES.md` by `grep -n` only.
- `docs/game/decisions.md`: D-game-24 (all), D-game-13 (the player's lines are text with the English beneath), D-game-18 (sounds), D-game-23's "Owner's answers, 2026-11-04" (decisions 17 and 18 of the design are adopted provisionally).
- `docs/game/implementation-plan.md` section 4.9 (your row) and the section 5 entries of N01, N02, N03, N04 and N06 (`grep -n '^- \*\*N0'`): N04 left English keys for your Spanish (`ui.beaconBelongings`, `ui.diedHunger`, `ui.diedThirst`, `ui.chestRest`); N06 asked for two NPC lines.
- The design (`/tmp/claude-1000/orch/adaptation-design.txt`): 9.3 (the stages table: every line's Spanish and English), 9.7 (death), 9.8 (from day 1, the tip, the opening acceptance), 9.9 (the HUD and sound), 9.10's "Game (MonoGame)" paragraph, 9.13 (the tests on the day-1 route), and section 18's decision 17 (`grep -n '^17\. A relaxed'`).
- Code: `src/Vocaterra.Game.Core/Needs/` (`PlayerNeeds`, `NeedsData`), `Session/VoxelSession.Needs.cs` (`NeedStage`, `SyncNeedStages`), `Session/VoxelSession.Death.cs`, `src/Vocaterra.Game/UI/Hud/` (`HudLayout`, `HotbarLayer`, `TipLayer`, `ToastLayer`, `FadeLayer`, `BeaconLabel`), `UI/Inventory/`, `tests/Vocaterra.Game.Tests/UI/Common/UiScaleLayoutTests.cs`, `Sound/SoundEffectOutput.cs`, `SaveSettings` (how the volumes and the text scale are kept), `content/game-v1/game/needs.json` (the `line` keys `need.<need>.<stage>`, not yet in any word file), `game/words.json`, `game/tips.json`, `content/locale/en/`.

## The deliverables
1. **The stage lines** (9.3, 9.8): every `line` key in `needs.json` (hunger, thirst, sleep 1 to 4; health's two) gets its Spanish in `game/words.json` (or the file the code reads lines from; say which) and its English in `content/locale/en/`, exactly as 9.3's table gives them: «Tengo un poco de hambre.» … «Me muero de sueño.», «Estoy débil.», «Estoy muy débil.». They are the player's own words: text only, no WAVs (D-game-13). The lint must hold them at A2 (check `sueño`, `débil`, `morir`/`muero`, `mucha`/`mucho` with `cefr-check` before landing; a word above A2 is reported, not waived silently). `GameDataValidator` refuses a stage `line` key with no Spanish.
2. **The HUD** (9.9, Game, `UI/Hud/`): health and stamina as two thin bars beside the hotbar; stamina only while below its maximum, with a dashed end showing what the drives cut from the maximum; a drive icon (a loaf, a drop, a moon, drawn in code or in the atlas from CC0 shapes) at stage 1 and above with one to four notches, gone on recovery; healthy drives and full stamina leave only the health bar. A stage change shows its line once above the hotbar (Spanish, the English beneath) and plays a soft sound (synthesised or CC0, D-game-18) that ducks under the voices. The inventory lists the five needs with their stage in Spanish and English. All layout logic that can be tested lives outside the drawing (as `HudLayout`), with tests.
3. **The view effects and their option** (9.9): dim edges at thirst 3 and above, a slow blink now and then at sleep 3 and above, red edges at health below 25; gentle, never a flash (no full-screen change faster than about 3 Hz, no full-brightness frame). One option turns them off and leaves the icons and the lines; kept with the other settings (as the volumes and the text scale are), with a launch flag for tests if the existing options have one.
4. **The tip and the death screen** (9.8, 9.7): the English tip of 9.8 in `game/tips.json` and its locale, shown at the first stage change of the first session (once). Death: a fade, then a short screen «Estás en tu campamento.» with its English and the N04 cause line (`ui.diedHunger`/`ui.diedThirst`); the beacon's label for the chest «tus cosas» (`ui.beaconBelongings`'s Spanish side, if the beacon shows Spanish labels; follow how other beacon labels work).
5. **The relaxed-needs option** (design decision 17, adopted provisionally): one setting, off by default: half every drive's rate; at zero health the player faints and wakes at the camp, with no chest (the bag kept). Kept in the settings, not the save's world; a launch flag `--relaxed-needs` for runs and tests. Real death stays the default (D-game-24).
6. **N06's lines:** «Hoy no tengo más pan.» (the stall and the villager) and «Hoy ya no tengo agua.» (Luna, the `guide`) in place of the borrowed lines N06 used for `<npc>.gift_used` (`grep -rn gift_used content/game-v1/agents`), each in its NPC's voice, with normal and slow WAVs through the content loop and the English in `content/locale/en/lines.json`.
7. **The day-1 route proven safe** (9.8's opening acceptance, 9.13): an engine-free test that plays day 1's route (the introduction, the camp objectives, the walk to the river and the fire) with needs on at the intended pace and at a slower novice pace (say what factor, at least 1.5 times the active time), and asserts no death, no collapse and no drive below stage 2 at the end; print the active time, the lowest meter and the recovery actions taken. If day 1 at the novice pace is not safe with N01's rates, do not retune silently: report the numbers (the owner tunes at the needs check).
8. **Documents:** `docs/game/running.md` for the new flag and option; the HUD's elements in the doc that lists the HUD layers, if one does.

## The tests
- `UiScaleLayoutTests` (or a new layout test beside it): the bars, the icons and the stage line never overlap the hotbar, the objective tracker or the tip box at every canvas and scale the existing tests cover.
- Which elements show for given meters (full; one drive at each stage; stamina below maximum; health below 50 and 25), from the layout or state logic, not pixels.
- A stage change shows its line once and requests its sound once; recovery hides the icon; the effects option off keeps the icons and lines.
- Relaxed needs: rates halved (a minute of play drops half as much); zero health wakes at the camp with the bag kept and no chest; off by default; saves unchanged by it.
- The day-1 route at both paces (deliverable 7).
- The validator's refusal of a missing stage line.

## Behaviour checks (before the report; show the lines and look at the screenshots)
- `--needs on --set-need thirst 20` and a few seconds of walking: a screenshot showing the drop icon with three notches, the dim edges and the line «Tengo mucha sed.» / "I'm very thirsty."; the same with the effects option off.
- `--set-need hunger 0 --set-need thirst 0` and a wait to death: the death screen's lines; the beacon on the chest.
- `--relaxed-needs` and the same: woke at the camp, no chest, bag kept.
- A screenshot of the inventory's needs list, at text scale 100 and the largest scale.
- N06's line: bread asked twice on day 2 at the stall: the new line.
