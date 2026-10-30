"""Verify native CTB/STB and real queued plot-style fixtures."""
import sys
from pathlib import Path
import pypdfium2 as pdfium
from pypdf import PdfReader
from PIL import Image, ImageDraw

for argument in sys.argv[1:]:
    folder = Path(argument)
    direct = (folder / 'mode.txt').exists()
    mode = (folder / 'mode.txt').read_text().strip() if direct else 'CTB'
    files = ['off.pdf', 'mono.pdf', 'dwg.pdf'] if direct else ['drawing_Binder_2.pdf', 'Выбранная папка PDF/001_Лист_141.pdf']
    proof = Image.new('RGB', (len(files)*200, 230), 'white')
    draw = ImageDraw.Draw(proof)
    colors = []
    for i, name in enumerate(files):
        path = folder / name
        page = PdfReader(path).pages[0]
        assert abs(float(page.mediabox.width)*25.4/72-594)<.01
        assert abs(float(page.mediabox.height)*25.4/72-420)<.01
        doc = pdfium.PdfDocument(path); native = doc[0]; bitmap = native.render(scale=.5)
        image = bitmap.to_pil().convert('RGB')
        x = int(250*72/25.4*.5); y = image.height-int(200*72/25.4*.5)
        color = image.getpixel((x,y)); colors.append(color)
        sample = image.crop((x-20,y-20,x+20,y+20)).resize((200,200))
        proof.paste(sample,(i*200,30));draw.text((i*200+5,5), str(color), fill='black')
        bitmap.close();native.close();doc.close()
    assert colors[0][0]>240 and colors[0][1]<10 and colors[0][2]<10, colors
    if mode == 'CTB':
        assert all(max(color)<10 for color in colors[1:]), colors
    proof.save(folder / 'plot-style-proof.png')
    message = f'PASS: {folder.name}, {mode}; style off/on/from-DWG rendered colors={colors}; exact A2 boxes'
    (folder/'style-render-check.log').write_text(message+'\n',encoding='utf-8')
    print(message)
