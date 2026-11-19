# Decisions for the game (Vocaterra)

The owner's decisions for the game (the game projects, `content/game-v1`, `assets/` and `docs/game/`). The language core's decisions are in its own file, `docs/nlp/decisions.md`: D1 CI, D2 the GPL-3.0 native libraries, D3 the
core's licence, D4 the OpenSubtitles terms, D5 the feed, D8 to D11 and D13 open core items, and D12 (the engine), which moved here on
2026-10-21. Status values: **open** (the owner has not decided), **decided**, **deferred** (the owner has set it aside; do not raise it).

| ID | Decision | Status | Blocks |
| --- | --- | --- | --- |
| D-game-1 | Game engine | decided for the prototype: MonoGame 3.8 DesktopGL (owner, 2026-10-21); final engine open, Godot .NET re-assessed after P10 | nothing in the prototype |
| D-game-2 | Licence of this repository | superseded by D-game-20 (was MIT, owner, 2026-10-21) | nothing |
| D-game-3 | Continuous integration | active since 2026-10-31 (`.github/workflows/ci.yml`: build, test, offline grammar replay and the live LanguageTool job, on pushes to `main`, pull requests and manual runs); reopened by the owner on 2026-10-23; SonarCloud and models in CI are not built | nothing |
| D-game-4 | Whether `content/game-v1` tracks the core's sample bundle or diverges from it | superseded (owner, 2026-10-23, R02): there is one bundle; the core's sample is deleted and the core runs on `content/game-v1` | nothing |
| D-game-5 | Art, font and sound sources while D-game-2 is open | decided: CC0 only (owner, 2026-10-21) | nothing |
| D-game-7 | One repository for the game and the core | done (owner, 2026-10-21): `nlp/` and `game/` as sibling folders of `james-d12/Vocaterra`, project references, one orchestrator and one gate | nothing |
| D-game-6 | When a noun counts as known (drawn in colour), and what typed input earns | decided (owner, 2026-10-21); amended the same day: spaced uses with growing gaps, the guide prompts due words; amended again for the prototype brief: the world in full colour, the grey atlas behind `--greyscale` | nothing |
| D-game-8 | The prototype brief adopted | decided (owner, 2026-10-21): 3D voxel, first person, MonoGame; the 2D slice parked (tag `2d-slice-p7`, removed at S4); Latin American neutral Spanish; nothing gated behind language or voice | nothing |
| D-game-9 | Speech rules for the prototype | decided (owner, 2026-10-21, from the brief) | nothing |
| D-game-10 | Learning measurement for the prototype | decided (owner, 2026-10-21, from the brief) | nothing |
| D-game-11 | The Noun Town review data | decided (owner, 2026-10-21): raw review JSON never committed; summaries and fetch scripts in `docs/research/2026-10-21-noun-town/` | nothing |
| D-game-12 | A competent Spanish reviewer for the phrase sheet and recordings | deferred by the owner (2026-10-21) with an interim workaround (provisional rows); the owner approved the LLM review pass (Opus) on 2026-10-21 | nothing; the playtest's content-trust gate stays provisional |
| D-game-13 | English guidance for a player with no Spanish | decided (owner, 2026-10-22): on-screen English text only, fading per phrase; only Spanish is spoken | nothing |
| D-game-14 | World scale and look | decided (owner, 2026-10-22): about 384 x 384 x 128, rich voxel (D-game-8 kept), close to the brief's references A to C, target a real GPU with quality presets | nothing |
| D-game-15 | The opening and the guide's lesson | decided (owner, 2026-10-22): start alone at the player's cliff camp; a gradual guide lesson of 5 to 10 minutes; the phrase set grows beyond D-game-12's interim limit | nothing |
| D-game-16 | The learner's book: words used and lessons completed | decided (owner, 2026-10-22): record every spoken and typed input; a book shows the nouns and verbs used and the lessons completed | nothing |
| D-game-17 | In-game bug and feature reports | decided (owner, 2026-10-22): F8 saves a Markdown report with a screenshot, game state, recent turns and the session log tail; local files only, no upload | nothing |
| D-game-18 | Sound | decided (owner, 2026-10-22): ambient, interaction and progress sounds; CC0 packs plus code-synthesized sounds; no music; ambient and effects duck under the voices and are silent while the microphone records | nothing |
| D-game-19 | Verbs in the lessons | decided (owner, 2026-10-22): verbs early but gradual, across sessions (yo/tú in session 1, the other persons in session 2, ir a + infinitive and a few past forms in session 3), lessons replayable from the book; conjugation tables in the book | nothing |
| D-game-20 | One licence for the whole repository | decided (owner, 2026-10-23): Apache-2.0 for the game as well as the core; one `LICENSE` and `NOTICE` at the root (R01) | nothing |
| D-game-21 | Content as data and the L1 locale layer (the C series, `docs/game/content-plan.md`) | decided (owner, 2026-10-27 to 2026-10-29): English moves into a locale bundle `content/locale/` outside game-v1's hash (`en` only); content stays JSON with JSON Schema; objectives, NPCs and target-language words become data; the series runs after R16 phase 2, on `feature/content` | nothing |
| D-game-22 | The village and the lessons (the V series, `docs/game/village-plan.md`) | decided for phase A (owner, 2026-10-30): phase A now (sessions 4 to 6), phase B held (started 2026-10-31 on the owner's word); the plan's proposals for (b), (c), (e), (f), (h) and (i); the farmer is León; voices provisional (the plan's first candidates) and recorded, the owner listens afterwards; all work by Opus 5.5 at xhigh effort; 2026-10-31: V18 and the core's M56, M59 (before V14), M58 and M62 (before V15) join phase B; 2026-11-01, the follow-ups: models mixed (Opus for M65 to M67, M69 and V20, Sonnet for V21 to V23 and M68, Haiku for V24), M65's narrowings (a) to (c), name plates of the faced NPC depth-tested; 2026-11-02: V24's bugs first (V26), the off-prompt lines (M72, V27), the small points (M73, V28) and the speech folder by speaker; after the pull request: weather and `¿cómo estás?` intents (M74, V29), the near-miss rule (M75), a study of spoken off-prompt lines (`docs/handovers/done/2026-11-02-after-followups.md`) | the V series |
| D-game-23 | Activities and the adaptation (the W series, `docs/game/activities-plan.md`; the owner's Adaptation Design Document, `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`) | open, decided in part (owner, 2026-11-03; 2026-11-04: the design built on `feature/adaptive_design`, section 18.2's recommendations adopted provisionally, for the owner's confirmation): the queued follow-ups first, then player needs (D-game-24), then the W series; an overlay station; the stall buys bread for one coin; the pilot's instructions heard only, no imperative taught; the rest proposed (activities-plan section 12, the design document's section 18) | the W series |
| D-game-24 | Player needs: hunger, thirst, sleep, health and stamina (the adaptation design's section 9; the N series, proposed ids) | decided (owner, 2026-11-03): five needs, hunger, thirst and sleep in stages whose effects grow worse, pushing health and stamina; built after the queued follow-ups and before every other system, from session 1; real death with a cost; the meters pause in conversations, lessons, the book and every panel; stamina spent by gathering and a new sprint; amends D-game-8's guardrail and replaces D-game-23 (b) | the N and W series |
| D-game-25 | Lessons optional: classes the player takes on, not the game's goal (the adaptation design's sections 5.3 and 5.4; L01) | decided (owner, 2026-11-04): a short required introduction with Luna, every other lesson an offered class; sleep whenever tired (the sleep drive below 75), every sleep a night, no nap; orders open once their NPC is met (amended by D-game-30, proposed: the NPC offers its order and «sí» takes it on); Luz shows the player how to bake on request, session 11 an optional class; amends V12c and D-game-15, answers the design's decision 16 | L01, N03, W08, W10, W11 |
| D-game-27 | Handing an order over in Spanish (issue #42) | decided (owner, 2026-11-08): the learner says what they give; the item must be named, the count is optional; «no sé» or two misses hand the goods over anyway | nothing |
| D-game-30 | Orders are offered and taken on (issue #54) | proposed (2026-11-09, for the owner's review on the pull request): the NPC offers its order when the talk opens and «sí» gives it; «no» puts it off until the next day; a class that tells an order gives it at its step; amends D-game-25's "orders open once their NPC is met" | nothing |
| D-game-31 | Day, night and weather (the E series, `docs/game/weather-plan.md`) | decided (owner, 2026-11-10): a free-running day and night cycle first, then weather in spells (sunny, cloudy, rain) with a new roll on waking; the first version is the atmosphere and truthful NPC answers, no gameplay effect; built in a new worktree from `main` | nothing |
| D-game-32 | The inn, bought land and the campsite (the Y series, `docs/game/settlement-plan.md` section 8) | decided (owner, 2026-11-10): the first slice built now on `feature/settlement`, in a new worktree from `main`; the inn's bed free once the innkeeper has given the room; placing refused in the village and on the roads, the camp kit and taking planks back only on owned land; saves break (version 8); amends D-game-15, D-game-24, D-game-25 and the village plan's principle 1, and takes W22's first lot and W23's stake out of the hold | nothing |
| D-game-34 | A tall procedural world round hand-crafted regions (the B series, `docs/game/biomes-plan.md`) | decided (owner, 2026-11-15): an endless, streamed world, y −64 to 320, generated outside authored regions (biomes, great ranges, rivers, lakes and sea above; rock layers, caves, lava and iron, tin and copper ores below); the valley the first region, opened by a pass to the east; amends the prototype brief's LEAVE FOR LATER line, D-game-22's inward growth and the adaptation design's decision 15; takes over the settlement plan's phase 7; saves break (version 9); the pass opens once every lesson listed in `world.json` (all of today's) is complete, or at once with a cheat setting, and until then a rockfall closes it and the valley's edge stays a wall; no lesson uses the new land; nothing new can be gathered (no digging, no ore items or nouns); lava blocks the body and does no harm | nothing |
| D-game-35 | Inventory and equipment (the I series, `docs/game/inventory-plan.md`) | decided (owner, 2026-11-15; building started the same day with I00; I01 waits for the owner's word): items move to data first (W12 taken over); a bag of the hotbar and 20 slots, a backpack slot whose bag (`el saco` 10, `la mochila` 20) adds slots; clothing, armour and cosmetic layers on head, body, legs and feet, three accessories and an off-hand; armour gives a rating for combat to come and reduces nothing yet; clothing's warmth against a temperature from the day, the weather and shelter, cold slows and never harms; every gear item craftable and buyable; gear drawn on the avatar; durability for gear and tools (W13 taken over); sort and trash; gear nouns above A2 join the world words; saves break; amends D-game-23 (c) | I00 to I10 |

## D-game-1: the game engine (prototype decided; final engine open)

**Question.** Which engine renders the game and plays its audio. The core's research report (core `docs/research`) suggested Godot .NET;
the core's high-level design also listed MonoGame, Bevy and macroquad, and Ebitengine. The core's language is C# (core ADR 0001), which
Godot .NET and MonoGame share.

**Owner, 2026-10-21.** The Terraria-inspired prototype uses MonoGame (3.8 DesktopGL), with a plan to perhaps move to Godot (.NET) later. The
layout from research R1 (`docs/archive/game/research/prototype-plan.md`), adopted by the owner the same day: `src/Vocaterra.Simulation` (no packages, no
engine), `src/Vocaterra.Game` (the `Vocaterra.Nlp.*` packages, no engine) and `src/Vocaterra.MonoGame` (the only engine code). Engine code never
goes into `src/Vocaterra.Host`, `src/Vocaterra.Simulation` or `src/Vocaterra.Game`. The final engine stays open; P10 (an optional Godot spike)
measures the cost of the move. G5 is replaced by P2.

## D-game-2: the licence of this repository (decided: MIT; superseded by D-game-20)

**Superseded (owner, 2026-10-23):** the game is Apache-2.0 like the core; see D-game-20. The text below is unchanged.

**Owner, 2026-10-21.** "Just make it the most permissive for now, it's a private project so it doesn't matter much at the moment." The
orchestrator chose **MIT** (the common permissive licence; 0BSD or MIT-0, which drop the notice requirement, are a one-file switch if the
owner wants them). The repository is private on GitHub. The `LICENSE` file is added by the next infrastructure milestone (G7).

**What MIT does not change.** Third-party parts keep their own terms: the content bundle's Wiktionary lexicon is CC BY-SA 4.0 and keeps its
notice in `content/game-v1/ATTRIBUTION.md`; the core's packages are Apache-2.0 (core D3); MonoGame is Ms-PL; the sherpa-onnx native
libraries carry GPL-3.0 code (core D2), so a distributed build that includes them waits on core D2. D-game-5 (CC0 assets only) stays in
force until the owner changes it.

## D-game-3: continuous integration (active since 2026-10-31)

**Now:** the token has the `workflow` scope and `.github/workflows/ci.yml` runs (see the table above); the paragraph below is the 2026-10-21 record.

The owner deferred CI on 2026-10-21, with the core's D1. Do not raise it. The parked workflow stays in `.github/workflows-pending/ci.yml`;
nothing under `.github/workflows/` is written, because the push token lacks the `workflow` scope.

## D-game-4: does `content/game-v1` track the core's sample or diverge (decided: diverge; superseded)

**Superseded (owner, 2026-10-23; done in R02):** one bundle. The core's tests, fuzz suite and demo run on `content/game-v1`, so a game content change that breaks the core fails the core's tests (the owner wants that signal); see core D7. The text below is unchanged.

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

The owner's Prototype Design Brief v1.2 (`docs/game/design/sources/Vocaterra_Prototype_Design_Brief.docx`, as Markdown in `docs/game/design/prototype-brief.md`) defines the
prototype's scope, scenario, guardrails and gates; `docs/game/scenario-plan.md` is its plan (the S series). World: 3D voxel, first person, MonoGame
(D-game-1 unchanged; engine code only in `src/Vocaterra.MonoGame`). The 2D Terraria slice is parked: its code is tagged `2d-slice-p7` and removed
once the 3D world reaches gathering and crafting (S4). Spanish variety: Latin American neutral (core ADR 0005). Nothing in the game is gated
behind language or voice; every failure has a recovery path. Work on the S series lands on the integration branch `feature/prototype`
(owner, 2026-10-21), not on `main`.

## D-game-9: speech rules (decided, from the brief)

ASR is used for intent in a constrained phrase set. An uncertain recognition (not communicated, or a per-word confidence mean under the
threshold) asks for a retry with help and is logged as a voice event, never as a language failure. A one-time mic check; visible recording and
thinking states; cancel and a timeout; a persistent "oí / esperaba" line; replay and slower audio; typed and button fallback; "no sé" always
available and never shamed.

**V31 (2026-11-07):** free talk at the seven constrained prompts (`baker-open`, `smith-open`, `farmer-open`, `fisher-open`, `stall-need`,
`stall-offer`, `guide-offer`) decodes a missed spoken line a second time, freely (the core's M80, with the 0.5 weakest-word floor). A line
heard so is answered as the same typed line is, credited as typed (half), and marked by the `heard_open` voice event and the session log.
Lesson steps, reviews and the mic check decode once. The playtest counts `heard_open`. (D-game-22, the owner's answers of 2026-11-06 and
2026-11-07.)

## D-game-10: learning measurement (decided, from the brief)

A hand-authored phrase sheet (`content/PHRASES.md`) is the source of truth. Each phrase is tracked through shown-card, supported, unaided,
choice-only (a recognition signal) and "no sé" uses. No automated CEFR placement or global score. A pre-check, a post-check and a later recall
check. Telemetry is local only (JSONL in the session folder).

## D-game-11: the Noun Town review data (decided)

The raw Steam review JSON (about 450 KB of third-party text) is never committed: `.gitignore` covers `scripts/noun_town/*.json` and
`docs/research/2026-10-21-noun-town/*.json`. The two summaries and the fetch scripts are committed and live in `docs/research/2026-10-21-noun-town/` (moved in S0).

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
Plan: `docs/game/scenario-plan.md` section 8, S16.

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
the player's machine only. It is game state (game words live in `content/game-v1`); the core already returns the word readings, so no core change.
The orchestrator's choices, open to the owner's change: verbs listed by infinitive with the forms used; only words the core read from its
lexicon are listed (recogniser noise is not); the save keeps the newest 500 lines. Plan: section 8, S19 (the record) and S20 (the book).

**Owner, 2026-10-22 (follow-up).** The book item counting as the taught noun `libro` (S20) is fine. Check answers (the pre, post and recall
checks) must **not** count in the word record: they stay in the input history, but the fold skips them (S19b).

## D-game-17: in-game bug and feature reports (decided)

**Owner, 2026-10-22.** "We need a bug / feature request feature in the game. So pressing F8 will upload a bug report and save as a markdown."
Asked where to upload, the owner chose a **local file only**: the report is a Markdown file with a PNG in the session's folder, and the
playtest folder collects them (no network, so core D8 is untouched, and no token in the game). The owner chose every attachment: the
screenshot, the game state (build, bundle, seed, position, objective, lesson step, settings, speech status), the last 10 turns the player
said or typed (from S19's history; they include the player's own transcripts, so S18's consent text names them) and the last 200 lines of
the session log. Plan: section 8, S21.

## D-game-18: sound (decided)

**Owner, 2026-10-22.** "We also need to plan in sounds. So ambient sounds, also sounds when things are done, like when progressing through a
lesson. Also sounds for interacting." Asked about sources and music, the owner chose CC0 packs (Kenney, OpenGameArt CC0) plus sounds
synthesized in code where no good CC0 file exists, and no music.
- Every file keeps D-game-5's CC0 rule, with its source logged.
- The orchestrator added two rules to protect the learning:
  - ambient and effects duck under the NPCs' Spanish voices (D-game-13: the ear stays on Spanish);
  - they are silent while the microphone records, so no game sound reaches speech recognition.
- Sounds never touch the evaluation path or the replay.
Plan: section 8, S24 (the system, interaction and progress) and S25 (ambient and footsteps).

## D-game-19: verbs in the lessons (decided)

**Owner, 2026-10-22.** "We also need to include verbs more in the lessons, verbs are important, with spanish we have various forms of the
verb for we, i, you, etc. Also past, present future. so its important to introduce them early, but of course not overwhelm the user." Asked
how, the owner chose:
- **Across sessions:**
  - session 1: a yo/tú unit in the guide's lesson;
  - session 2: the other persons;
  - session 3: ir a + infinitive and a few past forms.
- **Replay:** each lesson can be replayed from the book.
- **Tables:** conjugation tables in the book (S28) show used forms highlighted, taught forms plain, and untaught tenses hidden.
- **Scope:** vosotros is not taught (Latin American neutral). The simple future waits for A2.
Plan: section 8, S26, S27 and S28; core M49 for the future-tense checks.

## D-game-7: one repository for the game and the core (done)

**Owner, 2026-10-21.** "My plan is to actually merge the game and the NLP back together, for simplicity and as they both update they can
update together." The orchestrator stopped after P7 at the owner's request; P8 and later waited for the merged repository. The parts of the
game that existed only because the two were separate (packages only, the submodule and its feed, `docs/game/CORE_REQUESTS.md`, two
orchestrators, two gates) are listed in `implementation-plan.md`, section 4, the handover.

**Done, 2026-10-21 (the owner, with Claude).** Layout: `nlp/` (the core, with its own plan, decisions, docs and Apache-2.0 licence) and
`game/` (this half, MIT) as sibling folders of `james-d12/Vocaterra`; the shared solution, build settings, package versions, `nuget.config`,
`CLAUDE.md`, orchestrator and gate at the root. The core's history was rewritten under `nlp/` and merged, so `git log` follows its files.
Consumption: project references (no feed, no pack step). Ids on both sides are unchanged. Licences stay per folder; a single licence for the
repository is open only if the owner wants one. The old `james-d12/Vocaterra.NLP` repository is to be archived by the owner.

## D-game-20: one licence for the whole repository (decided: Apache-2.0)

**Owner, 2026-10-23,** while planning R01 (the flatten of `nlp/` and `game/` into one root layout): with both halves sharing `src/`,
`tests/` and `tools/`, a per-folder licence no longer fits. Asked "everything Apache-2.0, everything MIT, or keep both per project", the
owner chose **everything Apache-2.0**. The owner is the sole author, so the game's code, content and docs are relicensed from MIT.
Done in R01: the core's `LICENSE` and `NOTICE` moved to the root (NOTICE now names the whole project), the game's MIT `LICENSE` was
removed, and the READMEs and `content/game-v1/ATTRIBUTION.md` say Apache-2.0. Third-party terms are unchanged: the Wiktionary lexicon
stays CC BY-SA 4.0, assets stay CC0 only (D-game-5), and the models keep their own licences.

## D-game-21: content as data and the L1 locale layer (decided)

**Owner, 2026-10-27 to 2026-10-29.** The owner asked how `content/` could be made easier to manage, how the learner's native language could
leave the code ("what if someone speaking French wanted to learn Spanish?"), and how objectives and NPCs could be built from data, as the
ground for custom authoring tools. The answers, in `docs/game/content-plan.md`:
- English moves into a locale layer; no second locale is added yet (2026-10-27).
- Content stays JSON, with JSON Schema added (2026-10-27).
- The locale is a bundle of its own, `content/locale/`, beside `content/game-v1` and outside its hash, so an L1 edit touches no save, fixture
  or golden; game-v1's manifest names the default locale in `supportLanguage` (2026-10-28).
- R16 phase 2 runs first, and the series starts on its layout (2026-10-28).
- Proceed (2026-10-29): one session (Opus 5.5) does every milestone itself, with no subagents, on `feature/content`, made from `main` after
  R16 phase 2 landed there.
- Close (2026-10-29: "work for content is done"): C01 to C08 are done (pull request #16). C09 is assessed and C10, the authoring CLI the
  request looked towards, is specified in `content-plan.md` section 3.8. Neither is built until the owner asks.

## D-game-22: the village and the lessons (decided for phase A, 2026-10-30)

**Owner, 2026-10-30.** "Take the current map, make the village bigger with more NPCs (blacksmith, baker, etc.) and have more lessons. Avoid
making the map larger unless an objective or lesson genuinely justifies it. Make the NPCs interlink, and use the map space intelligently.
Lessons should be connected, and aim for A1 to A2 content." And, the same day: "The lesson should also include past, present, and future
use of verbs." The plan is `docs/game/village-plan.md` (the V series); its section 6 lists the nine answers it needs, none assumed:
- (a) phase A (sessions 4 to 6, A1 complete) now, or after the first pilot, as the brief's decision gate would have it;
- (b) the content gate: an A2 ceiling bundle-wide (`lint --strict --level a2 --fail-on-level`) with a per-lesson A1 check in the game's
  validator, and world words in `cefr/a1.txt` under a marked block (the gate today demands A1 bundle-wide);
- (c) the NPC set and their names (the fisher Mar, the baker Luz, the smith Domingo, a farmer or none; a name for the villager or none);
- (d) the voices: the Kokoro model has three Spanish voices and all three are taken, so the candidates are the Italian `if_sara` and
  `im_nicola` and speed variants of the Spanish three; the owner listens to a trial before any lesson is recorded (D-game-12 step 4);
- (e) the shopkeeper's pronoun: the label and the voice are male, the locale and the sheet say "she";
- (f) the economy (a coin, an apple, an axe; four orders; `sell`) as the price of giving numbers a use, or numbers as comprehension only;
- (g) phase B (A2, sessions 7 to 10), designed in the plan, started only on the owner's word;
- (h) the speech folder's sample rate once phase A adds about 190 files;
- (i) the tenses: the preterite as the one past (yo and tú from session 3, the regular `-ar` pattern in session 5, every person in
  session 8), two futures (`ir a` in every lesson from session 3; the simple future in session 9, at A2, with a small code milestone for the
  runner and the book), the imperfect and the perfect left to a later series; seven lessons rather than six.

**Owner's answers, 2026-10-30 (the same day, when asking for the plan to be built on a new worktree).**
- (a) **Phase A now**, before the pilot: V01 to V07, V10 to V12 and V09 (sessions 4 to 6, A1 complete). Phase B is not started.
- (b) **The plan's gate**: an A2 ceiling bundle-wide with a per-lesson A1 check, world words in `cefr/a1.txt` under a marked block. The gate's
  change itself is V08 (phase B); phase A's lessons are all `a1`, so today's A1 gate holds for them.
- (c) **The fisher Mar, the baker Luz, the smith Domingo, the farmer León** (a masculine noun, as section 3.3 asks); the villager stays unnamed.
- (d) **Provisional voices, recorded now**: the plan's first candidates (`if_sara` the baker, `im_nicola` the smith, `em_santa` at 0.9 the
  fisher, `pm_santa` the farmer); V06 renders the trial for the owner to hear afterwards, and a rejected voice is re-rendered.
- (e) **The shopkeeper is "he"** in the locale and the sheet (the label and the voice are male).
- (f) **The economy**: coins, apples, the axe and the orders (V03).
- (g) **Phase B held** until the owner has heard phase A. **Started on 2026-10-31** on the owner's word, after a playtest of phase A and
  before hearing the voices: "continue to phase B, I want all the work on the feature/village branch, I'm happy to review all at the end".
- (h) **The speech folder stays at 24 kHz.**
- (i) **The tenses as proposed**: one past (the preterite), two futures (`ir a`, and the simple future in session 9 at A2).
- **Models:** every V milestone is implemented by Opus 5.5 at xhigh effort (the owner's instruction for this series; it replaces the plan's
  Haiku and Sonnet column), with cheaper agents only where necessary.

**Owner's answers, 2026-10-31 (during phase B, on the study of the project's bottlenecks, `docs/research/2026-10-31-development-bottlenecks.md`).** The owner
asked for the study ("figure out during development what has caused the biggest bottlenecks and issues, and come up with a plan"), by Opus,
and answered its first three proposals "yes to 1, 2 and 3":
1. **The core's M59 before V14** (the person and tense checks the third person needs: session 8 retells the village's day with named
   subjects). M59 depends on M56, so **M56 runs first**, which also meets the next answer.
2. **V18, the content loop in one command,** between V13 and V14.
3. **The core's M58 and M56 before V15** (the market's price slips).

Not answered, so the rules stand: two agents at once (the heavy commands behind the one lock); effort below xhigh or a Haiku helper for the
mechanical steps; a content milestone split across two agents; CI (the token's read access to checks, CI on feature branches, the content
gate in CI); a speech sample before V15. Phase B's order is in `village-plan.md` section 4.

**Owner's answer, 2026-10-31 (after V13):** "Run m62 before v15". **The core's M62** (V13's adjective gaps: a predicate adjective agreed
across `ser` and `estar`, and a colour adjective before its noun) runs after M58 and before V15, whose `es muy caro` and `más barato que`
are predicate adjectives a learner will get wrong.

**Owner's answers, 2026-11-01 (the follow-ups after PR #20's merge, `docs/handovers/done/2026-11-01-village-phase-b.md` section 9.1),** on
`feature/village-followups`:
1. **Models, a mix** (it replaces "every V milestone by Opus" for the follow-ups): Opus 5.5 at xhigh for the core's M65, M66, M67 and M69
   and for V20, where Spanish is judged and the design is subtle; Sonnet 5.5 for V21 to V23 and the core's M68; Haiku 5.5 for V24, the
   documents. The QA pass uses the QA agent's own model.
2. **The fuzz narrowings of M65:** yes to (a) `egVgLELrxyL4`, (b) `haber` after a noun subject (`b3HSKSLCsL_1`, `4YuZ2yubxdC1`) and (c)
   `2ldq2rIKLrG2`; (d) `2tpLGUOSqj41` stays recorded, not narrowed.
3. **Name plates (V22):** only the plate of the NPC the player faces within talking range (and the beacon's target), drawn depth-tested
   against the scene and the figures.
4. **CI:** `main`'s CI was green after the merge, so M68 (the timed tests' budgets on CI) does not run unless a timed test fails there.

**Owner's answers, 2026-11-01 (on the QA passes of the follow-ups, `docs/qa-reports/2026-11-01-language.md` and `-progression.md`):**
1. **A load and the session: two counters.** The day (the village session: lessons, objectives, orders; advanced only by sleeping, V12c)
   is restored exactly as saved. A separate sitting counter still goes up on each launch or load, for the phrase-credit spacing rule ("a
   different play session" above, which keeps its meaning) and the session logs. It replaces S10's "a load is the next session" (V25).
2. **Typed `n` for `ñ`** is a typing slip, accepted as a dropped accent is (M26b), when the spelling without `ñ` is not itself a word
   (`ano`/`año`, `pena`/`peña`, `cana`/`caña` stay distinct and are graded as today) (M70).
3. **An accent that marks a verb's tense or person** (`compre` for `compré`, `compro` for `compró`) is still accepted on typed input with
   no deduction, but the feedback shows the accented form with a short note (M70, and the note's display in the game, V21).

**Owner's answers, 2026-11-02 (after V24, the last of the follow-ups' queue):**
1. **V24's four game bugs are fixed first, on `feature/village-followups`,** as one game milestone (V26, Sonnet 5.5), before the pull
   request into `main`: two replies to a line not understood, the book's credit of a refused request (`Quiero dos manzanas.` at Luz logged
   as a use of `quiero-dos-panes`), the book keeping a typed form without its accent (`compre`), and a scripted run's silent stop at 18 000
   ticks.
2. **A correct line with a word outside an open prompt's answer set** (`la tienda está en el pueblo` at `farmer-open`: `vocab.wrong`, «No
   te entendí»; V17, M69) **is planned now** as a core milestone and a game milestone; the plan goes to the owner before anything is built.

**Owner's answers, 2026-11-02 (the off-prompt lines, after a design study of the cause):** at an NPC's constrained open prompt a correct line
whose intent the prompt does not take was graded `vocab.wrong` (0.4, an empty expected word; M67's derived tag) and answered «No te entendí».
1. **Option 2:** the core derives `vocab.wrong` only where it names the word or explains a near miss, and signals a line that is understood
   but not taken at this prompt (`Verdict.OffPromptIntent`, a second resolver pass over the whole catalog, grammar and keyword rungs only):
   the core's M72 (Opus 5.5 at xhigh). The game answers it (V27, Sonnet 5.5, after M72).
2. **The book credits it** as free talk does since V20 (b): a use of its nouns and of any phrase of a lesson begun or done; no NPC action.
3. **The NPC says «Te entiendo.» and points to its own topic,** one reply per turn, A1, a normal and a slow WAV in each NPC's voice:
   Domingo «Te entiendo. Yo hago hachas.», Luz «Te entiendo. ¿Quieres pan?», León «Te entiendo. ¿Quieres manzanas?», Mar «Te entiendo.
   ¿Buscas un lugar?», the villager «Te entiendo. ¿Qué buscas?», the guide and the shopkeeper one line each in the same form.
4. **`hace frío` at `smith-open`** (read as `ask_activity`, a keyword rule on `hacer` alone) is fixed in M72.

**Owner's answers, 2026-11-06 (the spoken off-prompt lines, after the study `docs/research/2026-11-04-spoken-off-prompt-lines.md`,
its section 7):** "I accept all recommendations. However, for decision 5, I'm not going to be recording any of those, so skip that for
now. Make a note of it, but don't block it from continuing the work."
1. **Option 1:** a second, open decode only when the constrained decode is not taken, and only in free talk (the host's switch); lesson
   steps, reviews and the mic check stay constrained. The core's **M80**, then the game's **V31** (their models: the next answers).
2. **A spoken line the prompt allows but not among its answer rows** (`mañana voy a comprar pan` at `smith-open`) is understood and
   answered as the same typed line is: spoken and typed alike.
3. **Credit for a line heard on the open decode:** half, as a typed line (the recognizer may have smoothed a mistake away).
4. **All seven constrained prompts free talk uses:** the four NPC open prompts, the shopkeeper's `stall-need` and `stall-offer`, the
   guide's `guide-offer`.
5. **No owner recordings** for now. M80 measures on the synthetic Kokoro voices only and reports the accent risk as unmeasured on real
   speech; nothing waits on recordings. Noted for a later measurement if the owner records or testers' recordings exist (M24).
6. **Zero false takes** («Te entiendo» to a line that meant nothing) in the test corpus is the acceptance bar.

**Owner's answers, 2026-11-06 (the models of M80 and V31):** "The two quick questions, go with the recommendations is fine." **M80 by Sonnet
5.5 at high effort** (not Opus at xhigh as the study proposed); **V31 by Sonnet 5.5 at medium effort**, and the V series follows the owner's
size rule from now on (Haiku for small, Sonnet medium for medium, Sonnet high for large or design-heavy), replacing this decision's "every V
milestone by Opus 5.5 at xhigh" (2026-10-30) for milestones not yet launched.

**Owner's answer, 2026-11-07 (M80's two false takes):** "add the 0.5 weakest-word floor in M80". M80's measurement found two false
takes against answer 6's zero bar («ese libro es muy interesante», voice `ef_dora` at 1.0, heard «el libro…» with `el` at 0.45 and read as
`describe`, as the typed line is). The free pass's reading is taken only when its weakest word's confidence is at least **0.5**; the floor is
the core's (M80), not the game's. Measured on the Kokoro corpus, it stops both false takes and 2 of the 6 wrong takes and loses 1 of 108
understood lines. The zero bar stands.

**Owner's answers, 2026-11-02 (the open points after V27):**
1. **The small points are fixed on `feature/village-followups`,** before the pull request: the core's in M73 (Opus 5.5 at xhigh), the
   game's in V28 (Sonnet 5.5).
2. **The speech folder goes into subfolders by speaker** (`speech/<speaker>/`, the cards in `speech/cards/`), in V28: it held more than
   1,000 files in one folder.

**Owner's answers, 2026-11-02 (the open decisions after M73; built after the pull request merges, `docs/handovers/done/2026-11-02-after-followups.md`):**
1. **Weather lines get their own intents** (a statement and a question; `hace frío` is unknown today, `¿qué tiempo hace?` is
   `ask_activity`, `hace calor` is `state_action`): the core's M74. Each NPC answers a statement by echoing its weather word («Sí, hace
   frío.») and the question with «Hoy hace sol.» (V29).
2. **`¿cómo estás?` gets its own intent,** split from `greet` in M74, and each NPC answers «Bien, gracias.» in its own voice (V29), without
   «¿Y tú?».
3. **Spoken lines that a constrained prompt does not take are fixed** (the recognizer decodes only the answer set, so V27's «Te entiendo.»
   is never heard for a spoken line): a design study of the options first, whose recommendation comes back to the owner, then a core and
   a game milestone.
4. **M67's near-miss rule: a near miss must name its word** (the orchestrator's recommendation, "go with your recommendations"): the
   line and the nearest answer have the same number of words and differ in one, the two words are a slip pair (close in spelling, or one
   closed class), and the expected word is always named. The core's M75 (Opus 5.5 at xhigh); O3's verdict leg becomes strict again.
5. **Listening to the voices and deleting the merged remote branches** are the owner's, later.

**Owner's answers, 2026-11-02 (on `scripts/try-lines/`, after PR #28 was opened):** "I would prefer things like this to be a csharp
unit test, not some throwaway script or junk text file. This just adds to the general tech debt."
1. **A regression check is a C# test** run by `dotnet test` (so the gate and CI run it), never a script with text baselines.
2. **The core's M76 turns `scripts/try-lines/` into tests now, in PR #28,** before the merge, and deletes the script and its files.
3. **M76 also audits the other check and one-off scripts** of `scripts/`: each becomes a test, stays as tooling, or is deleted; anything
   large comes back to the owner.

**Owner's answers, 2026-11-02 (after M76's audit, and a review of PR #28):**
1. **"Do the IDE0005 warning too":** the core's M77 makes an unused using a build warning (an error under `-warnaserror`) in every project,
   and deletes `scripts/audit-usings.py`. The scripted runs as game tests (M76's other proposal, 3 to 5 days) are not asked for.
2. **The review of PR #28** (14 findings), "check what makes sense to fix": the orchestrator checked them against the code. Eleven go into
   V30, in PR #28 before the merge (the review's items 1 to 7 and 9 to 13; three of them to verify with a test first). The review's item 8
   (an empty answer set counts as a near miss) goes to M75, which rewrites that rule after the merge. Item 14 (pool names written in code)
   goes to V29, before it adds new pools.

## D-game-23: activities and the adaptation (open; decided in part)

**Question.** The W series (`docs/game/activities-plan.md`) proposed answers (a) to (q) in its section 12 on 2026-11-01. The owner's
Adaptation Design Document (revision 2, 2026-11-03) set the direction above it: one bread procedure with Luz as the pilot, then repair,
meals and farming. The current document lists open decisions in section 18, including needs edge cases, accessible instruction evidence and ingredient grants during lessons (decisions 18–20).

**Owner's answers, 2026-11-03** (during the review of the adaptation design):
1. **The order (replaces (j)):** the queued follow-ups first (M74, M75, V29, M78 and the study of spoken off-prompt lines,
   `docs/handovers/done/2026-11-02-after-followups.md`), then the W series; player needs (D-game-24) were later put between them. V29
   declares the NPCs' reply pools in their files before W03, W04 and W08 change those files.
2. **The station view ((o)):** an overlay panel over the live world for the pilot; an in-world kitchen only after it.
3. **What bread is for (new, (r)):** the stall buys bread for one coin, a fixed `buy` at the shopkeeper.
4. **The tú imperative (replaces (h)):** heard only in the pilot. No imperative is taught, so W09 leaves the pilot's path and the book
   gets no imperative column.

**Amended:** (c), "no combat; gear is for places", became "combat later" by D-game-35 (2026-11-15): armour gives a rating for combat
to come and reduces nothing yet.

**Still proposed:** (a), (c) to (g), (i), (k), (l), (m), (n), (p) and (q) of activities-plan section 12 ((b), needs as buffs only, is
replaced by D-game-24), and the recommendations in the design document's section 18: the pilot protocol, a listening gate for Luz's
lines, the fold rule, act evidence as an amendment to D-game-10, the recipe's changes, session 11 at `a2` with flour sold at the stall, a
presentation track (U01 to U04), combat left out, the cost of death, Spanish for needs, the pyramid's middle layers, and five points from
the owner's survival-first proposal (conditions, a first bush at the camp, expansion inside the valley, a chosen starting session and a
relaxed needs option).

**Owner's answers, 2026-11-04** (before an overnight run; the owner away until the morning): "implement this design" on a new branch,
`feature/adaptive_design`, with Sonnet as the workhorse, Haiku for light work and Opus sparingly.
1. **The order:** the queued follow-ups first, on their own branch (`feature/village-decisions`: M74, M75, V29, M78, the spoken-lines
   study), then a review notice to the owner, then the adaptation's phases B to E (N01 to N07 with L01, U01 and U02, then W01 and W05 to
   W10) on `feature/adaptive_design`, branched from the follow-ups. W11 (the pilot) waits for testers and the owner's listening.
2. **Section 18.2's recommendations are adopted as written, provisionally** ("adopt recommendations"): decisions 1 to 20 of the design
   document as its section 18.2 states them, including the rest of activities-plan section 12 (decision 8). **The owner confirms or
   changes each at the review;** a milestone built on one names it in its findings entry. Where a recommendation leaves a value open
   (decision 18's edge cases: the gathering stamina cost, the floors of a night and a collapse, eating at full hunger for healing,
   repeated deaths, an occupied chest site, returning before the chest expires; decision 19's muted play; decision 20's replenishment
   states), the milestone takes the smallest choice that keeps the no-dead-end rule and saves every persistent state, and lists it in
   its findings entry for the owner.

**Owner's answers, 2026-11-06** (after phases B to D merged): "I do the playtest, while a new agent works on a new branch (based off
feature/adaptive_design), with the bread stuff".
1. **The needs check no longer gates W05 to W07.** They are built on `feature/bread-pilot`, branched from `feature/adaptive_design`
   at the commit that records this answer, by a fresh orchestrator session, while the owner plays days 1 to 3 on
   `feature/adaptive_design`. W08 still waits for the spoken-lines study's six questions, and W10 for W08; W02 to W04 are not part of
   this answer.
