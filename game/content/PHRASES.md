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

**The review is provisional.** Every row was read by an LLM pass (Opus, 2026-10-21, D-game-12) for naturalness, Latin American neutral usage and
the English meaning, and its advice is applied. This is not native-speaker approval: the rows stay `llm-opus (provisional)` until a Spanish speaker
checks them. On the reviewer's advice the English of `pedir-agua` is `I want water, please.`, which departs from the brief's gloss ("I would like
water, please"): `quiero` is "I want", and the polite register is carried by `por favor`.

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
| hola | both | Hola. | Hello. | greet | villager-open | buen día ; buenos días ; buenas tardes ; buenas noches | buenos día (noun.number) | The villager greets the player first (`Hola.`); the learner greets back. The guide and the shopkeeper are greeted the same way. | Hola. = Hello. | villager_hello.1 | llm-opus (provisional) | 2026-10-21 |
| gracias | says | Gracias. | Thank you. | thank | villager-open | muchas gracias | mucho gracias (gender.agree, noun.number) | After the bottle, the bread, the bread from the villager or the guide's help; the NPC answers `De nada.`. | Gracias. = Thank you. | - | llm-opus (provisional) | 2026-10-21 |
| adios | says | Adiós. | Goodbye. | farewell | villager-open | hasta luego ; nos vemos ; hasta mañana | yo nos vemos (conj.person) | Leaving any NPC; the NPC answers with its own goodbye. The learner error example is contrived: the plainer `nos veo` is not flagged by the core, so the example is `yo nos vemos`. | Adiós. = Goodbye. | shop_bye.1 ; villager_bye.1 | llm-opus (provisional) | 2026-10-21 |
| no-se | says | No sé. | I don't know. | dont_know | villager-open | no lo sé ; yo no sé | no sabo (conj.irregular) | At any prompt, any time; never a failure. The NPC answers with the model phrase to say (for the stall: `Puedes decir: «Quiero agua, por favor».`). The game's `No sé` button does the same. | No sé. = I don't know. | - | llm-opus (provisional) | 2026-10-21 |
| ayuda | says | Ayuda. | Help. | ask_help | villager-open | ¿me ayudas? ; ayuda, por favor ; ¿me puedes ayudar? ; necesito ayuda | yo necesitas ayuda (conj.person) | At any prompt, any time. The NPC answers with the model phrase to say. The guide answers with a hint about the world. | Ayuda. = Help. · necesito ayuda = I need help | - | llm-opus (provisional) | 2026-10-21 |

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
  feedback (`Di «ayuda» y te ayudo.`, `Si necesitas ayuda, di «ayuda».`).
- The guide's existing lines (greeting, pointing, crafting, naming, the review) are the P-series content and are not part of this sheet.
