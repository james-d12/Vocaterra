#!/usr/bin/env bash
# The content loop in one command (V18; docs/research/2026-10-31-development-bottlenecks.md 2.4, action P2): every step after an edit of the game's content
# (content/game-v1, content/locale, content/PHRASES.md), in order. It stops at the first failure with a message naming the next thing to do,
# then prints one summary of at most 30 lines: each step, passed, failed, skipped or not run, what it changed (files, counts) and its log.
# Every step's full output goes to a log under .claude/content-loop/<stamp>/ (git-ignored; the ten newest runs are kept), never to the terminal.
#
#   scripts/content-loop.sh --grammar --speech      the line a content milestone's brief carries: run it after every edit of the content
#
# Do not run it under scripts/limited.sh. It calls limited.sh itself for each heavy step (each dotnet, content.sh and prerender call) and
# scripts/test.sh (which calls limited.sh) directly. limited.sh holds one machine-wide lock, so the loop run under it would wait for ever on its
# own first step: it refuses to start there (the VOCATERRA_LIMITED variable limited.sh sets, or limited.sh's lock inherited as an open file).
# The LanguageTool server is started with scripts/languagetool.sh, outside limited.sh (its own scope is its cap).
#
# Usage: scripts/content-loop.sh [--grammar] [--speech] [--record] [--tier fast|full|none]
#   --grammar  start the local LanguageTool server, check the strings the cache lacks and prune the entries no string uses (--prune), check that
#              a cache rebuilt from nothing equals it (CI's languagetool job), print every new finding (read each: fix the content or add a waiver
#              with its reason), and stop the server, even on a failure. Without it, new Spanish (a cache miss) stops the loop at step 1.
#              Needs scripts/fetch-tools.sh once, and 4 GB of free memory.
#   --speech   prerender the lines the renderer's --check names (its summary first), then refresh again (the manifest after the new WAVs, the
#              provenance, the strings). Without it, lines to render stop the loop at step 3.
#   --record   off by default: re-record the end-to-end goldens and the identity baseline, and print what moved (the git diff --stat and the
#              changed entries per file; they go in a commit of their own). Without it the goldens are checked, and a move stops the loop. The
#              stream digests are checked either way: since the core's M57 they run on frozen inputs, so content never moves them; if one moves,
#              the loop prints the digest the fuzz suite computed with the constant of FuzzLanguageStreamTests.cs it differs from (the script
#              edits no C#; with --record the later tests leave that class out), and the cause is a change outside the content (a generator).
#   --tier     the tests of step 8. fast (the default): scripts/test.sh fast, then the fuzz suite's grammar-sensitive classes at their full depth
#              (CorrectSentenceFuzzTests, MutationTagFuzzTests, RecastLoopFuzzTests; FuzzLanguageStreamTests is step 7). full: the whole suite,
#              as the gate runs it. none: no test, and no build, goldens or digests unless --record.
#
# The steps:
#   1 refresh   scripts/content.sh refresh: the manifests of content/game-v1 and content/locale, provenance, validate, the gate's lint and
#               cefr-checks, the host's --validate-content, the grammar strings and the grammar check offline from the committed cache
#   2 grammar   with --grammar, the LanguageTool steps of docs/game/content.md ("Refreshing the cache"), then CI's rebuilt-from-nothing check
#   3 speech    scripts/prerender-speech.sh --check (and the stale WAVs a render would delete); with --speech the render and a second refresh
#   4 fixtures  every file under tests/ that holds the bundle's old bundleHash (the save fixtures) gets the new one: the old value is the one at
#               HEAD, the one before step 1, or one an earlier run in this worktree saw (.claude/content-loop/bundle-values)
#   5 build     dotnet build Vocaterra.sln -warnaserror -m:4 (once; the tests run with --no-build)
#   6 goldens   tests/Vocaterra.Nlp.Golden.Tests, checked, or recorded with --record (VOCATERRA_RECORD_GOLDEN and VOCATERRA_RECORD_IDENTITY)
#   7 digests   FuzzLanguageStreamTests: since the core's M57 its digests run on frozen inputs (a fixture of the bundle's grammars and words), so a
#               content edit leaves them; the step checks they did not move, and a move means something other than the content changed
#   8 tests     the tier
# It runs the gate's content checks unchanged and skips none of them. Neither the line that starts it nor any command it prints for the user
# contains the words the sandbox refuses (the scripts it calls run those themselves), so an agent runs it as it is. Re-running it on an
# unedited tree changes no file.
#
# Exit status: 0 every step passed; 1 a step failed, or the stream digests are left to set; 2 usage; 3 refused (run under limited.sh).
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT" || exit 1
L="$ROOT/scripts/limited.sh"
LOCK="${XDG_RUNTIME_DIR:-/tmp}/vocaterra-heavy.lock"   # the lock limited.sh holds (keep in step with it)
GAME=content/game-v1
STRINGS=content/grammar/game-v1-strings.jsonl
CACHE=content/grammar/game-v1-cache.json
WAIVERS=content/grammar/game-v1-waivers.json
GOLDEN_DATA=tests/Vocaterra.Nlp.Golden.Tests/Data
GOLDEN_PROJECT=tests/Vocaterra.Nlp.Golden.Tests/Vocaterra.Nlp.Golden.Tests.csproj
STREAM_TEST=tests/Vocaterra.Nlp.Fuzzing.Tests/FuzzLanguageStreamTests.cs
STREAM_FILTER="FullyQualifiedName~.FuzzLanguageStreamTests."
FUZZ_FILTER="FullyQualifiedName~.CorrectSentenceFuzzTests.|FullyQualifiedName~.MutationTagFuzzTests.|FullyQualifiedName~.RecastLoopFuzzTests."
FINDING='^(error|warning|info|waived) [a-z]+\.[a-z.-]+ '

