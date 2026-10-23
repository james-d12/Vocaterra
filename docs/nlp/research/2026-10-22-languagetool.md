# LanguageTool as a grammar check on the content we author: research and proposal

Research report, 2026-10-22, written against `feature/prototype` at `1496d5a`. It answers the owner's request: "add this language tool app that can
be used to grammar check all the sentences that we are making, just as an additional layer of checking on top of our nlp" [1]. Nothing was
implemented and no content was edited. Numbers come from a hands-on run of a local server (section 2); claims about LanguageTool's behaviour that
were not measured carry a source, and judgements are marked **est.**

**The short answer.** Add it, as a development-time check on authored content, run against a local server we start ourselves (never the public
API). It is cheap (0.7 GB, 20 ms a sentence, a 12-second run for the whole of both bundles) and it is useful in one narrow way: it is an
independent second opinion on agreement and spelling in our Spanish. On the 530 distinct Spanish strings of both bundles it found **no error in
any authored NPC line, prompt, phrase or accepted answer**, and it found three small real things: an accent missing on an accepted answer
(`si quiero`), two example learner errors in the core's sample bundle that carry no error tag, and a style nit (`...`). It caught 11 of the 15 deliberate
learner errors and missed 4. Its raw output is 86 % noise (a casing rule that fires on every lowercase learner-side string), which two settings
remove. Its real value is as a regression net for template rendering (it flagged `de el` where Spanish needs `del`, the pattern one of our own
templates would produce for a singular masculine noun) and as the second oracle M22 wanted. Because the output is small and deterministic
for a pinned version, record it in a committed cache so that the merge gate never needs the server. Recommended shape: a `vocaterra-content grammar`
command that is a plain HTTP client (core milestone **M30**, size S), plus a small game milestone **S23** (size S) for the game's own strings.
The check is advisory first, required after one round of waivers, as the lint was.

## 1. Method

- **Server.** LanguageTool **6.6** (the current `stable` zip, built 2025-03-27, LGPL-2.1 [2][3]) with Temurin JRE **21.0.13+9** [4], both installed
  user-local under `~/.local/share/vocaterra/tools/` (outside the repository), started exactly as the owner specified: `systemd-run --user --scope
  -p MemoryMax=2G -p MemorySwapMax=0 -p CPUQuota=200% nice -n 10 java -Xmx1200m -cp languagetool-server.jar org.languagetool.server.HTTPServer --port 8081`.
  No n-gram data, no Docker, no `scripts/limited.sh`, **no dotnet command of any kind**. The public API was **not used**; no content text left the
  machine. Archive hashes (sha256): `jre.tar.gz` `a535a1df...47351dd`, `LanguageTool-stable.zip` `53600506...d11631`.
- **Corpus.** A Python script (`python3 -I`, reading the JSON and `PHRASES.md` directly) extracted every authored sentence (section 2.1), 1 344
  strings, 736 distinct, of which 530 are Spanish. Learner-side strings (answer aliases, intent examples) are stored lowercase because the core normalises
  ASR text, so each was checked twice: as stored (`es-picky`) and with its first letter upper-cased (`es-cap`). Parameters: `language=es`,
  `level=picky`, `motherTongue=en`, one request per sentence. English strings were checked with `en-US`, `picky`.
- **Slot templates** were rendered by a crude Python approximation of the template engine (two fillings each, one masculine and one feminine noun).
  It is **not** the core's engine, so some of the matches on rendered text are artefacts of my renderer; they are counted separately.

## 2. What was measured

### 2.1 Inventory: where the sentences live

| Source | Spanish strings (as authored) | Notes |
| --- | --- | --- |
| `game-v1/dialogue/dialogue.json` | 115 plain lines + 60 template renderings (from 30 lines with slots) | agent files (`agents/*.json`) hold no text, only pool ids |
| `game-v1/prompts/prompts.json` | 14 | |
| `game-v1/answer-sets/*.json` | 43 canonical + 221 aliases | learner side, lowercase |
| `game-v1/intents/intents.json` | 99 examples + 12 carrying learner errors | 8 slot-grammar patterns are patterns, not sentences: not checked |
| `game-v1/game/phrases.json` and `PHRASES.md` | 21 + 53 variants (and the sheet repeats them, plus 14 learner-error cells) | the sheet and the JSON are the same set |
| `game-v1/game/english.json` `lines[]`, `checks.json` | 104 + 11 | Spanish and English sides |
| `game-v1/game/lessons.json` | 0 Spanish (all English; the Spanish is in `lines`/phrases by id) | |
| `content/v1` (sample bundle) | 28 lines + 53 renderings, 18 prompts, 19 + 46 answers, 57 examples | |
| **Total** | **1 344 strings from all sources including English, 530 distinct Spanish** (419 plain, 111 rendered) | `game/scene.json` has no learner text |

