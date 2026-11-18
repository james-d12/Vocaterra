#!/usr/bin/env python3
"""PreToolUse hook for the Bash tool: enforces the waiting rule (B29c, the throughput review's item 7).

Claude Code runs this before every Bash call with the call as JSON on stdin. It blocks (exit 2, the reason on stderr, which Claude reads)
a command line that
  - sleeps more than 30 seconds (or for an argument it cannot read as a number of seconds),
  - loops (`until` or `while` ... `do` ... `done`) around a `sleep`, or
  - starts a `nohup` job in the background (`nohup ... &`).
The rule: run a long command with run_in_background: true, end the turn and wait for its notification. Polling with sleep costs a
Bash call and tokens each time (the study's 35 percent of Bash calls, cut to 8 percent by the rule and removed by this hook).

What it inspects: the Bash tool's command line only, not the scripts that command runs (so scripts/test.sh and the gate may sleep). The
text of a heredoc body is not a command, so it is removed first: a commit message or a note that mentions sleep passes.

Standard library only. Anything it cannot read (no JSON, another tool) is allowed: a hook must not block a call it does not understand.
"""

import json
import re
import sys

MAX_SLEEP_SECONDS = 30

RULE = (
    "Blocked by the waiting rule (scripts/hooks/no-polling.py). Do not sleep for more than 30 seconds, loop on sleep, or start nohup ... &. "
    "Run a long command with run_in_background: true, end your turn and wait for its notification; never poll it."
)

HEREDOC = re.compile(r"<<-?\s*(['\"]?)([A-Za-z_][A-Za-z0-9_]*)\1")
SLEEP = re.compile(r"(?<![\w.\-])sleep\s+([^\s;&|)]+)")
SLEEP_WORD = re.compile(r"(?<![\w.\-])sleep\s")
LOOP_WORD = re.compile(r"\b(until|while)\b")
DONE_WORD = re.compile(r"\bdone\b")
DO_WORD = re.compile(r"(?<![\w.\-])do(?![\w.\-])")
NOHUP_WORD = re.compile(r"\bnohup\b")
BACKGROUND_AMP = re.compile(r"(?<![>&])&(?![&>])")
DURATION = re.compile(r"^(\d+(?:\.\d+)?)([smhd]?)$")
UNIT_SECONDS = {"": 1, "s": 1, "m": 60, "h": 3600, "d": 86400}


def strip_heredocs(command: str) -> str:
    """The command with the bodies of its heredocs removed (the `<<WORD` line itself is kept)."""
    kept = []
    end = None
    for line in command.split("\n"):
        if end is not None:
            if line.strip() == end:
                end = None
            continue
        kept.append(line)
        match = HEREDOC.search(line)
        if match:
            end = match.group(2)
    return "\n".join(kept)


def long_sleep(command: str) -> str | None:
    """A reason when a sleep in the command is longer than the limit (or cannot be read), else None."""
    for match in SLEEP.finditer(command):
        argument = match.group(1)
        duration = DURATION.match(argument)
        if duration is None:
            return f"sleep {argument}: the argument is not a number of seconds the hook can check"
        seconds = float(duration.group(1)) * UNIT_SECONDS[duration.group(2)]
        if seconds > MAX_SLEEP_SECONDS:
            return f"sleep {argument} waits longer than {MAX_SLEEP_SECONDS} seconds"
    return None


def sleep_in_loop(command: str) -> str | None:
    """A reason when a sleep sits inside an until or while loop (its body: from the `do` to the `done`), else None."""
    for loop in LOOP_WORD.finditer(command):
        rest = command[loop.end():]
        do = DO_WORD.search(rest)
        if do is None:
            continue
        after_do = rest[do.end():]
        done = DONE_WORD.search(after_do)
        body = after_do[: done.start()] if done else after_do
        if SLEEP_WORD.search(body):
            return f"a {loop.group(1)} loop calls sleep"
    return None


def background_nohup(command: str) -> str | None:
    """A reason when a nohup command is started in the background with a trailing &, else None."""
    for nohup in NOHUP_WORD.finditer(command):
        if BACKGROUND_AMP.search(command, nohup.end()):
            return "nohup ... & starts a background job"
    return None


def violation(command: str) -> str | None:
    """The first reason the command breaks the waiting rule, or None when it is allowed."""
    code = strip_heredocs(command)
    return long_sleep(code) or sleep_in_loop(code) or background_nohup(code)


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0
    if not isinstance(payload, dict) or payload.get("tool_name", "Bash") != "Bash":
        return 0
    tool_input = payload.get("tool_input")
    command = tool_input.get("command") if isinstance(tool_input, dict) else None
    if not isinstance(command, str):
        return 0
    reason = violation(command)
    if reason is None:
        return 0
    print(f"{reason}. {RULE}", file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