usage() {
  echo "usage: scripts/content-loop.sh [--grammar] [--speech] [--record] [--tier fast|full|none]   (never under scripts/limited.sh)" >&2
  echo "       the options and the steps are in the script's header" >&2
  exit "$1"
}

grammar=0
speech=0
record=0
tier=fast
while [ "$#" -gt 0 ]; do
  case "$1" in
    --grammar) grammar=1 ;;
    --speech) speech=1 ;;
    --record) record=1 ;;
    --tier) tier="${2:-}"; shift ;;
    --tier=*) tier="${1#--tier=}" ;;
    -h | --help) usage 0 ;;
    *) echo "content-loop.sh: unknown option '$1'" >&2; usage 2 ;;
  esac
  shift
done
case "$tier" in
  fast | full | none) ;;
  *) echo "content-loop.sh: --tier is fast, full or none, not '$tier'" >&2; usage 2 ;;
esac

# ---- the lock: refuse to run under limited.sh instead of waiting for ever on its lock.
inside_limited() {
  [ -n "${VOCATERRA_LIMITED:-}" ] && return 0
  local fd
  for fd in /proc/$$/fd/*; do
    [ "$(readlink "$fd" 2>/dev/null)" = "$LOCK" ] && return 0
  done
  return 1
}
if inside_limited; then
  cat >&2 <<'EOF'
content-loop.sh: refused: it is running under scripts/limited.sh. It calls limited.sh itself for each heavy step, and limited.sh's one lock
  would make the first of them wait for ever. Run it directly: scripts/content-loop.sh --grammar --speech
EOF
  exit 3
fi

STAMP="$(date +%Y%m%d-%H%M%S)"
LOGDIR=".claude/content-loop/$STAMP"
mkdir -p "$LOGDIR" || exit 1
find .claude/content-loop -mindepth 1 -maxdepth 1 -type d -name '20*' | sort | head -n -10 | xargs -r rm -rf --
BLOCKS="$LOGDIR/blocks.txt"
: >"$BLOCKS"
START=$SECONDS
OPTIONS="$( ((grammar)) && printf -- '--grammar ')$( ((speech)) && printf -- '--speech ')$( ((record)) && printf -- '--record ')--tier $tier"

STEPS=(refresh grammar speech fixtures build goldens digests tests)
declare -A STATUS=() DETAIL=() CHANGES=() LOGS=() SECS=()
CUR=""
T0=0
NEXT=""
MISSES=0
DIGESTS_TODO=0
GOLDENS_MOVED=0

# ---- helpers
progress() { if [ -t 1 ]; then printf 'content-loop: %s\n' "$*"; fi; }

# run <log> <command...>: the command's whole output goes to the log, none to the terminal; returns the command's status.
run() {
  local log="$LOGDIR/$1"
  shift
  printf '$ %s\n' "$*" >>"$log"
  "$@" >>"$log" 2>&1
}

# Every file that differs from HEAD or is untracked (and not ignored), with a checksum of its bytes: two snapshots give the files a step wrote.
snapshot() {
  { git diff --name-only HEAD --; git ls-files --others --exclude-standard; } | sort -u | while IFS= read -r f; do
    if [ -f "$f" ]; then printf '%s %s\n' "$(sha1sum <"$f" | cut -c1-40)" "$f"; else printf 'gone %s\n' "$f"; fi
  done
}

# The files of a list (one per line) as one short line: the WAVs counted per folder, the others by name.
describe() {
  python3 -I - "$1" <<'PY'
import sys
from collections import Counter

files = [line.rstrip("\n") for line in open(sys.argv[1], encoding="utf-8") if line.strip()]
if files:
    wav = Counter(f.rsplit("/", 1)[0] for f in files if f.endswith(".wav"))
    items = [f"{folder}/ ({n} wav)" for folder, n in sorted(wav.items())] + [f for f in files if not f.endswith(".wav")]
    text, shown = "", 0
    for item in items:
        if shown and len(text) + len(item) > 140:
            break
        text += (", " if text else "") + item
        shown += 1
    more = f" (+{len(items) - shown} more)" if shown < len(items) else ""
    print(f"{len(files)} {'file' if len(files) == 1 else 'files'} changed: {text}{more}")
PY
}

begin() {
  CUR="$1"
  T0=$SECONDS
  snapshot >"$LOGDIR/.snapshot"
  progress "$1"
}

# end_step <status> <detail> [log...]
end_step() {
  local status="$1" detail="$2"
  shift 2
  STATUS[$CUR]="$status"
  DETAIL[$CUR]="$detail"
  SECS[$CUR]=$((SECONDS - T0))
  LOGS[$CUR]="$*"
  snapshot >"$LOGDIR/.snapshot2"
  awk 'FILENAME == ARGV[1] { b[substr($0, index($0, " ") + 1)] = substr($0, 1, index($0, " ") - 1); next }
       { a[substr($0, index($0, " ") + 1)] = substr($0, 1, index($0, " ") - 1) }
       END { for (p in b) if (!(p in a)) print p; for (p in a) if (b[p] != a[p]) print p }' \
    "$LOGDIR/.snapshot" "$LOGDIR/.snapshot2" | sort >"$LOGDIR/$CUR.changed"
  CHANGES[$CUR]="$(describe "$LOGDIR/$CUR.changed")"
  rm -f "$LOGDIR/.snapshot" "$LOGDIR/.snapshot2"
}

skip() {
  STATUS[$1]=skipped
  DETAIL[$1]="$2"
}

# fail <detail> <next> [log...]: ends the step as failed and the run with the summary.
fail() {
  local detail="$1"
  NEXT="$2"
  shift 2
  end_step failed "$detail" "$@"
  finish_run 1
}

block() { printf '%s\n' "$@" >>"$BLOCKS"; }

# The lines of a log from the marker line on to the next marker (exclusive), for example one sub-step of refresh.
section() { awk -v m="$2" 'index($0, m) == 1 { on = 1; next } /^== refresh: / { on = 0 } on' "$1"; }

# The passed, failed and skipped tests of a dotnet test log, summed over its assemblies.
counts() {
  grep -oE 'Failed: +[0-9]+, Passed: +[0-9]+, Skipped: +[0-9]+' "$1" | awk -F'[:,]' '
    { f += $2; p += $4; s += $6 } END { printf "%d passed, %d failed, %d skipped", p, f, s }'
}

# The failed tests of a dotnet test log, as Class.Method, and their messages (without stack traces) as a block.
failures() {
  local log="$1" names
  names="$(grep -oE '^ +Failed [A-Za-z0-9_.]+' "$log" | awk '{ n = split($2, p, "."); print p[n - 1] "." p[n] }' | sort -u)"
  [ -n "$names" ] || return 0
  block "tests: the failures in $log (messages only; the whole output is in the log):"
  awk '/^ +Failed [A-Za-z0-9_.]+/ { on = 1; print "  " $0; next } /^ +Stack Trace:/ { on = 0 } on { print "  " $0 }' "$log" | head -n 40 >>"$BLOCKS"
  printf '%s' "$names" | tr '\n' ' '
}

bundle_value() { python3 -I -c 'import json, sys; print(json.load(sys.stdin)["bundleHash"])'; }

LT_UP=0
stop_languagetool() {
  if [ "$LT_UP" -eq 1 ]; then
    LT_UP=0
    scripts/languagetool.sh stop >>"$LOGDIR/02-grammar.log" 2>&1 || echo "content-loop.sh: the LanguageTool server did not stop: scripts/languagetool.sh stop" >&2
  fi
}
trap stop_languagetool EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

summary() {
  local total=$((SECONDS - START)) verdict="passed" s i=0 line
  for s in "${STEPS[@]}"; do
    [ "${STATUS[$s]}" = failed ] && verdict="FAILED at $s"
  done
  [ "$verdict" = passed ] && [ "$DIGESTS_TODO" -eq 1 ] && verdict="passed, but the stream digests are left to set"
  printf 'content-loop: %s in %d min %d s (%s); logs in %s/\n' "$verdict" $((total / 60)) $((total % 60)) "$OPTIONS" "$LOGDIR"
  for s in "${STEPS[@]}"; do
    i=$((i + 1))
    if [ -z "${SECS[$s]:-}" ]; then # skipped or not run: one line
      printf '  %d %-8s %-8s %s\n' "$i" "$s" "${STATUS[$s]}" "${DETAIL[$s]:-}"
      continue
    fi
    printf '  %d %-8s %-8s %4s s  %s\n' "$i" "$s" "${STATUS[$s]}" "${SECS[$s]}" "${LOGS[$s]:-}"
    line="${DETAIL[$s]:-}"
    [ -n "${CHANGES[$s]:-}" ] && line="${line:+$line; }${CHANGES[$s]}"
    [ -n "$line" ] && printf '                       %s\n' "$line"
  done
  printf 'next: %s\n' "$NEXT"
}

finish_run() {
  local rc="$1" s
  stop_languagetool
  for s in "${STEPS[@]}"; do
    [ -n "${STATUS[$s]:-}" ] || STATUS[$s]="not run"
  done
  [ -s "$BLOCKS" ] && { cat "$BLOCKS"; echo; }
  summary | sed 's/ *$//' | tee "$LOGDIR/summary.txt"
  exit "$rc"
}

# The bundle's old value, which the save fixtures may hold: the one at HEAD, the one before step 1 (a manifest rewritten by hand before the
# loop), and every one an earlier run of the loop in this worktree saw or wrote into the fixtures (a run that failed after its refresh leaves
# the fixtures behind the manifest). VALUES keeps the last 50; it is in the loop's git-ignored folder.
VALUES=.claude/content-loop/bundle-values
OLD_START="$(bundle_value <"$GAME/manifest.json")"
OLD_HEAD="$(git show "HEAD:$GAME/manifest.json" 2>/dev/null | bundle_value 2>/dev/null)"
remember_value() {
  { [ -f "$VALUES" ] && cat "$VALUES"; printf '%s\n' "$@"; } | grep -E '^[0-9a-f]{64}$' | awk '!seen[$0]++' | tail -n 50 >"$VALUES.new"
  mv "$VALUES.new" "$VALUES"
}
remember_value "$OLD_HEAD" "$OLD_START"

# ---- 1. refresh
step_refresh() {
  begin refresh
  local log=01-refresh.log rc verdict sub
  run $log "$L" scripts/content.sh refresh
  rc=$?
  verdict="$(grep -E '^grammar: [0-9]+ errors' "$LOGDIR/$log" | tail -n 1)"
  if [ "$rc" -eq 0 ]; then
    end_step passed "every check clean; $verdict" $log
    return
  fi
  sub="$(grep '^== refresh: ' "$LOGDIR/$log" | tail -n 1 | sed 's/^== refresh: //; s/ (.*//')"
  if [ "$sub" = "grammar check" ]; then
    MISSES="$(grep -c '^error grammar.cache-miss ' "$LOGDIR/$log")"
    if [ "$MISSES" -gt 0 ] && [ "$grammar" -eq 1 ]; then
      end_step passed "every check clean but the offline grammar check: the cache lacks $MISSES of the strings (new Spanish, step 2 checks them)" $log
      return
    fi
    if [ "$MISSES" -gt 0 ]; then
      fail "the grammar cache lacks $MISSES of the strings (new Spanish)" \
        "run it again with --grammar (it starts LanguageTool, checks the new strings, prints the findings and stops the server)" $log
    fi
    block "refresh: the unwaived grammar warnings (the whole list is in $LOGDIR/$log):"
    section "$LOGDIR/$log" "== refresh: grammar check" | grep -E '^warning ' | head -n 20 | sed 's/^/  /' >>"$BLOCKS"
    fail "the offline grammar check: $verdict" \
      "read the warnings above: fix the content or add a waiver with its reason to $WAIVERS, then run the loop again" $log
  fi
  # The sub-step's findings when it prints lint-style lines (error, warning), else its first lines.
  block "refresh: the first problems of '$sub' (the whole output is in $LOGDIR/$log):"
  section "$LOGDIR/$log" "== refresh: $sub" | grep -v '^ *$' >"$LOGDIR/refresh-problems.txt"
  { grep -E '^(error|warning) ' "$LOGDIR/refresh-problems.txt" || cat "$LOGDIR/refresh-problems.txt"; } | head -n 12 | sed 's/^/  /' >>"$BLOCKS"
  fail "failed at '$sub'" "fix what the log reports under '== refresh: $sub' (the first lines are above), then run the loop again" $log
}

