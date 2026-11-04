"""The generator of the current Vocaterra Adaptation Design Document
(docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx).

It rebuilds the document's word/document.xml from revision 2's, kept in git at commit 4b312545. Every edit is anchored to an
element of revision 2's body (its index) and checked against the start of that element's text:
- expect(i, prefix) checks an anchor; set_text(i, prefix, text) rewrites a paragraph in its own formatting;
- replace[i] = xml swaps an element ('' deletes it); put_after(i, *xmls) inserts after it, in call order;
- the builders in xmlkit.py (h1, h2, lead, p, bullet, num, table, callout, code, spacer, page_break) write the document's own
  direct formatting, and their text takes **bold** and `code` markup;
- at assembly, number_subheadings() numbers every Heading2 "N.k" under its Heading1 and stops if a written number is out of order.
Conventions: cross-references say "section N" or "N.k"; owner decisions go in section 18 (and in docs/game/decisions.md); the document states the intended design and open decisions, without revision history or review transcripts.

Usage: tools/adaptation-design/rebuild.sh [out-dir], or python3 -I build_rev3.py <revision 2's document.xml> <output document.xml>
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from split import body_elements  # noqa: E402
from xmlkit import (INK, bullet, callout, code, h1, h2, lead, num, p, page_break, runs,  # noqa: E402
                    spacer, table)

SRC, DST = sys.argv[1], sys.argv[2]
x = open(SRC, encoding='utf-8').read()
pre, els, suf = body_elements(x)
texts = [e[4] for e in els]
replace, after = {}, {}


def expect(i, prefix):
    assert texts[i].startswith(prefix), (i, texts[i][:90], prefix)


def put_after(i, *xmls):
    after.setdefault(i, []).extend(xmls)


def keep_format(i, text):
    """The paragraph at i with new text, keeping its pPr and its main run's colour, size, bold and italics."""
    orig = els[i][3]
    m = re.search(r'<w:pPr>.*?</w:pPr>', orig)
    ppr_xml = m.group(0) if m else ''
    rprs = re.findall(r'<w:rPr>(.*?)</w:rPr>', orig)
    main = rprs[-1] if rprs else ''
    color = (re.search(r'w:color w:val="([0-9A-F]+)"', main) or [None, INK])[1]
    sz = int((re.search(r'w:sz w:val="(\d+)"', main) or [None, '20'])[1])
    return '<w:p>' + ppr_xml + runs(text, color, sz, bold='<w:b/>' in main, italic='<w:i/>' in main) + '</w:p>'


def set_text(i, prefix, text):
    expect(i, prefix)
    replace[i] = keep_format(i, text)


# ---------------------------------------------------------------- cover

expect(4, 'INTENT')
replace[4] = callout('INTENT', [
    'Define how player needs, useful activities and fading language support work together in Vocaterra. This document specifies the player experience, implementation requirements and playtests for the needs system and the bread pilot.'], 'gold')

set_text(6, 'Code review: 3 November 2026',
         'Design specification • 4 November 2026 • Implementation baseline: main at 14fb2d63 • Confirmed direction and open decisions: section 18.')

# ---------------------------------------------------------------- 1. Executive direction

expect(11, 'CORE DESIGN MOVE')
replace[11] = callout('CORE DESIGN MOVE', [
    'Player needs create reasons to gather, cook, trade and rest. Lessons introduce the language; repeatable activities put it to use; evidence of understanding reduces support. Build needs and fire cooking, prove the bread procedure, then extend the model to other activities.'], 'sand')
expect(20, 'Adapt')
put_after(20, bullet('Add player needs first (hunger, thirst and sleep in stages that grow worse, pushing health and stamina): the base that makes food, water, rest and tools '
                     'matter, and that sends the player to cooking and the other systems (section 9).'))

expect(12, '')
put_after(12,
          h2('Status: what may be built now'),
          p('Register each N, W and U milestone in `docs/game/implementation-plan.md` section 4 and resolve its required decisions before implementation. Section 18 distinguishes confirmed direction from recommendations; a detailed proposal is not approval to build it.'),
          p('**Order of work:** queued language follow-ups (M74, M75, V29, M78 and the spoken-lines study), then the N series, then the W pilot. The proposed presentation work U01 and U02 fits between needs and the pilot (decision 7). Section 15 gives the dependencies and acceptance gates.'),
          callout('BUILD ORDER', [
              'Player needs and consumption (N01 to N05) → renewable food and daily gifts (N06) → cooking at the fire (N07) → the needs playtest → bread with Luz (the W pilot) → repair (W14) and farming (W18).',
              'Each phase gives the next a purpose. Better food, tools and reliable supplies reduce the time spent meeting basic needs. Building, exploring and talking also offer goals of their own.'], 'gold'),
          spacer())

set_text(28, 'Use the concept images',
         'Use the concept images for warmth, density, composition and readable interactions. First person is the default, with an optional shoulder camera (F5). Section 4.3 defines the scope that governs the images.')

# ---------------------------------------------------------------- 2. Current game

set_text(31, 'Baseline:',
         'Implementation baseline: `main` at 14fb2d63. Source and bundle data establish implemented behaviour. The screenshots illustrate layout and visual style; the requirements in this document define the intended experience.')

set_text(40, '“Survival adventure” is the long-term setting',
         'Player needs begin in session 1 and make food, water and rest useful (D-game-24; section 9). Players can meet every need without Spanish, and the meters pause during conversations, lessons and panels.')
set_text(41, 'These findings are from the code',
         'The implementation facts below identify the foundations and interfaces used by the adaptation.')

FACTS = [
    ['Item kinds', 'Ten: Wood 0, Torch 21, Berry 25, Bottle 26, WaterBottle 27, Bread 28, Book 29, Coin 30, Apple 31, Axe 32. A new '
     'item takes the next free number (33 for the first); a number is never reused.', 'Game.Core `Items/ItemDefinition.cs`'],
    ['Where an item name is listed', 'The enum and its table; the item enums of four schemas (nouns, host-events, session, '
     'objective); the atlas icon; the held sprite; the scripted runs’ names.',
     '`content/schema/*.schema.json`; Game `Graphics/Blocks/BlockAtlasLayout.cs:391`, `Graphics/Figures/HeldArm.cs:57`; Game.Core '
     '`Items/ScriptItemNames.cs`'],
    ['Saves', 'Format 5; reads 1 to 5; items stored by enum name.', 'Game.Core `Save/SaveGame.cs:48, 54`'],
    ['Support ladder', 'EnglishAndCard → CardAndAudio at once; after that a credited use steps down once its gap has passed: 10 '
     'minutes after the first credited use, 1 hour after later ones, or a new sitting.',
     'Game.Core `Language/Phrases/PhraseProgress.cs:26, 413`'],
    ['Sittings', 'A launch, a load or a sleep starts a sitting, and a use in a new sitting meets any gap. The day (lessons, orders) '
     'moves only by sleeping.', 'Game.Core `Session/GuideSession.cs:255`'],
    ['Credit units', '6 per spoken use and 3 per typed (the default); 0 for any other input, so a click earns nothing. A choice, a '
     'fail-forward and “no sé” step a phrase up one rung.',
     'Game.Core `Language/Words/WordProgress.cs:64`; `PhraseProgress.cs:289, 454`'],
    ['Which phrases fade', 'Only the NPCs’ scaffold phrases and the lessons’ taught phrases; any other row stays at None.',
     'Game.Core `Session/GuideSession.cs:145`'],
    ['Lesson tenses', 'a1: pres, pret, ir-a. a2 adds fut and tener-que. No imperative.',
     'Game.Core `Language/Lessons/LessonBook.cs:102`'],
    ['Level lists', 'Lemmas only: `a1.txt` (with its world-words block) and `a2.txt`. harina, bol, tazón and cuchara are not in the '
     'lexicon; masa, echar and mezclar are in it but in neither list; sacar is A2; poner, hacer, madera, horno, pan and agua are A1.',
     '`content/game-v1/cefr/`, `content/game-v1/lexicon/`'],
    ['World events', 'A name, an NPC and a detail, but no amount (objectives read the private `_eventAmount`); a craft logs none.',
     'Game.Core `Event/WorldEvent.cs:9`; `Session/VoxelSession.cs:40, 352`'],
    ['Stations and the oven', 'A station is a block kind within reach (the workbench). The oven is an unlabelled cobble stamp with a '
     'scenery torch; Luz stands at the bakery door, about six blocks from it.',
     'Game.Core `Simulation/VoxelSimulation.cs:218`; `content/game-v1/game/scene.json`'],
    ['Trade', 'One fixed `sell` and one fixed `buy` per NPC (Luz sells bread for 2 and buys wood for 1). An exchange that cannot '
     'happen answers `<npc>.cant_pay`, `cant_sell`, `wont_buy` or `no_room`; `instead` sends a request for a noun to another host '
     'event (V21b).', '`content/game-v1/game/host-events.json`; Game.Core `Npc/NpcHostEvents.cs`'],
    ['Voices', 'Luz `if_sara` and Domingo `im_nicola` (Italian), León `pm_santa` (Portuguese), Mar and the villager `em_santa`, '
     'Luna `ef_dora`, the shopkeeper `em_alex`. The owner has not yet listened to any of the 1,082 speech files.',
     '`content/game-v1/game/npcs/*.json`; `docs/game/village-plan.md` section 5'],
    ['Debug text', 'The debug overlay and the dialogue’s developer note (confidence, verdict, milliseconds) show in every run '
     'except `--playtest`; there is no `--dev` flag.', 'Game `VoxelGame.cs:212`; Game.Core `Dialogue/DialogueModel.cs:50, 527`'],
    ['Runs and skips', 'The last scripted run is `r24-followups`; `--skip-to-session` goes only to a session that has a lesson; '
     '`--sleep` exists.', '`scripts/scripted-runs.sh:57`; Game.Core `Session/SessionSkip.cs:61`'],
    ['Playtest freeze', 'No edit of `content/game-v1` from the first tester to the last next-day recall.',
     '`docs/game/playtest.md` (“Freeze the bundle first”)'],
    ['Renderer', 'Lantern lights and a fixed `--dusk`; no particles, no day-night cycle; NPCs stand still.', 'Game `Graphics/`'],
    ['Needs', 'None: no hunger, thirst, stamina or health, no sprint, and nothing can hurt the player.', 'Game.Core (no such code)'],
    ['Interface scale', '`--text-scale`; layouts are tested on 1280×720 and 800×600 at 100, 125 and 150 %.',
     'Game `UI/Hud/HudLayout.cs:9`'],
]
put_after(41,
          h2('Facts an implementer needs'),
          p('Game.Core paths are under `src/Vocaterra.Game.Core/`; Game paths are under `src/Vocaterra.Game/`.'),
          table([2000, 5368, 3000], ['Fact', 'At the baseline', 'Where'], FACTS),
          spacer())

# ---------------------------------------------------------------- 3. Current learning UX

expect(56, '')
put_after(56,
          h2('Presentation requirements'),
          bullet('Show one speak/type hint in the conversation footer. Integrate lesson progress into the panel header, using the same unit count as the book.'),
          bullet('Keep the support card, heard/expected feedback, recovery buttons, word-card audio and noun icons visible when needed.'),
          bullet('Describe learning progress in plain language. Keep detailed counters and technical diagnostics in expandable views.'),
          p('Sections 7 and 14 specify the conversation layout and ordinary-play defaults.'))

# ---------------------------------------------------------------- 4. Target experience

expect(64, '')
put_after(64,
          h2('How the bread pilot shows each feeling'),
          table([2600, 7768], ['Feeling', 'Evidence in the pilot'], [
              ['Readable', 'After the lesson, testers find the oven and the stall from the square without the beacon; no tester asks '
               'what to do at the station after the first bake.'],
              ['Warm', 'Testers’ own ratings and comments on the bakery; no debug text in ordinary play (U01).'],
              ['Useful', 'The bread feeds the player and sells at the stall, and the coins buy flour; testers bake when hungry and sell without '
               'being told.'],
              ['Repetitive without feeling repetitive', 'Testers bake more than the protocol asks; the first two instructions come in a '
               'different order each bake; enjoyment holds from the first sitting to the third.'],
              ['Progressively independent', 'The share of instructions acted on right first time without help at audio only rises '
               'from sitting 2 to sitting 3, and the first grouped instruction (“Haz la masa”) appears in sitting 2.'],
          ]),
          spacer())

# ---------------------------------------------------------------- 5. The adaptation principle

expect(81, '6.  Reintroduce')
put_after(81,
          h2('How the six steps are built'),
          table([2200, 5568, 2600], ['Step', 'Mechanism', 'Status'], [
              ['1. Introduce', 'A lesson (`game/lessons/*.json`, `LessonRunner`): session 11, “En la panadería”, at Luz.',
               'Built (lessons); planned (W10)'],
              ['2. Act with support', 'The station shows each instruction at its row’s rung (`SupportLevel`); the highlight comes after '
               'a slip or on help (section 10.10).', 'Proposed (W06, W07; section 10)'],
              ['3. Repeat in the same system', 'The recipe is baked again for its reward: bread to eat, or to sell at the stall.',
               'Planned (W06); decided (the stall)'],
              ['4. Reuse elsewhere', '“Quiero harina” and “Vendo pan” at the stall; wood gathered for the oven; “pon … en …” across '
               'steps.', 'Proposed (section 10)'],
              ['5. Reduce support', 'Instruction rows fade by acts (`RecordAct`); steps fold into chunks; the goal replaces the chunks.',
               'Planned (W06), with section 10.10’s rules'],
              ['6. Reintroduce weak language', 'For instruction rows, Luz offers the guided bake when the goal row is due (“¿Hacemos '
               'pan juntos?”). The review deck asks only learner phrases with a prompt, so it does not serve them.',
               'Proposed (W06; guided review)'],
          ]),
          spacer())

# ---------------------------------------------------------------- 6. World and settlement

set_text(83, 'Keep the terrain generator',
         'Keep the terrain generator and improve destinations already in the scene. A world edit changes `content/game-v1`, which the '
         'pilot freezes from the first tester to the last recall, so world work lands before the freeze or after it (section 15).')
set_text(89, 'Use signs and environmental labels',
         'Use signs sparingly, with the scene labels the game teaches and the beacon resolves: panadería, herrería, tienda, campo, pozo and torre. The signpost uses tienda, río and montaña. Each additional sign word requires its own atlas art (`BlockCell.Sign*`).')
set_text(91, 'Use paths, fences, lamps',
         'Use paths, fences, lamps, crops and sound beds to bind the settlement together. Smoke needs a particle layer the renderer '
         'does not have (section 14).')
