# Decisions for the game (Vocaterra)

The owner's decisions for the game (`game/`). The language core's decisions are in its own file, `nlp/docs/DECISIONS.md`: D1 CI, D2 the GPL-3.0 native libraries, D3 the
core's licence, D4 the OpenSubtitles terms, D5 the feed, D8 to D11 and D13 open core items, and D12 (the engine), which moved here on
2026-10-21. Status values: **open** (the owner has not decided), **decided**, **deferred** (the owner has set it aside; do not raise it).

| ID | Decision | Status | Blocks |
| --- | --- | --- | --- |
| D-game-1 | Game engine | decided for the prototype: MonoGame 3.8 DesktopGL (owner, 2026-10-21); final engine open, Godot .NET re-assessed after P10 | nothing in the prototype |
| D-game-2 | Licence of this repository | decided: MIT (owner, 2026-10-21: "the most permissive for now"; the repository is private) | nothing |
| D-game-3 | Continuous integration | deferred by the owner, 2026-10-21; do not raise | nothing in this plan |
| D-game-4 | Whether `content/game-v1` tracks the core's sample bundle or diverges from it | decided: diverge fully (owner, 2026-10-21) | nothing |
| D-game-5 | Art, font and sound sources while D-game-2 is open | decided: CC0 only (owner, 2026-10-21) | nothing |
| D-game-7 | One repository for the game and the core | done (owner, 2026-10-21): `nlp/` and `game/` as sibling folders of `james-d12/Vocaterra`, project references, one orchestrator and one gate | nothing |
| D-game-6 | When a noun counts as known (drawn in colour), and what typed input earns | decided (owner, 2026-10-21); amended the same day: spaced uses with growing gaps, the guide prompts due words; amended again for the prototype brief: the world in full colour, the grey atlas behind `--greyscale` | nothing |
| D-game-8 | The prototype brief adopted | decided (owner, 2026-10-21): 3D voxel, first person, MonoGame; the 2D slice parked (tag `2d-slice-p7`, removed at S4); Latin American neutral Spanish; nothing gated behind language or voice | nothing |
| D-game-9 | Speech rules for the prototype | decided (owner, 2026-10-21, from the brief) | nothing |
| D-game-10 | Learning measurement for the prototype | decided (owner, 2026-10-21, from the brief) | nothing |
| D-game-11 | The Noun Town review data | decided (owner, 2026-10-21): raw review JSON never committed; summaries and fetch scripts in `docs/research/noun-town/` | nothing |
| D-game-12 | A competent Spanish reviewer for the phrase sheet and recordings | deferred by the owner (2026-10-21) with an interim workaround (provisional rows); the owner approved the LLM review pass (Opus) on 2026-10-21 | nothing; the playtest's content-trust gate stays provisional |
| D-game-13 | English guidance for a player with no Spanish | decided (owner, 2026-10-22): on-screen English text only, fading per phrase; only Spanish is spoken | nothing |
| D-game-14 | World scale and look | decided (owner, 2026-10-22): about 384 x 384 x 128, rich voxel (D-game-8 kept), close to the brief's references A to C, target a real GPU with quality presets | nothing |
| D-game-15 | The opening and the guide's lesson | decided (owner, 2026-10-22): start alone at the player's cliff camp; a gradual guide lesson of 5 to 10 minutes; the phrase set grows beyond D-game-12's interim limit | nothing |
| D-game-16 | The learner's book: words used and lessons completed | decided (owner, 2026-10-22): record every spoken and typed input; a book shows the nouns and verbs used and the lessons completed | nothing |

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

**Amended for the prototype brief (owner, 2026-10-21, D-game-8).** The world is drawn in full colour. The counts (heard, read, said, said
correctly), the journal, the typed-half and spoken-full credit and the spaced gaps stay and now also serve phrases (the delayed-recall check).
The grey atlas stays behind `--greyscale` for an A/B in the playtest. Colour never fades.

## D-game-8: the prototype brief adopted (decided)

