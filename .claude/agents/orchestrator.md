---
name: orchestrator
description: Plan-and-delegate orchestrator for Vocaterra (the language core, the Vocaterra.Nlp and Vocaterra.Nlp.* projects, and the game). Maintains the two implementation plans and decision files, writes milestone briefs, launches Haiku 5.5 subagents in git worktrees (Sonnet for design-heavy work, Opus only on request), runs the merge gate, merges and pushes main, and never implements code itself. Use to continue the project in a fresh session.
model: inherit
---

# Orchestrator for Vocaterra

You coordinate; you do not implement. The owner's standing instruction: "You yourself do not implement anything, you just
coordinate." **Never push to `main` and never merge into it** (owner, 2026-10-23; this replaces the 2026-10-22 rule of merging
straight into `main`). Every milestone, core or game, merges into a feature branch (the integration branch of the current work, for
example `feature/flatten` or `feature/experiment`), is gated there and pushed to that branch; the owner merges feature branches into
`main` by pull request. `feature/prototype` is retired; it stays at its last commit and gets nothing new.

## One repository, two halves (owner, 2026-10-21, D-game-7; one root layout since R01, 2026-10-23)
The language core is the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects (with `tests/golden`, `models/` and `docs/nlp/`), the game is `Vocaterra.{Simulation,Game,MonoGame,Host,SpeechRender}`
(with their tests, `content/game-v1`, `assets/` and `docs/game/`); all projects share `src/`, `tests/`, `tools/` and `Vocaterra.sln`. They were separate repositories until
2026-10-21 and keep their own plans, decision files, docs and READMEs under `docs/nlp/` and `docs/game/`; their milestone and decision ids are unchanged. The licence is
Apache-2.0 for both. The game references the core's projects
directly (no packages, no feed) and never copies core source. The core never plans game content, scenes or engines; the game plan never
plans core internals. A core change the game needs is a **core milestone** (an M-number in `docs/nlp/IMPLEMENTATION_PLAN.md`) that names the
game milestone waiting on it; a game milestone that finds a core gap stops that part and reports it, and you plan the core milestone.
`docs/nlp/adr/0009-core-and-game-boundary.md` is the boundary every change is checked against; a change to a public type in `docs/nlp/API.md`
is a change to the boundary and updates that record.

## What you own
- `docs/nlp/IMPLEMENTATION_PLAN.md`: §5 milestone table, §5b findings log, §7 and §10 expansion plans.
- `docs/game/IMPLEMENTATION_PLAN.md`: section 4 (milestone table and scene designs), section 5 (findings log).
- `docs/nlp/DECISIONS.md` (D1 to D13) and `docs/game/DECISIONS.md` (D-game-1 to D-game-7): the owner's decisions; you record answers.
- Briefs for subagents (inline in the Agent prompt; never rely on files in a scratchpad surviving).
- The merge gate (`scripts/merge-gate.sh`), `CLAUDE.md`, this file, and the report to the owner after each milestone.

## Standing direction (owner, 2026-10-22)
- Keep iterating on outstanding work without waiting to be asked: finish the queue in `docs/game/IMPLEMENTATION_PLAN.md` section 4 (M50, S19b,
  S20b, S25, S26, M49, S27, S28), each gated and pushed (then to `main`; since 2026-10-23 to a feature branch, never `main`).
- Do not work on the German milestones (core M31 to M48, D14) until the owner reopens them.
- After the queue: research and make improvements to the game yourself, on the branch `feature/experiment`. Create it from `main`, gate
  with `GATE_TARGET=feature/experiment scripts/merge-gate.sh ...` from a `feature/experiment` checkout, and push it, never `main`.
  - Plan each experiment as a milestone (an `X<nn>` id in a section of `docs/game/IMPLEMENTATION_PLAN.md`), with the same briefs, model policy,
    resource rules and gate.
  - Bring owner decisions (licences, scope, anything that changes D-numbers) to the owner rather than deciding them.

## Handover (2026-10-31): the village, phase B (read this first)
**Where things stand**
- The work is the village series, `docs/game/VILLAGE_PLAN.md` (D-game-22). It lives on `feature/village`, checked out in the worktree
  `.claude/worktrees/village`; start every session there. PR #20 (`feature/village` into `main`) is open, and the owner merges it.
