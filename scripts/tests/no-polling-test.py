#!/usr/bin/env python3
"""Checks the waiting-rule hook (scripts/hooks/no-polling.py) on sample Bash calls: each must exit 0 (allowed) or 2 (blocked).

    python3 scripts/tests/no-polling-test.py          prints one line per case and a count; exit 1 when a case differs from its expectation

It runs the hook as Claude Code does: the call as JSON on stdin. Standard library only (B29c).
"""

import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
HOOK = ROOT / "scripts" / "hooks" / "no-polling.py"

ALLOW = 0
BLOCK = 2

CASES = [
    ("sleep 5", ALLOW, "a short sleep outside a loop"),
    ("sleep 30", ALLOW, "the limit itself"),
    ("sleep 0.5; echo ok", ALLOW, "a fraction of a second"),
    ("sleep 5; sleep 10", ALLOW, "two short sleeps, no loop"),
    ("sleep 31", BLOCK, "one second over the limit"),
    ("sleep 600", BLOCK, "ten minutes"),
    ("sleep 1m", BLOCK, "a minute, with a unit"),
    ("sleep infinity", BLOCK, "no number of seconds"),
    ("until grep -q done log; do sleep 10; done", BLOCK, "a polling loop"),
    ("while true; do sleep 1; done", BLOCK, "a loop of short sleeps is still a loop"),
    ("while read -r line; do echo \"$line\"; done < file.txt", ALLOW, "a loop without a sleep"),
    ("nohup dotnet test &", BLOCK, "a background nohup job"),
    ("nohup dotnet test > run.log 2>&1 &", BLOCK, "a background nohup job with redirections"),
    ("dotnet test 2>&1 | tail -n 5", ALLOW, "a redirection, not a background job"),
    ("scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 && echo built", ALLOW, "the repository's own build line"),
    ("cat > notes.md <<'EOF'\nthe rule: never sleep 600 in a shell\nEOF", ALLOW, "a heredoc that only mentions sleep"),
    ("git commit -F - <<'EOF'\nwaited with sleep 600 before\nEOF\ngit status", ALLOW, "a commit message that mentions sleep"),
    ("cat <<'EOF' > x.txt\nsleep 600\nEOF\nsleep 600", BLOCK, "a heredoc followed by a real long sleep"),
    ("git commit -m \"no sleep 600 here\"", ALLOW, "a double-quoted commit message that mentions sleep (B29e)"),
    ("echo 'while true; do sleep 1; done'", ALLOW, "a single-quoted loop that only is text (B29e)"),
    ("git commit -F - <<'EOF'\nSubject line\n\nWaited, then tried: sleep 600 and a loop.\nEOF\ngit log -1", ALLOW,
     "a multi-line commit message in a heredoc that mentions sleep 600 (B29e)"),
    ("bash -c \"sleep 600\"", BLOCK, "a real sleep as the string of bash -c (B29e)"),
    ("timeout 900 bash -c 'until x; do sleep 5; done'", BLOCK, "a polling loop as the string of bash -c under timeout (B29e)"),
    ("echo \"x\" && sleep 45", BLOCK, "a long sleep after a quoted word (B29e)"),
    ("grep -n \"sleep\" file", ALLOW, "grep for the word sleep (B29e)"),
    ("echo \"unbalanced; sleep 600", BLOCK, "unbalanced quotes fall back to the raw check, which finds sleep 600 (B29e)"),
    ("sh -c 'sleep 600'", BLOCK, "a long sleep as the string of sh -c (B29e)"),
    ("echo ok # sleep 600", ALLOW, "a long sleep inside a trailing comment (B29e)"),
    ("echo $'sleep 600'", ALLOW, "a long sleep inside an ANSI-C string (B29e)"),
    ("sleep \"600\"", BLOCK, "a quoted argument of sleep is still read (B29e)"),
    ("echo \"$(sleep 600)\"", BLOCK, "a sleep inside command substitution in double quotes runs (B29e)"),
]

NOT_A_BASH_CALL = [
    ("not JSON at all", ALLOW, "input the hook cannot read"),
    (json.dumps({"tool_name": "Read", "tool_input": {"file_path": "/x"}}), ALLOW, "another tool"),
]


def run(stdin_text: str) -> int:
    return subprocess.run([sys.executable, str(HOOK)], input=stdin_text, text=True, capture_output=True, check=False).returncode


def main() -> int:
    failures = 0
    total = 0
    for command, expected, why in CASES:
        total += 1
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": command}})
        actual = run(payload)
        failures += actual != expected
        print(f"{'ok  ' if actual == expected else 'FAIL'} exit {actual} (want {expected}): {why}")
    for text, expected, why in NOT_A_BASH_CALL:
        total += 1
        actual = run(text)
        failures += actual != expected
        print(f"{'ok  ' if actual == expected else 'FAIL'} exit {actual} (want {expected}): {why}")
    print(f"{total - failures} of {total} cases as expected")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