expect(95, 'Use the existing river')
put_after(95,
          h2('For implementers: U03 and U04 (proposed, after the pilot’s last recall)'),
          bullet('**U03, a readable village (content and Game).** Signs as above: a sign prop or stamp with atlas cells per word, and a '
                 'validator rule that a sign’s word is a scene label. Profession stamps from existing block kinds where possible: sacks '
                 'and a bread table at the bakery, a tool rack at the smithy, a cart by the field. Everything inside the levelled village '
                 'disc (radius 24); no new terrain, site, road, or building outside the disc.'),
          bullet('**No labels on new props.** A labelled prop becomes a place the NPCs answer, which moves the `places-village` answer '
                 'set and the goldens.'),
          bullet('**U03’s acceptance:** every route stays reachable; a sightline test from the square to each sign; the world digest '
                 'and the scene tests re-recorded; saves of versions 1 to 5 load with their chunk edits; the seed-42 build-time budget '
                 'holds; `NamePlatesTests` and the talk range unchanged; screenshots from the square at 100, 125 and 150 %.'),
          bullet('**U04, light (Game).** Window glow (an emissive window block, or lantern light at windows) visible under `--dusk`; '
                 'warm light at the oven and the forge from the lantern lights that exist. Smoke only if the owner approves a '
                 'particle-layer milestone; NPC routines and work animations are later.'),
          bullet('**Rules that hold:** D-game-5 (CC0 or drawn in code), D-game-14 (Vocaterra’s own identity; no reference image '
                 'copied) and D-game-18 (no music).'))

# ---------------------------------------------------------------- 7. NPC conversations

expect(103, '')
U02 = [
    ['Debug overlay (frame rate, tick, seed, position, target)', 'hidden', '`--dev` or F3', '`VoxelGame.cs:212`; `UI/Hud/VoxelDebugText.cs`'],
    ['Developer note (“heard at 100 %, … ms”)', 'hidden', '`--dev` or F3; scripted runs that read it pass `--dev`',
     'Game.Core `Dialogue/DialogueModel.cs:50, 527`'],
    ['The NPC’s current line, with its English at the phrase’s rung', 'shown', 'always', '`UI/Dialogue/DialoguePanel.cs`, `DialogueLayout.cs`'],
    ['The learner’s last line and the outcome', 'shown', 'after a turn', '`DialogueModel.cs`'],
    ['Heard / expected line', 'shown', 'after every spoken turn, until the next turn (D-game-9)', '`DialogueModel.cs:104`'],
    ['One coaching line', 'shown', 'uncertain: ask again (the microphone, not your Spanish); not understood; off the prompt (typed '
     'lines only); a slip: one recast', 'Game.Core `Dialogue/TurnPresenter.cs`; the feedback text'],
    ['Older lines', 'collapsed', 'an expand key (Tab), and the book’s “What I said”', '`DialogueModel.MaxLines` (60)'],
    ['Lesson strip', 'in the panel header: “Lección · Primeras palabras · unidad 1 de 7 · Dilo”', 'while the panel is open; one HUD '
     'line during a `do` step', '`UI/Lesson/LessonStripLayout.cs`, `LessonLayer.cs`; `ui.stepOf`'],
    ['Speech buttons (Repetir, Más despacio, Ayuda, No sé, Escribir)', 'shown', 'always in the panel; each also on its key',
     'Game.Core `Dialogue/SpeechButtons.cs`'],
    ['Support card (“Puedes decir”)', 'at its rung', 'as today', '`UI/Dialogue/SupportLayer.cs`'],
    ['The speak and type hint', 'once', 'in the panel’s footer; `tip.speak` and the strip stop repeating it after the first conversation',
     '`content/locale/en/tips.json`, `ui.json`'],
    ['Recording, thinking, cancel and timeout states', 'shown', 'as today', 'Game.Core `Dialogue/DialogueScreen.cs`'],
]
put_after(103,
          p('The off-prompt state exists for typed lines only. At a constrained prompt the recognizer decodes against the answer set, '
            'so a spoken line off the prompt reads as the nearest answer or as not understood until the spoken-lines study (handover '
            '2026-11-02, section 3) lands.'),
          h2('For implementers: U02, the conversation surface (proposed)'),
          table([2700, 1700, 3268, 2700], ['Element', 'Ordinary play', 'When and how', 'Code'], U02),
          spacer(),
          p('**Acceptance.** Every D-game-9 route (the mic check, the recording and thinking states, cancel, the timeout, heard/expected, '
            'repeat, slower, help, no sé, typing and buttons) is reachable by key in each panel state, each with a test. The '
            'heard/expected line appears after every spoken turn. No two layers overlap on 1280×720 and 800×600 at 100, 125 and 150 % '
            '(the canvases of `HudLayout`). The scripted runs whose captures show the panel are re-baselined (`docs/game/scripted-runs.md`). '
            'U02 changes code and support-language text only (`content/locale` is outside game-v1’s hash), but it lands before the first '
            'pilot tester so that every tester sees the same panel.'))

# ---------------------------------------------------------------- 8. The learner book

set_text(107, 'Nouns:',
         '**Nouns:** retain encountered and used words, their articles, word-card audio and per-noun icons (`BookLayer.DrawNounIcon`; grey until known under `--greyscale`). W07 adds icons by key for fixtures such as el bol and la cuchara.')
set_text(108, 'Verbs:',
         'Verbs: retain forms taught or used, with locked cells hidden. The columns are present, preterite, near future, simple future and obligation (tener que). The pilot adds no imperative column (the instructions are for comprehension, section 18). When a later lesson teaches the imperative, note that echa, mezcla, saca and espera are spelt like the third-person present, which the book counts as present (`VerbReading`), so an imperative column needs its own rule.')
expect(113, 'Keep the five tabs')
put_after(113,
          h2('For implementers: what the bread pilot adds to the book'),
          bullet('**Phrases:** the nine instruction rows (section 10.16) in their own group, “Instrucciones”, each with heard, understood '
                 'first time, help and its rung, drawn in an “understood” tone rather than the “known” tone of production (the page '
                 'colours rows by production counts today).'),
          bullet('**Lessons:** session 11 like the others, with the recipe’s line beneath it: bakes, sittings finished without help, '
                 'mastered.'),
          bullet('**Nouns:** harina (an item), el bol and la cuchara (fixtures), with icons.'),
          bullet('**No new tab and no recipe book in the pilot;** the station lists the recipes the learner knows.'))

# ---------------------------------------------------------------- 9. Cooking

LOOP = [
    'Learn bread from Luz in a new lesson, session 11, “En la panadería”, after sessions 1–10 (section 10.6).',
    'Gather water in the bottle, one wood and flour bought at the stall. Luz provides missing ingredients once per in-game day (10.12). Keep lesson completion recoverable; the grant’s lesson exception is an open implementation decision (18.2, decision 20).',
    'Press E at Luz’s oven to open the station, an overlay panel over the live world (decided).',
    'Follow six short instructions by holding a thing and using it on a target: no timing or reflex test.',
    'Each instruction follows the phrase support ladder; repeat and slower audio are always there, and help (H, or “Ayuda”) shows the '
    'English and the highlight.',
    'Receive two loaves, or three when at least five steps were right first time. Eat them (a loaf fills 25 hunger, section 9) or '
    'sell them at the stall for a coin each (decided). Cooked dishes with buffs come later (W15).',
]
for k, (i, prefix) in enumerate([(120, '1. Learn'), (121, '2. Obtain'), (122, '3. Open'), (123, '4. Carry'), (124, '5. Follow'),
                                 (125, '6. Receive')]):
    expect(i, prefix)
    replace[i] = num(k + 1, LOOP[k])

expect(127, 'Stage')
replace[127] = table([2400, 7968], ['Rung', 'What the station shows, and what a right act means'], [
    ['1 — English and card', 'The instruction spoken, its Spanish text and its English. The thing to hold and its target are '
     'highlighted only after a slip or on help. A right act moves the row to rung 2 at once (the English rung has no gap) but is not '
     'evidence of Spanish: the English was on screen.'],
    ['2 — Card and audio', 'Spoken and written in Spanish; the English on help. A right act, first time and without help, is text-supported comprehension.'],
    ['3 — Audio only', 'Spoken only, with Repetir and Más despacio; the text on help. A right act, first time and without help, is '
     'listening comprehension.'],
    ['4 — None', 'When every step of a chunk is at rung 4, the chunk’s one instruction (“Haz la masa”) replaces them. When every chunk '
     'is at rung 4, the goal alone (“Haz pan”) replaces the chunks.'],
    ['Mastered', 'Not a fifth rung. When the goal row reaches rung 4 and the recipe was finished without help in two sittings, the '
     'station also offers the bake as one click; the guided bake stays. Beyond the pilot.'],
])

set_text(129, 'Add spaced action evidence',
         'Instruction rows use the new phrase kind `does` and a dedicated `PhraseProgress.RecordAct` path. Eligible acts receive spacing credit only within instruction rows. They never add spoken or typed production, transfer, or noun credit. Section 10.10 specifies the counters; decision 4 covers the D-game-10 amendment.')

expect(132, 'Step')
replace[132] = table([800, 4784, 4784], ['Step', 'Gameplay', 'Language and evidence'], [
    ['1', 'Before the oven: water in the bottle, one wood and flour bought at the stall or supplied by Luz’s daily grant. The bowl and spoon are station fixtures.', 'la harina, el agua, la madera. “Quiero harina” at the stall is production, '
     'with a different NPC.'],
    ['2', 'Flour and water into the bowl, in an order drawn for each bake; then mix with the spoon.',
     '“Pon la harina en el bol.” “Echa el agua en el bol.” “Mezcla con la cuchara.” Acts show understanding, never production.'],
    ['3', 'Wood into the oven, the dough into the oven, the bread out by hand.',
     '“Pon la madera en el horno.” “Pon la masa en el horno.” “Saca el pan del horno.”'],
    ['4', 'Two loaves into the bag once (three for five or more first-time steps); sell them at the stall.',
     'el pan. “Vendo pan” at the stall reuses session 10’s selling with another NPC.'],
    ['5', 'Later bakes with less help; grouped instructions; mastery and the one-click bake after the pilot.',
     'Spaced act evidence; no credit from repeats inside the gap.'],
])

expect(134, 'SCOPE CONTROL')
replace[134] = callout('SCOPE CONTROL', [
    'Use three ingredients (flour, water and wood), two fixtures (bowl and spoon) and the oven. Each new noun needs its gender, lexicon entry and a justified level waiver where required. Use Latin American neutral Spanish. Confirm bol or tazón during content review, then use that term consistently in labels, rows and recordings.'], 'red')

PROCEDURE_JSON = r'''
{
  "$schema": "../../../schema/procedure.schema.json",
  "schemaVersion": 1,
  "id": "pan",
  "station": "horno",
  "teacher": "baker",
  "level": "a2",
  "makes": { "item": "bread", "count": 2, "bonus": { "firstTimeSteps": 5, "count": 1 } },
  "uses": [
    { "item": "flour", "count": 1 },
    { "item": "water-bottle", "count": 1, "leaves": "bottle" },
    { "item": "wood", "count": 1 }
  ],
  "steps": [
    { "id": "harina",  "row": "pon-la-harina", "do": "poner",   "what": "harina", "where": "bol", "anyOrder": "bol" },
    { "id": "agua",    "row": "echa-el-agua",  "do": "echar",   "what": "agua",   "where": "bol", "anyOrder": "bol" },
    { "id": "mezclar", "row": "mezcla",        "do": "mezclar", "what": "bol",    "with": "cuchara" },
    { "id": "madera",  "row": "pon-la-madera", "do": "poner",   "what": "madera", "where": "horno" },
    { "id": "masa",    "row": "pon-la-masa",   "do": "poner",   "what": "masa",   "where": "horno" },
    { "id": "sacar",   "row": "saca-el-pan",   "do": "sacar",   "what": "pan",    "from": "horno" }
  ],
  "chunks": [
    { "id": "la-masa",  "row": "haz-la-masa",      "steps": ["harina", "agua", "mezclar"] },
    { "id": "el-horno", "row": "haz-el-pan-horno", "steps": ["madera", "masa", "sacar"] }
  ],
  "goal": { "row": "haz-pan" }
}
'''

STATION_JSON = r'''
game/stations.json
  { "id": "horno", "prop": "oven", "npc": "baker",
    "fixtures": [ { "noun": "bol", "icon": "bowl" }, { "noun": "cuchara", "icon": "spoon" }, { "noun": "horno", "icon": "oven" } ] }

game/actions.json
  { "use": "harina",  "on": "bol",   "verbs": ["poner"] },
  { "use": "agua",    "on": "bol",   "verbs": ["echar"] },
  { "use": "cuchara", "on": "bol",   "verbs": ["mezclar"] },
  { "use": "madera",  "on": "horno", "verbs": ["poner"] },
  { "use": "masa",    "on": "horno", "verbs": ["poner"] },
  { "use": "mano",    "on": "horno", "verbs": ["sacar", "abrir"] }
'''

SYSTEMS_JSON = r'''
"systems": {
  "procedures": {
    "version": 1,
    "known": ["pan"],
    "progress": { "pan": { "bakes": 4, "cleanSittings": [3, 5], "mastered": false } },
    "run": { "procedure": "pan", "state": "InRun", "step": "mezclar", "order": ["agua", "harina"],
             "done": ["agua", "harina"], "slips": { "harina": 1 }, "helped": ["agua"] }
  }
}
'''

