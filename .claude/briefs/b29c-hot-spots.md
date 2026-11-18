You are implementing B29c, "The hot spots and the polling hook", for the Vocaterra repository (C#, .NET 10). It changes tests that read
the save fixture, adds a script and a Claude Code hook; it changes no game or core behaviour.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b29c (an absolute path; cd there first), already on the local
branch `B29c-hot-spots`, cut from `B29a-throughput` at BASE `8e372d3e` (B29a, not yet gated, runs beside you: tests now build in Release
by default; read `scripts/test.sh`'s header). Commit after each item that builds and passes its tests, with the prefix `B29c: `. Do not
push. Never push or merge feature/meta_improvements, B29a-throughput or main. Leave the worktree clean on its branch. Another agent (B29b)
works beside you on wall-clock asserts in tests; do not touch timing tests, `ArchitectureRulesTests` or the `Settle` code.
Resources, waiting, environment and the sandbox's word filter: exactly as in .claude/briefs/b29a-throughput.md (in the worktree
/home/james/Dev/Vocaterra/.claude/worktrees/meta-improvements), its paragraphs "Resources" and "Environment"; scratch under
/home/james/Dev/Vocaterra/artifacts/tmp/b29c/. Up to three agents share the machine: one heavy command at a time, never raise a cap.

Why: the throughput review docs/research/2026-11-18-throughput-review.md, section 4, items 6 and 7, and the study
docs/research/2026-11-17-ai-development-throughput.md, item 6 (line about 172): 16 of 48 agents met a merge conflict, each costing a merge
and a full run; the save fixture's pinned bundle hash and `manifest.json` are two of the hot spots. The waiting rule cut polling from 35% to
8% of Bash calls; a hook removes the rest. Your row: docs/game/implementation-plan.md section 4.14, B29b (its second part, split out as
B29c by the orchestrator on 2026-11-18).

Read first: CLAUDE.md (the content bundle bullet: the hash step, the fixture); content/README.md ("The edit loop");
`scripts/content-loop.sh` (what it already does for the fixture's hash; do not duplicate it); the tests that read `bundleHash`
(tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json and its readers: search `bundleHash` and `save-v9` in tests/Vocaterra.Game.Core.Tests
and tests/Vocaterra.Host.Tests; leave tests/Vocaterra.Nlp.Fuzzing.Tests alone unless it reads the fixture). For the hook: the Claude Code
hooks documentation (PreToolUse on the Bash tool; a command hook reads the tool input as JSON on stdin and blocks with exit code 2 and a
reason on stderr).

Rules: the fixture's `stateDigest` stays pinned and unchanged; only the bundle hash stops being pinned. Never weaken a test's meaning. Do not
edit CLAUDE.md, `.claude/agents/`, scripts/limited.sh, scripts/merge-gate.sh, scripts/test.sh, `.github/workflows/`, either
implementation-plan.md or either decisions.md (put the replacement text in your report).

Deliverables:
1. **The save fixture stops pinning the bundle hash.** Each test that reads the fixture's `bundleHash` compares it with the hash in
   `content/game-v1/manifest.json` read at test time (or, if the value only identifies the bundle, ignores it with a one-line comment
   saying why). The fixture may keep a placeholder value; say which. Show it: change one content file's text in a scratch copy (or a
   throwaway commit you revert), re-hash, and the fixture tests still pass without editing the fixture. Say how many files a content edit
   now touches (before: the content, `manifest.json`, the fixture; and whatever else).
2. **A re-hash script**, `scripts/rehash-bundle.sh`: runs the content hash for `content/game-v1` with `--bundle-id game-v1`, then
   `scripts/content.sh validate`, through `scripts/limited.sh` (or say why not if `content.sh` already takes a slot), and prints the new
   bundle hash. Name it so a command line calling it does not contain the sandbox's refused word (the file name `rehash-bundle.sh` is fine
   only if the sandbox accepts it: try it; otherwise pick another name, for example `scripts/refresh-bundle.sh`). If `content-loop.sh` should
   call it instead of its own steps, make that change only if it is a few lines and its behaviour is unchanged. A conflict in
   `manifest.json` is then resolved by taking either side and running the script: say so in the script's header.
3. **A PreToolUse hook that enforces the waiting rule**: a project `.claude/settings.json` (none exists yet; create it, with only this
   hook) and a hook script `scripts/hooks/no-polling.sh` (or Python 3, standard library only) that blocks a Bash command containing a
   `sleep` longer than 30 seconds, an `until`/`while` loop that calls `sleep`, or `nohup ... &`, with a message pointing at the rule
   (run long commands with `run_in_background: true` and wait for the notification). Short sleeps (30 s or less) outside loops pass. A
   small test: a script under `scripts/tests/` that feeds sample JSON inputs (10 or so: allowed and blocked, including `sleep 5`,
   `sleep 600`, `until grep -q done log; do sleep 10; done`, `while true; do sleep 1; done`, `nohup dotnet test &`, a heredoc that merely
   mentions sleep in text) and checks the exit codes; run it and give the result. Check the hook does not block the repository's own scripts'
   ordinary commands (it inspects only the Bash tool's command line, not the scripts it runs).
4. **Documents:** content/README.md's edit loop (the fixture no longer needs the new hash; the script), docs/process/ only if a page names
   the fixture's hash step. In the report, give the exact replacement for CLAUDE.md's sentence beginning "After `hash`, run the whole suite"
   through "No core test pins the hash" and for the brief template (docs/process/milestone-briefs.md) if it names the step.

Gate (from your worktree root): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings);
scripts/test.sh full passes (give the count); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean; scripts/limited.sh
scripts/content.sh validate passes; the hook's test script passes. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDc2yqvjCy1ntkujnVS
Report, half a page: branch, head commit, worktree; the fixture change and its proof; the script and its name; the hook, its test's result
and its rules; files a content edit now touches; the replacement texts; test counts; anything left. No file dumps or long logs.
