# 阶段 27：PDF 参数与历史性能

> 阶段 31 更正：以下支持矩阵记录阶段 27 当时状态。随后通过本机配置解析器与编辑器静态核验，确认中望支持独立光栅、图层与直线合并，已开放面板；“统一输出 DPI”及不能映射 lines_overwrite 的旧结论已撤回。当前范围见 [阶段 31](PHASE31_ZWCAD_PDF_PARAMETERS.md)。

## 本轮实现

主窗口书签旁新增“PDF 参数…”窗口。所有选项默认跟随驱动；用可空 DPI 和布尔值保留三态，不把“关闭”混同于“默认”。参数进入用户设置、冻结任务副本、历史记录和失败重试，历史详情显示原参数。旧配置和旧历史缺字段时沿用驱动；不合法 DPI 拒绝保存或恢复。

| 参数 | AutoCAD 2018 原生 PDF | 中望 2026 原生 PDF |
| --- | --- | --- |
| 分辨率 | 矢量 DPI | 统一输出 DPI |
| 独立光栅 DPI | 彩色及单色光栅上限 | 暂不支持覆盖 |
| 文字转图形 | All_As_Geometry | TrueType 文字开关的反向值 |
| PDF 图层 | Include_Layer | 暂不支持覆盖 |
| 直线合并 | 暂不支持覆盖 | 暂不支持覆盖 |

未确认的选项在面板禁用；如果旧设置已有不支持的覆盖，应用被阻止并提示恢复驱动默认。生成器也会拒绝这些值。中望 lines_overwrite 不作为原版“直线合并”的映射。没有声称 TrueType 选项能够处理所有 SHX 文字。

有显式参数时，两套宿主引擎强制生成本次独立 PC3/PC5/PMP，即使原设备已有匹配纸张也不会忽略参数。默认选项不改源驱动字段。仍拒绝替换源配置已关联的 PMP；不修改原配置文件、搜索路径或全局系统变量。

AutoCAD 写入 Hardcopy_Resolution 与物理/有效分辨率、Raster_Limit、Monochrome_Raster_Limit 和自定义光栅标志、All_As_Geometry、Include_Layer；保留未知 Resolution 枚举。中望写入 resolution_x/y 和 truetype_as_text。文件字段映射通过不等于驱动实际出图效果通过。

## 性能

此前已完成任务历史创建消除重复全表验证，以及保存时避免序列化往返。1000 页创建从约 7430 毫秒降至约 23 毫秒，更新保存约 240 毫秒。基准见 audit-evidence/phase27-history-before.json、phase27-history-after.json 和复现脚本。

## 验证与产物

- 核心测试 488 项通过；离线 UI 291 项通过。
- .NET Framework 4.8 独立核心检查通过。
- 两套新包的独立界面、书签、PDF、历史、图层检查通过；未加载 CAD 插件。
- 五份官方中望 PC5 和 AutoCAD General Documentation PC3 的参数文件检查通过，所有源文件 SHA-256 和修改时间不变。
- 最小窗口截图：audit-evidence/phase27-pdf-parameters.png。
- Release 包：`artifacts/FastBatchPlot-20260920-190542-978`。构建起点、日志及全部项目 DLL 的绝对路径/时间/大小/摘要分别见 audit-evidence/phase27-parameters-build-start.txt、phase27-parameters-build.log、phase27-parameters-artifacts.json。
- 测试记录：audit-evidence/test-results/phase27-parameters.trx、phase27-parameters-ui.log、phase27-parameters-framework.log、phase27-parameters-package.log；原配置验证见 phase27-parameters-final-native/sources.json。

尚未进行 CAD 连接、安装、加载或真实打印。需要另行明确授权后，验证驱动接受临时配置、真实输出 DPI、文字、图层与预览一致性。PDF 功能整体对齐仍有待办，不能称为已经完整替代原版。图章功能继续排除。
