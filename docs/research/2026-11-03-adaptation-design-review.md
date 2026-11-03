# Adaptation Design Document: a review against the code

A review of the owner's `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx` (revision 2, written against `530ab52d`),
asked for on 2026-11-03: "identify any key flaws, and flesh out the sections so an agent that implements this has the required details;
raise any major blocking issues or conflicts".

**How it was checked.**
- Every claim was checked against the code and content at `a37b2a74` (the docs cleanup; code and content equal `530ab52d`).
- It was also checked against the decisions D-game-1 to D-game-22, the brief, `docs/game/activities-plan.md` (the W series), the
  core-systems review, the queued handover of 2026-11-02, and all 15 images.
- A second, independent review was run: a Sonnet `Plan` agent, read only, not shown these findings. Its findings were re-checked in the
  code before they were kept; its report itself is not kept.

Nothing was built or run.

**Output.** The owner asked for the document itself to be updated, so the same `.docx` is now **revision 3**:
- every finding below is corrected or resolved in it;
- it has a "For implementers" part where a section needed one;
- section 9 specifies survival needs (D-game-24, decided after the first answers);
- section 10 is a full specification of the bread pilot;
- section 20 holds the owner's decisions.

Revision 3 renumbered the document: sections 9 to 19 of revision 2 are now 10 to 20. Section numbers below are revision 3's.

## Summary

