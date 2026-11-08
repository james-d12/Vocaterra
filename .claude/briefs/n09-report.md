# N09 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `N09-needs-rate`, agent's final commit `f06c0c84` (local; d1eb0b9d and 75220415 were pushed before the owner's no-push rule).
Then synced with U01 by a Haiku agent (brief `n09-sync.md`): `LaunchOptions.cs` conflicted (U01's `--dev`, N09's `--needs-rate`).

- Rate: `SaveSettings.NeedsRate`, 0.5 to 4.0 in steps of 0.25; absent (0) means 1; written only when not 1; `SnapNeedsRate` clamps and
  snaps, NaN to 1. `VoxelSession.NeedsRate` and `RelaxedNeeds` are separate and combine into `Needs.RateFactor` (rate × 0.5 if relaxed).
  Only the three drives change; stamina, health recovery, food and the collapse delay do not. A setting: no meter, digest or replay holds it.
- Flag `--needs-rate <x>`: below 0.5, above 4, off a 0.25 step or not a number is refused with a message. Extra: `--settings` opens the
  panel at the end of a scripted run (for screenshots).
- Panel: `Screens/SettingsPanel.cs` (Core), `UI/Overlays/SettingsLayout.cs` and `SettingsLayer.cs` (Game), key F8 (listed in the controls
  overlay), rows: the slider ×0.5 to ×4, view effects, relaxed needs; Up/Down choose, Left/Right or -/+ change, Enter/Space flip, Esc/F8
  close; a menu pause; keyboard only (no mouse). English in `content/locale` (`ui.json`, `keys.json`); `×` added to the glyphs.
- Telemetry: `session_summary` carries `needsRate` when not 1; the console summary prints "drive rate ×x"; the closing card unchanged.
- Docs: `running.md`; `playtest.md` has no needs section, left alone.
- Tests +25 (`NeedsRateTests`, launch options, a layout test at every canvas and scale, a session-log test). Bug found: the
  `EffectiveNeedsRate` helper was serialised (it would have broken byte-equal default saves); now `[JsonIgnore]`.
- Behaviour: session 2, needs on, 60 s: hunger 97.46 / 92.37, thirst 96.18 / 88.55, sleep 98.73 / 96.18 at rate 1 / 3 (each drop exactly
  3x); `needsRate: 3` in the save. Panel screenshots at 1280x720 and 800x600 with text scale 150 fit (the agent was unsure the 150 % took
  effect at 1280x720).
- Scripted runs: the baseline was the adaptive worktree's binary (same commit as the tip); only `r30-no-classes/declined.png` differed (an
  on-screen ms timing). No re-baseline.
- Counts: 9461 → 9486 passed, 3 skipped; without models 9444 passed, 45 skipped. The first full run was killed once at the shared memory
  cap (exit 143) and passed on the rerun.
