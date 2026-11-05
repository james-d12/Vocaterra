# M78 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `M78-small-core-points` on V29's branch (4909f9b1); commits c2edeb8c (tools.md) .. 01e44992. Bundle hash
d6ed3334dd35e72cd668689f1206f993214bc008fcda85bc73a0a49c0027d674 (save-v1, save-v4).

1. Catalog (intents.json, no new intent): `la tienda está en el pueblo` -> describe (`[ART] [NOUN:object] [VERB:estar] en [ART] [NOUN]`);
   `usted hace hachas muy buenas` -> describe (`[VERB:hacer] [NOUN:object] (muy|más|menos)? [ADJ]`), off-prompt at smith-open (smith does not
   allow describe: «Te entiendo. Yo hago hachas.»); `tú pescas mucho` -> state_action (`[VERB:pescar|trabajar] (mucho|poco)`). Side effect:
   `yo trabajo mucho`, `trabajas mucho`, `ustedes trabajan mucho` are state_action.
2. Unknown names: `IAnalysisRules.IsNamePosition` (Spanish pack), `NameFallback`, `NameFallbacks.For(prompt)`, an `Analyze(text, grammars, names)`
   overload. A word with no reading that no grammar binds is a name after soy / llamo / mi nombre es / greetings / adiós / chao, or as a lone
   capitalised word, at prompts that allow intents. Clean now: `soy pedro`, `hola, soy pedro`, `hola pedro`, `Pedro`. Not fixed: bare lower-case
   `pedro` (indistinguishable from gibberish; game tests use `xyzzy`).
3. `necesito ayuda domingo`: accepted (domingo is the lexicon's day noun; the host strips the name). Recorded in NameLineTests and pipeline.md.
4. tools.md describes the live ci.yml.
5. Recast takes the verdict's errors: `cuál es esto` at lesson-what -> «¡Eso es! «Qué es esto».».
6. Fuzz oracles unchanged (only the harness's by-hand pipeline copies follow the name overload); 61 identity entries re-recorded; goldens unmoved.
   Full suite: 9127 passed, 4 failed, 3 skipped. Failures: the grammar-cache test (43 misses: V29's 39 plus `la tienda está en el pueblo`,
   `usted hace hachas muy buenas`, `tú pescas mucho`, `usted trabaja mucho`), and three game tests the change moved (fixed by M78b, a Haiku
   follow-up on the same branch). Build, format, validate, A2 lint, licence scan clean; core demo unchanged.
