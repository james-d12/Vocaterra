#!/usr/bin/env bash
# Runs the test suite, or a tier of it, the way the merge gate does: the build with -m:4 and the tests with -m:2, each through
# scripts/limited.sh (one heavy command on the machine at a time, under the memory and CPU caps). docs/process/test-performance-plan.md, T4.
#
#   scripts/test.sh fast                every test that is not marked slow ([Trait(TestSpeed.Trait, TestSpeed.Slow)] on a class or a method, or
#                                       the fuzz suite's assembly trait): run it after every step of the work
#   scripts/test.sh full                the whole suite, as the gate and CI run it: run it at a milestone and before a merge
#   scripts/test.sh project <name>      one test project, every test (Vocaterra.Game.Tests, or the path of a test project)
#
# Options after the mode go to dotnet test: --no-build skips the build, and a --filter is combined with the fast tier's. Model-gated tests run
# when VOCATERRA_MODELS_DIR is set, or the shared models folder (~/.local/share/vocaterra/models) exists, as in the gate. The TRX files land in
# .claude/test-timings/<mode>-<stamp>/, and the run ends with the wall time and counts of each assembly (scripts/test-timings.py); fast also
# names the tests it left out.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
L="$ROOT/scripts/limited.sh"
cd "$ROOT" || exit 1

usage() {
  echo "usage: scripts/test.sh fast|full|project <name> [dotnet test options]" >&2
  exit 2
}
[ "$#" -ge 1 ] || usage
mode="$1"
shift
case "$mode" in
  fast|full) target="Vocaterra.sln" ;;
  project)
    [ "$#" -ge 1 ] || usage
    name="$1"
    shift
    if [ -e "$name" ]; then target="$name"; else target="tests/$name/$name.csproj"; fi
    [ -e "$target" ] || { echo "scripts/test.sh: no test project '$name' (looked for $target)" >&2; exit 2; }
    ;;
  *) echo "scripts/test.sh: unknown mode '$mode'" >&2; usage ;;
esac

if [ -z "${VOCATERRA_MODELS_DIR:-}" ] && [ -d "$HOME/.local/share/vocaterra/models" ]; then
  export VOCATERRA_MODELS_DIR="$HOME/.local/share/vocaterra/models"
fi

build=1
filter=""
args=()
while [ "$#" -gt 0 ]; do
  case "$1" in
    --no-build) build=0 ;;
    --filter) filter="${2:?--filter needs a value}"; shift ;;
    --filter=*) filter="${1#--filter=}" ;;
    *) args+=("$1") ;;
  esac
  shift
done
if [ "$mode" = fast ]; then
  filter="${filter:+($filter)&}Speed!=Slow"
fi
[ -n "$filter" ] && args+=(--filter "$filter")

if [ "$build" -eq 1 ]; then
  "$L" dotnet build "$target" -warnaserror -m:4 -v:q -nologo || exit $?
fi

results=".claude/test-timings/$mode-$(date +%Y%m%d-%H%M%S)"
echo "test.sh: $mode on $target${filter:+ with --filter \"$filter\"}; models: ${VOCATERRA_MODELS_DIR:-none}; TRX files in $results"
"$L" dotnet test "$target" --no-build -m:2 --logger trx --results-directory "$results" "${args[@]}"
rc=$?

if [ "$mode" = fast ]; then
  echo
  echo "test.sh: left out of the fast tier (marked Speed=Slow; scripts/test.sh full runs them):"
  python3 -I - tests <<'PY'
import os, re, sys

MARK = re.compile(r"\[(assembly: AssemblyTrait|Trait)\(TestSpeed\.Trait, TestSpeed\.Slow\)\]")
DECL = re.compile(r"\b(class|void) (\w+)")
for directory, names, files in sorted(os.walk(sys.argv[1])):
    names[:] = sorted(n for n in names if n not in ("bin", "obj"))
    project = os.path.relpath(directory, sys.argv[1]).split(os.sep)[0]
    for name in sorted(f for f in files if f.endswith(".cs")):
        lines = open(os.path.join(directory, name), encoding="utf-8").read().split("\n")
        for i, line in enumerate(lines):
            mark = MARK.search(line)
            if not mark:
                continue
            if mark.group(1).startswith("assembly"):
                print(f"  {project}: every test of the assembly")
                continue
            decl = next((m for m in (DECL.search(l) for l in lines[i : i + 8]) if m), None)
            print(f"  {project}: {decl.group(1)} {decl.group(2)}" if decl else f"  {project}: {name}:{i + 1}")
PY
fi
echo
python3 -I scripts/test-timings.py "$results" --top 0
exit "$rc"
