# Speech checklist (S9, D-game-12 step 4)

The owner listens to every file for cut-offs and for text and audio that do not match (the interim content-trust check of
D-game-12). Mark each row `ok`, `cut-off` or `mismatch` in the last column, with a note after it if needed. The table is written by
`scripts/prerender-speech.sh` from the bundle: the marks of a row are kept while its file and its text are unchanged, and a row whose
text changed is listed again without a mark. The files are Kokoro model output (kokoro-multi-lang-v1_0), in the voices below.

## Speakers

| Speaker | Voice | Kokoro speaker | Speed (slow) | Says |
| --- | --- | --- | --- | --- |
| guide (Luna) | `ef_dora` | 28 | 1.0 (0.8) | every pool no other NPC has, its prompts, the word cards and the model answers |
| shopkeeper (Tendero) | `em_alex` | 29 | 1.0 (0.8) | the pools `shop_*` and `guide.lesson.shop.*`, and its prompts |
| villager (Aldeano) | `em_santa` | 53 | 1.0 (0.8) | the pools `villager_*` and `guide.lesson.villager.*`, and its prompts |

Provisional voices of the NPCs to come (VILLAGE_PLAN V06, D-game-22 (d)): their lines are recorded in these voices when their files come.
The owner hears each in the voice trial, `docs/game/voice-trial/` (its README); a voice the owner rejects is changed in the NPC's file and its
lines are rendered again.

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
| guide.lesson.end.1 | speech/guide.lesson.end.1.slow.wav | guide | 0.8 | ¡Muy bien! Ya tienes fuego. |  |
| guide.lesson.end.1 | speech/guide.lesson.end.1.wav | guide | 1.0 | ¡Muy bien! Ya tienes fuego. |  |
| guide.lesson.fast.1 | speech/guide.lesson.fast.1.slow.wav | guide | 0.8 | Mira. La madera es para la antorcha. |  |
| guide.lesson.fast.1 | speech/guide.lesson.fast.1.wav | guide | 1.0 | Mira. La madera es para la antorcha. |  |
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
| guide.lesson.shop.bottle.1 | speech/guide.lesson.shop.bottle.1.slow.wav | shopkeeper | 0.8 | ¿Tiene usted una botella? |  |
| guide.lesson.shop.bottle.1 | speech/guide.lesson.shop.bottle.1.wav | shopkeeper | 1.0 | ¿Tiene usted una botella? |  |
| guide.lesson.shop.had.1 | speech/guide.lesson.shop.had.1.slow.wav | shopkeeper | 0.8 | ¿Bebiste agua ayer? |  |
| guide.lesson.shop.had.1 | speech/guide.lesson.shop.had.1.wav | shopkeeper | 1.0 | ¿Bebiste agua ayer? |  |
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
| guide.lesson.take.1 | speech/guide.lesson.take.1.slow.wav | guide | 0.8 | Toma una fruta. |  |
| guide.lesson.take.1 | speech/guide.lesson.take.1.wav | guide | 1.0 | Toma una fruta. |  |
| guide.lesson.task.1 | speech/guide.lesson.task.1.slow.wav | guide | 0.8 | Necesitas madera y frutas. |  |
| guide.lesson.task.1 | speech/guide.lesson.task.1.wav | guide | 1.0 | Necesitas madera y frutas. |  |
| guide.lesson.villager.be.1 | speech/guide.lesson.villager.be.1.slow.wav | villager | 0.8 | Tú y yo somos amigos. |  |
| guide.lesson.villager.be.1 | speech/guide.lesson.villager.be.1.wav | villager | 1.0 | Tú y yo somos amigos. |  |
| guide.lesson.villager.eat.1 | speech/guide.lesson.villager.eat.1.slow.wav | villager | 0.8 | Ellos van a comer pan. |  |
| guide.lesson.villager.eat.1 | speech/guide.lesson.villager.eat.1.wav | villager | 1.0 | Ellos van a comer pan. |  |
| guide.lesson.villager.go.1 | speech/guide.lesson.villager.go.1.slow.wav | villager | 0.8 | Vamos a la tienda. |  |
| guide.lesson.villager.go.1 | speech/guide.lesson.villager.go.1.wav | villager | 1.0 | Vamos a la tienda. |  |
| guide.lesson.villager.goes.1 | speech/guide.lesson.villager.goes.1.slow.wav | villager | 0.8 | Hoy vas a la tienda. |  |
| guide.lesson.villager.goes.1 | speech/guide.lesson.villager.goes.1.wav | villager | 1.0 | Hoy vas a la tienda. |  |
| guide.lesson.villager.luna.1 | speech/guide.lesson.villager.luna.1.slow.wav | villager | 0.8 | ¿Qué quiere Luna? |  |
| guide.lesson.villager.luna.1 | speech/guide.lesson.villager.luna.1.wav | villager | 1.0 | ¿Qué quiere Luna? |  |
| guide.lesson.villager.plan.1 | speech/guide.lesson.villager.plan.1.slow.wav | villager | 0.8 | ¿Qué vas a comer mañana? |  |
| guide.lesson.villager.plan.1 | speech/guide.lesson.villager.plan.1.wav | villager | 1.0 | ¿Qué vas a comer mañana? |  |
| guide.lesson.villager.plan_we.1 | speech/guide.lesson.villager.plan_we.1.slow.wav | villager | 0.8 | ¿Qué vamos a comer? |  |
| guide.lesson.villager.plan_we.1 | speech/guide.lesson.villager.plan_we.1.wav | villager | 1.0 | ¿Qué vamos a comer? |  |
| guide.lesson.villager.she.1 | speech/guide.lesson.villager.she.1.slow.wav | villager | 0.8 | Luna es la guía. Ella es mi amiga. |  |
| guide.lesson.villager.she.1 | speech/guide.lesson.villager.she.1.wav | villager | 1.0 | Luna es la guía. Ella es mi amiga. |  |
| guide.lesson.villager.they.1 | speech/guide.lesson.villager.they.1.slow.wav | villager | 0.8 | Ellos tienen pan. |  |
| guide.lesson.villager.they.1 | speech/guide.lesson.villager.they.1.wav | villager | 1.0 | Ellos tienen pan. |  |
| guide.lesson.villager.want.1 | speech/guide.lesson.villager.want.1.slow.wav | villager | 0.8 | ¿Qué quieres? |  |
| guide.lesson.villager.want.1 | speech/guide.lesson.villager.want.1.wav | villager | 1.0 | ¿Qué quieres? |  |
| guide.lesson.villager.we.1 | speech/guide.lesson.villager.we.1.slow.wav | villager | 0.8 | ¿Qué necesitamos? |  |
| guide.lesson.villager.we.1 | speech/guide.lesson.villager.we.1.wav | villager | 1.0 | ¿Qué necesitamos? |  |
| guide.lesson.villager.where.1 | speech/guide.lesson.villager.where.1.slow.wav | villager | 0.8 | Ella está en el campamento. |  |
| guide.lesson.villager.where.1 | speech/guide.lesson.villager.where.1.wav | villager | 1.0 | Ella está en el campamento. |  |
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
| phrase.donde-esta-el-rio | speech/phrase.donde-esta-el-rio.slow.wav | guide | 0.8 | ¿Dónde está el río? |  |
| phrase.donde-esta-el-rio | speech/phrase.donde-esta-el-rio.wav | guide | 1.0 | ¿Dónde está el río? |  |
| phrase.ella-quiere-fruta | speech/phrase.ella-quiere-fruta.slow.wav | guide | 0.8 | Ella quiere fruta. |  |
| phrase.ella-quiere-fruta | speech/phrase.ella-quiere-fruta.wav | guide | 1.0 | Ella quiere fruta. |  |
| phrase.gracias | speech/phrase.gracias.slow.wav | guide | 0.8 | Gracias. |  |
| phrase.gracias | speech/phrase.gracias.wav | guide | 1.0 | Gracias. |  |
| phrase.hola | speech/phrase.hola.slow.wav | guide | 0.8 | Hola. |  |
| phrase.hola | speech/phrase.hola.wav | guide | 1.0 | Hola. |  |
| phrase.mas-despacio | speech/phrase.mas-despacio.slow.wav | guide | 0.8 | Más despacio, por favor. |  |
| phrase.mas-despacio | speech/phrase.mas-despacio.wav | guide | 1.0 | Más despacio, por favor. |  |
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
| phrase.que-es-esto | speech/phrase.que-es-esto.slow.wav | guide | 0.8 | ¿Qué es esto? |  |
| phrase.que-es-esto | speech/phrase.que-es-esto.wav | guide | 1.0 | ¿Qué es esto? |  |
| phrase.queremos-pan | speech/phrase.queremos-pan.slow.wav | guide | 0.8 | Queremos pan. |  |
| phrase.queremos-pan | speech/phrase.queremos-pan.wav | guide | 1.0 | Queremos pan. |  |
| phrase.quiero-madera | speech/phrase.quiero-madera.slow.wav | guide | 0.8 | Quiero madera. |  |
| phrase.quiero-madera | speech/phrase.quiero-madera.wav | guide | 1.0 | Quiero madera. |  |
| phrase.si | speech/phrase.si.slow.wav | guide | 0.8 | Sí. |  |
| phrase.si | speech/phrase.si.wav | guide | 1.0 | Sí. |  |
| phrase.si-por-favor | speech/phrase.si-por-favor.slow.wav | guide | 0.8 | Sí, por favor. |  |
| phrase.si-por-favor | speech/phrase.si-por-favor.wav | guide | 1.0 | Sí, por favor. |  |
| phrase.tengo-botella | speech/phrase.tengo-botella.slow.wav | guide | 0.8 | Tengo una botella. |  |
| phrase.tengo-botella | speech/phrase.tengo-botella.wav | guide | 1.0 | Tengo una botella. |  |
| phrase.tengo-fruta | speech/phrase.tengo-fruta.slow.wav | guide | 0.8 | Tengo una fruta. |  |
| phrase.tengo-fruta | speech/phrase.tengo-fruta.wav | guide | 1.0 | Tengo una fruta. |  |
| phrase.vamos-a-comer-fruta | speech/phrase.vamos-a-comer-fruta.slow.wav | guide | 0.8 | Vamos a comer fruta. |  |
| phrase.vamos-a-comer-fruta | speech/phrase.vamos-a-comer-fruta.wav | guide | 1.0 | Vamos a comer fruta. |  |
| phrase.voy-a-comer-pan | speech/phrase.voy-a-comer-pan.slow.wav | guide | 0.8 | Voy a comer pan. |  |
| phrase.voy-a-comer-pan | speech/phrase.voy-a-comer-pan.wav | guide | 1.0 | Voy a comer pan. |  |
| phrase.voy-a-ir-al-rio | speech/phrase.voy-a-ir-al-rio.slow.wav | guide | 0.8 | Mañana voy a ir al río. |  |
| phrase.voy-a-ir-al-rio | speech/phrase.voy-a-ir-al-rio.wav | guide | 1.0 | Mañana voy a ir al río. |  |
| prompt.guide-offer | speech/prompt.guide-offer.slow.wav | guide | 0.8 | ¿Quieres agua? |  |
| prompt.guide-offer | speech/prompt.guide-offer.wav | guide | 1.0 | ¿Quieres agua? |  |
| prompt.guide-open | speech/prompt.guide-open.slow.wav | guide | 0.8 | ¡Hola! Soy la guía. ¿En qué te ayudo? |  |
| prompt.guide-open | speech/prompt.guide-open.wav | guide | 1.0 | ¡Hola! Soy la guía. ¿En qué te ayudo? |  |
| prompt.guide-review | speech/prompt.guide-review.slow.wav | guide | 0.8 | ¿Qué es esto? |  |
| prompt.guide-review | speech/prompt.guide-review.wav | guide | 1.0 | ¿Qué es esto? |  |
| prompt.lesson-ate | speech/prompt.lesson-ate.slow.wav | shopkeeper | 0.8 | ¿Qué comiste ayer? |  |
| prompt.lesson-ate | speech/prompt.lesson-ate.wav | shopkeeper | 1.0 | ¿Qué comiste ayer? |  |
| prompt.lesson-bottle | speech/prompt.lesson-bottle.slow.wav | shopkeeper | 0.8 | ¿Tiene usted una botella? |  |
| prompt.lesson-bottle | speech/prompt.lesson-bottle.wav | shopkeeper | 1.0 | ¿Tiene usted una botella? |  |
| prompt.lesson-had | speech/prompt.lesson-had.slow.wav | shopkeeper | 0.8 | ¿Bebiste agua ayer? |  |
| prompt.lesson-had | speech/prompt.lesson-had.wav | shopkeeper | 1.0 | ¿Bebiste agua ayer? |  |
| prompt.lesson-have | speech/prompt.lesson-have.slow.wav | guide | 0.8 | ¿Tienes una fruta? |  |
| prompt.lesson-have | speech/prompt.lesson-have.wav | guide | 1.0 | ¿Tienes una fruta? |  |
| prompt.lesson-hello | speech/prompt.lesson-hello.slow.wav | guide | 0.8 | ¡Hola! |  |
| prompt.lesson-hello | speech/prompt.lesson-hello.wav | guide | 1.0 | ¡Hola! |  |
| prompt.lesson-name | speech/prompt.lesson-name.slow.wav | guide | 0.8 | Me llamo Luna. |  |
| prompt.lesson-name | speech/prompt.lesson-name.wav | guide | 1.0 | Me llamo Luna. |  |
| prompt.lesson-need | speech/prompt.lesson-need.slow.wav | guide | 0.8 | ¿Qué necesitas? |  |
| prompt.lesson-need | speech/prompt.lesson-need.wav | guide | 1.0 | ¿Qué necesitas? |  |
| prompt.lesson-offer | speech/prompt.lesson-offer.slow.wav | guide | 0.8 | ¿Quieres fruta? |  |
| prompt.lesson-offer | speech/prompt.lesson-offer.wav | guide | 1.0 | ¿Quieres fruta? |  |
| prompt.lesson-plan | speech/prompt.lesson-plan.slow.wav | villager | 0.8 | ¿Qué vas a comer mañana? |  |
| prompt.lesson-plan | speech/prompt.lesson-plan.wav | villager | 1.0 | ¿Qué vas a comer mañana? |  |
| prompt.lesson-plan-we | speech/prompt.lesson-plan-we.slow.wav | villager | 0.8 | ¿Qué vamos a comer? |  |
| prompt.lesson-plan-we | speech/prompt.lesson-plan-we.wav | villager | 1.0 | ¿Qué vamos a comer? |  |
| prompt.lesson-she | speech/prompt.lesson-she.slow.wav | villager | 0.8 | ¿Qué quiere Luna? |  |
| prompt.lesson-she | speech/prompt.lesson-she.wav | villager | 1.0 | ¿Qué quiere Luna? |  |
| prompt.lesson-task | speech/prompt.lesson-task.slow.wav | guide | 0.8 | Necesitas madera y frutas. |  |
| prompt.lesson-task | speech/prompt.lesson-task.wav | guide | 1.0 | Necesitas madera y frutas. |  |
| prompt.lesson-thanks | speech/prompt.lesson-thanks.slow.wav | guide | 0.8 | ¡Muy bien! Tienes una fruta. |  |
| prompt.lesson-thanks | speech/prompt.lesson-thanks.wav | guide | 1.0 | ¡Muy bien! Tienes una fruta. |  |
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
| prompt.lesson-we | speech/prompt.lesson-we.slow.wav | villager | 0.8 | ¿Qué necesitamos? |  |
| prompt.lesson-we | speech/prompt.lesson-we.wav | villager | 1.0 | ¿Qué necesitamos? |  |
| prompt.lesson-went | speech/prompt.lesson-went.slow.wav | shopkeeper | 0.8 | ¿Fuiste al río ayer? |  |
| prompt.lesson-went | speech/prompt.lesson-went.wav | shopkeeper | 1.0 | ¿Fuiste al río ayer? |  |
| prompt.lesson-what | speech/prompt.lesson-what.slow.wav | guide | 0.8 | Mira. |  |
| prompt.lesson-what | speech/prompt.lesson-what.wav | guide | 1.0 | Mira. |  |
| prompt.lesson-yesno | speech/prompt.lesson-yesno.slow.wav | guide | 0.8 | ¿Es madera? |  |
| prompt.lesson-yesno | speech/prompt.lesson-yesno.wav | guide | 1.0 | ¿Es madera? |  |
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
| shop_help_ask.1 | speech/shop_help_ask.1.slow.wav | shopkeeper | 0.8 | Puedes decir: «Quiero agua, por favor». |  |
| shop_help_ask.1 | speech/shop_help_ask.1.wav | shopkeeper | 1.0 | Puedes decir: «Quiero agua, por favor». |  |
| shop_help_offer.1 | speech/shop_help_offer.1.slow.wav | shopkeeper | 0.8 | Puedes decir: «Sí, por favor» o «No, gracias». |  |
| shop_help_offer.1 | speech/shop_help_offer.1.wav | shopkeeper | 1.0 | Puedes decir: «Sí, por favor» o «No, gracias». |  |
| shop_need.1 | speech/shop_need.1.slow.wav | shopkeeper | 0.8 | ¿Qué necesitas? |  |
| shop_need.1 | speech/shop_need.1.wav | shopkeeper | 1.0 | ¿Qué necesitas? |  |
| shop_offer.1 | speech/shop_offer.1.slow.wav | shopkeeper | 0.8 | ¿Quieres pan? |  |
| shop_offer.1 | speech/shop_offer.1.wav | shopkeeper | 1.0 | ¿Quieres pan? |  |
| shop_thank.1 | speech/shop_thank.1.slow.wav | shopkeeper | 0.8 | De nada. |  |
| shop_thank.1 | speech/shop_thank.1.wav | shopkeeper | 1.0 | De nada. |  |
| shop_thank.2 | speech/shop_thank.2.slow.wav | shopkeeper | 0.8 | ¡Con gusto! |  |
| shop_thank.2 | speech/shop_thank.2.wav | shopkeeper | 1.0 | ¡Con gusto! |  |
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
