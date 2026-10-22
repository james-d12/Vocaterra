# The phrase sheet

The source of truth for the prototype's Spanish (decision D-game-10; the brief is `../docs/PROTOTYPE_BRIEF.md`, the plan `../docs/SCENARIO_PLAN.md`,
milestone S5). The bundle `game-v1` and this sheet are the same set: every phrase here is in `game-v1`, and a test
(`game/tests/Vocaterra.Host.Tests/PhraseSheetTests.cs`) reads this file and checks it against the bundle on every run.

**Purpose.** A short list of practical phrases for requesting supplies, answering an offer, asking where something is and getting unstuck.
Each row says who says the phrase (the learner, the NPC, or both), what it means, which variants the game accepts, one typical learner
mistake with the error code the core gives it, where it happens, what the support card shows, which NPC dialogue lines speak it (for the
pre-rendered speech of S9) and who has reviewed it. Nothing outside this list is taught or tracked as a phrase.

**The Spanish variety.** Latin American neutral (core ADR 0005, `nlp/docs/adr/0005-*`): textbook A1, the informal address `tú` for the
NPCs (`¿Quieres...?`, `¿Qué necesitas?`), no vosotros, no Spain-only words (`vale` is neither spoken, taught nor accepted).
`me da agua` (the usted form, common at counters) and `me das agua` (the tú form) are both accepted; the card teaches `Quiero agua, por favor.`.
The word for the berries is `fruta` (`la fruta`, `las frutas`; A1, in the lexicon): `baya` is in neither the lexicon nor the A1 and A2 lists, so the
reviewer chose `fruta`. The camp objective's HUD will say `frutas 3/3`, and there is no separate noun for the bush.

**The review is provisional.** Every row of the first eleven was read by an LLM pass (Opus, 2026-10-21, D-game-12) for naturalness, Latin American neutral usage and
the English meaning, and its advice is applied. This is not native-speaker approval: the rows stay `llm-opus (provisional)` until a Spanish speaker
checks them. On the reviewer's advice the English of `pedir-agua` is `I want water, please.`, which departs from the brief's gloss ("I would like
water, please"): `quiero` is "I want", and the polite register is carried by `por favor`.

**The guide's lesson (S17, D-game-15).** The set grew by ten rows for the guide's lesson (`first-words`, in `game/content/game-v1/game/lessons.json`): five the learner
says (`si`, `no`, `que-es-esto`, `otra-vez`, `mas-despacio`) and five the learner only understands (`me-llamo`, `es-madera`, `quieres-fruta`, `de-nada`,
`necesitas-madera`). The guide's name is **Luna** (`Me llamo Luna.`): a name that is also an A1 word, so the lint and `cefr-check` need no special entry for it. The owner asked for
the larger set (D-game-15), which goes past D-game-12's interim limit of five phrases plus five service words. Every new row was read by the LLM pass of
D-game-12 step 3 (`llm-opus (provisional)`, 2026-10-22), and its advice is applied. The game's `cefr/a1.txt` gained `madera`, `antorcha`, `fuego` and `despacio` (world nouns the player handles from the first minutes,
and the adverb of `Más despacio, por favor`, a classroom phrase): provisional, like `botella` before them. `Sí.` and `No.` share their intents with `Sí, por favor.` and
`No, gracias.`; the game credits the phrase the lesson step has in play, and the plain `sí` at an offer stays `si-por-favor`.

**How a row is read.** The table below is parsed by the test, so keep its shape. Columns:

- `id`: kebab-case, the key the phrase tracking (S8) uses (`phrase.<id>`).
- `says`: `says` (the learner says it), `hears` (an NPC says it, the learner understands it), `both`.
- `Spanish`: the phrase as taught, with its accents and `¿ ¡` exactly. For `hears` rows it is the text of the prompt `prompt` or of a dialogue line.
- `English`: the intended meaning.
- `intent` and `prompt`: for a learner row, the bundle intent the phrase and every variant resolve to, and the prompt it is said at. For a
  `hears` row, `-` for the intent and the prompt whose text it is.
