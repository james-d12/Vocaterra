# Decisions for the game (Vocaterra)

The owner's decisions for this repository. The language core's decisions are in its own `docs/DECISIONS.md`
(`external/Vocaterra.NLP/docs/DECISIONS.md` in this checkout, pinned by the submodule): D1 CI, D2 the GPL-3.0 native libraries, D3 the
core's licence, D4 the OpenSubtitles terms, D5 the feed, D8 to D11 and D13 open core items, and D12 (the engine), which moved here on
2026-10-21. Status values: **open** (the owner has not decided), **decided**, **deferred** (the owner has set it aside; do not raise it).

| ID | Decision | Status | Blocks |
| --- | --- | --- | --- |
| D-game-1 | Game engine | open | G5 (the first engine host) |
| D-game-2 | Licence of this repository | open | any public release or distribution |
| D-game-3 | Continuous integration | deferred by the owner, 2026-10-21; do not raise | nothing in this plan |
| D-game-4 | Whether `content/game-v1` tracks the core's sample bundle or diverges from it | open; G2 proposes | the shape of G2 to G4 |

## D-game-1: the game engine (open)

**Question.** Which engine renders the game and plays its audio. The core's research report (core `docs/research`) suggested Godot .NET;
the core's high-level design also listed MonoGame, Bevy and macroquad, and Ebitengine. The core's language is C# (core ADR 0001), which
Godot .NET and MonoGame share.

**Until it is decided.** The host (`src/Vocaterra.Host`) stays an engine-agnostic console program. No engine packages, no engine types and no
rendering code go into it, and the packages are the only dependency. A decision starts a milestone (G5) that builds the first engine host
on the same call sequence, in its own project.

## D-game-2: the licence of this repository (open)

**Question.** Under which licence this repository is published. The core is Apache-2.0 (core D3, decided 2026-10-21), and its packages carry
that licence. The game's own licence is separate and is not chosen.

**Until it is decided.** This repository grants no permission to use, copy or modify it. The content bundle's Wiktionary lexicon is
CC BY-SA 4.0 and keeps its notice in `content/game-v1/ATTRIBUTION.md` whatever the game's licence is. The sherpa-onnx native libraries
carry GPL-3.0 code (core D2): the game does not distribute a build that includes them until D2 is settled in the core.

## D-game-3: continuous integration (deferred)

The owner deferred CI on 2026-10-21, with the core's D1. Do not raise it. The parked workflow stays in `.github/workflows-pending/ci.yml`;
nothing under `.github/workflows/` is written, because the push token lacks the `workflow` scope.

## D-game-4: does `content/game-v1` track the core's sample or diverge (open)

**Question.** The bundle began as a copy of the core's sample (`content/v1` in the core, a blacksmith, spells and items). The core keeps
changing that sample (M17b-core added the generic intents `confirm`, `deny`, `farewell`, `thank` and `introduce_self`, a blacksmith
`confirming` state and prompt text in M19d). Options: (a) track the sample and re-sync on each core release, so the game is also the
sample's integration test; (b) diverge now, with the game's own scenes and words replacing the sample's, and re-sync only what the game uses.

**G2 proposes (a) for the generic intents and the blacksmith, now, and (b) for everything scene-specific from G3 on.** The owner has not
accepted this yet; G2 records the decision here when it is made.

## Already decided

- **Packages only.** The game references the core's `Vocaterra.Nlp.*` packages, never its projects, and never copies its source (core ADR 0009).
  A missing call is a row in `docs/CORE_REQUESTS.md`.
- **The core is a git submodule** at `external/Vocaterra.NLP`, consumed through its local feed `artifacts/feed` (core D5, 2026-10-21). No
  hosted feed. The pinned commit is the gitlink; bumps follow the orchestrator's procedure.
- **Console host until D-game-1 is decided.**
- **Game words live here:** scenes, characters, items, spells, prices, rewards, dialogue and the meaning of each effect kind.
- **The content tool** is the core's `vocaterra-content` 0.1.0 from the feed, through `scripts/content.sh`.
- **Model policy:** Haiku 5.5 for delegated work by default; Sonnet 5.5 for design-heavy milestones; Opus only when the owner asks.
- **Two orchestrators:** this repository is run by its own orchestrator; the core orchestrator runs only the core.