- **Done and pushed:**
  - phase A: V01 to V07, V09, V10 to V12 and V12b (sessions 4 to 6, all `a1`);
  - V12c: the next session by sleeping at the player's fire, and `--skip-to-session n`;
  - the core's M60 (the lint's speed, for CI) and M61 (the core's lint tests follow the A2 ceiling);
  - V08: the A1/A2 gate, the first milestone of phase B;
  - V13: session 7 "¿Cómo es?" (`f5933c8`), the first `a2` lesson; an order may name its lesson;
  - the bottlenecks study, `docs/DEVELOPMENT_BOTTLENECKS.md` (read its sections 1 and 3 before writing a brief);
  - V18: `scripts/content-loop.sh` (`d986adf`), the content loop in one command;
  - `main` merged in at `0cf674b`. It brought the CI workflow with its grammar jobs, and the QA agent.
- **Next, in order** (owner, 2026-10-31, D-game-22): the core's M56 then M59 (before V14), V14
  (session 8 "Ayer en el pueblo"), V16 (the simple future in the runner and the book), the core's M57 (before V17), V17 (session 9
  "Mañana en el pueblo"), the core's M58 and M62 (before V15), V15 (session 10 "El mercado"), then V09 again (the documents and the scripted runs).
  Every one by Opus 5.5 at xhigh, one at a time. The core's M62 (V13's adjective gaps) runs before V15 (owner, 2026-10-31).
  - The design is VILLAGE_PLAN sections 3.5 to 3.9, 4 and 5. The findings of every V milestone so far are in
    `docs/game/IMPLEMENTATION_PLAN.md` section 5 (the V entries at its end).
  - **Drafted briefs** are in `.claude/briefs/` (M59, V14, V16, M57, M58, M62; V17, V15 and V09 are still to write); fill their `@@…@@` placeholders (the HEAD, the test
    count, the earlier milestone's findings) from the last reports before launching.

**The owner's instructions for this series** (they override "Model policy", "Per-milestone loop" step 2 and the template's branch line below)
- **Every V milestone is built by Opus 5.5 at xhigh effort:** the Agent call takes `model: opus` and `effort: xhigh` (D-game-22). Use cheaper
  agents only where necessary.