- `accepted variants`: other things the game accepts as the same phrase, separated by ` ; `. Each is an example of the intent in the bundle
  and resolves to it at the prompt with no error (the test checks it).
- `learner error`: one typical mistake as `text (code, code)`: the pipeline gives exactly these error codes (the test checks it); `-` when
  the row is not said by the learner.
- `NPC and situation`: who, where, and what comes before and after.
- `support card`: the text the game shows beside the phrase while it is supported (S7, S8).
- `audio line ids`: the bundle dialogue line ids (`dialogue/dialogue.json`) whose NPC audio speaks the phrase (S9): the text of a `hears`
  line is the phrase itself; for a learner phrase it is a line in which an NPC says it as a model. `-` when no NPC line says it.
- `reviewer` and `date`: `pending` until a pass is recorded (D-game-12): first the LLM pass, `llm-opus (provisional)`, later a native
  Spanish speaker. Rows marked provisional are re-checked when a speaker is found.

Cells never contain a `|`. A `-` means none.

**The run-time copy.** The game reads `content/game-v1/game/phrases.json` (S7), a machine-readable copy of the columns it needs: `id`, `says`,
`Spanish`, `English`, `intent`, `prompt`, the accepted variants, the support card and the audio line ids (`-` is `null` or an empty list). This file stays
the source of truth: edit the table here first, then the JSON, then re-hash the bundle. `game/tests/Vocaterra.Host.Tests/PhraseSheetTests.cs` compares
the two row by row on every run. The panel's `Esperaba: «…»` is the first part of a row's support card (the phrase before ` = `).

## Phrases