SPEC_9 = [
    spacer(),
    h2('10.5 Decisions this section rests on'),
    bullet('**Confirmed direction:** needs precede the pilot; the station is an overlay; the stall buys bread for one coin; learners understand commands but are not assessed on producing imperative forms. The support ladder includes written Spanish and English at its earlier rungs.'),
    bullet('**Proposed settings (section 18):** session 11 at A2, flour sold at the stall, the six-action recipe in 10.9, varied order for the first two steps, a verb choice only for distinct actions, and a one-loaf bonus. Act evidence and the pilot protocol require decisions 4 and 1.'),

    h2('10.6 The lesson: session 11, “En la panadería”'),
    p('A lesson file, `game/lessons/en-la-panaderia.json`, built like `el-mercado.json`: `npc` baker, `level` a2, `after` el-mercado, '
      '`session` 11, `autoStart` true, and `verbs` hacer (1 sg, ir-a), hacer (1 sg, pret) and vender (1 sg, pres). It follows the '
      'village plan’s principle 7: a plan, a doing and a report.'),
    table([1500, 1100, 5168, 2600], ['Step', 'Type', 'Content', 'Evidence'], [
        ['k0-review', 'review', 'Two candidates from sessions 9 and 10 at Luz.', 'as today'],
        ['k0-intro', 'explain', '(English) Luz will show you how to make bread. Her instructions are short commands: pon (put), echa '
         '(pour), mezcla (mix), saca (take out). You only need to understand them.', 'none'],
        ['k1-plan', 'model', 'Luz: “Hoy vamos a hacer pan. ¿Me ayudas?”', 'heard'],
        ['k1-answer', 'answer', '“Voy a hacer pan.” at a lesson prompt (tense ir-a, 1 sg).', 'production'],
        ['k2-mezcla', 'listen', '“Mezcla con la cuchara.” Pick its meaning from three.', 'comprehension'],
        ['k2-saca', 'listen', '“Saca el pan del horno.” Pick its meaning from three.', 'comprehension'],
        ['k3-oven', 'do', 'Trigger `procedure_done`, detail `pan`; the beacon points at the oven. The first bake runs at rung 1.',
         'acts (10.10)'],
        ['k4-report', 'answer', 'To “¿Qué hiciste?”: “Hice pan.” (pret, 1 sg).', 'production'],
        ['k5-sell', 'do', 'Sell a loaf at the stall; the trigger is the world event the stall’s purchase logs (check '
         '`VoxelSession.ApplyHostEvents`).', 'the selling turn'],
        ['k6-review', 'review', 'Two candidates from this lesson.', 'as today'],
        ['k7-recap', 'recap', '—', 'none'],
    ]),
    spacer(),
    p('Before writing rows, try in the core’s CLI that “voy a hacer pan”, “hice pan”, “quiero harina” and “vendo pan” are clean and '
      'that “ayer hago pan” gives `conj.tense`. A wrong grade waits for a core milestone.'),

    h2('10.7 The data'),
    p('Four file kinds with schemas in `content/schema/` (W05), loaded by `GameData` and checked by `GameDataValidator`. The recipe, '
      '`game/procedures/pan.json`:', keep_next=True),
    code(PROCEDURE_JSON),
    spacer(),
    p('`anyOrder` marks steps that may come in either order; the runner draws their order for each bake with '
      '`WorldRng.For(seed, "proc", bake)`. `makes.bonus` gives one more loaf when at least five steps were right first time. The '
      'station and its actions:', keep_next=True),
    code(STATION_JSON),
    spacer(),
    p('A pair lists two verbs only when they are different acts with different results (sacar or abrir); then a short choice of '
      'Spanish verbs appears. Synonyms (poner or echar the flour) are one act with one verb, so correct Spanish is never a wrong '
      'choice. Fixtures are not inventory items: a noun and an icon, nothing else.'),

    h2('10.8 The station'),
    bullet('**Entry.** E at any cell of the scene prop named in `stations.json` (`oven`), within the station reach, when the '
           'crosshair is on no NPC. Priority: an NPC, then a station prop, then the workbench. This needs a lookup from a prop’s name to '
           'its cells, built from the scene’s stamp placements (as `AtFireRing` does for rings). The oven keeps no `label`.'),
    bullet('**The teacher.** Luz stays at her door, about six blocks away. Talking from the station (V or Space, or W to type) goes to '
           'her wherever she stands (W08). After the lesson the station opens without talking to her first.'),
    bullet('**Layout.** The overlay of the W plan (its section 4.3), drawn over the live world: the instruction strip with Repetir, Más '
           'despacio and Ayuda and a dot per step; the fixtures on the left; the targets in the middle; what can be held along the '
           'bottom. The world keeps running (only F8 pauses it), but the need meters stop while the station is open, as in every panel '
           '(section 9.2).'),
    bullet('**What can be held:** every item in the bag, so apples, coins and torches are natural distractors; the fixture tools (la '
           'cuchara); intermediate things once they exist (la masa); and the empty hand (la mano). Targets: el bol, el horno.'),
    bullet('**Labels** are nouns with their article (la harina ×1). A noun’s English shows on hover until the noun is known (D-game-6).'),
    bullet('**Keys:** 1–9 select a held item on the current page; Page Up/Page Down page through all inventory items, fixtures and intermediates; 0 selects the empty hand. Tab/Shift+Tab move between targets; Enter uses the selection; H requests help, R repeats, S slows, V/Space speaks to Luz, W types and Esc leaves with the bake saved. The page indicator and selected item remain visible.'),
    bullet('**D-game-9 inside the station:** talking to Luz shows the recording and thinking states, cancel, the timeout and the '
           'heard/expected line, as the dialogue panel does.'),
    bullet('**Audio.** Play each row through `NpcVoice`, with the slow file for S. With no audio device or with `--mute`, show the Spanish card at the audio-only rung and classify the act as reading evidence. Decision 19 defines whether this evidence may advance listening-based support or mastery.'),

    h2('10.9 Step by step'),
    table([1000, 1900, 1500, 2868, 3100], ['Step', 'Hold → target', 'Act', 'What changes', 'Instruction row'], [
        ['harina', 'la harina → el bol', 'poner', 'The bowl shows flour.', '`pon-la-harina`: “Pon la harina en el bol.”'],
        ['agua', 'el agua → el bol', 'echar', 'The bowl shows flour and water.', '`echa-el-agua`: “Echa el agua en el bol.”'],
        ['mezclar', 'la cuchara → el bol', 'mezclar', 'Dough in the bowl; la masa can be held.', '`mezcla`: “Mezcla con la cuchara.”'],
        ['madera', 'la madera → el horno', 'poner', 'The oven’s fire grows.', '`pon-la-madera`: “Pon la madera en el horno.”'],
        ['masa', 'la masa → el horno', 'poner', 'The dough becomes bread. A brief animation shows the change without delaying the next input.',
         '`pon-la-masa`: “Pon la masa en el horno.”'],
        ['sacar', 'la mano → el horno', 'sacar (a choice with abrir)', 'The bake ends (10.13).',
         '`saca-el-pan`: “Saca el pan del horno.”'],
        ['—', 'chunk: steps 1 to 3', '—', '—', '`haz-la-masa`: “Haz la masa.”'],
        ['—', 'chunk: steps 4 to 6', '—', '—', '`haz-el-pan-horno`: “Haz el pan en el horno.” (or “Hornea el pan.”, which needs '
         'hornear)'],
        ['—', 'goal', '—', '—', '`haz-pan`: “Haz pan.”'],
    ]),
    spacer(),
    p('Anything held may be used on any target: that is an attempt, never an error message. There are three kinds of wrong act: the '
      'wrong thing (la harina on el horno), the wrong verb (abrir when the instruction says saca), and out of order (a later step’s '
      'act; Luz says “Primero.” and repeats the current instruction). A wrong act changes nothing in the station, uses nothing up and '
      'costs nothing.'),

    h2('10.10 Support, help and evidence'),
    bullet('**Slips.** At rung 1, the first slip shows the highlight. At rungs 2 to 4, the first slip repeats the instruction, the second '
           'plays it slowly, and the third shows the highlight (counted as help).'),
    bullet('**Help** (H, the Ayuda button, or “ayuda” or “no sé” said to Luz) shows the English and the highlight at once, and steps the '
           'row up one rung, as “no sé” does for a phrase (`PhraseProgress.cs:454`). On a folded step, the chunk unfolds for this bake '
           'and the chunk row steps up.'),
    bullet('**Repeat and slower** (R, S, “Otra vez”, “Más despacio”) replay the line. They are listening aids, never help.'),
    bullet('**Recoverable steps.** Help makes the required action explicit. A wrong act consumes nothing. Ingredient availability and a full bag use the recovery states in 10.12 and 10.13.'),
    table([3500, 4268, 2600], ['Event', 'Recorded as', 'Never'], [
        ['The step’s instruction shown (once per step per bake, not per replay)', 'Heard +1 on its row', '—'],
        ['A right act, first time, without help, at rung 2 or 3', 'Answered +1; `RecordAct` adds six instruction-spacing units and steps the row down when its gap has passed', 'supported, unaided, transfer; a noun’s said or correct'],
        ['A right act at rung 1, without help', 'the row moves to rung 2 at once (the English rung); no Answered', '—'],
        ['A right act after a slip, or with help', 'no Answered; no step down', '—'],
        ['Help', 'DontKnow +1; the row steps up one rung', '—'],
        ['A wrong act', 'the bake’s record (wrong thing, wrong verb, out of order) and the telemetry',
         '`PhraseRecord.Slips`, which counts production'],
        ['Repeat or slower', 'the telemetry', 'help'],
        ['A verb choice', 'the act, right or wrong', '`PhraseUseKind.Choice`, which would step the row up'],
        ['The station’s labels', 'each noun’s read count, once per bake', 'said or correct: no noun credit from clicks'],
    ]),
    spacer(),
    p('**Registration and spacing.** Register all instruction rows as scaffolds. The first successful rung-1 act records the spacing timestamp and moves the row to rung 2 without comprehension credit. Later eligible acts use the existing 10-minute, then one-hour gaps; a new sitting satisfies a gap. For a chunk or goal, award one act credit only after every constituent action succeeds on its first attempt without help. Do not also credit hidden constituent rows. Introduce a newly folded instruction on the next bake (10.11).'),

    h2('10.11 How the help fades'),
    p('With two bakes in each sitting, at least ten minutes of play apart and with no sleep between them, a tester who acts right first '
      'time without help sees:', keep_next=True),
    table([3400, 6968], ['When', 'What changes'], [
        ['Sitting 1, first bake (the lesson)', 'every row moves from rung 1 to rung 2, at once'],
        ['Sitting 1, second bake', 'rows right first time move to rung 3 (audio only)'],
        ['Sitting 2 (another day), first bake', 'rows right first time move to rung 4; a chunk whose steps are all at rung 4 folds'],
        ['Sitting 2, second bake', 'the first grouped instruction, “Haz la masa” (a chunk row starts at rung 2)'],
        ['Sitting 3', 'the chunk rows fade; the goal (“Haz pan”) once both chunks reach rung 4, which few testers will'],
        ['Mastery', 'beyond the pilot; a scripted run covers it'],
    ]),
    spacer(),
    p('With one bake per sitting, each line moves one sitting later: the first grouped instruction appears in sitting 4.'),

    h2('10.12 Ingredients and the economy'),
    bullet('**Flour** is a new item: `Flour`, the next free number (34 if N07’s roast apple takes 33), name `flour`, noun harina (feminine), stack 99, an atlas icon painted in code as V03 '
           'painted the coin. It joins the enum and its table, the item enums of four schemas (nouns, host-events, session, objective), '
           'the atlas, the held arm and the scripted names.'),
    bullet('**Flour is sold at the stall for one coin** (recommended, decision 6). `instead` (V21b) sends the shopkeeper’s answer to a '
           'request for harina to a fixed sell, `stall.sell_flour`, so the pilot needs no W04; W10 checks that an `instead` target may be '
           'a sell. If the owner prefers Luz to sell flour, W04 (price lists) comes first.'),
    bullet('**Water** comes in the bottle, filled at the river or the well; the bake gives the bottle back empty. **Wood** is gathered; '
           'one per bake.'),
    bullet('**Bread:** two loaves per bake, with one bonus loaf when at least five steps succeed on their first attempt. Help is allowed for this practical reward; only unassisted first attempts count as comprehension. Each loaf restores 25 hunger and 5 health. Flour costs one coin per bake, compared with four coins to buy two loaves from Luz; wood and gathering time remain additional costs.'),
    bullet('**The stall buys bread for one coin** (decided): a fixed buy, `stall.buy_bread`, on `offer_item` (“Vendo pan.”) at the '
           'shopkeeper’s open prompt, `stall-need`. Another item offered answers `stall.wont_buy`; no bread in the bag, '
           '`stall.cant_sell`. The lines are in the shopkeeper’s voice (`em_alex`). New answers at `stall-need` change what the '
           'recognizer decodes there, so session 1’s stall runs are checked again.'),
    bullet('**Missing ingredients:** on “¿Tienes harina?”, “Quiero harina” or “Ayuda”, Luz supplies the ingredients currently missing from the bake through `give_missing` (W08), once per in-game day. Persist grant use across reloads. Decision 20 specifies the lesson exception and the route after the daily grant has been used.'),
    bullet('**After session 11** no lesson follows. The pull to bake again is hunger, the coin, and Luz’s offer when the goal row is due.'),

    h2('10.13 Saving and transactions'),
    bullet('**Nothing is used up during the bake.** At the last step, one exchange checks everything and makes every change: take the '
           'flour, the water and the wood; give the bottle back; add the loaves. `CraftingRules` and `Inventory.CanExchange` are one-in, '
           'one-out, so this many-item exchange is new code.'),
    bullet('**The bake’s states:** NotStarted, InRun (at a step), ReadyToCollect, Done. Esc or walking away keeps InRun. An ingredient '
           'gone at the end (sold or used meanwhile) re-opens the step that used it, and Luz names it. A full bag leaves the bake at '
           'ReadyToCollect: the bread waits in the oven until there is room, and is never lost.'),
    bullet('**W06 adds `systems.procedures` to the version-6 map introduced by N01.** Example procedure entry (the save also retains `systems.needs`):'),
    code(SYSTEMS_JSON),
    spacer(),
    bullet('Versions 1–5 migrate to full needs and empty procedure state. A version-6 save made before W06 preserves its needs and initialises missing procedure state. Every supported older fixture still loads.'),
    bullet('Loading in the middle of a bake restores the station. A completion replayed after a load cannot grant twice: the state '
           'becomes Done in the same transaction that adds the loaves.'),
    bullet('Determinism: the runner reads no clock and draws no `System.Random`; the order draw is seeded; the same acts give the same '
           'events.'),

    h2('10.14 The teacher in the loop (W08)'),
    p('While a bake runs, Luz’s agent is in a `teaching` state at a constrained prompt, `baker-proc` (`npcs/baker.json` names it next '
      'to `idle`). It enters on `baker.teach_start` and leaves on `baker.teach_end`.', keep_next=True),
    table([2900, 2300, 2300, 2868], ['The learner says', 'Intent', 'Host kind', 'The game does'], [
        ['“Otra vez, por favor.”', '`ask_repeat`', '`procedure_repeat`', 'the instruction again'],
        ['“Más despacio, por favor.”', '`ask_slower`', '`procedure_slower`', 'the slow recording'],
        ['“Ayuda.” / “No sé.”', '`ask_help` / `dont_know`', '`procedure_help`', 'help, as the H key'],
        ['“¿Tienes harina?” / “Quiero harina.”', '`ask_availability` / `request_item`', '`give_missing`',
         'the missing ingredients, once per in-game day'],
        ['“Gracias.”', '`thank`', '—', 'Luz’s line'],
    ]),
    spacer(),
    p('W08 comes after V29, which declares the NPCs’ reply pools in their files, and after the spoken-lines study (the decided order). '
      'Until the study lands, a spoken line outside the answer set reads as its nearest answer.'),

    h2('10.15 Mastery and the one-click bake'),
    p('When the goal row reaches rung 4 and the recipe was finished without help in two different sittings, the recipe is mastered '
      '(`procedure_mastered`) and the station offers the bake as one click, through the crafting path, which logs `item_crafted` (W01). '
      'The guided bake stays on offer and comes first when the goal row is due again (“¿Hacemos pan juntos?”). It is never forced, and '
      'there is no batch craft.'),

    h2('10.16 Content'),
    table([1600, 5768, 3000], ['Kind', 'Rows (draft Spanish / English; the Opus pass decides)', 'Voice and notes'], [
        ['does (9)', ['Pon la harina en el bol. / Put the flour in the bowl.', 'Echa el agua en el bol. / Pour the water into the bowl.',
                      'Mezcla con la cuchara. / Mix with the spoon.', 'Pon la madera en el horno. / Put the wood in the oven.',
                      'Pon la masa en el horno. / Put the dough in the oven.',
                      'Saca el pan del horno. / Take the bread out of the oven.', 'Haz la masa. / Make the dough.',
                      'Haz el pan en el horno. / Bake the bread in the oven.', 'Haz pan. / Make bread.'],
         'Luz, normal and slow. The lines live in her pools (`baker_proc_*`), so the voice is hers (V01).'],
        ['says (4)', ['Voy a hacer pan. · Hice pan. · Quiero harina. · Vendo pan.',
                      'Each with its graded learner-error example, as the sheet test demands.'],
         'The learner; try each in the CLI first.'],
        ['hears', ['Hoy vamos a hacer pan. ¿Me ayudas? · ¿Qué hiciste? · Primero. · Toma. · ¿Hacemos pan juntos?',
                   'The stall’s lines for buying bread and selling flour.'], 'Luz; the shopkeeper'],
        ['Words', 'New lexicon entries with their gender: harina (f), bol or tazón (m), cuchara (f). Waivers in `cefr/a1.txt`’s '
         'world-words block, one reason each: harina, bol or tazón, cuchara, masa, echar, mezclar. sacar is A2; poner, hacer, madera, '
         'horno, pan and agua are A1.', 'Choose bol or tazón before any row is written'],
        ['Recordings', 'About 20 to 25 lines, 40 to 50 WAVs, through `scripts/prerender-speech.sh` into `speech/baker/` and '
         '`speech/shopkeeper/`; then `docs/game/speech-checklist.md`.', 'The owner listens to Luz’s lines before the pilot '
         '(decision 2)'],
        ['Support language', 'The English and the card of every row; the recipe’s, the station’s and the fixtures’ labels; `ui.station*` '
         'strings; a controls entry; the lesson’s text (`content/locale/en/`).', 'Outside game-v1’s hash'],
    ]),
    spacer(),

    h2('10.17 Telemetry and checks'),
    bullet('**A new event family, `procedure`** (beside lesson, phrase, voice and world). Per act: the bake, the recipe, the step, the '
           'row, the rung, the outcome (right, wrong thing, wrong verb, out of order), first time or not, help, repeats, slower, and the '
           'milliseconds since the instruction. Per bake: the sitting, the day, steps right first time, help, slips, seconds, completed, '
           'loaves. The log writer, `scripts/playtest-report.sh` and the consent text in `docs/game/playtest.md` change with it.'),
    bullet('**The session summary** (`Session/SessionSummary.cs`) gains a line: “Pan: 4 de 6 pasos sin ayuda.”'),
    bullet('**Checks:** two or three comprehension items for the lesson in `checks.json` (`lesson`: en-la-panaderia; hear an '
           'instruction, pick its meaning; three options, so chance is one in three). `CheckBank.AskedIn` asks the whole cumulative '
           'battery at session 11, so the pilot’s battery needs a lesson filter.'),

    h2('10.18 Tests and runs'),
    bullet('**Game.Core** (`tests/Vocaterra.Game.Core.Tests/Activities/`): every rung; folding and the goal; help unfolds and steps up; '
           'two bakes within ten minutes credit once; a sleep meets the gap; mastery after two sittings; leaving half-way takes nothing; '
           'a full bag leaves ReadyToCollect; an ingredient gone at the end re-opens its step; a bake survives a save and a load; a '
           'replayed completion grants once; the same acts give the same events; the order draw is seeded; the migration from version '
           '5 to 6; every save version from 1 loads.'),
    bullet('**The validator:** every noun is a catalogue noun or a fixture; every verb is in `actions.json` for its pair and in the '
           'lexicon; every row is a `does` row with its recording; chunks cover neighbouring steps; the prop exists in the scene; the '
           'teacher is an NPC; the Spanish is held to the recipe’s level.'),
    bullet('**Game** (`tests/Vocaterra.Game.Tests/UI/Station/`): layouts on 1280×720 and 800×600 at 100, 125 and 150 %; the verb '
           'choice only for a pair with two verbs; the English only at rung 1 or on help; the keyboard path.'),
    bullet('**Scripted runs:** r27-bread (session 11 end to end), r28-bread-again (a later sitting through `--sleep`) and r29-bread-fold (folding, the goal and mastery). Reserve r25 and r26 for needs. New steps: `--station`, `--act <hold> <target> [verb]`, `--proc-help`. Confirm unused run IDs when registering milestones.'),
    bullet('**The content loop:** `scripts/content-loop.sh --grammar --speech`; goldens re-recorded in a commit of their own; the new '
           'bundle hash in `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json`.'),
]
expect(135, '')
put_after(135, *SPEC_9)