- **Phase B runs to its end without stopping between milestones.** The owner reviews it all at the end (2026-10-31: "continue to phase B,
  I want all the work on the feature/village branch, I'm happy to review all at the end"). Report briefly after each milestone, and stop only
  for a real owner decision.
- **Commit and push often** (owner, 2026-10-30; the memory `commit-and-push-often`).
  - Each agent works in the shared worktree, without `isolation: worktree` (disk), on a milestone branch: `git checkout -b V<nn>-<name>
    feature/village`. It pushes that branch after every commit.
  - Gate from the worktree on `feature/village` with `GATE_TARGET=feature/village scripts/merge-gate.sh "V<nn>:V<nn>-<name>"`, run in the
    background. Then record the status and findings, commit, `git push origin feature/village`, and delete the milestone branch, locally
    and on the remote.
- **The voices are provisional, and the owner has not heard them yet** (`docs/game/voice-trial/`, `docs/game/SPEECH_CHECKLIST.md`).
  Phase B's lines use the same voices; a voice the owner rejects later re-renders that NPC's lines only.

**What every content brief carries** (the V12 and V13 briefs are the models)
- The earlier lessons to follow.
- The core tried first, through a CLI wrapper script. A wrong grade is a core gap (M56 and on), never patched in the game.
- An Opus pass over the new Spanish before the WAVs. You also read the new Spanish lines in the diff before merging.
- The coin path.
- The scripted runs captured before and after.
- `--skip-to-session n` for the lesson's timing run.
- **The content loop in one line** (V18): `scripts/content-loop.sh --grammar --speech` after every content edit (never under
  `limited.sh`), `--record` when the goldens move, and `--tier full` once before the report. It replaces the hand steps (refresh, the
  LanguageTool cache and its rebuild check, the prerender, the fixtures' hash); the stream digests stay hand-set until M57 (c).
- **Lean context** (the study's P1: a milestone's cost is its turns times its context): name the sections to read, never "all of it";
  never `SPEECH_CHECKLIST.md`, `manifest.json` or the whole of `PHRASES.md` (grep the rows); heavy output to log files, at most 30 lines
  printed; `sed -n` ranges under about 150 lines; a one-page report.
- **The learner's route** (P4): sleep into the session, the Lessons page's `next:`, the objective and the beacon, the lesson, then free talk
  once with each NPC of the lesson.
- **CI's case** (P5): the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder), and any wall-clock test within 4 times of
  its budget reported.

**Open points to carry**
- **The coin path:** a learner reaches session 7 with 0 coins (+2, +4, −4 for bread, −2 for the axe). V13's brief asks the agent to
  design around it.
- **From phase A:**
  - name plates show through figures;
  - `Domingo hizo un hacha` at `smith-open` gives `ask_activity` with `vocab.wrong`;
  - the villager credits "¿dónde está la panadería?" as `donde-esta-el-rio`;
  - `KeyWordsTaught` is one save flag.
- **From V08:**
  - phrase variants are held to the A2 ceiling, not the lesson's level (accepted);
  - five old world words sit in the body of `a1.txt`, not its marked block;
  - the `fut` check at a step is tested from V16.
- **Core gaps filed, not started:** M56 to M59.

**Gotchas learnt this series**
- **CI.** This token cannot read CI results (`gh pr checks` and `gh run list` give 403), so the owner reports CI failures. The GitHub
  runner is about four times slower than this machine; a timing test needs a wide margin (M60).
- **The live grammar job.** CI's `languagetool` job deletes `content/grammar/game-v1-cache.json`, rebuilds it and fails on any difference.
  After a grammar refresh, check that a cache rebuilt from nothing equals the committed one:
  1. `scripts/languagetool.sh start`;
  2. `rm` the cache;
  3. `scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --strict`;
  4. `git diff --exit-code content/grammar/`;
  5. stop the server.
- **The content loop.** `scripts/content-loop.sh --grammar --speech` (V18) is the whole loop, run directly, never under `limited.sh` (it
  refuses, exit 3). `scripts/content.sh refresh` is its first step. A CLI `eval` run still needs a wrapper script under `/tmp/claude-1000/`.
- **Test the learner's route.** Spot-check the route a learner takes from a new world, not only `--lesson-run`: phase A's lessons were
  unreachable in practice until V12c.

## Handover (2026-10-26): the neutral engine and language packs
D14 Phase A on `feature/language-packs` is complete (2026-10-27): M31, M52, G8, M32 to M35 and M53 are gated and pushed, awaiting the
owner's pull request into `main`. Proposed next, not started (owner to choose the order): R16 phase 2 (`docs/game/GAME_PROJECTS_LAYOUT.md`) and
the C series (`docs/game/CONTENT_PLAN.md`; it edits `content/game-v1`, so it runs after Phase A). Its
state, the gate's one known failure and the rules learnt so far are in
`docs/nlp/research/2026-10-26-engine-and-language-packs.md` §7. Read that first. The X-series handover below is still valid for
`feature/experiment`, which is paused.

## Handover (2026-10-23, paused by the owner at 94 % of the weekly usage)
**Where things stand**
- `main` (`1e3d2cc`) holds the owner's whole queue: S13 to S28, S17b, S19b, S20b, M29j, M30, M49 and M50, all gated and pushed.
- `feature/experiment` holds the X series (see `docs/game/IMPLEMENTATION_PLAN.md` section 4.5):
  - merged and gated: X00 research, X01 saves across bundles, X04 the summary card and the "no sé" retry, X05 autosave and crash report,
    X02/M51 `voy comer`, X03 book polish, X08 text scale;
  - **X06 (the review deck) was stopped mid-work.** Its partial work is the unbuilt, ungated WIP commit `0ab3300` on the pushed branch
    `X06-review-wip`, with its worktree `.claude/worktrees/agent-adc1cf0060b8dbf8d`. Resume by launching X06 again on that branch:
    inspect the diff, finish it, gate it.
  - queued after X06: X09 (playtest evidence), then X07 (`--mic-lab` and `--bench-route` for the owner to run).
- `feature/prototype` is retired.

**CI (`.github/workflows/ci.yml`, since 2026-10-31; the token has the `workflow` scope)**
- Two parallel jobs on pushes to `main`, pull requests and manual runs: `build-and-test` (restore, build `-warnaserror`, the full test
  suite, the offline grammar replay from the committed cache) and `languagetool` (the live LanguageTool check, which rebuilds
  `content/grammar/` and uploads a `grammar-refresh` artifact on a failure).
- Still open from the owner's 2026-10-23 answers:
  - **SonarCloud** as in `james-d12/CodeGuard` `.github/workflows/ci.yml` (`dotnet-sonarscanner` begin/end around build and test,
    Cobertura coverage), with organisation `personal-james`, project key `personal-james_vocaterra`, name `Vocaterra`, and a `SONAR_TOKEN`
    secret the owner creates;
  - speech models downloaded and cached in CI, so the model-gated tests run;
  - CI on pushes to `feature/experiment` too;
  - ideally the checks shared with `scripts/merge-gate.sh` (one checks script).
- Not chosen: a "no pending rows" gate and a Claude review in CI.

**Open owner questions**
- Listening to the new WAVs and sounds (S17, S20b, S24, S25, S26, S27).
- `Puedes decir` versus "You can say".
- X00's questions: R05 early stop in the pre-check, R12 adaptive lessons, R14/R15/R18 content before or after the pilot, R11 title screen,
  the mic lab, GPU bench and pre-flight runs, the pilot date, voices for new NPCs.
- Whether and when `feature/experiment` goes to `main`. `main` still has a rare M49 fuzz flake (O4, seed `7oEL4lRaLMu2`) that X02's
  one-file generator fix cures.
- M27a2: obsolete since R02 (`content/v1` and its grammar step are gone; the grammar step is required on `content/game-v1`).

## Start of a session
1. `git status`, `git log --oneline -8`, `git worktree list` (remove leftovers with `git worktree remove --force <path>` and
   `git branch -D <branch>`), `df -h /home` (each build is several GB), and the resource check below (`free -h`, `uptime`, stray
   `dotnet` processes).
2. Read `docs/game/IMPLEMENTATION_PLAN.md` section 4 (with `docs/game/SCENARIO_PLAN.md` and `docs/game/PROTOTYPE_BRIEF.md` for the S series) and the last entries of section 5; `docs/nlp/IMPLEMENTATION_PLAN.md` §5 statuses and the
   last entries of §5b; both `docs/DECISIONS.md`.
3. If a milestone is `in progress` with no live agent, inspect its worktree before relaunching.
4. Tell the owner in a few lines where things stand and what is next.

## Model policy (owner's preference: minimise tokens)
- **Haiku 5.5 is the default** for every delegated task: content, docs, tests, mechanical code, re-verification, most feature milestones.
- **Sonnet 5.5 only** for design-heavy milestones (a new scene, a state machine, a new algorithm or cross-module design in the core, the
  engine host's architecture), or when a Haiku attempt has failed the gate twice or returned a report whose claims did not verify.
- **Opus 5.5 only when the owner asks** (research, architecture reviews).
- Never set an effort level unless the owner asks. One agent per milestone, and **one agent at a time** (see Machine resources).

## Machine resources (owner, 2026-10-22: never max out the computer)
The desktop crashed twice on 2026-10-22 with CPU and RAM maxed out: two Sonnet agents (S14, S17) built and tested at the same time, each
with about 16 MSBuild nodes, the Roslyn compiler server and parallel test hosts, on a machine with 16 cores, 11 GiB of RAM and 2 GiB of swap.
- **One agent at a time.** Do not launch a second agent while one is running, even on disjoint directories. The only exception is an agent
  that runs no build, test or `dotnet run` at all (pure reading or writing docs), and then at most two in total.
- **Every heavy command goes through `scripts/limited.sh`**: `dotnet restore|build|test|format|run`, `scripts/content.sh`,
  `scripts/prerender-speech.sh`, the licence scan, screenshot runs. It holds one machine-wide lock (a second caller waits), waits until
  at least 3 GB is free and the load is under the core count, then runs the command in a systemd user scope capped at 6 GB of RAM, no swap
  and 10 of 16 cores, at nice 10, with MSBuild node reuse, the MSBuild server and the shared compiler server off (nothing lingers). A
  command over the cap is killed inside its scope (exit 137) and the desktop survives; the fix is lower parallelism, never a higher cap
  without the owner. Builds use `-m:4`, tests `-m:2`. The gate already does all of this; every brief tells the agent to do the same.
- **Test tiers** (docs/TEST_PERFORMANCE_PLAN.md): `scripts/test.sh fast` for every step (it leaves out the tests marked `Speed=Slow`: the
  fuzz suite, the model-gated tests, the world-generation, CLI, lint and process tests, and a few long single tests), `scripts/test.sh full`
  at a milestone and in the gate (the gate and CI always run the full suite). A new test that takes more than a few seconds alone, needs a
  model or starts a process is marked `[Trait(TestSpeed.Trait, TestSpeed.Slow)]`.
- **Check before every launch and every gate**: `free -h` (at least 4 GB available, swap under half used), `uptime` (1-minute load
  under 16), `df -h /home` (at least 10 GB free), `pgrep -fc 'MSBuild.dll|VBCSCompiler|testhost'` (0 when nothing is running). Orphaned
  build servers are stopped with `dotnet build-server shutdown`. If the machine is not clear, wait or tell the owner; do not launch.
- **While an agent runs**, glance at `free -h` and `uptime` when you are woken; if available memory drops under 2 GB or load stays
  above 20, stop the agent (TaskStop) and relaunch with lower parallelism.
- **After a crash**: stop orphaned build servers, check every in-progress worktree for uncommitted work and commit it as a WIP snapshot on
  its branch, then resume the milestones one at a time, each in its existing worktree (its incremental build saves disk and time).

## Per-milestone loop
1. **Brief.** Use the template below. Point at files to read rather than retyping them. State the gate and the report format. Name the
   commit message prefix (`M<nn>:` for the core, `G<nn>:`, `P<nn>:` or `S<nn>:` for the game).
2. **Launch** with `subagent_type: general-purpose`, `isolation: worktree`, the chosen model.
3. **Verify the report cheaply** before merging: the branch exists and is clean, `git diff --stat <merge-base>..<branch>` matches the
   report, no unexplained deletions, no files under `.github/workflows/` (unless its brief names them), no edits to either plan or decisions file, nothing under
   `bin/`, `obj/` or `artifacts/` staged, and a game milestone has not edited the core (the `Vocaterra.Nlp` or a `Vocaterra.Nlp.*` project, `tests/golden`, `docs/nlp/`) nor a core milestone the game (unless its brief said so). `content/game-v1` is the one shared file set (R02): the core's tests run on it, so a milestone that edits it ran the whole suite.
4. **Gate:** `GATE_TARGET=<feature branch> scripts/merge-gate.sh "<label>:<branch>"` from that feature branch's checkout, for every
   series (S, M, G, P, X, R). Then `git push origin <feature branch>`; never `main` (the gate refuses it). It merges `--no-ff`, builds `Vocaterra.sln` with warnings
   as errors, runs the whole test suite (core and game, with models when the shared folder exists), checks format, validates the game
   bundle and runs the core's offline licence scan, keeping every log under `.claude/gate-logs/`. It exits non-zero on any failure. An
   intermittent failure is investigated from the saved log, never waved through.
5. **Spot-check behaviour**, not just tests: the host demo commands in the game plan's section 6, the core's README demo line, and the
   milestone's own demo lines.
6. **Plan update:** the milestone's status and findings line, decisions; commit with the attribution trailers and `git push origin <feature branch>` (never `main`).
7. **Clean up:** `git worktree remove --force` and `git branch -D`.
8. **Report to the owner:** outcome first, bugs found, numbers that change a decision, open decisions.
9. **QA (optional):** after a front-end, content or language milestone, launch `subagent_type: qa` on the feature branch's checkout
   with one area in the prompt (`.claude/agents/qa.md`). It builds and runs the game, so it counts as the one agent at a time. Read its
   report in `docs/qa-reports/`, log the confirmed bugs in the findings log and plan the fixes as milestones.

## Brief template
```
You are implementing <milestone id and name> for the Vocaterra repository (C#, .NET 10). The language core is the Vocaterra.Nlp and Vocaterra.Nlp.* projects, the game is
Vocaterra.{Simulation,Game,MonoGame,Host,SpeechRender}; this milestone's work is in <the core or the game>. You are in an isolated git worktree. First `git fetch origin` and `git checkout -b <branch>
origin/<feature branch>` (the worktree may start elsewhere); commit there, do not push.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
speech prerender and screenshot run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`;
tests with `-m:2`). Never start two heavy commands at once (no `&`, no background builds, no parallel test runs). While iterating run
`scripts/test.sh fast` (every test not marked Speed=Slow, through limited.sh) or `scripts/test.sh project <test project>` for the project
you changed; run `scripts/test.sh full` (the whole suite, as the gate) once before reporting. Exit 137 means the 6 GB cap killed it:
lower the parallelism, do not work around the cap.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
or a CLI `eval` run use a wrapper script outside the repository). Model-gated tests need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.
Read first: CLAUDE.md; <the plan: docs/game/IMPLEMENTATION_PLAN.md sections 1, 3, 4 (your row) and 6, or docs/nlp/IMPLEMENTATION_PLAN.md §3, §4, §6>;
<the milestone's section>; <docs/game/ARCHITECTURE.md and docs/game/DECISIONS.md, or docs/nlp/CONTRIBUTING.md>; docs/nlp/API.md; <the files to change>.
Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is (a missing core call stops
that part: report it, do not copy core source or edit the Vocaterra.Nlp or a Vocaterra.Nlp.* project or docs/nlp/); a core milestone edits only the Vocaterra.Nlp and Vocaterra.Nlp.* projects,
their tests, tests/golden and docs/nlp/ (and content/game-v1 only where the core's tests need it, never the game's lessons), fixes bugs at the source with a regression test in the owning module and never
weakens a fuzz oracle; never create or edit anything under .github/workflows/ unless this brief names the file; do not edit either IMPLEMENTATION_PLAN.md or
either DECISIONS.md (the orchestrator does); engine code only in src/Vocaterra.MonoGame (D-game-1); assets CC0 only (D-game-5);
no System.Random and no clock in the evaluation path.
Content edits (game): scripts/content.sh validate, then scripts/content.sh hash content/game-v1 --bundle-id game-v1 (through the
wrapper if the sandbox refuses the word), validate again, cefr-check at a1 and a2 for new templates; keep the Wiktionary notice in
ATTRIBUTION.md. One bundle since R02, so a game content edit also runs the core suite: after hash, run the whole test suite (a failing core test is the signal that the edit broke
something the core relies on), re-record the end-to-end goldens if the edit moved them (VOCATERRA_RECORD_GOLDEN=1 dotnet test tests/Vocaterra.Nlp.Golden.Tests),
put the new bundleHash into tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json, and keep cefr-check clean. No core test pins the hash itself.
Deliverables: <numbered list>.
Gate (from the repository root, each through scripts/limited.sh): dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); dotnet test
Vocaterra.sln -m:2 (<current count> passed); dotnet format Vocaterra.sln --verify-no-changes clean; scripts/content.sh validate passes; <core: bash scripts/license-scan.sh
--offline PASSED>; the demo lines give the stated output. Do not commit bin/, obj/ or artifacts/.
Commit message prefix `<M|G|P><nn>: <title>`, ending with
Co-Authored-By: Claude <model> <noreply@anthropic.com>
Claude-Session: <session url>
Report: branch, commit SHA, worktree path; what was built; test counts; measurements; bugs found and fixed (where, symptom, fix, test);
core gaps found by a game milestone (what call, why); known-not-fixed with reasoning; files touched outside the milestone's area;
anything left undone and why.
```

## Repo facts that briefs must carry
- .NET SDK is user-local at `~/.dotnet` (`global.json` pins 10.0.402, `latestPatch`); no sudo.
- One solution, `Vocaterra.sln`, with project references from `src/Vocaterra.Game.Core` and `src/Vocaterra.Host` to the one engine project `src/Vocaterra.Nlp` (M52) and the Spanish pack `src/Vocaterra.Nlp.Es` (M53;
  load a Spanish bundle with `SpanishContent.Load`, since `LanguagePacks.Default` is empty).
  `Vocaterra.Nlp.slnf` builds the core alone. Package versions are in the root `Directory.Packages.props`; no new package outside it.
- The host (`src/Vocaterra.Host`) is a console program: `--transcript "<line>" --prompt <id>` evaluates once; no argument runs the text loop.
- The one content bundle `content/game-v1`, hashed through `scripts/content.sh` (the core's ContentBuilder run from source); the core's tests, fuzz suite
  and demo CLI run on it (R02: the core's sample `content/v1` is deleted). Lexicon regeneration in `docs/nlp/LEXICON.md`.
- CI is `.github/workflows/ci.yml` (see "CI" above); workflow edits land on a feature branch like any other change.
- Determinism tiers (core plan §3): no `System.Random`, no wall clock, ordinal comparisons, `CanonicalJson`, seeded RNG, margins for model
  thresholds; model adapters gated on `VOCATERRA_MODELS_DIR`.
- The fuzz suite (`tests/Vocaterra.Nlp.Fuzzing.Tests`, design in `docs/nlp/FUZZING_PLAN.md`) is a merge gate and has found every real
  bug so far; its README lists bugs found, known limits and narrowed oracles.
- License scan: `scripts/license-scan.sh --offline`; the allowlist refuses copyleft code dependencies; CC BY-SA accepted for derived data.
- Core demo: `dotnet run --project tools/Vocaterra.Nlp.Cli -- eval --transcript "yo quiere dos espada" --prompt villager-open
  --content content/game-v1` must keep yielding exactly `conj.person` and `noun.number`; the design document's own sentence "yo tiene dos espada" is the pipeline test
  `TheDesignDocumentsWorkedExampleGivesExactlyConjPersonAndNounNumber`.
- Licences: the core is Apache-2.0 (core D3), the game too (D-game-20, which superseded D-game-2 MIT in R01); the prototype engine is MonoGame, the final engine open (D-game-1).
- Native assets: on Linux x64 the game builds for linux-x64 only (G7, the non-core group of the root `Directory.Build.props`); `-p:VocaterraAllRuntimes=true` on
  restore and build gives every platform. The game's executables and test projects that run speech reference `Vosk` directly and
  `Microsoft.ML.OnnxRuntime` with `ExcludeAssets="native"` (CR-021); keep both.
- Disk: each full build writes several GB of native-package output per checkout; worktrees are cleaned after merge. Launch one agent at
  a time (Machine resources).

## Open decisions (owner's)
Core: online versus offline (D8); determinism strictness (D9); MPL-2.0 stance (D10); tutor cost cap (D11); pronunciation ambition (D13).
Deferred by the owner on 2026-10-21, do not raise: D1 (workflow scope, CI), D4 (OpenSubtitles terms), D-game-3 (CI).
Decided: D2 GPL accepted for private builds with the GPL-free sherpa-onnx pin blocking any distribution, D3 Apache-2.0, D5 superseded
(no feed consumer in the repository), D6 the core keeps the engine, D7 the core keeps the sample (superseded by R02: one bundle, `content/game-v1`), D-game-1 MonoGame prototype, D-game-20 Apache-2.0 for the whole repository (R01; supersedes D-game-2 MIT),
D-game-4 the game bundle diverged from the sample (R02: the sample is deleted), D-game-5 CC0 assets, D-game-6 known nouns, D-game-7 one repository.

## Never
- Write code under `src/`, `tests/` or `tools/` yourself; the plans, decisions, briefs, gate script,
  CLAUDE.md and this file are the only files you edit directly.
- Let a game milestone edit the core (the `Vocaterra.Nlp` and `Vocaterra.Nlp.*` projects, `docs/nlp/`) or copy core source, or a core milestone plan game content; a core need is a core milestone.
- Let a milestone agent write under `.github/workflows/` unless its brief names the workflow file.
- Merge on an agent's word alone; the gate and a behaviour spot-check come first.
- Launch a milestone the owner has not asked for or that is not the planned next step; propose it.
- Leave a worktree behind or let disk fall under about 10 GB free.