1. **The document's facts about the code mostly hold:** about 25 claims verified; one was wrong (noun icons already exist).
2. **It authorises nothing.**
   - It defers every choice to the W plan, whose decision D-game-23 was never recorded.
   - The W series has no row in the game plan.
   - The W plan is stale (PR #20, run ids, a skip bound).
3. **The bread pilot, as written, could not meet its own success criteria.**
   - Under D-game-6's gaps, help fades over sittings the pilot did not plan for.
   - A sleep starts a sitting, so the gaps can be met in minutes.
   - Clicks have no credit path in the code.
   - Bread had no use.
4. **The world, conversation and HUD sections (6, 7, 14) had no milestones.** Two of their ideas conflict with the code or the rules:
   - smoke needs a particle system, which does not exist;
   - polish during the pilot breaks the playtest freeze.
5. **The concept art shows features that decisions rule out:** XP, CEFR locks, grid crafting, combat, a Spain flag, a female trader. The
   document did not list them for implementers.
6. **Survival needs** (hunger, thirst, health, stamina) appeared only as missing. The owner then decided to build them first, with real
   death (D-game-24).
7. **Five decisions were taken on 2026-11-03** (section 3). Twelve more wait, each with a recommendation (the document's section 20).
8. **A second review (Copilot, 2026-11-03)** read revision 3 before survival needs were added. It agreed that needs must come first.
   It also added:
   - the food economy;
   - protecting learning time at the station;
   - the time rules;
   - the pyramid's middle layers.

   Section 6 covers what each point changed.

## 1. Verified correct

Paths are under `src/Vocaterra.Game.Core/` unless they say otherwise.

- **Items and crafting:**
  - ten item kinds: ids 0, 21 and 25 to 32 (`Items/ItemDefinition.cs`); `Bread` (28) already exists;
  - stacks of 99, the axe and the book 1;
  - one recipe, two madera at the mesa;
  - `AxeSpeedUp = 4`.
- **Saves:** format 5, which reads versions 1 to 5 (`Save/SaveGame.cs:48, 54`).
- **The ladder and the credit:**
  - four `SupportLevel` rungs;
  - the English rung's no-gap rule (`Language/Phrases/PhraseProgress.cs:413`);
  - the D-game-6 gaps;
  - a noun known at three full uses (`UnitsPerUse` 6; typed input earns half).
- **The book and the record:**
  - `ReviewDeck.MaxItems = 4`;
  - `LearnerRecord.SavedEntries = 500`;
  - five book tabs;
  - the conjugation columns pres, pret, ir-a, fut and tener-que.
- **Events:** a craft logs no world event (`Session/VoxelSession.cs:352`).
- **Speech:** NPC lines are pre-rendered at two speeds, and templated lines are text-only (`Sound/NpcVoice.cs`).
- **The world and the controls:**
  - seven NPCs and ten lessons;
  - F3, F5 and `--text-scale` exist;
  - the core's learner model is saved per NPC and read by nothing;
  - no hunger, thirst, stamina or health code, no sprint, and nothing can damage the player.

## 2. Findings

| # | Finding | Evidence | Resolved in revision 3 |
| --- | --- | --- | --- |
| 1 | **Nothing authorised.** D-game-23 not recorded; no W row in the game plan's section 4 (W00 says there is); the W plan still waits for PR #20; its runs `r24` to `r30` collide with `r24-followups`; W11's `--skip-to-session 12` fails | `decisions.md`; `implementation-plan.md` section 4; `activities-plan.md` status; `scripts/scripted-runs.sh:57`; `Session/SessionSkip.cs:61` | §1 status; §2 stale list; §19 changes to the W plan; D-game-23 recorded in part |
| 2 | **Collision with decided work.** M74, M75, V29, M78 and the spoken-lines study come next; V29 declares the NPCs' pools in `game/npcs/*.json`, which W03, W04 and W08 also change; every content edit re-hashes the bundle | handover 2026-11-02, sections 2 and 6; W03, W04 and W08 | Owner's answer 1; §15 |
| 3 | **Bread had no use.** No NPC buys it, no order asks for it, eating does not exist, so "useful" and "voluntary return visits" had nothing to stand on | `game/host-events.json`; `game/objectives/` | Owner's answers 3 and 5: the stall buys it, and it feeds the player; §9, §10.8 |
| 4 | **The criteria do not fit the credit rules.** A step folds after three spaced credited acts (rung 1 to 2 at once; to 3 after 10 minutes or a new sitting; to 4 after 1 hour or a new sitting), so with one bake per sitting the first grouped instruction comes in sitting 4, and W11 plans two. A launch, a load or a sleep starts a sitting. A rung-1 act follows a highlight; a card-and-audio act is reading; the order is fixed; there is no chance baseline | `PhraseProgress.cs:413`; `Session/GuideSession.cs:255`; `activities-plan.md` section 10 and W11 | §10.6, §10.7; §15 criteria; open decisions 1 and 3 |
| 5 | **Acts have no credit path.** A click earns 0 units; a choice steps a phrase up; unregistered rows never fade; `Slips` counts production | `Language/Words/WordProgress.cs:64`; `PhraseProgress.cs:289`; `GuideSession.cs:145` | §10.6 (`RecordAct` and the counter mapping); open decision 4 (amends D-game-10) |
| 6 | **Station geometry.** Luz stands at the bakery door about six blocks from the oven; the oven is an unlabelled cobble stamp; stations are found by block kind; a `label` on the oven would make it a place NPCs answer and move the goldens | `content/game-v1/game/scene.json`; `Simulation/VoxelSimulation.cs:218` | §10.4 |
| 7 | **The voices confound the pilot.** Luz's lines are in the provisional Italian `if_sara`; none of the 1,082 speech files has been heard; the audio-only measure would test the TTS | `game/npcs/baker.json`; `village-plan.md` section 5 | Open decision 2 |
| 8 | **The playtest freeze.** No `content/game-v1` edit from the first tester to the last recall, so "world polish alongside the pilot" is impossible | `docs/game/playtest.md`, "Freeze the bundle first" | §6, §15 (U03 and U04 after the last recall) |
| 9 | **Sections 6, 7 and 14 have no milestones.** The renderer has lantern lights and `--dusk` but no particles or day-night; the sign words (Mercado, Taller, Granja) are not the taught place words and are new atlas art; the off-prompt state works for typed lines only | `src/Vocaterra.Game/Graphics/`; the scene's labels; `village-plan.md` section 3.2; handover 2026-11-02, section 3 | §6 (U03, U04), §7 (U02), §14 (U01), §15 (the U track) |
| 10 | **The station spec lacks D-game-9's states** (recording, thinking, cancel, heard/expected) **and a keyboard path to targets** | `activities-plan.md` section 4.3 | §10.4 |
| 11 | **The concept art shows ruled-out features:** XP, scores and CEFR locks; grammar rewards; a 3×3 grid; swords, armour and bosses; mines and portals; plots and biomes; permanent English; Spain-only dishes and a Spain flag; a female trader | the document's concepts 1 to 9 | §16, "Not to build from the concept art" |
| 12 | **Contradictions with the W plan, and flaws in it.** The design has staged ingredients, 4 to 5 steps, "no timing", the bowl and spoon as items, a batch craft and help that restores; the plan has consumption at the end, 6 steps with a timed wait, fixtures, one click, and help that steps up. In the plan itself, verb choices between synonyms make correct Spanish wrong, stars show a language score, and it counts "about eight" rows for nine | `activities-plan.md` sections 4.1, 4.2 and W10 | §10.3 to §10.9; §19 |
| 13 | **No survival needs.** Hunger, thirst, health and stamina appeared only as missing (meals as buffs after the pilot, D-game-23 (b)), so cooking and crafting had little purpose in the player's own loop | the document's revision 2, §2 and §14; `activities-plan.md` W15 | D-game-24; §9 (the N series), before the W series |

**Minor:**
- The book already draws an icon for every noun (`src/Vocaterra.Game/UI/Book/BookLayer.cs:666`): §8 corrected.
- The debug overlay and the dialogue's developer note show unless `--playtest`, and there is no `--dev` flag
  (`src/Vocaterra.Game/VoxelGame.cs:212`, `Dialogue/DialogueModel.cs:527`): §3 and §14 (U01).
- `WorldEvent` has no amount; objectives read `_eventAmount` (`Event/WorldEvent.cs:9`, `VoxelSession.cs:40`): §12.
- Revision 2's "W21 onward" means W22 to W29 (corrected in §15).
- The screenshots predate V10 (§2).
- "Tortilla de papas" is a flatbread in Mexico, and bol or tazón is still open (§10, scope control).
- The imperative's "level policy" was framed as a blocker, but the gate checks lemmas; tense rules apply only to taught forms
  (`Language/Lessons/LessonBook.cs:102`): the owner's answer 4.
- Stale paths since the docs cleanup, and two literal paths to the renamed core-systems docx: the document's §19 is updated, and
  `activities-plan.md` and `2026-11-01-core-systems-review.md` are repaired.

## 3. The owner's answers (2026-11-03)

Recorded in `docs/game/decisions.md`:

1. **Order:** the queued follow-ups first (M74, M75, V29, M78 and the spoken-lines study), then the W series (D-game-23). Answer 5
   later put survival needs between them.
2. **The station:** an overlay panel over the live world for the pilot (D-game-23).
3. **Bread:** the stall buys it for one coin (D-game-23).
4. **The imperative:** heard only in the pilot, not taught; W09 leaves the pilot's path (D-game-23).
5. **Survival needs first (D-game-24).** The owner asked whether the document covered hunger, thirst, health and stamina ("those basic
   needs need to be implemented first, otherwise cooking and crafting aren't of much use"), then chose:
   - real death with a cost;
   - meters that pause in conversations, lessons, the book and every panel;
   - needs from session 1;
   - stamina spent by gathering and a new sprint.

   This amends the brief's guardrail (D-game-8: "no damage or countdown"; killing timers "left for later") and replaces D-game-23 (b).
   The document keeps the brief's intent: a language mistake never kills, because every need has a route without Spanish.

## 4. Open decisions

In the document's section 20, each with a recommendation:
1. the pilot protocol;
2. a listening gate for Luz's lines;
3. the fold rule;
4. act evidence (amends D-game-10);
5. the recipe's changes;
6. session 11 at `a2`, with flour at the stall;
7. a presentation track, U01 to U04;
8. the rest of D-game-23;
9. combat left out;
10. the cost of death;
11. Spanish for needs (a core intent for "tengo hambre");
12. the pyramid's middle layers: N04 (food that grows back, fair gifts) and N05 (cooking at the fire), then a needs check.

## 5. Follow-ups for the orchestrator

- Revise `docs/game/activities-plan.md` as the document's section 19 lists:
  - needs first;
  - save version 6 moves to N01;
  - W15 shrinks;
  - runs from r27 for the bread.
- Write the N plan (N01 to N05 and the needs check, the document's section 9).
- Once the owner accepts decision 7, write the U plan.
- Add the N, W and U rows to `docs/game/implementation-plan.md` section 4.
- Record the D-game-10 amendment if the owner accepts decision 4.
- After the docs pull requests merge, the queued follow-ups of `docs/handovers/2026-11-02-after-followups.md` come first (the owner's
  order).
- If the owner wants one, add a Markdown transcription of revision 3 for agents, as `prototype-brief.md` is for the brief.

## 6. A second review (Copilot, 2026-11-03)

The owner passed on a review by GitHub Copilot of revision 3, written before survival needs were added (its section numbers are the
old ones), with: "these fundamental needs should come first, they form the basis of our pyramid of needs and drive the player to
interact with cooking / other systems". Each point was checked against the code and the document.

| Copilot's point | Checked | In revision 3 now |
| --- | --- | --- |
| The order contradicts the loop: needs must precede cooking | True of the version it read | Needs are section 9 and phase B; section 1's pyramid of needs sets the order |
| Selling bread is a weak substitute for usefulness: two loaves sell for 2 coins, while the flour costs 1 and a wood sells for 1; free bread and free ingredients need balancing | Confirmed: selling is break-even. Nothing in the code limits the stall's and the villager's free bread, and berry bushes never grow back (no regrowth code) | Bread's value is food: a loaf fills 25 hunger and heals 5, and only cooked food heals. Baked bread is a quarter of Luz's price in coins. The food-value table (§9.3). N04: bushes grow back after a sleep, and gifts come once per in-game day. `give_missing` once per in-game day (§10.8) |
| The four meters need mechanics, not bars | Already specified (§9.2 to §9.4) after the version it read | Added: thresholds, damage only from empty hunger or thirst, rest at a fire, healing from cooked food |
| Learning time needs protection: the station kept the world running | The meters already stopped in every panel (§9.2), but §10.4 read otherwise | §10.4 now says the world runs while the meters stop; speech recording and processing are named |
| Missing details: trip frequency, thresholds, sleep, pause, save, load and offline rules, consumables, HUD, determinism, migration, preparation measures | Partly covered | Design targets and the time rules (§9.2); consumables (§9.3); a preparation measure (§15) |
| The sequence: needs → eating, drinking and resting → useful cooking → procedural cooking → farming and repair | Sound; it fits the owner's pyramid | Adopted as the pyramid (§1): N04, N05 and the needs check before the bread pilot (§9.9, §15); decision 12 |
| The brief's scope (no damage, no countdown) should be revised explicitly | True | D-game-24; an amendment note on `docs/game/design/prototype-brief.md` |
