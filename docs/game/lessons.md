# Lessons and sessions

How the game teaches: the English guidance, the guide's lesson, verbs and tenses, the village's sessions 4 to 10, sleeping into the
next session, and the summary card with its second try. Each section names the milestones that built it.

## Lessons are optional classes (L01, D-game-25)

L01 supersedes every rule below that says a lesson "starts by itself", "unlocks on a play session" or holds the night, an order or the beacon. The rules now:

- **One required introduction.** `first-words` is Luna's first meeting on day 1: units u1, u2, u3, u5 and u6 (hello, yes and no, what is this, no sé, otra vez, más despacio),
  25 steps, then the camp task. It alone has `autoStart` and Luna's greeting within 5 blocks (`GuideApproach`). Units u4 and u4v (please, thanks, I want, I have, I need; 12 steps)
  are **Luna's first class**, the lesson `first-class` (`after: first-words`, `autoStart: false`), with every step and line as they were. The camp objectives are unchanged.
- **Every other lesson is a class.** Nothing starts a class by itself. When the player talks to a teacher with a class open, the teacher adds one line, the offer
  (`guide.lesson.<npc>.offer.1`, «¿Quieres una clase? ¿Sí o no?»), beside the usual greeting and trade prompt. «Sí» (or «claro», «vale»...) starts the class; «No», «No, gracias» and
  walking away cost nothing: the offer is dropped, the NPC's prompt is where it was, and the teacher does not ask again that day. Any other line withdraws the offer, so a later «sí» is
  the NPC's own question's. The player can also ask: «¿Me enseñas?» (the core has no intent for it, so `game/words.json` `classAnswers` lists the plain forms the game reads from the
  turn's text; a core gap, reported) or the Lessons page's **Ask for a class** button (P key), which sends the beacon to the teacher and starts the class on the next talk. A class owns
  the conversation only once accepted.
- **Esc pauses.** Closing the panel on a class at a talk step pauses it and frees its NPC; a class at a `do` step (walk to the bush, the stall) stays engaged until the world event it waits for,
  and holds nothing back. A paused class holds back no sleep, trade, order or delivery; the teacher offers it again, or the Lessons page asks for it, and it resumes where it stopped.
  A save never keeps a lesson engaged: a loaded class is paused (versions 1 to 6).
- **What opens a class:** having met its teacher (`GuideSession.HasMet`: the panel opened on them once; saved as `metNpcs`, and for a save from before L01 every NPC counts as met from
  day 2) and its `after` lesson done where the grammar needs it (the preterite before session 8's retelling). Never the day: a lesson's `session` only orders the Lessons page and the
  skip aid. A teacher with several classes open offers the first of the series that is not done.
- **Sleep.** Day 1 keeps its ending (the introduction, the camp, the fire; `SessionComplete`). From day 2 the lit fire offers sleep whenever the sleep drive is below 75
  (`VoxelSession.SleepWanted`); above it E there shows the player's line «No tengo sueño.» with "I'm not sleepy." beside it, text only. No lesson, class, objective or order holds sleep back
  (`CanSleep` is a lit fire and a session at rest). The test aids `--sleep` and the scripted sleep step sleep whatever the drive says (`--needs off` keeps it full).
- **Orders** show once their NPC is met (`VoxelSession.Shown`), not on a day; no lesson holds a delivery (`HoldsOrder` is gone, and the oven order no longer waits for `n4-back`: a learner
  who carries the wood is paid at the first talk, and a class taken late finds the order done: its `do` step ends "delivered before"). The order's `session` field now only says
  when `--skip-to-session` delivers it.
- **Cards:** every scaffold phrase has its card from rung 1, whether or not a class taught it.
- **The beacon and the objective line** follow the tracked objective; neither names a class or its teacher. The Lessons page lists every class with who teaches it
  (`open: ask Luna for a class`), what it needs (`meet Luna first`, `after “Personas”`) and the **Ask for a class** button; it never shows a next required lesson, except the
  introduction (`next: talk to Luna`).
- **Tests and runs:** `--skip-to-session n` counts the classes before n as done and the teachers of n's classes as met; `r30-no-classes` plays days 1 to 3 taking no class
  (`docs/game/scripted-runs.md`).

## English guidance and the lesson runner (S16)

English is drawn, never spoken (D-game-13): an English line under the guide's Spanish in its own colour (`English: ...`), the bilingual camp checklist, plain-English
feedback after each turn, first-time tips and the controls in the menu (Esc); the support ladder starts with English and fades per phrase (see
[src/Vocaterra.Game/README.md](../../src/Vocaterra.Game/README.md)). A data-driven lesson runner (`content/game-v1/game/lessons.json`) walks the learner through the
guide's lesson (L starts it, Enter continues, K skips a step, H helps; S16 shipped a two-step sample, which S17 replaced with the real lesson). A scripted run
through it prints each step and its outcome:

```bash
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --talk guide --lesson first-words --lesson-next --lesson-next --say hola --screenshot /tmp/s16.png --frames 60
```

## The opening, the guide's lesson and the ending (S17)

- **The opening.** No quiz at the spawn. The first tips come one at a time, each at its delay (`delay` seconds in `game/tips.json`; their text is the locale's since C03): look at 0 s, walk at 9 s, jump at 18 s,
  the note pinned to the tent at 28 s (English, with the one Spanish word `camino`, saying the guide's camp is down the trail), collecting at 50 s; the first
  pickup then teaches the inventory. The objective area says `Go down the trail and find the guide` until the guide is met (the panel opened on her, or the first
  step of the checklist). The beacon points at her camp from the start.
- **The guide's lesson** (`first-words`, `game/lessons.json`). Luna, the guide, greets the player who comes within five blocks of her
  (`GuideApproach`; the same call as the E key, once per approach; Esc closes the panel and the lesson waits): the lesson starts by itself. Seven units since S26 (the verb unit
  below), 37 steps (S17: six units, 34 steps, about eight minutes at a learner's pace, estimated 4.8 minutes for a quick reader who answers in 4 s, 7.5 for a slow one, from the real WAV lengths, plus about a minute to walk down the trail): hello, yes and
  no, what is this, please and thank you (the guide offers fruit and the
  learner picks it from a bush: a `do` step with a beacon on the nearest berry bush), getting unstuck (`No sé`, `Otra vez, por favor`, `Más despacio, por favor`
  do what they say) and the camp's task, then a recap that opens the book's phrases page. While a lesson step takes a turn the guide's ordinary reply is not
  shown (the lesson speaks for her); a turn the core did not understand keeps her not-understood line and the English help.
- **The ending.** The complete checklist gives a spare torch, sets the beacon on the player's own camp and changes the objective line to `Go back up the trail and
  light your own fire`. A torch placed on the rim of the fire ring at `firering.player` lights it: the guide's closing line (`¡Muy bien! Ya tienes fuego.`) is
  said and shown with its English on a `Session complete` card, and in a playtest the post-check follows (the transfer offer first: she greets the player with
  it when they pass through her camp on the way back). The fire ring at the guide's camp keeps its role: the checklist's torch step. Outside a playtest the card
  offers the next session (`Enter: sleep until morning and start session 2`) and the objective line says `Session complete: sleep at your fire to start session 2`
  (V12c, "The next session: sleep at the fire" below); a playtest keeps `Session complete: your fire is lit`.
- **English on the buttons.** `[R] Repetir (repeat)`, `[S] Más despacio (slower)`, `[H] Ayuda (help)`, `[N] No sé (I don't know)`, `[W] Escribir (type)` and the hold-to-talk
  hint carry their English until the button (or its phrase) has been used three times; the keys and buttons work the same without it.

```bash
# the whole lesson with typed answers (every step and its outcome is printed), then the book's phrases page
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --lesson-run first-words --journal --journal-phrases --screenshot /tmp/s17-journal.png --frames 30
```

## Verbs in the lessons, sessions and replay (S26, D-game-19)

- **The verb unit** (in `first-words`, after "please and thank you", five steps): the present yo and tú of `querer`, `tener` and `necesitar`. Luna asks `¿Qué quieres?`, `¿Tienes una fruta?` and
  `¿Qué necesitas?`, and the learner answers with the yo forms (`Quiero madera.`, `Tengo una fruta.`, `Necesito agua.`; the fruit is the one the learner just picked). The English names the
  pattern once (-o is I, -as or -es is you) and then thins. The answers are statements (`state_want`, `state_have`), so nothing is handed over; a tú form after `yo` is a `conj.person` slip
  and `teno` for `tengo` a `conj.irregular` one, and the step goes on either way (fail-forward).
- **Sessions.** A lesson has a `session` in `lessons.json` (default 1) and is offered only once the play session has reached it; the session is the save's day (a load keeps it; only a sleep moves it).
  A lesson of a later session also has an English `objective` that names it: the objective area shows it (and the beacon points at the lesson's NPC) while the lesson is unlocked and not begun.
  `--session <n>` starts a run as play session n (`--session 2 --screenshot x.png --frames 120` shows session 2 opening at the camp: `Session 2: go to the village and talk to the villager to start
  the lesson “Personas”`, with the beacon on the villager). It only names the session: a lesson that comes `after` another still waits for it, so `--session 4` points at session 3's lesson.
  `--skip-to-session <n>` (V12c, a test aid) starts a new game where a learner who did sessions 1 to n-1 would stand, so session n's own lesson is open ("The next session" below). Since village-plan V10 a lesson may also name the lesson it comes `after`, and is offered only once that one is done: sessions 4 to 10
  (`--session 4` to `--session 10`) unlock `el-pueblo` after `ayer-y-manana`, `numeros-y-pan` after `el-pueblo`, `gente-del-pueblo` after `numeros-y-pan`, `como-es` after `gente-del-pueblo`,
  `ayer-en-el-pueblo` after `como-es`, `manana-en-el-pueblo` after `ayer-en-el-pueblo` and `el-mercado` after `manana-en-el-pueblo` ("The village: sessions 4 to 10" below), so `--session n`
  alone opens no village lesson; `--skip-to-session n` (2 to 10) does. `--lesson-run <id>` starts a lesson whatever came before.
- **Personas** (session 2, the villager then the shopkeeper, 22 steps since village-plan V10 added its two `review` steps, `autoStart`): él, ella, usted, nosotros, ustedes and ellos of `querer`, `tener`, `necesitar`, `ser`, `estar` and `ir`. The learner
  says what Luna wants, what we need and what we want and answers the shopkeeper's `usted` questions about themself; the other forms are heard (listen steps) and listed once. A step may name its NPC
  (`npc`), and a `do` step may wait for the panel to open on an NPC (`at`): the walk from the villager to the stall. While such a step waits the objective says `Walk to the stall and talk to the
  shopkeeper`. The villager's and the shopkeeper's lines are in pools named `guide.lesson.villager.*` and `guide.lesson.shop.*` (the voices follow the prefix; `--host-pools 'guide.*'` counts them).
  `--session 2 --lesson-run personas` runs it with typed answers, walking from the player's camp to the villager and then the stall.
- **The forms a lesson teaches.** `lessons.json` declares them (`verbs`: lemma, person 1 to 3, number sg or pl, tense): the yo and tú of the three verbs (`first-words`) and the third person singular, the
  first and the third person plural of the verbs it uses (`personas`, 12 forms: only those heard, said or named; the conjugation of `ir` waits for session 3). A test checks each is a word the learner meets in the lesson and that the core's morphology knows it; the book's conjugation
  tables (S28) read them.
- **The level a lesson keeps to.** Every lesson declares `level`, `a1` or `a2` (village-plan V08; the six lessons of sessions 1 to 6 are `a1`, the four of sessions 7 to 10 `a2`). The host's `--validate-content` holds the Spanish it
  teaches and says to the bundle's CEFR lists up to that level, its phrases' variants to A2 (the bundle's ceiling), and its tenses to the level's (`fut` only at `a2`); `content/README.md`, "A
  lesson's level", has the rule.
- **Practise again.** A completed lesson's heading on the book's Lessons page has a `Practise again` button (a click, or P with the page open). It closes the book, sets the objective line (`Practise
  again: talk to the guide to start “First words with Luna”`) and the beacon, and the lesson starts from its first step when the learner talks to its NPC. It stays done while it runs again.
  `--book-practise <id>` presses the button in a scripted run (`--lesson-run first-words --book-practise first-words --lesson-run first-words`).
- **The person of the answer's verb.** A step may declare the person and number its answer's verb must have (`person`: `1sg`, `3sg`, `1pl`...; and a `personRetry` line that names the ending).
  The runner reads the finite verb's person and number from the turn's word readings; a mismatch the core did not flag (no subject pronoun, so no `conj.person`: `quieres madera` at `¿Qué quieres?`,
  `quiero fruta` at `¿Qué quiere Luna?`) is a miss, not a success: the step stays, the retry names the ending, and after the step's attempts it moves on (fail-forward). The log's retry line says
  `wrong person: said 2sg, expected 1sg`. A wrong person next to a pronoun (`yo quieres`) stays a slip the core grades. Every verb step of both lessons declares its person.
- **The save** is version 4 (1 to 3 still load): each lesson keeps whether it was ever completed (`everCompleted`; a completed lesson of an older save counts) and the save keeps a replay that was asked for. Since C07 it is version 5 (1 to 4 still load): the objective's steps are keyed `<objective>.<step>` (`camp.wood`), and an older save's keys are mapped on load.

## Yesterday and tomorrow, the tense check (S27, D-game-19)

- **Ayer y mañana** (session 3, the villager then the shopkeeper, 21 steps with V10's two `review` steps, `autoStart`): the present of `ir` (voy, vas, va, vamos, van, listed once), the near future with `ir a` + an infinitive
  (`Voy a comer pan.`, `Vamos a comer fruta.`, `Mañana voy a ir al río.`; `Ellos van a comer pan.` is heard) and a few preterite forms, yo and tú (comí, bebí, fui; comiste, bebiste, fuiste), with the time words
  `hoy`, `ayer` and `mañana`. The villager covers today and tomorrow, a walk to the stall (the same short walk as Personas) leads to the shopkeeper, who asks about yesterday: `¿Qué comiste ayer?`,
  `¿Fuiste al río ayer?`, `¿Bebiste agua ayer?` and, last, `¿Qué vas a hacer mañana?`. The answers are statements about an action in any tense (the intent `state_action`), so nothing is handed over.
  The lesson of the newest session comes first at an NPC (a learner who reaches session 3 without session 2's `personas` is offered `ayer-y-manana`, and `personas` after it).
  `--session 3 --lesson-run ayer-y-manana` runs it with typed answers, walking from the player's camp to the villager and then the stall.
- **The tense of the answer's verb.** A step may declare `tense` (`pres`, `pret`, `ir-a` or, since V16, `fut`; below) with a `tenseRetry` line, next to S26's `person`. The runner reads the answer's finite verbs from the turn's word readings:
  for `pret` and `pres` one finite verb with that tense is enough; for `ir-a` it looks for a form of `ir` in the present, then `a`, then an infinitive (`voy a comer`; `vamos` is read as an imperative or a present, both count;
  `iba a comer` is a past, V16).
  A mismatch the core did not flag (a bare `como pan` or `comí pan` at `¿Qué vas a comer mañana?`, `voy a comer fruta` at `¿Qué comiste ayer?`) is a miss like a wrong person: the step stays, the retry names the form,
  the log says `wrong tense: said pres, expected ir-a`, and after the attempts the step moves on. Person is judged first. **Correct Spanish is never a miss:** a plan said in the present with a future-time word (`mañana voy al río`, `mañana como pan`; the words are `game/words.json`'s `nearFuture.timeWords`) is credited (the step ends Done, the log says `present for a plan, credited`)
  and the next step opens with a gentle English note naming the form the step practises (`That is right too. Here we practise “voy a ir”: Mañana voy a ir al río.`; the step's `presentNote`). A tense next to a time word is the core's `conj.tense` (M49: `mañana comí pan`,
  `ayer como fruta`, `voy a como pan`): a slip, the step ends. A miss shows none of the core's correction line (its recast would praise the learner's own wrong words).
- **A miss is not a use.** A turn the lesson counts as a miss for the person or the tense is no use of the phrase: it adds no phrase use (so no credit, no support step, no word of the phrase's progress); the
  right answer after it is the phrase's one use.
- **The near future in the conjugation tables.** `lessons.json` declares a taught form as `lemma`, `person`, `number`, `tense`; the tenses are `pres`, `pret`, `ir-a` (the near future) and `fut` (the simple future, A2; a step may
  expect it since V16, below). `ir-a` is a periphrastic form: it has no word of its own, so a form `{ "lemma": "comer", "person": "1", "number": "sg", "tense": "ir-a" }` is `voy a comer`: `ir` in the present for that person and
  number, `a`, then the lemma as the infinitive. The lemma of an `ir-a` form is the verb that follows the `a`. The book's table for a verb (S28) therefore has a column "future (ir a)" whose cells are built from the
  `ir` row of its present column and the verb's infinitive, never typed by hand; `TaughtVerbForm.Text(morphology)` gives the text of any declared form (`voy a comer`, `comí`, `fuiste`). The column and the preterite
  column stay hidden until a lesson declares a form of that tense.

## The simple future at a step (village-plan V16, phase B)

Session 9 ("Mañana en el pueblo", V17) teaches the simple future (`compraré`, `iré`, `hará`) against `ir a`; V16 made the runner and the book ready for it. Its four
answer steps are the bundle's `fut` steps (`TomorrowLessonTests`); `SimpleFutureStepTests` keeps V16's fixture lessons.

- **A `fut` step.** A repeat or answer step may declare `"tense": "fut"`, in an `a2` lesson only (the validator; V08 held a lesson's `verbs` to the same rule). Its answer is a finite
  verb the core reads in the future: the core's tense label is the lexicon's feature `fut`, mood `ind` (`compraré`, `iré`, `hará`), and the runner compares that as it is.
- **Correct Spanish for the future is never a miss.** At a `fut` step the near future (`voy a comprar pan`, `mañana voy a comprar pan`) and a plan in the present with a time word
  (`mañana compro pan`, `la semana que viene compro pan`) are credited: the step ends Done, and the next step opens with the step's `futureNote` (`That is right too. Here we
  practise “compraré”: …`). One note serves both, since it names the form the step practises whichever other form was said; a `fut` step has no `presentNote`. At an `ir-a` step
  the simple future (`mañana compraré pan`, `compraré pan`) is credited with the step's `presentNote`, which names the step's form in the same way, so a learner who has met the
  future in session 9 is not told off at a later `ir a` step. The log says `near future for a plan, credited`, `present for a plan, credited` or `simple future for a plan, credited`.
- **A wrong tense.** A preterite or a present without a time word at a `fut` step (`compré pan`, `compro pan`, `iba a comprar pan`) is the lesson's miss with the step's
  `tenseRetry`, as at the other tense steps. A wrong tense next to a time word is the core's `conj.tense` (`mañana compré pan` expects `voy a comprar`; `ayer compraré pan`
  expects `compré`): a slip, and the step ends.
- **The time words** (`game/words.json`, `nearFuture.timeWords`): `mañana`, `luego`, `después`, `pasado mañana` (V10) and the core's markers of a time noun with `próximo` or
  `que viene` for `semana`, `día`, `mes` and `año` (`próxima semana`, `semana que viene`, ...). A time word of several words is matched as a whole, its words in order, never one
  of them alone (`NearFutureWords.Mark`), and a verb inside one (the `viene` of `la semana que viene`) is not the answer's verb. `luego` and `después` are no markers to the core
  (`después compré pan` is narration, so the lesson judges it); `el día que viene` is left out, since it may be a relative clause (`el día que viene Luz`). Since V17 (after the
  core's M57) `state_action` takes the later markers at either end as phrases of its grammars (`pasado mañana`, `la semana que viene`, `el mes que viene`, `el año que viene`,
  `la próxima semana`, `el próximo día`, `el próximo mes`, `el próximo año`, and the past `la semana pasada` and `el año pasado`), so `la semana que viene compro pan` binds
  `pan`, not `semana`, and `la semana que viene compré pan` is the core's `conj.tense`.
- **The book.** The conjugation tables gain a fourth column, the simple future (`ui.bookColumnFuture`, "future"), after the future (ir a): time runs left to right and the two
  futures stand in the order the lessons teach them. It shows once a lesson teaches a `fut` form of the verb or the learner says one, as the others do, and its counts come from
  the word record like the present's and the past's. Its forms come from the lexicon (the Wiktionary import: `compraré`, `comprarás`, `comprará`, `iré`, `buscaré`, `hará`,
  `tendré`, `trabajará`, `pescará`); the core's regular paradigm builds no simple future, so a verb the import lacks has no `fut` cell. A page too narrow for four columns stacks
  them in blocks (X08).

Before session 9, the fourth column shows for a form the learner says (what the learner says is never hidden):

```bash
# the table of comprar with its four columns: session 8's lessons done, two lines said to the guide, the book open on the verb
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --no-autosave --skip-to-session 8 --say "mañana compraré pan" --say "compro pan" --leave \
  --book-verb comprar --screenshot /tmp/v16-book.png --frames 30
# the smallest window at the largest text: one column a block, the future the fourth block (--book-detail scrolls the right page to it)
DISPLAY=:0 dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --no-autosave --size 800x600 --text-scale 150 --skip-to-session 8 --say "mañana compraré pan" \
  --say "compro pan" --leave --book-verb comprar --book-detail 30 --screenshot /tmp/v16-book-small.png --frames 30
```

## The village: sessions 4 to 10 (village-plan phases A and B, D-game-22)

The plan is [docs/game/village-plan.md](village-plan.md): phase A (V01 to V07, V10 to V12, sessions 4 to 6, `a1`) and phase B (V08, V13 to V18, sessions 7
to 10, `a2`). Each lesson uses its verbs in the present, the preterite and the near future, in phase A around an order the learner carries out (the plan, the
doing, the report). Phase B, in a line a lesson:

- **7, "¿Cómo es?"** (León, then the stall and the river, 20 steps): adjectives after their noun and `gustar` in the present and the past; the water order.
- **8, "Ayer en el pueblo"** (the villager, then Domingo and Mar, 18 steps): the preterite in every person and `tener que`; no order.
- **9, "Mañana en el pueblo"** (Luz, then Domingo and Mar, 14 steps): the simple future against `ir a`; no order.
- **10, "El mercado"** (Luz, then León and the stall, 22 steps): 11 to 20, a total, `más … que`, `poder` and selling; no order. The series ends here.

**The follow-ups** (village-plan V22, V25, V23a, V20, V21 and V21b, on `feature/village-followups`, 2026-11-01 and 02; the core's M65 to M70 beside them). Free
talk takes the lessons' lines: an NPC understands the statements its lessons taught, and a phrase of a lesson that has begun counts as a use at any NPC (V20).
Each NPC answers in its own voice (`¡Qué bien!`, `Sí, es verdad.`, `Está bien.`, in ten pools; the shared feedback lines are in each NPC's voice, V21b).
The stall and the market answer what was said: `quiero madera` at the stall gets «No tengo eso. Tengo agua y pan.» and no bottle, a request for bread gets the
bread, and León answers `¿Cuánto es todo?` in free talk with the unit price (V21, V21b). A verb typed without the accent of its tense (`ayer compre pan`) is understood, and a note under the reply names the accented
form in the correction colour («compré», con tilde: es pasado), in free talk as in a lesson (M70, V21b). The Lessons page opens at the `next:` lesson (V21b). The
name plates show only for the NPC the learner faces and the beacon's NPC, drawn in the world (V22). A load keeps its day, and a sleep is the only thing that
moves it. Two counters: the day (lessons, objectives, the HUD, the book) and the sitting (the spacing rule, the review deck and the session log) (V25).

**The futures.** `Voy a comprar pan.` (from session 3) is a plan already made; `Mañana compraré pan.` (session 9 on) is a promise or a prediction. Either is
credited at the other's step with the step's note ("The simple future at a step" above), and the book's tables show both, the near future before the simple one.
**The market** (session 10) has no order: the learner sells (`offer_item`, the host's `buy`: Luz and Domingo buy wood, León fruit, a coin a piece) and buys
(the host's `sell`: bread from Luz at two coins, apples from León at one), and asks the total of León's table (`¿Cuánto es todo?`). "Selling" below has the rules.

**Counts** (measured from `content/game-v1` at V24, 2026-11-02; after phase B in brackets): 10 lessons, 229 steps (6 `a1` and 4 `a2`; the same); 155 phrase
rows, 74 said, 80 heard and 1 both (the same); 22 catalogue nouns (22); 20 verbs in the lessons' tables (20); 30 intents (29: `ask_how` came with V21) and 70
prompts (70); 691 speech lines in 1 382 files on 2026-11-09, in `speech/<speaker>/` and `speech/cards/` since V28 (541 lines in 1 082 files at V28; the count of the day is the "To hear" line of [speech-checklist.md](speech-checklist.md)); 87 check items, 43 heard and 44 said (the same). The
battery by play session: 31 items in sessions 1 to 3 (the pilot's), then 39, 47, 55, 63, 71, 79 and 87 in sessions 4 to 10 (eight a lesson; the same).


- **The people.** Seven NPCs, in the order of `game/npcs.json`: the guide Luna, the shopkeeper (`Tendero`), the villager (`Aldeano`), the fisher Mar (`Pescador`),
  the baker Luz (`Panadera`), the smith Domingo (`Herrero`) and the farmer León (`Granjero`). An NPC is data alone (`game/npcs/<id>.json`, schema 2, V01: its
  prompts by state, its opening, its voice, its key words; `content/README.md`, "Adding an NPC"). The four new voices are provisional (D-game-22 (d): the
  fisher `em_santa` at 0.9, the baker `if_sara`, the smith `im_nicola`, the farmer `pm_santa`; `docs/game/speech-checklist.md`; the voice trial is in git history).
- **Directions** (V02). The fisher and the villager point the beacon at the place the learner names (`¿Dónde está la panadería?`: the scene's labels, then a
  catalogue noun; any other word is "No sé dónde está."); every answer logs `place_asked` and the arrival at the arrow logs `beacon_reached`.
- **Coins, apples and the axe** (V03, V05): `moneda`, `manzana` and `hacha` are items with their icons and held sprites; the axe fells a trunk in a quarter of
  the time. An **order** is an objective shown and counted from its play session, delivered by walking up with the goods: the panel opens, the goods go, the
  NPC thanks and pays, and `order_delivered` is logged. `fruit-for-mar` (session 4: three fruits for two coins), `oven-wood` (session 5: five wood for four
  coins) and `axe-handle` (session 6: two wood and two coins for the axe). The baker sells bread at two coins a loaf (`Quiero dos panes, por favor.` buys
  two, and any number said as many, with no cap since V19; with too few coins nothing changes hands and she says `Necesitas más monedas.`, with no room in the bag
  `Tu bolsa está llena.`, and asked for none, `quiero cero panes`, she asks how many), and the stall says its price. `quiero cero panes` still credits the
  phrase `quiero-dos-panes` though nothing changes hands: the Spanish was right and practised the pattern (V20, handover 9.3 D (f)).
- **El pueblo** (session 4, after `ayer-y-manana`, the fisher at the bridge, 24 steps): `¿Dónde está…?`, `¿Está lejos?`, `¿Hay un pozo…?`, and the walk itself in
  three times (`Voy a la panadería.`, `Voy a comprar pan.`, back at the bridge `Sí, fui a la panadería.`): the arrow to the bakery and the smithy, the
  villager at the signpost, then back to the fisher, who asks for fruit (`fruit-for-mar`).
- **Números y pan** (session 5, after `el-pueblo`, the baker then the stall, 23 steps): the numbers to ten, plurals, `¿Cuánto cuesta?`, `Quiero dos panes, por
  favor.`, and the oven order in three times (`Voy a buscar madera.`, `Tengo cuatro monedas.`, `Compré dos panes.`: the regular `-ar` preterite).
- **Gente del pueblo** (session 6, after `numeros-y-pan`, the villager, the smith and the farmer, 28 steps): `¿Cómo te llamas?`, `¿Qué haces?`, `Es el herrero.`,
  `Vivo en el campamento.`, and the smith in the third person in three times (`Domingo trabaja en la herrería.`, `Va a hacer un hacha.`, `Domingo hizo un
  hacha.`) around the axe order.
- **¿Cómo es?** (session 7, after `gente-del-pueblo`, the farmer then the stall on the way to the river and back, 20 steps; the first `a2` lesson, phase B):
  `La manzana es roja.`, `Es grande.` (an adjective after its noun, agreeing with it), `Sí, me gusta el pan.`, `Sí, me gustan las manzanas.`, `Quiero tres
  manzanas rojas.` (one coin an apple: with no coins the farmer says `Necesitas más monedas.`), and the water order in three times (`Voy a buscar agua.`, the walk
  to the river, `Sí, fui al río.`) with `gustar` in the past (`Sí, me gustó el pan de Luz.`). The order gives three apples for a bottle of water; it names its
  lesson, so a learner who carries the bottle filled in session 1 says the plan and goes to the river before the farmer takes it.
- **Ayer en el pueblo** (session 8, after `como-es`, the villager, the smith and the fisher, 18 steps, `a2`; V14): the preterite in every person and `tener que`
  (`state_obligation`). The villager tells the day before and the learner retells it in the third person (`Luz hizo pan.`, `Mar pescó en el río.`) and the
  first plural (`Fuimos al río.`, true: session 7's walk ended on Mar's bank); at the smithy the third plural (`Ellos comieron pan.`) and `Tengo que ir al
  río.`; at the river Mar's sale retold (`Luz compró pescado.`). No order and no sale: a learner arrives with no coins, three apples, two loaves and the axe,
  and leaves with them. The listener's name is taken out of a line before the core reads it, at its start, after any greeting or at its end (`Luz, quiero
  pan`, `buenos días Luz, quiero pan` since V19; `Vocative.cs`).
- **Mañana en el pueblo** (session 9, after `ayer-en-el-pueblo`, the baker, the smith and the fisher, 14 steps, `a2`; V17): the simple future, the second way to say
  tomorrow after `voy a`, with an explain step on when each is used (a plan the learner already has, `Voy a comprar pan.`; a promise or a prediction, `Mañana compraré
  pan.`): `Mañana compraré pan.` to Luz (her `Mañana tendré pan fresco.`), `Buscaré madera para Luz.` and `Iré al río.` to Domingo (who says Mar `pescará`), then the
  walk to the river, where Mar tells the day of tomorrow (`Mañana Luz hará pan. Domingo trabajará en la herrería. Y yo pescaré en el río.`) and the learner retells
  `Luz hará pan.`. No order and no sale. Every answer step is `tense: fut`, so `mañana voy a comprar pan` is credited with the step's note. The baker's open
  prompt takes a plan since V17 (`mañana voy a comprar pan`, answered `¡Muy bien!`); the fisher's does not, since his directions take `busco` with any noun.
- **El mercado** (session 10, after `manana-en-el-pueblo`, the baker, the farmer and the stall, 22 steps, `a2`; V15, the last lesson): market day. Luz counts
  to twenty (`Tienes veinte panes.`) and buys the learner's wood (`Quiero vender madera.`, the trees, then `Vendo madera.`: a coin a piece); León shows his fifteen
  apples (`¿Cuánto es todo?`, `Quince manzanas, quince monedas.`, `Es muy caro.`, `La manzana es más barata que el pan.`, `¿Puedo pagar con fruta?`: coins only,
  but he buys fruit; `Mañana venderé fruta.`, a `fut` step) and sells three apples for the wood's coins; the stall hears the report (`Compré tres manzanas.`).
  A learner arrives with no coins and leaves with none, six apples, two loaves and the axe. No order: the sale and the purchase are the session's exchange.
  Afterwards the Lessons page says every lesson is done.
- **Selling** (V15): `vendo madera`, `quiero vender madera` (and `tengo madera para usted`) are the intent `offer_item`, which the host answers with `buy`
  (`content/README.md`, "What a host event does"): Luz and Domingo buy wood, León fruit, a coin a piece, every piece the bag holds (or the number said, with no
  cap), as often as the learner brings some. None in the bag: `No tienes madera.`; another item, or a thing that is no item (`vendo piedra`, V19): `No, gracias.
  Solo compro madera.`; none offered (`vendo cero bloques de madera`): `¿Cuántos bloques de madera me vendes?`; no room in the bag for the coins: `Tu bolsa está
  llena.`. A purchase is answered alike (`quiero casa` at the bakery: `Solo vendo pan.`). In every miss nothing changes hands and the NPC says no success line
  (`NpcEffect.ExchangeMiss`, asked on the NPC's worker and again of the bag itself). A sale is the present only: `vendí madera` and `venderé madera` are
  statements (`state_action`) and sell nothing. A greeting before another request is answered as the request (`hola, quiero dos panes` buys the bread;
  `Greeting.cs`, as `Vocative.cs` takes the listener's name out): the core reads one intent per line, so the game reads the line and then its rest. The typed and
  spoken paths and the host's `--transcript` read a line in one place, `LineReading.cs` (V19).
- **Runs.** `--session 4 --lesson-run el-pueblo`, `--session 5 --lesson-run numeros-y-pan` and `--session 6 --lesson-run gente-del-pueblo` run them with typed
  answers, `--skip-to-session 7 --lesson-run como-es` the farmer's, `--skip-to-session 8 --lesson-run ayer-en-el-pueblo` the villager's, and `--skip-to-session 9
  --lesson-run manana-en-el-pueblo` and `--skip-to-session 10 --lesson-run el-mercado` the baker's. The scripted runs r18 (the bakery purchase), r19 (asking the
  way), r22 (the market: wood sold, bread bought, the total) and r23 (the future in free talk and the book's future column) cover them
  (`docs/game/scripted-runs.md`). To play a village lesson by hand, `--skip-to-session n` (4 to 10) starts at it with the sessions before counted as done ("The
  next session: sleep at the fire" below; r21); from `--skip-to-session 7` a sleep at the fire after each lesson plays 7 to 10 in one sitting (V09).

```bash
# session 5: five trunks from the orchard, the oven order delivered at the baker (four coins), two loaves bought (r18 of the scripted runs);
# the inventory at the end holds the book and two loaves
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session 5 --teleport 309,216 --interact 311,38,214 --interact 311,39,214 --interact 311,40,214 \
  --interact 311,38,218 --interact 311,39,218 --talk baker --shot /tmp/v09-paid.png --say "quiero dos panes, por favor" --leave --inventory \
  --screenshot /tmp/v09-bakery.png --frames 30
# session 1: the fisher asked the way to the bakery, the arrow, the walk to its door (beacon_reached), the bakery and the baker (r19)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --teleport 236,206 --talk fisher --say "¿dónde está la panadería?" --shot /tmp/v09-arrow.png \
  --walk-to 282,206 --walk-to 282,212 --look 0,4 --screenshot /tmp/v09-bakery-door.png --frames 60
# session 4: the fisher's lesson with typed answers, from the player's camp
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session 4 --lesson-run el-pueblo --screenshot /tmp/v10-el-pueblo.png --frames 30
```

**The learner's route through phase B** (V09, played on 2026-11-01): from `--skip-to-session 7`, each lesson with typed answers, free talk with each of its
NPCs, then a sleep at the fire. Each session opens on its lesson: the objective names it and its NPC (`Session 8: go to the square and talk to the villager to
start the lesson “Ayer en el pueblo”`), the beacon points at the NPC and the morning toast shows; the Lessons page names the NPC under the lesson (`next: talk
to Aldeano` in session 8, `BookPagesTests`), far down its 192 lines. The four lessons ran to their ends (como-es 18 steps done, ayer-en-el-pueblo 17,
manana-en-el-pueblo 13, el-mercado 20; the rest were reviews with nothing due, and in the market the wood and the apples, which a scripted run cannot fetch:
the beacon's tree is out of `--interact`'s reach, where a learner cuts any trunk). After session 10 the objective says every lesson is done and the Lessons
page that the series ends. Of phase B's 27 said lines, 18 are understood in free talk by the NPC who taught them (all of León's; Domingo's `Ellos comieron
pan.` and futures; Luz's `Mañana compraré pan.` and the sale) and 9 are not, since the open prompt does not take the intent (`Es grande.` and `Compré tres
manzanas.` at the stall, the villager's three, `Tengo que ir al río.` at Domingo, `Luz compró pescado.` and `Luz hará pan.` at Mar, `Tienes veinte panes.` at
Luz); the route tried 15 of them, each as the prompts predict. One bug was found and fixed: a skipped game left the shopkeeper as if never met, so in session
7 his key words stood alone again and his next visit handed over a second bottle as a fail-forward, logging a use of `pedir-agua` the learner never made
(`SessionSkip`, `SessionSkipTests`). The rest is in the list below.

**The learner's route through sessions 4 to 10** (V24, 2026-11-02, from a new world, typed answers). The route ran in four runs, since a run stops at 18 000
ticks (the open points below): sessions 4 to 7 and 7 to 9 by a sleep at the fire each time, session 10 from `--skip-to-session 10`. Each lesson ran with
`--lesson-run`, and free talk once with each of the lesson's NPCs: a line the lesson taught and one it did not.

- **At each session's start** the objective names the lesson and its NPC, and the beacon points at that NPC. Session 4: «walk down the road to the bridge and
  talk to Mar, the fisher, to start the lesson “El pueblo”», the beacon 126 blocks off. Session 9: «go to the bakery and talk to Luz the baker to start the
  lesson “Mañana en el pueblo”», and the Lessons page opens at `next: talk to Luz` (lines 158 to 186 of 192). Every sleep moved the session (4 to 5 to 6 to
  7 to 8 to 9 to 10).
- **The lessons** ran to their ends: el-pueblo 22 steps done and 2 skipped, numeros-y-pan 19 and 4, gente-del-pueblo 25 and 3, como-es 18 and 2,
  ayer-en-el-pueblo 17 and 1, manana-en-el-pueblo 13 and 1, el-mercado 18 and 4. A skipped step is one with nothing to walk to (the bag held the wood, the
  apples or the bread already, V23a) or one the scripted walk could not reach (a tree out of `--interact`'s reach).
- **Free talk, taught lines:** 11 of 11 were understood by the NPC who taught them (`Voy a la panadería.`, `Tengo cuatro monedas.`, `Es el herrero.`,
  `Vivo en el campamento.`, `La manzana es roja.`, `Luz hizo pan.`, `Ellos comieron pan.`, `Mañana compraré pan.`, `Luz hará pan.`, `Tienes veinte panes.`,
  `¿Cuánto es todo?`).
- **Free talk, untaught lines:** 6 of 11 were understood and 5 were not. `Hoy hace sol.` is understood at the villager, the smith and the fisher, `Hace calor.`
  at Luz and `Hoy compro fruta.` at Luz; `Me gusta el río.` gets «No te entendí» at the villager and at the smith, `Tengo un hacha.` at León (twice),
  `Tengo dos manzanas.` at Mar. A true sentence of a kind the NPC takes is understood whether or not it was taught; the rest is not.
- **What a learner would find wrong or stuck:** nothing blocks the route. Two things are not right. A request the stall or Luz cannot fill is refused
  correctly (`Quiero dos manzanas.` at Luz: «Solo vendo pan.»), but the book logs it as a use of `quiero-dos-panes` (the noun `manzana`); and an untaught line
  gets «No te entendí» and then «¿Cómo?» at the same NPC, two replies for one line. No game
  change was made in this milestone: none of these had a one-line fix that a test would pin.

The open points after the follow-ups (V24, 2026-11-02) are in [village-plan.md](village-plan.md) section 5.

## The next session: sleep at the fire (V12c)

A play session used to advance only on a quit and a `--load`, which nothing in the game said. Now the player sleeps at their own fire, and the next session begins
in the same run.

- **Where.** The closing card of session 1 (the ending, S17) offers it: `Enter: sleep until morning and start session 2`, over its `Esc: keep looking around`. And
  the lit fire at the player's camp (`firering.player`) offers it whenever the session's work is done, the same moment the objective says to sleep (or that every
  lesson is done): standing within five blocks of it, the HUD's hint under the crosshair says `E: sleep until morning (session 3)` and **E** sleeps (E keeps
  talking to an NPC under the crosshair and opening the workbench first). On waking in a session whose lesson is left the fire offers nothing, so no sleep skips it. An open panel, a screen,
  a check, a recording or a lesson step that waits comes first: no prompt then. Every lesson starts by itself, so a lesson begun in the run stays engaged when its
  panel closes and holds the sleep back until it is done. A playtest plays one session per run (its post-check follows the ending) and offers no sleep.
- **A sleep is exactly a load** (`SaveGame.Sleep`). The game captures its own save and restores it into the running session as the next play session: what a save
  does not keep is cleared first (the review question, the second tries, the warm-up, which the new session offers again, the panel's support, the buttons' English
  counts, the word cards not yet shown), every part of the save is put back as a load puts it back, the session number goes up by one (which lifts the spacing gaps of
  D-game-6), and `BeginPlaySession` names the new session's lesson and points the beacon at its NPC. The stored time does not move, as a load adds none ("Time
  rule" above). A lesson under way stays at its step, not engaged, and takes up again when the learner talks to its NPC, as after a load. `SleepTests` holds the
  invariant: the save written after a sleep equals the save written after a save and a load of the same state (no field differs), and so does what the player
  meets next (the objective, the beacon, the lesson, the warm-up). Since V12c the guide's greeting on the way back reads her saved offer flag, so after a load or a
  sleep she does not greet the player again for an offer made.
- **The session log and the autosave do what a quit and a load into the same folder do.** The log writes the old session's `session_end` and the new session's
  `session_start` (no mic check, no check battery; a `slept` world event is the new session's first, `session 1 to 2`), and the `session_end` counts only its own
  session's events. The autosave writes the session as it ends (`autosave: ... written at sleep`, with no toast), so a load of the autosave starts the session the
  sleep starts; then its policy starts again from the sleep's tick, as a load's run starts it. The folder and the files stay the run's. The console says
  `sleep: ...: session 1 ends at the fire, session 2 begins (tick, stored time, world digest)` and a toast says `Good morning: session 2 begins`.
- **What the objective says next.** Once the session's work is done (session 1: its ending; from session 2: no lesson under way and every unlocked lesson done), the
  line says `Session complete: sleep at your fire to start session 2`, and when the work ends away from the fire (a lesson completed in the village) the beacon
  goes to the fire. When no later session has a lesson (after session 10, the last) it says `Every lesson so far is done: practise any of them again in the book (J), or
  talk to the people of the village`; the fire still lets the player sleep, which lifts the spacing gaps. From session 2, while a lesson is under way or left, it
  says `Next: talk to the villager for the lesson “Personas”`. These lines come after a lesson's own line, a replay, a review and a new lesson, and over an order,
  as the new lesson's line does, except that an open order keeps its own line rather than `Every lesson so far is done`.
- **`--sleep`** is a script step: it closes the panels and sleeps as the player's E at the fire would, wherever the player stands (a script needs no walk); refused
  with a console line when the fire is not lit, a lesson step waits or the run is a playtest. The scripted run r20-sleep reaches session 1's ending the short way
  (the camp, the shopkeeper's bottle on a second visit, the river and the fire by `--teleport`), shows the card, sleeps, and shows session 2's objective, the beacon
  on the villager (no fire prompt: Personas is left) and the book's Lessons page.

**`--skip-to-session <n>`** (a test aid, `SessionSkip`): a new game starts as play session n (from 2 to the last session that has a lesson, 10) where a learner
who played sessions 1 to n-1 and did everything would stand, built from the data alone (the lessons' `session` and `after`, the objectives, the orders and the NPCs'
host events), so a later session needs no code. Every lesson of an earlier session is completed in its own session (its steps recorded as skipped: the learner
took none); session 1's ending is reached (the camp's steps counted, a torch on the rim of the player's fire ring, and the camp's bottle of water in the bag: the
shopkeeper gave it, the river filled it, and nothing in the game empties it, V13; and the shopkeeper counts it as given and his key words as taught, so a
later visit is not a first one, V09); each earlier order is delivered by the NPC's own `<npc>.delivered` thanks (what
the bag lacks of its goods brought, the coins it asks for paid from the bag, since only an NPC gives coins, its reward paid); and each earlier lesson's purchases are
made (a `do` step waiting for an NPC that sells, as many as the lesson's request names: the two loaves of `numeros-y-pan`). So the coins and the items follow
village-plan 3.4: none at session 4, two coins at 5, two coins and two loaves at 6 (the axe order's two coins), no coins, two loaves and the axe at 7 (the farmer's
apples are the water order's). What the world gives (wood, fruit) is not carried over: the next lesson and order point at it. No
practice is faked: no turn is taken and the word and phrase records stay a new game's. The objective and the beacon then name session n's lesson and its NPC, as
after a load. The console says `session: skipped to play session 4 (sessions 1 to 3 counted as done; test aid)`, the session log has the same line (the world event
`session_skipped`) and `skipped_to` on its `session_start`, and the save carries `skippedTo` (left out of every other save), so no log or save made with it passes
for a learner's. It refuses `--load`, `--session` and `--playtest`.

```bash
# the short way to session 1's ending, the card's sleep, session 2's objective and beacon, the Lessons page (r20-sleep)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 \
  --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha \
  --walk-to 174,185 --place antorcha@175,44,187 --teleport 283,211 --talk shopkeeper --wait 3 --leave --talk shopkeeper --leave --teleport 234,222 --interact 233,34,222 \
  --teleport 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 --shot /tmp/v12c-ending.png --sleep --look 129,-20 --wait 2 --shot /tmp/v12c-session2.png \
  --book-page lessons --screenshot /tmp/v12c-lessons.png --frames 30
# session 4 at once: the fisher's lesson named and pointed at, the earlier sessions counted as done (r21-skip)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --skip-to-session 4 --wait 1 --shot /tmp/v12c-skip.png --book-page lessons --screenshot /tmp/v12c-skip-book.png --frames 30
```

## The session summary and the second try (X04, R01 and R02)

**The summary card.** When the session is complete (S17's closing card), the card shows **This session** under the closing line, in English: the lessons
completed or begun (with the steps done), the new nouns the learner's own lines used for the first time, the phrases said correctly without help, the verb
forms used (tense and person), up to two phrases to practise next time (a phrase said "no sé" first, then a slip, then a phrase whose help is still on), and
a line on how to replay a lesson (press J for the book, choose a finished lesson, Practise again). The card stays up for 45 seconds (it was 12) or until Esc,
and it fits at 1280 x 720 and 800 x 600. `SessionSummary` (engine-free, `Vocaterra.Game`) builds it from the learner's record and the run's events. It counts
things done only: no score, no CEFR label, no count of failures (D-game-10). The lines are written to `session.jsonl` as a `session_summary` line. The card is
not drawn when a session ends early (F6, F10, quit): that is left for later.

**The second try.** A phrase the learner says "no sé" to (typed, spoken, the button, or a lesson step's phrase; "ayuda" is help, not "no sé") waits in
`RetryQueue` for the next talk with its NPC. At that talk, when no lesson holds the conversation and no review is open, the guide asks it once more as her
review (the same request line and answer rules as a due phrase), with the English note *Let's try that one again: …*. The answer is logged as a world event
`second_try_answered`, detail `recovered` (the same phrase, said correctly, with or without the card) or `not_yet`. Why these rules:

- **A talk, not the next step.** A review opened between two lesson steps would take the next step's prompt (the core evaluates the answer on the review's
  prompt). So the retry waits for a talk, and never interrupts a lesson's own turn.
- **Once per session, two waiting at most** (repetition fatigue, R02). A second "no sé" on the retry does not bring it back.
- **Never in a check.** A check is graded and changes nothing else; a check's "no sé" queues nothing.
- **No credit before the gap.** The retry's use is counted like any review answer, so the spacing gap (D-game-6) still decides any credit.
- **Not saved.** A loaded game is a new sitting, and the queue belongs to the session it came from.

Not done (R02 as written in the backlog): the retry is not asked one rung lower on the support ladder, and a "miss" at a phrase is not queued (only "no sé").

```bash
# S17's route with a "no sé" at the villager's first talk, then a second talk: the villager asks the river question again with the English note (the
# console prints second_try_asked and second_try_answered); the closing card, with its summary, is in the last PNG (--size 800x600 for the small window)
dotnet run --project src/Vocaterra.Game -- --seed 42 --mute --session-dir /tmp/x04/session --lesson-run first-words --shot /tmp/x04/lesson.png \
  --talk guide --wav content/game-v1/speech/guide/phrase.hola.wav \
  --walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187 \
  --talk shopkeeper --say "quiero agua, por favor" --shot /tmp/x04/stall.png --say "sí, por favor" \
  --talk villager --say "no sé" --shot /tmp/x04/no-se.png --leave \
  --talk villager --say "¿dónde está el río?" --shot /tmp/x04/second-try.png \
  --walk-to 234,222 --interact 233,34,222 --look 180,-14 --shot /tmp/x04/river.png \
  --talk guide --say "sí, por favor" --walk-to 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 \
  --screenshot /tmp/x04/summary-card.png --frames 90
```