The English support text (`english.json`, `lessons.json`, `checks.json`, `phrases.json`, `PHRASES.md`): 206 distinct strings.

### 2.2 The server

| Item | Measured |
| --- | --- |
| Start to first `/v2/languages` answer | 2 s; the first Spanish check takes 2.9 s (tagger load), then 20 to 45 ms |
| Memory | 744 MB resident, cgroup peak 710 MiB of the 2 GiB cap; 44 threads; idle CPU nil |
| Latency, 530 Spanish strings, one request each | median 19 ms, p95 36 ms, max 84 ms, total 11.2 s; 200 sequential 2.6 s; one 4 KB request holding 200 lines 0.3 s |
| Disk | 857 MB: LanguageTool 406 MB, JRE 160 MB, the two archives 291 MB (deletable) |
| Languages | `es`, `es-ES`, `es-AR` (voseo) only; **there is no `es-MX`** and the other Latin American variants are not offered. `es` vs `es-AR` gave identical counts on our corpus |

`POST /v2/check` takes `text` (or `data`, annotated text), `language`, `level` (`default` or `picky`), `motherTongue`, `enabledRules`,
`disabledRules`, `enabledCategories`, `disabledCategories`, `enabledOnly`; I confirmed `disabledRules`, `disabledCategories` and `enabledOnly` with
`enabledCategories` change the result as documented [1][5]. A match is `{message, replacements[{value}], offset, length, rule{id, subId,
category{id}, issueType}}`, with offsets in the original text. `language=auto` mistakes short Spanish for Catalan: always pass the language. `picky`
adds two matches over `default` on our corpus (270 against 272) and is cheap, so use it. Annotated text (`data={"annotation":[{"text":...},
{"markup":"{noun}","interpretAs":"casa"}]}`) works and keeps offsets in the original: it is a second way to handle a slot (section 4).

### 2.3 Findings over the Spanish corpus (530 distinct strings, 272 raw matches)

| Class | Count | Detail |
| --- | --- | --- |
| False positive: `UPPERCASE_SENTENCE_START` on lowercase learner-side strings | **234 of 272 (86 %)** | all on aliases, canonicals, examples, variants; none on an NPC line, prompt or phrase. Removed by capitalising the first letter before the call (3 left, caused by my capitalising past a leading `¿`). |
| Deliberate learner errors caught | **11 of 15** distinct tagged strings | table 2.4 |
| Deliberate learner errors missed | **4 of 15** | table 2.4 |
| True positives in authored content (untagged) | **3 strings** | list in section 5 |
| Artefacts of my slot renderer | 24 matches (`tres casa`, `la station`, `la spell`, `Name`, `el ríos`) | a real renderer fills these correctly; two of the 24 (`...`) are genuine, see section 5 |
| Fragments, short answers (`Sí.`, `Ayuda.`, `No sé.`), A1 simplifications, slot-less NPC lines | **0 false positives** | the picky level is quiet on short strings; `Sí.`, `Hasta luego.`, `A ver, miro.` all pass |

So after the capitalising fix the run is clean: **41 matches in 40 strings, all explained.** Spanish authored content has no wrong sentence that
LanguageTool can see. That is worth stating plainly: it is a confirmation, not a source of work. I also read about 200 authored non-template NPC
lines, prompts and phrases myself for what LanguageTool cannot see: nothing wrong at A1. One variety note: the core's sample bundle uses `Vale`
in 5 lines; the game's ADR says `vale` is Spain-only, and LanguageTool does not flag it (it knows no register).

