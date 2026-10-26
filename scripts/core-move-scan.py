"""R16 phase 2: classifies every .cs file of src/Vocaterra.Game for a move to Vocaterra.Game.Core.

    python3 -I scripts/core-move-scan.py .

MOVABLE: no token Core's ArchitectureRulesTests bans, no package, and every Game file it needs is MOVABLE too (engine-free, not necessarily
game logic: docs/game/R16_PHASE2.md says which of them move). BLOCKED-self: the file itself uses a banned token or a package (the reason is
listed). BLOCKED-deps: it needs Game files that are blocked (they are listed).
"""
import os, re, sys
root = sys.argv[1]
game = os.path.join(root, "src/Vocaterra.Game")
BANNED = ["System.Random", "new Random", "Random.Shared", "Guid.NewGuid", "DateTime", "DateTimeOffset", "Stopwatch", "Environment.TickCount",
          "TimeProvider", "Thread.Sleep", "Microsoft.Xna", "FontStashSharp", "using Godot"]
DECL = re.compile(r"^\s*(?:public|internal)\s+(?:(?:sealed|static|readonly|abstract|partial)\s+)*(?:record\s+struct|record|class|struct|enum|interface)\s+(\w+)", re.M)
files = {}
for d, dn, fs in os.walk(game):
    dn[:] = [x for x in dn if x not in ("obj", "bin")]
    for f in fs:
        if f.endswith(".cs"):
            p = os.path.join(d, f)
            files[os.path.relpath(p, game)] = open(p, encoding="utf-8").read()
types = {}
for rel, t in files.items():
    for m in DECL.finditer(t):
        types.setdefault(m.group(1), rel)
def strip(t):
    return "\n".join(l.split("//")[0] for l in t.split("\n"))
out = {}
for rel, t in files.items():
    code = strip(t)
    banned = sorted({b for b in BANNED if b in code})
    pkgs = sorted(set(re.findall(r"^using (Vosk|SherpaOnnx|NAudio|OpenTK|Silk[\w.]*|Sdl[\w.]*|Microsoft.ML[\w.]*)", code, re.M)) | ({"SDL (DllImport)"} if "DllImport" in code or "LibraryImport" in code else set()))
    deps = set()
    for name, home in types.items():
        if home != rel and re.search(r"(?<![\w.])" + re.escape(name) + r"(?!\w)", code):
            deps.add(home)
    out[rel] = {"lines": t.count("\n"), "banned": banned, "packages": pkgs, "deps": sorted(deps)}
# A file can move when it has no banned token or package and every Game file it needs can move too.
movable = {r for r, v in out.items() if not v["banned"] and not v["packages"]}
changed = True
while changed:
    changed = False
    for r in list(movable):
        if any(d not in movable for d in out[r]["deps"]):
            movable.discard(r); changed = True
for r in sorted(out):
    v = out[r]
    tag = "MOVABLE" if r in movable else ("BLOCKED-self" if v["banned"] or v["packages"] else "BLOCKED-deps")
    blockers = v["banned"] + v["packages"] if tag == "BLOCKED-self" else [d for d in v["deps"] if d not in movable]
    print(f"{tag:13} {v['lines']:5} {r:55} {', '.join(blockers)[:150]}")
