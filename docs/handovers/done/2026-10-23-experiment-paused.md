# Handover: paused by the owner at 94 % of the weekly usage (2026-10-23)

> Moved on 2026-11-03 from `.claude/agents/orchestrator.md`, where it stood as a dated section; the text is unchanged. Its CI section, updated on 2026-10-31, stays in the orchestrator.

**Where things stand**
- `main` (`1e3d2cc`) holds the owner's whole queue: S13 to S28, S17b, S19b, S20b, M29j, M30, M49 and M50, all gated and pushed.
- `feature/experiment` holds the X series (see `docs/game/IMPLEMENTATION_PLAN.md` section 4.5):
  - merged and gated: X00 research, X01 saves across bundles, X04 the summary card and the "no sé" retry, X05 autosave and crash report,
    X02/M51 `voy comer`, X03 book polish, X08 text scale;
  - **X06 (the review deck) was stopped mid-work.** Its partial work is the unbuilt, ungated WIP commit `0ab3300` on the pushed branch
    `X06-review-wip`, with its worktree `.claude/worktrees/agent-adc1cf0060b8dbf8d`. Resume by launching X06 again on that branch:
    inspect the diff, finish it, gate it.
  - queued after X06: X09 (playtest evidence), then X07 (`--mic-lab` and `--bench-route` for the owner to run).
- `feature/prototype` is retired.

**Open owner questions**
- Listening to the new WAVs and sounds (S17, S20b, S24, S25, S26, S27).
- `Puedes decir` versus "You can say".
- X00's questions: R05 early stop in the pre-check, R12 adaptive lessons, R14/R15/R18 content before or after the pilot, R11 title screen,
  the mic lab, GPU bench and pre-flight runs, the pilot date, voices for new NPCs.
- Whether and when `feature/experiment` goes to `main`. `main` still has a rare M49 fuzz flake (O4, seed `7oEL4lRaLMu2`) that X02's
  one-file generator fix cures.
- M27a2: obsolete since R02 (`content/v1` and its grammar step are gone; the grammar step is required on `content/game-v1`).