**English support text** (secondary, `en-US`): 95 matches in 47 of 206 strings, 90 of them `MORFOLOGIK_RULE_EN_US` on the Spanish words and
respellings inside quotes (`Sí`, `OH-lah`). Excluding those: `traveller` in `checks.json:116` (en-GB spelling in en-US text, real, trivial), an
Oxford-comma suggestion, two `THANKS_SENT_END_COMMA` nits and one `THE_SENT_END` false positive. To be usable the English pass must mask the
quoted Spanish with annotated-text markup. Low value; keep it optional.

### 2.4 Agreement with our own error tags (M22's oracle idea)

The 15 distinct example learner errors (intent examples with `errors`, and the learner-error cells of `PHRASES.md`), as the `es-cap` run saw them:

| Example | Our tag(s) | LanguageTool rule, span, suggestion | Same span? |
| --- | --- | --- | --- |
| buenos día | noun.number | `AGREEMENT_ADJ_NOUN` "buenos día" -> buenos días | yes |
| sí, es un madera / no, es un madera | gender.agree | `AGREEMENT_DET_NOUN` "un madera" -> una madera | yes |
| mucho gracias / no, mucho gracias | gender.agree, noun.number | `AGREEMENT_DET_NOUN` "mucho gracias" -> muchas gracias | yes (both tags in one match) |
| yo quieres agua, por favor | conj.person | `AGREEMENT_PRONOUNSUBJECT_VERB` "quieres" -> quiero | yes |
| yo necesitas ayuda | conj.person | same rule, "necesitas" -> necesito | yes |
| quiero dos agua, por favor | noun.number | `AGREEMENT_NUMERAL_PLURAL` "agua" -> aguas | yes |
| otro vez, por favor | gender.agree | `AGREEMENT_DET_NOUN` "otro vez" -> otra vez | yes |
| más despasio, por favor | vocab.unknown | `MORFOLOGIK_RULE_ES` "despasio" -> despacio | yes |
| no sabo | conj.irregular | `MORFOLOGIK_RULE_ES` "sabo", suggestions cabo/sabe | span yes, **wrong correction** (ours: sé) |
| sí, por favo | vocab.unknown | no match (`favo` is a word) | missed |
| yo nos vemos | conj.person | no match | missed |
| dónde está la río | gender.agree | no match | missed |
| cuál es esto | vocab.wrong | no match | missed |

So on the shared taxonomy LanguageTool agrees on span for 11 of 15, and in the 9 grammar cases it names the same word as our tag. It adds nothing our
checks miss here; its three useful roles are (a) confirming our `expected` forms are what a second system suggests, (b) flagging an untagged example
that is an error, and (c) a **rule-to-tag map** (`AGREEMENT_PRONOUNSUBJECT_VERB` -> `conj.person`; `AGREEMENT_NUMERAL_PLURAL` -> `noun.number`;
`AGREEMENT_DET_NOUN`/`ADJ_NOUN` -> `gender.agree` or `noun.number`, ambiguous by design; `MORFOLOGIK_RULE_ES` -> `vocab.unknown` or `conj.irregular`).
As a replication of the scaling report's 20-sentence experiment [6], LanguageTool 6.6 flags 10 of 20 (7 by grammar rule, 3 by spelling only),
against the report's 9 on a 6.9 snapshot; it still misses `tú habla`, `las casa`, `un espada`, ser/estar, tense and word order.

## 3. Constraints that shape the design

- **Licence.** LanguageTool and its Spanish dictionary are LGPL-2.1 [2][3]. D10's recommendation (allow LGPL/GPL only as a separate process at
  development time, never linked or shipped) fits exactly; D10 is still marked open in `docs/nlp/DECISIONS.md`, and M22 already assumes it. A C#
  HTTP client has no LanguageTool code in it, so `scripts/license-scan.sh` (NuGet only, copyleft refused) stays clean and needs no new package
  (`System.Net.Http`). The JRE is GPLv2 with Classpath Exception [4]: a tool we download, not ship. Add both to a `NOTICE` development-tools note.
- **Determinism (plan section 3, D9).** Nothing in the evaluation path may depend on it. A network oracle at gate time would break reproducibility,
  so the oracle output is recorded and replayed, which is D9's own pattern ("model outputs are recorded and replayed").
