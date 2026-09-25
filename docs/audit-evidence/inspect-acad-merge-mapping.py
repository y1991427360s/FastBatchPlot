from pathlib import Path
import pefile,capstone,struct,hashlib,json,zlib
root=Path(r'D:\Autodesk\CAD2018\AutoCAD 2018'); names=['plotcfg14.dll','pc3edit.dll','zh-CN/pc3EditRes.dll','UserDataCache/Plotters/AutoCAD PDF (General Documentation).pc3']
def dis(name,rva,size):
 p=pefile.PE(str(root/name));base=p.OPTIONAL_HEADER.ImageBase;c=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64)
 return [f'{i.address-base:#x}: {i.mnemonic} {i.op_str}' for i in c.disasm(p.get_data(rva,size),base+rva)]
p=pefile.PE(str(root/'plotcfg14.dll'));base=p.OPTIONAL_HEADER.ImageBase
assert struct.unpack('<Q',p.get_data(0xad238,8))[0]==base+0x799b0
resources={};p=pefile.PE(str(root/'zh-CN/pc3EditRes.dll'))
for t in p.DIRECTORY_ENTRY_RESOURCE.entries:
 if t.id!=6:continue
 for b in t.directory.entries:
  for l in b.directory.entries:
   r=l.data.struct;d=p.get_data(r.OffsetToData,r.Size);o=0
   for i in range(16):
    n=struct.unpack_from('<H',d,o)[0];o+=2;s=d[o:o+n*2].decode('utf-16le');o+=n*2
    if (b.id-1)*16+i in [0x115,0x116]:resources[hex((b.id-1)*16+i)]=s
assert resources=={'0x115':'直线覆盖','0x116':'直线合并'}
report={'method':'只读磁盘静态核验，未加载驱动或操作 CAD','files':[{'path':str(root/n),'sha256':hashlib.sha256((root/n).read_bytes()).hexdigest()} for n in names],'resources':resources,'getter':dis('plotcfg14.dll',0x799b0,5),'setter':dis('plotcfg14.dll',0x79f60,0x10),'editor_summary':dis('pc3edit.dll',0x2a266,0x32),'editor_model':dis('pc3edit.dll',0x2fb8b,0x17),'editor_choices':dis('pc3edit.dll',0x32ec0,0x9c),'mapping':{'FALSE':'直线合并','TRUE':'直线覆盖'}}
print(json.dumps(report,ensure_ascii=False,indent=2))
