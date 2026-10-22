#!/usr/bin/env bash
# Summarises session folders written by the game's session log (S11, D-game-10): one session.jsonl per folder, one JSON object per line.
# It prints, per folder and in total: completion, retries, support used, ASR latency and failures, the pre/post/recall check scores per item
# kind (dont_know apart), transfer uses, "found without Spanish", and one line per decision gate of the prototype brief with its evidence.
# The gates are evidence, never a verdict on learning efficacy (the brief: a small pilot gives directional evidence only); "Enjoyable" needs
# observer notes, which the script cannot measure.
#
# Usage: scripts/playtest-report.sh <session folder> [<session folder> ...]
# Needs bash and Python 3 (standard library only; the Python runs with -I, so no user site or environment imports). Nothing is sent anywhere.
set -euo pipefail

if [ "$#" -eq 0 ]; then
  echo "usage: scripts/playtest-report.sh <session folder> [<session folder> ...]" >&2
  exit 2
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "scripts/playtest-report.sh needs python3 (Python 3, standard library only)." >&2
  exit 2
fi

python3 -I - "$@" <<'PY'
import json
import os
import sys


def read_lines(folder):
    path = os.path.join(folder, "session.jsonl")
    if not os.path.isfile(path):
        sys.exit(f"{folder}: no session.jsonl (is this a session folder?)")
    events = []
    with open(path, encoding="utf-8") as handle:
        for number, line in enumerate(handle, 1):
            line = line.strip()
            if not line:
                continue
            try:
                events.append(json.loads(line))
            except json.JSONDecodeError as error:
                sys.exit(f"{path}:{number}: not JSON ({error})")
    return events


def median(values):
    if not values:
        return None
    ordered = sorted(values)
    middle = len(ordered) // 2
    if len(ordered) % 2:
        return ordered[middle]
    return (ordered[middle - 1] + ordered[middle]) / 2


