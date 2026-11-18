"""The merge gate's test helpers (B29a): python3 -I scripts/gate-tests.py failed|report ...

  failed TRX_DIR        prints one line per failed test of the TRX files under TRX_DIR: PROJECT<TAB>CLASS.METHOD, where PROJECT is the test project's name
                        (tests/PROJECT/PROJECT.csproj) and CLASS.METHOD is the full name `dotnet test --filter "FullyQualifiedName=..."` takes (a theory's
                        failed rows give one line, by method). Duplicates are printed once.
  report LABEL=TRX_DIR [LABEL=TRX_DIR ...] --out FILE --logs DIR
                        prints each assembly's wall time and its slowest class's time next to the last gate's (the newest other
                        timing-*.json in DIR), writes this run's numbers to FILE (timing-<stamp>.json) and warns, never fails, when the
                        suite's time grew by more than 10 percent or a class took more than 5 minutes. With no earlier report it prints the times alone.
"""
import glob
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from datetime import datetime

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
DURATION = re.compile(r"(\d+):(\d+):(\d+(?:\.\d+)?)")
GROWTH = 0.10
CLASS_LIMIT = 300.0


def seconds(text):
    match = DURATION.fullmatch(text or "")
    if not match:
        return 0.0
    hours, minutes, secs = match.groups()
    return int(hours) * 3600 + int(minutes) * 60 + float(secs)


def stamp(text):
    return datetime.fromisoformat(re.sub(r"(\.\d{6})\d+", r"\1", text))


def trx_files(path):
    found = []
    for directory, _, names in os.walk(path):
        found.extend(os.path.join(directory, name) for name in names if name.endswith(".trx"))
    return sorted(found)


def parse(trx):
    """(assembly, wall, [(class, method, testName, seconds, outcome)]) of one TRX file, or None when it has no results."""
    root = ET.parse(trx).getroot()
    if root.find(NS + "Results") is None:
        return None
    units = {}
    storage = None
    for unit in root.iter(NS + "UnitTest"):
        method = unit.find(NS + "TestMethod")
        units[unit.get("id")] = (method.get("className"), method.get("name")) if method is not None else ("?", "?")
        storage = storage or unit.get("storage")
    assembly = os.path.basename(storage or trx).removesuffix(".dll")
    times = root.find(NS + "Times")
    wall = (stamp(times.get("finish")) - stamp(times.get("start"))).total_seconds()
    results = []
    for result in root.iter(NS + "UnitTestResult"):
        cls, method = units.get(result.get("testId"), ("?", "?"))
        results.append((cls, method, result.get("testName", "?"), seconds(result.get("duration")), result.get("outcome", "?")))
    return assembly, wall, results


def project_dir(assembly):
    """The test project's folder name: the TRX storage path is lower case, the folder under tests/ is not."""
    for name in os.listdir("tests"):
        if name.lower() == assembly.lower():
            return name
    return assembly


def failed(directory):
    seen = set()
    for trx in trx_files(directory):
        parsed = parse(trx)
        if not parsed:
            continue
        assembly, _, results = parsed
        for cls, method, _, _, outcome in results:
            if outcome == "Failed" and (assembly, cls, method) not in seen:
                seen.add((assembly, cls, method))
                print(f"{project_dir(assembly)}\t{cls}.{method}")


def collect(spec):
    """{assembly: {"wall": s, "slowest": [class, s]}} of the labelled TRX folders; the same assembly in two folders adds up."""
    data = {}
    for item in spec:
        _, directory = item.split("=", 1)
        for trx in trx_files(directory):
            parsed = parse(trx)
            if not parsed:
                continue
            assembly, wall, results = parsed
            per_class = defaultdict(float)
            for cls, _, _, secs, _ in results:
                per_class[cls] += secs
            cls, secs = max(per_class.items(), key=lambda pair: pair[1], default=("-", 0.0))
            entry = data.setdefault(assembly, {"wall": 0.0, "slowest": ["-", 0.0]})
            entry["wall"] += wall
            if secs > entry["slowest"][1]:
                entry["slowest"] = [cls, secs]
    return data


def fmt(secs):
    return f"{secs:6.0f} s"


def report(args):
    out = logs = None
    spec = []
    i = 0
    while i < len(args):
        if args[i] == "--out":
            out = args[i + 1]
            i += 2
        elif args[i] == "--logs":
            logs = args[i + 1]
            i += 2
        else:
            spec.append(args[i])
            i += 1
    data = collect(spec)
    previous = None
    if logs:
        earlier = sorted(f for f in glob.glob(os.path.join(logs, "timing-*.json")) if not out or os.path.abspath(f) != os.path.abspath(out))
        if earlier:
            try:
                with open(earlier[-1], encoding="utf-8") as handle:
                    previous = json.load(handle)
                previous["file"] = os.path.basename(earlier[-1])
            except (OSError, ValueError):
                previous = None
    total = sum(entry["wall"] for entry in data.values())
    old = previous["assemblies"] if previous else {}
    print("test times" + (f" (against the last gate's report, {previous['file']})" if previous else " (no earlier report to compare with)"))
    for name in sorted(data, key=lambda n: -data[n]["wall"]):
        entry = data[name]
        line = f"  {name:34} {fmt(entry['wall'])}  slowest class {fmt(entry['slowest'][1])} {entry['slowest'][0].rsplit('.', 1)[-1]}"
        if name in old:
            line += f"   | last: {fmt(old[name]['wall'])}, slowest {fmt(old[name]['slowest'][1])}"
        print(line)
    # The suite's time is the sum of its assemblies' times (the lanes overlap, so it is a cost, not the wall time of the run).
    line = f"  {'all assemblies together':34} {fmt(total)}"
    if previous:
        line += f"   | last: {fmt(previous['total'])}"
    print(line)
    if previous and previous["total"] > 0 and total > previous["total"] * (1 + GROWTH):
        print(f"WARNING: the test time grew {100 * (total / previous['total'] - 1):.0f} percent ({previous['total']:.0f} s to {total:.0f} s); find what was added (a warning, not a failure)")
    for name, entry in data.items():
        if entry["slowest"][1] > CLASS_LIMIT:
            print(f"WARNING: {entry['slowest'][0]} took {entry['slowest'][1]:.0f} s in {name}, over 5 minutes (a warning, not a failure)")
    if out:
        with open(out, "w", encoding="utf-8") as handle:
            json.dump({"total": total, "assemblies": data}, handle, indent=1)


def main():
    if len(sys.argv) >= 3 and sys.argv[1] == "failed":
        failed(sys.argv[2])
    elif len(sys.argv) >= 3 and sys.argv[1] == "report":
        report(sys.argv[2:])
    else:
        sys.exit(__doc__)


main()