- **Boundary (ADR 0009).** The core never plans game content or knows game files; `phrases.json`, `lessons.json`, `checks.json`, `english.json` are
  the game's. The core command must work from a bundle, with a generic string-list input for what the game owns.
- **D8 and the public API.** The public API [7] allows about 20 requests a minute and 20 KB a request (about 11 minutes for this corpus, and the content
  would be sent to a third party). Not recommended; offer it only behind an explicit flag and the owner's say-so.

## 4. Integration design

**Where it lives.** `vocaterra-content grammar <bundleDir> [--server http://localhost:8081] [--strings extra.jsonl] [--cache path]
[--waivers path] [--offline] [--json] [--english]` in `tools/Vocaterra.Nlp.ContentBuilder`, an HTTP client only, beside `lint` and `cefr-check`. A
script beside `content.sh` would have to re-implement bundle reading and the template engine; the core command gets both for free and joins
`scripts/content.sh` (add `grammar` to its case list). Game-owned strings enter through `--strings`, a JSONL list `{id, file, lang, text, tags[],
template?}` produced by a small game script (`scripts/grammar-strings.sh`, S23); the core never learns the game's file shapes.

**Which strings.** From the bundle: dialogue variants, prompt text, answer-set canonicals and aliases, intent examples (including those with
`errors`). From `--strings`: phrase rows and variants, Spanish `english.json` lines, `checks.json` Spanish, `PHRASES.md`. Learner-side strings
(aliases, examples, variants) get the first letter capitalised past any `¿¡`, which kills the 234 casing matches; set the rule's disable only as a
backstop. English is off unless `--english`, with quoted Spanish masked as markup.

**Slot templates.** Render with the core's own `TemplateEngine` (it already implements `article`, `plural-if`, `words`, `conj`, `cap`), against
sampled fillings: for each slot kind, a masculine and a feminine singular noun and a plural, drawn from the bundle's lexicon in a fixed order (no
RNG, no clock). The cache key is the rendered text, and the finding names the template line id plus the filling. This is better than my crude
renderer and better than annotated-text `interpretAs` for slots (which needs the same fillings anyway, though it is the right tool for a
host-supplied value the core cannot enumerate). A match on a rendering is a template or engine bug and is the check's best product: LanguageTool's
`CONTRACCIONES` rule flags `sale de el árbol` -> `del`, and `guide.find.source.1` (`... sale de {source:article(def)...}`) would render exactly that
for a singular masculine source (today the game only supplies plural sources, which is why no test sees it). **Untested against the real engine**
(no dotnet was allowed): confirm in M30.

**Output.** One finding per line, ordinal-sorted, like the lint: `severity check path [ids] message`, with check id `grammar.<RULE>`, severity
`warning` for agreement and spelling matches and `info` for style matches, then `grammar: N warnings, N info, N waived, N unchecked`; `--json` as in
the lint. Finding text carries the span and the first suggestion.

**Waivers.** `grammar-waivers.json` beside the cache (not inside the bundle, so the bundle hash is untouched):
`{"rule": "PUNTOS_SUSPENSIVOS", "id": "sell_item_reply.2", "reason": "..."}` or keyed by text hash for strings with no id. A reason is required;
a waiver that matches nothing is reported as stale. A waived finding still prints, as the lint does.

**Deliberate learner errors.** Keep them in the check and treat them as an extra oracle with three outcomes: example has tags and LanguageTool flags
the same span (agreement, counted); example has tags and LanguageTool is silent (info, "blind spot", expected, never fails); example has **no** tags
and LanguageTool flags it (warning: either add the expectation, as M27a2 already plans for `yo es Rosa`, or fix the example). The rule-to-tag map
lives in a small data file, not in code. A later extension feeds the analyzer's own corrected forms (recasts, O4 corrections) back to LanguageTool;
they should be clean.

**Determinism and the cache.** Pin the version (`6.6`) and the settings (`language`, `level`, `motherTongue`, disabled rules). Cache every result in
`<dir>/grammar-cache.json`, keyed `sha256(ltVersion + "\n" + settings + "\n" + text)`, value the matches (rule, offset, length, first three
replacements). Committed like a golden; `--offline` reads only the cache and reports a miss as `unchecked`. A run that adds new strings needs the
server once; the gate never does. One repeated call gave identical results, but byte-identical output across LanguageTool versions is not
promised, hence the version in the key (**unverified** across versions, **est.** low risk on a pinned version).

