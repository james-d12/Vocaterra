W10 Phase A report (2026-11-07, Sonnet 5.5 medium). Branch W10-bread-lesson, commits 83b90b3c (draft) and a2831ff4 (intent examples,
sheet dates); 36 files, +1328/-52; content and game only. Scripted baseline /tmp/claude-1000/w10/runs-before (26 runs).

Checks: content-loop --grammar --tier none: validate, A2 lint 0/0, cefr-check A2, --validate-content clean; grammar 0/0; stops at step 3
(66 files to render). PhraseSheetTests: two test bugs to fix in Phase B (pinned id list; W05's `row.Intent is null` vs "-").

bol over tazón (cognate, the design's rows, tazón is a bigger bowl).

New Spanish
- does (Luz, pools guide.lesson.baker.proc_*): «Pon la harina en el bol.», «Echa el agua en el bol.», «Mezcla con la cuchara.», «Pon la
  madera en el horno.», «Pon la masa en el horno.», «Saca el pan del horno.» (A2, sacar); chunks «Haz la masa.», «Haz el pan en el
  horno.»; goal «Haz pan.»
- says: «Voy a hacer pan.» (state_action, lesson-bake-plan; error voy a hago pan), «Hice pan.» (lesson-made-bread; error ayer hago pan),
  «Quiero harina.» (request_item, stall-need; error quiero el harina), «Vendo pan.» (offer_item, stall-need; error vendo la pan).
- hears: «Hoy vamos a hacer pan. ¿Me ayudas?», «¿Qué hiciste?», «Primero.» (out-of-order), W08's «Toma. Aquí tienes.» and «¿Quieres hacer
  pan?».
- Luz prompts: «¿Qué vas a hacer hoy?», «¿Qué hiciste?».
- Shopkeeper: shop_sell_flour «Aquí tienes la harina.»; shop_buy «¡Gracias! Aquí tienes tus monedas.»; shop_wont_buy «No, gracias. Solo
  compro pan.»; shop_cant_sell «No tienes pan.»; shop_cant_pay «Necesitas una moneda.»; shop_none_asked «Hay harina. ¿Cuánta quieres?»;
  shop_none_offered «¿Cuántos panes me vendes?»; shop_no_room «Tu bolsa está llena.»
- Lesson en-la-panaderia: a2, after ayer-y-manana, 14 steps (k3-oven the do step with GrantRequiredByLesson; k5-sell a do step).
  Checks hear.* x4, say.* x4.

CLI: all four production lines clean with variants; the error examples give the expected tags. No rewording, no core patch.

Values: flour item 34; station horno (prop oven, fixtures bol, cuchara, horno); uses flour 1, water-bottle 1, wood 1; bonus after 5;
chunks la-masa, el-horno; nouns harina, bol, cuchara, masa, horno, mano; waivers harina, bol, cuchara, masa, echar, mezclar; stall
sell_flour and buy_bread at 1 coin; instead {pan: give_bread, harina: sell_flour}; the class appended last in lessons.json.

Design vs code: pools renamed guide.lesson.baker.* for the strict lint; hears rows 4, 5 are W08's lines; review candidates from earlier
lessons; validator GameDataValidator.IsMadeThing added for `masa`; k5-sell credits vendo-pan by free talk.

Known gaps for Phase B: AnswerMisses checks only give_water's misses (instead target's misses to chain); shop_hay / shop_wont_sell still
deny flour; «Primero.» not wired (UI string only); flour icon placement vs the scripted runs' horizon.

---
W10 Phase B report (2026-11-07). Final commit ffad49b7; identity golden re-recorded in cb3912e8 (21 new lines, 131 pipeline hashes).
Suite 9847 passed, 3 skipped (first full run killed at the fuzz suite, exit 143; passed on retry). Lint 0, grammar 0/0, build, format,
validate clean. Corrections applied: «Todavía no.» (pool guide.lesson.baker.not_yet, played on an out-of-order act), shop_buy
«¡Gracias! Aquí tienes.», shop_cant_pay «La harina cuesta una moneda.» (flour only), shop_hay «Hay agua, pan y harina.», shop_wont_sell
«No tengo eso. Tengo agua, pan y harina.» (re-recorded). AnswerMisses chains the instead target's misses. PhraseSheet.Closest answers the
prompt's first row when nothing is close. SessionSkip stops at 10. The class's selling step is an answer step. TestGame.Data is the bundle
without the procedure; TestGame.BreadData has it. Flour icon after the UI-only atlas cells (no world mip moved).
Scripted runs: speech 691 lines/1382 files; lessons (12); Lessons page 212 lines; **r22-r24: Luz's offer swallows the first free-talk line
of the day («quiero dos panes, por favor» unknown, «Te entiendo. ¿Quieres pan?»; r22 buys no bread)**. r27-bread PNGs in
/tmp/claude-1000/w10/r27/ (offer, first, early, oven, done: 2 loaves after two slips). `--lesson-run en-la-panaderia` hangs (r27 uses the
offer). Left undone: Luz's repeat after «Todavía no.»; the offer's fall-through.