2. **The branches** (orchestrator's default; the owner may change it): milestone branches are gated into `feature/bread-pilot`, which is
   pushed. Fixes from the needs check land on `feature/adaptive_design`, and `feature/bread-pilot` merges them in when it needs them.
   When the pilot's milestones are done, `feature/bread-pilot` is gated into `feature/adaptive_design`, so `main` still takes one merge.
3. **A recipe book for cooking** (owner, later the same day): "for the cooking, ideally i would like a book interface. For example in KCD 1
   and 2 when you create potions you use the book as the receipe book essentially and its an interactive thing. Perhaps add this as
   future milestone". Planned as **W30**, proposed and not authorised (`docs/game/activities-plan.md` section 5 and "Proposed: the recipe
   book"); W07 keeps its instruction strip a separate model so the book can host it later. W05 to W07 are otherwise unchanged.
   The owner's shape, the same day: "it should be a book the player has on them, but can you placed down for longer cooking sessions. So
   like we have a journal, the player can also get a recipe book. Perhaps as a reward for making the first item with luz". A second
   carried book beside the journal, placeable open beside a station, given by Luz for the first bread (so W30 depends on W10).
   Its two open points, the same day ("both defaults are fine"): a page appears only once its recipe is taught, and it plays the `does`
   rows' normal and slow recordings.
4. **More work while the owner's answers are pending** (owner, 2026-11-06, after W05 to W07 merged; asked what was outstanding): "Yeah,
   start those ones now. Just start as much as possible, unless it obviously needs my review." Authorised on `feature/bread-pilot`, in
   this order, one building agent at a time (the disk holds one milestone worktree above the 10 GB floor): **W07b** (W07's three small
   points), **T8** (the nondeterministic `r30-no-classes` capture), **W02** (tools and materials), **W03** (fail-forward as data),
   **W04** (price lists; its code and tests only, the bakery's real price list and Spanish lines stay W10's, so nothing needs the owner's
   listening). Still waiting for the owner: W08 (the spoken-lines questions; answered and built since, item 5 onward), decision 19, W09, W30.
5. **Four fixes before PR #40 merges** (owner, 2026-11-07, after the queue was done and the orchestrator listed what it found): "do all
   four on this branch". On `feature/bread-pilot`, models by the size rule: the core's **M83** (the O7 fuzz seed: «ayuda por favor por
   favor» read as `request_item`) and **M84** (a free-talk line the constrained decode forces onto a wrong row with confidence gets the
   open decode too, measured on M80's corpus with the zero-false-take bar; a game follow-up **V32** only if the game's turn needs it);
   **W04b** (the stall's first price list: water, bread and flour by name, «No vendo eso.» for the rest, replacing W10's fixed flour sale);
   **T9** (`r27-bread`'s first capture alternating between ticks 2460 and 2461).
6. **After the water merge** (owner, 2026-11-08, PR #45 merged into `main` and merged into `feature/bread-pilot` at `bc881c58`): the
   Game.Core tests' `OutOfMemoryException` under water's 3 GB managed-heap cap is fixed **at the source** (**T10**; the cap stays);
   **W04b keeps water and bread free** at the stall (the price list covers what is sold: flour and any new goods; «No vendo eso.» for
   anything unlisted; sessions 1 to 10 unchanged), which narrows item 5's "water, bread and flour"; **H11** (the water plan's
   reconciliation: drowning through the needs, `player_drowned`, the blur setting, plus the merge's `--walk-to` bank stop and the save
   version 6 left in the docs) is planned on this branch after T10 and W04b; Game.Core's xUnit threads **stay at 3**.

## D-game-24: player needs (decided)

**Owner, 2026-11-03,** on reading the adaptation design: "does this design doc talk about hunger, thirst, health and stamina? those basic
needs need to be implemented first, otherwise cooking and crafting aren't of much use". The document had them only as missing (needs
as buffs after the pilot, D-game-23 (b), proposed). Asked how they should work, the owner chose:
1. **At zero: real death with a cost.** Health falls while hunger or thirst is empty; at zero health the player dies and wakes at the
   last fire they slept at. The cost recommended in the design (the bag, except the items that stack to 1, left in a chest until the
   next sleep) waits for the owner's word (its section 18, decision 10).
