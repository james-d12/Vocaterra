W08 interim report (stopped partway on the orchestrator's order, 2026-11-06, the owner ending the session).
Branch W08-teacher-loop (local, never pushed), commits cc6b7edf and f180f1a9; worktree .claude/worktrees/w08, clean.

Done and committed
- Six host kinds in HostEventTable: procedure_repeat, procedure_slower, procedure_help, give_missing, teach_recipe, plus start_procedure
  (the accepted offer opens the station). host-events.schema.json updated; its schema version stays 2 (the bump to 3 is W04's).
  VoxelSession.Teaching.cs applies them on the game thread. GuideSession.Activities.cs raises <npc>.teach_start and teach_end as host
  turns (procedure start, end, station close, bake Done). New API NpcService.HostTurn. A load that leaves the teacher in `teaching` is
  repaired at the next talk or by a second teach_start.
- The daily grant in ProceduresSave (GrantedToday; OfferedToday for the offer); older saves load.
- The offer to bake: Luz's talk opens with it once per in-game day until the learner has baked; «¿Cómo hago pan?» at her open prompt
  (ask_craft) is answered by the offer, which raises teach_recipe. Silent with no procedure in the bundle (tested).
- The station's talk: TeacherTalk and a talk line on the station; V/Space/W reach the teacher with the station open; TurnPump routes her
  turns to the station; the strict ProcedureCue plays the row after her line. New step --station-say "<line>", documented.
- Luz's content: agents/baker.json gains the offering_bake and teaching states; prompts baker-offer and baker-proc;
  answer-sets/baker-proc.json; six host events; English strings; 18 recordings; grammar cache; the save fixtures' hashes. Bundle hash
  0eebdd44... Validator rules for the new kinds.
- Tests: TeacherLoopTests (24 tests: each kind, the grant, the offer and its absence, teaching enter and leave, a load, the station talk).
  Pinned counts updated (host-event table 22, prompts, pools 316, baker 50).

New Spanish lines (pool: text)
- baker_offer_bake «¿Quieres hacer pan?» (the design's «...conmigo?» fails the A2 lint); baker_teach_go «¡Vamos!»; baker_listen
  «Escucha.» (repeat and slower); baker_helping «Te ayudo.»; baker_give_missing «Toma. Aquí tienes.»; baker_has_all «Ya tienes todo.»;
  baker_gift_used «Hoy no tengo más.». Prompt baker-offer is the offer's text; baker-proc reuses «¿Qué quieres?». teach_start and
  teach_end reuse baker_nice, unspoken. Answer set baker-proc: otra vez, más despacio, ayuda, no sé, quiero/hay madera, quiero/hay agua,
  gracias.

Values chosen (decision 20, provisional)
- A grant only while a run is InRun; it gives what `uses` lists and the bag lacks, all or nothing.
- Open for the first bake (procedure never completed), otherwise once per night; the first bake's grants do not count for the day.
- No room: baker_no_room. Nothing missing: «Ya tienes todo.» Already granted today: «Hoy no tengo más.»
- W10's seam: GuideSession.GrantRequiredByLesson, overridden in VoxelSession to open the grant while the lesson's do step waits for
  procedure_done (tested with a fixture lesson).
- The offer is also made when asked (ask_craft), independent of the daily limit.

Design versus code
- «¿Tienes harina?» is not ask_availability (which is only «hay X»); «Quiero X» (request_item) and «hay X» work.
- The CLI at baker-open reads «¿me enseñas a hacer pan?» as unknown, not communicated: a core intent is needed (not patched).
- «¿Cómo hago pan?» and «quiero hacer pan» read as off-prompt ask_craft.

Tested: Game.Core.Tests, Game.Tests (710), Host.Tests (189) once each, and a filtered run after the last commit (163 passed); the
solution build had 0 warnings; content loop --grammar --speech --tier none passed twice; the scripted baseline captured
(/tmp/claude-1000/w08/runs-before, from the main checkout's build).

Not done, in order
1. Scripted runs after, compared with /tmp/claude-1000/w08/runs-before (no difference expected).
2. scripts/content-loop.sh --speech --tier full: goldens (may need --record: prompts and the identity baseline changed), the full suite,
   the count (9759 passed, 3 skipped before).
3. dotnet format --verify-no-changes; content.sh validate.
4. Screenshots with a throwaway bundle: /tmp/claude-1000/w08/scripts/mkbundle.sh, then --content .../bundle/game-v1 --station horno
   --station-say "Otra vez, por favor." --screenshot.
5. The baker-proc lines checked in the CLI.
