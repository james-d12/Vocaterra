You are implementing Y00, "Brief and probes", for the Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp,
the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the engine-free
logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game, and it is
measurement and writing only: no gameplay code and no content change is committed.

Where to work: /home/james/Dev/Vocaterra/.claude/worktrees/y00, which already exists on the branch `Y00-brief-and-probes` (cut from
`feature/settlement`). Start every command with `cd /home/james/Dev/Vocaterra/.claude/worktrees/y00 &&` and use absolute paths for file
tools; your shell may start in another checkout, which you must not edit. Commit with the prefix `Y00: `. Do not push your branch: it
stays local and the gate merges it from there. Never push or merge `feature/settlement` or `main`. When you finish, leave the worktree
on your branch, clean.

Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
speech render and screenshot run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly. Never start two heavy
commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. Another session's
agents build on this machine too, so a heavy command may wait for a free slot: that is normal, let it wait. Exit 137 means the 6 GB cap
killed it: lower the parallelism, do not work round the cap.

Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for a CLI `eval` run or
`scripts/content.sh hash` use a wrapper script outside the repository). Model-gated work needs
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md; docs/game/settlement-plan.md sections 7 and 8 (8.1 the findings, 8.2 the defaults, 8.3 the table and the Y00,
Y04, Y06 and Y09 paragraphs, 8.4); the section "D-game-32" of docs/game/decisions.md (grep for it, read only that section);
content/game-v1/game/scene.json (whole, 283 lines); in content/README.md the sections "The edit loop" and "Adding an NPC" (to know what
Y06 will need, not to do it); in docs/game/village-plan.md the paragraphs about voices (grep "voice"), for how the provisional voices
were chosen and rendered. Read the sections named, never whole long files; never docs/game/speech-checklist.md,
content/game-v1/manifest.json or the whole of content/PHRASES.md (grep the rows). Heavy output goes to log files; print at most 30 lines.

Rules: your commits change one file, docs/game/settlement-plan.md, by adding a new section 9, "Layout and numbers", after section 8.
Do not touch section 8's status table, any other section, either implementation-plan.md or either decisions.md (the orchestrator edits
those). Commit nothing under src/, tests/, tools/, content/ or .github/. Throwaway probe code, scratch copies of the bundle and
screenshots live outside the repository (your scratchpad directory) or in untracked files you delete before reporting. The one write
outside your worktree that is asked for is the voice trial's folder (deliverable 5). Do not copy core source and do not edit a
Vocaterra.Nlp project. No System.Random and no clock in anything you propose for the evaluation path.

What the series is, in three lines: the player's first bed moves from their own fire on the mountain to a free bed at a new inn in the
village; later they mark a plot in a meadow with a stake, buy it from the mayor (Tomás, today's villager) and place a camp kit on it.
Your job is to settle the facts the later milestones need: where the inn stands, where land can be bought, what it costs, which
Spanish words and lines work today, and which voice the innkeeper could have.

Deliverables:

1. **The inn's site.** The plan's default is a ground-floor `house` of about 9 x 7 at the road's entrance, in place of `house3`
   (scene.json: village site at 292,218, radius 24; `house3` at dx -13, dz 7).
   - List everything that names `house3` or depends on its place: content, src, tests, scripts/scripted-runs.sh and the coordinates in
     docs/game/scripted-runs.md that fall on or beside it. Say what breaks if it goes.
   - Propose the inn's `house` prop line (dx, dz, width, depth, height, facing, name `inn`, label) so that every corner is inside the
     levelled disc and it clears the road's via points, the signpost, the fence lines, `lamp.house3`, the paving and the well. If 9 x 7
     does not fit, give the largest that does and the alternative sites you tried.
   - Draft the interior as a stamp (legend and layers, existing block names only; use `crate` where a bed will go, since the bed block
     comes in Y03): a guest room with two beds, a counter, the innkeeper's cell behind it, a partition with a door gap, window gaps.
     Name the cells: the bed the player sleeps at, the innkeeper's cell and facing, the cell the player talks from. Check the talk range
     and the line of sight the game uses (Graphics/Figures/FigureDirector.cs and what it calls).
   - Prove it in a scratch copy of the scene, not in the repository: the scene builds (the game's `--scene <file>` flag, or a throwaway
     test); a stamp placed inside a house's footprint builds; a legend entry `"air"` clears a wall cell for a window (say if it does
     not, and what the parser would need); an NPC placed inside gets a valid cell; a player can walk from the door to the bed.
   - Two screenshots, outside and inside (`--teleport`, `--look`, `--screenshot`), saved outside the repository; give the paths.

