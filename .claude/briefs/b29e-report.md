# B29e report (agent, 2026-11-18, about 15:35 UTC)

Branch `B29e-hook-quotes`, head `153240d0`, one commit on `9f2f3a31`; `scripts/hooks/no-polling.py` and its test only.
Approach: quoted strings (single, double, `$'…'`) and `#` comments are masked before the checks, their decoded text kept; the string
argument of `bash`/`sh`/`zsh`/`dash -c` (also behind `timeout …`) is checked recursively as a command; a double-quoted string holding `$(`
or a backquote stays code; unbalanced quotes fall back to the raw check; heredoc bodies stripped as before; the header names the rule.
Test: 33 of 33 (20 old, 13 new). Still wrong (outside the brief): `eval "sleep 600"` passes; `bash -s` with a heredoc body that sleeps passes.
