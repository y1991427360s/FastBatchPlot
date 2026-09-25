"""独立解析生成配置，比较所有非纸张字段；不加载 CAD。"""
from pathlib import Path
import configparser
import hashlib
import json
import sys

def parse(path):
    config = configparser.ConfigParser(interpolation=None, strict=True)
    config.read_string(path.read_bytes().decode('gbk', errors='strict'))
    return {s: dict(config[s]) for s in config.sections()}

root = Path(sys.argv[1])
results = []
changed = {'pmp_filepath', 'paper_name', 'paper_size_x', 'paper_size_y',
           'actual_printable_bounds_llx', 'actual_printable_bounds_lly',
           'actual_printable_bounds_urx', 'actual_printable_bounds_ury',
           'physical_offsetx', 'physical_offsety', 'actual_x', 'actual_y', 'actual_area', 'paper_unit'}
for record in json.loads((root / 'sources.json').read_text(encoding='utf-8-sig')):
    source = Path(record['source'])
    assert hashlib.sha256(source.read_bytes()).hexdigest().upper() == record['sha256']
    original = parse(source)
    generated = parse(root / source.name)
    assert original.keys() == generated.keys()
    for section in original:
        if section == 'Meta':
            assert {k:v for k,v in original[section].items() if k not in changed} == {
                k:v for k,v in generated[section].items() if k not in changed}
        else:
            assert original[section] == generated[section], section
    meta = generated['Meta']
    assert meta['pmp_filepath'] == record['pmp'] and meta['paper_name'] == record['mediaName']
    assert float(meta['paper_size_x']) == 634.25 and float(meta['paper_size_y']) == 301.125
    pmp = parse(root / (source.stem + '.pmp'))
    assert pmp['Meta']['userdef_num'] == '1'
    paper = pmp['user']
    assert paper['paper_name0'] == record['mediaName']
    assert float(paper['size_x0']) == float(paper['urx0']) == 634.25
    assert float(paper['size_y0']) == float(paper['ury0']) == 301.125
    assert float(paper['llx0']) == float(paper['lly0']) == 0
    assert not Path(record['configuration']).exists() and not Path(record['pmp']).exists()
    results.append({'source':str(source),'sha256':record['sha256'], 'all_non_media_fields_unchanged':True,
                    'private_pair_removed':True,'paper_mm':[634.25,301.125]})
assert len(results) == 5
(root / 'verification.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print('INDEPENDENT_PRESET_CHECK_OK=5; all non-media fields unchanged')
