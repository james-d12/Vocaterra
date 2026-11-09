You are resuming the core milestone **M36, the German lexicon import**, and folding in **M90, the Spanish source re-pin**, in the
Vocaterra repository (C#, .NET 10). Your full brief is
`/home/james/Dev/Vocaterra/.claude/worktrees/m36/.claude/briefs/m36-german-lexicon.md`: read it first; everything in it applies, with the
changes below. Do the work yourself; do not spawn subagents. Commit prefix `M36: ` (and `M90: ` for the re-pin commit).

## Where you start
The worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m36`, branch `M36-german-lexicon`. A previous agent was paused for machine load
and left a WIP commit `04f2f931` (unbuilt, untested); its condensed interim report is
`/home/james/Dev/Vocaterra/.claude/worktrees/m36/.claude/briefs/m36-pause-report.md` (read it: spike R4 is done, the source chosen, the
files already in `~/.cache/vocaterra/lexicon-sources/`). The orchestrator has since merged `feature/german` into your branch (`becd78c9`):
M87 is in, so the engine's pack interfaces no longer change under you, but they stay outside your edits. Build first; fix what the WIP
breaks; then continue from the pause report's "Next" line. Keep the WIP commit (do not rewrite history).

## Changes to the brief
- **Count before you:** 10418 passed, 3 skipped (re-measure if your first full run differs, and say so). The open-fallback fuzz reach
  check `PipelineFuzzTests.OpenFallback_LeavesATrustedFirstReadingAlone_…` is flaky (M89): if it alone fails, rerun it alone and say so.
- **Logs:** `/tmp/claude-1000/m36/`.
- **M90 (fold it in, its own commit):** kaikki's Spanish file changed upstream (now 1,119,672,965 bytes, SHA-256 `336903d1…`, the full
  hash is in the cached file: compute it), so `scripts/fetch-lexicon-sources.sh` refuses it. Re-pin the Spanish entry to the current file
  with its retrieval date and the dump it carries, in the script's existing comment style. **Do not regenerate**
  `content/game-v1/lexicon/`: its sidecar keeps the dump it was built from. Say in `docs/nlp/lexicon.md` that the committed Spanish
  lexicon was built from the earlier pin and that a regeneration from the new pin is a separate, deliberate step.
- **The Spanish byte-identity proof** (brief step 2): the importer before your change and after it, run on the same Spanish source file
  (the current cached one), must give identical bytes; compare those two outputs, not the committed file (which came from the older
  source). Also add the fixture test.
- Another agent (T02b, game only) builds beside you: every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it
  wait. Check `df -h /home` before each download and the full build.

Report as the brief says (one page, under 500 words), adding the M90 pin (bytes, SHA-256, date, dump).

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
