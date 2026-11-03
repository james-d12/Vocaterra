# Content and the grammar check

The game's content bundle, `content/game-v1`, its gate and the grammar check of its Spanish. How to edit the content, step by step, is in
[content/README.md](../../content/README.md).

## Content

`content/game-v1` is the game's bundle, under the bundle id `game-v1`. **The game owns it** (decision D-game-4, decided 2026-10-21: the bundle
diverges from the core's sample and is not re-synced). The core's former sample bundle (`content/v1`, a blacksmith with spells and items; deleted in R02, 2026-10-23, and kept in the git history)
was the starting point: the generic intents (`confirm`, `deny`, `farewell`, `thank`), their dialogue pools and the first feedback pools were
copied from it, reworded where they failed the game's A1 check, and stay credited to the sample (Apache-2.0) in `ATTRIBUTION.md`. Everything
added since is the game's own, hand-authored: the guide (agent `guide`, intents `ask_directions`, `ask_what` and `ask_help`, prompt
`guide-open`, the reply pools and the host-line pools the game renders itself), `answer-sets/places-basic.json` and `lexicon/world.json`, and
since S5 the prototype's stall and village: the agents `shopkeeper` and `villager`, the intents `request_item` and `dont_know`, the answer set
`supplies`, the prompts `stall-need`, `stall-offer`, `villager-open` and `guide-offer`; and since phase A of the village plan (V10 to V12) the
agents `fisher`, `baker`, `smith` and `farmer`, the intents `ask_far`, `ask_availability`, `ask_price`, `ask_name` and `ask_activity`, the answer sets
`places-village`, `supplies-bakery`, `people-smith` and `people-farmer`, and the prompts `fisher-open`, `baker-open`, `smith-open` and `farmer-open`
(with each lesson's `lesson-*` prompts and answer sets). The blacksmith and the spells left the bundle in S5 (they went with the core's former sample). **The phrase sheet [content/PHRASES.md](../../content/PHRASES.md) is the source of truth for the Spanish the learner is
taught** (decision D-game-10): every row is checked against the bundle by `tests/Vocaterra.Host.Tests/PhraseSheetTests.cs`. Since R02 this is the repository's only bundle: the
core's tests, its fuzz suite and its demo CLI run on it, so an edit of the game's content re-runs the core suite, and a core test that fails is the signal that the
edit broke something the core relies on (`docs/nlp/CONTRIBUTING.md`). The bundle has its own hash, which the loader
checks against `manifest.json`. After any edit, rewrite the manifest:

```bash
scripts/content.sh validate                                  # every file's hash and the cross-references
scripts/content.sh hash content/game-v1 --bundle-id game-v1  # rewrite manifest.json (keep the bundle id once it is set)
scripts/content.sh cefr-check content/game-v1 --level a2     # the bundle's ceiling: nothing listed (--level a1 is a report of what is above A1)
scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'   # 0 errors, 0 warnings (the guide's host-voiced pools are named)
scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --offline   # the grammar check from the committed cache, no server
```

**The grammar check (S23, core M30, D10).** The review chain is lint, `cefr-check`, grammar, then the Opus pass (D-game-12). The grammar step
sends every Spanish string the player hears or says to a LanguageTool server on this machine (never the public API) and reads the answers back
through a committed cache. It covers the bundle's dialogue, prompts, answer sets and intents, the phrase sheet (the phrases, the accepted variants
and the learner-error examples, with their tags), the Spanish side of `game/checks.json`, and the speech index (the support language, in `content/locale` since C03, is not checked). The
strings are written by the host from those sources, so the list is always the game's own:

```bash
dotnet run --project src/Vocaterra.Host -- --content content/game-v1 --grammar-strings content/grammar/game-v1-strings.jsonl
```

The committed files are `content/grammar/game-v1-strings.jsonl` (the strings), `game-v1-cache.json` (one result per string, keyed by the
LanguageTool version, the settings and the text) and `game-v1-waivers.json` (each accepted match, by rule and string id, with its reason; a
waiver with no reason is refused). Findings are warnings (agreement, spelling, grammar), info (style, and the learner-error oracle's facts) or
errors (a string with no cached result). The English support text is not checked (the owner's decision).

**Refreshing the cache after a content change.** `scripts/content-loop.sh --grammar` (V18) runs steps 1 to 5 and the check CI's `languagetool`
job makes (below) in one command, prints every new finding and stops the server even on a failure; it is the edit loop of
[content/README.md](../../content/README.md) ("The edit loop"), and the way to refresh the cache. Step 4 is still yours: read each finding it
prints. By hand, from the repository root:

1. Write the strings again (the command above). The strings file is checked by a test, so a stale file fails `dotnet test`.
2. Start the server: `scripts/languagetool.sh start` (it refuses below 4 GB free; `scripts/languagetool.sh status` shows it).
3. Check the new strings and prune the entries no string uses any more:
   `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --prune`
4. Read every warning and every new info finding. Fix the content, or add a waiver with a reason to `game-v1-waivers.json`. Do not
   waive a true positive to get a clean run.
5. Stop the server (`scripts/languagetool.sh stop`), run the offline command above (it must exit 0), and commit the strings, the cache and the
   waivers with the content change. The gate runs the offline command only and never needs the server.

CI runs the check twice, as two parallel jobs of `ci.yml` on every push to `main` and every pull request. The `build-and-test` job runs the
offline command. The `languagetool` job starts the pinned LanguageTool on the runner, writes the strings again, checks every string with an
empty cache, and fails when the result differs from the committed `content/grammar/` files or has an unwaived warning. On a failure, its
`grammar-refresh` artifact holds the refreshed strings and cache; review the diff and commit them, which stands in for steps 1 to 3 when no
local server is at hand.

Since S4 the catalogue holds only the nouns of the 3D world (the ores, tools and furniture of the 2D slice are gone), and the recipes are the
workbench's torch alone.

The content gate from S5 on: `lint --strict` clean (an example expectation for every phrase the learner says, in `intents/intents.json`), and
the phrase-sheet test green. Since V08 (VILLAGE_PLAN 3.9, D-game-22 (b)) the bundle's ceiling is A2: the lint runs at A2 with `--fail-on-level`,
`cefr-check` at A2 fails the gate on any lemma it lists, and `cefr-check` at A1 is a report for the owner that never fails; each lesson declares
its own `level` (`a1` or `a2`), and the host's `--validate-content` holds its Spanish and its tenses to it (`content/README.md`, "A lesson's
level"). `--host-pools 'guide.*'` tells the lint that the game voices the guide's templated world lines itself (they have no agent action);
without it those pools are reported as unreachable.

The script runs the core's content tool (`tools/Vocaterra.Nlp.ContentBuilder`, from source; see
[scripts/README.md](../../scripts/README.md)). `ATTRIBUTION.md` and `provenance.json` describe this bundle; the Wiktionary lexicon in it is CC BY-SA
and keeps its notice.