# ---- 2. grammar
step_grammar() {
  if [ "$grammar" -eq 0 ]; then
    skip grammar "no --grammar (the offline check passed, so no string is new)"
    return
  fi
  begin grammar
  local log=02-grammar.log prune=02-grammar-prune.log rebuild=02-grammar-rebuild.log rc cache verdict fresh equal=1
  LT_UP=1
  if ! run $log scripts/languagetool.sh start; then
    fail "the LanguageTool server did not start: $(grep 'languagetool.sh:' "$LOGDIR/$log" | tail -n 1)" \
      "install it once (scripts/fetch-tools.sh) or free memory (it needs 4 GB), then run it again with --grammar" $log
  fi
  progress "grammar: the strings the cache lacks, with --prune"
  run $prune "$L" scripts/content.sh grammar "$GAME" --strings "$STRINGS" --strings-only --prune
  rc=$?
  # "cache: 1003 entries, 2 new, 0 pruned; wrote <path>" as "cache 1003 entries, 2 new, 0 pruned (wrote)"
  cache="$(grep -E '^cache: ' "$LOGDIR/$prune" | tail -n 1 | sed -E 's/^cache: /cache /; s/; (wrote|unchanged) .*/ (\1)/')"
  if [ "$rc" -eq 3 ] || [ -z "$cache" ]; then
    fail "the check with --prune did not run: $(tail -n 1 "$LOGDIR/$prune")" "read $LOGDIR/$prune, then run it again with --grammar" $log $prune
  fi
  # A new finding is one the online run prints and the offline run of step 1 did not (that one printed a cache miss for each new string).
  section "$LOGDIR/01-refresh.log" "== refresh: grammar check" | grep -E "$FINDING" | sort -u >"$LOGDIR/grammar-offline.txt"
  grep -E "$FINDING" "$LOGDIR/$prune" | sort -u >"$LOGDIR/grammar-online.txt"
  comm -13 "$LOGDIR/grammar-offline.txt" "$LOGDIR/grammar-online.txt" >"$LOGDIR/grammar-new-findings.txt"
  fresh="$(wc -l <"$LOGDIR/grammar-new-findings.txt")"
  if [ "$fresh" -gt 0 ]; then
    block "grammar: $fresh new findings. Read each: fix the content, or add a waiver with its reason to $WAIVERS (never waive a true positive):"
    head -n 60 "$LOGDIR/grammar-new-findings.txt" | sed 's/^/  /' >>"$BLOCKS"
    [ "$fresh" -gt 60 ] && block "  (+$((fresh - 60)) more in $LOGDIR/grammar-new-findings.txt)"
  fi
  # CI's languagetool job: the cache rebuilt from nothing must equal the one that is committed (here, the one just pruned).
  progress "grammar: the cache rebuilt from nothing"
  cp "$CACHE" "$LOGDIR/cache.pruned.json"
  rm -f "$CACHE"
  run $rebuild "$L" scripts/content.sh grammar "$GAME" --strings "$STRINGS" --strings-only --strict
  rc=$?
  stop_languagetool
  verdict="$(grep -E '^grammar: [0-9]+ errors' "$LOGDIR/$rebuild" | tail -n 1 | sed -E 's/^grammar: //; s/, [0-9]+ unchecked$//')"
  if [ "$rc" -eq 3 ] || [ ! -f "$CACHE" ]; then
    cp "$LOGDIR/cache.pruned.json" "$CACHE"
    fail "the rebuild from nothing did not run (the pruned cache is put back): $(tail -n 1 "$LOGDIR/$rebuild")" \
      "read $LOGDIR/$rebuild, then run it again with --grammar" $log $prune $rebuild
  fi
  if ! cmp -s "$CACHE" "$LOGDIR/cache.pruned.json"; then
    equal=0
    diff -u "$LOGDIR/cache.pruned.json" "$CACHE" >"$LOGDIR/02-grammar-rebuild.diff"
  fi
  if [ "$rc" -ne 0 ]; then
    fail "$cache; $fresh new findings; rebuilt from nothing: $verdict (unwaived)" \
      "read the new findings above: fix the content or add a waiver with its reason to $WAIVERS, then run it again with --grammar" $log $prune $rebuild
  fi
  if [ "$equal" -eq 0 ]; then
    fail "the cache rebuilt from nothing differs from the pruned one (CI's languagetool job would fail); the rebuilt one is kept" \
      "read $LOGDIR/02-grammar-rebuild.diff (a result that is not the same twice is a LanguageTool or cache fault), then run it again with --grammar" \
      $log $prune $rebuild
  fi
  end_step passed "$cache; $fresh new findings; rebuilt from nothing: equal, $verdict" $log $prune $rebuild
}

