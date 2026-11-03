#!/usr/bin/env bash
# The scripted runs of docs/game/scripted-runs.md: runs each one and keeps what it wrote (stdout, screenshots, saves, sound logs, the session
# log, reports) in OUT/<name>, to compare two captures with scripts/compare-runs.py (a baseline before a change, a capture after it).
# Every run writes into the same work folder, so the paths it prints are the same in every capture.
#
#   scripts/scripted-runs.sh OUT [name...]     (no names: every run; run from the repository root after a build)
#
# GAME_BIN overrides the game binary (another worktree's build); VOCATERRA_RUNS_WORK the work folder.
set -uo pipefail
OUT="$1"; shift
ONLY=("$@")
REPO="$(pwd)"
BIN="${GAME_BIN:-$REPO/src/Vocaterra.Game/bin/Debug/net10.0/Vocaterra.Game}"
W="${VOCATERRA_RUNS_WORK:-${TMPDIR:-/tmp}/vocaterra-runs-work}"
export DISPLAY="${DISPLAY:-:0}"
export VOCATERRA_MODELS_DIR="$HOME/.local/share/vocaterra/models"
mkdir -p "$OUT"

PRE_PICKS=(--pick 1 --say "¿Qué es esto?" --say "Sí." --pick 3 --say "No, gracias." --pick 2 --say "Necesitamos agua." --say "¿Dónde está el río?" --say "No." --say "Más despacio, por favor." --say "Otra vez, por favor." --say "Ella quiere fruta." --say "Tengo una fruta." --pick 2 --pick 3 --pick 3 --say "Ayer comí fruta." --pick 3 --pick 2 --pick 3 --pick 3 --pick 2 --pick 1 --say "Sí, por favor." --pick 3 --pick 3 --pick 2 --pick 2 --say "Voy a comer pan." --say "Quiero madera." --say "Quiero agua, por favor.")
CAMP=(--walk-to 172,187 --interact 170,43,189 --interact 169,43,185 --walk-to 173,183 --interact 171,43,181 --walk-to 155,187 --interact 153,42,186 --interact 153,43,186 --interact 153,44,186 --interact 153,45,186 --interact 153,41,189 --walk-to 177,185 --craft antorcha --walk-to 174,185 --place antorcha@175,44,187)
POST_PICKS=(--say "Necesitamos agua." --say "Más despacio, por favor." --pick 1 --say "Sí, por favor." --say "Ella quiere fruta." --pick 2 --pick 1 --pick 1 --pick 3 --pick 1 --say "Voy a comer pan." --pick 1 --say "Ayer comí fruta." --say "¿Dónde está el río?" --pick 2 --say "Quiero madera." --pick 3 --say "No." --pick 1 --say "No, gracias." --pick 2 --pick 3 --pick 1 --pick 2 --say "Otra vez, por favor." --pick 2 --say "Sí." --say "Tengo una fruta." --say "¿Qué es esto?" --pick 2 --say "Quiero agua, por favor.")

run() {
  local name="$1"; shift
  if [ "${#ONLY[@]}" -gt 0 ] && [[ ! " ${ONLY[*]} " == *" $name "* ]]; then return; fi
  rm -rf "$W"; mkdir -p "$W"
  local started=$SECONDS
  scripts/limited.sh "$BIN" --session-dir "$W/session" "$@" > "$W/stdout.txt" 2>&1
  local code=$?
  echo "$code" > "$W/exit.txt"
  rm -rf "${OUT:?}/$name"; cp -r "$W" "$OUT/$name"
  echo "$name: exit $code, $((SECONDS - started)) s"
}

