# Decisions for the game (Vocaterra)

The owner's decisions for this repository. The language core's decisions are in its own `docs/DECISIONS.md`
(`external/Vocaterra.NLP/docs/DECISIONS.md` in this checkout, pinned by the submodule): D1 CI, D2 the GPL-3.0 native libraries, D3 the
core's licence, D4 the OpenSubtitles terms, D5 the feed, D8 to D11 and D13 open core items, and D12 (the engine), which moved here on
2026-10-21. Status values: **open** (the owner has not decided), **decided**, **deferred** (the owner has set it aside; do not raise it).

| ID | Decision | Status | Blocks |
| --- | --- | --- | --- |
| D-game-1 | Game engine | decided for the prototype: MonoGame 3.8 DesktopGL (owner, 2026-10-21); final engine open, Godot .NET re-assessed after P10 | nothing in the prototype |
| D-game-2 | Licence of this repository | decided: MIT (owner, 2026-10-21: "the most permissive for now"; the repository is private) | nothing |
| D-game-3 | Continuous integration | deferred by the owner, 2026-10-21; do not raise | nothing in this plan |
| D-game-4 | Whether `content/game-v1` tracks the core's sample bundle or diverges from it | decided: diverge fully (owner, 2026-10-21) | nothing |
| D-game-5 | Art, font and sound sources while D-game-2 is open | decided: CC0 only (owner, 2026-10-21) | nothing |
| D-game-6 | When a noun counts as known (drawn in colour), and what typed input earns | decided (owner, 2026-10-21); amended the same day: spaced uses with growing gaps, the guide prompts due words | nothing |

## D-game-1: the game engine (prototype decided; final engine open)

**Question.** Which engine renders the game and plays its audio. The core's research report (core `docs/research`) suggested Godot .NET;
the core's high-level design also listed MonoGame, Bevy and macroquad, and Ebitengine. The core's language is C# (core ADR 0001), which
Godot .NET and MonoGame share.

**Owner, 2026-10-21.** The Terraria-inspired prototype uses MonoGame (3.8 DesktopGL), with a plan to perhaps move to Godot (.NET) later. The
layout from research R1 (`docs/research/PROTOTYPE_PLAN.md`), adopted by the owner the same day: `src/Vocaterra.Simulation` (no packages, no
engine), `src/Vocaterra.Game` (the `Vocaterra.Nlp.*` packages, no engine) and `src/Vocaterra.MonoGame` (the only engine code). Engine code never
goes into `src/Vocaterra.Host`, `src/Vocaterra.Simulation` or `src/Vocaterra.Game`. The final engine stays open; P10 (an optional Godot spike)
measures the cost of the move. G5 is replaced by P2.

## D-game-2: the licence of this repository (decided: MIT)

**Owner, 2026-10-21.** "Just make it the most permissive for now, it's a private project so it doesn't matter much at the moment." The
orchestrator chose **MIT** (the common permissive licence; 0BSD or MIT-0, which drop the notice requirement, are a one-file switch if the
owner wants them). The repository is private on GitHub. The `LICENSE` file is added by the next infrastructure milestone (G7).

**What MIT does not change.** Third-party parts keep their own terms: the content bundle's Wiktionary lexicon is CC BY-SA 4.0 and keeps its
notice in `content/game-v1/ATTRIBUTION.md`; the core's packages are Apache-2.0 (core D3); MonoGame is Ms-PL; the sherpa-onnx native
libraries carry GPL-3.0 code (core D2), so a distributed build that includes them waits on core D2. D-game-5 (CC0 assets only) stays in
force until the owner changes it.

## D-game-3: continuous integration (deferred)

The owner deferred CI on 2026-10-21, with the core's D1. Do not raise it. The parked workflow stays in `.github/workflows-pending/ci.yml`;
nothing under `.github/workflows/` is written, because the push token lacks the `workflow` scope.

## D-game-4: does `content/game-v1` track the core's sample or diverge (decided: diverge)

**Question.** The bundle began as a copy of the core's sample (`content/v1` in the core, a blacksmith, spells and items). The core keeps
changing that sample (M17b-core added the generic intents `confirm`, `deny`, `farewell`, `thank` and `introduce_self`, a blacksmith
`confirming` state and prompt text in M19d). Options: (a) track the sample and re-sync on each core release, so the game is also the
sample's integration test; (b) diverge now, with the game's own scenes and words replacing the sample's, and re-sync only what the game uses.

**Owner, 2026-10-21: diverge fully (b), after G2.** G2 brought the bundle level with the core's sample at `db39999` (generic intents, the
blacksmith's `confirming` state, prompt text). From now on the game owns all of `content/game-v1`: no re-sync on core releases. A milestone
that wants something new from the sample copies only that piece, rewords it to pass the game's A1 check and records it in `provenance.json`.
What G2 measured: copying was conflict-free, but each re-sync needed rewording (three templates failed A1), a rewrite of the sample's
`ATTRIBUTION.md` and a reset of the bundle id.

## D-game-5: asset sources (decided, CC0 only)

**Owner, 2026-10-21.** While D-game-2 is open, every art, font and sound file committed to this repository is CC0 (for example the Kenney
packs and CC0 OpenGameArt sets); placeholders drawn in code come first. Each file is recorded in `assets/ATTRIBUTION.md` with its source URL
and licence. Research section 8 lists the candidates.

## D-game-6: when a noun is known (decided)

**Owner, 2026-10-21.** A noun counts as known after **three correct spoken uses in three separate turns**. Its colour grows by a third per
correct use; nothing decays in the prototype. Only nouns in the game's noun catalogue are counted. **Typed input** earns full credit until
P5 (spoken input) lands; from P5 it earns half credit by default, as a setting.

**Amended by the owner, 2026-10-21 (after P4): spaced repetition.** Three correct uses in a row within seconds must not make a word known.
- **Growing gaps.** A word's first correct use earns colour at any time. The second earns colour only if at least **10 minutes** have passed
  since the first credited use, or it falls in a **different play session**. The third earns colour only if at least **1 hour** has passed
  since the second credited use, or it falls in a different play session. Time is the host's stored time (play time, deterministic), never
  the clock in the evaluation path; play sessions exist once saves do (P9), and the progress records the session of each credited use now.
- **Early uses are logged, not coloured.** A correct use that comes before its gap still counts in the journal (said, correct) and the
  guide still answers it, but the colour does not move.
- **The guide prompts due words.** When the player talks to the guide and a word is due (started, not known, its gap has passed), she
  points at the nearest example in the world and asks what it is ("¿Qué es esto?"); a correct answer earns the spaced credit. Uses the
  player starts also count when they are due.
- Typed input's half credit from P5 still applies per credited use.

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