# ---- 3. speech
step_speech() {
  begin speech
  local check=03-speech-check.log log=03-speech.log again=03-speech-refresh.log rc line files would wavs stale checked rendered
  run $check "$L" scripts/prerender-speech.sh --check
  rc=$?
  line="$(grep -E '^renderer: ' "$LOGDIR/$check" | tail -n 1)"
  if [ "$rc" -gt 1 ] || [ -z "$line" ]; then
    fail "the renderer's check failed: $(tail -n 1 "$LOGDIR/$check")" "fix what $LOGDIR/$check reports, then run the loop again" $check
  fi
  files="$(sed -E 's/.* ([0-9]+) files;.*/\1/' <<<"$line")"
  would="$(sed -E 's/.*; ([0-9]+) would be rendered.*/\1/' <<<"$line")"
  # --check does not count the WAVs of lines that are gone; a render deletes them.
  wavs="$(find "$GAME/speech" -name '*.wav' | wc -l)"
  stale=$((wavs > files ? wavs - files : 0))
  checked="check: $would of $files files to render"
  [ "$stale" -gt 0 ] && checked="$checked, $stale stale WAVs to delete"
  progress "speech: $checked"
  if [ "$would" -eq 0 ] && [ "$stale" -eq 0 ]; then
    end_step passed "$checked" $check
    return
  fi
  if [ "$speech" -eq 0 ]; then
    block "speech: the files to render (the whole list is in $LOGDIR/$check):"
    grep -E '^would render: ' "$LOGDIR/$check" | head -n 10 | sed 's/^/  /' >>"$BLOCKS"
    fail "$checked" "run it again with --speech (it renders them, then refreshes the bundle)" $check
  fi
  progress "speech: rendering"
  if ! run $log "$L" scripts/prerender-speech.sh; then
    fail "$checked; the render failed: $(tail -n 1 "$LOGDIR/$log")" "fix what $LOGDIR/$log reports, then run the loop again" $check $log
  fi
  rendered="$(grep -E '^renderer: ' "$LOGDIR/$log" | tail -n 1 | sed -E 's/.*files; //')"
  # The render wrote WAVs, the index and provenance: the manifest, the strings (the speech index is one of their sources) and the checks again.
  progress "speech: refresh again"
  if ! run $again "$L" scripts/content.sh refresh; then
    if grep -q '^error grammar.cache-miss ' "$LOGDIR/$again"; then
      fail "$checked; $rendered; the refresh after it misses the grammar cache" \
        "the speech index added Spanish the cache lacks: run it again with --grammar" $check $log $again
    fi
    fail "$checked; $rendered; the refresh after it failed" "fix what $LOGDIR/$again reports, then run the loop again" $check $log $again
  fi
  end_step passed "$checked; $rendered; refreshed again" $check $log $again
}

