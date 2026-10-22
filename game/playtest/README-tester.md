# Vocaterra playtest (private build)

Thank you for helping. This is an early prototype of a small game in which you walk through a forest, gather wood and
berries, craft a torch, ask people for things in Spanish and fill a bottle at a river. It takes about 10 to 15 minutes.

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

If you see "Permission denied", run `chmod +x playtest.sh` once and try again.

## Controls

- **Move:** W, A, S, D (or the arrow keys). **Jump:** Space. **Look:** the mouse.
- **Gather:** hold the left mouse button on a tree or a berry bush. **Place:** the right mouse button.
- **Talk to a person:** point the crosshair at them and press **E**. Then:
  - **V** (hold it) or **Space** (hold it) to speak; let go when you have finished.
  - **T** to type; press Enter to send.
  - **R** repeats what they said, **S** says it more slowly, **H** asks for help, **N** means "I do not know".
  - Numbers **1, 2, 3** choose one of the offered answers.
- **C**: the crafting table (stand next to the table). **I**: the inventory. **J**: the notes on words you have met.
- **Esc** always gets you out of whatever you are doing. It never ends the playtest by itself, except on the final
  thank-you screen.

## What happens

1. A short **microphone check** (skip it with Esc if you wish).
2. A **short quiz before the game**: you hear a line and choose what it means, and you say a few things in English
   situations. Answer as well as you can; "I do not know" is always allowed and is useful to us.
3. The game. Find what you need: wood and berries, a torch, and water for a bottle. The people in the village can help.
4. When you are done, a **short quiz after the game**, then a thank-you screen. Press **Esc** to close the game.

Please talk out loud while you play if the facilitator asks you to (think aloud). Tell them what you tried, and what
you expected.

## Your session record

Every run writes a folder under `~/.local/share/vocaterra/sessions/`. It holds the log of what you did and the answers
to the quizzes (`session.jsonl`, `replay.jsonl`) and, after the quiz at the end, the saved game (`playtest.save`).
Please keep the folder until the facilitator has collected it. Only the facilitator can read it for the study.

## Notes

- The voices are recordings made by a text-to-speech model. They are not a person.
- This build is private and not for sharing. Please do not pass it on.
- The facilitator's end key (F10) and the facilitator's notes are for the facilitator only.
