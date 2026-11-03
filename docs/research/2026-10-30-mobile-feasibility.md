# Vocaterra on iOS and Android: feasibility report

Desk research by R2, 2026-10-30, written on `feature/mobile-research` (after `604903f`). It answers the owner's request: "research into if this game would
be available for mobile (ios, android), how big of a task that would be, what would need to fundamentally change". The owner set the scope:
the **same game** (the 3D first-person voxel game with touch controls, not a redesign or a companion app); a **report only** (no toolchain
installed, nothing built, no spikes); the owner has an iPhone or iPad but **no Mac and no Android phone**. No code was changed. Claims
about this repository cite `path:line`; claims about other projects cite a source (section 9). What could not be confirmed from a source is
marked *unverified*.

## 1. The short answer

**Yes, the game can go to mobile, and the engine-free split made in the S series and R16 makes most of the port additive.** No rewrite is
needed: the simulation, the language core and the content run unchanged, MonoGame ships iOS and Android back ends of the same version the
game uses, and the renderer uses nothing a phone lacks. What must change is the edge of the game: how it starts, where it reads and writes
files, how it is controlled, how its screens fit a phone, how hard it draws, and how the one native speech library is built for each phone.

| | Android | iOS (added after Android) |
| --- | --- | --- |
| Size | about **15 to 20 milestones** (S/M/L sizes in section 3), roughly two-thirds of the S series by count | about **6 to 9 more** |
| Biggest risk | frame time and heat on mid-range phones (section 3.6) | **Vosk has no iOS build**; one must be made (section 3.7) |
| Can be built here | yes: the .NET Android workload runs on Linux | **no**: iOS needs a Mac with Xcode (a cloud Mac works) |
| Owner prerequisites | an Android phone for testing (the emulator is heavy on this machine) | a cloud Mac or a Mac, and the Apple Developer Program |

The order this report recommends: core preparation first (section 3.8 and 3.9), Android second, iOS third (section 5).

## 2. What carries over unchanged

- **The engine-free layers.** `Vocaterra.Game.Core` (about 34,000 lines) and the core reference no engine package; `ArchitectureRulesTests`
  and `LayeringTests` keep it that way. The screens read input through `IInputFrame` and `GameKey`
  (`src/Vocaterra.Game.Core/Input/IInputFrame.cs`), and the microphone through `ISpeechCapture`
  (`src/Vocaterra.Game.Core/Input/ISpeechCapture.cs`). Only `src/Vocaterra.Game` (about 16,800 lines) names MonoGame.
- **The commands.** Keys call `GameCommands` through `KeyBindings` (`src/Vocaterra.Game.Core/Input/KeyBindings.cs:18-44`), so an on-screen
  button can call the same command a key does. No game action is reachable only through a key handler.
- **The renderer.** Only the stock `BasicEffect` and `AlphaTestEffect` (`src/Vocaterra.Game/Graphics/World/VoxelRenderer.cs:131,142`,
  `src/Vocaterra.Game/Graphics/Sky/SkyRenderer.cs:35`); no custom shader, so no shader rebuild for OpenGL ES. No content pipeline: the block
  atlas is drawn in code, the font is a TTF read through FontStashSharp (`src/Vocaterra.Game/Vocaterra.Game.csproj:3-6`), and index buffers
  are 16-bit (`VoxelRenderer.cs:600`), which every OpenGL ES device supports.
- **MonoGame itself.** `MonoGame.Framework.iOS` and `MonoGame.Framework.Android` are published at 3.8.6, the version the game pins. Their
  projects target `net8.0-ios` and `net8.0-android` (API 23 and up), which `net10.0-ios` and `net10.0-android` apps consume. Both use OpenGL
  (ES) and OpenAL Soft. The templates are `mgandroid` and `mgios`.
