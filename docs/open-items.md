# Open items

One page for what is not finished: the decisions only the owner can make, the things only the owner can do, the work that is planned but
not built, and the known gaps. It is an index, not a source: each row points to the document that holds the detail, and that document
wins if the two disagree. The orchestrator updates this page in the same change that opens or closes an item (a decision answered, a
milestone gated, a pull request merged).

Last checked against `main`: 2026-11-09 (after pull requests #46, #47, #48 and #50). Work on open pull requests is named but not described.

## 1. Decisions for the owner

| Id | Decision | What waits on it | Detail |
| --- | --- | --- | --- |
| Core D8 | Online or offline core | M25 (the tutor), the shape of M23 | [nlp/decisions](nlp/decisions.md) |
| Core D9 | Determinism strictness ("replay-exact", ADR 0010) | M20 onward, the tutor | [nlp/decisions](nlp/decisions.md) |
| Core D10 | MPL-2.0 for unmodified libraries (the GPL and LGPL half is decided) | M20's tagger choice | [nlp/decisions](nlp/decisions.md) |
| Core D11 | Cloud cost cap for a tutor | M25 | [nlp/decisions](nlp/decisions.md) |
| Core D13 | Pronunciation ambition | Follows M24's speech check | [nlp/decisions](nlp/decisions.md) |
| Core D14 | German: the variety, the tagger route, content trust | M36 to M48 | [nlp/decisions](nlp/decisions.md) |
| M79 | The typo slip: a new `vocab.spelling` tag or `vocab.unknown`; a suggestion list on `ErrorTag`; the typo's weight | M79 | [core plan](nlp/implementation-plan.md) §5 |
| M67b | Answer-set verb forms: parked, because it fails 32 game tests that pin V14's design | QA-language-5 and 10 | [core plan](nlp/implementation-plan.md) §5, [QA triage](qa-reports/README.md) |
| D-game-1 | The final engine (MonoGame or Godot .NET) | The optional P10 spike | [game/decisions](game/decisions.md) |
| D-game-22 | The review of village phase B; the four provisional village voices | Recording a rejected voice again | [game/decisions](game/decisions.md), [village plan](game/village-plan.md) section 5 |
| D-game-23 | Confirm or change the adaptation design's section 18.2 recommendations 1 to 20 (adopted provisionally) | The adaptation's final shape | [game/decisions](game/decisions.md) |
| D-game-23 | Decision 19 (heard and read evidence), decision 20 (grant values), and the items of activities-plan section 12 still proposed | The provisional values of W06 and W08 | [game/decisions](game/decisions.md), [activities plan](game/activities-plan.md) section 12 |
| D-game-23 | W09 (imperatives in lessons) and W30 (the recipe book): authorise or not | Both milestones | [activities plan](game/activities-plan.md) |
| D-game-24 | The cost of death; design decisions 13, 15 and 17 (conditions, expansion, relaxed needs) | The needs' tuning; the owner's settings page | [game/decisions](game/decisions.md) |
| D-game-26 | Should a dammed river drain? (Sources never drain today.) Not yet recorded as a decision | Nothing | [water plan](game/water-plan.md) section 8 |
| D-game-30 | Orders are offered and taken on (issue #54, PR #57, proposed): confirm the design, the wording of the eight new lines, and whether a declined order returns the next day (built) or at the next talk | Nothing; built on `fix/quest-giving` | [game/decisions](game/decisions.md) |
| Backlog | R05, R11, R12, R14, R15, R18: the pre-check stopping early, a title screen, adaptive pacing, a numbers lesson, more NPCs, an orders loop; before or after the first pilot | The X series' follow-ups | [improvement backlog](research/2026-10-23-improvement-backlog.md) section 5 |
| Studio | Accept or reject the Vocaterra Studio plan (ST01 to ST08; it would replace C10); five questions | Content tooling | [studio plan](research/2026-11-04-vocaterra-studio-plan.md) |
| German | Seven decisions: bundle shape, saves, register, voices, scope | T01 to T10 (part 1 is pull request #51) | [German in the game](research/2026-11-04-german-in-the-game.md) |
| Mobile | Six questions, in no decision register | Nothing planned | [mobile feasibility](research/2026-10-30-mobile-feasibility.md) section 6 |
| Core systems | Items as data, instructions as content, the learner signal; the cast's size; specialist words against the A2 gate | W12 onward | [core systems review](research/2026-11-01-core-systems-review.md) section 5 |
| Settlement proposal | Review inn accommodation, first land purchase, free site selection, interiors and village/map expansion; proposed defaults in section 7 | Milestone registration and implementation | [settlement plan](game/settlement-plan.md) |
| Playtest | Sessions 7 to 10: a late-start profile, the battery's size, testers with A1 Spanish | "Not planned" | [playtest](game/playtest.md) section 14 |
| Content | Should `content/PHRASES.md` be generated from data? | Nothing | [content plan](game/content-plan.md) |

Deferred by the owner and not to be raised: core D1's remainder and D4, and D-game-12 (a competent Spanish reviewer).

## 2. Waiting on the owner's action

| Item | What | Detail |
| --- | --- | --- |
| Pull requests | #51 (`feature/german`) is open | GitHub |
| Listening | Every speech file, none marked (the count is the checklist's "To hear" line) | [speech checklist](game/speech-checklist.md) |
| Pre-flight | The owner's pre-flight, steps 1 to 7; the next-day recall has never been run | [playtest](game/playtest.md) section 12 |
| W11 | The bread pilot: three tester sittings, delayed recall, the go or no-go | [activities plan](game/activities-plan.md) |
| X07 | The mic lab and the real-GPU bench: only the owner can run them (the tools are still planned) | [game plan](game/implementation-plan.md) section 4.5 |
| M24 | 100 to 200 recordings of real learners (declined for now, 2026-11-06) | [core plan](nlp/implementation-plan.md) §9 |
| Housekeeping | The merged milestone branches on the remote; old worktrees | [village plan](game/village-plan.md) section 5 |

## 3. Planned, not built

| Series | Items | State | Detail |
| --- | --- | --- | --- |
| Core | M86, O9's double silent h (fuzz seed `8HYgz_I9Ysn1`; a silent-h failure of `PronunciationFuzzTests` on "oh hundan", seed `9MOxzRBVGLK4`, was seen once on 2026-11-10 and may be the same family; the same day `PipelineFuzzTests.OpenFallback...` once had `fallback:second-chosen` at 4 against its minimum of 5, a thin margin) | open: launched 2026-11-08, nothing landed | [core plan](nlp/implementation-plan.md) §5 |
| Core | M27b, M27c: content layering, format de-duplication | todo | [core plan](nlp/implementation-plan.md) §10 |
| Core | M27a2, M28b, M28c, M28d, M29a, M29b, M29d, M29i | proposed | [core plan](nlp/implementation-plan.md) §5 |
| Core | M20 to M25: tagger, LLM drafting, oracles, intent classifier, speech check, tutor | unscheduled, behind D8 to D13 | [core plan](nlp/implementation-plan.md) §9 |
| Core | M36 to M48, German | recorded only on `main`; in progress on `feature/german` | [core plan](nlp/implementation-plan.md) §11 |
| Core | M68, CI timing budgets | only if a timed test fails on CI | [core plan](nlp/implementation-plan.md) §5 |
| Game W | W12 to W21: items as data, durability, the axe handle, dishes, the beacon, farming, the notice board, prices | planned | [activities plan](game/activities-plan.md) |
| Game W | W22 to W29 | held until the pilot's data | [activities plan](game/activities-plan.md) |
| Game C | C09 (assessed), C10 the authoring CLI (specified) | not to start unless the owner asks | [content plan](game/content-plan.md) |
| Game X | X07 the owner's tools, X09 playtest tooling | planned | [game plan](game/implementation-plan.md) section 4.5 |
| Game P | P10, the Godot spike | optional | [game plan](game/implementation-plan.md) section 4 |
| Game E | E06 (rain, running) and E08 (the close-out) (E03 the sound and switches, E02a the lanterns' glow, E01 the clock, E02 the drawn cycle, E04 the weather in Core, E05 the sky by weather and E07 the NPCs' answer are done; the 42 new clips of E07 wait for the owner's ear): a day and night cycle, then weather (sunny, cloudy, rain) and the NPCs' weather answer | planned 2026-11-10 (D-game-31); in progress on `feature/weather` | [weather plan](game/weather-plan.md) |
| Game U | U03, U04; any visual redesign | after the pilot; a redesign is not planned | [game plan](game/implementation-plan.md) section 4.9 |
| Tests | T6's follow-ups (a) to (h) | open, judged on the numbers | [test performance](process/test-performance-plan.md) |
| CI | SonarCloud, speech models in CI, the scripted runs as CI tests, the cross-platform matrix | not planned | [game plan](game/implementation-plan.md) section 4.6, [known gaps](nlp/known-gaps.md) |
| Release | The GPL-free sherpa-onnx pin (D2, ADR 0007), an app-local ICU (ADR 0006), the wav2vec2 and e5 ONNX pins | blocks any distribution | [known gaps](nlp/known-gaps.md) |

## 4. Open bugs and known gaps

| Source | Items | Detail |
| --- | --- | --- |
| GitHub issues | Bugs #52, #53, #54, #55; features #14, #23, #49 | GitHub |
| QA triage | QA-language-5 and 10 parked (M67b); QA-2026-11-06-ui-3 partly fixed | [QA reports](qa-reports/README.md) |
| Core intents | «¿Tienes manzanas?» reads as `state_have`; «quiero algo de beber», «¿me enseñas a hacer pan?», «soy Pedro» and «Gracias, adiós.» are unknown | [game findings](game/findings.md) (W04b, W08), [core findings](nlp/findings.md) (M78) |
| Core analysis | `fue` (ir or ser) not told apart; `nos veo`; `[WORD]` takes a mis-conjugated verb; recasts drop ¿ ? | [known gaps](nlp/known-gaps.md), [core findings](nlp/findings.md) |
| Core robustness | The replay envelope's copies are not cross-checked; the WAV sample-rate range; silent content defaults; the ONNX Runtime native collision | [known gaps](nlp/known-gaps.md) |
| Fuzz | The seed family `2tpLGUOSqj41`, kept recorded by the owner's choice; it can fail a gate | [village plan](game/village-plan.md) section 5 |
| Game, "not fixed" | The session card on an early quit; saves on the game loop; no `crash.save` inside a dialogue; colour-only signals; the unread `controls.json` | [game findings](game/findings.md) (X04, X05, X08, Menu) |
| Village | The open points after the follow-ups | [village plan](game/village-plan.md) section 5 |
| Water | The open points after H12 | [water plan](game/water-plan.md) section 8 |
