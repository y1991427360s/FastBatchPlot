from pathlib import Path
import json, struct, zlib, hashlib

root = Path(__file__).parent
results = []
for directory in root.glob('phase24-generated*'):
    for path in directory.glob('*'):
        if path.suffix.lower() not in ('.pc3', '.pmp'):
            continue
        data = path.read_bytes()
        assert data[:48] == b'PIAFILEVERSION_2.0,PC3VER1,compress\r\npmzlibcodec'
        checksum, size, compressed = struct.unpack('<III', data[48:60])
        assert checksum == zlib.adler32(data[60:])
        assert compressed == len(data)-60
        decoder = zlib.decompressobj()
        decoded = decoder.decompress(data[60:])
        assert decoder.eof and not decoder.unused_data and len(decoded) == size
        assert decoded.endswith(b'\0')
        text = decoded[:-1].decode('gbk', errors='strict')
        stack = [dict()]
        for raw in text.splitlines():
            line = raw.strip()
            if not line:
                continue
            if '=' in line:
                key, value = line.split('=', 1)
                assert key not in stack[-1]
                stack[-1][key] = value[1:] if value.startswith('"') else value
            elif line.endswith('{'):
                key = line[:-1]
                assert key not in stack[-1]
                node = dict()
                stack[-1][key] = node
                stack.append(node)
            else:
                assert line == '}' and len(stack)>1
                stack.pop()
        assert len(stack)==1
        document = stack[0]
        assert document['meta']['canonical_model_name']=='pdf'
        if path.suffix.lower()=='.pmp':
            media=document['udm']['media']
            size_node=media['size']['0']; desc=media['description']['0']
            assert len(media['size']) == len(media['description']) == 1
            assert size_node['media_description_name']==desc['name']
            assert float(desc['media_bounds_urx'])==634.25
            assert float(desc['media_bounds_ury'])==301.125
            assert float(desc['printable_bounds_llx'])==float(desc['printable_bounds_lly'])==0
            assert float(desc['printable_bounds_urx'])==634.25
            assert float(desc['printable_bounds_ury'])==301.125
            assert abs(float(desc['printable_area'])-634.25*301.125)<1e-6
            assert desc['dimensional']=='TRUE'
        else:
            assert float(document['media']['size']['media_description']['media_bounds']['urx'])==634.25
            assert document['meta']['user_defined_model_pathname'].endswith(path.stem+'.pmp')
        results.append(dict(path=str(path.resolve()), bytes=len(data), sha256=hashlib.sha256(data).hexdigest(),
                            zlib_verified=True, fields_verified=True))
(root/'phase24-independent-format-check.json').write_text(json.dumps(results,indent=2,ensure_ascii=False),encoding='utf-8')
assert results
print(json.dumps(results,indent=2,ensure_ascii=False))
