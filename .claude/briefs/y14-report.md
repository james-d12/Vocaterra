# Y14 report: QA fixes for the settlement slice

(The agent's report, 2026-11-12, condensed by the orchestrator.)

Branch `Y14-qa-fixes`, HEAD `7c4e2dbf` (four commits on `28da437e`); worktree `/home/james/Dev/Vocaterra/.claude/worktrees/y14`.

1. **The fail-forward's phrase (QA-1).** `GuideSession.AfterTurn` hard-coded `PhraseIds.RequestWater` for every fail-forward; it now
   credits the first phrase in play at the turn's prompt (`support.InPlay[0]`: the NPC's focus or the lesson step's phrase; the same
   snapshot feeds the «no sé» credit), and nothing when the prompt has none. The stall's second-visit credit stays `pedir-agua`. Test in
   `InnkeeperTests` («no sé» at the inn credits `quiero-una-habitacion`). The transfers (`pedir-agua transfer at baker (pan)`, `at
   villager (terreno)`) come from `PhraseOf`/`Classify`, not that constant (the «quiero …» mapping matches `pedir-agua`): left for the
   owner, as the report says.
2. **The wake card under the sleep prompt (QA-2).** While the death screen is up the bed's prompt and the plot's HUD line are hidden
   (`DeathScreenLayer.HintWhileUp`, in `VoxelGame`). `DeathScreenHintTests` (3). Screenshots `artifacts/y14/wake-before.png`,
   `wake-after.png` of the y14 worktree. Not fixed: the crosshair over the card's English line.
3. **«sí» to a class offer read "unknown" (QA-4).** The class offer is a lesson-layer overlay; the NPC stays at its own prompt
   (`villager-open`), which takes no `confirm`, so the core graded «sí» `unknown` while `OnClassTurn` started the class (every teacher's
   class offer, not only the mayor's). Grading at `guide-offer` broke later free talk and was reverted. The fix: in `AfterTurn`, when a
   class offer stood and the class took a Sí or No, the shown turn becomes `confirm`/`deny`, understood; credits and the NPC's state
   untouched. Tests in `ClassTests` and `PlotPurchaseTests`; one `PlotPurchaseTests` assertion that pinned the old «sí» "not understood"
   now uses «No, gracias.».
4. **r30's baker (QA-5).** The row now takes the order («sí, por favor»), hands the wood over at the next talk («aquí tienes la
   madera»), then buys bread: `order_accepted`, `order_delivered` (4 coins), `quiero-dos-panes supported`. The row in
   `docs/game/scripted-runs.md` matches.

Runs: r40 to r43 match the earlier captures but for frame timing (stdout compared with timing masked; `compare-runs.py` ran over two
minutes and was stopped); r30 differs only as in 4. Gate: build 0 warnings; 11,463 passed, 3 skipped; format, validate, A2 gate clean;
goldens unmoved. Not done: QA-3 (out of scope).