| id | says | Spanish | English | intent | prompt | accepted variants | learner error | NPC and situation | support card | audio line ids | reviewer | date |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| que-necesitas | hears | ¿Qué necesitas? | What do you need? | - | stall-need | - | - | The shopkeeper at the village stall opens the exchange when the player walks up (host event `shopkeeper.ask`). Comprehension: the learner answers with `pedir-agua`. | ¿Qué necesitas? = What do you need? · necesitas = you need | shop_need.1 | llm-opus (provisional) | 2026-10-21 |
| pedir-agua | says | Quiero agua, por favor. | I want water, please. | request_item | stall-need | agua, por favor ; quiero agua ; necesito agua ; necesito agua, por favor ; me da agua ; me da agua, por favor ; me das agua ; me das agua, por favor ; un agua, por favor | yo quieres agua, por favor (conj.person) | The learner answers the shopkeeper's question; there is no water at the stall: the shopkeeper hands over an empty bottle (host event `stall.give_water`), says the water is at the river, and then offers bread (`quieres-pan`). The objective needs the water, which the player draws at the river (S6). | Quiero agua, por favor. = I want water, please. · quiero = I want · agua = water · por favor = please | shop_help_ask.1 | llm-opus (provisional) | 2026-10-21 |
| quieres-pan | hears | ¿Quieres pan? | Would you like bread? | - | stall-offer | - | - | The shopkeeper offers bread right after handing over the bottle. Comprehension: the learner answers `si-por-favor` or `no-gracias`. | ¿Quieres pan? = Would you like bread? · pan = bread · quieres = you want | shop_offer.1 | llm-opus (provisional) | 2026-10-21 |
| si-por-favor | says | Sí, por favor. | Yes, please. | confirm | stall-offer | sí ; claro ; sí, gracias ; sí, quiero ; por favor, sí | sí, por favo (vocab.unknown) | The learner accepts the bread (the shopkeeper gives it, host event `stall.give_bread`). The same answer works for the guide's `¿Quieres agua?` at the camp (the transfer step). | Sí, por favor. = Yes, please. | shop_help_offer.1 ; guide_offer_help.1 | llm-opus (provisional) | 2026-10-21 |
| no-gracias | says | No, gracias. | No, thank you. | deny | stall-offer | no ; no, muchas gracias ; gracias, no | no, mucho gracias (gender.agree, noun.number) | The learner declines the bread (the shopkeeper says `Está bien.`). The same answer works for the guide's `¿Quieres agua?`. | No, gracias. = No, thank you. | shop_help_offer.1 ; guide_offer_help.1 | llm-opus (provisional) | 2026-10-21 |
| donde-esta-el-rio | says | ¿Dónde está el río? | Where is the river? | ask_directions | villager-open | dónde está el río, por favor ; dónde queda el río ; busco el río | dónde está la río (gender.agree) | The learner asks the villager (or the guide) where the river is; the villager points the way (host event `villager.point`), the player follows it and fills the bottle. | ¿Dónde está el río? = Where is the river? · dónde = where · río = river | villager_help.1 | llm-opus (provisional) | 2026-10-21 |
| hola | both | Hola. | Hello. | greet | villager-open | buen día ; buenos días ; buenas tardes ; buenas noches ; hola, Luna | buenos día (noun.number) | The villager greets the player first (`Hola.`); the learner greets back. The guide and the shopkeeper are greeted the same way. Unit 1 of the guide's lesson (S17): Luna says `¡Hola!` and the learner says it back. | Hola. = Hello. | villager_hello.1 ; guide.lesson.hello.1 | llm-opus (provisional) | 2026-10-22 |
| gracias | says | Gracias. | Thank you. | thank | villager-open | muchas gracias | mucho gracias (gender.agree, noun.number) | After the bottle, the bread, the bread from the villager or the guide's help; the NPC answers `De nada.`. | Gracias. = Thank you. | - | llm-opus (provisional) | 2026-10-21 |
| adios | says | Adiós. | Goodbye. | farewell | villager-open | hasta luego ; nos vemos ; hasta mañana | yo nos vemos (conj.person) | Leaving any NPC; the NPC answers with its own goodbye. The learner error example is contrived: the plainer `nos veo` is not flagged by the core, so the example is `yo nos vemos`. | Adiós. = Goodbye. | shop_bye.1 ; villager_bye.1 | llm-opus (provisional) | 2026-10-21 |
| no-se | says | No sé. | I don't know. | dont_know | villager-open | no lo sé ; yo no sé | no sabo (conj.irregular) | At any prompt, any time; never a failure. The NPC answers with the model phrase to say (for the stall: `Puedes decir: «Quiero agua, por favor».`). The game's `No sé` button does the same. | No sé. = I don't know. | - | llm-opus (provisional) | 2026-10-21 |
| ayuda | says | Ayuda. | Help. | ask_help | villager-open | ¿me ayudas? ; ayuda, por favor ; ¿me puedes ayudar? ; necesito ayuda | yo necesitas ayuda (conj.person) | At any prompt, any time. The NPC answers with the model phrase to say. The guide answers with a hint about the world. | Ayuda. = Help. · necesito ayuda = I need help | - | llm-opus (provisional) | 2026-10-21 |
| me-llamo | hears | Me llamo Luna. | My name is Luna. | - | lesson-name | - | - | Unit 1 of the guide's lesson (S17): Luna waves, says `¡Hola!` and then her name. Comprehension only: the learner does not say a name (core finding M29d), and the English says so. | Me llamo Luna. = My name is Luna. · me llamo = my name is | guide.lesson.name.1 | llm-opus (provisional) | 2026-10-22 |
| es-madera | hears | ¿Es madera? | Is it wood? | - | lesson-yesno | - | - | Unit 2: Luna holds up a log and asks; the learner answers `Sí.` (`si`). She asks `¿Es agua?` next (the same pattern with another noun, listed below) and the learner answers `No.` (`no`). | ¿Es madera? = Is it wood? · es = it is · madera = wood | guide.lesson.is_wood.1 | llm-opus (provisional) | 2026-10-22 |
| si | says | Sí. | Yes. | confirm | lesson-yesno | claro ; claro que sí ; sí, es madera | sí, es un madera (gender.agree) | Unit 2: the learner answers `¿Es madera?` with it. The same intent as `si-por-favor`; `Sí, por favor.` is the phrase for an offer (stall, `¿Quieres fruta?`). | Sí. = Yes. | - | llm-opus (provisional) | 2026-10-22 |
| no | says | No. | No. | deny | lesson-yesno | no es agua ; no, es madera | no, es un madera (gender.agree) | Unit 2: the learner answers `¿Es agua?` while Luna holds up wood. The same intent as `no-gracias`; `No, gracias.` is the phrase for an offer. | No. = No. | - | llm-opus (provisional) | 2026-10-22 |
| que-es-esto | says | ¿Qué es esto? | What is this? | ask_what | lesson-what | qué es eso ; cómo se llama esto ; y esto | cuál es esto (vocab.wrong) | Unit 3: Luna points at something and the learner asks; she names it (`Esto es madera.`, `Esto es una fruta.`, `Esto es agua.`). Outside the lesson the guide answers it for the block under the crosshair, and she asks it herself in her word review. | ¿Qué es esto? = What is this? · qué = what · esto = this | - | llm-opus (provisional) | 2026-10-22 |
| quieres-fruta | hears | ¿Quieres fruta? | Would you like some fruit? | - | lesson-offer | - | - | Unit 4: Luna offers fruit; the learner answers `si-por-favor`. Comprehension of the pattern `¿Quieres + noun?` the learner met at the stall (`quieres-pan`). | ¿Quieres fruta? = Would you like some fruit? · quieres = you want · fruta = fruit | guide.lesson.offer.1 | llm-opus (provisional) | 2026-10-22 |
| de-nada | hears | De nada. | You are welcome. | - | lesson-thanks | - | - | Unit 4: Luna answers the learner's `Gracias.` The shopkeeper, the villager and the guide say it in other places too. | De nada. = You are welcome. · de nada = you're welcome | guide.lesson.welcome.1 | llm-opus (provisional) | 2026-10-22 |
| otra-vez | says | Otra vez, por favor. | Again, please. | ask_repeat | lesson-unstuck | otra vez ; repite, por favor ; repite ; puedes repetir ; puedes repetir, por favor | otro vez, por favor (gender.agree) | Unit 5: the learner asks Luna to say her last line again; she does (host event `guide.repeat`: the line is shown and played again). Works with the guide at any time; the `Repetir` button does the same. | Otra vez, por favor. = Again, please. · otra vez = again · por favor = please | - | llm-opus (provisional) | 2026-10-22 |
| mas-despacio | says | Más despacio, por favor. | More slowly, please. | ask_slower | lesson-unstuck | más despacio ; habla más despacio, por favor ; despacio, por favor ; más lento ; más lento, por favor | más despasio, por favor (vocab.unknown) | Unit 5: the learner asks Luna to say her last line again, slowly; she does (host event `guide.repeat_slow`: the slow recording plays). Works with the guide at any time; the `Más despacio` button does the same. | Más despacio, por favor. = More slowly, please. · más = more · despacio = slowly | - | llm-opus (provisional) | 2026-10-22 |
| necesitas-madera | hears | Necesitas madera y frutas. | You need wood and fruit. | - | lesson-task | - | - | Unit 6: Luna says what the camp needs (a listen step with the English on offer), and the camp checklist begins. Understood only; the learner is not asked to say it. | Necesitas madera y frutas. = You need wood and fruit. · necesitas = you need · madera = wood · frutas = fruit | guide.lesson.task.1 | llm-opus (provisional) | 2026-10-22 |