run r01-route --seed 42 --sound-log "$W/sounds.log" --walk-to 176,186 "${CAMP[@]}" --look 180,-15 --screenshot "$W/shot.png"
run r02-river --seed 42 --look 120,-5 --say "¿dónde está el río?" --screenshot "$W/shot.png" --frames 300
run r03-npcs --seed 42 --mute --sound-log "$W/sounds.log" --talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" --talk villager --say "¿dónde está el río?" --leave --screenshot "$W/shot.png" --frames 60 --save "$W/game.save"
run r04-journal --seed 42 --mute --lesson-run first-words --journal --journal-phrases --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"
run r05-book-play --seed 42 --sound-log "$W/sounds.log" --lesson-run first-words --book-play fruta --shot "$W/noun.png" --book-play-slow phrase:pedir-agua --shot "$W/phrase.png" --screenshot "$W/shot.png" --frames 30
run r06-practise --seed 42 --mute --lesson-run first-words --book-practise first-words --screenshot "$W/shot.png" --frames 30
run r07-inventory --seed 42 --mute --inventory --screenshot "$W/shot.png" --frames 30
run r08-crafting --seed 42 --mute --crafting --screenshot "$W/shot.png" --frames 30
run r09-check-pre --seed 42 --check pre --wait 2 --shot "$W/check.png" "${PRE_PICKS[@]}" --screenshot "$W/shot.png" --frames 60 --save "$W/game.save"
run r10-playtest --playtest --seed 42 "${PRE_PICKS[@]}" --lesson-run first-words "${CAMP[@]}" --talk shopkeeper --say "quiero agua, por favor" --say "sí, por favor" --talk villager --say "¿dónde está el río?" --walk-to 234,222 --interact 233,34,222 --look 180,-14 --talk guide --say "sí, por favor" --leave --walk-to 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 "${POST_PICKS[@]}" --screenshot "$W/shot.png" --frames 60
run r11-mic --seed 42 --mic-check --screenshot "$W/shot.png"
run r12-report --seed 42 --mute --report "the bridge has a gap" --screenshot "$W/shot.png" --frames 30
run r14-third --seed 42 --mute --third-person --look 120,-5 --screenshot "$W/shot.png" --frames 60
run r15-grey-dusk --seed 42 --mute --greyscale --dusk --teleport 222,222 --look 80,-16 --screenshot "$W/shot.png" --frames 60
run r16-text-scale --seed 42 --mute --size 800x600 --text-scale 150 --lesson-run first-words --book-page lessons --screenshot "$W/shot.png" --frames 30
run r17-speak --seed 42 --talk shopkeeper --speak "quiero agua" --screenshot "$W/shot.png"
run r18-bakery --seed 42 --mute --session 5 --teleport 309,216 --interact 311,38,214 --interact 311,39,214 --interact 311,40,214 --interact 311,38,218 --interact 311,39,218 --talk baker --shot "$W/paid.png" --say "quiero dos panes, por favor" --shot "$W/sale.png" --leave --inventory --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"
run r19-directions --seed 42 --mute --teleport 236,206 --talk fisher --say "¿dónde está la panadería?" --shot "$W/arrow.png" --walk-to 282,206 --walk-to 282,212 --look 0,4 --screenshot "$W/shot.png" --frames 60
run r20-sleep --seed 42 --mute "${CAMP[@]}" --teleport 283,211 --talk shopkeeper --wait 3 --leave --talk shopkeeper --leave --teleport 234,222 --interact 233,34,222 --teleport 153,109 --look 330,-12 --place antorcha@153,74,105 --wait 1 --shot "$W/ending.png" --sleep --look 129,-20 --wait 2 --shot "$W/session2.png" --book-page lessons --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"
run r21-skip --seed 42 --mute --skip-to-session 4 --wait 1 --shot "$W/beacon.png" --book-page lessons --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"
run r22-market --seed 42 --mute --skip-to-session 10 --teleport 309,216 --interact 311,38,214 --interact 311,39,214 --interact 311,40,214 --interact 311,38,218 --shot "$W/before.png" --talk smith --say "vendo madera" --leave --shot "$W/sold.png" --talk baker --say "quiero dos panes, por favor" --leave --shot "$W/bought.png" --talk farmer --say "¿cuánto es todo?" --shot "$W/total.png" --leave --inventory --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"
run r23-future --seed 42 --mute --skip-to-session 9 --lesson-run manana-en-el-pueblo --talk baker --say "mañana compraré pan" --shot "$W/reply.png" --leave --book-verb comprar --screenshot "$W/shot.png" --frames 30
run r24-followups --seed 42 --mute --skip-to-session 10 --talk baker --say "mañana compraré pan" --shot "$W/future.png" --leave --talk shopkeeper --say "quiero madera" --shot "$W/stall.png" --leave --talk baker --say "ayer compre pan" --shot "$W/accent.png" --leave --screenshot "$W/shot.png" --frames 30 --save "$W/game.save"
