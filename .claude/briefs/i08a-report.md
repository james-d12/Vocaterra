# I08a report (gear content, first half: the materials and the clothes)

The agent's report, saved by the orchestrator (2026-11-18). Sonnet 5.5 at high effort; brief `i08a-gear-content-clothes.md`. Gated at
`1ed01a66`: 13,451 passed, 0 failed, 9 skipped (+121); timing 23; format clean; strict A2 lint clean; 0 new grammar findings; 36 speech files.

Branch `I08a-gear-clothes`: 0dc52b63 (items, recipes, prices, starting clothes, tests), 4702f792 (speech, grammar cache, manifests),
34f24151 (identity baseline re-recorded: 4 new, 129 moved, 1 removed, from the new lexicon words and shop lines), 3efee9a3 (merge).

**Items** (ids 100 to 109; icons painted in code, `BlockArtGear.cs`, one atlas cell each, the look colour the icon's): tela, cuero,
hierro (material, stack 99); sombrero head +1 straw #d7b56a; gorro head +2 orange #d9782d; camisa body +1 cream #ece6d0; chaqueta body +3
olive #5f7f3f; abrigo body +5 wine #8c2f3c; pantalón legs +2 blue #3f5f95; zapato feet +1 brown #5a3a22 (clothing, stack 1). The atlas grew
from 15 to 17 colour rows (136 cells; I08b's 13 fit): `BakedLightGoldenTests`' five literals re-recorded (V depends on the atlas height; not
proven to be V alone), mip levels 9 to 10 (`LookTests`).

**Words:** nouns with English (cloth, leather, iron, hat, woolly cap, shirt, jacket, coat, trousers, shoes); lexicon `tela`, `cuero`, `gorro`
in `lexicon/world.json`; world words in `cefr/a1.txt`: tela, cuero, hierro, gorro, chaqueta, abrigo, pantalón. A new noun flag `"plural":
true` (`NounEntry.PluralOnly`) for `pantalón` and `zapato`: the lemma stays singular; articles, cards, labels read «los pantalones», «los
zapatos». Cards 43 to 53.

**Lines:** Pablo's flour event and pool renamed `stall.sell_goods` / `shop_sell`: «Aquí tienes. ¡Gracias!» (Here you are. Thank you!),
«Necesitas más monedas.» (You need more coins.), «¿Cuánto quieres?» (How much do you want?), «No tengo eso. Tengo agua, pan, harina, tela,
cuero y ropa.» (I do not have that. I have water, bread, flour, cloth, leather and clothes.), «Hay agua, pan, harina, tela, cuero y ropa.»
Domingo: «Aquí tienes el hierro. ¡Gracias!», «El hierro cuesta tres monedas.», «Hay hierro. ¿Cuánto quieres?». Domingo's prompt takes
`state_want` («quiero hierro»), not `request_item` (both fail the strict lint). «¿Tienes pantalones?» is small talk (`state_have`), as
«¿Tienes harina?» is today: a core grammar change, left to the owner.

**Recipes (mesa) and prices:** as 3.13a (sombrero 1 tela 2; gorro 2 tela 3; camisa 2 tela 3; chaqueta 3 tela 1 cuero 6; abrigo 4 tela 2
cuero 10; pantalón 2 tela 3; zapato 1 cuero 3; tela 1, cuero 2 from Pablo; hierro 3 from Domingo).

**Starting clothes:** `game/session.json` `"wears": ["shirt","trousers","shoes"]` (schema, `ObjectiveBook.Wears`, validator,
`VoxelSession` `WearFromStart`); +4 (calor +4); a loaded save keeps what it saved. The new-game digest moved (pinned nowhere); the
end-to-end goldens did not; the scripted runs (windows) not run: their inventory shots now show clothes, temperature reads 4 warmer.

**Tests:** about 120 new (`ClothesAndMaterialsTests`, `ClothesRecipesTests`, `GearShopTests`, `StartingClothesTests`, `GearIconTests`);
counts moved (items 14 to 24, recipes 2 to 9, nouns 43 to 53, host events 27 to 28, pools 410 to 413, smith lines 33 to 36, mips 9 to 10);
shop lines in four tests; test items 100 to 103 moved to 900+; tests needing a bare player use `GearItems.Undress`. Known: a hat hides the
face (I09's one-box head).
