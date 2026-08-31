"""
Starts the Pix2Text FastAPI server on port 8503.
Called automatically by the C# app — do not run manually.
"""
import re

from fastapi import UploadFile, Form

from pix2text import read_img
from pix2text.serve import app, start_server
import pix2text.serve as serve


# Any LaTeX text-mode wrapper command LatexOCR might use to hold a run of Latin
# letters — a misclassified Cyrillic annotation ends up under one of several of
# these, not just \mathrm{}, e.g. \tt{B r r i o n o n o s s u n a} for a mangled
# "биссектриса" (bisector), or short ones like \mathrm{H e} for "не" (no).
TEXT_WRAPPER_RE = re.compile(
    r'\\(?:mathrm|mathbf|mathbb|mathit|mathsf|mathtt'
    r'|textrm|textsc|textbf|textit|textsf|texttt|text|operatorname|tt)\{([^}]*)\}'
)

# In this corpus's observed output, real math variables (A, B, x_1) never appear
# wrapped in a text-mode command — only units and standard function abbreviations
# do. So rather than a length cutoff (which can't tell "cm" from "he" — both 2
# chars), whitelist known-legitimate wrapped content and treat anything else as a
# misclassified annotation leak, however short.
KNOWN_TEXT_TOKENS = {
    'cm', 'dm', 'mm', 'm', 'km', 'kg', 'g', 'min', 's',
    'sin', 'cos', 'tg', 'ctg', 'lim', 'log', 'ln', 'max', 'mod',
}


def is_suspect_formula(text: str) -> bool:
    # Replacement char — failed glyph mapping, never appears in valid LaTeX output.
    if '\ufffd' in text:
        return True
    for m in TEXT_WRAPPER_RE.finditer(text):
        inner = m.group(1).replace(' ', '').replace('~', '')
        # isalnum() (not isalpha()): a garbled Cyrillic word sometimes comes out with a
        # digit standing in for a visually similar Cyrillic letter (e.g. "3" for "З", as
        # in the observed "HpH3MaTa" for "призмата"), which used to slip past a pure
        # isalpha() check. Per KNOWN_TEXT_TOKENS above, legitimate wrapped content is
        # always pure-alpha (a unit or function name) or pure-numeric (untouched, still
        # allowed here via the any(isalpha) guard) — never a letter+digit mix.
        if (inner.isalnum() and any(c.isalpha() for c in inner)
                and inner.lower() not in KNOWN_TEXT_TOKENS):
            return True
    return False


# Separate from Pix2Text's own /pix2text endpoint: recognizes only the formula-typed
# regions of a page image (LatexOCR), discarding the general-text regions (CnOcr)
# that file_type=text_formula would also return — CnOcr's default model has no
# Cyrillic support and garbles Bulgarian prose.
@app.post("/pix2text/formulas")
async def formulas(image: UploadFile, resized_shape: str = Form(default=768)):
    img0 = read_img(image.file, return_type='Image')
    regions = serve.P2T.recognize_text_formula(
        img0, return_text=False, resized_shape=int(resized_shape)
    )
    results = [
        r['text'].strip() for r in regions
        if r.get('type') in ('embedding', 'isolated')
        and len(r['text'].strip()) >= 3
        and not is_suspect_formula(r['text'])
    ]
    return {"formulas": results}


start_server(
    p2t_config={},
    output_md_root_dir="./pix2text_output",
    host="0.0.0.0",
    port=8503,
)
