# Vocaterra playtest (private build)

Thank you for helping. This is an early prototype. You wake alone at your own camp on a cliff, walk down a trail to meet a
guide, learn some Spanish from her in a short lesson, then gather wood and berries, make a torch, ask people for things in
Spanish, fill a bottle at the river and light a fire at your own camp. It takes about 15 to 25 minutes, the lesson included.

**Please read this first.** You do not need to know Spanish. The Spanish in the game has **not yet been checked by a
native speaker**; it was written and reviewed with tools only. If something sounds odd, say so. Nothing you do can
fail the game: if you get stuck, the game always has a way on.

## Before you start

- A Linux computer (64-bit), a screen, a keyboard and a mouse. A microphone is helpful but not needed: you can type
  every answer instead.
- You do not install .NET or anything else. The folder contains everything it needs.
- The game keeps a record of your session on this computer only. Nothing is sent anywhere. The facilitator may ask you
  to share that folder with them afterwards (see "Your session record" below). You can say no.

## Start the game

Open a terminal in this folder and run:

    ./playtest.sh

If you see "Permission denied", run `chmod +x playtest.sh` once and try again. If the launcher says the game is not in
this folder, the folder was not built: ask the facilitator for a built copy.

## Controls

- **Move:** W, A, S, D (or the arrow keys). **Jump:** Space. **Look:** the mouse.
- **F5:** switch between your own eyes and a view over your shoulder.
- **Gather:** hold the left mouse button on a tree or a berry bush. **Place:** the right mouse button.
- **Talk to a person:** point the crosshair at them and press **E** (or T). Then:
  - **V** (hold it) or **Space** (hold it) to speak; let go when you have finished.
  - **W** to type, then Enter to send.
  - **R** repeats what they said, **S** says it more slowly, **H** asks for help, **N** means "I do not know".
  - Numbers **1, 2, 3** choose one of the offered answers.
  - **Esc** closes the talk.
- **C**: the crafting table (stand next to the table). **I**: the inventory.
- **J**: the book. It is an open book with five pages, each with a ribbon at the top (nouns, verbs, phrases, lessons and what
  you said); click a ribbon or press Tab to turn the page. On the verbs page, pick a verb to see its table: the forms you said
  are highlighted, the forms the lessons taught are plain. A lesson you finished has a "Practise again" button on the lessons
  page. You can also right-click the book on your hotbar. Esc or J closes it.
- **F6**: save the game (to the session folder).
- **F8**: report a bug or ask for a feature (see "Reports" below).
- **Esc** always gets you out of whatever you are doing. It never ends the playtest by itself, except on the final
  thank-you screen.

## What happens

1. A short **microphone check** (skip it with Esc if you wish).
2. A **short quiz before the game**: 31 short items. You hear a line and choose what it means (16 items), and you say a
   Spanish phrase for an English situation (15 items). Answer as well as you can; "I do not know" (N, or say "no sé") is
   always allowed and is useful to us.
3. **The opening.** You wake at your own camp on a cliff. Short English tips explain looking, walking, jumping, the note
   at your tent (it says the guide's camp is down the trail), collecting and the inventory. Your goal is shown at the top:
   find the guide. The beacon points down the trail to her.
4. **The guide's lesson** (about 5 to 8.5 minutes, the walk down the trail included). Luna, the guide, greets you when you
   come close to her, and then teaches you a few short Spanish phrases: hello, yes and no, what something is, please and
   thank you, what to say when you did not understand, and what your camp needs. The English under her lines fades as you
   use the phrases. At the end the book opens on its phrases page.
5. **The camp.** Gather wood and berries, make a torch at the workbench, ask the shopkeeper for water and the villager
   for the river, and fill your bottle there. The guide may offer you water too.
6. **The ending.** Take the spare torch back up the trail and place it on the rim of the fire ring at your own camp. The
   fire lights, and the screen says "Session complete".
7. **A short quiz after the game**, again 31 items, then a thank-you screen. Press **Esc** to close the game.

Please talk out loud while you play if the facilitator asks you to (think aloud). Tell them what you tried, and what
you expected.

## Reports (F8)

Press **F8** at any time to report a bug or ask for a feature. The game pauses, asks you what the problem or the wish is,
and keeps a picture of the screen as you saw it. **Ctrl+Enter** saves the report; **Esc** cancels, and then nothing is
kept. A report is a Markdown file and a picture, in the `reports/` folder of your session (see below). It also holds the
game's state (where you stood, your goal and lesson step, the settings) and the last ten things you said or typed, as the
game heard or read them. Nothing is sent.

## Your session record

Every run writes a folder under `~/.local/share/vocaterra/sessions/`. It holds:

- the log of what you did and the answers to the quizzes (`session.jsonl`, `replay.jsonl`);
- the saved game (`playtest.save`), written when you press F6 and again at the end of the run;
- `reports/`, if you pressed F8.

The **book** keeps every line you said or typed during the run, as the game heard or read it (your voice is not kept),
and the words those lines used. The saved game keeps the newest 500 of those lines.

Please keep the folder until the facilitator has collected it. Only the facilitator can read it for the study.

## Notes

- The voices are recordings made by a text-to-speech model. They are not a person.
- This build is private and not for sharing. Please do not pass it on.
- The facilitator's end key (F10) and the facilitator's notes are for the facilitator only.
