"""Builders for WordprocessingML that match the Adaptation Design Document's own direct formatting.

Inline markup in text: **bold** and `code`. Text is XML-escaped here; callers pass plain Unicode.
Element order inside pPr, rPr, tblPr and tcPr follows the OOXML schema.
"""
import re
from xml.sax.saxutils import escape

INK = '28231D'      # body text
NAVY = '18233A'     # heading 1, table headers, numbers
GREEN = '3A7D44'    # heading 2
GREY = '6C665D'     # lead lines, captions
FONT = '<w:rFonts w:ascii="Aptos" w:hAnsi="Aptos"/>'
MONO = '<w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:cs="Consolas"/>'

CALLOUT = {  # fill, label colour (from the document's own callouts)
    'gold': ('F7EED8', 'D49C2F'),
    'sand': ('F5E8C8', 'D49C2F'),
    'green': ('EDF3E9', '3A7D44'),
    'blue': ('EFF4FA', '25344E'),
    'red': ('FAE9E6', 'A4473C'),
}

_MARK = re.compile(r'(\*\*[^*]+\*\*|`[^`]+`)')


def rpr(bold=False, italic=False, color=INK, sz=20, mono=False):
    return ('<w:rPr>' + (MONO if mono else FONT) + ('<w:b/>' if bold else '<w:b w:val="0"/>')
            + ('<w:i/>' if italic else '') + f'<w:color w:val="{color}"/><w:sz w:val="{sz}"/></w:rPr>')


def run(text, props):
    return f'<w:r>{props}<w:t xml:space="preserve">{escape(text)}</w:t></w:r>'


def runs(text, color=INK, sz=20, bold=False, italic=False):
    out = []
    for part in _MARK.split(text):
        if not part:
            continue
        if part.startswith('**') and part.endswith('**'):
            # Code inside bold text renders as bold code, not as literal backticks.
            for inner in re.split(r'(`[^`]+`)', part[2:-2]):
                if not inner:
                    continue
                if inner.startswith('`') and inner.endswith('`'):
                    out.append(run(inner[1:-1], rpr(True, italic, color, max(14, sz - 2), mono=True)))
                else:
                    out.append(run(inner, rpr(True, italic, color, sz)))
        elif part.startswith('`') and part.endswith('`'):
            out.append(run(part[1:-1], rpr(bold, italic, color, max(14, sz - 2), mono=True)))
        else:
            out.append(run(part, rpr(bold, italic, color, sz)))
    return ''.join(out)


def ppr(style=None, keep_next=False, before=None, after=120, line=True, ind=None, jc=None):
    s = '<w:pPr>'
    if style:
        s += f'<w:pStyle w:val="{style}"/>'
    if keep_next:
        s += '<w:keepNext/>'
    s += '<w:keepLines/>'
    sp = '<w:spacing'
    if before is not None:
        sp += f' w:before="{before}"'
    sp += f' w:after="{after}"'
    if line:
        sp += ' w:line="259" w:lineRule="auto"'
    s += sp + '/>'
    if ind:
        s += f'<w:ind w:left="{ind[0]}" w:hanging="{ind[1]}"/>'
    if jc:
        s += f'<w:jc w:val="{jc}"/>'
    return s + '</w:pPr>'


def h1(text):
    return ('<w:p>' + ppr('Heading1', True, 240, 100, line=False)
            + run(text, rpr(True, False, NAVY, 38)) + '</w:p>')


def h2(text):
    return ('<w:p>' + ppr('Heading2', True, 160, 80, line=False)
            + run(text, rpr(True, False, GREEN, 26)) + '</w:p>')


def h3(text):
    return ('<w:p>' + ppr('Heading3', True, 120, 60, line=False)
            + run(text, rpr(True, False, NAVY, 22)) + '</w:p>')


def lead(text):
    return '<w:p>' + ppr(after=200, line=False) + runs(text, GREY, 21, italic=True) + '</w:p>'


def p(text, after=120, keep_next=False):
    return '<w:p>' + ppr(keep_next=keep_next, after=after) + runs(text) + '</w:p>'


def bullet(text, level=1):
    style = 'ListBullet' if level == 1 else 'ListBullet2'
    return '<w:p>' + ppr(style, after=60, line=False) + runs(text) + '</w:p>'


def num(n, text):
    return ('<w:p>' + ppr(after=80, line=False, ind=(317, 259))
            + run(f'{n}.  ', rpr(True, False, NAVY, 20)) + runs(text) + '</w:p>')


def spacer():
    return '<w:p><w:pPr><w:keepLines/><w:spacing w:after="0"/></w:pPr></w:p>'


def page_break():
    return '<w:p><w:r><w:br w:type="page"/></w:r></w:p>'


_CELL_MAR = ('<w:tcMar><w:top w:w="80" w:type="dxa"/><w:start w:w="120" w:type="dxa"/>'
             '<w:bottom w:w="80" w:type="dxa"/><w:end w:w="120" w:type="dxa"/></w:tcMar>')


