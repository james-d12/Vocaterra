# Milestone briefs

How the orchestrator briefs a subagent for one milestone: the template, what a content milestone adds, the rules every brief carries
and the repository facts a brief may need. A brief is sent inline in the Agent prompt, never left in a scratchpad. The briefs that
were used are kept in `.claude/briefs/` (for example `v13-como-es.md`, `m70-qa-core-findings.md`) and are the models for the next
one: fill their `@@…@@` placeholders from the last reports before launching.

## The template

```
You are implementing <milestone id and name> for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp,
the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the engine-free
logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in <the core or the game>.
Where to work: <the worktree>. First `git fetch origin` and `git checkout -b <branch> origin/<feature branch>`. Commit after each item that
builds and passes its tests, with the prefix `<id>: `. Do not push your branch: it stays local and the gate merges it from there (owner,
2026-11-05). Never push or merge <feature branch> or main. When you finish, leave the worktree on your branch, clean.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
speech prerender and screenshot run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly. Never start two heavy
commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f` (it matches its own
command line and never ends). While iterating run `scripts/test.sh fast` or `scripts/test.sh project <test project>`; run
`scripts/test.sh full` (the whole suite, as the gate) once before reporting. Exit 137 means the 6 GB cap killed it: lower the
parallelism, do not work around the cap.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
or a CLI `eval` run use a wrapper script outside the repository). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.
Read first: CLAUDE.md; <the plan: docs/game/implementation-plan.md sections 1, 3, 4 (your row) and 6, or docs/nlp/implementation-plan.md
§3, §4, §6>; <the milestone's section>; <docs/game/architecture.md and docs/game/decisions.md, or docs/nlp/changing-the-core.md>;
docs/nlp/api.md; <the files to change>. Read the sections named, never whole long files.
Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is
(a missing core call stops that part: report it, do not copy core source or edit a Vocaterra.Nlp project or docs/nlp/); a core milestone
edits only the Vocaterra.Nlp projects, their tests, tests/golden and docs/nlp/ (and content/game-v1 only where the core's tests need it,
never the game's lessons), fixes bugs at the source with a regression test in the owning module and never weakens a fuzz oracle. A
regression check is a C# test, never a script with text baselines. Never create or edit anything under .github/workflows/ unless this
brief names the file; do not edit either implementation-plan.md or either decisions.md (the orchestrator does); engine code only in
src/Vocaterra.Game (D-game-1); assets CC0 only (D-game-5); no System.Random and no clock in the evaluation path.
<For a content milestone: the block "What a content milestone adds" below.>
<For a core milestone: before reporting, run the CLI on a few natural, correct lines at the NPCs' prompts (names, greetings,
usted/ustedes), not only the brief's lines.>
Deliverables: <numbered list>.
Gate (from the repository root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(<current count> passed); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean; scripts/limited.sh scripts/content.sh
validate passes; <core: bash scripts/license-scan.sh --offline PASSED>; the demo lines give the stated output. Do not commit bin/, obj/
or artifacts/.
Commit messages end with
Co-Authored-By: Claude <model> <noreply@anthropic.com>
Claude-Session: <session url>
Report, one page: branch, commit SHA, worktree path; what was built; test counts; measurements; bugs found and fixed (where, symptom,
fix, test); core gaps found by a game milestone (what call, why); known-not-fixed with reasoning; files touched outside the milestone's
area; anything left undone and why. <Name what to leave out.>
```

## What a content milestone adds

The v12 and v13 briefs are the models.

- The earlier lessons to follow.
- The core tried first, through a CLI wrapper script. A wrong grade is a core gap (M56 and on), never patched in the game.
- An Opus pass over the new Spanish before the WAVs. The orchestrator also reads the new Spanish lines in the diff before merging.
- The coin path.
- The scripted runs captured before and after.
- `--skip-to-session n` for the lesson's timing run.
- **The content loop in one line** (V18): `scripts/content-loop.sh --grammar --speech` after every content edit (run directly, never
  under `limited.sh`, which it refuses with exit 3), `--record` when the goldens move, and `--tier full` once before the report. It
  replaces the hand steps (refresh, the LanguageTool cache and its rebuild check, the prerender, the fixtures' hash); since M57 the
  stream digests run on frozen inputs and do not move with content. Read every grammar finding it prints. The steps are in
  `content/README.md`, "The edit loop". Keep the Wiktionary notice in `ATTRIBUTION.md`; one bundle since R02, so a content edit also
  runs the core suite, and a failing core test means the edit broke something the core relies on.
- **Lean context** (a milestone's cost is its turns times its context; `docs/research/2026-10-31-development-bottlenecks.md`, P1): name
  the sections to read, never "all of it"; never `speech-checklist.md`, `manifest.json` or the whole of `PHRASES.md` (grep the rows);
  heavy output to log files, at most 30 lines printed; `sed -n` ranges under about 150 lines; a one-page report.
- **The learner's route** (P4): sleep into the session, the Lessons page's `next:`, the objective and the beacon, the lesson, then free
  talk once with each NPC of the lesson. Spot-check the route from a new world, not only `--lesson-run`: phase A's lessons were
  unreachable in practice until V12c.
- **CI's case** (P5): the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder), and any wall-clock test within 4 times
  of its budget reported.

## Repository facts a brief may need

Every brief says to read `CLAUDE.md` first; these are the facts it does not hold.

- One solution, `Vocaterra.sln`, with project references from `src/Vocaterra.Game.Core` and `src/Vocaterra.Host` to the one engine
  project `src/Vocaterra.Nlp` (M52) and the Spanish pack `src/Vocaterra.Nlp.Es` (M53; load a Spanish bundle with `SpanishContent.Load`,
  since `LanguagePacks.Default` is empty). `Vocaterra.Nlp.slnf` builds the core alone. Package versions are in the root
  `Directory.Packages.props`; no new package outside it.
- The host (`src/Vocaterra.Host`) is a console program: `--transcript "<line>" --prompt <id>` evaluates once; no argument runs the text
  loop.
- The one content bundle `content/game-v1` is hashed through `scripts/content.sh` (the core's ContentBuilder run from source); the core's
  tests, fuzz suite and demo CLI run on it (R02). Lexicon regeneration is in `docs/nlp/lexicon.md`.
- Determinism tiers (core plan §3): no `System.Random`, no wall clock, ordinal comparisons, `CanonicalJson`, seeded RNG, margins for
  model thresholds; model adapters gated on `VOCATERRA_MODELS_DIR`.
- The fuzz suite (`tests/Vocaterra.Nlp.Fuzzing.Tests`, design in `docs/nlp/fuzzing-plan.md`) is a merge gate and has found every real
  bug so far; its README lists bugs found, known limits and narrowed oracles.
- Licence scan: `scripts/license-scan.sh --offline`; the allowlist refuses copyleft code dependencies; CC BY-SA is accepted for derived
  data.
- The core demo, `dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open
  --content content/game-v1`, must keep yielding exactly `conj.person` and `noun.number`; the design document's own sentence "yo tiene
  dos espada" is the pipeline test `TheDesignDocumentsWorkedExampleGivesExactlyConjPersonAndNounNumber`.
- Native assets: on Linux x64 the game builds for linux-x64 only (G7, the non-core group of the root `Directory.Build.props`);
  `-p:VocaterraAllRuntimes=true` on restore and build gives every platform. The game's executables and test projects that run speech
  reference `Vosk` directly and `Microsoft.ML.OnnxRuntime` with `ExcludeAssets="native"` (CR-021); keep both.
- Disk: each full build writes several GB of native-package output per checkout; a worktree is removed after its milestone merges.
