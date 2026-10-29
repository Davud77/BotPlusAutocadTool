"""Read-only verification of the independent 100-page GOST native fixture."""
import argparse
from pathlib import Path
import pypdf
import pypdfium2 as pdfium
from PIL import Image, ImageDraw

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
parser.add_argument("--expected-count", type=int, default=100)
args = parser.parse_args()
folder = args.directory
expected = [row.split("\t") for row in (folder / "frames.tsv").read_text(encoding="utf-8-sig").splitlines()]
pdf = folder / "drawing_Binder_2.pdf"
reader = pypdf.PdfReader(pdf)
assert len(expected) == len(reader.pages) == args.expected_count
document = pdfium.PdfDocument(pdf)
pt = 72 / 25.4
minimum = 10**9
proof = Image.new("RGB", (640, len(expected) * 80), "#eeeeee")
draw = ImageDraw.Draw(proof)
for i, (page, row) in enumerate(zip(reader.pages, expected)):
    sheet, name, w, h, scale = row
    w, h = float(w), float(h)
    for box in (page.mediabox, page.cropbox, page.trimbox, page.bleedbox, page.artbox):
        assert abs(float(box.width) / pt - w) < .01, (i, name, "width", box)
        assert abs(float(box.height) / pt - h) < .01, (i, name, "height", box)
    native = document[i]
    pw, ph = native.get_size()
    draw.text((5, i * 80 + 8), f"{i+1}. {name} 1:{scale}", fill="black")
    for corner, (x, y) in enumerate(((7, 7), (w-15, 7), (7, h-15), (w-15, h-15))):
        bitmap = native.render(scale=3, crop=(x*pt, y*pt, pw-(x+8)*pt, ph-(y+8)*pt))
        crop = bitmap.to_pil().convert("RGB")
        dark = sum(max(pixel) < 80 for pixel in crop.getdata())
        minimum = min(minimum, dark)
        assert dark > 100, (i+1, name, scale, corner, "missing corner marker", dark)
        proof.paste(crop, (210 + corner*100, i*80 + 5))
        bitmap.close()
    native.close()
document.close()
proof.save(folder / "corner-proof.png")
message = f"PASS: {len(expected)} GOST pages, {len(expected)*4} corner markers, both orientations, 1:1/1:100; minimum dark pixels={minimum}"
(folder / "geometry.log").write_text(message + "\n", encoding="utf-8")
print(message)