# ---------------------------------------------------------------- 10. Crafting

expect(143, '')
put_after(143, p('**Dependencies:** W12 (data-driven items and save version 7), W13 (wear) and W08 (teaching state). Item migration requires the owner’s approval (D-game-21). Review Domingo’s recordings before use. Check “se rompió el mango”, “el hacha está rota” and the agreement error “el hacha está roto” in the core’s CLI before content relies on them.'))
set_text(148, 'Repeat crafts:',
         'Repeat crafts: offer the one-click craft after evidenced mastery (one at a time; no batch); keep guided practice available and '
         'retain names and quantities.')

# ---------------------------------------------------------------- 11. Generalise

set_text(157, 'Build a deterministic',
         'Build a deterministic, engine-free `ProcedureRunner` in a new Game.Core folder, `Activities/` (namespace '
         '`Vocaterra.Game.Core.Activities`), next to the lesson runner’s `Language/Lessons/`, reusing its plain-state and trigger '
         'conventions; `docs/game/architecture.md` gains the folder. A lesson introduces an activity; a procedure supports repeated '
         'runs. It needs stable procedure, station and instruction ids; ordered steps; tool, ingredient and target requirements; '
         'quantities; allowed alternatives; consumption and outputs; phrase and audio links; help and completion events; references '
         'checked at bundle load. None of this exists at the baseline.')
set_text(160, 'Events:',
         '**Events:** a craft emits `SimulationEventKind.ItemCrafted`, but `VoxelSession` only learns its item (`VoxelSession.cs:352`); '
         'W01 logs `item_crafted`. A world event carries a name, an NPC and a detail but no amount: objectives read the private '
         '`_eventAmount` (`VoxelSession.cs:40`), and a recipe’s count goes the same way. Each event joins `WorldEvents.All` and the '
         'schemas that list event names, so a lesson’s `do` step can wait for `procedure_done pan` with no new step type:')
put_after(160,
          table([2500, 2900, 3468, 1500], ['Event', 'Raised by', 'Detail', 'Milestone'], [
              ['`item_crafted`', '`VoxelSession` (a craft, a one-click bake)', 'the output’s noun; the count as the amount', 'W01'],
              ['`procedure_started`', '`ProcedureRunner`', 'the recipe’s id', 'W06'],
              ['`procedure_step`', '`ProcedureRunner`', '`pan.mezclar` first-time, retried or helped', 'W06'],
              ['`procedure_done`', '`ProcedureRunner`', 'the recipe’s id; the loaves as the amount', 'W06'],
              ['`procedure_mastered`', '`ProcedureRunner`', 'the recipe’s id', 'W06'],
          ]),
          spacer())
set_text(161, 'Content:',
         '**Content:** new definitions go under `content/game-v1/game`, with schemas under `content/schema`, loaded and cross-checked '
         'through `GameData` and `GameDataValidator`; the support-language text goes in `content/locale/en`. Instruction rows are a new '
         'phrase kind, `does`, beside `says` and `hears` (`phrases.schema.json`, the columns of `content/PHRASES.md`, the sheet reader '
         'and the sheet test). Noun and phrase ids, the lexicon and inflections grow only as needed. The bundle has an A2 ceiling and '
         'per-lesson levels, and the pilot teaches no imperative.')
set_text(163, 'State:',
         '**State:** keep seed-based determinism, stored play time and stable ids. The bake’s state (step, order, steps done, slips, help) and each recipe’s progress go in save version 6’s `systems` map; nothing is used up before the end, so no ingredient is staged in the save (section 10.13). Save format 5 is current; W12’s version 7 (data item IDs and wear) comes after the pilot. Migrate old saves, and keep the enum item names until an explicit item migration exists.')
set_text(164, 'Transactions:',
         '**Transactions:** the last step checks every input and the room for the output, then exchanges everything in one step; '
         'outputs are granted once; a full bag leaves the bake waiting in the oven; replaying a completion or loading mid-bake never '
         'duplicates anything. World changes stay on the session side, and language evaluation on the existing turn-worker path.')

# ---------------------------------------------------------------- 12. Learner model

set_text(174, 'Credit timing uses stored play time',
         'Credit timing uses stored play time: 10 minutes after the first credited use, one hour after later uses, or a new sitting. '
         'The English rung steps down at once. A launch, a load or a sleep starts a sitting (`GuideSession.cs:255`), so a sleep also '
         'meets any gap; the day (lessons and orders) moves only by sleeping.')
expect(178, '')
put_after(178,
          h2('Act evidence (proposed amendment to D-game-10)'),
          p('D-game-10 tracks a phrase through the card shown and its supported, unaided, choice-only and “no sé” uses. Acts at a station '
            'are new evidence. They may fade instruction rows (the `does` kind). They count as comprehension (Answered) only when right '
            'first time without help at rung 2 or 3. They never count as production, transfer or noun mastery. The owner records this '
            'as an amendment before W06 (decision 4).'),
          p('Instruction rows use guided review through Luz’s bake offer when the goal row is due. The phrase review deck serves learner phrases with prompts.'))

# ---------------------------------------------------------------- 13. Visual and UI

set_text(184, 'Use warm local lights',
         'Use warm local light (the lanterns that exist), window glow and colour accents to give functional spaces identity. Smoke '
         'needs a particle layer, which the renderer does not have.')
set_text(190, 'Use the existing F3 debug toggle',
         'Make ordinary play clean: the debug overlay and the dialogue’s developer note are off by default, and on with a new `--dev` '
         'flag or F3 (U01).')
expect(193, 'Use deliberate panels')
put_after(193,
          h2('For implementers: U01, clean defaults (proposed)'),
          bullet('**A `--dev` launch flag** (`Startup/LaunchOptions.cs`) sets the start state of the debug overlay (`VoxelGame.cs:212`, '
                 'today `!_playtest`) and of the dialogue’s developer note (`DialogueModel.cs:527`). F3 toggles both in any mode; '
                 '`--playtest` stays clean.'),
          bullet('**Scripted runs** that read the developer note pass `--dev` (`scripts/scripted-runs.sh`), or their captures are '
                 're-baselined.'),
          bullet('**The tip box** (`UI/Hud/TipLayer.cs`) never overlaps the debug overlay when F3 is on.'),
          bullet('**One hint:** `tip.speak` shows at the first conversation only; the lesson strip and the footer stop repeating it.'),
          bullet('**The objective tracker** (`ObjectiveLayer`, `UI/Hud/HudLayers.cs:19`) shows one line per objective, three at most, the '
                 'most recently advanced first and the rest as “+n”. A lesson’s `do` step shows in the lesson strip, not also in the '
                 'tracker. The rest line (`VoxelSession.Sleep.cs:72`) keeps its place.'),
          bullet('**The need meters** (section 9) sit beside the hotbar; the tracker and the tip box leave them room.'),
          bullet('**Acceptance:** no overlap on 1280×720 and 800×600 at 100, 125 and 150 % (the layout tests); screenshot baselines '
                 're-recorded; the D-game-9 tests unchanged. U01 changes code and support-language text only.'))

# ---------------------------------------------------------------- 14. Sequence

set_text(196, 'Follow the proposed W-series dependencies',
         'Build the queued language follow-ups, then player needs, then the bread pilot. The sequence below assigns each dependency and acceptance gate; section 18 identifies the decisions still required.')
expect(197, 'Phase')
replace[197] = table([2100, 5068, 3200], ['Phase', 'Focus and dependencies', 'Reviewable deliverable'], [
    ['0 — Documents', 'Register N, W and U milestones in implementation-plan.md section 4, with dependencies and required decisions. D-game-23 and D-game-24 define the approved direction.', 'A plan an orchestrator can brief from'],
    ['A — Follow-ups (decided)', 'M74, M75, V29, M78 and the spoken-lines study with its milestones, then their pull request '
     '(`docs/handovers/2026-11-02-after-followups.md`).', 'Weather and “¿cómo estás?” replies; the near-miss rule; spoken off-prompt '
     'lines decided'],
    ['B — Player needs (decided), the base of the pyramid', 'N01 to N05; W01 (`item_crafted`); N06; N07 (and N08, decision 14); then the needs check '
     '(section 9.12). Save version 6 and its `systems` map come with N01. No other system starts before the check passes.', 'Five needs in play from session 1, in stages; food that grows back and fair gifts; cooking at the fire; the rates tuned'],
    ['C — A clean surface', 'U01 and U02 (decision 7): code and support-language text only.',
     'Ordinary play without debug text; a calmer conversation panel'],
    ['D — Foundations', 'W01 supplies item_crafted in phase B. W02 (tools/materials) and W03 (patience) are outside the pilot path. W04 is required only if the owner chooses multi-item vendor pricing.',
     'Small validated extensions'],
    ['E — One procedure', 'W05, W06 (its `procedures` entry joins the `systems` map), W07, W08 and W10. W09 (the imperative) is left out: instructions are for comprehension (decided).', 'Bread with Luz end to end; session 11; recordings; save recovery'],
    ['F — The pilot', 'The owner listens to Luz’s lines (decision 2); the bundle is frozen; W11 with testers (decision 1).',
     'The go or no-go on the criteria below'],
    ['G — Repair and meals', 'After the pilot passes: W02 (tool/material definitions), then W12 (items as data), W13 (wear), W14 (repair), W15 (cooked dishes and buffs), then W16 (stage checks).', 'The runner reused with Domingo and at the camp'],
    ['H — Transfer and the village', 'W17 to W20, then W21 (stage checks); U03 and U04 after the last recall.',
     'Vocabulary reused across systems; a readable village'],
    ['I — Later', 'W22 to W29, each assessed with the pilot’s data.', 'Land, storage, trust, gear or new regions, each separately'],
])
set_text(199, 'Record decisions before implementing',
         'Section 18 lists confirmed direction and open decisions. Resolve each decision before the dependent milestone starts.')
