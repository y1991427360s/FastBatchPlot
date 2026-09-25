# 阶段 31：中望 PDF 参数补齐

## 更正与实现

此前依据默认 PC5 未写出光栅和图层字段，将这两项判为暂不支持，证据不足。读取同安装目录 ZWPLOT_PDF.pc5，并静态核验 ZwPlotConfig.dll、ZwPlotConfigEditor.dll 与中文资源后，已确认并接入以下映射：

| 面板参数 | 中望原生 PC5 字段 | 取值 |
| --- | --- | --- |
| 矢量分辨率 | res_color_mem/resolution_x、resolution_y | 显式 DPI |
| 光栅分辨率 | res_color_mem/raster_resolution_x、raster_resolution_y | 显式 DPI |
| 文字转图形 | res_color_mem/truetype_as_text | 开启为 0，关闭为 1；只控制 TrueType |
| 输出 PDF 图层 | Retain/layerinclude | 开启为 1，关闭为 0 |
| 直线合并 | res_color_mem/lines_overwrite | 开启为 0，关闭为 1（线条覆盖） |

面板已开放中望五项参数，恢复正确的“矢量分辨率”名称。所有选项保留跟随驱动/显式覆盖，仍进入默认设置、任务副本、历史与重试。阶段 32 已继续核实并开放 AutoCAD 直线合并；当前两宿主均支持上述五项，见 PHASE32_AUTOCAD_LINE_MERGE.md。

## 校验及保护

- 中望两项 DPI 均明确时，面板直接阻止光栅大于矢量。
- 创建私有配置时检查两个轴；一项跟随驱动时读取源字段参与比较。无效或缺失的必要字段明确报错，不静默截断 DPI。
- 原生解析器对缺少的光栅字段读取 0；该值交由驱动决定，不能当作实际 0 DPI 或推测为 72 DPI。跟随时保持缺省值不动；实际驱动最终默认值不在本次静态验证范围。
- 未覆盖的字段，包括未知字段、图层和直线合并原值，保持不变。源 PC5 与 PMP 不改写。已有 PMP 引用、私有数据、未知驱动保护保持原规则；ZWPLOT_PDF 用于只读字段证据，不因其含 PMP/私有数据而绕开保护直接生成。
- 使用相互独立的矢量和光栅字段，避免光栅调整污染矢量输出。

## 静态证据

详见 `audit-evidence/phase31-driver-evidence.json`、`zwcad-merge-mapping-static.md`、`zwcad-merge-mapping-static.json`、`phase31-raster-layer-disassembly.txt`。源文件路径和 SHA-256 已记录，合并语义包含配置读写、编辑器分支及中文资源的完整对应关系。没有启动或加载这些驱动 DLL。

## 验证

- 512 项核心测试、314 项离线 UI 检查通过，.NET Framework 独立检查通过。
- 两套新包分别在 Windows PowerShell 独立进程中加载最新 Core/UI 程序集，完成主窗、五项中望参数、历史与 PDF 检查，均正常退出。
- 五种中望官方预设及 AutoCAD General Documentation 的私有配置检查通过，原配置 SHA-256 和修改时间不变，私有临时文件正常清理。
- 构建包：`artifacts/FastBatchPlot-20260920-194833-612`；两宿主 Release 构建均为 0 警告、0 错误。八个项目 DLL 的绝对路径、时间、大小与 SHA-256 已记录，并核对时间不早于本次构建起点。
- 证据：`audit-evidence/phase31-build-start.txt`、`phase31-build.log`、`phase31-artifacts.json`、`phase31-package.log`、`phase31-parameters-ui.log`、`phase31-framework.log`、`test-results/phase31-parameters.trx`。原驱动核验记录位于 `phase31-native-AutoCAD2018/sources.json` 与 `phase31-native-ZWCAD2026/sources.json`。
- 界面截图：`audit-evidence/phase31-pdf-parameters-zwcad.png`、`phase31-pdf-parameters-autocad.png`。最小窗口控件和提示完整可见。

源码和文件字段验证不等于 CAD 原生出图验收。未安装或加载 CAD 插件；仍须在用户明确授权后验证 PDF 驱动接受配置、真实 DPI、文字、图层和线条交叠效果。

图章模块继续排除。整体 PDF 功能对齐尚未全部完成。
