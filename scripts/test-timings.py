"""Reads the TRX files of a test run and prints where the time went: python3 -I scripts/test-timings.py RUN [BASE] [--top N].

RUN (and BASE) is a folder of .trx files, searched recursively, as `dotnet test --logger trx --results-directory RUN` writes them (one file per
test assembly), or a single .trx file. It prints the wall time and the outcome counts of each assembly, the N slowest tests (default 30) and the
total time of each class (the N largest); --top 0 prints the assemblies alone. With BASE it also prints, per assembly, the wall time and the counts of both runs side by side, so a
phase of docs/process/test-performance-plan.md reports against the baseline: the passed and skipped counts must match or rise only by the tests the
phase adds, and the wall time must go down.

A test's time is the duration the runner reports for it (a theory row is one test); the time of a class's constructor and fixtures lands on its
tests, the assembly's discovery and start-up only on the wall time. An assembly's wall time is the span between its TRX file's start and finish;
the run's is the span from the first start to the last finish, so the time a run spent queued in scripts/limited.sh is not counted. With -m:2 two
assemblies run at once, so the run's wall time is set by the longer of the two lanes, not by the sum.
"""
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from datetime import datetime

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
DURATION = re.compile(r"(\d+):(\d+):(\d+(?:\.\d+)?)")


def seconds(text):
    match = DURATION.fullmatch(text or "")
    if not match:
        return 0.0
    hours, minutes, secs = match.groups()
    return int(hours) * 3600 + int(minutes) * 60 + float(secs)


def stamp(text):
    # TRX times carry seven fractional digits and an offset (2026-10-29T10:00:00.1234567+00:00); datetime takes six.
    text = re.sub(r"(\.\d{6})\d+", r"\1", text)
    return datetime.fromisoformat(text)


def trx_files(path):
    if os.path.isfile(path):
        return [path]
    found = []
    for directory, _, names in os.walk(path):
        found.extend(os.path.join(directory, name) for name in names if name.endswith(".trx"))
    if not found:
        sys.exit(f"no .trx file under {path}")
    return sorted(found)


def read_run(path):
    """Per assembly: wall seconds, start, finish and counts; and every test as (seconds, assembly, class, name, outcome)."""
    assemblies = {}
    tests = []
    for trx in trx_files(path):
        root = ET.parse(trx).getroot()
        if root.find(NS + "Results") is None:
            continue  # an assembly whose tests were all filtered out (the fast tier's fuzz suite) has no results and names no assembly
        classes = {}
        storage = None
        for unit in root.iter(NS + "UnitTest"):
            method = unit.find(NS + "TestMethod")
            classes[unit.get("id")] = method.get("className") if method is not None else "?"
            storage = storage or unit.get("storage")
        assembly = os.path.basename(storage or trx).removesuffix(".dll")
        times = root.find(NS + "Times")
        start, finish = stamp(times.get("start")), stamp(times.get("finish"))
        wall = (finish - start).total_seconds()
        counts = defaultdict(int)
        for result in root.iter(NS + "UnitTestResult"):
            outcome = result.get("outcome", "?")
            counts[outcome] += 1
            tests.append((seconds(result.get("duration")), assembly, classes.get(result.get("testId"), "?"), result.get("testName", "?"), outcome))
        previous = assemblies.get(assembly)
        if previous:
            # The same assembly twice (a rerun in the folder): keep both counts, the longer wall time.
            wall = max(wall, previous["wall"])
            start, finish = min(start, previous["start"]), max(finish, previous["finish"])
            for key, value in previous["counts"].items():
                counts[key] += value
        assemblies[assembly] = {"wall": wall, "start": start, "finish": finish, "counts": counts}
    return assemblies, tests


def fmt(secs):
    return f"{secs:7.1f} s" if secs < 600 else f"{secs / 60:6.1f} m"


def run_wall(assemblies):
    if not assemblies:
        return 0.0
    return (max(data["finish"] for data in assemblies.values()) - min(data["start"] for data in assemblies.values())).total_seconds()


def counts_text(counts):
    return f"passed {counts.get('Passed', 0):5}  failed {counts.get('Failed', 0):3}  skipped {counts.get('NotExecuted', 0):3}"


def short(name, width=110):
    return name if len(name) <= width else name[: width - 3] + "..."


def report(path, top):
    assemblies, tests = read_run(path)
    print(f"== {path}")
    print("\nWall time per assembly")
    for name, data in sorted(assemblies.items(), key=lambda item: -item[1]["wall"]):
        print(f"  {fmt(data['wall'])}  {counts_text(data['counts'])}  {name}")
    total = defaultdict(int)
    for data in assemblies.values():
        for key, value in data["counts"].items():
            total[key] += value
    print(f"  {fmt(run_wall(assemblies))}  {counts_text(total)}  (the run: first start to last finish)")

    if top <= 0:
        return assemblies

    print(f"\nThe {top} slowest tests")
    for secs, assembly, cls, name, outcome in sorted(tests, key=lambda test: -test[0])[:top]:
        mark = "" if outcome == "Passed" else f" [{outcome}]"
        print(f"  {fmt(secs)}  {short(name)}{mark}")

    per_class = defaultdict(lambda: [0.0, 0])
    for secs, assembly, cls, _, _ in tests:
        entry = per_class[(assembly, cls)]
        entry[0] += secs
        entry[1] += 1
    print(f"\nTotal time per class (the {top} largest)")
    for (assembly, cls), (secs, count) in sorted(per_class.items(), key=lambda item: -item[1][0])[:top]:
        print(f"  {fmt(secs)}  {count:4} tests  {cls}")
    return assemblies


def compare(base_path, new_path):
    base, _ = read_run(base_path)
    new, _ = read_run(new_path)
    print(f"\n== {new_path} against {base_path}")
    print(f"  {'assembly':34} {'base wall':>10} {'new wall':>10} {'change':>8}   passed (base -> new)   skipped (base -> new)")
    for name in sorted(set(base) | set(new)):
        b = base.get(name, {"wall": 0.0, "counts": {}})
        n = new.get(name, {"wall": 0.0, "counts": {}})
        change = f"{(n['wall'] - b['wall']) / b['wall'] * 100:+.0f}%" if b["wall"] else "new"
        passed = f"{b['counts'].get('Passed', 0)} -> {n['counts'].get('Passed', 0)}"
        skipped = f"{b['counts'].get('NotExecuted', 0)} -> {n['counts'].get('NotExecuted', 0)}"
        failed = n["counts"].get("Failed", 0)
        print(f"  {name:34} {fmt(b['wall']):>10} {fmt(n['wall']):>10} {change:>8}   {passed:20}   {skipped}" + (f"   FAILED {failed}" if failed else ""))
    b, n = run_wall(base), run_wall(new)
    totals = [(base, b), (new, n)]
    passed = " -> ".join(str(sum(data["counts"].get("Passed", 0) for data in run.values())) for run, _ in totals)
    skipped = " -> ".join(str(sum(data["counts"].get("NotExecuted", 0) for data in run.values())) for run, _ in totals)
    change = f"{(n - b) / b * 100:+.0f}%" if b else ""
    print(f"  {'(the run)':34} {fmt(b):>10} {fmt(n):>10} {change:>8}   {passed:20}   {skipped}")


def main(argv):
    top = 30
    if "--top" in argv:
        index = argv.index("--top")
        top = int(argv[index + 1])
        del argv[index : index + 2]
    if len(argv) not in (1, 2):
        sys.exit(__doc__)
    report(argv[0], top)
    if len(argv) == 2:
        compare(argv[1], argv[0])


if __name__ == "__main__":
    main(sys.argv[1:])
