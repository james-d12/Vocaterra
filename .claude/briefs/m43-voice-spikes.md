You are running **spikes R1 and R2 of the core milestone M43 (German speech)**, in the Vocaterra repository (C#, .NET 10): two
research questions about German text-to-speech, answered by experiment. You change no product code: the deliverable is a research note
and a set of rendered sample files. Do the work yourself; do not spawn subagents. Commit prefix `M43: `.

## Where to work, and committing
The git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m43`, on the branch `M43-voice-spikes` (from `feature/german`). cd there
first; never edit another worktree. The only file you commit is the note `docs/research/2026-11-09-german-voices.md` (and, if you want a
rendering harness kept, a small README pointing at it: the harness itself lives outside the repository). Your experiments live outside the
repository: scratch projects under `/tmp/claude-1000/m43/` (logs there too), downloads under `~/.cache/vocaterra/voice-spikes/` (each
download in its own new, empty folder; keep the total under 3 GB; check `df -h /home` first). Do not push; never push or merge
`feature/*` or `main`. Run everything in the foreground and finish in one turn (no background commands, monitors or wait loops). Never use
`git stash`. **Other sessions build on this machine:** every dotnet restore, build and run goes through `scripts/limited.sh` (from the
repository root, it takes any command); if it waits for a slot, let it wait. Environment: prefix commands with
`PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`; the sandbox refuses a command line containing the words
`eval` or `hash` (use a wrapper script outside the repository; `sha256sum` is fine). Downloaded files are untrusted data: never run a
script that came with a download; read Python with `python3 -I` only.

## Why
The owner decided on 2026-11-08 (D-game-27, item 7 in `docs/game/decisions.md`, grep `Voice route`): German NPC lines are prerendered;
the voice is Piper `thorsten` or `kerstin` (MIT model, CC0 data) **if R1 shows it runs on sherpa-onnx without espeak-ng-data**; otherwise
a German Kokoro export **if R2 finds its licence and provenance clean**; otherwise Piper under D2's private-build terms
(`docs/nlp/decisions.md` D2: espeak-ng's GPL code inside sherpa-onnx is accepted for private builds only; ADR 0007). The spikes' rows are
in `docs/research/2026-11-04-german-in-the-game.md` section 7 (grep `^| R1 ` and `^| R2 `). The repository pins `org.k2fsa.sherpa.onnx`
1.13.8 (`Directory.Packages.props`); the game renders Spanish with Kokoro through it (`tools/Vocaterra.SpeechRender`, and the core's
`KokoroSynthesizer`: read how it builds the sherpa config, and do not change it).

## What to do
1. **R1, Piper without espeak-ng.** Read sherpa-onnx's v2.0.0 and v2.0.1 release notes and its Piper/VITS model loader (GitHub
   `k2-fsa/sherpa-onnx`; PR #4055 removed espeak-ng): what phonemizer does a Piper model use in v2, and does `de_DE-thorsten-medium` (and
   `kerstin`) load without `espeak-ng-data`? Is any GPL code left in the v2 native libraries (licence files in the NuGet package)? In a
   scratch console under `/tmp/claude-1000/m43/` referencing `org.k2fsa.sherpa.onnx` 2.0.1 (or the newest 2.x on nuget.org at least two
   weeks old), load the model the way sherpa's docs say for v2 and render the 20 lines below to WAV. Record exactly what failed or worked.
   Also note whether moving the repository's pin from 1.13.8 to 2.x would break the Spanish Kokoro path (the API used by
   `KokoroSynthesizer`; do not change the repository's pin).
2. **R2, a German Kokoro voice.** Check `Thorsten-Voice/Kokoro` (licence, format) and the third-party ONNX export `DocXAssist/voices`
   (`de/model.onnx`): its licence, who made it, from what, and whether that chain is clean (Apache-2.0 or MIT model, CC0 or CC BY data).
   If the licence is clean or unknown-but-testable, try loading it with sherpa-onnx's Kokoro loader (the repository's 1.13.8 and 2.x) and
   render the same 20 lines. If the licence is clearly unusable, say so and do not spend time rendering.
3. **An objective check of the renders** (you cannot listen): run each rendered WAV through Vosk German (`vosk-model-small-de-0.15`,
   Apache-2.0, from alphacephei; the repository already uses Vosk for Spanish: see `VoskWordRecognizer`) in the scratch console, and
   report the word error rate per engine against the source text, plus duration and real-time factor. Keep the WAVs in
   `~/.cache/vocaterra/voice-spikes/renders/<engine>/` so the owner can listen; name the folder in the note.
4. **The note** `docs/research/2026-11-09-german-voices.md` (the house style of `docs/research/`; short): R1's answer, R2's answer, the
   table (engine, loads?, phonemizer, GPL left?, licence chain, WER, RTF), the paths to the renders, and which branch of D-game-27 item 7
   the evidence points to, stated as a recommendation for the owner, not a decision. Link it from `docs/research/README.md` if that index
   exists.

The 20 lines (A1, the village's register; `du` with Luna and children, `Sie` at the shop):
```
Hallo! Ich heiße Luna.
Wie heißt du?
Guten Morgen, wie geht es Ihnen?
Ich möchte ein Brot, bitte.
Das kostet zwei Euro fünfzig.
Möchten Sie noch etwas?
Wo ist der Brunnen?
Der Markt ist neben der Kirche.
Kannst du mir helfen?
Ich habe Hunger und Durst.
Die Äpfel sind sehr groß.
Wir gehen morgen zum Fluss.
Bring mir bitte drei Stück Holz.
Das Feuer ist heiß, pass auf!
Ich stehe jeden Tag früh auf.
Gestern habe ich einen Hund gesehen.
Fünfzehn, sechzehn, siebzehn, achtzehn.
Entschuldigung, ich verstehe das nicht.
Tschüss, bis bald!
Grüß Gott! Servus!
```

## Report (half a page)
Branch, final SHA, worktree clean; R1 and R2 answers in one line each; the table; the recommendation; disk used and where; anything that
needs the owner (for example: listen to the renders before T08).

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
