"""Verify physical boxes, text positions and rendered corners of native unit fixtures."""
import argparse
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw
import pdfplumber
from pypdf import PdfReader

parser = argparse.ArgumentParser()
parser.add_argument('folders', nargs='+', type=Path)
parser.add_argument('--poppler', required=True, type=Path)
args = parser.parse_args()
pt = 72 / 25.4
for folder in args.folders:
    rows = [line.split('\t') for line in (folder / 'frames.tsv').read_text(encoding='utf-8-sig').splitlines()]
    assert len(rows) == 48
    proof = Image.new('RGB', (740, len(rows) * 65), '#eeeeee')
    draw = ImageDraw.Draw(proof)
    for i, (stem, w, h) in enumerate(rows):
        w, h = float(w), float(h)
        pdf = folder / (stem + '.pdf')
        reader = PdfReader(pdf)
        assert len(reader.pages) == 1
        page = reader.pages[0]
        for box in [page.mediabox, page.cropbox, page.trimbox, page.bleedbox, page.artbox]:
            assert abs(float(box.width) / pt - w) < .01, (stem, 'width', box)
            assert abs(float(box.height) / pt - h) < .01, (stem, 'height', box)
        expected = {'LL': (8, 8), 'LR': (w-25, 8), 'UL': (8, h-15), 'UR': (w-25, h-15), 'CENTER': (w/2, h/2)}
        with pdfplumber.open(pdf) as document:
            words = {word['text']: word for word in document.pages[0].extract_words()}
            # SHX fonts are emitted as vectors and have no extractable PDF text.
            # Check positioned text when available; always render all five markers below.
            for label, (x, y) in (expected.items() if words else []):
                assert label in words, (stem, 'clipped text', label)
                word = words[label]
                assert abs(word['x0'] / pt - x) < .5, (stem, 'wrong horizontal scale', word)
                assert abs((h - word['bottom'] / pt) - y) < 2, (stem, 'wrong vertical scale', word)
                assert 2 < word['height'] / pt < 5, (stem, 'wrong text scale', word)
        prefix = folder / (stem + '-render')
        subprocess.run([str(args.poppler), '-singlefile', '-r', '72', '-png', str(pdf), str(prefix)], check=True, capture_output=True)
        image = Image.open(prefix.with_suffix('.png')).convert('RGB')
        draw.text((5, i * 65 + 5), stem, fill='black')
        for j, label in enumerate(['LL', 'LR', 'UL', 'UR', 'CENTER']):
            x, y = expected[label]
            box = (int((x-2)/w*image.width), int((h-y-7)/h*image.height),
                   int((x+15)/w*image.width), int((h-y+2)/h*image.height))
            crop = image.crop(box)
            assert sum(max(pixel) < 120 for pixel in crop.get_flattened_data()) > 1, (stem, 'blank corner', label)
            if j < 4:
                proof.paste(crop.resize((120, 55)), (200 + j*130, i*65+3))
    proof.save(folder / 'units-proof.png')
    message = f'PASS: {folder.name}; 48 pages, 240 rendered markers at expected physical positions, all five exact physical boxes'
    (folder / 'geometry.log').write_text(message + '\n', encoding='utf-8')
    print(message)