# ---- 4. fixtures
step_fixtures() {
  begin fixtures
  local new f was
  local -a files
  new="$(bundle_value <"$GAME/manifest.json")"
  grep -vxF "$new" "$VALUES" >"$LOGDIR/old-values.txt"
  sed 's|.*|s/&/'"$new"'/g|' "$LOGDIR/old-values.txt" >"$LOGDIR/old-values.sed"
  mapfile -t files < <(grep -rlF --exclude-dir=bin --exclude-dir=obj -f "$LOGDIR/old-values.txt" tests/)
  if [ "${#files[@]}" -gt 0 ]; then
    was="$(grep -ohF -f "$LOGDIR/old-values.txt" "${files[@]}" | sort -u | cut -c1-8 | sed 's/$/…/' | paste -sd' ')"
    for f in "${files[@]}"; do
      sed -i -f "$LOGDIR/old-values.sed" "$f"
    done
    remember_value "$new"
    end_step passed "bundle $was -> ${new:0:8}…"
  elif grep -rqF --exclude-dir=bin --exclude-dir=obj "$new" tests/; then
    remember_value "$new"
    end_step passed "bundle ${new:0:8}…, which the fixtures hold already"
  else
    fail "no file under tests/ holds the bundle's new value (${new:0:8}…) or an old one this worktree has had" \
      "put $new into the save fixture that pins it (tests/Vocaterra.Game.Core.Tests/Fixtures/save-v10.json) by hand, then run the loop again"
  fi
}