def p90(values):
    if not values:
        return None
    ordered = sorted(values)
    rank = max(1, -(-len(ordered) * 9 // 10))  # nearest rank: ceil(0.9 n)
    return ordered[rank - 1]


def ms(value):
    return "-" if value is None else f"{value:.0f} ms"


def ratio(correct, total):
    return f"{correct}/{total}" if total else "none"


SUPPORT_KINDS = [
    ("voice", "replay", "Repetir"),
    ("voice", "slower", "Más despacio"),
    ("voice", "hint", "Ayuda"),
    ("voice", "no_se", "No sé"),
    ("voice", "choice", "choices"),
    ("voice", "typed_fallback", "typed fallback"),
]


def summarise(events):
    """Everything the report prints, from one folder's events (a folder may hold several runs)."""
    summary = {
        "runs": 0,
        "sessions": [],
        "seeds": set(),
        "arms": set(),  # the profile and the A/B arm of each run (S12)
        "checks_run": [],
        "objective_complete": False,
        "objective_done": 0,
        "objective_steps": 0,
        "complete_tick": None,
        "end_tick": 0,
        "turns": 0,
        "spoken_latencies": [],
        "spoken_turns": 0,
        "check_spoken_not_understood": 0,
        "not_understood": 0,
        "uncertain": 0,
        "turn_failed": 0,
        "timeouts": 0,
        "mic_failed": 0,
        "support": {label: 0 for _, _, label in SUPPORT_KINDS},
        "cards": 0,
        "fail_forward": 0,
        "transfer": 0,
        "found_without_spanish": False,
        "checks": {},  # (check, kind) -> {"correct", "answered", "dont_know", "skipped"}
        "phrases": {},
    }
    for event in events:
        kind = event.get("type")
        if kind == "session_start":
            summary["runs"] += 1
            summary["sessions"].append(event.get("session"))
            summary["seeds"].add(event.get("seed"))
            profile = event.get("profile") or {}
            if "playtest" not in profile:
                profile_label = "before the playtest flag"
            else:
                profile_label = "playtest" if profile.get("playtest") else "not the playtest profile"
            arm = "arm B, greyscale" if profile.get("greyscale") else "arm A, colour"
            summary["arms"].add(f"{profile_label}, {arm}")
        elif kind == "session_end":
            summary["end_tick"] = max(summary["end_tick"], event.get("tick") or 0)
            steps = event.get("objective") or []
            done = sum(1 for step in steps if step.get("count", 0) >= step.get("target", 1))
            summary["objective_done"] = max(summary["objective_done"], done)
            summary["objective_steps"] = max(summary["objective_steps"], len(steps))
            if event.get("objective_complete"):
                summary["objective_complete"] = True
                if summary["complete_tick"] is None and event.get("objective_complete_tick") is not None:
                    summary["complete_tick"] = event["objective_complete_tick"]
        elif kind == "check_start":
            summary["checks_run"].append(event.get("check"))
        elif kind == "check_result":
            key = (event.get("check"), event.get("kind"))
            bucket = summary["checks"].setdefault(key, {"correct": 0, "answered": 0, "dont_know": 0, "skipped": 0})
            if event.get("input") == "spoken":
                # A spoken check answer is recognition too: its latency counts, and a line the core did not understand is a failure.
                summary["spoken_turns"] += 1
                if event.get("latency_ms") is not None:
                    summary["spoken_latencies"].append(float(event["latency_ms"]))
                if not event.get("communicated"):
                    summary["check_spoken_not_understood"] += 1
            if event.get("input") == "skipped":
                bucket["skipped"] += 1
            else:
                bucket["answered"] += 1
                if event.get("correct"):
                    bucket["correct"] += 1
                if event.get("dont_know"):
                    bucket["dont_know"] += 1
        elif kind == "turn":
            summary["turns"] += 1
            if event.get("input") == "spoken":
                summary["spoken_turns"] += 1
                if event.get("latency_ms") is not None:
                    summary["spoken_latencies"].append(float(event["latency_ms"]))
            if not event.get("communicated") and not event.get("uncertain") and event.get("input") != "button":
                summary["not_understood"] += 1
        elif kind == "turn_failed":
            summary["turn_failed"] += 1
        elif kind == "voice":
            voice = event.get("kind")
            if voice == "uncertain":
                summary["uncertain"] += 1
            elif voice == "timeout":
                summary["timeouts"] += 1
            elif voice == "mic_check_failed":
                summary["mic_failed"] += 1
            for _, name, label in SUPPORT_KINDS:
                if voice == name:
                    summary["support"][label] += 1
        elif kind == "world":
            name = event.get("name")
            if name == "support_card_shown":
                summary["cards"] += 1
            elif name == "found_without_spanish":
                summary["found_without_spanish"] = True
        elif kind == "phrase":
            if event.get("kind") == "transfer":
                summary["transfer"] += 1
            elif event.get("kind") == "fail_forward":
                summary["fail_forward"] += 1
    return summary


def print_folder(title, summary, out):
    out.append(f"== {title}")
    out.append(
        f"  runs: {summary['runs']} (sessions {', '.join(str(s) for s in summary['sessions']) or '-'}; "
        f"seeds {', '.join(str(s) for s in sorted(summary['seeds'], key=str)) or '-'})"
    )
    out.append(f"  profile: {'; '.join(sorted(summary['arms'])) or '-'}")
    steps = f"objective {summary['objective_done']}/{summary['objective_steps']} steps"
    if summary["objective_complete"] and summary["complete_tick"] is None:
        out.append(f"  Completion: {steps}; already complete when this run started (no completion in this folder's runs)")
    elif summary["objective_complete"]:
        seconds = summary["complete_tick"] / 60
        out.append(f"  Completion: {steps}; complete in {seconds:.0f} s of play (tick {summary['complete_tick']})")
    else:
        out.append(f"  Completion: {steps}; not complete (last tick {summary['end_tick']})")
    out.append(
        f"  Retries: uncertain recognitions {summary['uncertain']}, not understood {summary['not_understood']} "
        f"(of {summary['turns']} turns)"
    )
    support = ", ".join(f"{label} {summary['support'][label]}" for _, _, label in SUPPORT_KINDS)
    out.append(f"  Support used: cards shown {summary['cards']}, {support}")
    latencies = summary["spoken_latencies"]
    out.append(
        f"  ASR latency: median {ms(median(latencies))}, p90 {ms(p90(latencies))} over {summary['spoken_turns']} spoken turns; "
        f"failures {summary['uncertain'] + summary['turn_failed'] + summary['timeouts'] + summary['mic_failed'] + summary['check_spoken_not_understood']} "
        f"(uncertain {summary['uncertain']}, failed {summary['turn_failed']}, timeouts {summary['timeouts']}, mic {summary['mic_failed']}, "
        f"check answers not understood {summary['check_spoken_not_understood']})"
    )
    for check in ("pre", "post", "recall"):
        if check not in summary["checks_run"] and not any(key[0] == check for key in summary["checks"]):
            out.append(f"  Checks {check}: not run")
            continue
        for kind in ("comprehension", "production"):
            bucket = summary["checks"].get((check, kind), {"correct": 0, "answered": 0, "dont_know": 0, "skipped": 0})
            out.append(
                f"  Checks {check} {kind}: correct {ratio(bucket['correct'], bucket['answered'])}, "
                f"dont_know {bucket['dont_know']}, skipped {bucket['skipped']}"
            )
    out.append(f"  Transfer uses: {summary['transfer']}; fail-forward uses: {summary['fail_forward']}")
    out.append(f"  Found without Spanish: {'yes' if summary['found_without_spanish'] else 'no'}")


def gates(summary, out, label):
    """One line per decision gate of the brief, with its evidence. Evidence only: never a verdict on learning."""
    out.append(
        f"  Gate Playable (evidence): objective {'complete' if summary['objective_complete'] else 'not complete'}; "
        f"fail-forward uses {summary['fail_forward']} (the game carried on without the phrase); "
        f"timeouts {summary['timeouts']}, failed turns {summary['turn_failed']}"
    )
    late = []
    for check in ("post", "recall"):
        bucket = summary["checks"].get((check, "production"), None)
        if bucket is not None:
            late.append(f"{check} production {ratio(bucket['correct'], bucket['answered'])}")
        comp = summary["checks"].get((check, "comprehension"), None)
        if comp is not None:
            late.append(f"{check} comprehension {ratio(comp['correct'], comp['answered'])}")
    if late:
        out.append(f"  Gate Learning signal: evidence only ({'; '.join(late)}; pre production "
                   f"{ratio(summary['checks'].get(('pre', 'production'), {}).get('correct', 0), summary['checks'].get(('pre', 'production'), {}).get('answered', 0))})")
    else:
        out.append("  Gate Learning signal: not measured (no post or recall check in these sessions)")
    out.append(
        f"  Gate Transfer: evidence only ({summary['transfer']} transfer uses; "
        f"found without Spanish: {'yes' if summary['found_without_spanish'] else 'no'})"
    )
    out.append("  Gate Enjoyable: observer notes needed (the script cannot measure it)")
    _ = label


def main(folders):
    out = []
    totals = {
        "runs": 0, "sessions": [], "seeds": set(), "arms": set(), "checks_run": [], "objective_complete": False, "complete_tick": None,
        "objective_done": 0, "objective_steps": 0,
        "end_tick": 0, "turns": 0, "spoken_latencies": [], "spoken_turns": 0, "not_understood": 0, "uncertain": 0,
        "turn_failed": 0, "timeouts": 0, "mic_failed": 0, "support": {label: 0 for _, _, label in SUPPORT_KINDS},
        "check_spoken_not_understood": 0,
        "cards": 0, "fail_forward": 0, "transfer": 0, "found_without_spanish": False, "checks": {}, "phrases": {},
    }
    for folder in folders:
        events = read_lines(folder)
        summary = summarise(events)
        print_folder(folder, summary, out)
        gates(summary, out, folder)
        out.append("")
        for key in ("runs", "turns", "spoken_turns", "not_understood", "uncertain", "turn_failed", "timeouts", "mic_failed", "cards",
                    "fail_forward", "transfer", "check_spoken_not_understood"):
            totals[key] += summary[key]
        totals["sessions"] += summary["sessions"]
        totals["seeds"] |= summary["seeds"]
        totals["arms"] |= summary["arms"]
        totals["checks_run"] += summary["checks_run"]
        totals["objective_complete"] = totals["objective_complete"] or summary["objective_complete"]
        totals["objective_done"] = max(totals["objective_done"], summary["objective_done"])
        totals["objective_steps"] = max(totals["objective_steps"], summary["objective_steps"])
        if totals["complete_tick"] is None:
            totals["complete_tick"] = summary["complete_tick"]
        totals["end_tick"] = max(totals["end_tick"], summary["end_tick"])
        totals["spoken_latencies"] += summary["spoken_latencies"]
        totals["found_without_spanish"] = totals["found_without_spanish"] or summary["found_without_spanish"]
        for label in totals["support"]:
            totals["support"][label] += summary["support"][label]
        for key, bucket in summary["checks"].items():
            merged = totals["checks"].setdefault(key, {"correct": 0, "answered": 0, "dont_know": 0, "skipped": 0})
            for field in merged:
                merged[field] += bucket[field]
    print_folder(f"TOTAL ({len(folders)} folder{'s' if len(folders) != 1 else ''})", totals, out)
    gates(totals, out, "total")
    out.append("")
    out.append("Evidence only: a small pilot gives directional evidence, not proof of learning efficacy (PROTOTYPE_BRIEF).")
    print("\n".join(out))


main(sys.argv[1:])
PY
