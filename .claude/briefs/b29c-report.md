# B29c report (agent, 2026-11-18, about 14:05 UTC)

Branch `B29c-hot-spots`, head `8ac16012`, three commits on `8e372d3e`. Gate in its worktree: Release build 0 warnings; `test.sh full` 12,631
passed, 9 skipped; format clean; validate clean; `rehash-bundle.sh` hash unchanged (`bdf4dc49…`); hook test 20 of 20.

1. **Fixture:** `TestSupport/SaveFixture.cs`, `SaveFixture.Read(name)` returns the fixture with `bundleHash` set to the current
   `TestGame.Bundle.ContentHash`; nine test files read through it; `save-v9.json` keeps 64 zeros; `stateDigest` unchanged. Proof: `day.json`
   edited and re-hashed (`bdf4…` to `f96c…`), the 9 fixture classes passed (112) with the fixture unedited; reverted. A content edit touches 2
   files (was 3).
2. **`scripts/rehash-bundle.sh`:** hash and validate through limited.sh, prints the hash; header says how to resolve a `manifest.json`
   conflict. `content-loop.sh` step 4 now only reports (more than the brief's "few lines": the old step would fail on the placeholder; not run
   end to end, `bash -n` only).
3. **Hook:** `.claude/settings.json` (only this hook) runs `python3 "$CLAUDE_PROJECT_DIR"/scripts/hooks/no-polling.py` on PreToolUse Bash;
   blocks with exit 2 a `sleep` over 30 s (any unit; unreadable argument blocks), an `until`/`while` loop whose body sleeps, `nohup … &`;
   heredoc bodies stripped; non-Bash or malformed input passes. Test `scripts/tests/no-polling-test.py`, 20 cases. Known limit: a quoted
   "sleep 600" is blocked.
4. **Docs edited:** content/README.md, scripts/README.md, docs/game/saves.md, docs/nlp/changing-the-core.md (one line, outside the brief),
   docs/process/milestone-briefs.md (the content-loop line). CLAUDE.md replacement applied by the orchestrator.
