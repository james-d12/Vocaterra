#!/usr/bin/env python3
"""PreToolUse hook for the Bash tool: enforces the waiting rule (B29c, the throughput review's item 7; quoted text B29e).

Claude Code runs this before every Bash call with the call as JSON on stdin. It blocks (exit 2, the reason on stderr, which Claude reads)
a command line that
  - sleeps more than 30 seconds (or for an argument it cannot read as a number of seconds),
  - loops (`until` or `while` ... `do` ... `done`) around a `sleep`, or
  - starts a `nohup` job in the background (`nohup ... &`).
The rule: run a long command with run_in_background: true, end the turn and wait for its notification. Polling with sleep costs a
Bash call and tokens each time (the study's 35 percent of Bash calls, cut to 8 percent by the rule and removed by this hook).

What it inspects: the command's own shell words, not text that is only quoted or commented. The text inside single quotes, double
quotes and $'...' strings, and the text of a # comment, is data, not a command, so it is blanked before the checks (B29e): a commit
message such as `git commit -m "no sleep 600 here"` or `echo 'while true; do sleep 1; done'` passes. The body of a heredoc is
removed for the same reason. Two exceptions keep real waits blocked: the string argument of `bash -c`, `sh -c` (and so
`timeout 900 bash -c '...'`) is a command and is checked as one, and a double-quoted string that holds `$(` or a backquote is kept as
code, since it runs. A command the hook cannot tokenize (unbalanced quotes) is checked as raw text, as before B29e.

The scripts the command runs are not inspected (scripts/test.sh and the gate may sleep).

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
PLACEHOLDER = re.compile("(\\d+)")
SLEEP = re.compile(r"(?<![\w.\-])sleep\s+([^\s;&|)]+)")
SLEEP_WORD = re.compile(r"(?<![\w.\-])sleep\s")
LOOP_WORD = re.compile(r"\b(until|while)\b")
DONE_WORD = re.compile(r"\bdone\b")
DO_WORD = re.compile(r"(?<![\w.\-])do(?![\w.\-])")
NOHUP_WORD = re.compile(r"\bnohup\b")
BACKGROUND_AMP = re.compile(r"(?<![>&])&(?![&>])")
SHELL_C = re.compile(r"(?<![\w.\-])(?:bash|sh|zsh|dash)\s+(?:-\w+\s+)*-\w*c\w*\s+(\d+(?!\w)|[^\s;&|()]+)")
DURATION = re.compile(r"^(\d+(?:\.\d+)?)([smhd]?)$")
UNIT_SECONDS = {"": 1, "s": 1, "m": 60, "h": 3600, "d": 86400}
SHELL_ESCAPES = '"\\$`'
COMMENT_BEFORE = " \t\n;&|("


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


def mask(command: str) -> tuple[str, list[str]]:
    """The command with quoted strings and comments blanked, and the decoded strings they held.

    Each quoted string is replaced by a placeholder naming its entry in the returned list. A double-quoted string holding `$(` or a
    backquote is kept as code. Raises ValueError on unbalanced quotes.
    """
    code = []
    strings: list[str] = []
    i = 0
    n = len(command)

    def placeholder(text: str) -> str:
        strings.append(text)
        return f"{len(strings) - 1}"

    while i < n:
        c = command[i]
        if c == "\\":
            code.append(" ")
            i += 2
        elif c == "'":
            end = command.find("'", i + 1)
            if end < 0:
                raise ValueError("unbalanced single quote")
            code.append(placeholder(command[i + 1:end]))
            i = end + 1
        elif c == "$" and command[i + 1:i + 2] == "'":
            j = i + 2
            text = []
            while j < n and command[j] != "'":
                if command[j] == "\\" and j + 1 < n:
                    text.append(command[j + 1])
                    j += 2
                else:
                    text.append(command[j])
                    j += 1
            if j >= n:
                raise ValueError("unbalanced $' quote")
            code.append(placeholder("".join(text)))
            i = j + 1
        elif c == '"':
            j = i + 1
            text = []
            while j < n and command[j] != '"':
                if command[j] == "\\" and j + 1 < n and command[j + 1] in SHELL_ESCAPES:
                    text.append(command[j + 1])
                    j += 2
                else:
                    text.append(command[j])
                    j += 1
            if j >= n:
                raise ValueError("unbalanced double quote")
            raw = command[i + 1:j]
            if "$(" in raw or "`" in raw:
                code.append(f" {raw} ")
            else:
                code.append(placeholder("".join(text)))
            i = j + 1
        elif c == "#" and (i == 0 or command[i - 1] in COMMENT_BEFORE):
            end = command.find("\n", i)
            i = n if end < 0 else end
        else:
            code.append(c)
            i += 1
    return "".join(code), strings


def resolve(word: str, strings: list[str]) -> str:
    """A word of the masked command as text: a placeholder becomes the string it held."""
    match = PLACEHOLDER.fullmatch(word)
    if match:
        return strings[int(match.group(1))]
    return word


def long_sleep(code: str, strings: list[str]) -> str | None:
    """A reason when a sleep in the command is longer than the limit (or cannot be read), else None."""
    for match in SLEEP.finditer(code):
        argument = resolve(match.group(1), strings)
        duration = DURATION.match(argument)
        if duration is None:
            return f"sleep {argument}: the argument is not a number of seconds the hook can check"
        seconds = float(duration.group(1)) * UNIT_SECONDS[duration.group(2)]
        if seconds > MAX_SLEEP_SECONDS:
            return f"sleep {argument} waits longer than {MAX_SLEEP_SECONDS} seconds"
    return None


def sleep_in_loop(code: str) -> str | None:
    """A reason when a sleep sits inside an until or while loop (its body: from the `do` to the `done`), else None."""
    for loop in LOOP_WORD.finditer(code):
        rest = code[loop.end():]
        do = DO_WORD.search(rest)
        if do is None:
            continue
        after_do = rest[do.end():]
        done = DONE_WORD.search(after_do)
        body = after_do[: done.start()] if done else after_do
        if SLEEP_WORD.search(body):
            return f"a {loop.group(1)} loop calls sleep"
    return None


def background_nohup(code: str) -> str | None:
    """A reason when a nohup command is started in the background with a trailing &, else None."""
    for nohup in NOHUP_WORD.finditer(code):
        if BACKGROUND_AMP.search(code, nohup.end()):
            return "nohup ... & starts a background job"
    return None


def shell_string(code: str, strings: list[str]) -> str | None:
    """A reason when the string argument of `bash -c`, `sh -c` (or zsh, dash) breaks the rule, else None."""
    for match in SHELL_C.finditer(code):
        argument = resolve(match.group(1), strings)
        reason = violation(argument)
        if reason is not None:
            return f"{reason} (inside a shell -c string)"
    return None


def violation(command: str) -> str | None:
    """The first reason the command breaks the waiting rule, or None when it is allowed."""
    text = strip_heredocs(command)
    try:
        code, strings = mask(text)
    except ValueError:
        code, strings = text, []
    return (
        long_sleep(code, strings)
        or sleep_in_loop(code)
        or background_nohup(code)
        or shell_string(code, strings)
    )


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
