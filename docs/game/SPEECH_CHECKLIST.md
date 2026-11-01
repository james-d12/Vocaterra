# Speech checklist (S9, D-game-12 step 4)

The owner listens to every file for cut-offs and for text and audio that do not match (the interim content-trust check of
D-game-12). Mark each row `ok`, `cut-off` or `mismatch` in the last column, with a note after it if needed. The table is written by
`scripts/prerender-speech.sh` from the bundle: the marks of a row are kept while its text and its recording are unchanged, and a row
whose text changed or whose file was recorded again differently is listed again without a mark. The files are Kokoro model output
(kokoro-multi-lang-v1_0), in the voices below.

## Speakers

| Speaker | Voice | Kokoro speaker | Speed (slow) | Says |
| --- | --- | --- | --- | --- |
| guide (Luna) | `ef_dora` | 28 | 1.0 (0.8) | every pool no other NPC has, its prompts, the word cards and the model answers |
| shopkeeper (Tendero) | `em_alex` | 29 | 1.0 (0.8) | the pools `shop_*` and `guide.lesson.shop.*`, and its prompts |
| villager (Aldeano) | `em_santa` | 53 | 1.0 (0.8) | the pools `villager_*` and `guide.lesson.villager.*`, and its prompts |
| fisher (Mar) | `em_santa` | 53 | 0.9 (0.72) | the pools `fisher_*` and `guide.lesson.fisher.*`, and its prompts |
| baker (Luz) | `if_sara`, gain 0.32 | 35 | 1.0 (0.8) | the pools `baker_*` and `guide.lesson.baker.*`, and its prompts |
| smith (Domingo) | `im_nicola`, gain 0.6 | 36 | 1.0 (0.8) | the pools `smith_*` and `guide.lesson.smith.*`, and its prompts |
| farmer (León) | `pm_santa` | 44 | 1.0 (0.8) | the pools `farmer_*` and `guide.lesson.farmer.*`, and its prompts |

**Provisional voices** (VILLAGE_PLAN V06, D-game-22 (d)). The voices below were chosen from the voice trial and their NPCs' lines recorded
in them before the owner heard them: hear each in the trial, `docs/game/voice-trial/` (its README; the trial is at the model's own level,
the recordings carry the gain of the table above), then the NPC's rows. A voice the owner rejects is changed in the NPC's file,
`content/game-v1/game/npcs/<id>.json` (`voice`: `speaker`, `name`, `speed`, and `gain` for a voice louder or quieter than the Spanish
three), and `scripts/limited.sh scripts/prerender-speech.sh` renders that NPC's lines again: the speech index records each file's voice, so
only its files are rendered (`--force` renders every file and is not needed), and their rows lose their marks. The bundle's hash moves, so
the content loop follows (`content/README.md`, "The edit loop"). A voice is listed here while the trial's `trial.json` names its NPC.

| NPC | Voice | Kokoro speaker | Speed | Trial file |
| --- | --- | --- | --- | --- |
| baker (Luz) | `if_sara` | 35 | 1.0 | `voice-trial/if_sara-1.0.wav` |
| smith (Domingo) | `im_nicola` | 36 | 1.0 | `voice-trial/im_nicola-1.0.wav` |
| farmer (León) | `pm_santa` | 44 | 1.0 | `voice-trial/pm_santa-1.0.wav` |
| fisher (Mar) | `em_santa` | 53 | 0.9 | `voice-trial/em_santa-0.9.wav` |

## Lines