## NPC lines outside the phrase rows

These bundle lines are spoken by the NPCs but are not phrases: they are not taught, not tracked and have no card. They are listed so the
reviewer reads every Spanish line the learner can hear (the content-trust gate covers recordings too). The bundle file is
`game-v1/dialogue/dialogue.json`; the line id is the pool id plus `.1`, `.2`.

- The shopkeeper: `Toma la botella. El agua está en el río.` / `Aquí tienes la botella. El agua está en el río.` (gives the empty bottle, S6; if the exchange ends without a request the shopkeeper says the same line and hands it over anyway, fail-forward), `Aquí tienes el pan.` / `Toma el pan.`, `Está bien.` / `Bueno.` (declined),
  `De nada.` / `¡Con gusto!`, `¡Adiós! Hasta luego.` / `Hasta luego.`, `¡Hasta luego!` / `¡Nos vemos!`, and the two help lines
  (`Puedes decir: «Quiero agua, por favor».` and `Puedes decir: «Sí, por favor» o «No, gracias».`).
- The villager: `¡Hola! ¿Qué buscas?`, `Mira, está allí.` / `Está allí, mira.`, `Aquí tienes.` / `Toma.` (a request at a house), `De nada.` /
  `¡Con gusto!`, `¡Adiós! Hasta luego.` / `Hasta luego.`, and `Puedes preguntar: «¿Dónde está el río?».`