The owner's Prototype Design Brief v1.2 (`docs/Vocaterra_Prototype_Design_Brief.docx`, as Markdown in `docs/PROTOTYPE_BRIEF.md`) defines the
prototype's scope, scenario, guardrails and gates; `docs/SCENARIO_PLAN.md` is its plan (the S series). World: 3D voxel, first person, MonoGame
(D-game-1 unchanged; engine code only in `src/Vocaterra.MonoGame`). The 2D Terraria slice is parked: its code is tagged `2d-slice-p7` and removed
once the 3D world reaches gathering and crafting (S4). Spanish variety: Latin American neutral (core ADR 0005). Nothing in the game is gated
behind language or voice; every failure has a recovery path. Work on the S series lands on the integration branch `feature/prototype`
(owner, 2026-10-21), not on `main`.

## D-game-9: speech rules (decided, from the brief)

ASR is used for intent in a constrained phrase set. An uncertain recognition (not communicated, or a per-word confidence mean under the
threshold) asks for a retry with help and is logged as a voice event, never as a language failure. A one-time mic check; visible recording and
thinking states; cancel and a timeout; a persistent "oí / esperaba" line; replay and slower audio; typed and button fallback; "no sé" always
available and never shamed.

## D-game-10: learning measurement (decided, from the brief)

A hand-authored phrase sheet (`content/PHRASES.md`) is the source of truth. Each phrase is tracked through shown-card, supported, unaided,
choice-only (a recognition signal) and "no sé" uses. No automated CEFR placement or global score. A pre-check, a post-check and a later recall
check. Telemetry is local only (JSONL in the session folder).

## D-game-11: the Noun Town review data (decided)

The raw Steam review JSON (about 450 KB of third-party text) is never committed: `.gitignore` covers `scripts/noun_town/*.json` and
`docs/research/noun-town/*.json`. The two summaries and the fetch scripts are committed and live in `docs/research/noun-town/` (moved in S0).

## D-game-12: the Spanish reviewer (deferred, interim workaround)

The brief wants every phrase, translation, accepted response and recording checked by a competent Spanish speaker before testing; none is
available yet. Interim workaround until a speaker checks the sheet: (1) the phrase set stays at the brief's five phrases plus the five service
words (`hola`, `gracias`, `adiós`, `no sé`, `ayuda`), all textbook A1, so a later one-hour check closes the gap; (2) every row passes the core's
lint `--strict` and `cefr-check` at A1 (automated and supplementary, never approval); (3) an LLM review pass at authoring time (Opus, approved
by the owner on 2026-10-21, run by the orchestrator in S5) checks naturalness, Latin American neutral usage and the English meaning of every row
and accepted variant, recorded in `PHRASES.md` as `reviewer: llm-opus (provisional)` with the date; (4) the owner listens to every
pre-rendered WAV in S9 for cut-offs and text-audio mismatches; (5) the playtest checklist marks the content-trust gate provisional and testers
are told the Spanish has not been checked by a native speaker. Rows marked provisional are re-checked when a speaker is found.

## D-game-13: English guidance (decided)

**Owner, 2026-10-22, after playing the S12 build.** "We should use English to help the person be guided through; some bits can be a bit
confusing if you know no Spanish at all." Asked how, the owner chose on-screen text: the English is written in a guide panel and only Spanish
is spoken, so the ear stays on Spanish and no English recordings are needed. The English fades per phrase as S8's scaffolds do (a new first
rung: English, card and audio; then card and audio with the English on the help key; then audio; then nothing), and the help key always shows it
(rule 8). English also carries the controls, first-time tips, objectives (bilingual at first) and the feedback after a turn, in game content.
Plan: `docs/SCENARIO_PLAN.md` section 8, S16.

## D-game-14: world scale and look (decided)

**Owner, 2026-10-22.** "Start on a cliff side ... make the map graphics better, with high mountains, and dense forest, with vegetation, so it's
much more realistic. And make the map much larger." Then: "It should look close to the reference images used in the documentation" (the
brief's references A, B, C). Answers: about 384 x 384 blocks and 128 tall (from 96 x 96 x 32); stay with blocks, made rich (textures, several
tree species, dense undergrowth, sun shading, ambient occlusion, sky, clouds, haze, water), so D-game-8 holds; the target machine is one with a
real GPU, with `--quality` presets so this VM (software GL) still runs and takes screenshots. The brief's art-direction rules still apply:
Vocaterra's own identity, no Minecraft assets, textures and models drawn in code or CC0 (D-game-5); the reference images are never committed
or copied. Plan: section 8, S13 to S15.

