"""Pack an unpacked .docx tree in the entry order, compression and attributes of a reference .docx.
Usage: python3 -I pack.py <tree> <reference.docx> <out.docx>"""
import os, sys, zipfile
tree, ref, out = sys.argv[1:4]
with zipfile.ZipFile(ref) as zin, zipfile.ZipFile(out, 'w') as zout:
    names = set()
    for info in zin.infolist():
        names.add(info.filename)
        ni = zipfile.ZipInfo(info.filename, date_time=info.date_time)
        ni.compress_type, ni.external_attr, ni.create_system = info.compress_type, info.external_attr, info.create_system
        if info.is_dir():
            zout.writestr(ni, b'')
        else:
            zout.writestr(ni, open(os.path.join(tree, info.filename), 'rb').read())
on_disk = {os.path.relpath(os.path.join(d, f), tree) for d, _, fs in os.walk(tree) for f in fs}
missing = sorted(on_disk - names)
assert not missing, ('files not in the reference', missing)
print('packed', out, os.path.getsize(out), 'bytes')