- The guide's transfer offer at the camp: `¿Quieres agua?` (the transfer of `quieres-pan` to a different NPC), `¡Claro! Aquí tienes.` /
  `Toma.`, `Está bien.` / `Bueno.`, and the help line `Puedes decir: «Sí, por favor» o «No, gracias».`
- Every NPC: `No te entendí. ¿Me lo dices otra vez?` / `Perdón, ¿qué dices?` when a line is not understood, and the help lines of the
  feedback (`Di «ayuda» y te ayudo.`, `Si necesitas ayuda, di «ayuda».`). A line that is not understood gets these help lines and never a
  hint (S9, D-game-9): the feedback policy's not-communicated type is `SlowRepeat`, whose pool holds only the help lines; a hint
  (`Casi. Piensa otra vez.`) is said only for a real language error in a turn that was understood.
- The guide's phrase review (S8, S11, the request of a due phrase in context): `¿Qué necesitas?` (for `pedir-agua`, graded at the stall's
  prompt), `¿Qué buscas?` (for `donde-esta-el-rio`, graded at the villager's prompt), and `¿Quieres agua?` for the yes and the no (`si-por-favor`,
  `no-gracias`). The guide accepts the request, the yes and the no with `¡Muy bien!`, `¡Bien!` or `¡Eso es!` (the bundle's `answer_accepted` pool).
  All four are A1 lines already in the bundle as other NPCs' lines (cefr-check clean at A1 and A2). The review lines are not reviewed by a
  Spanish speaker or an LLM pass yet (provisional); the owner's listening step covers their WAVs (game/docs/SPEECH_CHECKLIST.md).
- The guide's existing lines (greeting, pointing, crafting, naming, the review) are the P-series content and are not part of this sheet.
- The guide's lesson (S17), lines that are in no row, read by the LLM pass (`llm-opus (provisional)`, 2026-10-22): `¡Hola!` (`guide.lesson.hello.1`, the greeting the learner says back), `¿Es agua?` (`guide.lesson.is_water.1`,
  the second yes-or-no question, answered `No.`), `Mira.` (`guide.lesson.look.1`), the six namings `Esto es madera.`, `Esto es una fruta.`, `Esto es agua.`, `Esto es una botella.`,
  `Esto es una mesa.`, `Esto es una antorcha.` and `Esto es una piedra.` (`guide.lesson.it_is.1` to `.7`; the stone is what she points at before the learner says `No sé`; the learner asks `¿Qué es esto?` and she names what she points at), `Toma una fruta.`
  (`guide.lesson.take.1`), `¡Muy bien! Tienes una fruta.` (`guide.lesson.have.1`, after the learner picks the fruit), `Mira. La madera es para la antorcha.` (`guide.lesson.fast.1`, the line the
  learner has repeated and slowed) and the closing line `¡Muy bien! Ya tienes fuego.` (`guide.lesson.end.1`, said when the player's own fire is lit). The lesson also reuses
  the existing `Mira. ¿Qué es esto?` (`guide.review.ask.1`, the question the learner answers with `No sé`). Her replies to `Otra vez, por favor` and `Más despacio, por favor`
  outside the lesson are `Claro. Otra vez.` / `¡Claro! Te lo digo otra vez.` (`guide_again`) and `Claro. Más despacio.` / `¡Claro! Te lo digo más despacio.` (`guide_slower`); she
  then says her last line again. The lesson prompts (`lesson-*` in `prompts/prompts.json`) carry the text of the line they are said at.
- The guide's phrase review (S8) also asks `hola` (with `¡Hola!`), `si` and `no` (with `¿Quieres fruta?`) when they are due, using the lesson's lines.