**Gate.** Phase 1 (advisory): `scripts/merge-gate.sh` runs `grammar --offline` after the existing content gate, prints the counts, never fails.
Phase 2 (required) after one round of waivers on both bundles: fail on a non-waived `warning` and on a stale cache entry for a changed string
("re-run with the server"). No server running means: online mode prints `grammar: skipped (no server at URL)` and exits 3 (not 0, so a script
cannot confuse it with a pass; the gate only calls `--offline`). This is the same maturity path the plan gave the lint ("not a hard merge gate
until one round of waivers has been done").

**The D-game-12 chain.** Run it **before the Opus pass**: lint, `cefr-check`, **grammar**, then Opus. LanguageTool removes mechanical errors for
free, so the Opus pass spends its tokens on naturalness, register and meaning; hand Opus the grammar findings and waivers as context. It is
supplementary like the lint: a clean run is never approval, and `PHRASES.md`'s `reviewer` column does not change (a native speaker is still the
gap). It can also guard the Opus pass: an edit Opus suggests is re-checked.

**Setup for an agent.** `scripts/fetch-tools.sh` (like `fetch-models.sh`) driven by a new `tools/manifest.json` that pins the Temurin JRE and the
`LanguageTool-6.6.zip` by versioned URL and sha256 (the two hashes in section 1; the JRE URL I used was the `latest` redirect, so M30 pins the
versioned release URL), extracting to `${VOCATERRA_TOOLS_DIR:-~/.local/share/vocaterra/tools}`. `scripts/languagetool.sh start|stop|status` starts the server
bound to localhost on a free port with `systemd-run --user --scope -p MemoryMax=2G -p MemorySwapMax=0 -p CPUQuota=200% nice -n 10` and `-Xmx1200m`
when `systemd-run` exists (plain `nice` and `-Xmx` otherwise), and does not use `limited.sh`: the server is idle between runs and only a bulk run
costs CPU (about 12 s for both bundles), so wrap the *client* run in `limited.sh` if the owner wants. Drop `--allow-origin` (not needed by a CLI client).
Never fetch n-gram data: it enables confusion-pair rules (`a ver`/`haber`) but is many GB and out of scope.

## 5. Content fixes found (not applied)

1. `game-v1/answer-sets/yes-no.json` (`yes-no.yes.alias6`) and `lesson-yes-no.json` (`lesson-yes-no.yes.alias6`): `si quiero` -> rule `SI_AFIRMACION`,
   span `si`, suggestion `Sí`. The accepted answer is missing its accent. Decide: add `sí quiero` (the normaliser may fold both, check) or waive
   with the reason "unaccented typing".
2. `content/v1/intents/intents.json` `confirm.ex5` `yo estás de acuerdo` and `introduce_self.ex4` `yo es Rosa` (`AGREEMENT_PRONOUNSUBJECT_VERB`),
   and `thank.ex4` `mucho gracias` (`AGREEMENT_DET_NOUN`): untagged examples that are learner errors. Already named in M27a2; add the expectations
   (`conj.person`, `conj.person`, `gender.agree`+`noun.number`).
3. `content/v1/dialogue/dialogue.json` `sell_item_reply.2`: `...` (`PUNTOS_SUSPENSIVOS`, suggests `…`). Style only; waive or change to `…`.
   Check that the speech/TTS path treats both alike.
