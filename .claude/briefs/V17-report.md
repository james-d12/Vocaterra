# V17's report (agent, 2026-10-31), kept for the next orchestrator session

V17 was built by an Opus 5.5 agent at xhigh from `.claude/briefs/V17-manana-en-el-pueblo.md`. The agent was asked to wrap up early so the
orchestrator could hand over to a fresh session.

**State at handover:** the branch `V17-manana-en-el-pueblo` is at `7176734` and pushed. It is clean and **not gated**.

**The orchestrator checked:**
- the branch matches its remote;
- 77 files changed (2357 insertions, 156 deletions);
- nothing under `src/Vocaterra.Nlp*`, `docs/nlp/`, `.github/`, either plan, either decisions file, `CLAUDE.md`, `.claude/`, or `bin/`, `obj/` or `artifacts/`.

**Not yet done:**
- reading the new Spanish in the diff;
- the gate;
- the spot-checks;
- the agent's four "left undone" items below.

Delete this file once V17 is recorded in the plans.

The agent's report follows.

---

**V17 report.** Branch `V17-manana-en-el-pueblo`, final commit `7176734`, pushed. `git status` is clean and nothing of mine is still
running. I wrapped up early because you asked me to.

**What was built**
- **1. The lesson** `manana-en-el-pueblo` (a2, session 9, 14 steps). It goes Luz at the bakery, then Domingo at the smithy, then Mar at
  the river. An explain step says when each future is used: "Voy a comprar pan" for a plan the learner already has, "Mañana compraré
  pan" for a promise or a prediction. Reviews: `compre-dos-panes`, then `luz-compro-pescado` or `voy-a-comprar-pan`. All four answer
  steps are `tense: fut`.
- **2. The later markers.** All of M57's markers are now in every `state_action` grammar, at both ends. I also added `para` plus a
  village name after `buscar`'s item. The fuzz project passed right after the edit (78 tests, digests unmoved).
- **3. The book** shows compraré, comprarás, comprará, iré and buscaré (taught) and hará, tendré, trabajará and pescará (heard). A test
  checks it.
- **4. No order in session 9.** The four orders in the plan are all done. A fifth order to Luz would replay her single
  thanks-and-reward line and pay her 4 coins again, which changes the coin path V15 is built on.
- **5. Coin path:** the learner arrives with 0 coins, 3 apples, 2 loaves and the axe, and leaves with the same.
- **6. Mar tells the day of tomorrow.** A teller speaks of himself in the first person, so Mar says `pescaré`. That keeps Luz's `hará`
  and Domingo's `trabajará` in the third person, the person the learner retells. `pescará` is heard from Domingo instead.
- **7. Plans in free talk:** `baker-open` now takes plans. `fisher-open` does not: the lint's confusion check gives `buscaré madera` and
  `busqué madera` to `ask_directions` there.
- **8.** Eight check items.
- **9.** New tests: `TomorrowLessonTests`, `TomorrowSpokenTests`, the skip to session 9, and new `VillageTalkTests` cases.
- **10.** The content loop and the docs now say the stream digests run on frozen inputs since M57.

**Rows**
- Said: `manana-comprare-pan`, `buscare-madera`, `ire-al-rio`, `luz-hara-pan`.
- Heard: `que-haras-manana`, `tendre-pan` "¡Qué bien! Mañana tendré pan fresco.", `luz-necesita-madera`, `mar-pescara`,
  `el-dia-de-manana`, `que-hara-luz`.
- Changes from the plan (my Opus pass): Luz's "¡Qué bien!" and "fresco"; Mar's "Y yo pescaré"; Domingo's "toda la tarde" line. No rows
  dropped.

**Core tries:** every grade was right, so no new core gap. One old limit remains: a word outside a constrained prompt's answer set gets
`vocab.wrong` (for example `usted comprará pan` at a lesson step).

**V16's machinery** lacked nothing.

**Recognizer:** all four said rows were heard word for word at 1.00.

**Tests:**
- Before: 6659 passed and 3 skipped. After: 6771 passed and 3 skipped, with models (`scripts/test.sh full`).
- Format and the build with warnings as errors are clean.
- The content loop's refresh passed the lint, cefr-check, `--validate-content` and the offline grammar check.
- The host demos print what the brief asks. `lesson-must-go` still shows the `vocab.wrong` V14 already noted.

**Bundle and goldens:**
- The bundle is `8f27dffd…`.
- The identity baseline got 6 new entries and 66 moved, all in the analyze stage only and all `state_action` sentences, in a commit of
  their own. My commit message there wrongly says "and the ones after it".
- No end-to-end golden moved.

**Bugs found and fixed:** none in the game. All the failures I fixed were in my own test assumptions.

**Left undone (you asked me to stop):**
- the scripted runs after the change, and the comparison with the before capture (deleted for disk space);
- the learner's route from session 8, then free talk with each NPC;
- the timed `--lesson-run` and its minutes estimate;
- CI's case without models, and the check for timed tests close to their budgets.