expect(200, '')
put_after(200,
          h2('The presentation track: U01 to U04 (proposed)'),
          table([2500, 2368, 2900, 2600], ['Milestone', 'Scope', 'When', 'Size; model'], [
              ['U01 Clean defaults', 'section 14', 'before the pilot (phase C)', 'S; Haiku'],
              ['U02 The conversation surface', 'section 7', 'before the pilot (phase C)', 'M; Sonnet (layout design)'],
              ['U03 A readable village', 'section 6', 'after the last recall', 'M; Sonnet, then content'],
              ['U04 Light', 'sections 6 and 14; smoke only with a particle milestone', 'after the last recall', 'S/M; Haiku'],
          ]),
          spacer(),
          p('U identifies presentation work. Sections 6, 7 and 14 are its specification; register delivery status in `docs/game/implementation-plan.md` section 4.'))

CRITERIA = [
    '**Protocol (decision 1):** recruit 3–5 testers who completed sessions 1–10, or learners prepared for the same A2 material. Start at session 11 (`--skip-to-session 11`) with a primer. Freeze the executable, bundle, rates and presentation settings across three sittings on three days, with two bakes per sitting at least ten minutes of play apart and no intervening sleep. Run the lesson-filtered checks before the lesson, after it and at least 24 hours after the last sitting. The owner’s run checks functionality; testers provide the pilot evidence.',
    '**Playable:** every tester finishes a bake in every sitting by speech, typing or help. Missing ingredients, wrong acts, a full '
    'bag, leaving and resuming are covered by tests and scripted runs, and noted when they happen. Deaths are counted with their '
    'cause; a death caused by a language problem is a defect.',
    '**Listening (proposed target: 70%):** count first-attempt, unassisted responses to audio-only instructions, grouped by sitting and by step or chunk. Report the number of eligible attempts as well as the success rate; fewer attempts after folding must remain visible. Report text-supported and muted-play evidence separately. Record available items, targets and verb choices to describe the difficulty of each action; repeated procedures also test memory of the task.',
    '**Fading (proposed):** most testers see the first grouped instruction by sitting 2’s second bake, and help restores support at '
    'once. The goal row and mastery are beyond the pilot (scripted runs).',
    '**Replay:** bakes beyond the two per sitting, counting apart those made when hungry; bread sold at the stall without prompting; the tester’s own rating of the bakery '
    '(and of the camp objective, where they played it).',
    '**Preparation:** testers take food and water with them before a long walk (to the farm, the bridge or the mountain) without '
    'being told, and start bakes before hunger runs low.',
    '**Learning and transfer:** the bread battery (lesson-filtered, three options) from the pre-check to the post-check and the recall; '
    'harina asked for and pan sold at the stall, correctly and without the station’s support.',
    '**Cost and presentation:** new lines, WAVs, art and steps per recipe; minutes and help per bake; no overlapping layers (the '
    'layout tests).',
]
expect(202, 'Playability')
replace[202] = ''.join(bullet(c) for c in CRITERIA)
for i, prefix in [(203, 'Comprehension target'), (204, 'Fading target'), (205, 'Replay value'), (206, 'Learning:'),
                  (207, 'Scalability')]:
    expect(i, prefix)
    replace[i] = ''
set_text(208, 'Priority:',
         'Priority: one existing village, one bread procedure and recoverable support. Take the pilot’s go or no-go before recipe '
         'expansion, tool repair, farming or new regions, and keep sessions 1–10 playable throughout. If understanding is weak, change '
         'the ladder (more first-rung bakes, slower audio) before stage 3; if it works but feels like homework, shorten the recipe and '
         'lean on the one-click bake.')

# ---------------------------------------------------------------- 15 and 16. The galleries go; their table moves to section 4

# Revision 2's two closing galleries repeated the 15 images; each appears once in the body (the cover and sections 2 to 11).
expect(209, '')
expect(258, '')
assert 'w:type="page"' in els[209][3] and 'w:type="page"' in els[258][3]
for i, prefix in [(210, '15. Visual reference'), (211, 'Original concept artwork'), (239, '16. Current-build'),
                  (240, 'Original supplied screenshots')]:
    expect(i, prefix)
for i in range(210, 259):
    assert i not in after, i
    replace[i] = ''

expect(67, 'A USEFUL LOOP')
expect(68, '')
put_after(68,
          h2('Not to build from the concept art'),
          p('The concept images are illustrative, not evidence of implemented gameplay, camera defaults or learning metrics. They are '
            'the cover; the village and market vision and the systems concept board, above; the deep systems learning board '
            '(section 5); exploration and sanctuary, and mine and discovery (section 6); the adaptation concept board (section 7); '
            'the cooking lesson plaza (section 10); and the adventure concept board (section 11). The features below appear in them. '
            'Decisions in force rule them out, and an implementing agent builds none of them.'),
          table([3300, 2300, 4768], ['Depicted', 'Shown in', 'Ruled out by'], [
              ['Language XP (“+10 XP de idioma”), progress scores (“Mi Progreso 68/100”, “24/50 palabras”) and CEFR level locks (A1 '
               'to B2)', 'The systems, adaptation and adventure boards', 'The brief’s risk 6 and D-game-10: no automated CEFR '
               'placement or global score; keep learner evidence (section 1)'],
              ['A reward for correct grammar (“Recompensa: Pico de hierro”, “Extra: Gramática”)', 'The adaptation and adventure boards',
               'D-game-8 and the brief: communication enables play; correctness is coached, not paid'],
              ['A 3×3 crafting grid', 'The systems, adaptation and adventure boards', 'The owner’s crafting principle '
               '(`docs/game/design/crafting-and-cooking.md`); the brief: Minecraft is not a source for exact UI'],
              ['Swords, armour slots, bosses and durability numbers (“Durabilidad: 250”)', 'The systems and adventure boards',
               'The brief (combat left for later); D-game-23 (c), proposed: no combat'],
              ['Mines, ores, crystals, caves, ruins, temples and portals', 'Exploration, the mine, and the systems, adaptation and '
               'adventure boards', 'Section 6: no terrain work; mining is not a game system'],
              ['Land plots (“Tu parcela”), deserts, coasts and other biomes', 'The systems, adaptation and adventure boards',
               'D-game-22 (grow the village inward) and the brief (no extra biomes); land waits for the pilot (W22)'],
              ['English glosses always beside the Spanish (“el pico (pickaxe)”)', 'The systems, adaptation and adventure boards',
               'D-game-13: the English fades per phrase'],
              ['A full recipe checklist on screen', 'The deep systems board', 'The ladder: at audio only and at rung 4 the text is '
               'hidden'],
              ['Spain-only Spanish and dishes (paella, tortilla de patatas, patatas, sofreír) and a Spain flag', 'The village vision, '
               'and the systems, adaptation, adventure and deep systems boards', 'D-game-8: Latin American neutral Spanish (core ADR '
               '0005)'],
              ['A female trader (“La vendedora”, “la comerciante”) and a cast beyond the seven NPCs', 'The systems and adaptation '
               'boards', 'D-game-22 (e): the shopkeeper is “he”; D-game-23 (e), proposed: the seven NPCs'],
              ['Crowds with speech bubbles, a companion, animals and festivals', 'The village vision, the cooking plaza, exploration '
               'and the adventure board', 'Later behaviour work (section 6); NPCs stand still today'],
              ['A noun label on every object', 'The mine', 'Section 6: signs sparingly; only the faced NPC’s name plate (D-game-22, '
               'V22)'],
              ['Third-person framing', 'Every image but the deep systems board', 'D-game-8: first person by default; the shoulder '
               'view (F5) is optional'],
          ]),
          spacer())

# ---------------------------------------------------------------- 17. Design summary

expect(261, 'DESIGN COMMITMENT')
replace[261] = callout('DESIGN COMMITMENT', [
    'Evolve the working first-person village and language foundation. Use one bread procedure to prove repeated contextual practice, '
    'recoverable support and a useful reward: bread that feeds the player and sells at the stall. Build player needs first; '
    'preserve current learning credit, speech recovery and sessions 1–10. Expand to repair, meals and farming only after the '
    'pilot’s go and the decisions of section 18.'], 'green')

# ---------------------------------------------------------------- 18. Source basis

set_text(264, 'Reviewed against local HEAD',
         'Use the source index below when implementing the design. The required checks cover learning evidence, content, persistence, accessibility and playtest reporting.')
set_text(266, 'Paths start at the repository root',
         'Paths start at the repository root; within each entry, abbreviated paths continue from the named project or bundle directory. '
         'Elsewhere in this document, Game.Core paths are under `src/Vocaterra.Game.Core/` and Game paths under `src/Vocaterra.Game/`.')
set_text(267, 'World, cast and session arc',
         '**World, cast and session arc:** `content/game-v1/game/scene.json`; `game/npcs.json` and `game/npcs/*.json`; '
         '`game/lessons.json` and `game/lessons/*.json`; `game/host-events.json`. Decisions: `docs/game/decisions.md` (D-game-6, 8–10, '
         '13, 16, 21 and 22; D-game-23 in part). The brief: `docs/game/design/prototype-brief.md`.')
set_text(268, 'Crafting and items',
         '**Crafting and items:** `src/Vocaterra.Game.Core/Items/Inventory.cs`; `Items/ItemDefinition.cs`; `Items/Crafting/RecipeBook.cs`, '
         '`CraftingRules.cs`, `CraftingScreen.cs` and `CraftingPanel.cs`; `Simulation/VoxelSimulation.cs`; `Session/VoxelSession.cs`; '
         '`Npc/HostEventTable.cs` and `NpcHostEvents.cs`. Recipe data: `content/game-v1/game/recipes.json`.')
set_text(269, 'Learning evidence',
         '**Learning evidence:** `src/Vocaterra.Game.Core/Language/Words/WordProgress.cs` and `VerbReading.cs`; '
         '`Language/Phrases/PhraseProgress.cs` and `ReviewDeck.cs`; `Language/Learner/LearnerRecord.cs`; `Language/Lessons/LessonBook.cs` '
         '(the levels’ tenses); `Session/GuideSession.cs` (the scaffold phrases, the sitting); `Npc/NpcService.Save.cs`. The book: '
         '`Language/Book/BookTabs.cs`, `ConjugationTable.cs`, `BookPractise.cs` and `BookRecordings.cs`; '
         '`src/Vocaterra.Game/UI/Book/BookLayer.cs`.')
set_text(270, 'Presentation and persistence',
         '**Presentation and persistence:** `src/Vocaterra.Game/VoxelGame.cs` (the debug default); `Startup/LaunchOptions.cs`; '
         '`UI/Dialogue/`; `UI/Lesson/`; `UI/Hud/HudLayers.cs`, `HudLayout.cs` and `VoxelLayers.cs`; `UI/Crafting/`; '
         '`src/Vocaterra.Game.Core/Dialogue/DialogueModel.cs`; `Save/SaveGame.cs` and `SaveDocument.cs`; `Session/VoxelSession.Sleep.cs`; '
         '`Sound/NpcVoice.cs`. Content loading: `Content/GameData.cs` and `GameDataValidator.cs`.')
set_text(271, 'Design lineage',
         '**Design and delivery:** `docs/game/design/crafting-and-cooking.md`; `docs/game/activities-plan.md`; `docs/game/implementation-plan.md`; `docs/game/decisions.md`. **Architecture:** `docs/game/architecture.md` and `docs/nlp/adr/0009-core-and-game-boundary.md`. Sections 9, 10 and 15 specify the adaptation’s mechanics and sequence.')
set_text(274, 'For game/content changes',
         'For game and content changes, use `scripts/test.sh fast` while iterating and `scripts/test.sh full` for the milestone, through `scripts/limited.sh`; model-gated tests need `VOCATERRA_MODELS_DIR`. Content changes run `scripts/content-loop.sh --grammar --speech` (schemas, provenance, hashes, locale, lint, CEFR and grammar checks) and update the scripted baselines they move. Record the results with the implementation milestone.')