- **Audio playback and capture compile on phones.** MonoGame's OpenAL build compiles `Microphone.OpenAL.cs` and
  `DynamicSoundEffectInstance.OpenAL.cs` for Android and iOS as well as DesktopGL (MonoGame `Platform/OpenAL.targets`, 3.8.6). So
  `MicrophoneCapture` (`src/Vocaterra.Game/Sound/MicrophoneCapture.cs:42`) and the voice output (`src/Vocaterra.Game/Sound/DynamicSoundOutput.cs`)
  have a mobile implementation. Whether capture *works* on a device (permissions, the iOS audio session) is *unverified* (section 3.7).
- **Speech needs little.** The game uses one recogniser and one model: Vosk with `vosk-model-small-es-0.42` (60 MB unpacked, Apache-2.0;
  `models/manifest.json`). The NPCs speak pre-rendered WAVs from `content/game-v1/speech` (`src/Vocaterra.Game/Program.cs:108-112`: "the
  playback never builds Kokoro"). Kokoro (401 MB), Parakeet (671 MB), wav2vec2 and e5 are tools or developer switches and never ship.
- **The GPL question is already solved.** The playtest build drops the sherpa-onnx and ONNX Runtime natives, which carry espeak-ng
  (`src/Vocaterra.Game/Vocaterra.Game.csproj:24-28,37-42`, checked by `scripts/playtest-build.sh`). A mobile build reuses that switch.
- **Speech can fail safely.** If Vosk cannot load, the game says so and the player types (`src/Vocaterra.Game.Core/Language/Speech/SpeechEngine.cs:196-199`).
  A phone without a working recogniser still plays the whole game (D-game-8: nothing is gated behind voice).
- **No forbidden runtime features.** No `Process.Start`, `Assembly.Load`, `Reflection.Emit` or `Activator` anywhere in `src/`. Most JSON is
  read with `JsonDocument` and `Utf8JsonReader`, which are AOT-safe; the lexicon loads from embedded resources.

## 3. What has to change

Each part says what changes, where, whose milestone it is (**core** or **game**, by CLAUDE.md's rule "a core change the game needs is a
core milestone"), the risk, and a size: **S** is one ordinary milestone, **M** two or three (or one design-heavy), **L** four or more.

### 3.1 Platform heads (game, M)

`Vocaterra.Game` is one DesktopGL executable. The standard MonoGame layout becomes: a shared game library holding today's `src/Vocaterra.Game`
code, plus three small heads (`Vocaterra.Game.Desktop`, `Vocaterra.Game.Android` with its `Activity`, `Vocaterra.Game.iOS` with its
`AppDelegate`), each referencing its own `MonoGame.Framework.*` package. The library either multi-targets (`net10.0`, `net10.0-android`,
`net10.0-ios`) or is compiled into each head.

Also changes:
- `Directory.Build.props` pins `RuntimeIdentifier` to `linux-x64` for every game project on a Linux x64 host (the G7 native trim). Mobile
  heads need their own RIDs (`android-arm64`, `ios-arm64`) and must be excluded from that rule.
- `Vocaterra.sln` gains projects that need the `android` and `ios` workloads. A solution filter (like `Vocaterra.Nlp.slnf`) keeps the merge
  gate and Linux CI building without them; the iOS head can only build on macOS.
- `src/Vocaterra.Game/Input/MouseLook.cs:80-84` loads SDL by desktop file names; it stays in the desktop head.

### 3.2 Platform services: files, settings, start-up (game M, core S)

The game finds everything through environment variables, the executable's folder, the working directory and the home folder. On a phone
the app bundle is read-only, the working directory is `/`, and nothing sets environment variables.

| What | Today | On a phone |
| --- | --- | --- |
| Models | `VOCATERRA_MODELS_DIR` (`src/Vocaterra.Nlp/Asr/Models/ModelStore.cs:11-14`, `SpeechEngine.cs:78`) | shipped in the app; Android must unpack them from the APK on first run (assets are not files there); iOS bundle files are readable in place |
| Content bundle | `VOCATERRA_CONTENT_DIR`, else a search up from `AppContext.BaseDirectory` (`src/Vocaterra.Game.Core/Session/GameBundle.cs:19-28`) | same as models; the search finds nothing inside an APK |
| Fonts, sounds | `AppContext.BaseDirectory/assets` (`src/Vocaterra.Game/Startup/AssetPaths.cs:10,16`) | iOS works; Android needs unpacking or streams |
| Manual save (F6) | `vocaterra.save.json` in the working directory (`src/Vocaterra.Game/VoxelGame.cs:81`) | **unwritable**; must move to the app's data folder |
| Sessions, autosave, crash reports | `~/.local/share/vocaterra/sessions` (`src/Vocaterra.Game/Diagnostics/SessionLog.cs:79`, `CrashHandler.cs:101`) | must move to the app's data folder |
| TTS cache | `$XDG_CACHE_HOME` or `~/.cache` (`SpeechEngine.cs:62-71`) | unused (no Kokoro on phones) |
| Vosk runtime copy | `Path.GetTempPath()/slg-vosk-runtime`, symlinks with a copy fallback (`src/Vocaterra.Nlp/Asr/VoskRuntimeModel.cs:38,143-147`) | works in the app's cache folder; costs a 60 MB copy if symlinks are refused |
| Options | about 50 KB of command-line parsing (`src/Vocaterra.Game/Startup/LaunchOptions.cs`) | no command line; the player's options become in-game settings stored in the save |

Most seams already exist: `ModelStore.FromEnvironment` and `SpeechEngine.FindModels` take a variable reader, and `VoskRuntimeModel` takes a
`runtimeRoot`. So the work is mostly in the game (one `IPlatformPaths` service the heads fill, and a first-run unpack step on Android). The
core part (a model store from an explicit folder, a runtime root that is never the shared temp folder) is small.

### 3.3 Touch controls (game, L; design-heavy)

The input surface is keyboard and mouse. `IInputFrame` exposes Shift, Ctrl, two mouse buttons, the wheel and a pointer. The screens read 28
distinct `GameKey`s, for example:
- world: Space, V (push-to-talk), T, E;
- panels: J, C, I, Escape, Enter, Tab;
- lists: Up, Down, PageUp, PageDown;
- text editing: Back, Delete, Left, Right;
- global: F1, F3, F5, F6, F9, F10.

Movement (W, A, S, D and the arrows) and the hotbar slots (1 to 0) are read as engine keys in
`src/Vocaterra.Game/Input/WorldInput.cs:16,48-50`. 19 files in `src/` read `IInputFrame` or `InputSnapshot`.

A phone needs:
- a left-thumb movement stick;
- right-thumb drag to look, replacing SDL relative mouse mode;
- buttons for jump, mine and place (today the left and right mouse buttons), talk, type and inventory;
- a held **push-to-talk** button, which suits a phone well;
- taps on the hotbar, list rows and panel buttons, with swipes replacing PageUp and PageDown;
- a back gesture for Escape.

The clean way is a touch layer in the Game project that issues the existing `GameCommands`, plus a touch-pointer shape in `IInputFrame` (a
small Game.Core change, tested there). This is the largest single part of the port and the one most worth a Sonnet design milestone before
the Haiku work.

### 3.4 Typing on a phone (game, S to M)

Typed answers come through `Window.TextInput` (`src/Vocaterra.Game/VoxelGame.cs:330,778`). MonoGame's mobile text entry is
`KeyboardInput.Show`, which on Android opens a modal dialog with a text field (MonoGame `Platform/Input/KeyboardInput.Android.cs`). A modal
box per typed answer is acceptable for a first port. An in-place field needs native code in each head. The Alt+vowel accent shortcuts
(`src/Vocaterra.Game.Core/Input/AccentShortcuts.cs`) are not needed, because phone keyboards have accents. The Spanish keyboard must be
offered or suggested.

### 3.5 Screens for a phone (game, M to L)

The interface is laid out on a canvas derived from a resizable desktop window and a text-size percentage (`src/Vocaterra.Game/UI/Common/UiCanvas.cs`,
`UiScale.cs`). A phone needs:
- a landscape lock;
- safe areas around notches and rounded corners;
- touch targets of at least about 7 to 9 mm;
- a pass over the dense layers: the book (`UI/Book/BookLayer.cs`, 37 KB), dialogue, crafting and inventory.

Tablets are the easier first target, because their screens are closer to the desktop layouts.

### 3.6 Drawing budget and heat (game, M; the main Android risk)

The quality presets draw 224, 304 and 400 blocks out, with plants, ambient occlusion and clouds on every level
(`src/Vocaterra.Game/Graphics/QualityPreset.cs:41-46`). Chunk meshing runs on the frame thread with a per-frame budget of 4 to 6 ms. The
only measurements are on desktop: software GL (llvmpipe) frame medians of 23 to 49 ms, and about 2 s to mesh 1,762 chunks
(`docs/game/implementation-plan.md:894,922,987`, the S13 and S14 findings).

A phone needs:
- a **Mobile** preset, likely 96 to 160 blocks with fewer plants and no clouds;
- meshing moved to a worker thread, with only the buffer upload on the frame thread;
- a 30 fps target with a frame limiter, to manage battery and heat;
- a check of vertex buffer memory against the smaller GPU memory of phones.

Android may destroy the GL context when the app goes to the background. MonoGame restores what it loaded, but buffers the game built itself
must be rebuilt (a re-mesh). This is *unverified*: test it on a device.

### 3.7 Speech on phones (game and core: Android S, iOS L; the main iOS risk)

The `Vosk` NuGet package (0.3.38) carries linux-x64, win-x64 and osx only (`docs/nlp/tools.md:97-108`). The managed wrapper calls a native
`libvosk`.

- **Android: low risk.** The Vosk project publishes an Android library (`vosk-android` AAR on Maven) carrying `libvosk.so` for the Android ABIs.
  - The head ships that `.so` as an `AndroidNativeLibrary` for `arm64-v8a` (and `x86_64` for the emulator).
  - The managed wrapper's P/Invoke then resolves as on Linux.
  - The Android head must ask for `RECORD_AUDIO` at run time.
- **iOS: the biggest risk of the port.** No prebuilt iOS `libvosk` is published.
  - The Vosk documentation offers build scripts only.
  - The React Native wrapper's README tells users to obtain the iOS library from the maintainers.
  - The Rust bindings' notes say iOS needs a static library split per architecture, plus the Accelerate framework.
  - So someone builds Kaldi, OpenFST and Vosk for `ios-arm64` on a Mac, as a static library or an xcframework. The P/Invoke must then resolve to it: an `__Internal` mapping for a static library, or an embedded `libvosk` framework.
  - This is days of native build work with an uncertain end, and it needs a Mac.
  - Alternatives, in order:
    1. **sherpa-onnx on iOS.** It publishes iOS xcframework and Android releases. It would need a small Spanish model, and the core's constrained decoding (`asr-constrained`, the grammar from `src/Vocaterra.Nlp/Asr/GrammarBuilder.cs`) has no direct equivalent, so the spoken path would change. This is a core milestone, and its espeak-ng part must stay out.
    2. **Apple's `SFSpeechRecognizer`.** It runs on the device for Spanish on recent devices, but it has no grammar, only hints. Its results are not reproducible, and it is a platform service rather than part of the core.
    3. **Ship iOS typing-only first.** The game supports this already (section 2).
- **The iOS audio session.** MonoGame does not set the audio-session category ("Do not override AVAudioSessionCategory set by the game
  developer", MonoGame `OpenALSoundController.cs`). The iOS head must set *play and record* before opening the microphone, and must
  declare `NSMicrophoneUsageDescription`. MonoGame already pauses and resumes audio around interruptions such as calls.
- **ARM64 Vosk.** Vosk's confidences come from native code compiled for a different CPU, so the confidence threshold of D-game-9 should be
  re-checked on a phone (section 3.10).

### 3.8 Trimming and ahead-of-time compilation (core, M)

- **Trimming is on by default for phones.** iOS device builds are always trimmed, and Android Release builds are trimmed (the default `partial` trim mode).
- **That turns off reflection JSON.** When `PublishTrimmed` is on and `JsonSerializerIsReflectionEnabledByDefault` is not set, reflection-based System.Text.Json is turned off automatically.
- **The core relies on reflection JSON.** `CanonicalJson` (`src/Vocaterra.Nlp/Core/Determinism/CanonicalJson.cs:37-47`) calls reflection-based `JsonSerializer` for 17 root types, which covers the save, the manifests, prompts, agents, intents, answer sets, effects, dialogue and replay records. No `JsonSerializerContext` exists, so out of the box a trimmed phone build cannot load the content bundle.
- **No JIT on iOS.** The default iOS runtime is Mono with ahead-of-time compilation. NativeAOT is optional and gives smaller, faster apps, but it requires a build with no trim or AOT warnings.

Two steps, both core:
1. **Short term (S):** the mobile heads set `JsonSerializerIsReflectionEnabledByDefault=true`. With `partial` trimming the app's own
   assemblies are not trimmed, so the reflection serializer keeps working.
2. **Proper fix (M):**
   - Add a source-generated `JsonSerializerContext` for the 17 root types.
   - Replace the non-generic `JsonStringEnumConverter` (`CanonicalJson.cs:28`), which NativeAOT does not support, with `UseStringEnumConverter` or the generic converter.
   - Mark the libraries `IsAotCompatible`.

   The canonical bytes must not move: content hashes and the end-to-end goldens are computed from this serialization, and the existing golden tests are the check. NativeAOT on iOS needs this step.

### 3.9 App lifecycle (game, S to M)

- Save automatically when the app is deactivated (MonoGame's `Game.Deactivated`), because phones kill background apps without notice.
- Do not call `Exit()` on iOS: apps there never quit. The playtest's thank-you screen exits on Escape (`src/Vocaterra.Game/VoxelGame.cs:691`).
- Handle Android's context loss (section 3.6) and audio focus.
- Console output is harmless: it goes to the device log.

### 3.10 Results on ARM64 (core, S)

The goldens and the fuzz oracles are recorded on x64. The core already avoids `Math.Sin` and `Math.Exp` in audio for reproducibility
(`src/Vocaterra.Nlp/Audio/DeterministicMath.cs:6`). The scheduler still uses `Math.Pow` and `Math.Exp`
(`src/Vocaterra.Nlp/Decision/Scheduling/Fsrs.cs:90-157`), but rounds to 8 decimals, which almost always hides last-bit differences between
platforms. `docs/research/2026-10-20-scaling-approaches.md:90` already raises relaxing "bit-identical across x64, ARM64".

Recommendation: run the full suite once on a linux-arm64 machine (a cloud ARM VM is enough) before the port, and decide from the result
whether the cross-platform promise is "bit-identical" or "identical verdicts".

### 3.11 Toolchain, testing, stores (owner and game, S to M)

- **Android** builds on this Linux machine with `dotnet workload install android`, a JDK and the Android SDK.
  - Testing needs a phone. The emulator works, including the host microphone, but it is heavy, and this desktop has crashed from overload before (CLAUDE.md, machine resources). If it is used, it goes through `scripts/limited.sh` rules, one at a time.
- **iOS** cannot be built on Linux: MonoGame's iOS build needs the latest Xcode on a Mac.
  - Without a Mac, the options are a cloud Mac: a macOS CI runner (that waits for the token's workflow scope; nothing is written under `.github/workflows/` until then), Codemagic, or MacinCloud.
  - Shipping, and TestFlight to the owner's iPhone or iPad, needs the Apple Developer Program ($99 a year).
- **Size.** About 40 to 60 MB of .NET runtime and game assemblies, plus the 38 MB bundle (33 MB of it speech WAVs), plus the 60 MB Vosk model (40 MB zipped), for roughly 120 to 160 MB installed.
  - Google Play allows 500 MB for the base download and only warns above 200 MB on mobile data.
  - The App Store asks before cellular downloads over 200 MB.
  - Parakeet and Kokoro could never ship. The speech WAVs could be compressed later.
- **Store pages.**
  - A privacy statement. The microphone is used on the device and nothing leaves it (core D8: no network), which is a selling point.
  - An age rating.
  - Licence notices: Apache-2.0 for the game and the Vosk model, CC0 for the assets (`assets/ATTRIBUTION.md`).
  - OpenAL Soft is LGPL and is already redistributed by the desktop build. Its terms on iOS should go through `scripts/license-scan.sh` and the owner (*unverified*).

## 4. The engine question (D-game-1, P10)

D-game-1 left the final engine open, with Godot .NET to be re-assessed, and P10 is an optional Godot spike. For mobile:

| | MonoGame mobile heads | Godot 4 .NET |
| --- | --- | --- |
| Game code | kept; heads and a touch layer are added | `src/Vocaterra.Game` (about 16,800 lines) rewritten as scenes and nodes |
| C# on phones | stable back ends at the pinned version | C# export to iOS and Android still marked experimental in 4.5 (Godot forum, *unverified* against official 4.5 notes); iOS uses NativeAOT |
| JSON (3.8) | the short-term switch is enough | NativeAOT on iOS makes the source-generated context mandatory |
| Vosk on iOS | must be built (3.7) | the same problem |
| Editor and tooling | none; code only | a full editor, better mobile UI tools |

**Recommendation:** port with MonoGame. The engine-free split means the port adds code rather than rewriting it. Moving to Godot would not
remove the two hardest parts, Vosk on iOS and touch design, and would add a rewrite. The choice is the owner's (D-game-1); this report takes
no decision.

## 5. Proposed order

Each step is a milestone row or rows. The core rows go in `docs/nlp/implementation-plan.md`, and the game rows in a new section of
`docs/game/implementation-plan.md`.

1. **Core preparation** (core, S each):
   - the explicit model and runtime folders (3.2);
   - the JSON reflection switch, documented (3.8 step 1);
   - one ARM64 run of the suite (3.10).
2. **Heads and services** (game, M): split the Game project; add the Android head; add `IPlatformPaths`, first-run unpacking and in-game settings (3.1, 3.2). Exit check: the world draws on the Android emulator.
3. **Mobile preset and worker meshing** (game, M) (3.6). Exit check: a measured frame time on a mid-range Android phone.
4. **Touch design** (game, Sonnet, design), **then touch controls and the phone UI pass** (game, Haiku, L) (3.3, 3.5).
5. **Android speech**: `libvosk.so` from the AAR, the microphone permission, text entry (game, S to M) (3.4, 3.7). Exit check: a spoken turn on a phone.
6. **Lifecycle and an Android store build**: autosave on pause, context loss, the size check, a signed internal-test release (3.9, 3.11).
7. **iOS**:
   - a cloud Mac and the Apple account (owner);
   - the Vosk iOS build, or a decision for an alternative or typing-only (L);
   - the iOS head and audio session (S);
   - optionally the source-generated JSON and NativeAOT (core, M);
   - TestFlight to the owner's device.

Steps 1 to 3 settle the two big unknowns (frame time and the file seams) early. Step 7 can begin with the Vosk build alone, in parallel,
once a Mac is available.

## 6. Owner decisions to raise

None of these is raised in `docs/game/decisions.md` yet; each becomes a decision only when the owner opens it.

1. **Is mobile a goal**, and is Android first acceptable?
2. **The engine for mobile** (D-game-1): MonoGame heads (this report's recommendation) or Godot (P10 first).
3. **iOS build access**: a cloud Mac service and the Apple Developer Program, or a Mac.
4. **iOS speech**: build Vosk for iOS, or switch iOS to sherpa-onnx, Apple's recogniser, or typing-only at first.
5. **Cross-platform results**: keep "bit-identical across x64 and ARM64" or relax it to "identical verdicts" (3.10).
6. **Test hardware**: an Android phone (a mid-range one is the honest frame-time target).

## 7. What this report did not check

- Nothing was built or run on a phone. Frame time, heat, microphone capture on devices, GL context loss and Vosk recognition on ARM64 are
  all *unverified* until steps 2 to 5 of section 5.
- The exact contents of the `vosk-android` AAR (the ABIs and the version) were not opened.
- Whether `Window.TextInput` fires on mobile at all with a hardware keyboard was not checked.

## 8. Facts at a glance

- Game-only native libraries on phones: `libvosk` and OpenAL Soft (with MonoGame's own natives). No sherpa-onnx and no ONNX Runtime.
- Models shipped: `vosk-model-small-es-0.42` only (60 MB).
- Content shipped: `content/game-v1` (38 MB) and `assets/` (1.2 MB).
- Core code touched: `ModelStore`, `VoskRuntimeModel` (folders), `CanonicalJson` (JSON), and possibly the ASR seam if iOS changes recogniser.

## 9. Sources

- MonoGame platforms and templates: <https://docs.monogame.net/articles/getting_started/platforms.html>
- MonoGame 3.8.6 iOS and Android packages: <https://www.nuget.org/packages/monogame.framework.ios>, <https://www.nuget.org/packages/MonoGame.Framework.Android>
- MonoGame 3.8.6 source (`MonoGame.Framework.Android.csproj`, `MonoGame.Framework.iOS.csproj`, `Platform/OpenAL.targets`,
  `Platform/Audio/Microphone.OpenAL.cs`, `Platform/Audio/OpenALSoundController.cs`, `Platform/Input/KeyboardInput.Android.cs`):
  <https://github.com/MonoGame/MonoGame/tree/v3.8.6/MonoGame.Framework>
- Vosk install and Android notes: <https://alphacephei.com/vosk/install>, <https://alphacephei.com/vosk/android>; the NuGet package: <https://www.nuget.org/packages/Vosk/>
- Vosk on iOS (third-party notes): <https://docs.rs/crate/vosk/latest>, <https://www.npmjs.com/package/voicer-rn-vosk>
- sherpa-onnx platforms and releases: <https://k2-fsa.github.io/sherpa/onnx/index.html>, <https://github.com/k2-fsa/sherpa-onnx/releases>
- ONNX Runtime package (Android and iOS natives): <https://www.nuget.org/packages/microsoft.ml.onnxruntime>
- System.Text.Json and trimming: <https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation>
- Trimming defaults on iOS and Android: <https://learn.microsoft.com/en-us/dotnet/maui/deployment/trimming>
- NativeAOT on iOS: <https://learn.microsoft.com/en-us/dotnet/maui/deployment/nativeaot>
- Godot C# on mobile: <https://forum.godotengine.org/t/is-the-ios-support-good-enough-when-using-the-mono-version-for-a-full-release/128950>,
  <https://godotengine.org/article/platform-state-in-csharp-for-godot-4-2/>
- Google Play size limits: <https://support.google.com/googleplay/android-developer/answer/9859372?hl=en>
- App Store cellular download limit: <https://www.iphoneincanada.ca/2019/05/31/cellular-data-limit-app-store>
