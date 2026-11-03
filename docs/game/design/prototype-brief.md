Source: [`sources/Vocaterra_Prototype_Design_Brief.docx`](sources/Vocaterra_Prototype_Design_Brief.docx) (v1.2, the owner's). The three concept images (references A, B, C) stay inside the docx.

PROTOTYPE DESIGN BRIEF / V1.2

# VOCATERRA

*A Minecraft-inspired survival slice for learning practical spoken Spanish*

*Visual concept reference only. The prototype should use an original, consistent voxel art direction.*

## Purpose

Build one compact, end-to-end playable scenario to test Vocaterra’s central value before investing in procedural generation, extensive survival systems or an adaptive language platform. The prototype must feel like a small game, not a language quiz in a 3D scene.

> **Provisional product promise**
>
> Vocaterra helps beginners learn to understand and use practical spoken Spanish by making language a useful tool for exploring, gathering, crafting and interacting with the world.

> **The prototype question**
>
> Can a short survival-and-exploration objective make beginners meaningfully practise spoken Spanish, while still feeling like playing a game?

Target experience: 10–15 minutes • one handcrafted forest-to-village route • one simple craft • 2–3 NPCs • one language-dependent objective

## DESIGN REVIEW / 01

### Key risks and possible responses

These are the main failure modes identified so far. “Prototype default” is the recommended starting choice; the alternatives are options to test, not features to build all at once.

| ISSUE | WHY IT MATTERS | POSSIBLE SOLUTIONS / PROTOTYPE DEFAULT |
| --- | --- | --- |
| 1. Language becomes a tax on gameplay<br>CRITICAL | Prompts after every pickup or action turn play into homework; completely optional practice may be ignored. | Options: Tie Spanish to meaningful decisions, information, trade and rewards; keep routine actions immediate.<br>Prototype default: Practise at a few authored decision points. Do not ask players to name every tree, rock or resource. |
| 2. Beginners are punished for not knowing Spanish<br>CRITICAL | A failed phrase could block food, water or essential progress, mixing language difficulty with survival stress. | Options: Hints, replay, example phrases, retries, fail-forward outcomes and non-verbal fallback.<br>Prototype default: No starvation, death or permanent loss from language mistakes. A failed attempt always has a recovery path. |
| 3. Recognition is mistaken for mastery<br>CRITICAL | Matching an image to a word does not prove listening comprehension, unaided recall or flexible use. | Options: Track recognition, listening, supported production, unaided recall and transfer separately. Add an “I don’t know / help” path; multiple-choice is only a recognition signal.<br>Prototype default: Track a handful of phrases through supported use and later unaided use. Do not build a universal mastery model yet. |
| 4. Speech recognition gives misleading feedback<br>HIGH | Noise, accent, microphone quality and ASR errors can make a reasonable attempt appear wrong. Transcript confidence is not pronunciation quality. | Options: Provide a mic check, clear recording/processing state, cancel/retry, a timeout, persistent “heard vs expected” feedback, replay and slower reference audio, plus text/button fallback.<br>Prototype default: Use ASR only for intent in a constrained phrase set. Uncertain recognition prompts help/retry; it never counts as a language failure. |
| 5. Open-ended NPC dialogue is hard to control<br>HIGH | Unrestricted conversations can drift beyond the learner’s level, behave inconsistently or make success difficult to assess. | Options: Use goal-oriented conversation, authored response variants, several valid answers and optional scaffolding. Expand freedom later.<br>Prototype default: Use a small dialogue graph and deterministic fallbacks. No unrestricted AI chat in this prototype. |

> **Design rule**
>
> Language should help the player make progress; it should not be a toll on every action. Keep basic survival forgiving, and make communication valuable through information, trade and optional advantages.

## DESIGN REVIEW / 02

### Key risks and possible responses

| ISSUE | WHY IT MATTERS | POSSIBLE SOLUTIONS / PROTOTYPE DEFAULT |
| --- | --- | --- |
| 6. The system tracks the wrong progress<br>HIGH | Word counts and automatic A1–C2 labels may exaggerate ability. CEFR describes broader communicative capabilities, not a simple list of word difficulty. | Options: Use concrete “can do” outcomes; measure independent communication; keep CEFR as a future curriculum reference.<br>Prototype default: Hand-author and test one beginner scenario. Defer automated CEFR placement and global proficiency scores. |
| 7. The curriculum teaches irrelevant Spanish<br>HIGH | Gathering and crafting naturally foreground materials, tools and fantasy nouns, while everyday conversation may be neglected. | Options: Prioritise common real-life functions and vocabulary; define the Spanish variety being taught; accept valid, reviewed alternatives. Keep specialist game vocabulary secondary.<br>Prototype default: Use a small set of practical phrases for requesting supplies and finding a location. Do not derive the curriculum from whatever objects happen to spawn. |
| 8. Game and learning compete<br>HIGH | A full sandbox and a full language platform are both large projects; one may become a thin wrapper for the other. | Options: Build one vertical slice that tests one objective, one location, a few interactions and a transfer moment. Fake or omit anything else.<br>Prototype default: Prototype enough gathering/crafting to feel game-like, but defer procedural worlds, combat and broad progression. |
| 9. Repetition is tedious or easy to game<br>MEDIUM | Repeating an identical line can become boring and reward prompt memorisation rather than flexible communication. | Options: Mix listening and speaking, revisit after an intervening activity, vary speaker/context, allow “I don’t know”, and gradually remove prompts. Avoid arbitrary daily caps.<br>Prototype default: Teach a phrase with support, revisit it later, then test a changed context without showing the answer. A guessed multiple-choice answer is not mastery. |
| 10. Unverified content teaches errors<br>HIGH | Beginners cannot reliably spot incorrect Spanish, translations or audio/text mismatches. Errors quickly undermine trust in the entire learning system. | Options: Review target phrases, translations, accepted responses and audio together with a competent Spanish speaker. Specify regional assumptions; use automated checks as a supplement, not approval.<br>Prototype default: No unreviewed live-generated lesson content. Keep a small source-of-truth phrase sheet and verify each line before it enters the build. |

## COMPARABLE GAME REVIEW / DESIGN IMPLICATIONS

### What Noun Town reviews suggest

The premise resonates, but execution is fragile. In the supplied summaries, reviewers praise learning nouns through 3D objects, speaking aloud, a cosy low-pressure loop and visible progress. Approximate theme counts include ~290 positive reviews mentioning vocabulary/nouns and ~155 mentioning speaking/pronunciation; speech/microphone issues also recur in ~63 negative and ~120 positive reviews. Beginner support, content accuracy and rough edges are recurring concerns too.

> **Audience decision:** Vocaterra currently promises to help beginners. Test with people who know little or no Spanish; success among learners who already know basic phrases does not validate the beginner-first promise.

### Signals to carry into the prototype

| WHAT REVIEWS SIGNAL | VOCATERRA RESPONSE |
| --- | --- |
| Spatial context helps vocabulary | Pair a useful word with a visible object, audio and a real action. Organise a small set by place/activity, then revisit it when crafting, trading or navigating. Treat colour as discovery/progress feedback, never as proof of mastery or a penalty for forgetting. |
| Voice is a critical dependency | Provide a one-time mic check, visible recording/processing states, cancel/retry and timeout, persistent “what we heard vs what we expected”, repeat/slower audio and text/button fallback. A voice failure must not block the objective. |
| Beginners need a bridge to sentences | Teach key words and phrase chunks before full NPC exchanges. Keep utterances short, allow optional word-level meaning/help, then gradually remove scaffolding. Do not start with unrestricted native-speed conversation. |
| Useful and accurate Spanish beats volume | Prioritise everyday functions: request, answer, ask directions and clarify. Choose the Spanish variety for the prototype and accept reviewed alternatives. Check written Spanish, English meaning and spoken recording together with a competent reviewer. |
| Recognition is not independent recall | Offer “I don’t know / help”, a model answer and retry without shame. Revisit the phrase after another activity, then test a changed context without showing the answer. Multiple choice, colour or quest completion alone is not mastery. |
| Basic polish protects trust | Keep controls and tutorial clear, settings stable, saving reliable and every interaction recoverable. Avoid arbitrary daily caps and extra minigames; for this prototype, a small number of dependable interactions is more informative than breadth. |

Prototype defaults: Hand-authored dialogue; one defined Spanish variety; a compact phrase set; validated text/audio; recoverable voice failures; one delayed-recall or transfer check. Design guardrails: practise at meaningful decision points, not every pickup; scaffold then fade support; colour, completion and ASR do not prove mastery; no language/voice failure can trap progression. Defer production adaptive learning and unrestricted NPC AI.

Evidence note: Based on user-provided Steam review summaries for Noun Town (app 2313720): 648 positive reviews dated 2 Nov 2023–4 Oct 2026 and 115 negative reviews dated 2 Nov 2023–13 Oct 2026. Theme counts are approximate keyword-pass counts; reviews are self-selected. Use these as design-risk signals, not proof of learning efficacy or a representative market study.

## END-TO-END PLAYABLE SLICE

### Prototype scenario: gather, craft, ask, explore

Target first-run duration: 10–15 minutes. The player needs to prepare a simple campsite and obtain water. Gathering and crafting establish the survival-game feel; Spanish helps the player get supplies and locate the river. There is no ticking clock or survival penalty for language mistakes.

| Step | Moment | What happens | Design note |
| --- | --- | --- | --- |
| 1 | Start in the forest | Show the campsite goal and a small route toward the village. Let players move and inspect the world before introducing speech. | Exploration should feel uninterrupted; no quiz at spawn. |
| 2 | Gather resources | Collect a small amount of wood and berries. Introduce a few useful words in context, paired with clear audio and the written form; revisit them during crafting or trading. | Spatial context supports discovery, but pickup is never gated by a vocabulary quiz. Group words by place/activity and reuse them later. |
| 3 | Craft one useful item | Use a simple workbench recipe to create a torch or basic camp item. Keep the recipe short and inventory minimal. | Establish gathering → crafting without building a crafting tree. |
| 4 | Ask for supplies | At the village stall, teach the key words before the full exchange. The shopkeeper asks “¿Qué necesitas?” The player can answer “Quiero agua, por favor.” | Offer phrase replay, slower audio and an optional word-level hint. Accept reviewed valid variants; if ASR is uncertain, show what it heard and allow retry or fallback. |
| 5 | Ask for directions | The player asks “¿Dónde está el río?” A villager provides a short answer or points toward a visible route. The player follows it to the river and fills a bottle. | Spanish unlocks useful information and directs a real in-game action. |
| 6 | Complete and transfer | After the objective, revisit one phrase without the original prompt or with a different NPC/context. If feasible, check recall again in a later session. | Measure independent recall and transfer, not completion, colour restoration or a successful transcript alone. |

### Initial target phrases

| SPANISH | INTENDED MEANING |
| --- | --- |
| ¿Qué necesitas? | What do you need? |
| Quiero agua, por favor. | I would like water, please. |
| ¿Quieres pan? | Would you like bread? |
| Sí, por favor. / No, gracias. | Yes, please. / No, thank you. |
| ¿Dónde está el río? | Where is the river? |

These are prototype examples, not a final curriculum. Have a Spanish-language reviewer check naturalness, learner level and regional assumptions before testing.

## REFERENCE MOODBOARD / GENERATED CONCEPT ART

### Graphics and visual direction

Use a Minecraft-inspired voxel/block-based presentation as the readability and production-efficiency baseline, but give Vocaterra its own identity. Minecraft is a visual reference, not a source for its assets or exact UI. These generated images are concept references, not target screenshots or final assets.

Reference A — world shape, colour, scale and sense of exploration.

Reference B — village interaction: NPCs and short contextual dialogue.

Reference C — survival interaction: simple crafting and readable HUD.


### Art-direction rules for the prototype

- Blocky silhouettes and a consistent voxel scale across terrain, props, resources and characters.
- Bright, saturated natural colours: greens for vegetation, clear blues for sky/water, warm wood and stone accents; lighting should keep key objects legible.
- Slightly more expressive characters and environmental detail than default Minecraft, without increasing modelling scope unnecessarily.
- Use a small, distinctive set: forest clearing, path, village stall, a few houses, riverbank and workbench. No extra biomes are required.
- Make interactions readable through subtle highlights, simple icons and short dialogue panels; support replay/hints without filling the world with labels. Colour may signal discovery or progress, but objects should not fade simply because a word has not been practised.

## BUILD ONLY WHAT TESTS THE HYPOTHESIS

### Prototype scope and validation

The prototype should feel survival-like through gathering, one craft, a minimal inventory and a useful objective. It does not need to simulate a complete survival sandbox.

| INCLUDE IN THE PROTOTYPE | LEAVE FOR LATER |
| --- | --- |
| ✓ Small voxel-style outdoor scene: forest, path, village stall and river. | – Procedural terrain, multiple biomes or a large open world. |
| ✓ Movement, interactions and a few resource pickups (e.g. wood and berries). | – Complex crafting chains, large inventory, equipment or loot progression. |
| ✓ One simple crafting recipe and minimal inventory/item use. | – Combat, bosses, base-building or destructive terrain editing. |
| ✓ One soft survival objective: get supplies and refill water. No damage or countdown. | – Hunger/thirst timers that can kill or permanently punish the player. |
| ✓ Two or three NPCs with a constrained dialogue flow and several valid responses. | – Unrestricted LLM conversations with every NPC. |
| ✓ Speech capture for a constrained phrase set: mic check, visible state, cancel/retry, heard-vs-expected feedback and text/button fallback. | – Unrestricted AI dialogue, perfect accent scoring or a speech service with no fallback. |
| ✓ Minimal telemetry: support used, ASR latency/failures, response, delayed recall and transfer. | – World-wide grayscale/fading tied to presumed mastery. |

### What to test

- **Gameplay hypothesis**
  - Is the gathering/crafting/exploration loop enjoyable, and does Spanish feel like part of the objective rather than a detached quiz?
  - Measure: Observe completion, friction, retries, player comments and desire to continue.
- **Learning hypothesis**
  - Can players understand and produce useful phrases after support is reduced?
  - Measure: Use a short pre-check, immediate post-check and a later recall check.
- **Transfer hypothesis**
  - Can players use a phrase with a different speaker or in a changed situation?
  - Measure: Test comprehension and unaided production; do not score only exact scripted strings.

### Decision gates

| Gate | Criterion |
| --- | --- |
| Playable | Players finish without developer rescue; ASR failure never traps progress. |
| Enjoyable | Players understand why they needed Spanish and show interest in another scenario. |
| Learning signal | Players independently recall or understand several phrases after support is reduced. |
| Transfer | At least some target language works with a different NPC or changed context; a next-day check is preferable. |
| Content trust | Every prototype phrase, translation, accepted response and spoken recording is checked together before testing. |

A small pilot gives directional evidence, not statistically strong proof of learning efficacy. If the game is fun but recall/transfer is weak, improve the learning loop before expanding the world. If learning works but feels tedious, change how practice is integrated into objectives. Test beginner comprehension, voice reliability and independent transfer separately; do not treat transcription accuracy as learning.