# ---- 5. build
step_build() {
  if [ "$tier" = none ] && [ "$record" -eq 0 ]; then
    skip build "--tier none"
    return
  fi
  begin build
  local log=05-build.log
  if ! run $log "$L" dotnet build Vocaterra.sln -warnaserror -m:4 -v:q -nologo; then
    block "build: the first errors (the whole output is in $LOGDIR/$log):"
    grep -E ': (error|warning) ' "$LOGDIR/$log" | sort -u | head -n 10 | sed 's/^/  /' >>"$BLOCKS"
    fail "the build failed" "fix the errors above, then run the loop again" $log
  fi
  end_step passed "Vocaterra.sln with warnings as errors: clean" $log
}

# ---- 6. goldens
# What moved between the copy taken before the step and the files now: per file, the entries (by id) or the JSON values (by path).
golden_moves() {
  python3 -I - "$LOGDIR/goldens-before" "$GOLDEN_DATA" "$LOGDIR/goldens-moved.txt" <<'PY'
import json, os, sys

before, after, listing = sys.argv[1:4]

def files(root):
    return {os.path.relpath(os.path.join(d, n), root) for d, _, names in os.walk(root) for n in names}

def leaves(value, path=""):
    if isinstance(value, dict):
        for key, item in value.items():
            yield from leaves(item, f"{path}.{key}" if path else key)
    elif isinstance(value, list) and value:
        for i, item in enumerate(value):
            yield from leaves(item, f"{path}[{i}]")
    else:
        yield path, json.dumps(value, ensure_ascii=False)

def entries(path):
    if not os.path.exists(path):
        return {}
    text = open(path, encoding="utf-8").read()
    if path.endswith(".jsonl"):
        rows = {}
        for i, line in enumerate(l for l in text.split("\n") if l.strip()):
            doc = json.loads(line)
            rows[doc["id"] if isinstance(doc, dict) and "id" in doc else f"line {i + 1}"] = line
        return rows
    return dict(leaves(json.loads(text)))

parts, lines, moved_all = [], [], []
for rel in sorted(files(before) | files(after)):
    a, b = entries(os.path.join(before, rel)), entries(os.path.join(after, rel))
    new = sorted(set(b) - set(a))
    gone = sorted(set(a) - set(b))
    moved = sorted(k for k in set(a) & set(b) if a[k] != b[k])
    if not (new or gone or moved):
        continue
    keys = new + moved + gone
    parts.append(f"{rel} {len(new)} new, {len(moved)} moved, {len(gone)} removed")
    lines.append(f"  {rel}: {len(new)} new, {len(moved)} moved, {len(gone)} removed: " + ", ".join(keys[:6]) + (" ..." if len(keys) > 6 else ""))
    moved_all += [f"{rel} new {k}" for k in new] + [f"{rel} moved {k}" for k in moved] + [f"{rel} removed {k}" for k in gone]
with open(listing, "w", encoding="utf-8") as out:
    out.write("".join(line + "\n" for line in moved_all))
for line in lines:
    print(line, file=sys.stderr)
print("; ".join(parts))
PY
}