2. **The meters pause** in conversations, lessons, the book and every panel, so no one runs down while working out their Spanish.
3. **From the start:** the meters run from session 1.
4. **Stamina** is spent by gathering and by a new sprint.

**What it changes:**
- **It amends D-game-8.** The brief's prototype had "no damage or countdown" and left "hunger/thirst timers that can kill or
  permanently punish" for later. It also replaces D-game-23 (b), needs as buffs only.
- **What still holds from the brief:** a language mistake never kills. Every need has a route without Spanish (berries by hand; water by
  hand at the river, the lake or the well). Death never loses the learner record, the lessons or the book (D-game-16).
- **The order:** the queued follow-ups, then the N series (proposed ids; N01 to N07 and a needs check since the addenda below), then
  the W series. Save version 6 and its `systems` map come with N01, and W15 shrinks to cooked dishes and buffs.
- **The specification** is the adaptation design's section 9 (`docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`,
  revision 3).

**Owner, 2026-11-03 (later),** with a second review of the design by Copilot: "these fundamental needs should come first, they form the
basis of our pyramid of needs and drive the player to interact with cooking / other systems". The design now orders the work as a
pyramid:
1. needs and consumption (N01 to N05);
2. food that grows back, fair gifts and rest, checked in a short playtest (N06 and the needs check);
3. useful cooking at the fire (N07);
4. procedural cooking with fading guidance (the bread pilot);
5. farming and repair.

