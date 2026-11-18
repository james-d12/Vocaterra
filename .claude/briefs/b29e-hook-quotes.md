You are implementing B29e, "The polling hook lets quoted text pass", for the Vocaterra repository. It changes the Claude Code hook script
`scripts/hooks/no-polling.py` and its test `scripts/tests/no-polling-test.py` only.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b29e (an absolute path; cd there first), already on the local
branch `B29e-hook-quotes`, cut from `feature/meta_improvements` at BASE `9f2f3a31`. Commit with the prefix `B29e: `. Do not push. Never
push or merge feature/meta_improvements or main. Leave the worktree clean on its branch. Another agent (B29d) works beside you on tests.
No dotnet command is needed. Python 3 standard library only; run scripts with `python3 -I`. Never poll with `sleep` or loops.

Why: the owner's decision D-game-34 item 60 (docs/game/decisions.md); your row docs/game/implementation-plan.md section 4.14, B29e. The
hook (B29c, .claude/briefs/b29c-report.md) blocks a Bash command with a `sleep` over 30 s, an `until`/`while` loop that sleeps, or
`nohup … &`. It also blocks a command that only quotes such text, for example `git commit -m "no sleep 600 here"` or
`echo 'while true; do sleep 1; done'`: a false positive.
Read first: `scripts/hooks/no-polling.py`, `scripts/tests/no-polling-test.py`, `.claude/settings.json`.

Deliverables:
1. The hook ignores text inside quotes (single quotes, double quotes, and `$'…'`) and in comments, as it already ignores heredoc bodies,
   using `shlex` or a small tokenizer; a command it cannot tokenize (unbalanced quotes) falls back to today's check on the raw text. Real
   sleeps and loops still block, including `bash -c "sleep 600"` and `sh -c '…sleep…'` (the string is a command: check the argument of
   `bash -c`, `sh -c` and `timeout … bash -c`).
2. The test gains at least 8 cases: the two above pass; a multi-line commit message with "sleep 600" in a heredoc passes; `bash -c "sleep 600"`
   blocks; `timeout 900 bash -c 'until x; do sleep 5; done'` blocks; `echo "x" && sleep 45` blocks; `grep -n "sleep" file` passes;
   unbalanced quotes with `sleep 600` blocks. All old cases keep their expected results. Run it and give the result.
3. The hook's header names the rule for quoted text.

Gate: `python3 -I scripts/tests/no-polling-test.py` passes every case. Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDc2yqvjCy1ntkujnVS
Report, a few lines: branch, head commit; the approach; the case count and result; any case it still gets wrong.
