"""只读分析本机 CAD 安装内的文本配置，不加载 CAD 或写入其配置目录。"""
from pathlib import Path
import configparser
import hashlib
import json

root = Path(r"D:\ZWCAD2026\UserDataCache\zh-CN\Plotters")
paths = [root / "DWG to PDF.pc5", root / "ZWPLOT_PDF.pc5",
         root / "PMP Files" / "ZWPLOT_PDF.pmp"]
paths += sorted(root.glob("ZWCAD PDF(*).pc5"))
paths += [Path(r"D:\ZWCAD2026\Support\zh-CN\drivercfg\PDF.ini")]
result = []
for path in paths:
    raw = path.read_bytes()
    tests = {}
    for encoding in ("ascii", "utf-8", "gbk"):
        try:
            raw.decode(encoding, errors="strict")
            tests[encoding] = True
        except UnicodeDecodeError:
            tests[encoding] = False
    parser = configparser.ConfigParser(interpolation=None, strict=True)
    parser.optionxform = str
    parser.read_string(raw.decode("gbk", errors="strict"))
    sections = {section: dict(parser.items(section)) for section in parser.sections()}
    if path.suffix.lower() == ".pmp":
        fields = sections["user"]
        count = int(sections["Meta"]["userdef_num"])
        bases = ("paper_name", "paper_local_name", "size_x", "size_y", "llx", "lly",
                 "urx", "ury", "actual_x", "actual_y", "area", "Unit")
        expected = {name + str(i) for i in range(count) for name in bases}
        user_summary = {
            "count": count,
            "field_count": len(fields),
            "exact_indexed_fields": set(fields) == expected,
            "first": {name: fields[name + "0"] for name in bases},
            "last": {name: fields[name + str(count - 1)] for name in bases},
            "all_units_1": all(fields["Unit" + str(i)] == "1" for i in range(count)),
            "all_zero_margins": all(float(fields["llx" + str(i)]) == 0 and
                                    float(fields["lly" + str(i)]) == 0 and
                                    float(fields["urx" + str(i)]) == float(fields["size_x" + str(i)]) and
                                    float(fields["ury" + str(i)]) == float(fields["size_y" + str(i)])
                                    for i in range(count)),
            "all_area_products": all(abs(float(fields["area" + str(i)]) -
                                          float(fields["actual_x" + str(i)]) *
                                          float(fields["actual_y" + str(i)])) < 1e-6 for i in range(count)),
        }
        sections["user"] = user_summary
    result.append({"path": str(path), "bytes": len(raw), "sha256": hashlib.sha256(raw).hexdigest(),
                   "strict_decoding": tests, "utf8_bom": raw.startswith(b"\xef\xbb\xbf"),
                   "crlf_count": raw.count(b"\r\n"), "bare_lf_count": raw.count(b"\n") - raw.count(b"\r\n"),
                   "sections": sections})
output = Path(__file__).with_name("phase22-zwcad-pc5-inspection.json")
output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(output.resolve())
for item in result:
    print(Path(item["path"]).name, item["bytes"], item["strict_decoding"])