N06 and N07 are proposed (the design's section 18, decision 12).

**Owner, 2026-11-03 (later still):** "I'm thinking around player needs that we include hunger, thirst, sleep, health and stamina.
Hunger, [thirst] and sleep should affect and link to health and stamina. Hunger and thirst are required needs and have increasingly
negative effects the more hungry or thirsty you are. I like the idea from project zomboid that has needs. And needs have stages
(slightly thirsty, thirsty, dehydrated, etc) [...] this is the fundamental pillar and needs to be done before the rest of the systems
are added." ("[thirst]" is this record's reading of the owner's "hunger health and sleep".) The design's section 9 now has:
- five needs: three drives (hunger, thirst and sleep), each with four stages whose effects grow worse, and two conditions (health and
  stamina) that the drives push;
- hunger and thirst required: their last stage costs health. Sleep never kills, but its last stage ends in a collapse;
- sleep as it is today (a night, once the day's work is done, moves the day and the sitting), plus a nap at the player's fire for a
  tired player, so Spanish never keeps a tired player from sleep and the needs add no sittings;
  (superseded by D-game-25: sleep whenever tired, every sleep a night, no nap)
- needs before every other system: the N series is N01 to N07 and the needs check (proposed ids).

The owner also gave a Survival-First Game Design Proposal, incorporated into sections 1, 9, 12, 13 and 16 of the design.
Five points wait for the owner: the
design's section 18, decisions 13 to 17 (conditions, a first bush at the camp, expansion, a chosen starting session and a relaxed
needs option); D-game-25 answered the chosen starting session).

**Owner's answers, 2026-11-05** (on N05's findings):
1. **The drive rates are a setting for now:** "make this a configurable slider in the settings for now so it can be easily adjusted for
   playtesting. Perhaps in the final test we just put up a fixed value." N05 found that day 1 barely touches the needs at N01's rates
   (thirst 91 at the lowest at a novice's pace). Built by N09; the fixed value is chosen after the needs check.
2. **«Me muero de hambre / sed / sueño» stays**, though `morir` is above A2 in the lists: "it applies to the game about death, so we kind
   of need that in the game". The HUD's stage lines are text the player reads, not lines the lint holds to the ceiling.

**Owner's answer, 2026-11-07 (the needs check):** "I've checked the needs, and they are too easy i think. however in a future piece of
work im going to add a settings page that allows all of this to be configurable." The owner played days 1 to 3 with needs on: the needs
are too easy at the current rates. No fixed value is chosen now and no tuning milestone is planned; a later piece of work, the owner's,
adds a settings page that makes the needs configurable (beyond N09's `--needs-rate` slider). The needs check no longer blocks anything.

## D-game-25: lessons optional (decided)

**Owner, 2026-11-04,** on reviewing the adaptation design: "One important thing we need to move the game away from, is centering it
around the lessons. The lessons so far are ok, but they shouldnt be the main goal. They should be optional things the player takes on."
The code made lessons the spine: from day 2 the fire offered no sleep until the day's lesson was done (V12c), a waiting lesson owned its
NPC's conversation, orders opened on the lesson calendar (days 4 to 7) and the beacon pointed at the next teacher. Asked how the pieces
should work, the owner chose the recommended answer each time:
1. **The first lesson: a short required introduction.** Luna's first meeting teaches the getting-unstuck phrases (hola, sí and no,
   ¿qué es esto?, no sé, otra vez, más despacio) and the camp task; the rest of `first-words` becomes her first class. Every other lesson
   is a class an NPC offers and the player chooses; declining costs nothing.
2. **Sleep whenever tired.** From day 2 the fire offers sleep whenever the sleep drive is below 75; every sleep is a night (the day and
   the sitting move). The nap goes; a collapse is always a night.
3. **Orders open once their NPC is met** (and any order or capability they need), not on a day.
4. **Luz shows the player how to bake** on request or by her offer; the first guided bake is the teaching. Session 11, "En la
   panadería", becomes an optional class.

**What it changes:** it amends V12c (the day moved only after the day's lesson work) and D-game-15 (the guide's 5 to 10 minute lesson
is now a short introduction plus a class). It answers the design's decision 16 (a chosen starting session is no longer needed). The
specification is the adaptation design's sections 5.3 and 5.4, with 9.5 (sleep) and section 10 (bread); L01 builds it, after N01 and
before N03. `docs/game/lessons.md` describes today's behaviour and changes with L01.

## D-game-26: water (decided, 2026-11-07)

**Owner, 2026-11-07,** on the water series (`docs/game/water-plan.md`, section 6; entered at H11, 2026-11-08, once the series and the
bread pilot met on `feature/bread-pilot`):
1. **Running out of air:** the player takes damage until they surface or die: 10 health a second out of air, through the bread pilot's
   death flow, with the `player_drowned` hint (water plan 4.4). No interim fallback; live since H11 (`NeedsHealth`). With needs off, no
   damage, as for hunger and thirst.
2. **Water that would move when woken (H07):** report each cell with screenshots and fix the layout or keep the cells as built, cell by
   cell; no special cases in the flow rules.
3. **Placing blocks into water:** yes (dams, water plan 4.5). The bucket stays after W12.
4. **The underwater blur:** configurable, `Off` / `Low` (default) / `Medium` / `High` (water plan 4.9); since H11 a settings row saved in
   `SaveSettings.UnderwaterBlur`, `--underwater-blur` overriding it for a run, `ViewEffectsOff` forcing it off.
5. **What water washes away:** water behaves basically like Minecraft, kept simple. It replaces only the five sown plants
   (`BlockDefinition.IsUndergrowth`: tall grass, fern, reeds, the two flowers); the market stall's goods, wheat and every other decor
   block stop it like a solid block (H06a).
6. **Water at rest, H07's clusters** (report `docs/research/2026-11-07-water-at-rest-h07.md`): fix the layout in all three. (a) The
   waterfall: one row of sources at the top over falling water down to the pool's surface layer. (b) The river's staircase below the
   pool: each one-block drop a short falling-water step. (c) The river mouth: the river's surface clamped to the lake's level inside the
   lake (H07a).

**The cliff at the lake, from H11 (owner, 2026-11-08: "Fix the lake bank"):** the sand bank at (212, 262) stood 2 blocks above a
1-block-deep shelf, too shallow to swim, so no player could climb out there (a wading jump rises about 1.3 blocks; the hop-out needs
swimming; `.claude/briefs/h11-report.md`, item 5). The bank is fixed in the world, not in the swim rules: every water edge a player can
reach is left by walking, a step or the hop-out. Built by H12 (2026-11-08, `docs/game/water-plan.md`): 582 such edges, every shore's rim, now a step
over the shallow bed; the swim rules unchanged.

## D-game-27: handing an order over in Spanish (decided, 2026-11-08)

Issue #42: "when handing in quest items (e.g. handing over 5 wood to the baker), we should have to say what we are giving", across every
current order. Until now an order was delivered the moment the talk opened with its goods in the bag (village-plan V03, rule 6: walking
up is enough). The owner's answers (2026-11-08):

1. **Say it, with fail-forward.** The NPC asks for the goods when the talk opens (`¡Hola! ¿Qué tienes para mí?`, `<npc>.order_ready` into the
   agent's `receiving` state) and the learner says what they hand over (`Aquí tienes cinco bloques de madera.`). Rule 8 (D-game-8,
   D-game-9) is kept: help models the line, «no sé» says the model and the goods go, two missed lines hand them over anyway (a
   `fail_forward` phrase use), and an unsure recognition is never a miss. Not a strict gate, and not the stall's second-visit fallback.
2. **The item must be named, the count is optional.** `Aquí tienes la madera.` passes at A1; the card shows the full line with the
   number. A wrong count gets the core's feedback and still passes. For an order of two goods (Domingo's wood and coins) either one counts.
