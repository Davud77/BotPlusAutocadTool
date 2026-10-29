"""Verify native A2 page bounds and rendered transparency, including background publication."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw
from pypdf import PdfReader
import pypdfium2 as pdfium

for argument in sys.argv[1:]:
    folder = Path(argument)
    direct = (folder / 'off-top.pdf').exists()
    files = [('off-top.pdf', 250, 200, 594, 420), ('on-twist90.pdf', 220, 250, 420, 594)] if direct else [
        ('drawing_Binder_2.pdf', 220, 250, 420, 594), ('Выбранная папка PDF/001_Лист_141.pdf', 220, 250, 420, 594)]
    shades = []
    proof = Image.new('RGB', (400, 230), 'white')
    draw = ImageDraw.Draw(proof)
    for i, (filename, x, y, width, height) in enumerate(files):
        reader = PdfReader(folder / filename)
        assert len(reader.pages) == 1
        page = reader.pages[0]
        for bounds in [page.mediabox, page.cropbox, page.trimbox, page.bleedbox, page.artbox]:
            assert abs(float(bounds.width) * 25.4 / 72 - width) < .01
            assert abs(float(bounds.height) * 25.4 / 72 - height) < .01
        document = pdfium.PdfDocument(folder / filename)
        native = document[0]
        bitmap = native.render(scale=.5)
        image = bitmap.to_pil().convert('RGB')
        px = int(x * 72 / 25.4 * .5)
        py = image.height - int(y * 72 / 25.4 * .5)
        sample = image.crop((px-10, py-10, px+10, py+10))
        shade = sum(sum(pixel)/3 for pixel in sample.getdata()) / 400
        shades.append(shade)
        proof.paste(sample.resize((200, 200)), (i * 200, 30))
        draw.text((i*200 + 5, 6), f'{"OFF" if i == 0 else "ON"}: {shade:.1f}', fill='black')
        bitmap.close()
        native.close()
        document.close()
    assert shades[0] < 10 and 40 < shades[1] < 240 and shades[1]-shades[0] > 30, shades
    proof.save(folder / 'transparency-proof.png')
    message = f'PASS: {folder.name}; exact A2 boxes; transparency off/on grayscale={shades}'
    (folder / 'render-check.log').write_text(message+'\n', encoding='utf-8')
    print(message)
