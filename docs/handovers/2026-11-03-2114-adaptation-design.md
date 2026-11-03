# Handover: the adaptation design, revision 3

Created: 2026-11-03T21:14Z (UTC)
Workstream: the owner's Adaptation Design Document, iterated with the owner
Branch: `feature/adaptation-design`; HEAD: `35432471` (this handover is the next commit)
Worktree: `/home/james/Dev/Vocaterra`
Previous handover: none for this workstream

## Outcome

The owner's Adaptation Design Document (`docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`) is at revision 3.
It is the single source for the adaptation design: 69 pages, 19 sections, every subheading numbered N.k.

The work stopped with the document committed and pushed with this handover on pull request #30 (`feature/adaptation-design` into
`main`), waiting for the owner's review. The owner is iterating on the document round by round.

## Working tree and running work

- **Commits:** `96a8c7b4` (the review and revision 3) and `35432471` (the owner's round of 2026-11-03), pushed with this handover.
  Nothing is merged; the owner merges pull requests.
- **The owner's own file:** `docs/Vocaterra_Design.odt` is untracked. Leave it alone.
- **The document may be open in the owner's LibreOffice.** A lock file, `.~lock.Vocaterra_Adaptation_Design_Document.docx#`
  (ignored by git), sat in `docs/game/design/sources/`. A save from LibreOffice would overwrite the generated document, so ask the
  owner to close it without saving before you rebuild.
- **Running work:** no agents, processes or worktrees.

## Completed

- **The document:**

| Section | Holds |
| --- | --- |
| 1 | Needs first; the build order ("the pyramid of needs"); why the player acts |
| 2 to 8 | Corrected against the code, each with a "For implementers" part |
| 9 | Player needs: five needs, the stages, the links, sleep, death, the HUD, data, save version 6, Spanish, N01 to N07 and the needs check, the tests |
| 10 | The bread pilot; its specification is 10.5 to 10.18 |
| 11 to 15 | Crafting, the generalised pattern, the learner model, the presentation track (U01 to U04), the sequence and the criteria |
| 16 | Principles and risks |
| 17 | The source basis and the changes to the W plan |
| 18 | Owner decisions: 5 decided, 17 open, each with a recommendation |
| 19 | The review record: findings and evidence, the Copilot review, the survival-first proposal's assessment, follow-ups |

- **Decisions:** D-game-23 is decided in part and D-game-24 (player needs first) is recorded, both with the owner's words, in
  [`docs/game/decisions.md`](../game/decisions.md).
- **The generator:** `tools/adaptation-design/`. Its `build_rev3.py` rebuilds `word/document.xml` from revision 2 (git commit
  `4b312545`). Its `rebuild.sh` reproduces the committed document byte for byte.
- **Small edits:**
  - `CLAUDE.md`'s decision range;
  - the prototype brief's D-game-24 note;
  - the activities plan's status;
  - the docs index rows;
  - two literal paths to the renamed core-systems docx.

## Unfinished and blocked

- **The owner's review** of pull request #30 and of the document.
- **Owner decisions:** the document's section 18 holds 1 to 17, each with a recommendation. The pressing ones are:
  - 10, the cost of death;
  - 12, N06 and N07;
  - 13 to 17, from the owner's survival-first proposal.

  One more is open: whether needs come before the queued language follow-ups. That is a one-line change in section 15 and D-game-23.
- **Not authorised to build:** nothing in the document may be built until its milestones have rows in
  [`docs/game/implementation-plan.md`](../game/implementation-plan.md) section 4 and their decisions are taken (the document's
  section 1).
- **The orchestrator's follow-ups** (the document's 19.5) wait for the merge:
  - revise the activities plan as 17.3 lists;
  - add the N, W and U rows to the game plan;
  - record the D-game-10 amendment if the owner takes decision 4;
  - the queued follow-ups of [`2026-11-02-after-followups.md`](2026-11-02-after-followups.md) come first.
- **The docs-cleanup handover** ([`2026-11-03-1133-docs-cleanup.md`](2026-11-03-1133-docs-cleanup.md)) waited for PR #29, which
  merged at 19:56 UTC on 2026-11-03. Its other points were not checked here.

## How to work on the document (the owner's rules, 2026-11-03)

- **Everything goes into the document:** findings, second opinions, the owner's inputs (parsed and woven in, not copied) and open
  decisions (also recorded in `decisions.md`). Write no separate Markdown records or copies. The owner removed the review record that
  had been under `docs/research/`.
- **Gather code facts with Haiku agents** and keep the writing in the main session.
- **Edit the generator**, `tools/adaptation-design/build_rev3.py` (its docstring explains the helpers):
  - edits are anchored to revision 2's elements: `expect`, `set_text`, `replace`, `put_after`;
  - the builders in `xmlkit.py` write the document's own formatting, and their text takes `**bold**` and `` `code` `` markup;
  - the build stops if a subheading's written number is out of order;
  - cross-references say "section N" or "N.k". When numbers move, update the Markdown that cites them: `docs/README.md`,
    `docs/game/activities-plan.md` and `docs/game/decisions.md`.
- **Then rebuild and check:**
  1. Run `tools/adaptation-design/rebuild.sh <out-dir>`. It writes `<out-dir>/out.docx`, with revision 2 beside it as
     `<out-dir>/rev2.docx`.
  2. Validate with the docx skill's `scripts/office/validate.py <out-dir>/out.docx --original <out-dir>/rev2.docx`. It needs
     `defusedxml`: install it with `pip install --target <dir> defusedxml` and set `PYTHONPATH=<dir>`.
  3. Render with the skill's `scripts/office/soffice.py --headless --convert-to pdf --outdir <dir> <out-dir>/out.docx`, turn the
     changed pages into images with `pdftoppm`, and look at them.
  4. Copy `out.docx` over the document. Commit and push to `feature/adaptation-design` only when the owner asks; never to `main`.
  5. Update the pull request's description to match (`gh pr edit 30`).
- **Size:** the document is 31 MB, mostly its 15 images, and each committed version adds about that much to the history.

## Verification

- **The document:** `validate.py` passed (426 to 1,802 paragraphs). It rendered in LibreOffice at 69 pages, and the changed pages
  were inspected.
- **The generator:** `rebuild.sh` reproduced the committed document byte for byte.
- **The Markdown:** `MarkdownLinkTests` passed (1 test), and `git diff --check` was clean.
- **Not run:** the build, the full suite and the merge gate (documentation only). CI on pull request #30 was not checked.

## Next steps

1. **Check the state against git:** the branch, the HEAD (this handover's commit) and pull request #30. Then read the document's
   sections 9, 10, 18 and 19.
2. **Record the owner's decisions:** ask which of section 18's decisions the owner wants to take. Record each answer in section 18
   and in `docs/game/decisions.md`, and rebuild the document through the generator.
3. **Keep iterating with the owner** on the document. Commit and push each round only when the owner asks, and update the pull
   request's description each time.
4. **When the owner merges pull request #30:**
   - move this handover to `done/`;
   - close the docs-cleanup handover if its points are done;
   - start the follow-ups in the document's 19.5, in order.
