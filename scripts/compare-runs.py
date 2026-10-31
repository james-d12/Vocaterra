"""Compares two captures of scripts/scripted-runs.sh: python3 -I scripts/compare-runs.py BASE NEW. Prints every difference; exit 1 when there is one.

Timings (a number followed by ms, or by s in a duration), wall-clock times and dates are masked first; everything else must be the same
byte for byte, the pictures included. The content bundles' hashes are masked too (CONTENT_PLAN C01), so a content move that changes no behaviour
compares equal: game-v1's in the session log, the saves and the core's version stamps, and the locale bundle's beside it. So are the checkout's
path (a baseline taken in another worktree) and the lines scripts/limited.sh prints while it waits for another heavy command (R16 phase 2).
"""
import difflib
import os
import re
import sys

base, new = sys.argv[1], sys.argv[2]
MASKS = [
    (re.compile(r"\d+(\.\d+)?\s?ms\b"), "<ms>"),
    (re.compile(r"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:?\d{2})?"), "<time>"),
    (re.compile(r"\b\d{8}-\d{6}\b"), "<stamp>"),
    (re.compile(r"Duration: [^,]+"), "Duration: <d>"),
    (re.compile(r"\"(wall|at|time|started|ended|unix|wall_seconds|\w*_ms|\w*latency\w*)\":\s?[\d.]+"), r'"\1": <n>'),
    (re.compile(r"(p50|p90|p95|p99|max|min|mean|avg|median)[ =:]+[\d.]+"), r"\1 <n>"),
    (re.compile(r'"(bundle|bundleHash|contentHash|locale_bundle|localeHash)":\s?"[0-9a-f]{12,64}"'), r'"\1": "<hash>"'),
    (re.compile(r"\b(bundle|locale) [0-9a-f]{12}\b"), r"\1 <hash>"),
    (re.compile(r"\b(hash) [0-9a-f]{64}\b"), r"\1 <hash>"),
    (re.compile(r"(\S*/)?(content|src|tests|assets)/(game-v1|locale|Vocaterra\.)"), r"<repo>/\2/\3"),
    (re.compile(r"^limited\.sh: .*\n", re.M), ""),
]
TEXT = (".txt", ".log", ".jsonl", ".json", ".md", ".csv")

# Runs whose turns land a few ticks apart from one capture to the next (the worker's timing reaches them): only the order of what they print
# and log is compared, with every number and digest masked; their pictures, saves and sound logs move with the ticks and are not compared.
LOOSE = {"r02-river", "r03-npcs", "r04-journal", "r05-book-play", "r06-practise", "r10-playtest", "r11-mic", "r16-text-scale", "r17-speak", "r18-bakery", "r19-directions"}
LOOSE_FILES = ("stdout.txt", "session/session.jsonl")
NUMBERS = re.compile(r"\b[0-9a-f]{12,}\b|\d+(\.\d+)?")


def mask(text):
    for pattern, repl in MASKS:
        text = pattern.sub(repl, text)
    return text


def files(root):
    out = {}
    for d, _, fs in os.walk(root):
        for f in fs:
            p = os.path.join(d, f)
            out[os.path.relpath(p, root)] = p
    return out


different = False
for run in sorted(set(os.listdir(base)) | set(os.listdir(new))):
    a, b = os.path.join(base, run), os.path.join(new, run)
    if not os.path.isdir(a) or not os.path.isdir(b):
        print(f"{run}: only in {'base' if os.path.isdir(a) else 'new'}")
        different = True
        continue
    fa, fb = files(a), files(b)
    problems = []
    loose = run in LOOSE
    for rel in sorted(set(fa) | set(fb)):
        if loose and rel not in LOOSE_FILES:
            continue
        if rel not in fa or rel not in fb:
            problems.append(f"  {rel}: only in {'base' if rel in fa else 'new'}")
            continue
        if rel.endswith(TEXT) or rel.endswith(".save"):
            ta = mask(open(fa[rel], encoding="utf-8", errors="replace").read())
            tb = mask(open(fb[rel], encoding="utf-8", errors="replace").read())
            if loose:
                ta, tb = NUMBERS.sub("#", ta), NUMBERS.sub("#", tb)
            ta, tb = ta.splitlines(), tb.splitlines()
            if loose and rel.endswith(".jsonl"):
                ta, tb = sorted(ta), sorted(tb)  # a turn's line can land before or after an event line of the same frame
            if ta != tb:
                diff = list(difflib.unified_diff(ta, tb, "base", "new", n=0, lineterm=""))
                problems.append(f"  {rel}: {sum(1 for l in diff if l.startswith(('+', '-')) and not l.startswith(('+++', '---')))} lines differ")
                problems.extend("    " + l[:220] for l in diff[2:40])
        elif open(fa[rel], "rb").read() != open(fb[rel], "rb").read():
            problems.append(f"  {rel}: bytes differ")
    print(f"{run}: {'same' if not problems else 'DIFFERENT'}{' (order only)' if loose else ''}")
    for p in problems:
        print(p)
    different |= bool(problems)
sys.exit(1 if different else 0)
