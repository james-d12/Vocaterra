You are implementing the game milestone **H11, the water series reconciled with the bread pilot**, in the Vocaterra repository (C#,
.NET 10, a MonoGame first-person voxel game that teaches Spanish). The game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`; the language
core (`Vocaterra.Nlp*`) is not yours. Do the work yourself; do not spawn subagents. Commit prefix `H11: `.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/h11`, already on the branch `H11-water-reconcile` (from
`feature/bread-pilot` at its tip, which holds the water series merged from `main`, the needs and death of the bread pilot, and W04b; it has
bin/ and obj/ from an earlier build). cd there first; never edit `/home/user/Dev/Vocaterra` or another worktree (`adaptive`, `german`
are not yours). Commit after each item that builds and passes its tests; never end your turn with uncommitted work. Do not push; never
push or merge `feature/*` or `main`. Run everything in the foreground and finish in one turn (no background commands, monitors or wait
loops).

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**,
**Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/h11/` for your logs, about 12 GB of disk free, and the count
before you given in your prompt. If you edit `content/game-v1` or `content/locale` (the death hint's text), run the content loop as
`docs/process/milestone-briefs.md` "What a content milestone adds" says (`sed -n 53,77p`), and put any new English in the locale bundle,
not in code. Do not edit `docs/game/decisions.md`, either implementation plan, or `docs/game/water-plan.md` (the orchestrator records the
owner's water decisions and the plan's lines); say in the report what they should say.

## The specification
- **The row:** `docs/game/water-plan.md` section 5, H11 (`grep -n '^| H11 ' docs/game/water-plan.md`); the owner authorised it on
  2026-11-08 (`docs/game/decisions.md`, D-game-23 item 6).
- **The design:** the water plan's section 3, the table "Hooks in files both branches touch" (`sed -n 48,90p`); 4.4 Breath (`sed -n
  129,146p`); 4.9 the view under water (`sed -n 206,226p`); 4.10 the bubble meter (`sed -n 227,234p`); section 6, the owner's decisions
  (`sed -n 272,290p`), decision 1 (drowning: 10 health per `OutOfAir`, the bread pilot's death flow) and 4 (the blur: `Off`/`Low`
  default/`Medium`/`High`).
- **What the merge already did** (game plan section 5, "Main merged in: PR #45"; report `.claude/briefs/water-merge-report.md`):
  `speedPermille` passed to the swim step, `BreathHudLayer` above `NeedsHudLayer` in the layer order with no `raise` passed, save version
  7 holding both sides' fields. Check each against the row; finish what is left.
- **The bread pilot's side:** `PlayerNeeds` and the needs systems (`src/Vocaterra.Game.Core/Needs/`), death (`VoxelSession.Death.cs`, the
  death screen, its hints), the settings screen and `SaveSettings` (`ViewEffectsOff`, `RelaxedNeeds`, `NeedsRate`), `NeedsHudLayout`.
  `docs/game/saves.md` for the save format's rules.

## What to build (each committed)
1. **`NeedsHealth`**, the `IPlayerHealth` adapter over `PlayerNeeds`, wired in the session, so running out of air damages and kills
   through the bread pilot's death flow; health does not recover while drowning; the `player_drowned` cause gives a death hint ("Swim up
   to breathe" or close; English in the locale bundle). With needs off (`--needs off` or the setting), say what happens and keep it
   consistent with how other damage behaves then.
2. **The bubble meter** stacked above `NeedsHudLayout` where the needs block and the bubbles could overlap (check every HUD layout and
   UI scale the needs HUD supports); unchanged where they cannot.
3. **The blur setting:** a row in the settings screen (`Off`/`Low`/`Medium`/`High`, default `Low`), saved in `SaveSettings`
   (`UnderwaterBlur`), `--underwater-blur` still overriding for a run, and `ViewEffectsOff` forcing it off. Save version: follow
   `docs/game/saves.md`; the merge made it 7 (no migration between the two old version-6 shapes, the game is unreleased); bump only if
   the format's rules require it for the new field.
4. **The docs that still say save version 6:** `docs/game/saves.md` and `docs/game/architecture.md` (water's and the needs' fields now in
   version 7), and the "wadable" line wherever the game's docs other than the plans still say it.
5. **The `--walk-to` bank stop:** after the merge, `--walk-to 212,262` from the lake back to the bank gave up 0.8 blocks short after 14400
   ticks with 375 jump cues (`/tmp/claude-1000/water-merge/lake/` may be gone; reproduce it). Find whether the scripted walker can climb out
   of water (the swim hop-out, section 4.3) and fix it at the source in the walker or the hop-out, with a test; if it also happened on
   water's branch before the merge, say so.
6. **Tests:** drowning damages 10 per `OutOfAir` and kills through the death flow with the `player_drowned` hint; no damage after
   surfacing; the adapter with needs off; the blur saved, loaded and forced off; the bubble layout against the needs HUD; the walker
   leaving the lake.
7. **The scripted lake run** (water plan section 5's verification: walk in, dive, surface, swim to the bank, hop out) with screenshots,
   plus one run that stays under until death (the death screen with the drowning hint). Capture `scripts/scripted-runs.sh` before your
   first edit (`/tmp/claude-1000/h11/runs-before`) and after; compare with `scripts/compare-runs.py` and explain every difference.

## Report (one page)
Branch, final SHA, worktree clean; each item done and how; the save version; the walker's cause and fix; test counts before and after;
the scripted runs compared; screenshot paths (the bubbles beside the needs bars, the death screen); what the orchestrator should record
(the owner's six water decisions for `docs/game/decisions.md`, the water plan's "wadable" line); anything left undone.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
