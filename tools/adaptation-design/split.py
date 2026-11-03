import re, sys
def body_elements(x):
    """Return (prefix, [(kind, start, end, xml, text)], suffix) for the top-level children of w:body."""
    b0 = x.index('<w:body>') + len('<w:body>')
    b1 = x.rindex('</w:body>')
    body = x[b0:b1]
    els = []
    i = 0
    tok = re.compile(r'<(/?)w:(p|tbl|sdt|sectPr)\b[^>]*?(/?)>')
    depth = 0; start = None; kind = None
    for m in tok.finditer(body):
        closing, name, selfclose = m.group(1), m.group(2), m.group(3)
        if depth == 0 and not closing:
            if selfclose:
                els.append((name, m.start(), m.end())); continue
            start = m.start(); kind = name; depth = 1; continue
        if depth > 0 and name == kind:
            if closing: depth -= 1
            elif not selfclose: depth += 1
            if depth == 0:
                els.append((kind, start, m.end())); start = None; kind = None
    out = []
    for kind, s, e in els:
        xml = body[s:e]
        text = ''.join(re.findall(r'<w:t(?:\s[^>]*)?>([^<]*)</w:t>', xml))
        out.append((kind, b0 + s, b0 + e, xml, text))
    return x[:b0], out, x[b1:]