## D-game-15: the opening and the guide's lesson (decided)

**Owner, 2026-10-22.** "It should be more gradual. For example the guide should teach you a few things (this interaction alone should perhaps
be 5 to 10 minutes). Also I want you to start on a cliff side in your own camp on your own." The player wakes alone at their own camp on a cliff
ledge, learns the controls in English, walks down a trail to the guide, and the guide's lesson teaches greetings, yes and no, "¿Qué es esto?",
please and thank you, and the getting-unstuck phrases before the camp task; the session ends by lighting the fire at the player's own camp.
This departs from the brief's "Start in the forest" (the camp is now the player's, on the cliff) but keeps "no quiz at spawn". It also grows the
phrase set beyond D-game-12's interim limit (the brief's five phrases plus five service words); every new row is still textbook A1, passes lint
`--strict` and `cefr-check`, and gets the Opus pass, and the later native-speaker check grows accordingly. Plan: section 8, S17.

## D-game-16: the learner's book (decided)

**Owner, 2026-10-22.** "A book that can be opened that tracks the nouns / verbs that the player has used, for this we need to track all the
input they provide to us (speaking / writing). I also want a UI (perhaps in the book too), to track all completed lessons." The game records
every line the learner speaks or types, with the core's reading of each word, and folds it into a record of the nouns and verbs used and the
lessons completed. The journal (J) becomes that book, with the lessons on a page of it. The record lives in the save and the session log, on
the player's machine only. It is game state (game words live in `game/`); the core already returns the word readings, so no core change.
The orchestrator's choices, open to the owner's change: verbs listed by infinitive with the forms used; only words the core read from its
lexicon are listed (recogniser noise is not); the save keeps the newest 500 lines. Plan: section 8, S19 (the record) and S20 (the book).

## D-game-7: one repository for the game and the core (done)

**Owner, 2026-10-21.** "My plan is to actually merge the game and the NLP back together, for simplicity and as they both update they can
update together." The orchestrator stopped after P7 at the owner's request; P8 and later waited for the merged repository. The parts of the
game that existed only because the two were separate (packages only, the submodule and its feed, `docs/CORE_REQUESTS.md`, two
orchestrators, two gates) are listed in `IMPLEMENTATION_PLAN.md`, section 4, the handover.

**Done, 2026-10-21 (the owner, with Claude).** Layout: `nlp/` (the core, with its own plan, decisions, docs and Apache-2.0 licence) and
`game/` (this half, MIT) as sibling folders of `james-d12/Vocaterra`; the shared solution, build settings, package versions, `nuget.config`,
`CLAUDE.md`, orchestrator and gate at the root. The core's history was rewritten under `nlp/` and merged, so `git log` follows its files.
Consumption: project references (no feed, no pack step). Ids on both sides are unchanged. Licences stay per folder; a single licence for the
repository is open only if the owner wants one. The old `james-d12/Vocaterra.NLP` repository is to be archived by the owner.

## Already decided

- **The core by project reference** (D-game-7; until 2026-10-21 "packages only" from a submodule's local feed). The game never copies core
  source (core ADR 0009); a missing call is a core milestone in `nlp/IMPLEMENTATION_PLAN.md`.
- **Console host until D-game-1 is decided.**
- **Game words live here:** scenes, characters, items, spells, prices, rewards, dialogue and the meaning of each effect kind.
- **The content tool** is the core's ContentBuilder run from source, through the root `scripts/content.sh`.
- **Model policy:** Haiku 5.5 for delegated work by default; Sonnet 5.5 for design-heavy milestones; Opus only when the owner asks.
- **One orchestrator** for both halves since D-game-7 (before it, one per repository); it never lets a game milestone edit `nlp/`.
