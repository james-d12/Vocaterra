"""Lists the Vocaterra.* usings of the given files that no type of that namespace is named in (in code or a cref).

    python3 -I scripts/audit-usings.py . <file>...

dotnet format does not report unused usings here (IDE0005 is not enforced). A namespace used only through members (an extension method the
scan does not see) is listed too, and so is Program.cs's `using Vocaterra.Game` (a known false report): let the build confirm a removal.
"""
import os, re, sys
root = sys.argv[1]
files = sys.argv[2:]
DECL = re.compile(r"^\s*(?:public|internal|private|protected)?\s*(?:(?:sealed|static|readonly|abstract|partial|file)\s+)*(?:record\s+struct|record|class|struct|enum|interface)\s+(\w+)")
DELEG = re.compile(r"^\s*(?:public|internal)\s+delegate\s+[\w<>?,\s]+?\s(\w+)\s*\(")
byns = {}
ext = {}
for top in ("src", "tests", "tools"):
    for d, dn, fs in os.walk(os.path.join(root, top)):
        dn[:] = [x for x in dn if x not in ("obj", "bin")]
        for f in fs:
            if f.endswith(".cs"):
                t = open(os.path.join(d, f), encoding="utf-8").read()
                m = re.search(r"^namespace ([\w.]+);", t, re.M)
                if not m: continue
                s = byns.setdefault(m.group(1), set())
                for l in t.split("\n"):
                    x = DECL.match(l) or DELEG.match(l)
                    if x: s.add(x.group(1))
                for em in re.finditer(r"public static [\w<>?,() ]+? (\w+)(?:<[^>]*>)?\(this ", t):
                    ext.setdefault(m.group(1), set()).add(em.group(1))
for f in files:
    p = os.path.join(root, f)
    if not os.path.exists(p) or not f.endswith(".cs"): continue
    t = open(p, encoding="utf-8").read()
    body = "\n".join(l for l in t.split("\n") if not l.startswith("using "))
    for u in re.findall(r"^using (Vocaterra\.[\w.]+);", t, re.M):
        names = byns.get(u, set())
        hit = [n for n in names if re.search(r"(?<![\w.])" + n + r"(?!\w)", body)]
        hit += [n for n in ext.get(u, set()) if "." + n + "(" in body]
        if not hit:
            print("UNUSED", f, u)