step_goldens() {
  if [ "$tier" = none ] && [ "$record" -eq 0 ]; then
    skip goldens "--tier none"
    return
  fi
  begin goldens
  local log=06-goldens.log build=06-goldens-build.log check=06-goldens-check.log moved names
  rm -rf "$LOGDIR/goldens-before"
  cp -r "$GOLDEN_DATA" "$LOGDIR/goldens-before"
  if [ "$record" -eq 0 ]; then
    if ! run $log scripts/test.sh project Vocaterra.Nlp.Golden.Tests --no-build; then
      names="$(failures "$LOGDIR/$log")"
      fail "moved or failed: ${names:-see the log}" \
        "if the edit means to move them, run it again with --record (the goldens go in a commit of their own); if not, fix the edit" $log
    fi
    end_step passed "unmoved (the end-to-end goldens and the identity baseline): $(counts "$LOGDIR/$log")" $log
    return
  fi
  if ! run $log env VOCATERRA_RECORD_GOLDEN=1 VOCATERRA_RECORD_IDENTITY=1 scripts/test.sh project Vocaterra.Nlp.Golden.Tests --no-build; then
    names="$(failures "$LOGDIR/$log")"
    fail "the recording run failed: ${names:-see the log}" "fix what $LOGDIR/$log reports, then run the loop again" $log
  fi
  moved="$(golden_moves 2>"$LOGDIR/goldens-moved-lines.txt")"
  if [ -z "$moved" ]; then
    end_step passed "recorded: nothing moved" $log
    return
  fi
  GOLDENS_MOVED=1
  block "goldens: recorded; what moved (every entry is in $LOGDIR/goldens-moved.txt; commit them in a commit of their own):"
  git diff --stat -- "$GOLDEN_DATA" | sed 's/^/  /' >>"$BLOCKS"
  cat "$LOGDIR/goldens-moved-lines.txt" >>"$BLOCKS"
  # The tests read the goldens from their output folder: build again to copy them, then check them as recorded.
  if ! run $build "$L" dotnet build "$GOLDEN_PROJECT" -warnaserror -m:4 -v:q -nologo || ! run $check scripts/test.sh project Vocaterra.Nlp.Golden.Tests --no-build; then
    fail "recorded ($moved), but the check after it failed" "read $LOGDIR/$check, then run the loop again" $log $build $check
  fi
  end_step passed "recorded: $moved; checked again" $log $build $check
}

