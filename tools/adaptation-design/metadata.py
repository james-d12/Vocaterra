"""Set reproducible descriptive metadata for the current design export."""
import sys
from pathlib import Path
from xml.sax.saxutils import escape
import re


path = Path(sys.argv[1]) / 'docProps' / 'core.xml'
xml = path.read_text(encoding='utf-8')
values = {
    'dc:subject': 'Player needs, repeatable activities and Spanish learning in Vocaterra',
    'dc:description': 'Design requirements, implementation sequence, pilot criteria and open decisions.',
    'cp:lastModifiedBy': 'Codex',
    'cp:revision': '4',
    'dcterms:modified': '2026-11-04T00:00:00Z',
}
for tag, value in values.items():
    xml, count = re.subn(
        rf'(<{tag}(?:\s[^>]*)?>).*?(</{tag}>)',
        lambda match: match[1] + escape(value) + match[2],
        xml,
        flags=re.DOTALL,
    )
    assert count == 1, (tag, count)
path.write_text(xml, encoding='utf-8')