DECIDED = [
    ['Order of work', 'Queued language follow-ups (M74, M75, V29, M78 and the spoken-lines study), then the N series and its needs playtest, then the W pilot.',
     'W03, W04 and W08 build on V29; W08 follows the study.'],
    ['The bread station', 'An overlay panel over the live world.', 'W07 as planned; an in-world kitchen only after the pilot.'],
    ['What bread is for', 'The stall buys it for one coin.', 'A fixed buy at the shopkeeper; no W04 for it.'],
    ['The tú imperative', 'Comprehension of commands in the pilot; no assessment of imperative production.',
     'W09 leaves the pilot’s path; no imperative column; session 11 can be a2.'],
    ['Player needs (D-game-24)', 'Five needs: hunger, thirst, sleep, health and stamina. Hunger, thirst and sleep fall and have '
     'stages whose effects grow worse; they push health and stamina. Built after the follow-ups and before every other system, '
     'from session 1; real death with a cost; the meters pause in conversations, lessons, the book and every panel; stamina spent '
     'by gathering and a new sprint.', 'Section 9 specifies the N series; section 15 places it before the bread pilot.'],
]
OPEN = [
    ['1. The pilot protocol', '3 to 5 testers who completed sessions 1–10, or learners prepared for the same A2 material. Use a primer, three sittings on three days, two bakes per sitting, a frozen build and bundle, filtered checks and recall at least 24 hours later. The owner’s run checks functionality; the tester sessions provide the pilot evidence.', 'W11'],
    ['2. A listening gate', 'The owner hears Luz’s instruction lines, and decides whether `if_sara` stays, before audio-only '
     'understanding is measured.', 'W11'],
    ['3. The fold rule', 'Fold a chunk when every step reaches rung 4. Start its grouped instruction at rung 2 on the next bake. Use two spaced bakes per sitting for the pilot; section 10.11 gives the expected progression.', 'W06'],
    ['4. Act evidence (amends D-game-10)', 'Acts fade instruction rows only; Answered only when right first time without help at rung '
     '2 or 3; never production, transfer or noun credit.', 'W06'],
    ['5. The recipe', 'Six actions including oven fuel; a seeded order for flour and water; verb choices only between distinct actions; one bonus loaf for at least five first-attempt successes. Help remains available without reducing the food reward.', 'W05, W10'],
    ['6. Session 11 and flour', 'Session 11 at a2 (it follows the a2 sessions 7–10, and sacar is A2). Flour sold at the stall (a fixed '
     'sell, so no W04 in the pilot) rather than by Luz through W04.', 'W10'],
    ['7. A presentation track', 'U01 and U02 before the pilot; U03 and U04 after the last recall.', 'the pilot’s look'],
    ['8. The rest of D-game-23', 'Resolve the W-series options in activities-plan section 12: setting and scope, cast, one-click mastery, item migration, staffing, price-line delivery and instruction wording. The mechanics in sections 9 and 10 define needs, station behaviour and the pilot recipe.', 'each W milestone'],
    ['9. Combat and other harm', 'Combat stays out (D-game-23 (c)). Health falls only from hunger and thirst: no fall damage '
     '(the player’s camp is on a cliff ledge) and no hazards for now.', 'N01, N04, W26'],
    ['10. The cost of death', 'Everything in the bag except the items that stack to 1 stays in a chest where the player died, until '
     'the next night; the learner record, lessons, objectives, placed blocks and the book are never lost (section 9.7).', 'N04'],
    ['11. Spanish for needs', '“Tengo hambre”, “Tengo sed” and “Tengo sueño”, the HUD’s own stage lines, answered by Luna and '
     'Luz (a daily gift of bread or water; where to sleep), after a core intent for needs (section 9.11).', 'after N01'],
    ['12. The pyramid’s middle layers', 'N06 (berry bushes grow back after a night; free bread and water once per in-game day each) '
     'and N07 (cooking at the fire), then the needs check, before the bread procedure (section 1, build order).', 'N06, N07'],
    ['13. Conditions: disease, unsafe water and food', 'Not in the needs base. If added later, a condition is a status with one '
     'clear cause, one clear cure and visible symptoms, never a meter; remedies are fictional; session 1’s river water stays '
     'safe. Wait until the needs check shows the base is fun.', 'after the needs check'],
    ['14. Self-sufficiency at the camp', 'Optional N08: plant a berry near the camp to establish a bush using N06’s regrowth rules. Include it before the needs playtest only if accepted. Shelter that improves sleep belongs to building work after the pilot.', 'N08; later'],
    ['15. Expansion', 'Inside the valley for now: D-game-22 grows the village inward and the brief has no extra biomes. Ambition '
     'comes from projects and the places already in the scene (the lake, the waterfall, the mountains). New regions, outposts and '
     'other peoples need a new decision after the pilot.', 'W22 onward'],
    ['16. Learners who already know Spanish', 'A starting session the learner chooses, from a menu on `--skip-to-session`’s rules, '
     'rather than automated placement (the brief’s risk 6; D-game-10). Today the day moves only after each session’s work.',
     'a later milestone'],
    ['17. A relaxed needs option', 'One setting, “relaxed needs”: half the rates; at zero health the player faints and wakes at the '
     'camp without the chest. Real death stays the default (D-game-24).', 'N05'],

    ['18. Needs edge cases', 'Set the gathering stamina cost; nap and collapse hunger/thirst floors; whether full players can eat for healing; and the handling of repeated deaths, occupied chest sites and returning to camp before chest expiry. Preserve the no-dead-end rule and save every persistent state.', 'N01 to N04'],
    ['19. Accessible instruction evidence', 'Keep text and keyboard recovery available. Report reading and listening separately; decide how muted play and text fallback affect instruction fading and mastery before implementing RecordAct.', 'W06, W07'],
    ['20. Ingredient grants during lessons', 'Keep the daily grant for repeat bakes, with a recoverable route for the introductory lesson after a grant is used. Define which lesson state permits replenishment and prevent repeated requests or reloads from duplicating it.', 'W08, W10'],
]
expect(275, 'Capture a station run')
put_after(275,
          h2('Implementation responsibilities'),
          table([2300, 8068], ['Milestone', 'Required scope'], [
              ['N01 to N07', 'Needs, consumption, sleep, death, the HUD, regrowth and daily gifts, and cooking at the fire (section 9). N01 owns save version 6 and the systems map. W01 supplies item_crafted before N07.'],
              ['W05', 'Procedure, station, action and progress schemas; stable ids; cross-reference validation (10.7).'],
              ['W06', 'ProcedureRunner; act evidence and spacing; folding; the systems.procedures save entry; atomic completion and ReadyToCollect (10.10 to 10.15).'],
              ['W07 and W08', 'An overlay station with complete keyboard access and speech recovery; Luz’s teaching state and a daily ingredient grant (10.8 and 10.14).'],
              ['W10', 'Session 11 at A2; six actions, two chunk rows and one goal row; flour sold and bread bought at the stall; normal and slow recordings (section 10).'],
              ['W11', 'Three-sitting pilot, filtered checks and delayed recall (15.3). Runs r27 to r29; --sleep advances a completed day.'],
              ['W12 to W21', 'Items as data (W12), wear (W13), repair (W14), cooked dishes and buffs (W15), stage checks (W16), directions (W17), farming (W18), orders (W19), prices (W20) and stage checks (W21).'],
              ['U01 to U04', 'Presentation work specified in sections 6, 7 and 14; the timing is in 15.2.'],
          ]),
          p('Milestone registration and delivery status belong in docs/game/implementation-plan.md. Record the decisions required by each milestone before implementation (section 18).'),
          page_break(),
          h1('18. Owner decisions'),
          lead('Confirmed direction is listed first. Open decisions include recommendations and the milestones that depend on them; recommendations remain proposals until accepted.'),
          h2('Confirmed direction'),
          table([2300, 4268, 3800], ['Question', 'The owner’s answer', 'What follows'], DECIDED),
          spacer(),
          p('Recorded as D-game-23 (in part) and D-game-24 in `docs/game/decisions.md`.'),
          h2('Open, with recommendations'),
          table([2400, 6168, 1800], ['Decision', 'Recommendation', 'Blocks'], OPEN))


for i, old, new in [(115, '9. Cooking', '10. Cooking: prove the loop at Luz’s bakery'),
                    (137, '10. Crafting', '11. Crafting: from instant recipes to workshop processes'),
                    (152, '11. Generalise', '12. Generalise the pattern across the game'),
                    (167, '12. Learner-model', '13. Learner-model integration'),
                    (180, '13. Visual and UI', '14. Visual and UI adaptation'),
                    (195, '14. Recommended', '15. Recommended adaptation sequence'),
                    (259, '17. Design summary', '16. Design summary'),
                    (263, '18. Source basis', '17. Source basis and implementation checks')]:
    set_text(i, old, new)
set_text(275, 'Capture a station run',
         'Capture a station run, its repeat and help path at supported text scales. Add event/report fields for instruction ID, '
         'support rung, first-try success, help, repetition, completion and transfer; compare delayed recall with the existing '
         'playtest/check flow. The numerical pilot targets in section 15 come from the proposed W11 gate and require agreement and '
         'measured results.')

