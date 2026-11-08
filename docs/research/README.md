# Research

Studies, reviews and reports for both halves, each answering one question at one point in time. A report is a record: it is not
updated as the project moves on. Its outcome is recorded here, and a later report that replaces it says so in this table.

- **Naming:** `YYYY-MM-DD-<slug>.md`, dated by when it was written. A study with more than one file (data, scripts, recordings) gets a
  folder with the same kind of name.
- **Editing:** only a status note at the top (for example "Historical" or "Superseded by …") and link repairs after a move.
- **Area:** `game`, `core` or `process` (how the work is done).

| Date | Report | Area | Question | Outcome |
| --- | --- | --- | --- | --- |
| 2026-11-07 | [Water at rest (H07)](2026-11-07-water-at-rest-h07.md) | game | Does waking all water of the shipped scene move anything? | Yes: 1,201 cells in three clusters (waterfall sheet, river staircase, river mouth over the lake); a waterfall of one source over falling water is at rest if its bottom cell is in the pool's surface layer; the owner decides each cluster |
| 2026-11-04 | [Vocaterra Studio plan](2026-11-04-vocaterra-studio-plan.md) | game | How should a desktop editor let a person author the game's lessons, objectives, NPCs, voices, dialogue and English text? | An Avalonia app on a library that edits the JSON in place and shows the loaders' findings; milestones ST01 to ST08; replaces C10 once accepted; five owner questions |
| 2026-11-04 | [German in the game](2026-11-04-german-in-the-game.md) | game | What would the game and the core need so a player can learn German as well as Spanish? | The core is ready (Phase A done); the game needs a neutral pass first (T01 to T05), then the core's German pack and a German A1 bundle. Content and review are the main cost; seven owner decisions |
| 2026-11-01 | [Core systems review](2026-11-01-core-systems-review.md) | game | How does the owner's Core Systems Design Plan compare with the code? | The conversation half is mostly built, the survival-sandbox half is not; five conflicts for the owner. Led to [the W series](../game/activities-plan.md) |
| 2026-10-31 | [Development bottlenecks](2026-10-31-development-bottlenecks.md) | process | Where do development time and tokens go, and what would cut them? | Cost is context times turns; actions P1 to P5 (P2 became V18's `scripts/content-loop.sh`) |
| 2026-10-30 | [Mobile feasibility](2026-10-30-mobile-feasibility.md) (R2) | game | Can the same game ship on iOS and Android, and at what cost? | Yes; the engine-free split makes most of the port additive. Nothing planned |
| 2026-10-26 | [One engine and language packs](2026-10-26-engine-and-language-packs.md) | core | How can the core become language-neutral, with Spanish as a pack? | D14 phase A: M31, M52, M32 to M35 and M53, done and merged. Its session section is now [a handover](../handovers/done/2026-10-26-engine-and-language-packs.md) |
| 2026-10-23 | [Improvement backlog](2026-10-23-improvement-backlog.md) (X00) | game | What would improve the game most per unit of effort? | A ranked backlog (its section 2); the X series drew on it |
| 2026-10-22 | [Multilingual core](2026-10-22-multilingual.md) | core | What would German and a multi-language core take? | About a quarter of the core is Spanish; the target language becomes a bundle property. D14 (roadmap only) and ADR 0011 |
| 2026-10-22 | [LanguageTool](2026-10-22-languagetool.md) | core | Should LanguageTool check the Spanish we author? | Yes, at development time on a local server; became S23 and CI's `languagetool` job |
| 2026-10-21 | [Prototype research](2026-10-21-prototype-research.md) (R1) | game | How to build a 2D Terraria-like prototype on the core? | Built as P1 to P7, then retired by the prototype brief (D-game-8). Its plan is [archived](../archive/game/research/prototype-plan.md) |
| 2026-10-21 | [Noun Town reviews](2026-10-21-noun-town/noun-town-positive-reviews-summary.md) | game | What do players praise and criticise in Noun Town? | Summaries of 648 positive and [115 negative](2026-10-21-noun-town/noun-town-negative-reviews-summary.md) Steam reviews, with the fetch scripts |
| 2026-10-21 | [Content authoring](2026-10-21-content-authoring.md) | core | How can writing intents and bundles be made cheaper? | One intent costs 70 to 100 lines over six files; ranked recommendations in its summary |
| 2026-10-20 | [Scaling approaches](2026-10-20-scaling-approaches.md) | core | How to scale the Spanish processing without building a whole NLP application? | Keep the game infrastructure and the hand-written checks; a trained tagger at the input, LLM drafting at build time, an optional tutor later |
