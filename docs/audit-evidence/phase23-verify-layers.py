import json
import subprocess
from pathlib import Path
from PIL import Image, ImageChops
from pypdf import PdfReader, PdfWriter
from pypdf.generic import NameObject as N, ArrayObject as A

root = Path(__file__).parent / 'phase23-layers'
source = Image.open(root / 'source.png').convert('RGB')
evidence = []
for name in ('after', 'package-acad', 'package-zwcad'):
    file = root / (name + '.pdf')
    if not file.exists():
        continue
    reader = PdfReader(file)
    props = reader.trailer['/Root']['/OCProperties']
    groups = props['/OCGs']
    assert len(groups) == 2 and len(props['/D']['/OFF']) == 2
    assert groups[0].idnum != groups[1].idnum
    for index, page in enumerate(reader.pages):
        ref = page['/Resources']['/Properties'].raw_get('/L1')
        assert ref.idnum == groups[index].idnum
    subprocess.run(['pdftoppm', '-r', '72', '-png', str(file), str(root/name)], check=True)
    for index in (1,2):
        assert ImageChops.difference(source, Image.open(root/f'{name}-{index}.png').convert('RGB')).getbbox() is None
    # 只打开第一份图纸的隐藏层，第二份同名层必须仍然关闭。
    writer = PdfWriter(clone_from=file)
    out_props = writer._root_object['/OCProperties']
    out_props['/D'][N('/ON')] = A([out_props['/OCGs'][0]])
    out_props['/D'][N('/OFF')] = A([out_props['/OCGs'][1]])
    toggled = root/f'{name}-toggled.pdf'
    with toggled.open('wb') as output:
        writer.write(output)
    subprocess.run(['pdftoppm','-r','72','-png',str(toggled),str(root/f'{name}-toggled')],check=True)
    first=Image.open(root/f'{name}-toggled-1.png').convert('RGB')
    second=Image.open(root/f'{name}-toggled-2.png').convert('RGB')
    assert first.getpixel((180,130)) == (255,0,0)
    assert ImageChops.difference(source,second).getbbox() is None
    evidence.append({'file':str(file.resolve()),'default_pixels_match':True,'separate_layer_toggle':True,'page_layer_references_match_catalog':True})
(root/'verification.json').write_text(json.dumps(evidence,indent=2,ensure_ascii=False),encoding='utf-8')
print(json.dumps(evidence,indent=2,ensure_ascii=False))