# ---- 7. digests
step_digests() {
  if [ "$tier" = none ] && [ "$record" -eq 0 ]; then
    skip digests "--tier none"
    return
  fi
  begin digests
  local log=07-digests.log pairs actual recorded name names=""
  if run $log scripts/test.sh project Vocaterra.Nlp.Fuzzing.Tests --no-build --filter "$STREAM_FILTER"; then
    end_step passed "unmoved: $(counts "$LOGDIR/$log")" $log
    return
  fi
  pairs="$(grep -oE 'The stream digest is [0-9A-F]{64}, not the recorded [0-9A-F]{64}' "$LOGDIR/$log" | sort -u)"
  if [ -z "$pairs" ]; then
    names="$(failures "$LOGDIR/$log")"
    fail "FuzzLanguageStreamTests failed, not on a digest: ${names:-see the log}" "fix what $LOGDIR/$log reports, then run the loop again" $log
  fi
  block "digests: the fuzz suite's stream digests moved. They run on frozen inputs (the core's M57), so the content did not move them:"
  block "  a generator or the fixture changed. If that change means to move them, set them in $STREAM_TEST (this script edits no C#)"
  block "  and say in the comment above them which change moved them:"
  while read -r _ _ _ _ actual _ _ _ recorded; do
    name="$(grep -oE "const string [A-Za-z]+ = \"$recorded\"" "$STREAM_TEST" | awk '{ print $3 }')"
    block "  private const string ${name:-<the constant that holds $recorded>} = \"$actual\";   (was ${recorded:0:12}…)"
    names="${names:+$names, }${name:-?}"
  done <<<"$(sed 's/,//' <<<"$pairs")"
  if [ "$record" -eq 1 ]; then
    DIGESTS_TODO=1
    end_step "to do" "moved: $names (printed above); the tests below leave FuzzLanguageStreamTests out until they are set" $log
    return
  fi
  fail "moved: $names (printed above); a content edit does not move them since M57" \
    "find the change outside the content that moved them (a generator, the fixture); if it means to, set them in FuzzLanguageStreamTests.cs" $log
}

# ---- 8. tests
step_tests() {
  if [ "$tier" = none ]; then
    skip tests "--tier none"
    return
  fi
  begin tests
  local fast=08-tests-fast.log fuzz=08-tests-fuzz.log full=08-tests-full.log names args=()
  if [ "$tier" = full ]; then
    [ "$DIGESTS_TODO" -eq 1 ] && args=(--filter "FullyQualifiedName!~.FuzzLanguageStreamTests.")
    if ! run $full scripts/test.sh full --no-build "${args[@]}"; then
      names="$(failures "$LOGDIR/$full")"
      fail "full: $(counts "$LOGDIR/$full"); failed: ${names:-see the log}" "read the failures above, fix them, then run the loop again" $full
    fi
    end_step passed "full: $(counts "$LOGDIR/$full")" $full
    return
  fi
  if ! run $fast scripts/test.sh fast --no-build; then
    names="$(failures "$LOGDIR/$fast")"
    fail "fast: $(counts "$LOGDIR/$fast"); failed: ${names:-see the log}" "read the failures above, fix them, then run the loop again" $fast
  fi
  progress "tests: the fuzz suite's grammar-sensitive classes"
  if ! run $fuzz scripts/test.sh project Vocaterra.Nlp.Fuzzing.Tests --no-build --filter "$FUZZ_FILTER"; then
    names="$(failures "$LOGDIR/$fuzz")"
    fail "fast: $(counts "$LOGDIR/$fast"); fuzz: $(counts "$LOGDIR/$fuzz"); failed: ${names:-see the log}" \
      "a fuzz oracle failed: read its counterexample above; fix the content (a grammar that generates a wrong sentence), never the oracle" $fast $fuzz
  fi
  end_step passed "fast: $(counts "$LOGDIR/$fast"); fuzz classes: $(counts "$LOGDIR/$fuzz")" $fast $fuzz
}

step_refresh
step_grammar
step_speech
step_fixtures
step_build
step_goldens
step_digests
step_tests

if [ "$DIGESTS_TODO" -eq 1 ]; then
  NEXT="set the digests printed above in FuzzLanguageStreamTests.cs, then run scripts/test.sh project Vocaterra.Nlp.Fuzzing.Tests --filter FullyQualifiedName~FuzzLanguageStreamTests"
  finish_run 1
fi
NEXT="commit the edit with the files the steps changed (git status)"
[ "$GOLDENS_MOVED" -eq 1 ] && NEXT="$NEXT; the re-recorded goldens ($GOLDEN_DATA) in a commit of their own"
[ "$tier" = fast ] && NEXT="$NEXT; scripts/test.sh full before the milestone ends"
finish_run 0