def _cell(width, content, header=False, fill=None):
    paras = content if isinstance(content, list) else [content]
    tc = f'<w:tc><w:tcPr><w:tcW w:type="dxa" w:w="{width}"/>'
    if fill:
        tc += f'<w:shd w:val="clear" w:color="auto" w:fill="{fill}"/>'
    tc += _CELL_MAR + f'<w:vAlign w:val="{"center" if header else "top"}"/></w:tcPr>'
    for i, para in enumerate(paras):
        after = 0 if i == len(paras) - 1 else 60
        tc += f'<w:p><w:pPr><w:spacing w:after="{after}"/></w:pPr>'
        tc += runs(para, 'FFFFFF' if header else INK, 18, bold=header) + '</w:p>'
    return tc + '</w:tc>'


def table(widths, header, rows):
    """A data table in the document's style: navy header row, 9-point cells, widths in DXA summing to 10368."""
    assert sum(widths) == 10368, sum(widths)
    t = ('<w:tbl><w:tblPr><w:tblStyle w:val="TableGrid"/><w:tblW w:type="dxa" w:w="10368"/><w:jc w:val="center"/>'
         '<w:tblLayout w:type="fixed"/>'
         '<w:tblLook w:val="04A0" w:firstRow="1" w:lastRow="0" w:firstColumn="1" w:lastColumn="0" w:noHBand="0" w:noVBand="1"/>'
         '</w:tblPr><w:tblGrid>' + ''.join(f'<w:gridCol w:w="{w}"/>' for w in widths) + '</w:tblGrid>')
    t += '<w:tr><w:trPr><w:cantSplit/><w:tblHeader/></w:trPr>'
    t += ''.join(_cell(w, h, header=True, fill=NAVY) for w, h in zip(widths, header)) + '</w:tr>'
    for row in rows:
        assert len(row) == len(widths), row
        t += '<w:tr><w:trPr><w:cantSplit/></w:trPr>' + ''.join(_cell(w, c) for w, c in zip(widths, row)) + '</w:tr>'
    return t + '</w:tbl>'


def callout(label, paras, kind='blue'):
    fill, label_color = CALLOUT[kind]
    t = ('<w:tbl><w:tblPr><w:tblW w:type="dxa" w:w="10368"/><w:jc w:val="center"/><w:tblLayout w:type="fixed"/>'
         '<w:tblLook w:val="04A0" w:firstRow="1" w:lastRow="0" w:firstColumn="1" w:lastColumn="0" w:noHBand="0" w:noVBand="1"/>'
         '</w:tblPr><w:tblGrid><w:gridCol w:w="10368"/></w:tblGrid><w:tr><w:trPr><w:cantSplit/></w:trPr><w:tc><w:tcPr>'
         f'<w:tcW w:type="dxa" w:w="10368"/><w:shd w:val="clear" w:color="auto" w:fill="{fill}"/>'
         '<w:tcMar><w:top w:w="150" w:type="dxa"/><w:start w:w="180" w:type="dxa"/><w:bottom w:w="150" w:type="dxa"/>'
         '<w:end w:w="180" w:type="dxa"/></w:tcMar></w:tcPr>')
    t += '<w:p><w:pPr><w:spacing w:after="100"/></w:pPr>' + run(label, rpr(True, False, label_color, 18)) + '</w:p>'
    for i, para in enumerate(paras):
        after = 0 if i == len(paras) - 1 else 80
        t += f'<w:p><w:pPr><w:spacing w:after="{after}"/></w:pPr>' + runs(para) + '</w:p>'
    return t + '</w:tc></w:tr></w:tbl>'


def code(text, fill='F4F1EC'):
    """A monospace block: one paragraph per line, inside a light single-cell table."""
    lines = text.strip('\n').split('\n')
    t = ('<w:tbl><w:tblPr><w:tblW w:type="dxa" w:w="10368"/><w:jc w:val="center"/><w:tblLayout w:type="fixed"/>'
         '<w:tblLook w:val="04A0" w:firstRow="0" w:lastRow="0" w:firstColumn="0" w:lastColumn="0" w:noHBand="1" w:noVBand="1"/>'
         '</w:tblPr><w:tblGrid><w:gridCol w:w="10368"/></w:tblGrid><w:tr><w:tc><w:tcPr>'
         f'<w:tcW w:type="dxa" w:w="10368"/><w:shd w:val="clear" w:color="auto" w:fill="{fill}"/>'
         '<w:tcMar><w:top w:w="100" w:type="dxa"/><w:start w:w="160" w:type="dxa"/><w:bottom w:w="100" w:type="dxa"/>'
         '<w:end w:w="160" w:type="dxa"/></w:tcMar></w:tcPr>')
    for line in lines:
        t += ('<w:p><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/></w:pPr>'
              + run(line, rpr(False, False, INK, 16, mono=True)) + '</w:p>')
    return t + '</w:tc></w:tr></w:tbl>'
