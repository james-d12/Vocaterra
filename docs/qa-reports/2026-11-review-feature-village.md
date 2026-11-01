# Review of `feature/village` (PR #20), 2026-11-01

Against the merge base `54ed22e` (`origin/main`), at `3575d46`. Read-only: nothing on the branch was changed. Two read-only reviewers (the core;
the game's logic) read the diffs; every finding below was reproduced with the CLI or the host, or confirmed in the code, by the orchestrator.

## Must fix before the merge (recommended)

**R1. Correct Spanish with a coordinated subject is marked wrong, and the NPC recasts it into wrong Spanish (core).**
- `SpanishAgreementChecker.CheckSubjectVerb` takes the pronoun nearest the verb as the subject, with no guard for `y`/`e`. Since M62,
  `PredicateSubjectOf` (step 1) does the same, which adds a second false error on a predicate adjective.
- Phase B leads the learner straight into it. In session 8 the villager models «Por la mañana, Luz y yo fuimos a la tienda.», then asks
  «¿Y Mar y tú? ¿Adónde fueron ayer?».

| Where | Line | Result |
| --- | --- | --- |
| `lesson-we-went` (host) | `mar y yo fuimos al río` | `conj.person` (expects `fui`), correctness 0.15, NPC: «¡Eso es! «Mar y yo fui al río».» |
| `lesson-we-went` (host) | `luz y yo fuimos al río`, `tú y yo fuimos al río`, `mis amigos y yo fuimos al río` | the same (`fui`) |
| `villager-open` | `tú y yo somos amigos` (Luna's own line `guide.lesson.villager.be.1`) | `conj.person` (expects `soy`) |
| `villager-open` | `él y yo somos altos`; `ella y yo estamos cansadas` | `conj.person` and, new with M62, `noun.number` (expects `alto`, `cansada`) |
| `villager-open` | `ella y él son altos`; `tú y ella sois altas` | `conj.person` (expects `es`) |

- **Age:** the person check is older than the branch (M56 added only the guard for a pronoun after a preposition); M62's copy is new; the
  lesson content that invites it is new.
- **Why the fuzz suite missed it:** the sentence generator re-rolls any coordinated subject (`skip:coordinated-subject`, M59), so O3
  never sees one.
- **Fix:** a core milestone (M64) on `feature/village`.
  - A subject pronoun joined by `y`/`e` to another phrase is a plural subject: `yo` in it gives 1pl; `tú` without `yo` gives 2pl or 3pl
    (`ustedes`), both accepted; otherwise 3pl. The conservative alternative is not to check it.
  - The same in `PredicateSubjectOf`.
  - Regression tests on the lines above.
  - Coordinated pronoun subjects in the generator (a widening).

**R2. ADR 0009 is not updated for the public surface the branch adds (boundary, docs only).**
- ADR 0009: "A change to a public type listed there is a change to the boundary and needs this record updated". M32, M34, M52 and M53 each
  added a dated note; M56 to M62 added none. `API.md` records the changes correctly:
  - `IAnalysisRules` +4 members with no default (`IsElapsedTimePreposition`, `IsElapsedTimeNoun` (M59), `IsPostnominalAdjective`,
    `IsDegreeAdverb` (M62)): breaking for any implementer;
  - `IMorphology` +2 default members (`TryGetRegularizationOfWord`, `TryGetPluralSlip`) and the public record `PluralSlip` (M58);
  - the public static class `SpanishTimeMarkers` (M57).
- One note in ADR 0009 is the fix; the boundary is unchanged in kind (no game word in the core).
- **API.md cleanup:** it lists `IsVerbSeekingQuestionWord` as added by M56, but it came with M34 (`678d9ad`) and is on `main` already.

**R3. CI.** This session cannot read PR #20's checks. If the build-and-test job is red on a timed test (handover section 4, item 3), it is
fixed here before the merge.

## Should fix (the follow-up branch; R4 is small enough to go with M64)

**R4. The listener's name after any greeting but `hola` is not taken out (game, `Vocative.cs:25`, `Greetings = ["hola"]`).**
- `Greeting.cs` knows all five greetings; `Vocative` knows only `hola`. Sessions open in the morning, so `buenos días` is the likely
  greeting.

| Prompt | Line | Result (host) |
| --- | --- | --- |
| `baker-open` | `buenos días luz quiero pan` | "No te entendí" |
| `smith-open` | `buenos días domingo vendo madera` | read as a statement about Sunday, `state_action`; no sale; recast «domingo vendo madera» |
| `baker-open` | `hola luz quiero pan` | understood, the sale |

- **Fix:** take the openings from `Greeting`, or strip the name from the rest after `Greeting.Rest`, in `NpcService.Take` and
  `Host/ConversationSession`, with tests in `VocativeTests`/`GreetingTests`.

**R5. An amount before a singular `ser` is flagged (core, M59).** `diez monedas es mucho dinero` and `dos panes es suficiente` give
`conj.person` (expects `son`). Both forms are accepted Spanish, and session 10 is about totals.

**R6. `es cerca del mediodía` gives `ser_estar` (expects `está`) (core, M56).** `IsApproximateTime` tests `de` and misses `del`.

**R7. A catalogue noun that names no item counts as asking for the NPC's own item (game, `NpcHostEvents.ItemSaidIn`).**
- The rule is `noun.Items.Count == 0`, which takes in 15 nouns: `casa`, `río`, `piedra`, `árbol` and the rest.
- `quiero casa` at the bakery buys bread («¡Eso es! «Quiero casa».» then the sale); `vendo piedra` to Domingo sells wood.
- The doc comment is written for a word the catalogue lacks. Fix: the code or the comment.

**R8. A sale or purchase with a full bag says its success line and changes nothing (game).**
- `NpcService.Miss` checks coins and goods, not room. `VoxelSession.Sell`/`Buy` then return silently.
- A lesson's `do` step waiting on the item would not advance. Rare (40 slots).

**R9. Quantities are capped at 5, though the V15 entry says "No cap: the goods are the cap" (game, `NpcHostEvents.MaxQuantity`).**
- Holding 8 wood, `vendo ocho bloques de madera` takes 5; `vendo madera` takes all 8.
- With 15 coins, `quiero quince manzanas` at León gives 5 apples for 5 coins, with the success line.
- Session 10 teaches 11 to 20. Fix: the code or the entry.

**R10. `quiero cero panes` buys one loaf (game, `NpcHostEvents.Quantity`).** The core gives `qty: "0"`, and any number of 0 or less is
read as 1.

## Risks (no action needed for the merge)

- **The fuzz suite's blind spots.** The branch added seven re-rolls to the sentence generator: `coordinated-subject`,
  `literal-verb-subject`, `colour-before-noun`, `noun-reads-as-adjective`, `ser-of-place`, `predicate-subject` and
  `time-marker-of-generated-words`. Each is documented in the fuzz README and none edits an oracle, but each is a sentence shape the
  oracles never see. The first one hid R1. Worth a look when the owner decides the three open seeds.
- **The rest of the line after a greeting or a name is handled in three places:** the typed and spoken paths in `NpcService.Take`, and
  `Host/ConversationSession`. R4 is that drift already.
- **`SessionSkip.SkipBuy` calls `Sell`,** which returns silently when it cannot buy. Only a coin order catches the shortfall, later.
- **`WorldFacts.NearestOfNounJoinedTo` floods a water body with no bound.** Fine for the village's river.
- **The timed tests on CI** (handover section 4, item 3) and **the known free-talk points** (handover section 5, item 4) stand as recorded.

## Cleanups

- `SpanishTimeMarkers` exposes arrays as `IReadOnlyList<string>`; a caller could cast them and write to them (the checks copy them into
  `FrozenSet`s, so this is theoretical).
- The R2 note on `IsVerbSeekingQuestionWord`.

## Checked and not reproduced, or accepted

- **Not reproduced:**
  - `quiero una naranja leche` was suspected of a false `word_order`; it is clean.
  - A missing `count` in an order file was suspected of throwing a raw exception; the loader's `catch` already reports it as a load error.
- **Accepted by the findings log:**
  - the listener's own name as the subject is taken out (`Luz hará pan.` said to Luz);
  - M63 (filed);
  - `¿puede usted vender pan?` gives `conj.person` (expects `vende`), from V15.

## What was checked and found clean

- **Build:** `dotnet build Vocaterra.sln -warnaserror -m:4` at `3575d46`: 0 warnings, 0 errors. Only documents changed since V09's gate
  (`da8dba6`: 7259 passed, 3 skipped, with the models), so the tests were not run again.
- **Core demo:** `yo quiere dos espada` gives exactly `conj.person` and `noun.number`.
- **99 correct learner lines** through the CLI and the host. Beyond R1, R5, R6 and the known V15 line, they are clean of grammar errors:
  - a lone name said to the listener (`luz quiero pan`, `domingo tengo madera`);
  - greetings;
  - `usted`/`ustedes`, `luz y mar compraron pan`, a subject after its verb;
  - `gustar`, `hay`, `tener que`, `ir a`, `hace dos días`/`hace frío`;
  - `ser`/`estar` (`la fiesta es en la plaza`);
  - `el agua`, `la mano`, `el día`, `el mapa`;
  - colours, numbers and totals, the preterite and the future;
  - `la mayoría de los niños comen pan`, mixed and feminine coordinations of nouns.

  The `vocab.wrong` flags at constrained prompts are the known flag.
- **Determinism and architecture:** no `System.Random`, `Guid`, clock or culture-sensitive comparison in any added line under `src/` or
  `tools/`, and no MonoGame type in `Game.Core`.
- **Fuzz oracles:** the only changes are a refactor for M57's frozen inputs and new fixture grammars. M58 (c) is the one narrowing, as
  the owner approved.
- **No `.github/` changes.**
- **Scripts:**
  - the gate's new content checks read their exit codes correctly (no `set -e`);
  - `content-loop.sh` restores the grammar cache if the rebuild fails, stops LanguageTool on exit and prunes only old log folders.
- **Content:** the 144 new dialogue lines read natural and correct. `ustedes fueron` follows the bundle's es-MX/CO/AR.
- **Documents:** the counts are true (10 lessons, 229 steps, 155 phrase rows, 902 speech files, each lesson's `level`).
- **The game reviewer read these by code; they were not run:** saves and old-save loading, the lesson runner's `after`, the
  order-completion guards, `GameDataValidator`'s new rules.
- **Not covered:**
  - the engine project's art code (`BlockArt`, `NpcFigures`) in depth;
  - the tests by eye, beyond the fuzz suite;
  - a game run (V09 played the learner's route through sessions 7 to 10).