NEEDS_SECTION = [
    h1('9. Player needs: hunger, thirst, sleep, health and stamina'),
    lead('Hunger, thirst and sleep create the need to find food, water and rest. Their stages affect stamina and health. Build this foundation before procedural cooking and the wider activity systems.'),
    callout('FIVE NEEDS, IN TWO KINDS', [
        '**Hunger, thirst and sleep are drives.** Their meters fall during active play. Four severity stages progress from a warning to reduced stamina, blocked passive health recovery and, finally, hunger or thirst damage or a sleep collapse.',
        '**Stamina and health are conditions.** Sprinting and gathering spend stamina; its capacity and recovery depend on the drives. Health reaches zero only through prolonged hunger or thirst. Sleep affects stamina and passive healing but causes no direct damage.',
        'Stage names, icons, numerical values and HUD treatments use Vocaterra’s own visual identity (D-game-14).'], 'gold'),
    spacer(),
    h2('9.1 Scope and guardrails'),
    bullet('Build N01 to N07 after the queued language follow-ups, with W01 before N07, and pass the needs playtest before the bread pilot. Optional camp planting depends on decision 14.'),
    bullet('D-game-24 defines five needs, stages of increasing severity, death with a cost, and stamina spent on gathering and sprinting. All need changes pause during conversations, lessons and panels.'),
    bullet('Every need has a route without Spanish: berries by hand, water at the river, lake or well, and sleep at the camp. A language error or recognition failure never damages the player or blocks these routes (D-game-8, D-game-9).'),
    bullet('**Never lost, even in death:** the learner record and the book (D-game-16), lessons and their progress, objectives and '
           'orders, placed blocks, and the items that stack to 1 (the book, the axe).'),
    bullet('**Still out:** combat (D-game-23 (c), proposed). Health falls only at the last stage of hunger or thirst.'),
    h2('9.2 The five needs, and the time they run on'),
    p('Starting values, kept in data (`game/needs.json`) and tuned in the needs check. **Active play** is time with the world running '
      'and no panel open: no conversation, lesson panel, book, inventory, crafting, station, report or menu.', keep_next=True),
    table([1700, 1250, 2850, 2550, 2018], ['Need', 'Kind', 'Falls', 'Restored by', 'At its end'], [
        ['Hunger (el hambre)', 'drive; required', '1 every 36 s of active play: 100 to 0 in about 60 minutes; 1.5 times as fast '
         'while sprinting or gathering', 'eating (9.6)', 'starving: health falls'],
        ['Thirst (la sed)', 'drive; required', '1 every 24 s: about 40 minutes; 1.5 times as fast while sprinting or gathering',
         'drinking (9.6)', 'dehydrated: health falls faster'],
        ['Sleep (el sueño)', 'drive', '1 every 72 s: about two hours; 1.25 times as fast while sprinting or gathering',
         'sleeping (9.5)', 'exhausted: a collapse'],
        ['Stamina (la energía)', 'condition', 'sprinting, 10 a second; gathering, a cost per gather tick (the axe, a quarter of the '
         'ticks, costs a quarter)', 'refills by itself, as fast as the drives allow (9.4); a sleep fills it',
         '0: no sprint; gathering at half speed, never blocked'],
        ['Health (la salud)', 'condition', 'only at the last stage of hunger or thirst', 'recovers by itself while no drive is at '
         'stage 3 or 4; rest at a fire, sleep and cooked food (9.4)', '0: death (9.7)'],
    ]),
    spacer(),
    bullet('**Design targets:** during moderate activity, drinking roughly every 15 minutes, eating every 25–30 minutes and sleeping every one to two hours. Starting with full needs, a full water bottle and two loaves should support a half-hour trip. Validate this against effort rates; continuous work consumes supplies faster.'),
    bullet('**Progress reduces maintenance.** Renewable food, better meals, baking, farming and repair reduce the time spent meeting basic needs. The needs playtest measures this share of active play: the initial target is 20% or less in sessions 1 to 3, decreasing as capabilities grow.'),
    bullet('**Time:** the needs count active-play ticks themselves. The world’s ticks keep running under the dialogue, the book, the '
           'inventory and crafting (only the check battery and the F8 report stop them, `VoxelGame.cs:546–552`), and stored time '
           'counts them all, so the session tells the needs each tick whether play is active. Nothing drains while the game is closed '
           'or paused (F8, the menu, any panel, speech recording and processing); a load restores the saved needs exactly; a night, a '
           'nap or a collapse applies its change once.'),
    h2('9.3 The stages'),
    p('Evaluate stages from the current meter: healthy at 75–100; stage 1 at 50 ≤ value < 75; stage 2 at 25 ≤ value < 50; stage 3 at 0 < value < 25; stage 4 at 0. Recompute the stage after every change, so recovery to exactly 25, 50 or 75 removes the more severe stage. Clamp meters to 0–100 and stamina to its current maximum. Each stage replaces the previous stage’s effects. The HUD shows its Spanish line with English support (9.9).',
      keep_next=True),
    table([1150, 1050, 1700, 2200, 4268], ['Need', 'Stage', 'English', 'Spanish', 'Effects'], [
        ['Hunger', '1: below 75', 'Slightly hungry', 'Tengo un poco de hambre', 'The icon appears; nothing else.'],
        ['Hunger', '2: below 50', 'Hungry', 'Tengo hambre', 'Stamina’s maximum −15; it refills at 0.85 of its speed.'],
        ['Hunger', '3: below 25', 'Very hungry', 'Tengo mucha hambre', 'Maximum −30; refill at 0.7; health stops recovering.'],
        ['Hunger', '4: at 0', 'Starving', 'Me muero de hambre', 'Maximum −45; refill at 0.5; no sprint; walking at 0.9; health −1 '
         'every 6 s (full to nothing in 10 minutes).'],
        ['Thirst', '1: below 75', 'Slightly thirsty', 'Tengo un poco de sed', 'The icon appears; nothing else.'],
        ['Thirst', '2: below 50', 'Thirsty', 'Tengo sed', 'Maximum −20; refill at 0.8.'],
        ['Thirst', '3: below 25', 'Very thirsty', 'Tengo mucha sed', 'Maximum −35; refill at 0.6; health stops recovering; the '
         'edges of the view dim.'],
        ['Thirst', '4: at 0', 'Dehydrated', 'Me muero de sed', 'Maximum −50; refill at 0.4; no sprint; walking at 0.85; health −1 '
         'every 4 s (full to nothing in under 7 minutes).'],
        ['Sleep', '1: below 75', 'Sleepy', 'Tengo un poco de sueño', 'The icon appears; nothing else.'],
        ['Sleep', '2: below 50', 'Tired', 'Tengo sueño', 'Maximum −10; refill at 0.8.'],
        ['Sleep', '3: below 25', 'Very tired', 'Tengo mucho sueño', 'Maximum −25; refill at 0.6; health stops recovering; now and '
         'then the view blinks.'],
        ['Sleep', '4: at 0', 'Exhausted', 'Me muero de sueño', 'Maximum −40; refill at 0.5; no sprint; walking at 0.85; after 60 s '
         'more of active play, a collapse (9.5).'],
        ['Health', 'below 50', 'Weak', 'Estoy débil', 'The heart pulses, with a soft heartbeat.'],
        ['Health', 'below 25', 'Very weak', 'Estoy muy débil', 'No sprint; walking at 0.9; the edges of the view redden.'],
    ]),
    spacer(),
    p('The Spanish follows one pattern a learner can use: “un poco de”, nothing, “mucha” or “mucho”, and “me muero de”, with hambre, '
      'sed and sueño. It carries an agreement point (mucha hambre and mucha sed, feminine; mucho sueño, masculine) and the lines a '
      'learner needs to ask for help (9.11). Stamina has no stages: at 0 the player is out of breath.'),
    h2('9.4 How the needs link'),
    p('The drives push the two conditions; effort pulls the drives. The links are the whole of the system, so each is a rule with '
      'its numbers in `needs.json`.', keep_next=True),
    bullet('**Stamina’s maximum** is 100 minus the penalties of the three drives’ stages, never below 20. It refills 15 a second after '
           '1 s without effort, times the three drives’ refill factors, never below a quarter of that; twice as fast while resting at a '
           'lit fire.'),
    bullet('**Health** falls only at the last stage of hunger (1 every 6 s) or thirst (1 every 4 s); with both, the losses add (full '
           'to nothing in 4 minutes). It recovers 1 every 12 s while no drive is at stage 3 or 4, twice as fast while resting at a lit '
           'fire; a night adds 20, and cooked food heals at once (9.6).'),
    bullet('**Sleep never costs health itself.** It drags the others down (a smaller and slower stamina; no recovery of health from '
           'stage 3), and at its last stage the player collapses (9.5).'),
    bullet('**Effort:** sprinting and gathering make hunger and thirst fall 1.5 times as fast and sleep 1.25 times as fast.'),
    bullet('**Walking** slows only at the drives’ last stages and at very weak health. The slowest factor in force applies; they never '
           'multiply, so the player can always walk to water.'),
    bullet('**Rest:** standing still within 3 blocks of a lit fire doubles stamina’s refill and health’s recovery. A lit fire is the '
           'player’s own fire once its torch is on the rim (the only lit state today, `VoxelSession.cs:199`) and the forge, whose '
           'post-torch always burns; N02 gives the other fire rings the same lit state. Rest does not restore sleep; only sleeping '
           'does.'),
    p('Example: starting from full meters with no sprinting or gathering, after 25 minutes without drinking, 35 without eating and 70 without sleeping, all three drives are at stage 2. Stamina’s maximum is 55 (100 − 20 − 15 − 10); its refill factor is 0.544 (0.8 × 0.85 × 0.8). Passive health recovery remains available. Meeting a need removes its penalty; stamina then refills up to the new maximum.'),
    callout('NO DEAD ENDS', [
        'Warnings precede health loss, and basic recovery remains available without Spanish.',
        '**Warning time.** Without effort, the first thirst warning precedes damage by about 30 minutes; the first hunger warning by about 45. Continuous sprinting or gathering shortens those intervals to about 20 and 30 minutes. Test both cases.',
        '**The middle stages cost stamina and recovery, never health,** and walking never stops: the slowest factor in force is '
        '0.85.',
        '**Recovery routes.** Drink by hand, gather berries and nap at the camp. Daily gifts provide another source of food and water. Death returns the player to the camp; the proposed belongings chest is specified in 9.7.',
        '**Reachability check.** Starting at full health with both hunger and thirst at stage 4, a player at the farthest walkable point must reach water before death. At 0.85 walking speed, four minutes covers about 880 blocks on level ground; test actual routes, slopes and obstacles (9.13).',
        '**Every death has a named cause:** the event and the death screen say whether it was hunger or thirst.'], 'blue'),
    spacer(),
    h2('9.5 Sleep'),
    bullet('**A night.** Once the day’s work is done (`VoxelSession.SessionWorkDone`), E at the player’s lit camp fire or Enter on the closing card sleeps until morning (`SleepOffer`). Advance the day and sitting, then begin the next lesson. Sleep applies fixed need changes rather than elapsed-time decay. `--playtest` supports one lesson session per run, so use ordinary play for the multi-day needs playtest.'),
    bullet('**A night** sets sleep to 100, adds 20 health up to 100, and subtracts 15 hunger and 20 thirst without reducing a drive below 10. Recompute stage penalties before filling stamina to its effective maximum. The treatment of a drive already below 10 is part of decision 18.'),
    bullet('**A nap (new).** Before the day’s work is done, a tired player (stage 2 or worse) can still sleep: E at their lit fire offers “sleep a while; the lesson waits”. A nap sets sleep to 75 and fills stamina; it costs 10 hunger and 10 thirst and gives no health. The day, the sitting and the lesson do not move, so a nap never skips a lesson (V12c’s rule) and Spanish never keeps a tired player from sleep (9.1).'),
    bullet('**A collapse.** After 60 s of active play at the last stage, the screen fades and the player sleeps where they stand: sleep to 50, stamina full, no health gain, and a cost of 15 hunger and 20 thirst. It counts as a night if the day’s work is done (the day moves) and as a nap if not. The player wakes where they fell; the tip explains it the first time.'),
    bullet('**The day’s resets come with a night,** when the day moves: picked bushes grow back and gifts return (N06), and a chest '
           'left by a death goes (9.7).'),
    bullet('**Sittings and days.** A completed night or a collapse after the day’s work advances both counters. A nap or pre-completion collapse advances neither. A launch or load starts a sitting without advancing the day. Preserve D-game-6 spacing; `--sleep` is the scripted aid for a night.'),
    bullet('**No day–night cycle.** The renderer has none (only the `--dusk` look and its evening sounds), and a night is the '
           'session’s boundary, not a clock. An evening look at bedtime would be presentation work (the U series), not a need.'),
    h2('9.6 Eating, drinking and sprinting'),
    bullet('**Eat:** select a food in the hotbar and right-click, as the book is used today (food cannot be placed, so right-click is '
           'free). At full hunger the food is kept and a line says so. World event `meal_eaten`, detail the food’s noun.'),
    bullet('**Drink:** right-click with the water bottle (it becomes the empty bottle), or press E while aiming at water within reach '
           '(the river, the lake, the well): a drink by hand, the route without Spanish and without a bottle. World event '
           '`water_drunk`, detail `botella` or the place. Session 1’s objective still needs the bottle filled, and its hint for water '
           'without a bottle stays.'),
    bullet('**Sprint:** a new control, `sprint` (Left Shift by default), walks about 1.6 times as fast while stamina lasts and no '
           'stage forbids it. Scripted runs never hold it, so their walking is unchanged.'),
    bullet('**Food values are data** (`game/needs.json`: per item, its hunger, its thirst, its healing and what it leaves), so needs '
           'do not wait for W12 (items as data); W12 later moves them into each item.'),
    bullet('**Cooking at the fire (N07):** E at a lit fire ring opens the crafting panel with the fire’s recipes, as E at the workbench '
           'does. A catalogue noun, fuego, for the fire-ring block makes the fire a station; the roast apple is listed under the noun '
           'manzana, as agua covers the bottle of water, and its label adds “asada”.'),
    p('**What food is worth.** Only cooked food heals, raw food stays the free base within reach, and cooking improves bought ingredients; gathered berries remain free.', keep_next=True),
    table([3400, 2000, 2600, 2368], ['Source', 'Gives', 'Costs', 'Note'], [
        ['A berry (fruta), picked by hand', 'hunger +8', 'nothing', 'a picked bush grows back after the next night (N06)'],
        ['An apple, from León', 'hunger +12', '1 coin', '—'],
        ['A roast apple, at any lit fire (N07)', 'hunger +20, health +5', 'an apple', 'the first cooking'],
        ['Bread, from Luz', 'hunger +25, health +5', '2 coins', '—'],
        ['Bread, baked with Luz (section 10)', 'two loaves', 'flour (1 coin), water and one wood', 'a quarter of Luz’s price in coins'],
        ['Water by hand, at the river, the lake or the well', 'thirst +10', 'nothing', 'always there'],
        ['The water bottle', 'thirst +40', 'a refill at the river or the well', 'the bottle is kept'],
        ['A gift: bread at the stall or from the villager, water from Luna', 'the item', 'a request in Spanish',
         'once per in-game day each (N06); a lesson’s gift is always given'],
        ['A loaf sold at the stall', '1 coin', 'a loaf', 'eat for recovery or sell a surplus'],
    ]),
    spacer(),
    p('N06 provides a renewable food supply: berry bushes regrow after a night, and each designated NPC offers one gift per in-game day. Lesson-required gifts remain available during their lesson steps.'),
    h2('9.7 Death and its cost'),
    bullet('**At zero health the player dies.** The screen fades, and the player wakes by their own fire at the cliff camp, the one place to sleep, with health and the three drives at 50, and stamina at its effective maximum. World events `player_died` (detail: hambre or sed) and `player_woke`. A death is not a night: the day does not move.'),
    bullet('**The cost (recommended; the owner confirms, decision 10):** everything in the bag except the items that stack to 1 stays '
           'where the player died, in a chest (the existing `Chest` block) on the nearest free ground cell. The beacon can point to it '
           '(“tus cosas”): a directions moment. E at the chest takes back what the bag can hold. At the next night the chest '
           'disappears with whatever is left in it.'),
    bullet('**A lesson, an order or an objective in progress continues where it was.** Death cannot happen in a conversation, a '
           'lesson, the station or a menu, because the meters stop there.'),
    h2('9.8 From session 1'),
    bullet('**The needs start full when the player wakes at the cliff camp:** fed, watered and rested. An English tip at the first '
           'stage explains them (`game/tips.json` and its locale): “Hunger, thirst and sleep go down as you walk and work. Eat berries '
           'or bread, drink water from the river or your bottle, and sleep at your camp when you are tired.”'),
    bullet('**Opening acceptance.** The camp objectives introduce berries and water before deprivation becomes dangerous. Verify that the session-1 route can be completed safely at the intended pace, including a slower novice pace. Record active time, effort and recovery actions; completing the objectives alone is not a guarantee against death if the player delays indefinitely.'),
    bullet('**The lessons do not change:** sessions 1–10 keep their steps and lines, and their phrases gain a reason: “Necesito '
           'agua.” and “Tengo una fruta.” in session 1, then the apples and the bread of the village.'),
    bullet('**The stage lines are written Spanish in the HUD,** text only with no recordings (the player has no voice): the player’s '
           'own words, such as “Tengo sed.”, “Tengo mucha hambre.”, “Me muero de sueño.” and “Estoy débil.”, with the English beneath '
           '(D-game-13). They are what the learner can say to someone who can help (9.11). The lint holds them to the A2 ceiling '
           '(hambre is A1; sed and salud are A2); check sueño, débil and morir there before the content lands.'),
    h2('9.9 The HUD and sound'),
    bullet('**Health and stamina** are two thin bars beside the hotbar. Stamina shows only while it is below its maximum, and a dashed '
           'end shows how much the drives have cut that maximum, so the link is visible.'),
    bullet('A drive icon appears at stage 1: a loaf, drop or moon drawn in code, with one to four severity notches. It disappears on recovery. With healthy drives and full stamina, only the health bar remains visible.'),
    bullet('**A stage change** shows its line once above the hotbar (“Tengo sed.” with “I’m thirsty.” beneath) and plays a soft '
           'sound (CC0 or synthesized, D-game-18) that ducks under the voices. The inventory lists the five needs with their stages in '
           'Spanish and English.'),
    bullet('**The effects on the view** (dim edges for thirst, a blink for sleep, red edges for very weak health) are gentle and never '
           'flash; an option turns them off and leaves the icons and the lines.'),
    bullet('**Room on screen:** the bars and icons never cover the hotbar, the objective tracker or the tip box (the layout tests at '
           'every canvas and scale).'),
    bullet('**Death:** a fade, then a short screen in Spanish with its English: “Estás en tu campamento.”'),
    h2('9.10 Data, code and saves'),
    bullet('**Data:** `content/game-v1/game/needs.json` with `content/schema/needs.schema.json`: per drive its rate, its effort factors '
           'and its four stages (the point, the line’s key and the effects: the stamina penalty, the refill factor, walking, sprint, '
           'health); health’s recovery and warning stages; stamina’s costs and refill; rest; a sleep’s and a collapse’s effects and '
           'the collapse delay; the foods (item, hunger, thirst, healing, what it leaves); the regrowth and gift rules (N06); the death '
           'rules (where the player wakes, the values on waking, what stays in the bag, the chest’s lifetime). Loaded by `GameData`; '
           '`GameDataValidator` checks that every food is an item, every rate is positive and the stages fall in order. The lines '
           'live in `game/words.json` and `content/locale/en`.'),
    bullet('**Game.Core:** a new folder, `Needs/`, with `PlayerNeeds`: plain state for the five needs, with `Step`, `Eat`, `Drink`, '
           '`Sleep`, `Collapse`, `Die` and `Wake`, and `StageOf` and `Effects` (the stamina maximum and refill, walking, sprint, the '
           'health change) computed from the stages, never stored. It reads stored time from the host, no clock, and draws no '
           '`System.Random`. The wiring goes in a new partial, `Session/VoxelSession.Needs.cs` (as `VoxelSession.Sleep.cs`), with the '
           'eat and drink actions and the nap; the sprint and the walking factor scale the player’s walk speed (294 units a tick, '
           'about 4.3 blocks a second, `Physics/VoxelPhysics.cs:32`). The game tells the session each tick whether play is active.'),
    bullet('**Game (MonoGame):** the bars and icons (`UI/Hud/`), the icons in the atlas, the view effects and their option, the death '
           'fade and the sprint key (`Input/`).'),
    bullet('**World events,** each joining `WorldEvents.All` and the schemas that list event names: `meal_eaten`, `water_drunk`, '
           '`need_stage` (detail: hambre, sed, sueño or salud; the stage itself goes in the save and the telemetry), '
           '`player_collapsed`, `player_died`, `player_woke`, `belongings_recovered`.'),
    bullet('**Save version 6 (N01).** Add an optional `systems` map to `SaveDocument`. The `needs` entry stores the five meters, the exhaustion timer, regrowth and gift state, and any belongings chest. Derive stage effects from the meters. Versions 1–5 initialise full needs; W06 adds `systems.procedures` without another format change.'),
    bullet('**The needs stay out of the world digest** (`VoxelSimulation.cs:333`), which a load and a night compare '
           '(`SaveGame.cs:300`, `353`): adding them would refuse every older save. A load checks them by range instead. A night is a '
           'load in the same run, so the `needs` entry passes through it with the night’s changes applied.'),
    bullet('**Scripted runs:** a test aid, `--needs off`, freezes the needs full so that the language runs keep their baselines; '
           '`--set-need <need> <value>` sets one. New runs: `r25-thirst` (the thirst stages, a drink by hand and from the bottle) and '
           '`r26-death` (a collapse from sleep, then death from thirst, waking at the camp, the chest taken back).'),
    h2('9.11 Spanish for needs'),
    p('Needs give Spanish a use without new lessons, and the HUD teaches the lines. “Tengo hambre.”, “Tengo sed.” or “Necesito agua.” '
      'said to Luna or Luz could earn a gift once a day (bread, water), and “Tengo sueño.” a reminder of where to sleep: a Spanish '
      'route beside the ones without Spanish. First check in the core’s CLI how “tengo hambre” and “tengo sueño” are read today '
      '(perhaps `state_have`); an intent for needs is a core milestone, and the replies are content, in each NPC’s voice. '
      'Recommended after N01, not before it (decision 11).'),
    bullet('**Spanish enables communication.** No need represents language ability, and no meter falls because of a wrong answer or a misheard line (D-game-9).'),
    bullet('**Routine actions never need Spanish:** walking, gathering, placing, eating, drinking by hand and sleeping. '
           'Communication and knowledge do: buying, asking for a gift, directions, recipes and help.'),
    table([1600, 4200, 4568], ['Need', 'Where Spanish enters', 'For example'], [
        ['Hunger', 'buying food, asking for it, quantities', '“Quiero dos manzanas.” “Tengo hambre.”'],
        ['Thirst', 'asking for water and where it is', '“Necesito agua.” “¿Dónde hay agua?”'],
        ['Sleep', 'time and rest', '“Buenos días.” “Tengo sueño.”'],
        ['Health', 'how the player feels', '“Estoy débil.”; body parts only with conditions (decision 13)'],
    ]),
    spacer(),
    h2('9.12 Milestones (proposed ids)'),
    table([2500, 5668, 2200], ['Milestone', 'Scope', 'Size; model'], [
        ['N01 The needs model', '9.2 to 9.4 and 9.10: the five needs, their stages and links, `needs.json` and its schema, save '
         'version 6 and the `systems` map, the events, `--needs off` and `--set-need`; engine-free, with the tests of 9.13',
         'M/L; Sonnet'],
        ['N02 Eating, drinking and sprinting', '9.6: the eat and drink actions, water by hand, the sprint control, the gathering cost, '
         'rest at a fire', 'S/M; Haiku'],
        ['N03 Sleep, the nap and the collapse', '9.5: a night’s effects on the needs, the nap at the fire and its prompt, the collapse and waking where the player fell; a night or a post-work collapse advances the day; loading starts a sitting without advancing the day',
         'S/M; Haiku'],
        ['N04 Health and death', '9.4 and 9.7: losses and recovery, death and the chest, waking at the fire; `r25-thirst` and '
         '`r26-death`', 'M; Sonnet'],
        ['N05 The HUD and the opening', '9.8 and 9.9: the bars, the icons and their stages, the lines, the sounds, the view effects '
         'and their option, the tip and the death screen; layout tests at every scale; the session-1 route proven safe',
         'S/M; Haiku'],
        ['N06 Food that grows back, and fair gifts', '9.6: a picked berry bush grows back after the next night; the stall’s and the '
         'villager’s bread and Luna’s water once per in-game day each (a lesson’s gift is always given); the rules in `needs.json` '
         'and the save', 'S/M; Haiku'],
        ['N07 Cooking at the fire', '9.6: the catalogue noun fuego and an instant recipe at any lit fire, an apple to a roast apple '
         '(hunger +20, health +5), through the crafting path that exists and W01’s `item_crafted`; the roast apple takes the next '
         'free item number', 'S/M; Haiku, then content'],
        ['The needs check', 'The owner and two or three testers play sessions 1 to 3 with needs on, in ordinary play (`--playtest` '
         'offers no sleep), so through two nights; the rates and stages are tuned; deaths, naps, collapses and their causes are '
         'noted; the go for the W pilot', 'the owner'],
        ['Later: Spanish for needs', '9.11: a core intent, then the gifts and the replies', 'a core milestone, then content'],
    ]),
    spacer(),
    p('The N series runs in this order: N01 to N05; N06; W01 before N07; N07; optional N08 if decision 14 is accepted; then the needs playtest. Sessions 1–10 retain their lesson steps. Existing language runs use `--needs off` where fixed needs are required; needs-enabled runs test the survival experience.'),
    p('**The needs playtest passes** when:',
      keep_next=True),
    bullet('the needs push testers to act without irritating them;'),
    bullet('every tester uses at least two ways to get food and two to get water;'),
    bullet('once they know it, testers roast an apple rather than eat it raw;'),
    bullet('no tester dies without being able to say why, and none is caught in a spiral they cannot leave;'),
    bullet('Spanish has a use: a gift asked for, food bought;'),
    bullet('the share of active play spent on the drives is about a fifth or less;'),
    bullet('familiar words come back across eating, buying and cooking (fruta, agua, pan, manzana).'),
    h2('9.13 Tests'),
    bullet('Each drive falls only in active play, at its rate and its effort factors; nothing falls while a conversation, lesson, '
           'book, inventory, crafting, station, report or menu is open.'),
    bullet('Boundary tests cover each threshold just below, exactly at and just above 0, 25, 50 and 75, on both decay and recovery. Stage effects, HUD and events agree with 9.3.'),
    bullet('Stamina’s maximum is 100 minus the three penalties, never below 20; the refill factors multiply, never below a quarter; '
           'at zero stamina gathering slows but never stops; no sprint at any drive’s last stage or at very weak health.'),
    bullet('Health damage adds when hunger and thirst are both at stage 4. Passive recovery requires every drive below stage 3; sleep and cooked-food healing follow their explicit rules. Rest doubles passive recovery and stamina refill.'),
    bullet('Check the night, nap and collapse effects against 9.5, including drive floors and stamina’s effective maximum. A collapse occurs after 60 active seconds at stage 4. A night or post-work collapse advances the day and sitting; a nap or pre-completion collapse does not. Test the edge cases resolved by decision 18.'),
    bullet('Eating and drinking restore and cap; at full hunger the food is kept; the bottle comes back empty; a drink by hand works '
           'with nothing held.'),
    bullet('Death at zero health: the chest holds the bag minus the items that stack to 1; the waking values; the learner record, '
           'lessons, objectives and placed blocks unchanged; the day unchanged; the chest gone at the next night.'),
    bullet('The session-1 route completes without death or collapse at the intended pace and a slower novice pace.'),
    bullet('No dead ends (9.4): from the farthest walkable point, at the last stages of thirst and hunger with full health, the '
           'walk to water is shorter than the health that is left.'),
    bullet('A picked berry bush grows back after the next night; a gift comes once per in-game day, except in a lesson step; a roast '
           'apple heals; nothing drains while the game is closed; a load restores the meters exactly.'),
    bullet('Determinism: the same inputs give the same meters, stages and events; saves round-trip; the migration from version 5; '
           'every version from 1 loads.'),
    page_break(),
]
expect(114, '')
put_after(114, *NEEDS_SECTION)

