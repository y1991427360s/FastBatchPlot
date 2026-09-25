from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import DictionaryObject as D, NameObject as N, ArrayObject as A, TextStringObject as S, DecodedStreamObject

root = Path(__file__).parent / 'phase23-layers'
root.mkdir(exist_ok=True)
for index in (1, 2):
    writer = PdfWriter()
    page = writer.add_blank_page(width=300, height=200)
    layer = writer._add_object(D({N('/Type'): N('/OCG'), N('/Name'): S('Hidden CAD layer')}))
    page[N('/Resources')] = D({N('/Properties'): D({N('/L1'): layer})})
    stream = DecodedStreamObject()
    # 蓝色图形为正常内容，红色图形属于关闭的图层。
    stream.set_data(b'0 0 1 rg 20 20 70 70 re f\n/OC /L1 BDC\n1 0 0 rg 160 20 70 70 re f\nEMC\n')
    page[N('/Contents')] = writer._add_object(stream)
    writer._root_object[N('/OCProperties')] = D({N('/OCGs'): A([layer]), N('/D'): D({
        N('/BaseState'): N('/ON'), N('/OFF'): A([layer]), N('/Order'): A([S('Drawing '+str(index)), layer])})})
    with (root / f'source-{index}.pdf').open('wb') as f:
        writer.write(f)
print(root)