4. `game-v1/game/checks.json:116`: `traveller` -> `traveler` in en-US text (the game's English is US everywhere else).
5. Template, not text, **to verify**: `game-v1/dialogue/dialogue.json` `guide.find.source.1` renders `de el` for a singular masculine source;
   the core's template engine has no `de`+`el` contraction (no `del` in `src` templating). No current caller triggers it; either add a
   contraction modifier (a core item) or keep the sources plural. M30 will confirm by rendering with the real engine.
6. Style notes the check cannot make: the sample bundle's `Vale` (5 lines) is Spain-only; fix only if the sample is meant to follow ADR 0005.

## 6. Milestone proposal

**M30: Content grammar check (LanguageTool as a development-time oracle).** Size **S**, tier **Haiku 5.5** (Sonnet only if the template-rendering
sampler turns out to be fiddly). Depends on M27a (done) and D10. Replaces the LanguageTool half of M22; M22 keeps the tagger and the
COWS-L2H sample.
- Scope: `vocaterra-content grammar` as in section 4 (`LanguageToolClient` in ContentBuilder, bundle string extraction, `TemplateEngine`
  sampling, `--strings` JSONL input, cache, waivers, rule-to-tag map, lint-style and `--json` output, exit codes 0/1/2/3); `scripts/fetch-tools.sh`,
  `tools/manifest.json`, `scripts/languagetool.sh`; docs (`docs/nlp/TOOLS.md` section, a `NOTICE` development-tools line, `CONTRIBUTING.md` step);
  run it on `content/v1`, commit `grammar-cache.json` and the waivers with a reason each.
- Deliverables: the command with unit tests that use a **fake HTTP server** (a canned response; the tests never need Java, so `dotnet test` stays
  unchanged); the cache and waivers for the sample bundle; the item-2 and item-3 fixes via the bundle's re-hash procedure.
- Gate: the normal merge gate; plus `grammar --offline content/v1` clean (advisory in the gate script); `license-scan.sh` unchanged (no new package).
  Not a hard gate until one waiver round is done.

**S23: Grammar check on the game bundle** (game plan, after M30, size **S**, Haiku). `scripts/grammar-strings.sh` (the phrase sheet, `phrases.json`,
`english.json` Spanish lines, `checks.json`), run `grammar` over `game-v1`, apply item 1 and 4, commit cache and waivers, add the advisory step to
the content gate in `scripts/merge-gate.sh` and to the D-game-12 checklist (before the Opus pass). Gate: `grammar --offline content/game-v1
--strings ...` clean on `feature/prototype`.

## 7. Open decisions for the owner

1. **Advisory or required?** Recommendation: advisory now, required after one waiver round (needs D10 accepted for the LGPL process either way).
2. **Local server or public API?** Recommendation: local only. The public API sends our content to a third party (D8 "online or offline" is still
   open) and is rate-limited; local costs 857 MB (595 MB after deleting the archives) and a JRE.
3. **D10** (LGPL as a separate dev-time process): needs the owner's yes before M30 is scheduled; it is already on M22's waiting list.
4. **English support text:** check it (`--english`, masked) or leave it out? Recommendation: leave it out until a second English pass is wanted.
5. **Latin American variety:** LanguageTool has `es`, `es-ES`, `es-AR` only. Accept `es` as the proxy for neutral Spanish (the recommendation), or
   skip `es-AR` voseo-specific noise if it appears later.
6. **Should the cache be committed** (recommended; small) or regenerated per run (needs the server in CI)?
7. **Tooling cost:** wrap bulk client runs in `limited.sh` (machine-wide lock) or not (recommended: not; the server is capped by cgroup).

## 8. Sources

1. LanguageTool HTTP server: https://dev.languagetool.org/http-server
2. LanguageTool repository and licence (LGPL-2.1): https://github.com/languagetool-org/languagetool
3. LanguageTool download (`LanguageTool-stable.zip`, here 6.6): https://languagetool.org/download/
4. Eclipse Temurin (Adoptium) 21 JRE, GPLv2 with Classpath Exception: https://adoptium.net/temurin/releases/
5. LanguageTool HTTP API (`/v2/check` parameters, annotated text): https://languagetool.org/http-api/
6. `docs/nlp/research/SCALING_APPROACHES.md` section 3.2 (the 9-of-20 experiment) and its sources 24 to 28.
7. LanguageTool public API and its limits: https://dev.languagetool.org/public-http-api
8. Repository: `docs/nlp/DECISIONS.md` D8 to D10; `docs/nlp/IMPLEMENTATION_PLAN.md` M22, M27a, M27a2, section 3; `docs/game/DECISIONS.md` D-game-10, 12, 13;
   `docs/nlp/TOOLS.md` (lint); `scripts/license-scan.sh`; `scripts/content.sh`; `scripts/merge-gate.sh`.