| Line id | File | Speaker | Speed | Spanish | ok / cut-off / mismatch |
| --- | --- | --- | --- | --- | --- |
| answer_accepted.1 | speech/answer_accepted.1.slow.wav | guide | 0.8 | ¡Muy bien! |  |
| answer_accepted.1 | speech/answer_accepted.1.wav | guide | 1.0 | ¡Muy bien! |  |
| answer_accepted.2 | speech/answer_accepted.2.slow.wav | guide | 0.8 | ¡Bien! |  |
| answer_accepted.2 | speech/answer_accepted.2.wav | guide | 1.0 | ¡Bien! |  |
| answer_accepted.3 | speech/answer_accepted.3.slow.wav | guide | 0.8 | ¡Eso es! |  |
| answer_accepted.3 | speech/answer_accepted.3.wav | guide | 1.0 | ¡Eso es! |  |
| baker_buy.1 | speech/baker_buy.1.slow.wav | baker | 0.8 | ¡Gracias! Aquí tienes tus monedas. |  |
| baker_buy.1 | speech/baker_buy.1.wav | baker | 1.0 | ¡Gracias! Aquí tienes tus monedas. |  |
| baker_bye.1 | speech/baker_bye.1.slow.wav | baker | 0.8 | ¡Adiós! Hasta luego. |  |
| baker_bye.1 | speech/baker_bye.1.wav | baker | 1.0 | ¡Adiós! Hasta luego. |  |
| baker_bye.2 | speech/baker_bye.2.slow.wav | baker | 0.8 | Hasta luego. |  |
| baker_bye.2 | speech/baker_bye.2.wav | baker | 1.0 | Hasta luego. |  |
| baker_cant_pay.1 | speech/baker_cant_pay.1.slow.wav | baker | 0.8 | Necesitas más monedas. |  |
| baker_cant_pay.1 | speech/baker_cant_pay.1.wav | baker | 1.0 | Necesitas más monedas. |  |
| baker_cant_sell.1 | speech/baker_cant_sell.1.slow.wav | baker | 0.8 | No tienes madera. |  |
| baker_cant_sell.1 | speech/baker_cant_sell.1.wav | baker | 1.0 | No tienes madera. |  |
| baker_delivered.1 | speech/baker_delivered.1.slow.wav | baker | 0.8 | ¡Gracias! Toma cuatro monedas. |  |
| baker_delivered.1 | speech/baker_delivered.1.wav | baker | 1.0 | ¡Gracias! Toma cuatro monedas. |  |
| baker_greet.1 | speech/baker_greet.1.slow.wav | baker | 0.8 | ¿Qué quieres? |  |
| baker_greet.1 | speech/baker_greet.1.wav | baker | 1.0 | ¿Qué quieres? |  |
| baker_hay.1 | speech/baker_hay.1.slow.wav | baker | 0.8 | Aquí hay pan. ¿Cuántos panes quieres? |  |
| baker_hay.1 | speech/baker_hay.1.wav | baker | 1.0 | Aquí hay pan. ¿Cuántos panes quieres? |  |
| baker_hello.1 | speech/baker_hello.1.slow.wav | baker | 0.8 | ¡Hola! |  |
| baker_hello.1 | speech/baker_hello.1.wav | baker | 1.0 | ¡Hola! |  |
| baker_help.1 | speech/baker_help.1.slow.wav | baker | 0.8 | Puedes decir: «Quiero dos panes, por favor». |  |
| baker_help.1 | speech/baker_help.1.wav | baker | 1.0 | Puedes decir: «Quiero dos panes, por favor». |  |
| baker_price.1 | speech/baker_price.1.slow.wav | baker | 0.8 | El pan cuesta dos monedas. |  |
| baker_price.1 | speech/baker_price.1.wav | baker | 1.0 | El pan cuesta dos monedas. |  |
| baker_sell.1 | speech/baker_sell.1.slow.wav | baker | 0.8 | Aquí tienes el pan. ¡Gracias! |  |
| baker_sell.1 | speech/baker_sell.1.wav | baker | 1.0 | Aquí tienes el pan. ¡Gracias! |  |
| baker_sell.2 | speech/baker_sell.2.slow.wav | baker | 0.8 | Toma tu pan. ¡Gracias! |  |
| baker_sell.2 | speech/baker_sell.2.wav | baker | 1.0 | Toma tu pan. ¡Gracias! |  |
| baker_thank.1 | speech/baker_thank.1.slow.wav | baker | 0.8 | De nada. |  |
| baker_thank.1 | speech/baker_thank.1.wav | baker | 1.0 | De nada. |  |
| baker_thank.2 | speech/baker_thank.2.slow.wav | baker | 0.8 | ¡Con gusto! |  |
| baker_thank.2 | speech/baker_thank.2.wav | baker | 1.0 | ¡Con gusto! |  |
| baker_wont_buy.1 | speech/baker_wont_buy.1.slow.wav | baker | 0.8 | No, gracias. Solo compro madera. |  |
| baker_wont_buy.1 | speech/baker_wont_buy.1.wav | baker | 1.0 | No, gracias. Solo compro madera. |  |
| baker_wont_sell.1 | speech/baker_wont_sell.1.slow.wav | baker | 0.8 | Solo vendo pan. |  |
| baker_wont_sell.1 | speech/baker_wont_sell.1.wav | baker | 1.0 | Solo vendo pan. |  |
| card.agua | speech/card.agua.slow.wav | guide | 0.8 | el agua |  |
| card.agua | speech/card.agua.wav | guide | 1.0 | el agua |  |
| card.antorcha | speech/card.antorcha.slow.wav | guide | 0.8 | la antorcha |  |
| card.antorcha | speech/card.antorcha.wav | guide | 1.0 | la antorcha |  |
| card.arbol | speech/card.arbol.slow.wav | guide | 0.8 | el árbol |  |
| card.arbol | speech/card.arbol.wav | guide | 1.0 | el árbol |  |
| card.arena | speech/card.arena.slow.wav | guide | 0.8 | la arena |  |
| card.arena | speech/card.arena.wav | guide | 1.0 | la arena |  |
| card.botella | speech/card.botella.slow.wav | guide | 0.8 | la botella |  |
| card.botella | speech/card.botella.wav | guide | 1.0 | la botella |  |
| card.camino | speech/card.camino.slow.wav | guide | 0.8 | el camino |  |
| card.camino | speech/card.camino.wav | guide | 1.0 | el camino |  |
| card.casa | speech/card.casa.slow.wav | guide | 0.8 | la casa |  |
| card.casa | speech/card.casa.wav | guide | 1.0 | la casa |  |
| card.fruta | speech/card.fruta.slow.wav | guide | 0.8 | la fruta |  |
| card.fruta | speech/card.fruta.wav | guide | 1.0 | la fruta |  |
| card.hacha | speech/card.hacha.slow.wav | guide | 0.8 | el hacha |  |
| card.hacha | speech/card.hacha.wav | guide | 1.0 | el hacha |  |
| card.hierba | speech/card.hierba.slow.wav | guide | 0.8 | la hierba |  |
| card.hierba | speech/card.hierba.wav | guide | 1.0 | la hierba |  |
| card.libro | speech/card.libro.slow.wav | guide | 0.8 | el libro |  |
| card.libro | speech/card.libro.wav | guide | 1.0 | el libro |  |
| card.madera | speech/card.madera.slow.wav | guide | 0.8 | la madera |  |
| card.madera | speech/card.madera.wav | guide | 1.0 | la madera |  |
| card.manzana | speech/card.manzana.slow.wav | guide | 0.8 | la manzana |  |
| card.manzana | speech/card.manzana.wav | guide | 1.0 | la manzana |  |
| card.mesa | speech/card.mesa.slow.wav | guide | 0.8 | la mesa |  |
| card.mesa | speech/card.mesa.wav | guide | 1.0 | la mesa |  |
| card.moneda | speech/card.moneda.slow.wav | guide | 0.8 | la moneda |  |
| card.moneda | speech/card.moneda.wav | guide | 1.0 | la moneda |  |
| card.pan | speech/card.pan.slow.wav | guide | 0.8 | el pan |  |
| card.pan | speech/card.pan.wav | guide | 1.0 | el pan |  |
| card.piedra | speech/card.piedra.slow.wav | guide | 0.8 | la piedra |  |
| card.piedra | speech/card.piedra.wav | guide | 1.0 | la piedra |  |
| card.puente | speech/card.puente.slow.wav | guide | 0.8 | el puente |  |
| card.puente | speech/card.puente.wav | guide | 1.0 | el puente |  |
| card.puerta | speech/card.puerta.slow.wav | guide | 0.8 | la puerta |  |
| card.puerta | speech/card.puerta.wav | guide | 1.0 | la puerta |  |
| card.rio | speech/card.rio.slow.wav | guide | 0.8 | el río |  |
| card.rio | speech/card.rio.wav | guide | 1.0 | el río |  |
| card.tienda | speech/card.tienda.slow.wav | guide | 0.8 | la tienda |  |
| card.tienda | speech/card.tienda.wav | guide | 1.0 | la tienda |  |
| card.tierra | speech/card.tierra.slow.wav | guide | 0.8 | la tierra |  |
| card.tierra | speech/card.tierra.wav | guide | 1.0 | la tierra |  |
| farewell_reply.1 | speech/farewell_reply.1.slow.wav | guide | 0.8 | ¡Adiós, amigo! Hasta luego. |  |
| farewell_reply.1 | speech/farewell_reply.1.wav | guide | 1.0 | ¡Adiós, amigo! Hasta luego. |  |
| farewell_reply.2 | speech/farewell_reply.2.slow.wav | guide | 0.8 | Hasta luego. ¡Buen día! |  |
| farewell_reply.2 | speech/farewell_reply.2.wav | guide | 1.0 | Hasta luego. ¡Buen día! |  |
| farewell_reply.3 | speech/farewell_reply.3.slow.wav | guide | 0.8 | ¡Nos vemos, amigo! |  |
| farewell_reply.3 | speech/farewell_reply.3.wav | guide | 1.0 | ¡Nos vemos, amigo! |  |
| farmer_buy.1 | speech/farmer_buy.1.slow.wav | farmer | 0.8 | ¡Gracias! Aquí tienes tus monedas. |  |
| farmer_buy.1 | speech/farmer_buy.1.wav | farmer | 1.0 | ¡Gracias! Aquí tienes tus monedas. |  |
| farmer_bye.1 | speech/farmer_bye.1.slow.wav | farmer | 0.8 | ¡Adiós! Hasta luego. |  |
| farmer_bye.1 | speech/farmer_bye.1.wav | farmer | 1.0 | ¡Adiós! Hasta luego. |  |
| farmer_bye.2 | speech/farmer_bye.2.slow.wav | farmer | 0.8 | Hasta luego. |  |
| farmer_bye.2 | speech/farmer_bye.2.wav | farmer | 1.0 | Hasta luego. |  |
| farmer_cant_pay.1 | speech/farmer_cant_pay.1.slow.wav | farmer | 0.8 | Necesitas más monedas. Una manzana cuesta una moneda. |  |
| farmer_cant_pay.1 | speech/farmer_cant_pay.1.wav | farmer | 1.0 | Necesitas más monedas. Una manzana cuesta una moneda. |  |
| farmer_cant_sell.1 | speech/farmer_cant_sell.1.slow.wav | farmer | 0.8 | No tienes fruta. |  |
| farmer_cant_sell.1 | speech/farmer_cant_sell.1.wav | farmer | 1.0 | No tienes fruta. |  |
| farmer_delivered.1 | speech/farmer_delivered.1.slow.wav | farmer | 0.8 | ¡Gracias! Toma tres manzanas. |  |
| farmer_delivered.1 | speech/farmer_delivered.1.wav | farmer | 1.0 | ¡Gracias! Toma tres manzanas. |  |
| farmer_greet.1 | speech/farmer_greet.1.slow.wav | farmer | 0.8 | ¡Hola! Este es mi campo. |  |
| farmer_greet.1 | speech/farmer_greet.1.wav | farmer | 1.0 | ¡Hola! Este es mi campo. |  |
| farmer_hello.1 | speech/farmer_hello.1.slow.wav | farmer | 0.8 | ¡Hola! |  |
| farmer_hello.1 | speech/farmer_hello.1.wav | farmer | 1.0 | ¡Hola! |  |
| farmer_help.1 | speech/farmer_help.1.slow.wav | farmer | 0.8 | Puedes preguntar: «¿Qué haces?». |  |
| farmer_help.1 | speech/farmer_help.1.wav | farmer | 1.0 | Puedes preguntar: «¿Qué haces?». |  |
| farmer_job.1 | speech/farmer_job.1.slow.wav | farmer | 0.8 | Soy el granjero. Trabajo en el campo. |  |
| farmer_job.1 | speech/farmer_job.1.wav | farmer | 1.0 | Soy el granjero. Trabajo en el campo. |  |
| farmer_name.1 | speech/farmer_name.1.slow.wav | farmer | 0.8 | Me llamo León. |  |
| farmer_name.1 | speech/farmer_name.1.wav | farmer | 1.0 | Me llamo León. |  |
| farmer_pay.1 | speech/farmer_pay.1.slow.wav | farmer | 0.8 | Aquí se paga con monedas. Pero yo te compro fruta. |  |
| farmer_pay.1 | speech/farmer_pay.1.wav | farmer | 1.0 | Aquí se paga con monedas. Pero yo te compro fruta. |  |
| farmer_price.1 | speech/farmer_price.1.slow.wav | farmer | 0.8 | Una manzana cuesta una moneda. |  |
| farmer_price.1 | speech/farmer_price.1.wav | farmer | 1.0 | Una manzana cuesta una moneda. |  |
| farmer_sell.1 | speech/farmer_sell.1.slow.wav | farmer | 0.8 | Aquí tienes. ¡Gracias! |  |
| farmer_sell.1 | speech/farmer_sell.1.wav | farmer | 1.0 | Aquí tienes. ¡Gracias! |  |
| farmer_sell.2 | speech/farmer_sell.2.slow.wav | farmer | 0.8 | Toma. ¡Gracias! |  |
| farmer_sell.2 | speech/farmer_sell.2.wav | farmer | 1.0 | Toma. ¡Gracias! |  |
| farmer_thank.1 | speech/farmer_thank.1.slow.wav | farmer | 0.8 | De nada. |  |
| farmer_thank.1 | speech/farmer_thank.1.wav | farmer | 1.0 | De nada. |  |
| farmer_thank.2 | speech/farmer_thank.2.slow.wav | farmer | 0.8 | ¡Con gusto! |  |
| farmer_thank.2 | speech/farmer_thank.2.wav | farmer | 1.0 | ¡Con gusto! |  |
| farmer_total.1 | speech/farmer_total.1.slow.wav | farmer | 0.8 | Quince manzanas, quince monedas. |  |
| farmer_total.1 | speech/farmer_total.1.wav | farmer | 1.0 | Quince manzanas, quince monedas. |  |
| farmer_wont_buy.1 | speech/farmer_wont_buy.1.slow.wav | farmer | 0.8 | No, gracias. Solo compro fruta. |  |
| farmer_wont_buy.1 | speech/farmer_wont_buy.1.wav | farmer | 1.0 | No, gracias. Solo compro fruta. |  |
| farmer_wont_sell.1 | speech/farmer_wont_sell.1.slow.wav | farmer | 0.8 | Solo vendo manzanas. |  |
| farmer_wont_sell.1 | speech/farmer_wont_sell.1.wav | farmer | 1.0 | Solo vendo manzanas. |  |
| feedback.hint.1 | speech/feedback.hint.1.slow.wav | guide | 0.8 | Casi. Piensa otra vez. |  |
| feedback.hint.1 | speech/feedback.hint.1.wav | guide | 1.0 | Casi. Piensa otra vez. |  |
| feedback.hint.2 | speech/feedback.hint.2.slow.wav | guide | 0.8 | ¿Seguro? Dilo otra vez. |  |
| feedback.hint.2 | speech/feedback.hint.2.wav | guide | 1.0 | ¿Seguro? Dilo otra vez. |  |
| feedback.hint.gender.agree.1 | speech/feedback.hint.gender.agree.1.slow.wav | guide | 0.8 | Piensa otra vez: ¿el o la? |  |
| feedback.hint.gender.agree.1 | speech/feedback.hint.gender.agree.1.wav | guide | 1.0 | Piensa otra vez: ¿el o la? |  |
| feedback.slow_repeat.1 | speech/feedback.slow_repeat.1.slow.wav | guide | 0.8 | Di «ayuda» y te ayudo. |  |
| feedback.slow_repeat.1 | speech/feedback.slow_repeat.1.wav | guide | 1.0 | Di «ayuda» y te ayudo. |  |
| feedback.slow_repeat.2 | speech/feedback.slow_repeat.2.slow.wav | guide | 0.8 | Si necesitas ayuda, di «ayuda». |  |
| feedback.slow_repeat.2 | speech/feedback.slow_repeat.2.wav | guide | 1.0 | Si necesitas ayuda, di «ayuda». |  |
| fisher_bye.1 | speech/fisher_bye.1.slow.wav | fisher | 0.72 | ¡Adiós! Hasta luego. |  |
| fisher_bye.1 | speech/fisher_bye.1.wav | fisher | 0.9 | ¡Adiós! Hasta luego. |  |
| fisher_bye.2 | speech/fisher_bye.2.slow.wav | fisher | 0.72 | Hasta luego. |  |
| fisher_bye.2 | speech/fisher_bye.2.wav | fisher | 0.9 | Hasta luego. |  |
| fisher_delivered.1 | speech/fisher_delivered.1.slow.wav | fisher | 0.72 | ¡Gracias! Toma dos monedas. |  |
| fisher_delivered.1 | speech/fisher_delivered.1.wav | fisher | 0.9 | ¡Gracias! Toma dos monedas. |  |
| fisher_far.1 | speech/fisher_far.1.slow.wav | fisher | 0.72 | ¿Lejos? ¿Qué buscas? |  |
| fisher_far.1 | speech/fisher_far.1.wav | fisher | 0.9 | ¿Lejos? ¿Qué buscas? |  |
| fisher_greet.1 | speech/fisher_greet.1.slow.wav | fisher | 0.72 | ¿Qué buscas? |  |
| fisher_greet.1 | speech/fisher_greet.1.wav | fisher | 0.9 | ¿Qué buscas? |  |
| fisher_hello.1 | speech/fisher_hello.1.slow.wav | fisher | 0.72 | ¡Hola! |  |
| fisher_hello.1 | speech/fisher_hello.1.wav | fisher | 0.9 | ¡Hola! |  |
| fisher_help.1 | speech/fisher_help.1.slow.wav | fisher | 0.72 | Puedes preguntar: «¿Dónde está la panadería?». |  |
| fisher_help.1 | speech/fisher_help.1.wav | fisher | 0.9 | Puedes preguntar: «¿Dónde está la panadería?». |  |
| fisher_near.1 | speech/fisher_near.1.slow.wav | fisher | 0.72 | No, está cerca. |  |
| fisher_near.1 | speech/fisher_near.1.wav | fisher | 0.9 | No, está cerca. |  |
| fisher_point.1 | speech/fisher_point.1.slow.wav | fisher | 0.72 | Mira, está allí. |  |
| fisher_point.1 | speech/fisher_point.1.wav | fisher | 0.9 | Mira, está allí. |  |
| fisher_point.2 | speech/fisher_point.2.slow.wav | fisher | 0.72 | Está por allí, mira. |  |
| fisher_point.2 | speech/fisher_point.2.wav | fisher | 0.9 | Está por allí, mira. |  |
| fisher_thank.1 | speech/fisher_thank.1.slow.wav | fisher | 0.72 | De nada. |  |
| fisher_thank.1 | speech/fisher_thank.1.wav | fisher | 0.9 | De nada. |  |
| fisher_thank.2 | speech/fisher_thank.2.slow.wav | fisher | 0.72 | ¡Con gusto! |  |
| fisher_thank.2 | speech/fisher_thank.2.wav | fisher | 0.9 | ¡Con gusto! |  |
| fisher_unknown_place.1 | speech/fisher_unknown_place.1.slow.wav | fisher | 0.72 | No sé dónde está. |  |
| fisher_unknown_place.1 | speech/fisher_unknown_place.1.wav | fisher | 0.9 | No sé dónde está. |  |
| guide.craft.none.1 | speech/guide.craft.none.1.slow.wav | guide | 0.8 | Eso no se puede hacer. |  |
| guide.craft.none.1 | speech/guide.craft.none.1.wav | guide | 1.0 | Eso no se puede hacer. |  |
| guide.craft.ready.1 | speech/guide.craft.ready.1.slow.wav | guide | 0.8 | ¡Ya lo tienes todo! |  |
| guide.craft.ready.1 | speech/guide.craft.ready.1.wav | guide | 1.0 | ¡Ya lo tienes todo! |  |
| guide.craft.station.none.1 | speech/guide.craft.station.none.1.slow.wav | guide | 0.8 | No necesitas nada más. |  |
| guide.craft.station.none.1 | speech/guide.craft.station.none.1.wav | guide | 1.0 | No necesitas nada más. |  |
| guide.help.ask.1 | speech/guide.help.ask.1.slow.wav | guide | 0.8 | Pregunta: ¿dónde está el árbol? |  |
| guide.help.ask.1 | speech/guide.help.ask.1.wav | guide | 1.0 | Pregunta: ¿dónde está el árbol? |  |
| guide.help.ask.2 | speech/guide.help.ask.2.slow.wav | guide | 0.8 | Pregunta por una cosa. ¿Dónde está la piedra? |  |
| guide.help.ask.2 | speech/guide.help.ask.2.wav | guide | 1.0 | Pregunta por una cosa. ¿Dónde está la piedra? |  |
| guide.help.walk.1 | speech/guide.help.walk.1.slow.wav | guide | 0.8 | Camina hasta la flecha. |  |
| guide.help.walk.1 | speech/guide.help.walk.1.wav | guide | 1.0 | Camina hasta la flecha. |  |
| guide.help.walk.2 | speech/guide.help.walk.2.slow.wav | guide | 0.8 | Ve hasta la flecha. |  |
| guide.help.walk.2 | speech/guide.help.walk.2.wav | guide | 1.0 | Ve hasta la flecha. |  |
| guide.identify.none.1 | speech/guide.identify.none.1.slow.wav | guide | 0.8 | No sé qué es. |  |
| guide.identify.none.1 | speech/guide.identify.none.1.wav | guide | 1.0 | No sé qué es. |  |
| guide.identify.none.2 | speech/guide.identify.none.2.slow.wav | guide | 0.8 | Busca una cosa y pregunta otra vez. |  |
| guide.identify.none.2 | speech/guide.identify.none.2.wav | guide | 1.0 | Busca una cosa y pregunta otra vez. |  |
| guide.lesson.baker.bought.1 | speech/guide.lesson.baker.bought.1.slow.wav | baker | 0.8 | ¿Qué compraste? |  |
| guide.lesson.baker.bought.1 | speech/guide.lesson.baker.bought.1.wav | baker | 1.0 | ¿Qué compraste? |  |
| guide.lesson.baker.coins.1 | speech/guide.lesson.baker.coins.1.slow.wav | baker | 0.8 | ¿Cuántas monedas tienes? |  |
| guide.lesson.baker.coins.1 | speech/guide.lesson.baker.coins.1.wav | baker | 1.0 | ¿Cuántas monedas tienes? |  |
| guide.lesson.baker.fifteen.1 | speech/guide.lesson.baker.fifteen.1.slow.wav | baker | 0.8 | Once, doce, trece, catorce, quince. |  |
| guide.lesson.baker.fifteen.1 | speech/guide.lesson.baker.fifteen.1.wav | baker | 1.0 | Once, doce, trece, catorce, quince. |  |
| guide.lesson.baker.five.1 | speech/guide.lesson.baker.five.1.slow.wav | baker | 0.8 | Uno, dos, tres, cuatro, cinco. |  |
| guide.lesson.baker.five.1 | speech/guide.lesson.baker.five.1.wav | baker | 1.0 | Uno, dos, tres, cuatro, cinco. |  |
| guide.lesson.baker.fresh.1 | speech/guide.lesson.baker.fresh.1.slow.wav | baker | 0.8 | ¡Qué bien! Mañana tendré pan fresco. |  |
| guide.lesson.baker.fresh.1 | speech/guide.lesson.baker.fresh.1.wav | baker | 1.0 | ¡Qué bien! Mañana tendré pan fresco. |  |
| guide.lesson.baker.how_many.1 | speech/guide.lesson.baker.how_many.1.slow.wav | baker | 0.8 | ¿Cuántos panes quieres? |  |
| guide.lesson.baker.how_many.1 | speech/guide.lesson.baker.how_many.1.wav | baker | 1.0 | ¿Cuántos panes quieres? |  |
| guide.lesson.baker.loaves.1 | speech/guide.lesson.baker.loaves.1.slow.wav | baker | 0.8 | Tengo diez panes. |  |
| guide.lesson.baker.loaves.1 | speech/guide.lesson.baker.loaves.1.wav | baker | 1.0 | Tengo diez panes. |  |
| guide.lesson.baker.mar.1 | speech/guide.lesson.baker.mar.1.slow.wav | baker | 0.8 | ¿Y qué hizo Mar ayer? |  |
| guide.lesson.baker.mar.1 | speech/guide.lesson.baker.mar.1.wav | baker | 1.0 | ¿Y qué hizo Mar ayer? |  |
| guide.lesson.baker.market.1 | speech/guide.lesson.baker.market.1.slow.wav | baker | 0.8 | ¡Hoy hay mercado! ¿Cuántos panes tengo? |  |
| guide.lesson.baker.market.1 | speech/guide.lesson.baker.market.1.wav | baker | 1.0 | ¡Hoy hay mercado! ¿Cuántos panes tengo? |  |
| guide.lesson.baker.model.1 | speech/guide.lesson.baker.model.1.slow.wav | baker | 0.8 | Ayer compré fruta en la tienda. |  |
| guide.lesson.baker.model.1 | speech/guide.lesson.baker.model.1.wav | baker | 1.0 | Ayer compré fruta en la tienda. |  |
| guide.lesson.baker.oven.1 | speech/guide.lesson.baker.oven.1.slow.wav | baker | 0.8 | Necesito cinco bloques de madera para el horno. Te pago cuatro monedas. ¿Qué vas a hacer? |  |
| guide.lesson.baker.oven.1 | speech/guide.lesson.baker.oven.1.wav | baker | 1.0 | Necesito cinco bloques de madera para el horno. Te pago cuatro monedas. ¿Qué vas a hacer? |  |
| guide.lesson.baker.pay.1 | speech/guide.lesson.baker.pay.1.slow.wav | baker | 0.8 | Te pago una moneda por cada bloque de madera. |  |
| guide.lesson.baker.pay.1 | speech/guide.lesson.baker.pay.1.wav | baker | 1.0 | Te pago una moneda por cada bloque de madera. |  |
| guide.lesson.baker.plan.1 | speech/guide.lesson.baker.plan.1.slow.wav | baker | 0.8 | ¡Hola! ¿Qué vas a hacer hoy? |  |
| guide.lesson.baker.plan.1 | speech/guide.lesson.baker.plan.1.wav | baker | 1.0 | ¡Hola! ¿Qué vas a hacer hoy? |  |
| guide.lesson.baker.sell.1 | speech/guide.lesson.baker.sell.1.slow.wav | baker | 0.8 | Necesito madera para el horno. ¿Qué quieres hacer? |  |
| guide.lesson.baker.sell.1 | speech/guide.lesson.baker.sell.1.wav | baker | 1.0 | Necesito madera para el horno. ¿Qué quieres hacer? |  |
| guide.lesson.baker.ten.1 | speech/guide.lesson.baker.ten.1.slow.wav | baker | 0.8 | Seis, siete, ocho, nueve, diez. |  |
| guide.lesson.baker.ten.1 | speech/guide.lesson.baker.ten.1.wav | baker | 1.0 | Seis, siete, ocho, nueve, diez. |  |
| guide.lesson.baker.tomorrow.1 | speech/guide.lesson.baker.tomorrow.1.slow.wav | baker | 0.8 | ¿Qué harás mañana? |  |
| guide.lesson.baker.tomorrow.1 | speech/guide.lesson.baker.tomorrow.1.wav | baker | 1.0 | ¿Qué harás mañana? |  |
| guide.lesson.baker.twenty.1 | speech/guide.lesson.baker.twenty.1.slow.wav | baker | 0.8 | Dieciséis, diecisiete, dieciocho, diecinueve, veinte. |  |
| guide.lesson.baker.twenty.1 | speech/guide.lesson.baker.twenty.1.wav | baker | 1.0 | Dieciséis, diecisiete, dieciocho, diecinueve, veinte. |  |
| guide.lesson.baker.you.1 | speech/guide.lesson.baker.you.1.slow.wav | baker | 0.8 | Compraste dos panes. ¡Muy bien! |  |
| guide.lesson.baker.you.1 | speech/guide.lesson.baker.you.1.wav | baker | 1.0 | Compraste dos panes. ¡Muy bien! |  |
| guide.lesson.end.1 | speech/guide.lesson.end.1.slow.wav | guide | 0.8 | ¡Muy bien! Ya tienes fuego. |  |
| guide.lesson.end.1 | speech/guide.lesson.end.1.wav | guide | 1.0 | ¡Muy bien! Ya tienes fuego. |  |
| guide.lesson.farmer.apple.1 | speech/guide.lesson.farmer.apple.1.slow.wav | farmer | 0.8 | ¿Cómo es la manzana? |  |
| guide.lesson.farmer.apple.1 | speech/guide.lesson.farmer.apple.1.wav | farmer | 1.0 | ¿Cómo es la manzana? |  |
| guide.lesson.farmer.apples.1 | speech/guide.lesson.farmer.apples.1.slow.wav | farmer | 0.8 | Mira mis manzanas. Son rojas y grandes. |  |
| guide.lesson.farmer.apples.1 | speech/guide.lesson.farmer.apples.1.wav | farmer | 1.0 | Mira mis manzanas. Son rojas y grandes. |  |
| guide.lesson.farmer.ate.1 | speech/guide.lesson.farmer.ate.1.slow.wav | farmer | 0.8 | Ayer comí pan de Luz. ¡Me gustó mucho! |  |
| guide.lesson.farmer.ate.1 | speech/guide.lesson.farmer.ate.1.wav | farmer | 1.0 | Ayer comí pan de Luz. ¡Me gustó mucho! |  |
| guide.lesson.farmer.bought.1 | speech/guide.lesson.farmer.bought.1.slow.wav | farmer | 0.8 | ¡Hola! ¿Qué compraste en la panadería? |  |
| guide.lesson.farmer.bought.1 | speech/guide.lesson.farmer.bought.1.wav | farmer | 1.0 | ¡Hola! ¿Qué compraste en la panadería? |  |
| guide.lesson.farmer.cheaper.1 | speech/guide.lesson.farmer.cheaper.1.slow.wav | farmer | 0.8 | Una manzana cuesta una moneda, y un pan, dos. ¿Qué es más barato? |  |
| guide.lesson.farmer.cheaper.1 | speech/guide.lesson.farmer.cheaper.1.wav | farmer | 1.0 | Una manzana cuesta una moneda, y un pan, dos. ¿Qué es más barato? |  |
| guide.lesson.farmer.dear.1 | speech/guide.lesson.farmer.dear.1.slow.wav | farmer | 0.8 | ¿Es caro o es barato? |  |
| guide.lesson.farmer.dear.1 | speech/guide.lesson.farmer.dear.1.wav | farmer | 1.0 | ¿Es caro o es barato? |  |
| guide.lesson.farmer.end.1 | speech/guide.lesson.farmer.end.1.slow.wav | farmer | 0.8 | ¡Hablas muy bien español! |  |
| guide.lesson.farmer.end.1 | speech/guide.lesson.farmer.end.1.wav | farmer | 1.0 | ¡Hablas muy bien español! |  |
| guide.lesson.farmer.how_many.1 | speech/guide.lesson.farmer.how_many.1.slow.wav | farmer | 0.8 | ¿Cuántas manzanas quieres? |  |
| guide.lesson.farmer.how_many.1 | speech/guide.lesson.farmer.how_many.1.wav | farmer | 1.0 | ¿Cuántas manzanas quieres? |  |
| guide.lesson.farmer.like.1 | speech/guide.lesson.farmer.like.1.slow.wav | farmer | 0.8 | ¿Te gusta el pan? |  |
| guide.lesson.farmer.like.1 | speech/guide.lesson.farmer.like.1.wav | farmer | 1.0 | ¿Te gusta el pan? |  |
| guide.lesson.farmer.like_apples.1 | speech/guide.lesson.farmer.like_apples.1.slow.wav | farmer | 0.8 | ¿Y te gustan las manzanas? |  |
| guide.lesson.farmer.like_apples.1 | speech/guide.lesson.farmer.like_apples.1.wav | farmer | 1.0 | ¿Y te gustan las manzanas? |  |
| guide.lesson.farmer.liked.1 | speech/guide.lesson.farmer.liked.1.slow.wav | farmer | 0.8 | ¿Te gustó el pan de Luz? |  |
| guide.lesson.farmer.liked.1 | speech/guide.lesson.farmer.liked.1.wav | farmer | 1.0 | ¿Te gustó el pan de Luz? |  |
| guide.lesson.farmer.market.1 | speech/guide.lesson.farmer.market.1.slow.wav | farmer | 0.8 | ¡Hola! Hoy hay mercado. Tengo quince manzanas. |  |
| guide.lesson.farmer.market.1 | speech/guide.lesson.farmer.market.1.wav | farmer | 1.0 | ¡Hola! Hoy hay mercado. Tengo quince manzanas. |  |
| guide.lesson.farmer.water.1 | speech/guide.lesson.farmer.water.1.slow.wav | farmer | 0.8 | Mis plantas necesitan agua. Si me traes agua del río, te doy tres manzanas. ¿Qué vas a hacer? |  |
| guide.lesson.farmer.water.1 | speech/guide.lesson.farmer.water.1.wav | farmer | 1.0 | Mis plantas necesitan agua. Si me traes agua del río, te doy tres manzanas. ¿Qué vas a hacer? |  |
| guide.lesson.farmer.went.1 | speech/guide.lesson.farmer.went.1.slow.wav | farmer | 0.8 | ¿Fuiste al río? |  |
| guide.lesson.farmer.went.1 | speech/guide.lesson.farmer.went.1.wav | farmer | 1.0 | ¿Fuiste al río? |  |
| guide.lesson.farmer.will_sell.1 | speech/guide.lesson.farmer.will_sell.1.slow.wav | farmer | 0.8 | ¿Y qué venderás mañana? |  |
| guide.lesson.farmer.will_sell.1 | speech/guide.lesson.farmer.will_sell.1.wav | farmer | 1.0 | ¿Y qué venderás mañana? |  |
| guide.lesson.fast.1 | speech/guide.lesson.fast.1.slow.wav | guide | 0.8 | Mira. La madera es para la antorcha. |  |
| guide.lesson.fast.1 | speech/guide.lesson.fast.1.wav | guide | 1.0 | Mira. La madera es para la antorcha. |  |
| guide.lesson.fisher.bought.1 | speech/guide.lesson.fisher.bought.1.slow.wav | fisher | 0.72 | ¿Qué compró Luz ayer? |  |
| guide.lesson.fisher.bought.1 | speech/guide.lesson.fisher.bought.1.wav | fisher | 0.9 | ¿Qué compró Luz ayer? |  |
| guide.lesson.fisher.day.1 | speech/guide.lesson.fisher.day.1.slow.wav | fisher | 0.72 | ¡Hola! Mañana Luz hará pan. Domingo trabajará en la herrería. Y yo pescaré en el río. |  |
| guide.lesson.fisher.day.1 | speech/guide.lesson.fisher.day.1.wav | fisher | 0.9 | ¡Hola! Mañana Luz hará pan. Domingo trabajará en la herrería. Y yo pescaré en el río. |  |
| guide.lesson.fisher.end.1 | speech/guide.lesson.fisher.end.1.slow.wav | fisher | 0.72 | ¡Muy bien! Ya conoces el pueblo. |  |
| guide.lesson.fisher.end.1 | speech/guide.lesson.fisher.end.1.wav | fisher | 0.9 | ¡Muy bien! Ya conoces el pueblo. |  |
| guide.lesson.fisher.going.1 | speech/guide.lesson.fisher.going.1.slow.wav | fisher | 0.72 | ¿Adónde vas? |  |
| guide.lesson.fisher.going.1 | speech/guide.lesson.fisher.going.1.wav | fisher | 0.9 | ¿Adónde vas? |  |
| guide.lesson.fisher.hay.1 | speech/guide.lesson.fisher.hay.1.slow.wav | fisher | 0.72 | En el pueblo hay una tienda, una panadería y tres casas. |  |
| guide.lesson.fisher.hay.1 | speech/guide.lesson.fisher.hay.1.wav | fisher | 0.9 | En el pueblo hay una tienda, una panadería y tres casas. |  |
| guide.lesson.fisher.hungry.1 | speech/guide.lesson.fisher.hungry.1.slow.wav | fisher | 0.72 | Tengo hambre. ¿Tienes fruta? |  |
| guide.lesson.fisher.hungry.1 | speech/guide.lesson.fisher.hungry.1.wav | fisher | 0.9 | Tengo hambre. ¿Tienes fruta? |  |
| guide.lesson.fisher.left.1 | speech/guide.lesson.fisher.left.1.slow.wav | fisher | 0.72 | En el pueblo, mira la torre. La panadería está a la izquierda. |  |
| guide.lesson.fisher.left.1 | speech/guide.lesson.fisher.left.1.wav | fisher | 0.9 | En el pueblo, mira la torre. La panadería está a la izquierda. |  |
| guide.lesson.fisher.luz_will.1 | speech/guide.lesson.fisher.luz_will.1.slow.wav | fisher | 0.72 | ¿Qué hará Luz mañana? |  |
| guide.lesson.fisher.luz_will.1 | speech/guide.lesson.fisher.luz_will.1.wav | fisher | 0.9 | ¿Qué hará Luz mañana? |  |
| guide.lesson.fisher.more.1 | speech/guide.lesson.fisher.more.1.slow.wav | fisher | 0.72 | ¿Algo más? |  |
| guide.lesson.fisher.more.1 | speech/guide.lesson.fisher.more.1.wav | fisher | 0.9 | ¿Algo más? |  |
| guide.lesson.fisher.near.1 | speech/guide.lesson.fisher.near.1.slow.wav | fisher | 0.72 | No, está cerca. La montaña está lejos. |  |
| guide.lesson.fisher.near.1 | speech/guide.lesson.fisher.near.1.wav | fisher | 0.9 | No, está cerca. La montaña está lejos. |  |
| guide.lesson.fisher.plan.1 | speech/guide.lesson.fisher.plan.1.slow.wav | fisher | 0.72 | ¿Qué vas a hacer allí? |  |
| guide.lesson.fisher.plan.1 | speech/guide.lesson.fisher.plan.1.wav | fisher | 0.9 | ¿Qué vas a hacer allí? |  |
| guide.lesson.fisher.right.1 | speech/guide.lesson.fisher.right.1.slow.wav | fisher | 0.72 | Y la herrería está a la derecha. |  |
| guide.lesson.fisher.right.1 | speech/guide.lesson.fisher.right.1.wav | fisher | 0.9 | Y la herrería está a la derecha. |  |
| guide.lesson.fisher.sold.1 | speech/guide.lesson.fisher.sold.1.slow.wav | fisher | 0.72 | ¡Hola! Ayer le vendí pescado a Luz. Mañana va a comprar más. |  |
| guide.lesson.fisher.sold.1 | speech/guide.lesson.fisher.sold.1.wav | fisher | 0.9 | ¡Hola! Ayer le vendí pescado a Luz. Mañana va a comprar más. |  |
| guide.lesson.fisher.tomorrow.1 | speech/guide.lesson.fisher.tomorrow.1.slow.wav | fisher | 0.72 | ¿Y tú? ¿Qué vas a hacer mañana? |  |
| guide.lesson.fisher.tomorrow.1 | speech/guide.lesson.fisher.tomorrow.1.wav | fisher | 0.9 | ¿Y tú? ¿Qué vas a hacer mañana? |  |
| guide.lesson.fisher.well.1 | speech/guide.lesson.fisher.well.1.slow.wav | fisher | 0.72 | Sí, hay un pozo. |  |
| guide.lesson.fisher.well.1 | speech/guide.lesson.fisher.well.1.wav | fisher | 0.9 | Sí, hay un pozo. |  |
| guide.lesson.fisher.went.1 | speech/guide.lesson.fisher.went.1.slow.wav | fisher | 0.72 | ¿Fuiste a la panadería? |  |
| guide.lesson.fisher.went.1 | speech/guide.lesson.fisher.went.1.wav | fisher | 0.9 | ¿Fuiste a la panadería? |  |
| guide.lesson.have.1 | speech/guide.lesson.have.1.slow.wav | guide | 0.8 | ¡Muy bien! Tienes una fruta. |  |
| guide.lesson.have.1 | speech/guide.lesson.have.1.wav | guide | 1.0 | ¡Muy bien! Tienes una fruta. |  |
| guide.lesson.have_fruit.1 | speech/guide.lesson.have_fruit.1.slow.wav | guide | 0.8 | ¿Tienes una fruta? |  |
| guide.lesson.have_fruit.1 | speech/guide.lesson.have_fruit.1.wav | guide | 1.0 | ¿Tienes una fruta? |  |
| guide.lesson.hello.1 | speech/guide.lesson.hello.1.slow.wav | guide | 0.8 | ¡Hola! |  |
| guide.lesson.hello.1 | speech/guide.lesson.hello.1.wav | guide | 1.0 | ¡Hola! |  |
| guide.lesson.is_water.1 | speech/guide.lesson.is_water.1.slow.wav | guide | 0.8 | ¿Es agua? |  |
| guide.lesson.is_water.1 | speech/guide.lesson.is_water.1.wav | guide | 1.0 | ¿Es agua? |  |
| guide.lesson.is_wood.1 | speech/guide.lesson.is_wood.1.slow.wav | guide | 0.8 | ¿Es madera? |  |
| guide.lesson.is_wood.1 | speech/guide.lesson.is_wood.1.wav | guide | 1.0 | ¿Es madera? |  |
| guide.lesson.it_is.1 | speech/guide.lesson.it_is.1.slow.wav | guide | 0.8 | Esto es madera. |  |
| guide.lesson.it_is.1 | speech/guide.lesson.it_is.1.wav | guide | 1.0 | Esto es madera. |  |
| guide.lesson.it_is.2 | speech/guide.lesson.it_is.2.slow.wav | guide | 0.8 | Esto es una fruta. |  |
| guide.lesson.it_is.2 | speech/guide.lesson.it_is.2.wav | guide | 1.0 | Esto es una fruta. |  |
| guide.lesson.it_is.3 | speech/guide.lesson.it_is.3.slow.wav | guide | 0.8 | Esto es agua. |  |
| guide.lesson.it_is.3 | speech/guide.lesson.it_is.3.wav | guide | 1.0 | Esto es agua. |  |
| guide.lesson.it_is.4 | speech/guide.lesson.it_is.4.slow.wav | guide | 0.8 | Esto es una botella. |  |
| guide.lesson.it_is.4 | speech/guide.lesson.it_is.4.wav | guide | 1.0 | Esto es una botella. |  |
| guide.lesson.it_is.5 | speech/guide.lesson.it_is.5.slow.wav | guide | 0.8 | Esto es una mesa. |  |
| guide.lesson.it_is.5 | speech/guide.lesson.it_is.5.wav | guide | 1.0 | Esto es una mesa. |  |
| guide.lesson.it_is.6 | speech/guide.lesson.it_is.6.slow.wav | guide | 0.8 | Esto es una antorcha. |  |
| guide.lesson.it_is.6 | speech/guide.lesson.it_is.6.wav | guide | 1.0 | Esto es una antorcha. |  |
| guide.lesson.it_is.7 | speech/guide.lesson.it_is.7.slow.wav | guide | 0.8 | Esto es una piedra. |  |
| guide.lesson.it_is.7 | speech/guide.lesson.it_is.7.wav | guide | 1.0 | Esto es una piedra. |  |
| guide.lesson.look.1 | speech/guide.lesson.look.1.slow.wav | guide | 0.8 | Mira. |  |
| guide.lesson.look.1 | speech/guide.lesson.look.1.wav | guide | 1.0 | Mira. |  |
| guide.lesson.name.1 | speech/guide.lesson.name.1.slow.wav | guide | 0.8 | Me llamo Luna. |  |
| guide.lesson.name.1 | speech/guide.lesson.name.1.wav | guide | 1.0 | Me llamo Luna. |  |
| guide.lesson.need.1 | speech/guide.lesson.need.1.slow.wav | guide | 0.8 | ¿Qué necesitas? |  |
| guide.lesson.need.1 | speech/guide.lesson.need.1.wav | guide | 1.0 | ¿Qué necesitas? |  |
| guide.lesson.offer.1 | speech/guide.lesson.offer.1.slow.wav | guide | 0.8 | ¿Quieres fruta? |  |
| guide.lesson.offer.1 | speech/guide.lesson.offer.1.wav | guide | 1.0 | ¿Quieres fruta? |  |
| guide.lesson.shop.all.1 | speech/guide.lesson.shop.all.1.slow.wav | shopkeeper | 0.8 | ¿Qué quieren ustedes? |  |
| guide.lesson.shop.all.1 | speech/guide.lesson.shop.all.1.wav | shopkeeper | 1.0 | ¿Qué quieren ustedes? |  |
| guide.lesson.shop.ate.1 | speech/guide.lesson.shop.ate.1.slow.wav | shopkeeper | 0.8 | ¿Qué comiste ayer? |  |
| guide.lesson.shop.ate.1 | speech/guide.lesson.shop.ate.1.wav | shopkeeper | 1.0 | ¿Qué comiste ayer? |  |
| guide.lesson.shop.ayer.1 | speech/guide.lesson.shop.ayer.1.slow.wav | shopkeeper | 0.8 | Ayer comí pan. |  |
| guide.lesson.shop.ayer.1 | speech/guide.lesson.shop.ayer.1.wav | shopkeeper | 1.0 | Ayer comí pan. |  |
| guide.lesson.shop.big.1 | speech/guide.lesson.shop.big.1.slow.wav | shopkeeper | 0.8 | Mira este pan. ¿Cómo es? |  |
| guide.lesson.shop.big.1 | speech/guide.lesson.shop.big.1.wav | shopkeeper | 1.0 | Mira este pan. ¿Cómo es? |  |
| guide.lesson.shop.bottle.1 | speech/guide.lesson.shop.bottle.1.slow.wav | shopkeeper | 0.8 | ¿Tiene usted una botella? |  |
| guide.lesson.shop.bottle.1 | speech/guide.lesson.shop.bottle.1.wav | shopkeeper | 1.0 | ¿Tiene usted una botella? |  |
| guide.lesson.shop.bread.1 | speech/guide.lesson.shop.bread.1.slow.wav | shopkeeper | 0.8 | Sí, hay pan. Es pan de Luz. |  |
| guide.lesson.shop.bread.1 | speech/guide.lesson.shop.bread.1.wav | shopkeeper | 1.0 | Sí, hay pan. Es pan de Luz. |  |
| guide.lesson.shop.had.1 | speech/guide.lesson.shop.had.1.slow.wav | shopkeeper | 0.8 | ¿Bebiste agua ayer? |  |
| guide.lesson.shop.had.1 | speech/guide.lesson.shop.had.1.wav | shopkeeper | 1.0 | ¿Bebiste agua ayer? |  |
| guide.lesson.shop.luz.1 | speech/guide.lesson.shop.luz.1.slow.wav | shopkeeper | 0.8 | El pan es de Luz, de la panadería. |  |
| guide.lesson.shop.luz.1 | speech/guide.lesson.shop.luz.1.wav | shopkeeper | 1.0 | El pan es de Luz, de la panadería. |  |
| guide.lesson.shop.market.1 | speech/guide.lesson.shop.market.1.slow.wav | shopkeeper | 0.8 | ¡Hola! ¿Qué compraste en el mercado? |  |
| guide.lesson.shop.market.1 | speech/guide.lesson.shop.market.1.wav | shopkeeper | 1.0 | ¡Hola! ¿Qué compraste en el mercado? |  |
| guide.lesson.shop.need.1 | speech/guide.lesson.shop.need.1.slow.wav | shopkeeper | 0.8 | ¿Qué necesita usted? |  |
| guide.lesson.shop.need.1 | speech/guide.lesson.shop.need.1.wav | shopkeeper | 1.0 | ¿Qué necesita usted? |  |
| guide.lesson.shop.they.1 | speech/guide.lesson.shop.they.1.slow.wav | shopkeeper | 0.8 | Ellos quieren agua. |  |
| guide.lesson.shop.they.1 | speech/guide.lesson.shop.they.1.wav | shopkeeper | 1.0 | Ellos quieren agua. |  |
| guide.lesson.shop.tomorrow.1 | speech/guide.lesson.shop.tomorrow.1.slow.wav | shopkeeper | 0.8 | ¿Qué vas a hacer mañana? |  |
| guide.lesson.shop.tomorrow.1 | speech/guide.lesson.shop.tomorrow.1.wav | shopkeeper | 1.0 | ¿Qué vas a hacer mañana? |  |
| guide.lesson.shop.went.1 | speech/guide.lesson.shop.went.1.slow.wav | shopkeeper | 0.8 | ¿Fuiste al río ayer? |  |
| guide.lesson.shop.went.1 | speech/guide.lesson.shop.went.1.wav | shopkeeper | 1.0 | ¿Fuiste al río ayer? |  |
| guide.lesson.shop.you.1 | speech/guide.lesson.shop.you.1.slow.wav | shopkeeper | 0.8 | Ayer fuiste al río. |  |
| guide.lesson.shop.you.1 | speech/guide.lesson.shop.you.1.wav | shopkeeper | 1.0 | Ayer fuiste al río. |  |
| guide.lesson.smith.ate.1 | speech/guide.lesson.smith.ate.1.slow.wav | smith | 0.8 | ¡Hola! Ayer trabajé todo el día. ¿Qué comieron Luz y Mar? |  |
| guide.lesson.smith.ate.1 | speech/guide.lesson.smith.ate.1.wav | smith | 1.0 | ¡Hola! Ayer trabajé todo el día. ¿Qué comieron Luz y Mar? |  |
| guide.lesson.smith.go.1 | speech/guide.lesson.smith.go.1.slow.wav | smith | 0.8 | ¡Muy bien! Mar pescará en el río toda la tarde. ¿Adónde irás ahora? |  |
| guide.lesson.smith.go.1 | speech/guide.lesson.smith.go.1.wav | smith | 1.0 | ¡Muy bien! Mar pescará en el río toda la tarde. ¿Adónde irás ahora? |  |
| guide.lesson.smith.lives.1 | speech/guide.lesson.smith.lives.1.slow.wav | smith | 0.8 | ¿Dónde vives? |  |
| guide.lesson.smith.lives.1 | speech/guide.lesson.smith.lives.1.wav | smith | 1.0 | ¿Dónde vives? |  |
| guide.lesson.smith.must.1 | speech/guide.lesson.smith.must.1.slow.wav | smith | 0.8 | Mar fue al río y te busca. ¿Adónde tienes que ir? |  |
| guide.lesson.smith.must.1 | speech/guide.lesson.smith.must.1.wav | smith | 1.0 | Mar fue al río y te busca. ¿Adónde tienes que ir? |  |
| guide.lesson.smith.wood.1 | speech/guide.lesson.smith.wood.1.slow.wav | smith | 0.8 | ¡Hola! Luz necesita madera para el horno. ¿Qué harás? |  |
| guide.lesson.smith.wood.1 | speech/guide.lesson.smith.wood.1.wav | smith | 1.0 | ¡Hola! Luz necesita madera para el horno. ¿Qué harás? |  |
| guide.lesson.take.1 | speech/guide.lesson.take.1.slow.wav | guide | 0.8 | Toma una fruta. |  |
| guide.lesson.take.1 | speech/guide.lesson.take.1.wav | guide | 1.0 | Toma una fruta. |  |
| guide.lesson.task.1 | speech/guide.lesson.task.1.slow.wav | guide | 0.8 | Necesitas madera y frutas. |  |
| guide.lesson.task.1 | speech/guide.lesson.task.1.wav | guide | 1.0 | Necesitas madera y frutas. |  |
| guide.lesson.villager.ate.1 | speech/guide.lesson.villager.ate.1.slow.wav | villager | 0.8 | Por la tarde, Luz y Mar comieron pan en la plaza. |  |
| guide.lesson.villager.ate.1 | speech/guide.lesson.villager.ate.1.wav | villager | 1.0 | Por la tarde, Luz y Mar comieron pan en la plaza. |  |
| guide.lesson.villager.axe.1 | speech/guide.lesson.villager.axe.1.slow.wav | villager | 0.8 | Domingo te quiere hacer un hacha. |  |
| guide.lesson.villager.axe.1 | speech/guide.lesson.villager.axe.1.wav | villager | 1.0 | Domingo te quiere hacer un hacha. |  |
| guide.lesson.villager.be.1 | speech/guide.lesson.villager.be.1.slow.wav | villager | 0.8 | Tú y yo somos amigos. |  |
| guide.lesson.villager.be.1 | speech/guide.lesson.villager.be.1.wav | villager | 1.0 | Tú y yo somos amigos. |  |
| guide.lesson.villager.day.1 | speech/guide.lesson.villager.day.1.slow.wav | villager | 0.8 | Ayer Luz hizo pan. Domingo trabajó en la herrería. Mar pescó en el río. |  |
| guide.lesson.villager.day.1 | speech/guide.lesson.villager.day.1.wav | villager | 1.0 | Ayer Luz hizo pan. Domingo trabajó en la herrería. Mar pescó en el río. |  |
| guide.lesson.villager.eat.1 | speech/guide.lesson.villager.eat.1.slow.wav | villager | 0.8 | Ellos van a comer pan. |  |
| guide.lesson.villager.eat.1 | speech/guide.lesson.villager.eat.1.wav | villager | 1.0 | Ellos van a comer pan. |  |
| guide.lesson.villager.go.1 | speech/guide.lesson.villager.go.1.slow.wav | villager | 0.8 | Vamos a la tienda. |  |
| guide.lesson.villager.go.1 | speech/guide.lesson.villager.go.1.wav | villager | 1.0 | Vamos a la tienda. |  |
| guide.lesson.villager.goes.1 | speech/guide.lesson.villager.goes.1.slow.wav | villager | 0.8 | Hoy vas a la tienda. |  |
| guide.lesson.villager.goes.1 | speech/guide.lesson.villager.goes.1.wav | villager | 1.0 | Hoy vas a la tienda. |  |
| guide.lesson.villager.house.1 | speech/guide.lesson.villager.house.1.slow.wav | villager | 0.8 | Domingo vive en una casa cerca de la herrería. |  |
| guide.lesson.villager.house.1 | speech/guide.lesson.villager.house.1.wav | villager | 1.0 | Domingo vive en una casa cerca de la herrería. |  |
| guide.lesson.villager.luna.1 | speech/guide.lesson.villager.luna.1.slow.wav | villager | 0.8 | ¿Qué quiere Luna? |  |
| guide.lesson.villager.luna.1 | speech/guide.lesson.villager.luna.1.wav | villager | 1.0 | ¿Qué quiere Luna? |  |
| guide.lesson.villager.luz.1 | speech/guide.lesson.villager.luz.1.slow.wav | villager | 0.8 | ¿Qué hizo Luz ayer? |  |
| guide.lesson.villager.luz.1 | speech/guide.lesson.villager.luz.1.wav | villager | 1.0 | ¿Qué hizo Luz ayer? |  |
| guide.lesson.villager.made.1 | speech/guide.lesson.villager.made.1.slow.wav | villager | 0.8 | ¿Qué hizo Domingo? |  |
| guide.lesson.villager.made.1 | speech/guide.lesson.villager.made.1.wav | villager | 1.0 | ¿Qué hizo Domingo? |  |
| guide.lesson.villager.mar.1 | speech/guide.lesson.villager.mar.1.slow.wav | villager | 0.8 | ¿Y qué hizo Mar? |  |
| guide.lesson.villager.mar.1 | speech/guide.lesson.villager.mar.1.wav | villager | 1.0 | ¿Y qué hizo Mar? |  |
| guide.lesson.villager.morning.1 | speech/guide.lesson.villager.morning.1.slow.wav | villager | 0.8 | Por la mañana, Luz y yo fuimos a la tienda. |  |
| guide.lesson.villager.morning.1 | speech/guide.lesson.villager.morning.1.wav | villager | 1.0 | Por la mañana, Luz y yo fuimos a la tienda. |  |
| guide.lesson.villager.much.1 | speech/guide.lesson.villager.much.1.slow.wav | villager | 0.8 | Ayer Domingo trabajó mucho. |  |
| guide.lesson.villager.much.1 | speech/guide.lesson.villager.much.1.wav | villager | 1.0 | Ayer Domingo trabajó mucho. |  |
| guide.lesson.villager.need.1 | speech/guide.lesson.villager.need.1.slow.wav | villager | 0.8 | ¿Qué necesitas ahora? |  |
| guide.lesson.villager.need.1 | speech/guide.lesson.villager.need.1.wav | villager | 1.0 | ¿Qué necesitas ahora? |  |
| guide.lesson.villager.plan.1 | speech/guide.lesson.villager.plan.1.slow.wav | villager | 0.8 | ¿Qué vas a comer mañana? |  |
| guide.lesson.villager.plan.1 | speech/guide.lesson.villager.plan.1.wav | villager | 1.0 | ¿Qué vas a comer mañana? |  |
| guide.lesson.villager.plan_we.1 | speech/guide.lesson.villager.plan_we.1.slow.wav | villager | 0.8 | ¿Qué vamos a comer? |  |
| guide.lesson.villager.plan_we.1 | speech/guide.lesson.villager.plan_we.1.wav | villager | 1.0 | ¿Qué vamos a comer? |  |
| guide.lesson.villager.river.1 | speech/guide.lesson.villager.river.1.slow.wav | villager | 0.8 | ¡Hola! ¿Fuiste al río ayer? |  |
| guide.lesson.villager.river.1 | speech/guide.lesson.villager.river.1.wav | villager | 1.0 | ¡Hola! ¿Fuiste al río ayer? |  |
| guide.lesson.villager.she.1 | speech/guide.lesson.villager.she.1.slow.wav | villager | 0.8 | Luna es la guía. Ella es mi amiga. |  |
| guide.lesson.villager.she.1 | speech/guide.lesson.villager.she.1.wav | villager | 1.0 | Luna es la guía. Ella es mi amiga. |  |
| guide.lesson.villager.sign.1 | speech/guide.lesson.villager.sign.1.slow.wav | villager | 0.8 | El letrero dice: tienda, río, montaña. |  |
| guide.lesson.villager.sign.1 | speech/guide.lesson.villager.sign.1.wav | villager | 1.0 | El letrero dice: tienda, río, montaña. |  |
| guide.lesson.villager.smith.1 | speech/guide.lesson.villager.smith.1.slow.wav | villager | 0.8 | Mira, allí está Domingo. Es el herrero. |  |
| guide.lesson.villager.smith.1 | speech/guide.lesson.villager.smith.1.wav | villager | 1.0 | Mira, allí está Domingo. Es el herrero. |  |
| guide.lesson.villager.smith_waits.1 | speech/guide.lesson.villager.smith_waits.1.slow.wav | villager | 0.8 | Domingo te busca. Está en la herrería. |  |
| guide.lesson.villager.smith_waits.1 | speech/guide.lesson.villager.smith_waits.1.wav | villager | 1.0 | Domingo te busca. Está en la herrería. |  |
| guide.lesson.villager.they.1 | speech/guide.lesson.villager.they.1.slow.wav | villager | 0.8 | Ellos tienen pan. |  |
| guide.lesson.villager.they.1 | speech/guide.lesson.villager.they.1.wav | villager | 1.0 | Ellos tienen pan. |  |
| guide.lesson.villager.want.1 | speech/guide.lesson.villager.want.1.slow.wav | villager | 0.8 | ¿Qué quieres? |  |
| guide.lesson.villager.want.1 | speech/guide.lesson.villager.want.1.wav | villager | 1.0 | ¿Qué quieres? |  |
| guide.lesson.villager.we.1 | speech/guide.lesson.villager.we.1.slow.wav | villager | 0.8 | ¿Qué necesitamos? |  |
| guide.lesson.villager.we.1 | speech/guide.lesson.villager.we.1.wav | villager | 1.0 | ¿Qué necesitamos? |  |
| guide.lesson.villager.went_bakery.1 | speech/guide.lesson.villager.went_bakery.1.slow.wav | villager | 0.8 | ¡Hola! ¿Fuiste a la panadería? |  |
| guide.lesson.villager.went_bakery.1 | speech/guide.lesson.villager.went_bakery.1.wav | villager | 1.0 | ¡Hola! ¿Fuiste a la panadería? |  |
| guide.lesson.villager.where.1 | speech/guide.lesson.villager.where.1.slow.wav | villager | 0.8 | Ella está en el campamento. |  |
| guide.lesson.villager.where.1 | speech/guide.lesson.villager.where.1.wav | villager | 1.0 | Ella está en el campamento. |  |
| guide.lesson.villager.where_went.1 | speech/guide.lesson.villager.where_went.1.slow.wav | villager | 0.8 | ¿Y Mar y tú? ¿Adónde fueron ayer? |  |
| guide.lesson.villager.where_went.1 | speech/guide.lesson.villager.where_went.1.wav | villager | 1.0 | ¿Y Mar y tú? ¿Adónde fueron ayer? |  |
| guide.lesson.villager.who.1 | speech/guide.lesson.villager.who.1.slow.wav | villager | 0.8 | ¿Quién es Domingo? |  |
| guide.lesson.villager.who.1 | speech/guide.lesson.villager.who.1.wav | villager | 1.0 | ¿Quién es Domingo? |  |
| guide.lesson.villager.will_make.1 | speech/guide.lesson.villager.will_make.1.slow.wav | villager | 0.8 | ¿Qué va a hacer Domingo? |  |
| guide.lesson.villager.will_make.1 | speech/guide.lesson.villager.will_make.1.wav | villager | 1.0 | ¿Qué va a hacer Domingo? |  |
| guide.lesson.villager.works.1 | speech/guide.lesson.villager.works.1.slow.wav | villager | 0.8 | ¿Dónde trabaja Domingo? |  |
| guide.lesson.villager.works.1 | speech/guide.lesson.villager.works.1.wav | villager | 1.0 | ¿Dónde trabaja Domingo? |  |
| guide.lesson.want.1 | speech/guide.lesson.want.1.slow.wav | guide | 0.8 | ¿Qué quieres? |  |
| guide.lesson.want.1 | speech/guide.lesson.want.1.wav | guide | 1.0 | ¿Qué quieres? |  |
| guide.lesson.welcome.1 | speech/guide.lesson.welcome.1.slow.wav | guide | 0.8 | De nada. |  |
| guide.lesson.welcome.1 | speech/guide.lesson.welcome.1.wav | guide | 1.0 | De nada. |  |
| guide.name.none.1 | speech/guide.name.none.1.slow.wav | guide | 0.8 | Mira una cosa y dime qué es. |  |
| guide.name.none.1 | speech/guide.name.none.1.wav | guide | 1.0 | Mira una cosa y dime qué es. |  |
| guide.name.none.2 | speech/guide.name.none.2.slow.wav | guide | 0.8 | No sé qué es. Mira algo. |  |
| guide.name.none.2 | speech/guide.name.none.2.wav | guide | 1.0 | No sé qué es. Mira algo. |  |
| guide.review.ask.1 | speech/guide.review.ask.1.slow.wav | guide | 0.8 | Mira. ¿Qué es esto? |  |
| guide.review.ask.1 | speech/guide.review.ask.1.wav | guide | 1.0 | Mira. ¿Qué es esto? |  |
| guide.review.ask.2 | speech/guide.review.ask.2.slow.wav | guide | 0.8 | A ver, ¿qué es esto? |  |
| guide.review.ask.2 | speech/guide.review.ask.2.wav | guide | 1.0 | A ver, ¿qué es esto? |  |
| guide.review.ask.3 | speech/guide.review.ask.3.slow.wav | guide | 0.8 | ¿Qué es esto? Mira bien. |  |
| guide.review.ask.3 | speech/guide.review.ask.3.wav | guide | 1.0 | ¿Qué es esto? Mira bien. |  |
| guide.review.phrase.donde_esta_el_rio.1 | speech/guide.review.phrase.donde_esta_el_rio.1.slow.wav | guide | 0.8 | ¿Qué buscas? |  |
| guide.review.phrase.donde_esta_el_rio.1 | speech/guide.review.phrase.donde_esta_el_rio.1.wav | guide | 1.0 | ¿Qué buscas? |  |
| guide.review.phrase.no_gracias.1 | speech/guide.review.phrase.no_gracias.1.slow.wav | guide | 0.8 | ¿Quieres agua? |  |
| guide.review.phrase.no_gracias.1 | speech/guide.review.phrase.no_gracias.1.wav | guide | 1.0 | ¿Quieres agua? |  |
| guide.review.phrase.pedir_agua.1 | speech/guide.review.phrase.pedir_agua.1.slow.wav | guide | 0.8 | ¿Qué necesitas? |  |
| guide.review.phrase.pedir_agua.1 | speech/guide.review.phrase.pedir_agua.1.wav | guide | 1.0 | ¿Qué necesitas? |  |
| guide.review.phrase.si_por_favor.1 | speech/guide.review.phrase.si_por_favor.1.slow.wav | guide | 0.8 | ¿Quieres agua? |  |
| guide.review.phrase.si_por_favor.1 | speech/guide.review.phrase.si_por_favor.1.wav | guide | 1.0 | ¿Quieres agua? |  |
| guide.uncertain.1 | speech/guide.uncertain.1.slow.wav | guide | 0.8 | No te entendí bien. ¿Lo dices otra vez? |  |
| guide.uncertain.1 | speech/guide.uncertain.1.wav | guide | 1.0 | No te entendí bien. ¿Lo dices otra vez? |  |
| guide.uncertain.2 | speech/guide.uncertain.2.slow.wav | guide | 0.8 | No te entendí bien. ¿Me lo dices otra vez? |  |
| guide.uncertain.2 | speech/guide.uncertain.2.wav | guide | 1.0 | No te entendí bien. ¿Me lo dices otra vez? |  |
| guide_again.1 | speech/guide_again.1.slow.wav | guide | 0.8 | Claro. Otra vez. |  |
| guide_again.1 | speech/guide_again.1.wav | guide | 1.0 | Claro. Otra vez. |  |
| guide_again.2 | speech/guide_again.2.slow.wav | guide | 0.8 | ¡Claro! Te lo digo otra vez. |  |
| guide_again.2 | speech/guide_again.2.wav | guide | 1.0 | ¡Claro! Te lo digo otra vez. |  |
| guide_craft.1 | speech/guide_craft.1.slow.wav | guide | 0.8 | ¡Claro! Mira. |  |
| guide_craft.1 | speech/guide_craft.1.wav | guide | 1.0 | ¡Claro! Mira. |  |
| guide_craft.2 | speech/guide_craft.2.slow.wav | guide | 0.8 | A ver, te digo. |  |
| guide_craft.2 | speech/guide_craft.2.wav | guide | 1.0 | A ver, te digo. |  |
| guide_craft.3 | speech/guide_craft.3.slow.wav | guide | 0.8 | Sí, te ayudo. |  |
| guide_craft.3 | speech/guide_craft.3.wav | guide | 1.0 | Sí, te ayudo. |  |
| guide_find.1 | speech/guide_find.1.slow.wav | guide | 0.8 | A ver, busco. |  |
| guide_find.1 | speech/guide_find.1.wav | guide | 1.0 | A ver, busco. |  |
| guide_find.2 | speech/guide_find.2.slow.wav | guide | 0.8 | ¡Claro! Busco. |  |
| guide_find.2 | speech/guide_find.2.wav | guide | 1.0 | ¡Claro! Busco. |  |
| guide_find.3 | speech/guide_find.3.slow.wav | guide | 0.8 | Un minuto, busco. |  |
| guide_find.3 | speech/guide_find.3.wav | guide | 1.0 | Un minuto, busco. |  |
| guide_greet.1 | speech/guide_greet.1.slow.wav | guide | 0.8 | ¡Hola! Soy la guía. |  |
| guide_greet.1 | speech/guide_greet.1.wav | guide | 1.0 | ¡Hola! Soy la guía. |  |
| guide_greet.2 | speech/guide_greet.2.slow.wav | guide | 0.8 | ¡Hola, amigo! ¿Qué buscas? |  |
| guide_greet.2 | speech/guide_greet.2.wav | guide | 1.0 | ¡Hola, amigo! ¿Qué buscas? |  |
| guide_greet.3 | speech/guide_greet.3.slow.wav | guide | 0.8 | Buenos días. ¿En qué te ayudo? |  |
| guide_greet.3 | speech/guide_greet.3.wav | guide | 1.0 | Buenos días. ¿En qué te ayudo? |  |
| guide_greet_again.1 | speech/guide_greet_again.1.slow.wav | guide | 0.8 | Hola otra vez. ¿Qué buscas? |  |
| guide_greet_again.1 | speech/guide_greet_again.1.wav | guide | 1.0 | Hola otra vez. ¿Qué buscas? |  |
| guide_greet_again.2 | speech/guide_greet_again.2.slow.wav | guide | 0.8 | ¡Hola! ¿Qué necesitas? |  |
| guide_greet_again.2 | speech/guide_greet_again.2.wav | guide | 1.0 | ¡Hola! ¿Qué necesitas? |  |
| guide_help.1 | speech/guide_help.1.slow.wav | guide | 0.8 | ¡Claro, te ayudo! |  |
| guide_help.1 | speech/guide_help.1.wav | guide | 1.0 | ¡Claro, te ayudo! |  |
| guide_help.2 | speech/guide_help.2.slow.wav | guide | 0.8 | Sí, te ayudo. |  |
| guide_help.2 | speech/guide_help.2.wav | guide | 1.0 | Sí, te ayudo. |  |
| guide_help.3 | speech/guide_help.3.slow.wav | guide | 0.8 | Con gusto, te ayudo. |  |
| guide_help.3 | speech/guide_help.3.wav | guide | 1.0 | Con gusto, te ayudo. |  |
| guide_look.1 | speech/guide_look.1.slow.wav | guide | 0.8 | Miro bien. |  |
| guide_look.1 | speech/guide_look.1.wav | guide | 1.0 | Miro bien. |  |
| guide_look.2 | speech/guide_look.2.slow.wav | guide | 0.8 | A ver qué es. |  |
| guide_look.2 | speech/guide_look.2.wav | guide | 1.0 | A ver qué es. |  |
| guide_look.3 | speech/guide_look.3.slow.wav | guide | 0.8 | ¡Mira, mira! |  |
| guide_look.3 | speech/guide_look.3.wav | guide | 1.0 | ¡Mira, mira! |  |
| guide_name.1 | speech/guide_name.1.slow.wav | guide | 0.8 | A ver si es eso. |  |
| guide_name.1 | speech/guide_name.1.wav | guide | 1.0 | A ver si es eso. |  |
| guide_name.2 | speech/guide_name.2.slow.wav | guide | 0.8 | Mira bien. |  |
| guide_name.2 | speech/guide_name.2.wav | guide | 1.0 | Mira bien. |  |
| guide_name.3 | speech/guide_name.3.slow.wav | guide | 0.8 | Vamos a ver. |  |
| guide_name.3 | speech/guide_name.3.wav | guide | 1.0 | Vamos a ver. |  |
| guide_offer.1 | speech/guide_offer.1.slow.wav | guide | 0.8 | ¿Quieres agua? |  |
| guide_offer.1 | speech/guide_offer.1.wav | guide | 1.0 | ¿Quieres agua? |  |
| guide_offer_help.1 | speech/guide_offer_help.1.slow.wav | guide | 0.8 | Puedes decir: «Sí, por favor» o «No, gracias». |  |
| guide_offer_help.1 | speech/guide_offer_help.1.wav | guide | 1.0 | Puedes decir: «Sí, por favor» o «No, gracias». |  |
| guide_offer_no.1 | speech/guide_offer_no.1.slow.wav | guide | 0.8 | Está bien. |  |
| guide_offer_no.1 | speech/guide_offer_no.1.wav | guide | 1.0 | Está bien. |  |
| guide_offer_no.2 | speech/guide_offer_no.2.slow.wav | guide | 0.8 | Bueno. |  |
| guide_offer_no.2 | speech/guide_offer_no.2.wav | guide | 1.0 | Bueno. |  |
| guide_offer_yes.1 | speech/guide_offer_yes.1.slow.wav | guide | 0.8 | ¡Claro! Aquí tienes. |  |
| guide_offer_yes.1 | speech/guide_offer_yes.1.wav | guide | 1.0 | ¡Claro! Aquí tienes. |  |
| guide_offer_yes.2 | speech/guide_offer_yes.2.slow.wav | guide | 0.8 | Toma. |  |
| guide_offer_yes.2 | speech/guide_offer_yes.2.wav | guide | 1.0 | Toma. |  |
| guide_point.1 | speech/guide_point.1.slow.wav | guide | 0.8 | A ver, miro. |  |
| guide_point.1 | speech/guide_point.1.wav | guide | 1.0 | A ver, miro. |  |
| guide_point.2 | speech/guide_point.2.slow.wav | guide | 0.8 | Un minuto, miro. |  |
| guide_point.2 | speech/guide_point.2.wav | guide | 1.0 | Un minuto, miro. |  |
| guide_point.3 | speech/guide_point.3.slow.wav | guide | 0.8 | ¡Claro! Miro. |  |
| guide_point.3 | speech/guide_point.3.wav | guide | 1.0 | ¡Claro! Miro. |  |
| guide_slower.1 | speech/guide_slower.1.slow.wav | guide | 0.8 | Claro. Más despacio. |  |
| guide_slower.1 | speech/guide_slower.1.wav | guide | 1.0 | Claro. Más despacio. |  |
| guide_slower.2 | speech/guide_slower.2.slow.wav | guide | 0.8 | ¡Claro! Te lo digo más despacio. |  |
| guide_slower.2 | speech/guide_slower.2.wav | guide | 1.0 | ¡Claro! Te lo digo más despacio. |  |
| guide_thank.1 | speech/guide_thank.1.slow.wav | guide | 0.8 | ¡Con gusto, amigo! |  |
| guide_thank.1 | speech/guide_thank.1.wav | guide | 1.0 | ¡Con gusto, amigo! |  |
| guide_thank.2 | speech/guide_thank.2.slow.wav | guide | 0.8 | ¡De nada! |  |
| guide_thank.2 | speech/guide_thank.2.wav | guide | 1.0 | ¡De nada! |  |
| guide_thank.3 | speech/guide_thank.3.slow.wav | guide | 0.8 | ¡De nada, amigo! |  |
| guide_thank.3 | speech/guide_thank.3.wav | guide | 1.0 | ¡De nada, amigo! |  |
| not_understood.1 | speech/not_understood.1.slow.wav | guide | 0.8 | No te entendí. ¿Me lo dices otra vez? |  |
| not_understood.1 | speech/not_understood.1.wav | guide | 1.0 | No te entendí. ¿Me lo dices otra vez? |  |
| not_understood.2 | speech/not_understood.2.slow.wav | guide | 0.8 | Perdón, ¿qué dices? |  |
| not_understood.2 | speech/not_understood.2.wav | guide | 1.0 | Perdón, ¿qué dices? |  |
| phrase.adios | speech/phrase.adios.slow.wav | guide | 0.8 | Adiós. |  |
| phrase.adios | speech/phrase.adios.wav | guide | 1.0 | Adiós. |  |
| phrase.ayer-bebi-agua | speech/phrase.ayer-bebi-agua.slow.wav | guide | 0.8 | Ayer bebí agua. |  |
| phrase.ayer-bebi-agua | speech/phrase.ayer-bebi-agua.wav | guide | 1.0 | Ayer bebí agua. |  |
| phrase.ayer-comi-fruta | speech/phrase.ayer-comi-fruta.slow.wav | guide | 0.8 | Ayer comí fruta. |  |
| phrase.ayer-comi-fruta | speech/phrase.ayer-comi-fruta.wav | guide | 1.0 | Ayer comí fruta. |  |
| phrase.ayer-fui-al-rio | speech/phrase.ayer-fui-al-rio.slow.wav | guide | 0.8 | Ayer fui al río. |  |
| phrase.ayer-fui-al-rio | speech/phrase.ayer-fui-al-rio.wav | guide | 1.0 | Ayer fui al río. |  |
| phrase.ayuda | speech/phrase.ayuda.slow.wav | guide | 0.8 | Ayuda. |  |
| phrase.ayuda | speech/phrase.ayuda.wav | guide | 1.0 | Ayuda. |  |
| phrase.buscare-madera | speech/phrase.buscare-madera.slow.wav | guide | 0.8 | Buscaré madera para Luz. |  |
| phrase.buscare-madera | speech/phrase.buscare-madera.wav | guide | 1.0 | Buscaré madera para Luz. |  |
| phrase.como-te-llamas | speech/phrase.como-te-llamas.slow.wav | guide | 0.8 | ¿Cómo te llamas? |  |
| phrase.como-te-llamas | speech/phrase.como-te-llamas.wav | guide | 1.0 | ¿Cómo te llamas? |  |
| phrase.compre-dos-panes | speech/phrase.compre-dos-panes.slow.wav | guide | 0.8 | Compré dos panes. |  |
| phrase.compre-dos-panes | speech/phrase.compre-dos-panes.wav | guide | 1.0 | Compré dos panes. |  |
| phrase.compre-tres-manzanas | speech/phrase.compre-tres-manzanas.slow.wav | guide | 0.8 | Compré tres manzanas. |  |
| phrase.compre-tres-manzanas | speech/phrase.compre-tres-manzanas.wav | guide | 1.0 | Compré tres manzanas. |  |
| phrase.cuanto-cuesta | speech/phrase.cuanto-cuesta.slow.wav | guide | 0.8 | ¿Cuánto cuesta? |  |
| phrase.cuanto-cuesta | speech/phrase.cuanto-cuesta.wav | guide | 1.0 | ¿Cuánto cuesta? |  |
| phrase.cuanto-es-todo | speech/phrase.cuanto-es-todo.slow.wav | guide | 0.8 | ¿Cuánto es todo? |  |
| phrase.cuanto-es-todo | speech/phrase.cuanto-es-todo.wav | guide | 1.0 | ¿Cuánto es todo? |  |
| phrase.donde-esta-el-rio | speech/phrase.donde-esta-el-rio.slow.wav | guide | 0.8 | ¿Dónde está el río? |  |
| phrase.donde-esta-el-rio | speech/phrase.donde-esta-el-rio.wav | guide | 1.0 | ¿Dónde está el río? |  |
| phrase.donde-esta-la-herreria | speech/phrase.donde-esta-la-herreria.slow.wav | guide | 0.8 | ¿Dónde está la herrería? |  |
| phrase.donde-esta-la-herreria | speech/phrase.donde-esta-la-herreria.wav | guide | 1.0 | ¿Dónde está la herrería? |  |
| phrase.donde-esta-la-panaderia | speech/phrase.donde-esta-la-panaderia.slow.wav | guide | 0.8 | ¿Dónde está la panadería? |  |
| phrase.donde-esta-la-panaderia | speech/phrase.donde-esta-la-panaderia.wav | guide | 1.0 | ¿Dónde está la panadería? |  |
| phrase.ella-quiere-fruta | speech/phrase.ella-quiere-fruta.slow.wav | guide | 0.8 | Ella quiere fruta. |  |
| phrase.ella-quiere-fruta | speech/phrase.ella-quiere-fruta.wav | guide | 1.0 | Ella quiere fruta. |  |
| phrase.ellos-comieron-pan | speech/phrase.ellos-comieron-pan.slow.wav | guide | 0.8 | Ellos comieron pan. |  |
| phrase.ellos-comieron-pan | speech/phrase.ellos-comieron-pan.wav | guide | 1.0 | Ellos comieron pan. |  |
| phrase.es-el-herrero | speech/phrase.es-el-herrero.slow.wav | guide | 0.8 | Es el herrero. |  |
| phrase.es-el-herrero | speech/phrase.es-el-herrero.wav | guide | 1.0 | Es el herrero. |  |
| phrase.es-grande | speech/phrase.es-grande.slow.wav | guide | 0.8 | Es grande. |  |
| phrase.es-grande | speech/phrase.es-grande.wav | guide | 1.0 | Es grande. |  |
| phrase.es-muy-caro | speech/phrase.es-muy-caro.slow.wav | guide | 0.8 | Es muy caro. |  |
| phrase.es-muy-caro | speech/phrase.es-muy-caro.wav | guide | 1.0 | Es muy caro. |  |
| phrase.esta-lejos | speech/phrase.esta-lejos.slow.wav | guide | 0.8 | ¿Está lejos? |  |
| phrase.esta-lejos | speech/phrase.esta-lejos.wav | guide | 1.0 | ¿Está lejos? |  |
| phrase.fui-a-la-panaderia | speech/phrase.fui-a-la-panaderia.slow.wav | guide | 0.8 | Sí, fui a la panadería. |  |
| phrase.fui-a-la-panaderia | speech/phrase.fui-a-la-panaderia.wav | guide | 1.0 | Sí, fui a la panadería. |  |
| phrase.fui-al-rio | speech/phrase.fui-al-rio.slow.wav | guide | 0.8 | Sí, fui al río. |  |
| phrase.fui-al-rio | speech/phrase.fui-al-rio.wav | guide | 1.0 | Sí, fui al río. |  |
| phrase.fuimos-al-rio | speech/phrase.fuimos-al-rio.slow.wav | guide | 0.8 | Fuimos al río. |  |
| phrase.fuimos-al-rio | speech/phrase.fuimos-al-rio.wav | guide | 1.0 | Fuimos al río. |  |
| phrase.gracias | speech/phrase.gracias.slow.wav | guide | 0.8 | Gracias. |  |
| phrase.gracias | speech/phrase.gracias.wav | guide | 1.0 | Gracias. |  |
| phrase.hay-pan | speech/phrase.hay-pan.slow.wav | guide | 0.8 | ¿Hay pan? |  |
| phrase.hay-pan | speech/phrase.hay-pan.wav | guide | 1.0 | ¿Hay pan? |  |
| phrase.hay-un-pozo | speech/phrase.hay-un-pozo.slow.wav | guide | 0.8 | ¿Hay un pozo en el pueblo? |  |
| phrase.hay-un-pozo | speech/phrase.hay-un-pozo.wav | guide | 1.0 | ¿Hay un pozo en el pueblo? |  |
| phrase.hizo-un-hacha | speech/phrase.hizo-un-hacha.slow.wav | guide | 0.8 | Domingo hizo un hacha. |  |
| phrase.hizo-un-hacha | speech/phrase.hizo-un-hacha.wav | guide | 1.0 | Domingo hizo un hacha. |  |
| phrase.hola | speech/phrase.hola.slow.wav | guide | 0.8 | Hola. |  |
| phrase.hola | speech/phrase.hola.wav | guide | 1.0 | Hola. |  |
| phrase.ire-al-rio | speech/phrase.ire-al-rio.slow.wav | guide | 0.8 | Iré al río. |  |
| phrase.ire-al-rio | speech/phrase.ire-al-rio.wav | guide | 1.0 | Iré al río. |  |
| phrase.la-manzana-es-roja | speech/phrase.la-manzana-es-roja.slow.wav | guide | 0.8 | La manzana es roja. |  |
| phrase.la-manzana-es-roja | speech/phrase.la-manzana-es-roja.wav | guide | 1.0 | La manzana es roja. |  |
| phrase.luz-compro-pescado | speech/phrase.luz-compro-pescado.slow.wav | guide | 0.8 | Luz compró pescado. |  |
| phrase.luz-compro-pescado | speech/phrase.luz-compro-pescado.wav | guide | 1.0 | Luz compró pescado. |  |
| phrase.luz-hara-pan | speech/phrase.luz-hara-pan.slow.wav | guide | 0.8 | Luz hará pan. |  |
| phrase.luz-hara-pan | speech/phrase.luz-hara-pan.wav | guide | 1.0 | Luz hará pan. |  |
| phrase.luz-hizo-pan | speech/phrase.luz-hizo-pan.slow.wav | guide | 0.8 | Luz hizo pan. |  |
| phrase.luz-hizo-pan | speech/phrase.luz-hizo-pan.wav | guide | 1.0 | Luz hizo pan. |  |
| phrase.manana-comprare-pan | speech/phrase.manana-comprare-pan.slow.wav | guide | 0.8 | Mañana compraré pan. |  |
| phrase.manana-comprare-pan | speech/phrase.manana-comprare-pan.wav | guide | 1.0 | Mañana compraré pan. |  |
| phrase.manana-vendere-fruta | speech/phrase.manana-vendere-fruta.slow.wav | guide | 0.8 | Mañana venderé fruta. |  |
| phrase.manana-vendere-fruta | speech/phrase.manana-vendere-fruta.wav | guide | 1.0 | Mañana venderé fruta. |  |
| phrase.mar-pesco-en-el-rio | speech/phrase.mar-pesco-en-el-rio.slow.wav | guide | 0.8 | Mar pescó en el río. |  |
| phrase.mar-pesco-en-el-rio | speech/phrase.mar-pesco-en-el-rio.wav | guide | 1.0 | Mar pescó en el río. |  |
| phrase.mas-barato-que | speech/phrase.mas-barato-que.slow.wav | guide | 0.8 | La manzana es más barata que el pan. |  |
| phrase.mas-barato-que | speech/phrase.mas-barato-que.wav | guide | 1.0 | La manzana es más barata que el pan. |  |
| phrase.mas-despacio | speech/phrase.mas-despacio.slow.wav | guide | 0.8 | Más despacio, por favor. |  |
| phrase.mas-despacio | speech/phrase.mas-despacio.wav | guide | 1.0 | Más despacio, por favor. |  |
| phrase.me-gusta-el-pan | speech/phrase.me-gusta-el-pan.slow.wav | guide | 0.8 | Sí, me gusta el pan. |  |
| phrase.me-gusta-el-pan | speech/phrase.me-gusta-el-pan.wav | guide | 1.0 | Sí, me gusta el pan. |  |
| phrase.me-gustan-las-manzanas | speech/phrase.me-gustan-las-manzanas.slow.wav | guide | 0.8 | Sí, me gustan las manzanas. |  |
| phrase.me-gustan-las-manzanas | speech/phrase.me-gustan-las-manzanas.wav | guide | 1.0 | Sí, me gustan las manzanas. |  |
| phrase.me-gusto-el-pan | speech/phrase.me-gusto-el-pan.slow.wav | guide | 0.8 | Sí, me gustó el pan de Luz. |  |
| phrase.me-gusto-el-pan | speech/phrase.me-gusto-el-pan.wav | guide | 1.0 | Sí, me gustó el pan de Luz. |  |
| phrase.necesitamos-agua | speech/phrase.necesitamos-agua.slow.wav | guide | 0.8 | Necesitamos agua. |  |
| phrase.necesitamos-agua | speech/phrase.necesitamos-agua.wav | guide | 1.0 | Necesitamos agua. |  |
| phrase.necesito-agua | speech/phrase.necesito-agua.slow.wav | guide | 0.8 | Necesito agua. |  |
| phrase.necesito-agua | speech/phrase.necesito-agua.wav | guide | 1.0 | Necesito agua. |  |
| phrase.necesito-pan | speech/phrase.necesito-pan.slow.wav | guide | 0.8 | Necesito pan. |  |
| phrase.necesito-pan | speech/phrase.necesito-pan.wav | guide | 1.0 | Necesito pan. |  |
| phrase.no | speech/phrase.no.slow.wav | guide | 0.8 | No. |  |
| phrase.no | speech/phrase.no.wav | guide | 1.0 | No. |  |
| phrase.no-gracias | speech/phrase.no-gracias.slow.wav | guide | 0.8 | No, gracias. |  |
| phrase.no-gracias | speech/phrase.no-gracias.wav | guide | 1.0 | No, gracias. |  |
| phrase.no-se | speech/phrase.no-se.slow.wav | guide | 0.8 | No sé. |  |
| phrase.no-se | speech/phrase.no-se.wav | guide | 1.0 | No sé. |  |
| phrase.otra-vez | speech/phrase.otra-vez.slow.wav | guide | 0.8 | Otra vez, por favor. |  |
| phrase.otra-vez | speech/phrase.otra-vez.wav | guide | 1.0 | Otra vez, por favor. |  |
| phrase.pedir-agua | speech/phrase.pedir-agua.slow.wav | guide | 0.8 | Quiero agua, por favor. |  |
| phrase.pedir-agua | speech/phrase.pedir-agua.wav | guide | 1.0 | Quiero agua, por favor. |  |
| phrase.puedo-pagar-con-fruta | speech/phrase.puedo-pagar-con-fruta.slow.wav | guide | 0.8 | ¿Puedo pagar con fruta? |  |
| phrase.puedo-pagar-con-fruta | speech/phrase.puedo-pagar-con-fruta.wav | guide | 1.0 | ¿Puedo pagar con fruta? |  |
| phrase.que-es-esto | speech/phrase.que-es-esto.slow.wav | guide | 0.8 | ¿Qué es esto? |  |
| phrase.que-es-esto | speech/phrase.que-es-esto.wav | guide | 1.0 | ¿Qué es esto? |  |
| phrase.que-haces | speech/phrase.que-haces.slow.wav | guide | 0.8 | ¿Qué haces? |  |
| phrase.que-haces | speech/phrase.que-haces.wav | guide | 1.0 | ¿Qué haces? |  |
| phrase.queremos-pan | speech/phrase.queremos-pan.slow.wav | guide | 0.8 | Queremos pan. |  |
| phrase.queremos-pan | speech/phrase.queremos-pan.wav | guide | 1.0 | Queremos pan. |  |
| phrase.quiero-dos-panes | speech/phrase.quiero-dos-panes.slow.wav | guide | 0.8 | Quiero dos panes, por favor. |  |
| phrase.quiero-dos-panes | speech/phrase.quiero-dos-panes.wav | guide | 1.0 | Quiero dos panes, por favor. |  |
| phrase.quiero-madera | speech/phrase.quiero-madera.slow.wav | guide | 0.8 | Quiero madera. |  |
| phrase.quiero-madera | speech/phrase.quiero-madera.wav | guide | 1.0 | Quiero madera. |  |
| phrase.quiero-tres-manzanas-rojas | speech/phrase.quiero-tres-manzanas-rojas.slow.wav | guide | 0.8 | Quiero tres manzanas rojas. |  |
| phrase.quiero-tres-manzanas-rojas | speech/phrase.quiero-tres-manzanas-rojas.wav | guide | 1.0 | Quiero tres manzanas rojas. |  |
| phrase.quiero-vender-madera | speech/phrase.quiero-vender-madera.slow.wav | guide | 0.8 | Quiero vender madera. |  |
| phrase.quiero-vender-madera | speech/phrase.quiero-vender-madera.wav | guide | 1.0 | Quiero vender madera. |  |
| phrase.si | speech/phrase.si.slow.wav | guide | 0.8 | Sí. |  |
| phrase.si | speech/phrase.si.wav | guide | 1.0 | Sí. |  |
| phrase.si-por-favor | speech/phrase.si-por-favor.slow.wav | guide | 0.8 | Sí, por favor. |  |
| phrase.si-por-favor | speech/phrase.si-por-favor.wav | guide | 1.0 | Sí, por favor. |  |
| phrase.tengo-botella | speech/phrase.tengo-botella.slow.wav | guide | 0.8 | Tengo una botella. |  |
| phrase.tengo-botella | speech/phrase.tengo-botella.wav | guide | 1.0 | Tengo una botella. |  |
| phrase.tengo-cuatro-monedas | speech/phrase.tengo-cuatro-monedas.slow.wav | guide | 0.8 | Tengo cuatro monedas. |  |
| phrase.tengo-cuatro-monedas | speech/phrase.tengo-cuatro-monedas.wav | guide | 1.0 | Tengo cuatro monedas. |  |
| phrase.tengo-fruta | speech/phrase.tengo-fruta.slow.wav | guide | 0.8 | Tengo una fruta. |  |
| phrase.tengo-fruta | speech/phrase.tengo-fruta.wav | guide | 1.0 | Tengo una fruta. |  |
| phrase.tengo-que-ir | speech/phrase.tengo-que-ir.slow.wav | guide | 0.8 | Tengo que ir al río. |  |
| phrase.tengo-que-ir | speech/phrase.tengo-que-ir.wav | guide | 1.0 | Tengo que ir al río. |  |
| phrase.tienes-veinte-panes | speech/phrase.tienes-veinte-panes.slow.wav | guide | 0.8 | Tienes veinte panes. |  |
| phrase.tienes-veinte-panes | speech/phrase.tienes-veinte-panes.wav | guide | 1.0 | Tienes veinte panes. |  |
| phrase.trabaja-en-la-herreria | speech/phrase.trabaja-en-la-herreria.slow.wav | guide | 0.8 | Domingo trabaja en la herrería. |  |
| phrase.trabaja-en-la-herreria | speech/phrase.trabaja-en-la-herreria.wav | guide | 1.0 | Domingo trabaja en la herrería. |  |
| phrase.va-a-hacer-un-hacha | speech/phrase.va-a-hacer-un-hacha.slow.wav | guide | 0.8 | Va a hacer un hacha. |  |
| phrase.va-a-hacer-un-hacha | speech/phrase.va-a-hacer-un-hacha.wav | guide | 1.0 | Va a hacer un hacha. |  |
| phrase.vamos-a-comer-fruta | speech/phrase.vamos-a-comer-fruta.slow.wav | guide | 0.8 | Vamos a comer fruta. |  |
| phrase.vamos-a-comer-fruta | speech/phrase.vamos-a-comer-fruta.wav | guide | 1.0 | Vamos a comer fruta. |  |
| phrase.vendo-madera | speech/phrase.vendo-madera.slow.wav | guide | 0.8 | Vendo madera. |  |
| phrase.vendo-madera | speech/phrase.vendo-madera.wav | guide | 1.0 | Vendo madera. |  |
| phrase.vivo-en-el-campamento | speech/phrase.vivo-en-el-campamento.slow.wav | guide | 0.8 | Vivo en el campamento. |  |
| phrase.vivo-en-el-campamento | speech/phrase.vivo-en-el-campamento.wav | guide | 1.0 | Vivo en el campamento. |  |
| phrase.voy-a-buscar-agua | speech/phrase.voy-a-buscar-agua.slow.wav | guide | 0.8 | Voy a buscar agua. |  |
| phrase.voy-a-buscar-agua | speech/phrase.voy-a-buscar-agua.wav | guide | 1.0 | Voy a buscar agua. |  |
| phrase.voy-a-buscar-madera | speech/phrase.voy-a-buscar-madera.slow.wav | guide | 0.8 | Voy a buscar madera. |  |
| phrase.voy-a-buscar-madera | speech/phrase.voy-a-buscar-madera.wav | guide | 1.0 | Voy a buscar madera. |  |
| phrase.voy-a-comer-pan | speech/phrase.voy-a-comer-pan.slow.wav | guide | 0.8 | Voy a comer pan. |  |
| phrase.voy-a-comer-pan | speech/phrase.voy-a-comer-pan.wav | guide | 1.0 | Voy a comer pan. |  |
| phrase.voy-a-comprar-pan | speech/phrase.voy-a-comprar-pan.slow.wav | guide | 0.8 | Voy a comprar pan. |  |
| phrase.voy-a-comprar-pan | speech/phrase.voy-a-comprar-pan.wav | guide | 1.0 | Voy a comprar pan. |  |
| phrase.voy-a-ir-al-rio | speech/phrase.voy-a-ir-al-rio.slow.wav | guide | 0.8 | Mañana voy a ir al río. |  |
| phrase.voy-a-ir-al-rio | speech/phrase.voy-a-ir-al-rio.wav | guide | 1.0 | Mañana voy a ir al río. |  |
| phrase.voy-a-la-panaderia | speech/phrase.voy-a-la-panaderia.slow.wav | guide | 0.8 | Voy a la panadería. |  |
| phrase.voy-a-la-panaderia | speech/phrase.voy-a-la-panaderia.wav | guide | 1.0 | Voy a la panadería. |  |
| prompt.baker-open | speech/prompt.baker-open.slow.wav | baker | 0.8 | ¿Qué quieres? |  |
| prompt.baker-open | speech/prompt.baker-open.wav | baker | 1.0 | ¿Qué quieres? |  |
| prompt.farmer-open | speech/prompt.farmer-open.slow.wav | farmer | 0.8 | ¡Hola! Este es mi campo. |  |
| prompt.farmer-open | speech/prompt.farmer-open.wav | farmer | 1.0 | ¡Hola! Este es mi campo. |  |
| prompt.fisher-open | speech/prompt.fisher-open.slow.wav | fisher | 0.72 | ¿Qué buscas? |  |
| prompt.fisher-open | speech/prompt.fisher-open.wav | fisher | 0.9 | ¿Qué buscas? |  |
| prompt.guide-offer | speech/prompt.guide-offer.slow.wav | guide | 0.8 | ¿Quieres agua? |  |
| prompt.guide-offer | speech/prompt.guide-offer.wav | guide | 1.0 | ¿Quieres agua? |  |
| prompt.guide-open | speech/prompt.guide-open.slow.wav | guide | 0.8 | ¡Hola! Soy la guía. ¿En qué te ayudo? |  |
| prompt.guide-open | speech/prompt.guide-open.wav | guide | 1.0 | ¡Hola! Soy la guía. ¿En qué te ayudo? |  |
| prompt.guide-review | speech/prompt.guide-review.slow.wav | guide | 0.8 | ¿Qué es esto? |  |
| prompt.guide-review | speech/prompt.guide-review.wav | guide | 1.0 | ¿Qué es esto? |  |
| prompt.lesson-apple | speech/prompt.lesson-apple.slow.wav | farmer | 0.8 | ¿Cómo es la manzana? |  |
| prompt.lesson-apple | speech/prompt.lesson-apple.wav | farmer | 1.0 | ¿Cómo es la manzana? |  |
| prompt.lesson-ate | speech/prompt.lesson-ate.slow.wav | shopkeeper | 0.8 | ¿Qué comiste ayer? |  |
| prompt.lesson-ate | speech/prompt.lesson-ate.wav | shopkeeper | 1.0 | ¿Qué comiste ayer? |  |
| prompt.lesson-big | speech/prompt.lesson-big.slow.wav | shopkeeper | 0.8 | Mira este pan. ¿Cómo es? |  |
| prompt.lesson-big | speech/prompt.lesson-big.wav | shopkeeper | 1.0 | Mira este pan. ¿Cómo es? |  |
| prompt.lesson-bottle | speech/prompt.lesson-bottle.slow.wav | shopkeeper | 0.8 | ¿Tiene usted una botella? |  |
| prompt.lesson-bottle | speech/prompt.lesson-bottle.wav | shopkeeper | 1.0 | ¿Tiene usted una botella? |  |
| prompt.lesson-bought | speech/prompt.lesson-bought.slow.wav | baker | 0.8 | ¿Qué compraste? |  |
| prompt.lesson-bought | speech/prompt.lesson-bought.wav | baker | 1.0 | ¿Qué compraste? |  |
| prompt.lesson-cheaper | speech/prompt.lesson-cheaper.slow.wav | farmer | 0.8 | Una manzana cuesta una moneda, y un pan, dos. ¿Qué es más barato? |  |
| prompt.lesson-cheaper | speech/prompt.lesson-cheaper.wav | farmer | 1.0 | Una manzana cuesta una moneda, y un pan, dos. ¿Qué es más barato? |  |
| prompt.lesson-coins | speech/prompt.lesson-coins.slow.wav | baker | 0.8 | ¿Cuántas monedas tienes? |  |
| prompt.lesson-coins | speech/prompt.lesson-coins.wav | baker | 1.0 | ¿Cuántas monedas tienes? |  |
| prompt.lesson-dear | speech/prompt.lesson-dear.slow.wav | farmer | 0.8 | ¿Es caro o es barato? |  |
| prompt.lesson-dear | speech/prompt.lesson-dear.wav | farmer | 1.0 | ¿Es caro o es barato? |  |
| prompt.lesson-far | speech/prompt.lesson-far.slow.wav | fisher | 0.72 | Y la herrería está a la derecha. |  |
| prompt.lesson-far | speech/prompt.lesson-far.wav | fisher | 0.9 | Y la herrería está a la derecha. |  |
| prompt.lesson-going | speech/prompt.lesson-going.slow.wav | fisher | 0.72 | ¿Adónde vas? |  |
| prompt.lesson-going | speech/prompt.lesson-going.wav | fisher | 0.9 | ¿Adónde vas? |  |
| prompt.lesson-had | speech/prompt.lesson-had.slow.wav | shopkeeper | 0.8 | ¿Bebiste agua ayer? |  |
| prompt.lesson-had | speech/prompt.lesson-had.wav | shopkeeper | 1.0 | ¿Bebiste agua ayer? |  |
| prompt.lesson-have | speech/prompt.lesson-have.slow.wav | guide | 0.8 | ¿Tienes una fruta? |  |
| prompt.lesson-have | speech/prompt.lesson-have.wav | guide | 1.0 | ¿Tienes una fruta? |  |
| prompt.lesson-hay | speech/prompt.lesson-hay.slow.wav | fisher | 0.72 | En el pueblo hay una tienda, una panadería y tres casas. |  |
| prompt.lesson-hay | speech/prompt.lesson-hay.wav | fisher | 0.9 | En el pueblo hay una tienda, una panadería y tres casas. |  |
| prompt.lesson-hello | speech/prompt.lesson-hello.slow.wav | guide | 0.8 | ¡Hola! |  |
| prompt.lesson-hello | speech/prompt.lesson-hello.wav | guide | 1.0 | ¡Hola! |  |
| prompt.lesson-like-apples | speech/prompt.lesson-like-apples.slow.wav | farmer | 0.8 | ¿Y te gustan las manzanas? |  |
| prompt.lesson-like-apples | speech/prompt.lesson-like-apples.wav | farmer | 1.0 | ¿Y te gustan las manzanas? |  |
| prompt.lesson-like-bread | speech/prompt.lesson-like-bread.slow.wav | farmer | 0.8 | ¿Te gusta el pan? |  |
| prompt.lesson-like-bread | speech/prompt.lesson-like-bread.wav | farmer | 1.0 | ¿Te gusta el pan? |  |
| prompt.lesson-liked | speech/prompt.lesson-liked.slow.wav | farmer | 0.8 | ¿Te gustó el pan de Luz? |  |
| prompt.lesson-liked | speech/prompt.lesson-liked.wav | farmer | 1.0 | ¿Te gustó el pan de Luz? |  |
| prompt.lesson-lives | speech/prompt.lesson-lives.slow.wav | smith | 0.8 | ¿Dónde vives? |  |
| prompt.lesson-lives | speech/prompt.lesson-lives.wav | smith | 1.0 | ¿Dónde vives? |  |
| prompt.lesson-loaves | speech/prompt.lesson-loaves.slow.wav | baker | 0.8 | ¡Hoy hay mercado! ¿Cuántos panes tengo? |  |
| prompt.lesson-loaves | speech/prompt.lesson-loaves.wav | baker | 1.0 | ¡Hoy hay mercado! ¿Cuántos panes tengo? |  |
| prompt.lesson-luz-bought | speech/prompt.lesson-luz-bought.slow.wav | fisher | 0.72 | ¿Qué compró Luz ayer? |  |
| prompt.lesson-luz-bought | speech/prompt.lesson-luz-bought.wav | fisher | 0.9 | ¿Qué compró Luz ayer? |  |
| prompt.lesson-luz-made | speech/prompt.lesson-luz-made.slow.wav | villager | 0.8 | ¿Qué hizo Luz ayer? |  |
| prompt.lesson-luz-made | speech/prompt.lesson-luz-made.wav | villager | 1.0 | ¿Qué hizo Luz ayer? |  |
| prompt.lesson-luz-will | speech/prompt.lesson-luz-will.slow.wav | fisher | 0.72 | ¿Qué hará Luz mañana? |  |
| prompt.lesson-luz-will | speech/prompt.lesson-luz-will.wav | fisher | 0.9 | ¿Qué hará Luz mañana? |  |
| prompt.lesson-made | speech/prompt.lesson-made.slow.wav | villager | 0.8 | ¿Qué hizo Domingo? |  |
| prompt.lesson-made | speech/prompt.lesson-made.wav | villager | 1.0 | ¿Qué hizo Domingo? |  |
| prompt.lesson-mar-fished | speech/prompt.lesson-mar-fished.slow.wav | villager | 0.8 | ¿Y qué hizo Mar? |  |
| prompt.lesson-mar-fished | speech/prompt.lesson-mar-fished.wav | villager | 1.0 | ¿Y qué hizo Mar? |  |
| prompt.lesson-market | speech/prompt.lesson-market.slow.wav | shopkeeper | 0.8 | ¡Hola! ¿Qué compraste en el mercado? |  |
| prompt.lesson-market | speech/prompt.lesson-market.wav | shopkeeper | 1.0 | ¡Hola! ¿Qué compraste en el mercado? |  |
| prompt.lesson-must-go | speech/prompt.lesson-must-go.slow.wav | smith | 0.8 | Mar fue al río y te busca. ¿Adónde tienes que ir? |  |
| prompt.lesson-must-go | speech/prompt.lesson-must-go.wav | smith | 1.0 | Mar fue al río y te busca. ¿Adónde tienes que ir? |  |
| prompt.lesson-name | speech/prompt.lesson-name.slow.wav | guide | 0.8 | Me llamo Luna. |  |
| prompt.lesson-name | speech/prompt.lesson-name.wav | guide | 1.0 | Me llamo Luna. |  |
| prompt.lesson-need | speech/prompt.lesson-need.slow.wav | guide | 0.8 | ¿Qué necesitas? |  |
| prompt.lesson-need | speech/prompt.lesson-need.wav | guide | 1.0 | ¿Qué necesitas? |  |
| prompt.lesson-offer | speech/prompt.lesson-offer.slow.wav | guide | 0.8 | ¿Quieres fruta? |  |
| prompt.lesson-offer | speech/prompt.lesson-offer.wav | guide | 1.0 | ¿Quieres fruta? |  |
| prompt.lesson-place | speech/prompt.lesson-place.slow.wav | fisher | 0.72 | ¿Algo más? |  |
| prompt.lesson-place | speech/prompt.lesson-place.wav | fisher | 0.9 | ¿Algo más? |  |
| prompt.lesson-plan | speech/prompt.lesson-plan.slow.wav | villager | 0.8 | ¿Qué vas a comer mañana? |  |
| prompt.lesson-plan | speech/prompt.lesson-plan.wav | villager | 1.0 | ¿Qué vas a comer mañana? |  |
| prompt.lesson-plan-there | speech/prompt.lesson-plan-there.slow.wav | fisher | 0.72 | ¿Qué vas a hacer allí? |  |
| prompt.lesson-plan-there | speech/prompt.lesson-plan-there.wav | fisher | 0.9 | ¿Qué vas a hacer allí? |  |
| prompt.lesson-plan-we | speech/prompt.lesson-plan-we.slow.wav | villager | 0.8 | ¿Qué vamos a comer? |  |
| prompt.lesson-plan-we | speech/prompt.lesson-plan-we.wav | villager | 1.0 | ¿Qué vamos a comer? |  |
| prompt.lesson-sell | speech/prompt.lesson-sell.slow.wav | baker | 0.8 | Necesito madera para el horno. ¿Qué quieres hacer? |  |
| prompt.lesson-sell | speech/prompt.lesson-sell.wav | baker | 1.0 | Necesito madera para el horno. ¿Qué quieres hacer? |  |
| prompt.lesson-she | speech/prompt.lesson-she.slow.wav | villager | 0.8 | ¿Qué quiere Luna? |  |
| prompt.lesson-she | speech/prompt.lesson-she.wav | villager | 1.0 | ¿Qué quiere Luna? |  |
| prompt.lesson-sign | speech/prompt.lesson-sign.slow.wav | villager | 0.8 | El letrero dice: tienda, río, montaña. |  |
| prompt.lesson-sign | speech/prompt.lesson-sign.wav | villager | 1.0 | El letrero dice: tienda, río, montaña. |  |
| prompt.lesson-task | speech/prompt.lesson-task.slow.wav | guide | 0.8 | Necesitas madera y frutas. |  |
| prompt.lesson-task | speech/prompt.lesson-task.wav | guide | 1.0 | Necesitas madera y frutas. |  |
| prompt.lesson-thanks | speech/prompt.lesson-thanks.slow.wav | guide | 0.8 | ¡Muy bien! Tienes una fruta. |  |
| prompt.lesson-thanks | speech/prompt.lesson-thanks.wav | guide | 1.0 | ¡Muy bien! Tienes una fruta. |  |
| prompt.lesson-they-ate | speech/prompt.lesson-they-ate.slow.wav | smith | 0.8 | ¡Hola! Ayer trabajé todo el día. ¿Qué comieron Luz y Mar? |  |
| prompt.lesson-they-ate | speech/prompt.lesson-they-ate.wav | smith | 1.0 | ¡Hola! Ayer trabajé todo el día. ¿Qué comieron Luz y Mar? |  |
| prompt.lesson-tomorrow | speech/prompt.lesson-tomorrow.slow.wav | shopkeeper | 0.8 | ¿Qué vas a hacer mañana? |  |
| prompt.lesson-tomorrow | speech/prompt.lesson-tomorrow.wav | shopkeeper | 1.0 | ¿Qué vas a hacer mañana? |  |
| prompt.lesson-unstuck | speech/prompt.lesson-unstuck.slow.wav | guide | 0.8 | Mira. ¿Qué es esto? |  |
| prompt.lesson-unstuck | speech/prompt.lesson-unstuck.wav | guide | 1.0 | Mira. ¿Qué es esto? |  |
| prompt.lesson-usted | speech/prompt.lesson-usted.slow.wav | shopkeeper | 0.8 | ¿Qué necesita usted? |  |
| prompt.lesson-usted | speech/prompt.lesson-usted.wav | shopkeeper | 1.0 | ¿Qué necesita usted? |  |
| prompt.lesson-ustedes | speech/prompt.lesson-ustedes.slow.wav | shopkeeper | 0.8 | ¿Qué quieren ustedes? |  |
| prompt.lesson-ustedes | speech/prompt.lesson-ustedes.wav | shopkeeper | 1.0 | ¿Qué quieren ustedes? |  |
| prompt.lesson-want | speech/prompt.lesson-want.slow.wav | guide | 0.8 | ¿Qué quieres? |  |
| prompt.lesson-want | speech/prompt.lesson-want.wav | guide | 1.0 | ¿Qué quieres? |  |
| prompt.lesson-water-plan | speech/prompt.lesson-water-plan.slow.wav | farmer | 0.8 | Mis plantas necesitan agua. Si me traes agua del río, te doy tres manzanas. ¿Qué vas a hacer? |  |
| prompt.lesson-water-plan | speech/prompt.lesson-water-plan.wav | farmer | 1.0 | Mis plantas necesitan agua. Si me traes agua del río, te doy tres manzanas. ¿Qué vas a hacer? |  |
| prompt.lesson-water-went | speech/prompt.lesson-water-went.slow.wav | farmer | 0.8 | ¿Fuiste al río? |  |
| prompt.lesson-water-went | speech/prompt.lesson-water-went.wav | farmer | 1.0 | ¿Fuiste al río? |  |
| prompt.lesson-we | speech/prompt.lesson-we.slow.wav | villager | 0.8 | ¿Qué necesitamos? |  |
| prompt.lesson-we | speech/prompt.lesson-we.wav | villager | 1.0 | ¿Qué necesitamos? |  |
| prompt.lesson-we-went | speech/prompt.lesson-we-went.slow.wav | villager | 0.8 | ¿Y Mar y tú? ¿Adónde fueron ayer? |  |
| prompt.lesson-we-went | speech/prompt.lesson-we-went.wav | villager | 1.0 | ¿Y Mar y tú? ¿Adónde fueron ayer? |  |
| prompt.lesson-went | speech/prompt.lesson-went.slow.wav | shopkeeper | 0.8 | ¿Fuiste al río ayer? |  |
| prompt.lesson-went | speech/prompt.lesson-went.wav | shopkeeper | 1.0 | ¿Fuiste al río ayer? |  |
| prompt.lesson-went-there | speech/prompt.lesson-went-there.slow.wav | fisher | 0.72 | ¿Fuiste a la panadería? |  |
| prompt.lesson-went-there | speech/prompt.lesson-went-there.wav | fisher | 0.9 | ¿Fuiste a la panadería? |  |
| prompt.lesson-what | speech/prompt.lesson-what.slow.wav | guide | 0.8 | Mira. |  |
| prompt.lesson-what | speech/prompt.lesson-what.wav | guide | 1.0 | Mira. |  |
| prompt.lesson-who | speech/prompt.lesson-who.slow.wav | villager | 0.8 | ¿Quién es Domingo? |  |
| prompt.lesson-who | speech/prompt.lesson-who.wav | villager | 1.0 | ¿Quién es Domingo? |  |
| prompt.lesson-will-buy | speech/prompt.lesson-will-buy.slow.wav | baker | 0.8 | ¿Qué harás mañana? |  |
| prompt.lesson-will-buy | speech/prompt.lesson-will-buy.wav | baker | 1.0 | ¿Qué harás mañana? |  |
| prompt.lesson-will-fetch | speech/prompt.lesson-will-fetch.slow.wav | smith | 0.8 | ¡Hola! Luz necesita madera para el horno. ¿Qué harás? |  |
| prompt.lesson-will-fetch | speech/prompt.lesson-will-fetch.wav | smith | 1.0 | ¡Hola! Luz necesita madera para el horno. ¿Qué harás? |  |
| prompt.lesson-will-go | speech/prompt.lesson-will-go.slow.wav | smith | 0.8 | ¡Muy bien! Mar pescará en el río toda la tarde. ¿Adónde irás ahora? |  |
| prompt.lesson-will-go | speech/prompt.lesson-will-go.wav | smith | 1.0 | ¡Muy bien! Mar pescará en el río toda la tarde. ¿Adónde irás ahora? |  |
| prompt.lesson-will-make | speech/prompt.lesson-will-make.slow.wav | villager | 0.8 | ¿Qué va a hacer Domingo? |  |
| prompt.lesson-will-make | speech/prompt.lesson-will-make.wav | villager | 1.0 | ¿Qué va a hacer Domingo? |  |
| prompt.lesson-will-sell | speech/prompt.lesson-will-sell.slow.wav | farmer | 0.8 | ¿Y qué venderás mañana? |  |
| prompt.lesson-will-sell | speech/prompt.lesson-will-sell.wav | farmer | 1.0 | ¿Y qué venderás mañana? |  |
| prompt.lesson-wood-plan | speech/prompt.lesson-wood-plan.slow.wav | baker | 0.8 | Necesito cinco bloques de madera para el horno. Te pago cuatro monedas. ¿Qué vas a hacer? |  |
| prompt.lesson-wood-plan | speech/prompt.lesson-wood-plan.wav | baker | 1.0 | Necesito cinco bloques de madera para el horno. Te pago cuatro monedas. ¿Qué vas a hacer? |  |
| prompt.lesson-works | speech/prompt.lesson-works.slow.wav | villager | 0.8 | ¿Dónde trabaja Domingo? |  |
| prompt.lesson-works | speech/prompt.lesson-works.wav | villager | 1.0 | ¿Dónde trabaja Domingo? |  |
| prompt.lesson-yesno | speech/prompt.lesson-yesno.slow.wav | guide | 0.8 | ¿Es madera? |  |
| prompt.lesson-yesno | speech/prompt.lesson-yesno.wav | guide | 1.0 | ¿Es madera? |  |
| prompt.smith-open | speech/prompt.smith-open.slow.wav | smith | 0.8 | ¡Hola! Esta es mi herrería. |  |
| prompt.smith-open | speech/prompt.smith-open.wav | smith | 1.0 | ¡Hola! Esta es mi herrería. |  |
| prompt.stall-need | speech/prompt.stall-need.slow.wav | shopkeeper | 0.8 | ¿Qué necesitas? |  |
| prompt.stall-need | speech/prompt.stall-need.wav | shopkeeper | 1.0 | ¿Qué necesitas? |  |
| prompt.stall-offer | speech/prompt.stall-offer.slow.wav | shopkeeper | 0.8 | ¿Quieres pan? |  |
| prompt.stall-offer | speech/prompt.stall-offer.wav | shopkeeper | 1.0 | ¿Quieres pan? |  |
| prompt.villager-open | speech/prompt.villager-open.slow.wav | villager | 0.8 | Hola. |  |
| prompt.villager-open | speech/prompt.villager-open.wav | villager | 1.0 | Hola. |  |
| shop_bye.1 | speech/shop_bye.1.slow.wav | shopkeeper | 0.8 | ¡Adiós! Hasta luego. |  |
| shop_bye.1 | speech/shop_bye.1.wav | shopkeeper | 1.0 | ¡Adiós! Hasta luego. |  |
| shop_bye.2 | speech/shop_bye.2.slow.wav | shopkeeper | 0.8 | Hasta luego. |  |
| shop_bye.2 | speech/shop_bye.2.wav | shopkeeper | 1.0 | Hasta luego. |  |
| shop_deny.1 | speech/shop_deny.1.slow.wav | shopkeeper | 0.8 | Está bien. |  |
| shop_deny.1 | speech/shop_deny.1.wav | shopkeeper | 1.0 | Está bien. |  |
| shop_deny.2 | speech/shop_deny.2.slow.wav | shopkeeper | 0.8 | Bueno. |  |
| shop_deny.2 | speech/shop_deny.2.wav | shopkeeper | 1.0 | Bueno. |  |
| shop_done.1 | speech/shop_done.1.slow.wav | shopkeeper | 0.8 | ¡Hasta luego! |  |
| shop_done.1 | speech/shop_done.1.wav | shopkeeper | 1.0 | ¡Hasta luego! |  |
| shop_done.2 | speech/shop_done.2.slow.wav | shopkeeper | 0.8 | ¡Nos vemos! |  |
| shop_done.2 | speech/shop_done.2.wav | shopkeeper | 1.0 | ¡Nos vemos! |  |
| shop_give.1 | speech/shop_give.1.slow.wav | shopkeeper | 0.8 | Toma la botella. El agua está en el río. |  |
| shop_give.1 | speech/shop_give.1.wav | shopkeeper | 1.0 | Toma la botella. El agua está en el río. |  |
| shop_give.2 | speech/shop_give.2.slow.wav | shopkeeper | 0.8 | Aquí tienes la botella. El agua está en el río. |  |
| shop_give.2 | speech/shop_give.2.wav | shopkeeper | 1.0 | Aquí tienes la botella. El agua está en el río. |  |
| shop_give_bread.1 | speech/shop_give_bread.1.slow.wav | shopkeeper | 0.8 | Aquí tienes el pan. |  |
| shop_give_bread.1 | speech/shop_give_bread.1.wav | shopkeeper | 1.0 | Aquí tienes el pan. |  |
| shop_give_bread.2 | speech/shop_give_bread.2.slow.wav | shopkeeper | 0.8 | Toma el pan. |  |
| shop_give_bread.2 | speech/shop_give_bread.2.wav | shopkeeper | 1.0 | Toma el pan. |  |
| shop_hay.1 | speech/shop_hay.1.slow.wav | shopkeeper | 0.8 | Hay agua y pan. |  |
| shop_hay.1 | speech/shop_hay.1.wav | shopkeeper | 1.0 | Hay agua y pan. |  |
| shop_help_ask.1 | speech/shop_help_ask.1.slow.wav | shopkeeper | 0.8 | Puedes decir: «Quiero agua, por favor». |  |
| shop_help_ask.1 | speech/shop_help_ask.1.wav | shopkeeper | 1.0 | Puedes decir: «Quiero agua, por favor». |  |
| shop_help_offer.1 | speech/shop_help_offer.1.slow.wav | shopkeeper | 0.8 | Puedes decir: «Sí, por favor» o «No, gracias». |  |
| shop_help_offer.1 | speech/shop_help_offer.1.wav | shopkeeper | 1.0 | Puedes decir: «Sí, por favor» o «No, gracias». |  |
| shop_need.1 | speech/shop_need.1.slow.wav | shopkeeper | 0.8 | ¿Qué necesitas? |  |
| shop_need.1 | speech/shop_need.1.wav | shopkeeper | 1.0 | ¿Qué necesitas? |  |
| shop_offer.1 | speech/shop_offer.1.slow.wav | shopkeeper | 0.8 | ¿Quieres pan? |  |
| shop_offer.1 | speech/shop_offer.1.wav | shopkeeper | 1.0 | ¿Quieres pan? |  |
| shop_price.1 | speech/shop_price.1.slow.wav | shopkeeper | 0.8 | El pan cuesta dos monedas. |  |
| shop_price.1 | speech/shop_price.1.wav | shopkeeper | 1.0 | El pan cuesta dos monedas. |  |
| shop_thank.1 | speech/shop_thank.1.slow.wav | shopkeeper | 0.8 | De nada. |  |
| shop_thank.1 | speech/shop_thank.1.wav | shopkeeper | 1.0 | De nada. |  |
| shop_thank.2 | speech/shop_thank.2.slow.wav | shopkeeper | 0.8 | ¡Con gusto! |  |
| shop_thank.2 | speech/shop_thank.2.wav | shopkeeper | 1.0 | ¡Con gusto! |  |
| smith_buy.1 | speech/smith_buy.1.slow.wav | smith | 0.8 | ¡Gracias! Aquí tienes tus monedas. |  |
| smith_buy.1 | speech/smith_buy.1.wav | smith | 1.0 | ¡Gracias! Aquí tienes tus monedas. |  |
| smith_bye.1 | speech/smith_bye.1.slow.wav | smith | 0.8 | ¡Adiós! Hasta luego. |  |
| smith_bye.1 | speech/smith_bye.1.wav | smith | 1.0 | ¡Adiós! Hasta luego. |  |
| smith_bye.2 | speech/smith_bye.2.slow.wav | smith | 0.8 | Hasta luego. |  |
| smith_bye.2 | speech/smith_bye.2.wav | smith | 1.0 | Hasta luego. |  |
| smith_cant_sell.1 | speech/smith_cant_sell.1.slow.wav | smith | 0.8 | No tienes madera. |  |
| smith_cant_sell.1 | speech/smith_cant_sell.1.wav | smith | 1.0 | No tienes madera. |  |
| smith_delivered.1 | speech/smith_delivered.1.slow.wav | smith | 0.8 | ¡Gracias! Toma el hacha. |  |
| smith_delivered.1 | speech/smith_delivered.1.wav | smith | 1.0 | ¡Gracias! Toma el hacha. |  |
| smith_greet.1 | speech/smith_greet.1.slow.wav | smith | 0.8 | ¡Hola! Esta es mi herrería. |  |
| smith_greet.1 | speech/smith_greet.1.wav | smith | 1.0 | ¡Hola! Esta es mi herrería. |  |
| smith_hello.1 | speech/smith_hello.1.slow.wav | smith | 0.8 | ¡Hola! |  |
| smith_hello.1 | speech/smith_hello.1.wav | smith | 1.0 | ¡Hola! |  |
| smith_help.1 | speech/smith_help.1.slow.wav | smith | 0.8 | Puedes preguntar: «¿Cómo te llamas?». |  |
| smith_help.1 | speech/smith_help.1.wav | smith | 1.0 | Puedes preguntar: «¿Cómo te llamas?». |  |
| smith_job.1 | speech/smith_job.1.slow.wav | smith | 0.8 | Soy el herrero. Trabajo con fuego y madera. |  |
| smith_job.1 | speech/smith_job.1.wav | smith | 1.0 | Soy el herrero. Trabajo con fuego y madera. |  |
| smith_name.1 | speech/smith_name.1.slow.wav | smith | 0.8 | Me llamo Domingo. |  |
| smith_name.1 | speech/smith_name.1.wav | smith | 1.0 | Me llamo Domingo. |  |
| smith_offer.1 | speech/smith_offer.1.slow.wav | smith | 0.8 | ¿Quieres un hacha? Necesito dos bloques de madera y dos monedas. |  |
| smith_offer.1 | speech/smith_offer.1.wav | smith | 1.0 | ¿Quieres un hacha? Necesito dos bloques de madera y dos monedas. |  |
| smith_thank.1 | speech/smith_thank.1.slow.wav | smith | 0.8 | De nada. |  |
| smith_thank.1 | speech/smith_thank.1.wav | smith | 1.0 | De nada. |  |
| smith_thank.2 | speech/smith_thank.2.slow.wav | smith | 0.8 | ¡Con gusto! |  |
| smith_thank.2 | speech/smith_thank.2.wav | smith | 1.0 | ¡Con gusto! |  |
| smith_wont_buy.1 | speech/smith_wont_buy.1.slow.wav | smith | 0.8 | No, gracias. Solo compro madera. |  |
| smith_wont_buy.1 | speech/smith_wont_buy.1.wav | smith | 1.0 | No, gracias. Solo compro madera. |  |
| villager_bye.1 | speech/villager_bye.1.slow.wav | villager | 0.8 | ¡Adiós! Hasta luego. |  |
| villager_bye.1 | speech/villager_bye.1.wav | villager | 1.0 | ¡Adiós! Hasta luego. |  |
| villager_bye.2 | speech/villager_bye.2.slow.wav | villager | 0.8 | Hasta luego. |  |
| villager_bye.2 | speech/villager_bye.2.wav | villager | 1.0 | Hasta luego. |  |
| villager_give.1 | speech/villager_give.1.slow.wav | villager | 0.8 | Aquí tienes. |  |
| villager_give.1 | speech/villager_give.1.wav | villager | 1.0 | Aquí tienes. |  |
| villager_give.2 | speech/villager_give.2.slow.wav | villager | 0.8 | Toma. |  |
| villager_give.2 | speech/villager_give.2.wav | villager | 1.0 | Toma. |  |
| villager_greet.1 | speech/villager_greet.1.slow.wav | villager | 0.8 | ¡Hola! ¿Qué buscas? |  |
| villager_greet.1 | speech/villager_greet.1.wav | villager | 1.0 | ¡Hola! ¿Qué buscas? |  |
| villager_hello.1 | speech/villager_hello.1.slow.wav | villager | 0.8 | Hola. |  |
| villager_hello.1 | speech/villager_hello.1.wav | villager | 1.0 | Hola. |  |
| villager_help.1 | speech/villager_help.1.slow.wav | villager | 0.8 | Puedes preguntar: «¿Dónde está el río?». |  |
| villager_help.1 | speech/villager_help.1.wav | villager | 1.0 | Puedes preguntar: «¿Dónde está el río?». |  |
| villager_point.1 | speech/villager_point.1.slow.wav | villager | 0.8 | Mira, está allí. |  |
| villager_point.1 | speech/villager_point.1.wav | villager | 1.0 | Mira, está allí. |  |
| villager_point.2 | speech/villager_point.2.slow.wav | villager | 0.8 | Está allí, mira. |  |
| villager_point.2 | speech/villager_point.2.wav | villager | 1.0 | Está allí, mira. |  |
| villager_thank.1 | speech/villager_thank.1.slow.wav | villager | 0.8 | De nada. |  |
| villager_thank.1 | speech/villager_thank.1.wav | villager | 1.0 | De nada. |  |
| villager_thank.2 | speech/villager_thank.2.slow.wav | villager | 0.8 | ¡Con gusto! |  |
| villager_thank.2 | speech/villager_thank.2.wav | villager | 1.0 | ¡Con gusto! |  |
| villager_unknown_place.1 | speech/villager_unknown_place.1.slow.wav | villager | 0.8 | No sé dónde está. |  |
| villager_unknown_place.1 | speech/villager_unknown_place.1.wav | villager | 1.0 | No sé dónde está. |  |
