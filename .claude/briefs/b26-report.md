B26 report (agent ae7a6ec928e959ac0, Haiku 5.5, 2026-11-17; round 1 about 13:30 UTC, round 2 about 16:05)

Branch `B26-game-licence-scan`, head 9d7c7c8b (merge cd3d1db9 of feature/biomes f9093bb4 on aef0e1aa), worktree
`/home/james/Dev/Vocaterra/.claude/worktrees/b26`, clean.
Round 1: scans of Vocaterra.Host and SpeechRender PASSED (14 packages each); Vocaterra.Game 28 pass, 1 UNKNOWN. Zlib joined
license-allowlist.txt (FontStashSharp.Base, .MonoGame, .Rasterizers: nuspec `Zlib`); overrides with the licence files read from
~/.nuget/packages: Microsoft.DotNet.PlatformAbstractions 3.1.6 MIT, NVorbis 0.10.4 MIT, MonoGame.Library.SDL 2.32.10.2 Zlib. Stopped on
MonoGame.Library.OpenAL 1.24.3.4: its files name only BSD-3-Clause portions, and it ships OpenAL Soft's native library (LGPL-2.0-or-later):
asked of the owner, who accepted it (D-game-34 item 49).
Round 2: scripts/license-exceptions.txt (`<id>|<version>|<spdx>|<evidence>|<reviewed-on>|<reason>`), read by license-scan.sh; an entry
applies only to a non-PASS verdict of its exact id and version, the allowlist unchanged (other LGPL, GPL or unknown packages still fail);
one entry, OpenAL 1.24.3.4, LGPL-2.0-or-later, evidence https://github.com/kcat/openal-soft/blob/master/COPYING, the owner's reason.
merge-gate.sh scans Vocaterra.Game, Vocaterra.Host and SpeechRender into the licence log, failing the gate like the others.
Scans: Game 29/29, Host 14/14, SpeechRender 14/14, the core 30/30, Game.Core.Tests 30/30, all PASSED. docs/game/building-and-testing.md
"The licence scan". Not run: the full merge gate.
