"""只读核验本机中望 PDF 合并控制映射；不加载 DLL、不连接或启动 CAD。

依赖：pefile、capstone。输出可重现的文件哈希、资源文字和关键指令。
地址对应本次调查的 ZWCAD 2026 文件；其他版本需重新定位，不能复用地址。
"""
from pathlib import Path
import hashlib
import json
import struct
import capstone
import pefile


ROOT = Path(r"D:\ZWCAD2026")
FILES = [
    "ZwPlotConfig.dll",
    "ZwPlotConfigEditor.dll",
    r"zh-CN\ZwPlotConfigEditorRes.dll",
    r"UserDataCache\zh-CN\Plotters\ZWPLOT_PDF.pc5",
]


def disassemble(name, rva, size):
    pe = pefile.PE(str(ROOT / name))
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    return [
        {"rva": hex(i.address - pe.OPTIONAL_HEADER.ImageBase),
         "instruction": f"{i.mnemonic} {i.op_str}"}
        for i in decoder.disasm(pe.get_data(rva, size), pe.OPTIONAL_HEADER.ImageBase + rva)
    ]


def strings():
    pe = pefile.PE(str(ROOT / r"zh-CN\ZwPlotConfigEditorRes.dll"))
    result = {}
    for kind in pe.DIRECTORY_ENTRY_RESOURCE.entries:
        if kind.id != 6:
            continue
        for block in kind.directory.entries:
            for language in block.directory.entries:
                entry = language.data.struct
                data = pe.get_data(entry.OffsetToData, entry.Size)
                offset = 0
                for index in range(16):
                    length = struct.unpack_from("<H", data, offset)[0]
                    offset += 2
                    value = data[offset:offset + length * 2].decode("utf-16le")
                    offset += length * 2
                    resource_id = (block.id - 1) * 16 + index
                    if resource_id in (0x7e7, 0x7e8, 0x7e9):
                        result[hex(resource_id)] = value
    return result


parser = pefile.PE(str(ROOT / "ZwPlotConfig.dll"))
parser_data = (ROOT / "ZwPlotConfig.dll").read_bytes()
field_offset = parser_data.index("lines_overwrite".encode("utf-16le"))
report = {
    "method": "只读磁盘文件静态分析，无 DLL 加载、无 CAD 连接或运行",
    "files": [{"path": str(ROOT / name), "size": (ROOT / name).stat().st_size,
               "sha256": hashlib.sha256((ROOT / name).read_bytes()).hexdigest()}
              for name in FILES],
    "lines_overwrite_string_rva": hex(parser.get_rva_from_offset(field_offset)),
    "parser_read": disassemble("ZwPlotConfig.dll", 0x1adcc, 0x23),
    "parser_write": disassemble("ZwPlotConfig.dll", 0x1dfbb, 0x39),
    "editor_summary": disassemble("ZwPlotConfigEditor.dll", 0x1973a, 0x3b),
    "resources": strings(),
    "interpretation": {
        "lines_overwrite=0": "直线合并（MergeLines=true）",
        "lines_overwrite=1": "直线覆盖（MergeLines=false）",
        "limitation": "证实配置与编辑器语义；未进行 CAD 实际打印验证"
    }
}
assert report["resources"]["0x7e8"] == "直线合并>"
assert report["resources"]["0x7e9"] == "直线覆盖>"
print(json.dumps(report, ensure_ascii=False, indent=2))
