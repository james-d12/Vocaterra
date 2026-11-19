# Brief: I-fuzz, the cuanto noun the sentence generator still draws (CI failure on PR #71)

Sent 2026-11-19 to a general-purpose agent, Sonnet 5.5 at high effort. Worktree `/home/james/Dev/Vocaterra/.claude/worktrees/i-fuzz`,
branch `I-fuzz-cuanto`, cut from `feature/inventory` at `65af156b`. A core test-tooling fix on the inventory branch, so PR #71's CI passes;
the owner asked for CI to be fixed. Main's CI passes.

```
CI on PR #71 (feature/inventory to main) fails one fuzz test: Vocaterra.Nlp.Fuzzing.Tests.HomographRerollTests
.TheCuantoPatternNoLongerRealisesANounThatReadsAsADeterminer, "seed 1892: 'sueño biblias 1 cuanto transporta por favor'". It reproduces
locally (`scripts/limited.sh dotnet test tests/Vocaterra.Nlp.Fuzzing.Tests -c Release --no-build --filter
FullyQualifiedName~HomographRerollTests` after a Release build). The pattern is `[NOUN:item] [NOUN:qty] [NUM] [NOUN:spell]
[VERB:transportar]`; the realiser drew «cuanto» for `[NOUN:spell]`, a noun that also reads as the determiner cuánto, which
`LexiconCatalog.NounExclusion` / `ReadsAsDeterminer` (tests/Vocaterra.Nlp.Fuzzing.Tests/Generation/LexiconCatalog.cs, about line 610) and the
M97 re-roll (commit 245c6149, "the realiser re-rolls a noun that reads as the determiner of the next noun") are meant to keep out.

Where: /home/james/Dev/Vocaterra/.claude/worktrees/i-fuzz, branch `I-fuzz-cuanto`. Start every command with
`cd /home/james/Dev/Vocaterra/.claude/worktrees/i-fuzz &&`; absolute paths for file tools. Commits prefixed `I-fuzz: `. Do not push.
Read CLAUDE.md, tests/Vocaterra.Nlp.Fuzzing.Tests/README.md (the oracles, "never weaken an oracle", how earlier generator fixes were made:
M73, M91, M97, M98) and docs/nlp/changing-the-core.md.

1. Find why «cuanto» passes the exclusion on this branch and not on main: compare `origin/main` (11fc1415) with this branch's content
   (content/game-v1: lexicon/world.json gained entries in I08a and I08b; game/nouns.json; cefr lists) and whatever changes the noun pool or
   the draw order. Say the cause in one paragraph: a content change that exposes a generator gap (the likeliest), or a content entry that
   is itself wrong (for example a lexicon entry that drops or shadows cuanto's determiner reading), or something else.
2. Fix the cause where it lives, never by weakening the test's oracle or skipping the seed: a generator fix in the fuzz project (the
   exclusion catches every noun spelled like a determiner, whatever comes after it) in the style of M97 and M98, with its re-roll test; or
   a content fix if an entry is wrong (then the content loop, `scripts/rehash-bundle.sh`, and the identity baseline re-recorded only if it
   moves). If the fix touches src/Vocaterra.Nlp or src/Vocaterra.Nlp.Es (the engine or the Spanish pack), stop and report instead: that
   needs a core milestone.
3. Run the whole fuzz suite and the core suites: `scripts/test.sh ci` (everything, including the fuzz suite and the timing tests, as CI
   runs it; see scripts/test.sh), and say the counts. A different fuzz failure that this change cannot have caused is reported with its seed,
   not fixed.

Gate: scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings); scripts/test.sh ci (all pass); format;
content validate if content changed. No game window. Long commands in the foreground with the Bash tool's timeout at 600000 ms, output to a
log in your scratchpad named with i-fuzz, at most 30 lines printed; never background them, never poll. A wait for a limited.sh slot is
normal.

Report, one page: the cause; the fix and its test; the counts of `test.sh ci`; files touched (say which are core test tooling). Leave the
worktree clean on your branch with no process of yours running. Commit messages: subject, blank line, body, blank line, then
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDdyChBqPxmdjJZJnkC
```
