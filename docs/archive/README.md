# Archive

Documents that no longer describe the project as it is or a plan anyone will act on. They are kept for their reasoning and evidence,
never updated, and each starts with a note saying when it was archived and what to read instead.

- **Path:** a document keeps its original folder under `archive/`, so `docs/game/r16-phase2.md` became
  `docs/archive/game/r16-phase2.md`. An old reference is found by adding `archive/` after `docs/` (names before 2026-11-03 were
  upper case: `R16_PHASE2.md` is now `r16-phase2.md`).
- **What moves here:** a superseded design or proposal, a finished migration plan, a description of something that no longer
  exists. A plan whose series is done but whose ids are still cited (the S and C series) stays where it is with its status. Records
  have their own homes and never move here: handovers go to [`handovers/done/`](../handovers/README.md), research and QA reports stay
  in their folders.
- **How:** `git mv` the file, add the note at its top and a row below, and repair links and literal paths to it in the same change.

| Archived | Document | What it was | Read instead |
| --- | --- | --- | --- |
| 2026-11-03 | [organisation-plan.md](organisation-plan.md) | A proposed documentation layout (2026-11-03), not carried out | [The documentation rules](../process/documentation.md) |
| 2026-11-03 | [game/r16-phase2.md](game/r16-phase2.md) | R16 phase 2: moving the engine-free logic into `Vocaterra.Game.Core` (done 2026-10-28) | [Game architecture](../game/architecture.md) |
| 2026-11-03 | [game/game-projects-layout.md](game/game-projects-layout.md) | The Game and Game.Core folder report (2026-10-27, done 2026-10-28) | [Game architecture](../game/architecture.md) |
| 2026-11-03 | [game/research/prototype-plan.md](game/research/prototype-plan.md) | R1's plan for the 2D Terraria-like slice, retired by D-game-8 | [The prototype brief](../game/design/prototype-brief.md) |
| 2026-11-03 | [nlp/history/high-level-docs.md](nlp/history/high-level-docs.md) | The original design note for the voice recognition and feedback system (2026-10-18) | [The pipeline](../nlp/pipeline.md) |