3. **All four orders** (`fruit-for-mar`, `oven-wood`, `axe-handle`, `water-for-plants`) take it, through `handOver` in their files
   (`content/schema/objective.schema.json`); an order without it is still delivered by walking up.
4. **No class while the NPC waits for the goods** (the owner's follow-up, 2026-11-08): a class at the NPC is not offered, started or
   resumed at the opening of a talk that asks for the goods; it follows the hand-over in the same talk, so the NPC's question and a lesson
   step never share the panel.
5. **«mí» and «ti» are A1** (the owner, 2026-11-08): the hand-written `cefr/a1.txt` lacked the pronouns after a preposition, so the lint
   had refused `¿Qué tienes para mí?`; they joined the list and the NPC asks it. The same day the core began to grade `la agua` (`el`),
   which the owner asked for (`docs/nlp/implementation-plan.md` 5b).

How it is built: the intent `hand_over` (`aquí tienes`, `aquí está(n)`, `te doy`, `toma`, then the goods), the prompts `<npc>-receive`
and the answer set `order-receive`, a `receiving` state in each order NPC's agent, five phrase rows (`content/PHRASES.md`, "Handing over
an order"), and `GuideSession.HandOver.cs`, which checks the line names one of the order's nouns before it raises `<npc>.delivered`
(the thanks and the reward, as the walk-up did). The validator checks each `handOver` order (`GameDataValidator.CheckHandOver`).

## D-game-28: the book's hierarchy (decided, 2026-11-08)

*Numbered D-game-28 because `feature/german` and `feature/hand-in-speech` each took D-game-27 the same day; first recorded here as D-game-27, renumbered before the pull request.*

**Owner, 2026-11-08,** on the U07 mockup (GitHub issue #7; the artifact "Book Hierarchy Mockup", spec `.claude/briefs/u07-book-spec-draft.md`;
plan section 4.10): "Go with the recommendations." The three questions and the answers:
1. **Double strike** (the pixel font's only bolder weight: the text drawn twice, 1 px apart): **on, in a few places only**: known entries,
   the right page's section labels ("What you did", "Units", "Phrases", "Feedback") and the next lesson's line. Never running text.
2. **A lesson's phrases move to the right page** with their speaker buttons; the left list keeps one row per lesson. The right page gets
   the speaker buttons' hit tests; the keys still reach every recording from the Phrases page.
3. **The Phrases page is grouped by help** (Without help, With help, Not met yet), not by lesson: a phrase row carries no lesson, and the
   Lessons page's right page already lists each lesson's phrases.

## D-game-30: orders are offered and taken on (proposed, 2026-11-09)

*Numbered D-game-30 because `merge-main-into-german` renumbers the hand-over (D-game-27 here) as D-game-29.*

Issue #54: "These should act as optional quests we can accept. Currently there is no giving of these quests, they just magically appear after first
talking to the NPCs that ask. Like Mar for fruit, Luz for wood, etc. We should review all quests and ensure they are given." Since L01 (D-game-25) an
order showed, counted and could be delivered as soon as its NPC was met, with nothing said about it. The answer, built on `fix/quest-giving` for the
owner's review:

1. **The NPC offers its order** when the talk opens (`<npc>.offer_order` into the agent's `offering_order` state): Mar «Tengo hambre. ¿Me traes tres
   frutas? Te doy dos monedas.», Luz «¿Me traes cinco bloques de madera para el horno? Te pago cuatro monedas.», Domingo his existing «¿Quieres un hacha?
   Necesito dos bloques de madera y dos monedas.», León «Mis plantas necesitan agua. ¿Me traes agua del río? Te doy tres manzanas.». An English note says it
   is the player's choice.
2. **«Sí» gives it**, and the NPC thanks the player and says where the goods are (Mar «¡Gracias! Hay fruta en el campamento de Luna.», Luz «¡Gracias! Hay
   árboles cerca del pueblo.», Domingo «¡Muy bien! Te hago un hacha.», León «¡Gracias! El río está cerca.»). Only a given order is shown, counted, asked for
   and delivered; the save keeps the list (`givenOrders`).
3. **Optional, at no cost:** «no» («Está bien.») puts the order off until the next day, as a class's «no» does (L01); any other line is answered as at the
   NPC's open prompt and leaves the offer for the next talk, as walking away does. Goods carried before the order is given stay in the bag.
4. **The classes that tell an order give it at their step** (`lessonStep` in the order's file: `numeros-y-pan` at `n3-oven`, `gente-del-pueblo` at `g4-offer`,
   `como-es` at `c3-plan`, the steps where the NPC asks for the goods), with no offer of the NPC's own; while the teacher would offer that class, the order
   is not offered beside it, and a «no» to the class lets the order be offered at the next talk. The goods of such an order are handed over at the class's
   step that waits for them, so the NPC's ask comes before its thanks (what V14 meant; under L01 a learner who carried the goods was paid at the first talk
   and the class found the order done). A class that does not tell the order (Mar's `el-pueblo`) is offered right after the order's question is answered.
5. **The skip aid** (`--skip-to-session`) gives the orders it delivers; the target session's orders wait for their offers.

How it is built: `GuideSession.OrderOffer.cs`, the objectives' `lessonStep` (`content/schema/objective.schema.json`), an `offering_order` state and an
`<npc>-order-offer` prompt for each order NPC, ten new NPC lines with their recordings, and the validator's `CheckOrderOffer`. Open for the owner: the wording
of the eight lines, and whether a declined order should come back at the next talk rather than the next day.

## D-game-31: day, night and weather (decided, 2026-11-10)

*Numbered D-game-31 because `merge-main-into-german` holds D-game-29 and D-game-30 is the orders decision above.*

The owner asked for "a modular foundational weather system", the first version with "the basics like rain, cloudy, sunny", that "should
allow for future weather systems and be modular". The plan is [the weather plan](weather-plan.md) (the E series, on `feature/weather`).
The questions and the answers:

1. **How far the first version reaches:** "Atmosphere + NPC answers". Sunny, cloudy and rain as saved, deterministic world state with
   sky, light, fog, rain and a rain sound; the NPCs answer «¿Qué tiempo hace?» truthfully. No gameplay effect yet.
2. **How the weather changes:** "I guess we need a day / night cycle implemented first, then add the spells + new roll on waking." So a
   day and night cycle is built first; the weather then moves on its own in spells of a few minutes, and a sleep rolls the morning's
   weather.
3. **The clock and sleeping:** "Free-running cycle". Day and night loop on their own (about 30 minutes of day and 10 of night, set in
   data). A sleep jumps to the morning; the day counter still moves only on a sleep, so one "Sesión n" can see several sunrises. Sleep's
   rule (D-game-25) does not change.
4. **Where it is built:** "Make sure to make a new git worktree from main" (`.claude/worktrees/weather`, branch `feature/weather`).
## D-game-32: the inn, bought land and the campsite (decided, 2026-11-10)

*Numbered D-game-32 because `feature/weather` holds D-game-31 (day, night and weather) and `merge-main-into-german` holds D-game-29.*

The owner asked for a review of [the settlement plan](settlement-plan.md) (the proposal of 2026-11-09: an inn with a bed, land
bought from the mayor, a campsite on it, then a larger village and valley), the decisions it needed, and an implementation plan.
The review's findings are in that plan's section 8.1. The questions and the answers:

1. **(a) When it is built:** "Build now on a branch". The first slice (the inn, the first night, the land purchase, the
   campsite) is built on `feature/settlement`; `main` moves only by the owner's merge, so the bread pilot (W11) can run from
   `main` or a tag at any time. On the plan itself: "Do the work on a new git worktree from main" (`.claude/worktrees/settlement`).
2. **(b) The inn's bed:** "Free bed, coins for other things". Asking the innkeeper for a room gives lodging for good: no fee, no
   expiry, no bunk. The proposal's fee bought nothing, since every bed gives the same night. Coins go to the land now and to
   meals at the inn later.
3. **(c) Where the player may build:** "Protect village, own land adds rights". Placing stays allowed in the wild, so a plank
   dam in the river still works; it is refused inside protected regions (the village, the roads). Only owned land takes the camp
   kit, and only there can the player take back planks they placed.
4. **(d) Old saves:** "Saves break". The save version becomes 8, older saves are refused and the fixtures are recorded again.
   No "legacy mountain home" and no migration work, as the owner's rule of 2026-11-04 already said.

**The defaults the owner accepted with the plan** (each can be changed by a word): Tomás the villager becomes the mayor
(alcalde) and keeps his id, his lessons and his services, as W22 foresaw; one innkeeper, whose voice is provisional and heard
afterwards (the rule of D-game-22 (d)); no one is locked out of sleep by Spanish (the innkeeper offers the room, and «no sé» or
two misses give it anyway, as D-game-27 does for a hand-over); one plot, a fixed rectangle marked with a stake the mayor gives
for free, its size and price set as data from measured earnings; the purchase includes one camp kit (a tent, a bedroll, a fire
ring and a torch, placed as one piece); the land's steps are one checklist, «Mi terreno», shown from day 2, which never holds
sleep back; the inn stands inside the village's present disc, in place of `house3`; the models follow the size rule of
2026-11-06.

**What it changes:**
- **D-game-15:** the player still wakes alone on the cliff ledge, but the ledge is only where they arrive. The camp there goes,
  and the first day no longer ends by lighting a fire at it.
- **D-game-24, point 1:** the player "wakes at the last fire they slept at" becomes the last place slept, a bed or a fire.
  Before any sleep that place is Luna's fire.
- **D-game-25:** day one's ending is the introduction, the camp task and a room at the inn. From day 2 the player sleeps at any
  sleep place they may use (the inn's bed, later their own bedroll or lit fire) whenever the sleep drive is below 75. Lessons
  stay optional.
- **The village plan's principle 1** ("the world grows inward"): the village is rearranged to take the inn, and land outside
  its disc can be owned. The map and the disc do not grow in this slice, so D-game-22's "avoid making the map larger" holds.
- **The activities plan's decision (f)** and its stage 5: W22's first lot and W23's stake are built by Y09 to Y12, before the
  pilot's data. W24 onward (storage, trust, gear, the healer, tenants, fishing) stay held. W23's spoken claim by direction and
  landmark is not part of this slice.

**After Y00's probes (the plan's section 9), the same evening,** the owner answered three more questions:

5. **(e) The words:** "Add the four words". `posada` (the inn), `terreno` (the plot), `alcalde` (the mayor) and `estaca` (the
   stake) join the A2 list and the lexicon, each in the milestone that first uses it. They are not on the lists the content gate
   checks today, and `posada` and `estaca` had no lexicon entry; `hotel` and `tierra` would have passed as they stand.
   *Note (Y04, 2026-11-11):* `posada` went into the marked block of world words at the end of `cefr/a1.txt`, the rule of
   `content/README.md` for a word of the world, not into the A2 list; the lists are cumulative, so the A2 gate passes it, and an
   A1 lesson may use it too. The other three words follow the same rule unless the owner says otherwise.
6. **(f) The innkeeper's voice:** "pf_dora, provisional". A woman's voice, the nearest to native Spanish of the three unused
   ones, and probably close to Luna's. Recorded with it now; the owner listens afterwards and may change it (the rule of
   D-game-22 (d)). The trial's files are in `artifacts/voice-trial/innkeeper/` of the feature branch's worktree (moved to `artifacts/settlement/voice-trial/innkeeper/` of the main checkout on 2026-11-15, when that worktree was removed).
7. **(g) A third building agent:** "Launch Y01 now". The weather session held both of the machine's two building agents, so
   Y01 was launched as a third: one agent of this series beside the weather session's two, with every heavy command still
   queued through `scripts/limited.sh`. The answer was given for Y01.
   *Lifted (owner, 2026-11-11):* "you can run two agents at once now": the weather series is merged (PR #64) and no other
   session builds on the machine, so two agents of this series may run at once (the machine's limit of two).

**Not decided here:** the larger village and the larger valley (the proposal's phases 6 and 7), which wait for a playtest of
the first slice; a fee, meals or comfort at the inn; more than one plot.

## D-game-34: a tall procedural world round hand-crafted regions (decided, 2026-11-15)

*Numbered D-game-34 because `main` holds D-game-32 (the settlement series) and D-game-33 is reserved on `feature/talk-sets`.*

The owner asked how to add procedural generation "like minecraft" while keeping "areas of custom crafted, like what exists now with the
village, and mountains", with basic biomes, modular SOLID code ready for a temperature system, caves, lava, a few kinds of stone, iron, tin
and copper ores, and a tall world like Minecraft 1.18's. The plan is [the biomes plan](biomes-plan.md) (the B series, on `feature/biomes`);
the owner's answers of 2026-11-15 are in its section 5.

**Decided** (the owner accepted the proposal as written, 2026-11-15):

1. **The world:** endless and streamed, y −64 to 320, with the sea level kept at 32. It is generated procedurally outside authored
   regions: biomes, great mountain ranges, rivers, lakes and sea above ground; rock layers, caves, lava, and iron, tin and copper ores
   below. Biomes, rock layers and ores are rows of data; no id of one appears in `src`.
2. **The valley:** the first authored region, byte-identical inside until B13 (which brings the underground into its stone), opened by a
   pass through the east side of its ring with a road out. Later towns are authored regions placed the same way (B15).
3. **Saves:** only changed chunks are saved; the format becomes version 9 and older saves stop loading (the rule of no save backward
   compatibility).
4. **What it amends:** the prototype brief's LEAVE FOR LATER line on procedural terrain, multiple biomes and a large open world; D-game-22's
   inward-only growth; the adaptation design's decision 15 ("inside the valley for now").
5. **What it takes over:** the settlement plan's phase 7 (the larger valley, a fixed 512×512 world).

**The questions and the answers** (2026-11-15; the plan's section 5):

6. **When the new land opens:** "Its unlocked after completeing currently all the lessons. However a cheat mode in the settings should
   bypass this." The pass opens once every lesson in a list in `world.json` is complete; the list holds all of today's lessons, so a
   lesson added later does not close the pass again unless the list is edited. A cheat setting in the settings menu opens it at once.
   Until then (the owner chose the plan's recommendation): a rockfall blocks the pass and the valley's outer edge keeps today's
   invisible wall, so the ring cannot be climbed over; the land outside is drawn but cannot be reached. The rockfall goes and the wall
   lifts when the pass opens. Built in B08.
7. **Lessons:** none in this series uses the new land or the underground.
8. **Digging and gathering:** "Nothing can be gathered by hand yet, this is just the world generation for it." Rock, gravel and the
   ores are not gatherable; the ores have no items and no nouns in this series, so the question of «estaño» at the A2 ceiling does not
   arise yet. Bedrock stays unbreakable for when digging comes.
9. **Lava:** blocks the body and glows; it does no harm and the needs model does not change.
10. **Parallel work** (2026-11-15): "Let's try to do as much in parallel as we can". Asked how many building agents, the owner chose
    "Up to three" for the B series (from four, three or two); `scripts/limited.sh` is unchanged, so at most two heavy commands still run
    at once within its caps. The orchestrator split B08 into B08a (the pass, which needs nothing new) and B08b (its lock, after B01), and
    moved the edge wall to B07.
11. **Review stops** (2026-11-15): at B02, B07, B08b and B13 only the work that depends on the review waits for the owner ("Hold only
    what depends on it"); independent milestones keep building.
12. **Pictures** (2026-11-15): "Make sure to get the agents to provide screenshots at important parts so that I can review the biomes and
    how they look and what's being generated." Every B milestone that changes what is generated or drawn leaves screenshots or maps for
    the owner (the plan's section 4, "Pictures for the owner"); B03b adds a procedural sample to the game so the biomes can be seen in
    3D before the streamed world.
13. **Data and code** (2026-11-15): "The data should just be for basic stuff like, you know, the types of biomes, but the biome generation
    code might be different. Like the deserts, we might have to have custom code ... I'm not expecting that stuff to be in the data."
    Data holds biome types and parameters; biome-specific generation is code, named by a biome row as a strategy (the plan's section 3.2,
    "Data, not code").
14. **Regions that can change** (2026-11-15): "In the future, that custom area might expand in size, shrink in size, and there might be
    more custom areas added ... if I want to make that starting area twice as big, I shouldn't have to change much." Of the four changes
    proposed, the owner chose all four, as one milestone at the end: "add the 4 regions, but add them to the end of the milestones ...
    It's not important at the moment." B16, after B14 and B15.
15. **Leaving the valley after the unlock** (2026-11-15): B08a found the ring walkable almost everywhere (a walk from the village reaches
    the east edge over the mountains), so before the unlock the edge wall is what keeps the player in and the rockfall closes the road.
    Asked how the player may leave once the pass opens, the owner chose "Anywhere": the edge wall lifts all the way round; the pass and
    its road are the easy way out (from "Only through the pass" and "Make the ring unclimbable").
16. **The review of B02's maps** (2026-11-15): ranges were needles about 50 blocks wide, most "ocean" scattered inland basins with
    steep coasts, and snow lay on cold lowland. The owner chose, each from two or three options: "Long ranges" (chains hundreds of
    blocks wide with foothills, peaks 200 to 300, like Minecraft 1.18), "Big seas and continents" (connected oceans, coastal shelves,
    wide beaches, inland basins as lakes) and "Only up high" (snow on the mountains only; cold lowland stays taiga). B02b tunes to it.
17. **One agent, then stopped** (2026-11-15, about 17:27 UTC, while playing games): "I need you to scale down resources, im playing
    gaems right now", then "only have 1 afgent max, you can let the agents finish but going forward", then "actually im going to stop
    the work" and "pls make sure everything is writte ndown". Item 10's three agents become one building agent at a time from now on.
    The running agents (B03b, B04b's merge, B06 just launched) and B02's gate were stopped; their state is in the handover.
    Resumed at about 18:37 UTC ("continue"), still one building agent, every heavy command at one machine-wide slot while he plays;
    and no game windows (screenshots, game runs, scripted runs) until he says he has finished ("After you finish", about 19:10 UTC).
18. **Rivers near a region start in the hills** (2026-11-15, on B06's pictures, where rivers stop in a rounded end at the blend
    margin): near a region a river narrows and shallows to a stream and starts at a spring or a small pond on the slopes outside the
    margin, and flows away to a lake or the sea; no river or lake water inside the margin, and no lake cut straight by it. Chosen over
    bending rivers round the valley and over rivers flowing through it (which would change the hand-made valley). B02b does it.
19. **Up to four agents** (2026-11-15, about 21:45 UTC): "you can have up to 4 agents now". Item 17's one agent becomes up to four
    building agents at once; heavy commands go back to `scripts/limited.sh`'s two machine-wide slots. No game windows still waits for
    the owner's word that he has finished playing.
20. **B02b held for a second round; sea about 35 to 45%** (2026-11-15, about 21:58 UTC, on B02b's maps: 60 to 65% sea in islands of
    2,000 to 5,000 blocks, flat snow plateaus for mountains, the valley on a low coastal dome ringed by beach and puddles): "Hold B02b,
    redo it" (over merging it with a follow-up, or keeping it) and "About 35 to 45%" sea (over 50% or keeping 60 to 65%). The redo
    also asks for ranges of peaks, land rising inland round the valley, no puddled beach flats and rivers from the mountains
    (`.claude/briefs/b02b-redo.md`).
21. **The far view, folded into B07** (2026-11-15, about 22:02 UTC; the owner: "we need high render distance, performance is important",
    "like say horizons mod in minecraft, without taking big performance hits"): beyond the voxel chunks (High 400 blocks today), level-
    of-detail terrain tiles read straight from the pure height model and biome table, coarser with distance and built on worker
    threads, show the land **to 16 km at 60 fps on the High preset** on his machine. "Fold it into B07" (over a separate milestone
    after B07, or a later series); "16 km" (over 8 or 4 km). The plan's "does not do" loses distant-terrain detail levels.
22. **The body sinks into lava** (2026-11-15, about 23:15 UTC, on B10's report that a body could stand on a lava surface): "You should
    sink into it. Damage doesn't need to be done in this pr." Lava acts on the body as a thick liquid (slow sinking, moving and
    swimming up); no harm in this series. B10b does it. Item 9's "lava only blocks the body" is replaced.
23. **The ores easier to see** (2026-11-15, about 23:25 UTC, on B10's tile sheet): "I think the ores should be more obvious. In terms of
    their colour." Larger clusters of strong colour with a rim and a highlight (iron peach-tan, tin silver-white, copper orange with
    green patina) on the same stone. Added to B10b.
24. **B02b gets a third round before it merges** (2026-11-16, about 01:30 UTC, on round 2's maps: 38 to 42% sea in one ocean,
    continents of 8,000 to 12,000 blocks, ranges rising inland round the valley, clean beaches; but rivers mostly dry valleys since water
    stands only at sea level, broad massifs, polygon coasts): "Another round first" (over merging round 2 with a follow-up B02c, or
    merging it as it is). Round 3: rivers hold water all the way down (a water level per column, generated at rest), thinner chains,
    smoother coasts; then the whole tuning is gated once (`.claude/briefs/b02b-round3.md`).
25. **The far view gets detail like the Distant Horizons mod (B07c)** (2026-11-16, about 08:50 UTC, on B07's pictures: "that looks
    pretty crap, just a big green swath"; he asked for better distant quality that keeps the performance). Chosen: "Blocks, trees,
    colour" (over adding real-chunk level of detail for built things now, or a design note first): blocky far land (box columns with
    tops and sides, flat runs merged), trees in the far view from the tree pass's own deterministic placement (single canopies out to
    about 1 to 2 km, a raised canopy layer beyond), colour from each block's texture with per-cell variation, rock on steep slopes,
    top and side shading, rivers at least a cell wide, finer cells near (2 blocks) and coarse beyond about 8 km; High held at about
    12 ms a frame on the RTX 3070. Real-chunk level of detail (towns, the valley's buildings, edits seen far off) is a later step.
26. **The far view works like Distant Horizons, "one-to-one parity ideally"; the world stays block-based** (2026-11-16, about 09:00
    UTC): "the world is block-based, not kind of smooth voxel-based, right? So we need to make sure that is true ... it should operate
    like the horizons mod ... if we can get one-to-one parity with that, ideally would be great, so that we can basically see in the
    distance and it still has the high level of detail. Because currently having just the smooth, it looks terrible." Widens item 25:
    B07c builds Distant Horizons' model (level-of-detail columns of vertical segments with block and light, built from the generator's
    own columns, trees and regions; greedy box meshes, never a smooth face, tested; block-texture colour with noise; translucent water;
    stored light for night and glow; 1-block columns past the voxel zone with detail halving by distance bands); B07d adds what remains
    for parity: level of detail from loaded chunks as they unload (edits seen far off), the store saved to disk per world, the detail
    settings on the settings page. The frame-time target relaxed (about 09:10 UTC): "the performance target can degrade slightly if
    needed ... prefer quality over performance to some degree, so don't obsess over the 12 millisecond per frame ... What we want to focus
    on at the moment is getting it to render distant chunks properly." B07c does not cut detail to reach 12 ms; it measures and names the
    costs for later tuning.
27. **More rock variety, in caves and on bare stone** (2026-11-16, about 09:25 UTC): "We probably need some more rock variation, like
    gravel, sand stuff. So, for example, in a cave, rather than just all being stone, it should have different mixtures of stone to break
    things up ... perhaps some segments of gravel in there or something to break it up." Added to B11b: pockets and bands of gravel,
    sand, dirt and the stone kinds (granite, limestone, slate, mossy stone, new kinds if they read better) in cave walls and floors and on
    exposed stone at the surface (cliffs, pits, mountainsides, scree), as data. The valley's own surface stays as authored unless he asks.
28. **limited.sh's shared memory pool raised from 8 GB to 12 GB** (2026-11-16, about 11:35 UTC; four agents' test runs were killed in the
    8 GB pool and B12's first gate with them): "Yeah, I think increase the shared pool to 12 is fine." `VOCATERRA_TOTAL_MEM_MAX` defaults
    to 12G; each command keeps its 6 GB cap and the two machine-wide slots stay. Raised again about 11:55 UTC: "We have a decent amount
    of memory on this machine so i would increase to 16gb": the default is 16G. The cap per command raised from 6 GB to 10 GB (about 12:40
    UTC; the gate's single test command, two assemblies at once, peaked at the 6 GB cap and a fuzz test threw OutOfMemoryException in
    B07b's first gate, passing alone at 1.7 GB): "Raise the cap to 10 GB" (over testing one project at a time, or rerunning on failure).
29. **The far view built like the Voxy mod** (2026-11-16, about 11:45 UTC, with three screenshots of Voxy: individual tree crowns, lakes,
    rivers and snowy ranges as blocks kilometres out): "For the LODS we want something like https://modrinth.com/mod/voxy. Of course ignore
    shaders that is not part of this work." Replaces item 26's Distant Horizons columns: B07c builds a 3D voxel level of detail (cubic
    sections, each level 2^L blocks a cell, mipped 8 to 1) from real chunks as they are generated or loaded (edits included) and, beyond a
    ring of real chunks, from the generator's own model in the same format; kept on disk per world; greedy textured block faces with the
    stored light; culled by a node tree on the CPU (MonoGame has no compute shaders). Voxy's code is All Rights Reserved: ideas only.
    B07d: the detail settings, a world import or pre-generation command, performance tuning.
30. **Custom shaders planned, not built** (2026-11-16, about 12:05 UTC, after hearing that the processor bakes all light into vertex
    colours and the card only draws, because MonoGame's shader compiler needs Wine on Linux): "yes plan the custom shaders milestone.
    Dont do any work but just document it." B17 in the biomes plan (section 3.9): light, fog and water in the game's own shaders; not to be
    launched until he says.
31. **B11b held: its cave entrances must look natural** (2026-11-16, about 13:45 UTC, on B11b's pictures of a straight 3-wide stair
    with flat walls and a slab rim): "The second cave entrance is that a natural cave entrance? Because it looks man made." Chosen: "Hold
    B11b and rework it" (over merging it and fixing the entrances in a B11c). Its gate was stopped and the merge undone; the rework makes
    each passage wind, vary in width and height, with noise-carved walls, an uneven floor and a mouth that fits the ground (an overhang
    in a hillside, a ragged hole on flat ground, no rim), still walkable to a cave 20 or more down. The rock variety stays.
32. **B07c gets an Opus review before its gate** (2026-11-16, about 14:00 UTC): "once it's done, spawn an Opus 5.5 agent on high effort
    to review the changes." When B07c reports, an Opus 5.5 agent at high effort reviews its branch (correctness, the Voxy design, the
    store on disk, threading, performance, the tests) before the gate; its findings go back to B07c's agent or into a follow-up.
33. **Caves stay at 28% of daylight; river drainage and the core fuzz fixes launched** (2026-11-16, about 15:30 UTC): "The caves can
    remain at 28% daylight, that's fine. Kick off an agent for the river drainage issue and the core fuzz fixes as well." B12's cave
    minimum 0.28 stands. B02c (rivers that only fall: a drainage network, closing B02b's river humps) and the core's M97 (the fuzz seeds
    `2LxdNIrNHnQ6` and `9kz6LGNo8tf2`, and the load-sensitive OpenFallback coverage oracle) are launched.
34. **More grass, with a slider** (2026-11-16, about 16:00 UTC): "there's not a lot of grass, I guess. Could we increase the volume of
    grass? Perhaps make that a slider in the settings as well." B18: a decorative grass layer drawn on grass-block tops (crossed tufts
    like the undergrowth's tall grass, at positions from a hash of the cell, no world block, no collision, not saved), its density a
    slider on the settings page (saved with the other settings), its default denser than today's look; the generated world and the
    valley unchanged (a setting must not change the world's blocks). Then: "at least, you know, grass in the plains and the forests and
    stuff like that": lush in plains, meadows and forest floors, sparser in taiga and dry land, none on sand, snow or stone.
35. **The far view at Voxy's distances: B19 documented, his screenshots the reference** (2026-11-16, about 16:10 UTC): after asking what it
    takes to reach Voxy's and Distant Horizons' render distances ("that's what we want to achieve") and hearing the four limits (building
    the data, draw calls, the card's memory, precision) and a two-step plan (within MonoGame first; card-driven rendering only if needed):
    "Use those as reference" (his three Voxy screenshots of item 29, kept outside git in `artifacts/reference/voxy/`, third-party, never
    committed). B19 in the biomes plan (section 3.10), planned only like B17.
36. **No snow in the valley** (2026-11-16, about 18:15 UTC): "can you add a milestone to remove the snow from the valley, it now doesnt
    make sense to have snow on the mountain when the valley with the procedural generated area". The hand-crafted valley lays snow on its
    ring's tops above `snowLine` 100 (scene.json; `SceneBuilder`), while the generated land round it has snow only above y 120 on its
    mountains. B20 removes the valley's snow (the tops become the stone or grass the slope would carry); snowfall as weather is unchanged.
37. **All the codebase review's fixes, go** (2026-11-16, about 22:15 UTC, on `docs/research/2026-11-16-codebase-review.md`: "go ahead with all
    the fixes recommended from review"). Planned in this order, at most four agents at once: B07f (the far store's edit layer: C1 to C3,
    C6, C7, C10, race tests), B22 (a fidelity bundle with built-in effects: MSAA, a moving sun and deeper dusk, biome colour maps, clouds and
    water), B21 (meshing off the game thread, dirty-only rechecks, per-frame allocations: P1, P2, P5, C4, C5, C11), B23 (sky and block light
    propagation with smooth light: G1), B07d widened (the far store's memory and IO: P3, P4, real-section LRU, compaction, with the
    rendering review's R1), B24 (guards and cleanups: A1, A2, C8, C9, T1, T2, the docs) and the core's M98 (a realisation-rate gate for the
    fuzz generator: T3). The rendering review's decisions D1 to D6 stay open (B07d gets a worker-count setting whose default is the owner's).
38. **Five agents, three slots** (2026-11-16, about 22:30 UTC): "we have a good amount of memory available so we can have more agents run in
    paralel", then, offered 6 agents with 3 slots, 8 with 3, or 6 with 2: "what about 4 agents like now, but one extra slot", then "5
    agents and 3 gates looks ok". Up to five building agents; limited.sh runs three heavy commands at once (`VOCATERRA_HEAVY_SLOTS`
    default 3); 10 GB a command and 16 GB in all unchanged.
39. **macOS is out of scope** (2026-11-16, about 22:45 UTC, the rendering review's D4): "macOS does not matter". Opens the raw OpenGL 4.x route
    (OpenGL 4.6 is not available on macOS) for B17's shaders and B19's later steps.
40. **B17's shaders as raw GLSL compiled at run time** (2026-11-16, about 22:50 UTC, the rendering review's D3, offered (a) mgfxc on a Windows
    CI runner with committed binaries, (b) a pinned Wine, (c) raw GLSL on MonoGame's OpenGL context through an MIT binding (Silk.NET or
    OpenTK), (d) waiting for MonoGame's new back ends): "Let's go with c". The driver compiles the shaders at start; the world's drawing
    goes through the binding on MonoGame's own context with its state saved and restored; it opens OpenGL 4.x for B19's step 2. B17 starts
    with a spike proving one GLSL shader draws chunks inside MonoGame's frame.
41. **B17 goes ahead** (2026-11-17, about 08:05 UTC, after B17a's spike recommended go and the orchestrator said B17 is mainly for the
    look and for B19, not a big speed gain): "Okay, you can kick off B17, that's fine." Split into B17b (every chunk pass on the GLSL
    program, the state scope, render targets, the fallback, a timer query) and B17c (light as data, fog, water, the far view on the same
    light model), after B21; B23 (propagated light) follows B17c so its light goes straight into the shader's data.
42. **Game worker threads: Auto by default, and a setting** (2026-11-17, about 08:10 UTC, the rendering review's D1; offered a "Worker
    threads" setting with Auto, cores minus 4 capped at 8, as the default for play, or a fixed 4): "Yeah, auto is fine. I mean, perhaps
    we should allow that to be configured in the game as well." The setting is on the settings page (Auto or 1 to 8) and a launch
    option; scripted runs, tests and agents keep 2 (the machine rules for builds are unchanged). It lands with B07d; `JobPool.MaxWorkers`
    (2) becomes the setting's bound. The world does not depend on the count.
43. **Every added graphics effect has a setting** (2026-11-17, about 08:45 UTC): "For the B17 work, all the additional graphics we're
    adding probably should be toggleable via a setting"; asked about B22's (already merged): "Sure, add those as well. Probably group them
    under the graphics setting." A Graphics group on the settings page: B17b's shaders on or off, then each B17c effect (light as data,
    fog by height, the water shader, the far view's light) and B22's sun per band with the deeper dusk, climate colours and water depth
    tint, with MSAA and the grass slider moved into the group; on by default, saved, a launch option each, and the quality presets may
    set them.
44. **The game's packages in the licence scan** (2026-11-17, about 13:10 UTC, proposed with B17a's spike): "Sure, add the game to the
    license scan." B26: the gate scans `Vocaterra.Game`, `Vocaterra.Host` and `tools/Vocaterra.SpeechRender`; Zlib joins the allowlist
    (FontStashSharp, SDL); overrides with evidence for NVorbis and PlatformAbstractions (MIT). MonoGame.Library.OpenAL ships OpenAL
    Soft's native library under LGPL-2.0-or-later: asked of the owner separately.
45. **The far view's targets** (2026-11-17, about 13:10 UTC, the rendering review's D2, D5, D6, each with the orchestrator's
    recommendation): detail chosen by screen pixels, about 2 per block at High, rather than rings tied to the voxel view distance ("For
    two, also go with the recommended"; in B19); the distance kept at 16 km, B19's effort spent on detail ("For free [three], go with the
    recommended"); built things far off placed by the far view's model as trees are, option (b) ("B is okay for now. It's cheap, so we go
    with it"); the player's own builds still reach the far view through B07f's edit layer.
46. **The silent-h fuzz seed fixed in the core** (2026-11-17, about 13:10 UTC; seed `a2RKk5N6Ibt4`, «ah hambrientos» scored with two
    silent h's): "Fix the silent HC, yeah, yep. Yeah, fix number five." Core milestone M99.
47. **B03b runs** (2026-11-17, about 13:10 UTC; offered dropping it, since B14's tour photographs each biome): "For number six, yeah, run
    that, it's fine." Read as finishing B03b after B14, for what the tour lacks; the owner was told he can say "drop".
48. **Cave mouths in hill and cliff faces** (2026-11-17, about 13:10 UTC, B11b's follow-up): "Seven, also do that as well." B11d, after
    B11c.
49. **OpenAL accepted** (2026-11-17, about 14:30 UTC; B26 found MonoGame.Library.OpenAL ships OpenAL Soft's native library under
    LGPL-2.0-or-later; offered a one-package exception or refusing it): "We don't really care about license, so just accept it, it
    doesn't really matter. This is still only an internal game, so licensing is really not a problem at the moment." A one-package
    exception in the licence scan (LGPL stays off the general allowlist); a release would carry the LGPL text and a source link.
50. **The throughput study's changes, once the running work drains** (2026-11-17, about 20:20 UTC; the study
    docs/research/2026-11-17-ai-development-throughput.md, offered (A) tests in Release builds, (B) a new slot policy in limited.sh, (C) a
    gate that skips the test run when the agent's full run covered the exact tree, (D) a test-time report in the gate): "For the meta agent
    analysis can we kickoff [it] once all current active work is done". Read as yes to A to D (A only after one measurement shows it
    faster with the goldens unchanged; B keeps the slice's 14-core and memory caps), applied by one milestone with nothing else running;
    no new milestone starts until the running ones have merged. The owner was told he can drop any of A to D.
51. **feature/biomes into main by the morning of 2026-11-18** (2026-11-17, about 23:15 UTC): "Don't worry about the caching for now.
    We'll come back to performance later on. We need to basically just finish up everything and get this feature biome merged to main by
    tomorrow morning"; asked which milestones go in first: "Small ones only (Recommended)": M101, B11d and B02d join B17c and B03b,
    with B30 (CI ready: the timing tests on CI's slower runner, CI's timeout); B29 (item 50's changes), B23, B16 and the caching ideas
    wait until after the merge, on a fresh branch from main. The orchestrator opens the pull request; the owner merges it. Also: "Make
    sure to include document any milestones that we haven't accomplished once this is done, because we'll obviously need to do that in
    a fresh agent on a fresh PR": a new handover lists them.
52. **Chunk generation's speed is the top priority** (2026-11-18, about 08:45 UTC, after the far view of a fresh world, seed 5315, took
    minutes to fill and showed a sky-coloured ring between the near zone and the distant hills): "Yeh lets kickoff an opus 5.5 agent high
    to review how to speed up the chunk generation", then "this is the biggest issue right now and we need to get it right". An Opus 5.5
    review at high effort (measuring, read only) was launched; its ranked plan goes to the owner before any milestone is built.
53. **Where the speed-ups land** (2026-11-18, about 08:55 UTC): "speed upgs go in a new pr / branch. b31 once done is merged to main".
    B31 (the Shaders switch crash) is the last change on `feature/biomes`; once it is gated and pushed, PR #69 is ready for the owner to
    merge. The chunk generation speed-ups go on a new branch from `main`, as a new pull request.
54. **The speed-ups on feature/biomes after all** (2026-11-18, about 09:00 UTC, replacing item 53's second part): "actually no the
    performance improvements can go on this branch too". The chunk generation speed-ups are milestones on `feature/biomes`, gated there
    like the rest, and reach `main` through PR #69 (or a later pull request from the same branch if the owner merges #69 first).
55. **More and bigger rivers, kept logical** (2026-11-18, about 08:45 UTC; asked after "we seem to also have fewer rivers now? im testing
    seed 42, and it used to have a large river", offered more rivers, bigger rivers, a major river near the start, or all three): "more
    rivers and bigger rivers, but they need to be logical". B34: a lower source threshold so streams appear and join, widths and depths
    that grow with the catchment (major rivers about 12 to 40 blocks wide), every river downhill to a lake or the sea, widening downstream,
    in a valley of its own; no river forced near the start.
56. **All the new work in a new pull request; only B31 on feature/biomes** (2026-11-18, about 08:47 UTC, replacing item 54): "lets do all
    this in a new PR actually so queue all those up. only push b31 the fix for shader on and off to the feature/biomes branch", then "put
    all these in the handover, for a new agent to pickup, so once b31 and the opus agent come back, record performance findings from opus,
    push b31 and then we are done". B31 is the last change for PR #69. The speed-ups from the Opus review, B32 (grass-step sides), B33
    (leaf aliasing) and B34 (rivers) are queued in the handover for a new agent on a new branch from `main` after the owner merges #69.
57. **The throughput review's changes, on their own branch** (2026-11-18, about 11:45 UTC; the review
    docs/research/2026-11-18-throughput-review.md checked the study of item 50 and re-ranked its changes): "I approve of all cahnges,
    except we should not run the chunk speed milestones in this branch", then "Commit to new branch called feature/meta_improvements,
    based off of the latest main branch". B29 becomes B29a (Release tests, the gate in one slot, failed tests re-run alone, a test-time
    report, item 58) and B29b (no wall-clock asserts outside the timing collections, the conflict hot spots), plus a hook against polling;
    B29's token policy (item 50 B) and stamp skip (item 50 C) are deferred until a re-measurement. All on `feature/meta_improvements`;
    the chunk-speed series (0a to 0f) does not run there. A failed test that passes alone is listed as FLAKY and recorded as a finding;
    one that flakes twice moves to a CPU-time budget.
58. **Timing tests in the gate and CI only; the fuzz suite in CI only** (2026-11-18, about 12:00 UTC): "I think timing tests should be
    reserved for gate runs, and ci. Fuzz testing is reserved only for CI." `scripts/test.sh` leaves out the timing tests (a `timing`
    mode runs them on purpose) and the fuzz suite; the gate runs the timing tests as their own step after the suite and no fuzz suite;
    CI's `build-and-test` runs everything. The fuzz suite's oracles are never weakened. Part of B29a.
59. **The orchestrator reads CI** (2026-11-18, about 12:10 UTC; asked how fuzz failures would be seen with the fuzz suite only in CI):
    "It does run on pull requests, i have checked. Yes allow orchestrator to run gh run list." A branch whose work needs CI has a pull
    request open (a draft until it is ready), so every push runs CI; the orchestrator checks it with `gh run list --branch <branch>` and
    `gh run view` (the token's scopes now allow it) after each push, and a failure becomes a milestone.
60. **The throughput follow-ups** (2026-11-18, about 15:20 UTC; after the re-measure: the gate about 10 minutes, `test.sh fast` 97 s):
    "Go ahead with whatever you recommend." B29's deferred parts B (the five-token slot policy) and C (the stamp skip) are **dropped**:
    the gate holds one slot and takes about 10 minutes, so neither pays. Two follow-ups run on `feature/meta_improvements`: B29d, the four
    timing classes still timed with a wall `Stopwatch` (`ChunkIndexTests`, `RegionTimeTests`, `ProceduralTimingTests`,
    `ProceduralChunkTimeTests`) move to `CpuTime`; B29e, the polling hook lets a command that only quotes `sleep` text pass.
61. **The chunk-speed series goes ahead** (2026-11-18, about 18:10 UTC; after the speed review
    `docs/research/2026-11-18-chunk-generation-speed.md` was explained in plain terms): "Yeah, let's go ahead with these changes. So we'll
    make it on a new Git work tree off of main, push to a new feature branch, and clean up the docs." The review's milestones are B35 to
    B40 (game plan 4.14) on `feature/world-next`, cut from `main` at `ae30d65b`, before B32 to B34. B37 (cheap coarse levels) still needs
    the owner's sign-off on before/after pictures, and server GC in B40 his yes (it raises memory). B36 commits a generation bench so each
    step is measured against the same baseline.
62. **Nearer chunks first** (2026-11-18, about 22:00 UTC; after his test of `feature/world-next` and B43's suspects): "the job order is
    interesting, as definately closer chunks are more important at least." Work near the player (the voxel zone's chunks and their meshes)
    takes priority over far work (far tiles, stand-ins, the ring). B43 measures a strict near-first order; the fix follows from its report.
    He also asked for a fly mode for testing and withdrew it ("dont add it pls").
63. **B37's far picture approved; B43's fix 1 first** (2026-11-19, about 07:10 UTC): shown B37's before/after pictures (0.01 to 0.02% of pixels
    differ; distant cliff-rock specks turn green), the owner chose "Approve" (not the stronger `MergeLevels` 3, which loses most distant cliff
    rock). Shown B43's ranked fixes for the start area, he chose "Only fix 1 first": B44, the sky tile off the game thread; the others wait
    for its measurement.
64. **After B44, the near-first gate** (2026-11-19, about 08:05 UTC): shown that B44 cut the start area to 11.7 s and offered four follow-ups
    (a near-first gate for far work, loading caps, a bigger group cache, a bigger mesh budget), the owner chose only the near-first gate: B45.

## D-game-35: inventory and equipment (decided, 2026-11-15)

*Numbered D-game-35 because `main` holds D-game-32 (the settlement series), D-game-33 is reserved on `feature/talk-sets` and
`feature/biomes` holds D-game-34.*

The owner asked to plan "a proper inventory system to the player. This will include armor slots, clothing slots, accessory slots and a
backpack slot. Kind of like minecraft", and pointed to the Core Systems Design Document's section "Inventory and equipment". The plan is
[the inventory plan](inventory-plan.md) (the I series, on `feature/inventory`); the owner's answers of 2026-11-15, given while it was
planned, are in its section 5.

**Decided** (the owner accepted the proposal as written, 2026-11-15, "Accept, but don't build yet"; the answers to its two
questions are below). The owner said to build later the same day ("Work on the plan"); I00 registered the series. Asked whether to launch I01 beside the
biomes session's agent while he games (D-game-34 item 17), the owner said "stop and wait for me to tell you to start": I01 and I06 wait
for his word.

1. **Armour gives an armour rating for combat**, which a later series adds. It reduces nothing now (not drowning, lava or cold); the
   damage code is a pipeline whose causes and rules are data, so later causes need no code change.
2. **Clothing is a warmth layer under armour** on head, body, legs and feet. A temperature from the time of day, the weather's channels,
   shelter and a nearby fire (and later the biomes series' climate) is set against the clothes' warmth; cold slows the player and never
   harms.
3. **The bag:** the hotbar's 10 slots and 20 more (base 30, down from 40). The backpack slot's bag adds 10 or 20 (at most 50); a bag
   cannot come off while its slots hold items.
4. **Items as data first** (`game/items.json`), taking over the activities plan's W12; the item enum keeps only the members code needs
   by behaviour.
5. **Every gear item is craftable and buyable:** workbench recipes, and Pablo or Domingo by voice through `sell` rows.
6. **Worn gear is drawn** on the third-person avatar (cosmetic over armour over clothing), and the first-person arm takes the sleeve's
   colour.
7. **Accessories:** three slots, no duplicates; the first effects are light at night, walk speed, slower hunger or thirst and more
   stamina, each a row of data.
8. **The extras:** an off-hand slot (shield, torch, lantern), cosmetic slots for the four body slots, durability for gear and tools
   (taking over W13; a worn-out item is never lost, a recipe repairs it), a sort button and a trash slot.
9. **Saves break:** each change of the save's shape takes the next version at its merge and refuses older saves; no migration (the rule
   of 2026-11-04).

**What it changes:** D-game-23 (c), proposed as "no combat; gear is for places", becomes "combat later"; the activities plan's W12 and
W13 move to this series, and W26 keeps only the tailor and its voice trial.

**The two questions, answered the same day:**

10. **(a) The gear words:** "World words". The gear nouns on neither CEFR list (`chaqueta`, `abrigo`, `pantalones`, `botas`, `gorro`,
    `mochila`, `escudo`, `tela`, `cuero`, `hierro`, `cinturón`, `collar`, `amuleto`, `farol`, the armour's leg piece and `saco`) join the
    marked world-words block at the end of `cefr/a1.txt`, each with a one-line reason, in the milestone that first uses it (the rule of
    `content/README.md`, as D-game-32 (e) did). Lessons and phrases stay at the A2 ceiling. The final list is settled at the plan's stop 3,
    before I08a.
11. **(b) The small bag:** "el saco". The small bag (10 slots) is `el saco`; the large one stays `la mochila` (20). «La bolsa» keeps
    naming the whole inventory («Tu bolsa está llena»).

## Already decided

- **The core by project reference** (D-game-7; until 2026-10-21 "packages only" from a submodule's local feed). The game never copies core
  source (core ADR 0009); a missing call is a core milestone in `docs/nlp/implementation-plan.md`.
- **Console host until D-game-1 is decided** (decided for the prototype on 2026-10-21: MonoGame; `Vocaterra.Host` remains the console host).
- **Game words live here:** scenes, characters, items, spells, prices, rewards, dialogue and the meaning of each effect kind.
- **The content tool** is the core's ContentBuilder run from source, through the root `scripts/content.sh`.
- **Model policy** (the owner, 2026-11-06): Haiku 5.5 for small tasks; Sonnet 5.5 at medium effort for medium tasks, at high for large or design-heavy ones; Opus only when the owner asks or a decision names it.
- **One orchestrator** for both halves since D-game-7 (before it, one per repository); it never lets a game milestone edit `nlp/`.