# ---------------------------------------------------------------- the survival-first proposal: sections 12, 13 and 16

expect(165, '')
assert 'w:type="page"' not in els[165][3]
put_after(165,
          h2('Progress that changes what the player can do'),
          p('Progress should unlock a capability or reduce routine effort: portable water, renewable food, better meals and longer-lasting tools.', keep_next=True),
          table([2700, 4668, 3000], ['Capability', 'What it removes or opens', 'Where'], [
              ['Water: the bottle, and drinking by hand', 'thirst anywhere near water; a bottle for longer trips',
               'the bottle is built (session 1); drinking, N02'],
              ['Food that grows back, and fair gifts', 'renewable berries without coins or Spanish; gifts through conversation', 'N06'],
              ['Cooking at the fire', 'more food from the same apple, and healing', 'N07'],
              ['Baking with Luz', 'cheap bread, and a procedure whose guidance fades', 'section 10 (the W pilot)'],
              ['Farming', 'food the player grows near the camp', 'W18; a first bush, decision 14'],
              ['Repair', 'tools that last', 'W14'],
              ['Trading', 'coins for what the player cannot make', 'built; W04'],
              ['Better rest', 'a better night in a sheltered camp', 'decision 14'],
              ['Spanish', 'help, recipes, directions and trade without the English', 'the learner model (section 13)'],
          ]),
          spacer())

put_after(178,
          h2('Exposure, comprehension, supported and independent production'),
          p('Keep exposure, demonstrated comprehension and production separate:',
            keep_next=True),
          table([2600, 5268, 2500], ['Level', 'Evidence in the game', 'Where'], [
              ['Exposure', 'a word or phrase presented in audio or text; exposure counts record presentation, not demonstrated recognition',
               '`Language/Words/WordProgress.cs`'],
              ['Comprehension', 'acting correctly on what was heard or read: the check battery; at the station, an instruction acted on right first time without help (decision 4)', '`CheckBank`; section 10.10'],
              ['Supported production', 'a right use with the English or the card shown (the first two rungs)',
               '`Language/Phrases/PhraseProgress.cs`'],
              ['Independent production', 'a right use at audio only or with nothing shown, without help; transfer when it is '
               'used in a new place', '`PhraseProgress.cs` (unaided, transfer)'],
          ]),
          p('Correct use reduces support after its spacing gap, except the immediate rung-1 transition. Help restores support immediately (D-game-6). The book should show these levels in words rather than its raw counters (section 8), and never as a score (D-game-10).'))

expect(262, '')
put_after(262,
          h2('Design principles'),
          p('Survival motivates action, activities provide solutions, progress increases independence, and language enables deeper interaction with the world. Apply these principles:', keep_next=True),
          num(1, '**Survival is the foundation.** Needs give reasons to gather, cook, craft, build, talk and explore.'),
          num(2, '**Systems interact.** A new mechanic feeds another: bread feeds the player, the fire cooks, coins buy flour.'),
          num(3, '**Preparation pays.** Water and food carried before a long walk are rewarded (section 15’s preparation measure).'),
          num(4, '**Self-sufficiency lowers pressure.** Each layer cuts the time spent on the ones below (section 9.2).'),
          num(5, '**Spanish is practical.** It gets knowledge, goods, help and services; it is never a meter.'),
          num(6, '**Learning is contextual.** Words recur across activities: fruta, agua and pan; echar and mezclar.'),
          num(7, '**Failure is understandable and recoverable.** Warnings before harm, a named cause, and nothing of the learner '
                 'lost.'),
          num(8, '**Learning is never punished.** A wrong answer or a misheard line costs nothing in needs.'),
          num(9, '**Realism serves play.** Five needs; no nutrition model and no spoilage; conditions only with a clear cause and '
                 'cure.'),
          num(10, '**Freedom in the valley.** Several ways to meet each need, and projects for their own sake.'),
          num(11, '**Small steps.** Prove how two systems interact before deepening either: the needs check before the pilot.'),
          h2('Risks and how this design answers them'),
          table([2400, 4968, 3000], ['Risk', 'The answer here', 'Measured by'], [
              ['Survival becomes maintenance', 'moderate rates; pressure that falls with each layer; no penalty counted twice',
               'the needs check: time on the drives; testers’ ratings'],
              ['A spiral the player cannot leave', 'stages with warnings; walking never stops; free water and food; no dead ends '
               '(section 9.4)', 'its test; deaths and their causes'],
              ['Spanish becomes optional', 'buying, gifts, recipes, directions and help need Spanish; the day moves only after the '
               'day’s lesson work (V12c)', 'gifts asked for; conversations per hour'],
              ['Spanish interrupts survival', 'routine actions need none; the meters stop in every conversation and panel',
               'testers’ comments; the panel tests (section 9.13)'],
              ['Too complex a simulation', 'five needs; disease, nutrition and spoilage left out (decision 13)',
               'the N series’ scope'],
              ['Expansion lacks purpose', 'capabilities and projects inside the valley; new regions only by a later decision '
               '(decision 15)', 'after the pilot'],
              ['Systems drift apart', 'shared flows: food, water, coins, fire and words', 'the pilot’s transfer measure (section 15)'],
              ['Punishment undermines learning', 'no needs cost for a language error; a misheard line never blocks food or water '
               '(D-game-9)', 'tests: no meter change during speech'],
          ]),
          spacer())

# ---------------------------------------------------------------- assemble

gaps = []
out = [x[:els[0][1]]]
prev = els[0][1]
for i, (kind, s, e, xml, text) in enumerate(els):
    gap = x[prev:s]
    if gap.strip():
        gaps.append((i, gap[:80]))
    out.append(gap)
    out.append(replace.get(i, xml))
    out.extend(after.get(i, []))
    prev = e
out.append(x[prev:])
new = ''.join(out)


def number_subheadings(doc):
    """Prefix every Heading2 with N.k (N from the Heading1 above it), checking the ones that already carry a number."""
    _, els_, _ = body_elements(doc)
    out_, last, n, k, added = [], 0, None, 0, 0
    for kind, start, end, xml, text in els_:
        head = xml[:400]
        if 'w:val="Heading1"' in head:
            n, k = int(text.split('.')[0]), 0
        elif 'w:val="Heading2"' in head and n is not None:
            k += 1
            want = f'{n}.{k}'
            m = re.match(r'^(\d+\.\d+) ', text)
            if m:
                assert m.group(1) == want, (text[:40], want)
            else:
                new_xml = re.sub(r'(<w:t(?: [^>]*)?>)', lambda t: t.group(1) + want + ' ', xml, count=1)
                assert new_xml != xml, text[:40]
                out_.append(doc[last:start])
                out_.append(new_xml)
                last = end
                added += 1
    out_.append(doc[last:])
    print('subheadings numbered:', added)
    return ''.join(out_)


new = number_subheadings(new)
open(DST, 'w', encoding='utf-8').write(new)
print('elements:', len(els), 'replaced:', len(replace), 'insert points:', len(after),
      'inserted blocks:', sum(len(v) for v in after.values()), 'non-empty gaps:', gaps)
print('size:', len(x), '->', len(new))