2. **The meadows and the plot.** Outside the village's clearing nine cells in ten hold a tree, and leaves cannot be gathered.
   - Write a throwaway probe that builds the scene for seeds 42 and three others and counts valid plot origins for footprints of
     10 x 10, 12 x 12 and 16 x 16 under these draft rules: every column's ground is grass or dirt; the heights differ by at most 1
     (count also with at most 2); no water; no trunk or leaves above; outside the protected set (the village's disc of radius 24 plus
     its blend of 8 plus a margin of 2; every `path` and `bridge` block plus a margin of 2; Luna's clearing plus 2).
   - Run it on the scene as it is, and on a scratch scene with tree clearings for a north meadow and a south meadow. Find how
     `trees.clearings` takes its place (a site name, a point, a radius, `fade`, `keep`), and whether a new named site or point is
     needed and what a site without a radius does to the ground.
   - Give a table (seed, footprint, region, valid origins, whether three plots that do not overlap fit). Recommend the footprint, the
     slope limit, the two meadows' centres (as offsets from the `village` site) and radii, and the margins. Give the walking distance
     in blocks from the inn's door to each meadow's centre and to the mayor.

3. **The price.** From the content, not by playing: every way to gain or spend coins (the NPC, the amount, what the learner says,
   any limit per day), the largest stack of coins the bag holds, and the coins a player holds at the end of day 2 and of day 3 for
   two ways of playing ("the orders only" and "the orders and ten wood sold"), with your assumptions stated. Recommend a price that
   can be reached on day 2 or day 3 without grinding, and say what the player must do to reach it.

4. **The language probe.** Through `tools/Vocaterra.Nlp.Cli -- eval --transcript "<line>" --prompt <id> --content content/game-v1`
   (by a wrapper script outside the repository). Take the prompt ids from the agents and prompts the shopkeeper and the villager use
   today.
   - Lines: «Quiero una habitación, por favor.», «Una habitación, por favor.», «¿Tiene una habitación?», «¿Tienes una habitación?»,
     «Quiero dormir aquí.», «¿Cuánto cuesta?», «Quiero comprar un terreno.», «Quiero comprar esta tierra.», «Quiero este terreno.»,
     «¿Cuánto cuesta el terreno?», «¿Me das una estaca?», «sí», «no sé», and any better wording you find. For each: the prompt, the
     intent, the slots, the verdict and the error tags.
   - Words: the CEFR level the content gate gives (the lists are content/game-v1/cefr/a1.txt and a2.txt; the gate's command is in
     CLAUDE.md) and whether the lexicon has a reading with a gender, for posada, hostal, hotel, pensión, habitación, cuarto, cama,
     llave, noche, dormir, terreno, tierra, parcela, alcalde, estaca, campamento, tienda de campaña, saco de dormir, comprar, vender,
     precio. Note that `tienda` is already the stall's label.
   - Recommend the inn's word, the plot's word and the mayor's title, and two or three player lines for lodging and for the land that
     are A1 or A2 and that the core reads correctly today. Say what the innkeeper's agent and the mayor's sale can do with content
     alone (agents, prompts, dialogue) after the model of an existing NPC.
   - Each core gap as one paragraph: the line, what the core answers, the call or intent that is missing, and the Y milestone that
     waits on it (Y06 for lodging, Y11 for the land). Do not patch a gap in the game.

5. **The voice trial.** Read how a voice is set (`game/npcs/*.json`: speaker, name, speed, gain) and which speakers the model has.
   Choose three candidates that no other NPC uses, with a reason for each, and say whether you propose a woman or a man (four of the
   seven NPCs are men). Render «¡Hola! ¿Quieres una habitación?» and «Buenas noches.» in each with tools/Vocaterra.SpeechRender, at
   the speech folder's sample rate, into /home/james/Dev/Vocaterra/.claude/worktrees/settlement/artifacts/voice-trial/innkeeper/
   (git-ignored; your own worktree is removed after the gate). Name each file by its speaker.

6. **Section 9, "Layout and numbers",** in docs/game/settlement-plan.md: at most 150 lines, tables where three or more like things
   are listed, every number with how it was measured, every recommendation with the one it replaces in section 8 if it differs. Its
   parts follow the five deliverables above. End it with "What Y04, Y06, Y09 and Y11 take from this", one short list per milestone.

Gate (from your worktree's root): only a document changed, so no full suite. `scripts/test.sh project tests/Vocaterra.Nlp.Tests
--filter MarkdownLinkTests` passes; `git status` is clean; `git diff --stat feature/settlement..HEAD` shows only
docs/game/settlement-plan.md. Do not commit bin/, obj/ or artifacts/.

Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfPceqswnMMBmTQzg9

Report, one page: branch, commit SHA, worktree path; the recommendation for each deliverable in one or two lines; the tables' headline
numbers; the paths of the screenshots and the voice files; core gaps found (what call, why); anything a later milestone's plan must
change because of what you measured; anything left undone and why. Leave out how you searched.
