# 中望 PDF 直线合并参数静态核验

结论：`[res_color_mem] lines_overwrite=0` 对应直线合并，`=1` 对应直线覆盖。因此 `MergeLines=true` 应写 `0`，`MergeLines=false` 应写 `1`。未指定时保留源配置。

证据来自本机中望 2026 的配置解析器与配置编辑器，不仅依据英文键名推测。

1. `ZwPlotConfig.dll` 的 `readPc5File` 导出函数内，RVA `0x1ADD5` 引用 `lines_overwrite`（字符串 RVA `0x4C890`），RVA `0x1ADCF` 设置缺省整数 `1`，RVA `0x1ADE3` 调用导入的 `GetPrivateProfileIntW`，RVA `0x1ADE9` 将值存入配置对象偏移 `0xF0`。
2. 同一 DLL 的 `writePc5File` 导出函数，RVA `0x1DFBB` 读取对象偏移 `0xF0`，RVA `0x1DFE0` 引用相同 `lines_overwrite` 字符串，RVA `0x1DFEE` 调用 `WritePrivateProfileStringW`。
3. `ZwPlotConfigEditor.dll` RVA `0x19741` 比较所持配置对象偏移 `0xF0` 与零；为零时 RVA `0x1974F` 加载资源 `0x7E8`，否则转到 RVA `0x1976B` 加载资源 `0x7E9`。
4. `zh-CN\ZwPlotConfigEditorRes.dll` 的字符串资源 `0x7E8` 为“直线合并>”，`0x7E9` 为“直线覆盖>”，前缀资源 `0x7E7` 为“合并控制<”。
5. 配置编辑器两个事件处理函数分别在 RVA `0x196A7` 将同一字段写 `1`、RVA `0x196C7` 写 `0`，均随后更新上述摘要。

额外确认：`UserDataCache\zh-CN\Plotters\ZWPLOT_PDF.pc5` 的 `[Standard]` 明确使用 `DriverPath=ZwPDFDriver.dll`、`DriverCfgPath=PDF.ini`、`DeviceName=PDF`，在 `[res_color_mem]` 存在 `raster_resolution_x=72`、`raster_resolution_y=72`、`lines_overwrite=1`，在 `[Retain]` 存在 `layerinclude=1`。这说明默认 `DWG to PDF.pc5` 缺少部分可选字段不能据此推断不支持这些能力。

`inspect-zwcad-merge-mapping.py` 可重现关键指令和资源；实际输出及全部源文件 SHA-256 见 `zwcad-merge-mapping-static.json`。地址仅适用于所记录哈希的版本。

整个核验只读磁盘文件，没有加载 DLL、连接或启动 CAD，没有修改原配置。静态核验确认配置字段语义，实际 PDF 输出效果仍未经过 CAD 验证。

## 光栅缺省与图层读写补充

`phase31-raster-layer-disassembly.txt` 记录配置解析器的引用：RVA `0x1ABFA` / `0x1AC1A` 把 GetPrivateProfileIntW 的缺省参数 r8d 清零，随后读取 raster_resolution_x/y。缺少字段表示驱动缺省（0），不能据 ZWPLOT_PDF 样本把 72 认作所有配置的缺省 DPI。实现保留缺省 0，不替换成猜测值。显式非零光栅或用户覆盖值会与两个矢量轴逐项比较。

layerinclude 在 RVA `0x1B32B` 的缺省为 1；读取后 test/setne 写入布尔字段偏移 `0x311`；writePc5File 在 RVA `0x1E479` 从同一布尔字段规范化写回。其节为 Retain，与 ZWPLOT_PDF.pc5 实例一致。图层 1 表示包含，0 表示不包含。
