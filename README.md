# FastBatchPlot

中望 CAD 2026 批量 PDF 打印插件，对标 `M-批打印软件(中望版)V20260805B`。当前交付目标只有中望版（AutoCAD 源码保留，默认不打包）；出图章、注册章、签名库及授权功能已退出目标范围。

**最新：[阶段 46：中望版全面检查与升级](docs/PHASE46_ZWCAD_UPGRADE.md)**：修复了来源核验误判导致整批失败、无法覆盖同名成果、`.tk` 块名与命名规则错位（图框型识别不到图框）、A 系列 2 倍关系误识别、加长图幅缺失、改写 `M_PDF.pmp` 及遗留临时 PC5、桌面日志、批处理乱码等问题；645 项核心测试、389 项离线 UI 检查通过，原生验收清单见该文档。使用说明见 [用户使用说明](docs/USER_MANUAL.md)。

当前范围及后续验收以 [PDF 专项工作范围](docs/PDF_SCOPE.md) 为准。

- [阶段 46：中望版全面检查与升级](docs/PHASE46_ZWCAD_UPGRADE.md)
- [阶段 45：中望原生输出攻克与来源指纹](docs/PHASE45_NATIVE_FIXES.md)
- [阶段 44：重复框与内含框过滤开关](docs/PHASE44_DETECTION_FILTERS.md)
- [阶段 43：中望原生测试发现与待验修复](docs/PHASE43_NATIVE_FINDINGS.md)
- [阶段 42：页后核验与委派记录](docs/PHASE42_PAGE_CONSISTENCY.md)
- [阶段 41：打印资源核验](docs/PHASE41_PLOT_RESOURCES.md)
- [阶段 40：外参来源核验](docs/PHASE40_XREF_REVISION.md)
- [阶段 39：纸张尺寸列](docs/PHASE39_PAPER_COLUMNS.md)
- [阶段 38：单张 PDF](docs/PHASE38_SINGLE_PDF.md)
- [阶段 37：识别报告](docs/PHASE37_DETECTION_REPORT.md)
- [阶段 36：识别比例](docs/PHASE36_DETECTION_SCALE.md)
- [阶段 35：外边线控制](docs/PHASE35_OUTER_BORDER.md)
- [阶段 34：AutoCAD PMP 继承](docs/PHASE34_AUTOCAD_PMP_INHERITANCE.md)
- [阶段 33：中望 PMP 继承](docs/PHASE33_ZWCAD_PMP_INHERITANCE.md)
- [阶段 32：AutoCAD 直线合并](docs/PHASE32_AUTOCAD_LINE_MERGE.md)
- [阶段 31：中望 PDF 参数](docs/PHASE31_ZWCAD_PDF_PARAMETERS.md)
- [阶段 30：来源命名与书签快照](docs/PHASE30_SOURCE_NAMING.md)
- [阶段 29：自定义纸张编辑](docs/PHASE29_CUSTOM_PAPER_EDITOR.md)
- [阶段 28：打印顺序与性能](docs/PHASE28_PRINT_ORDER.md)
- [阶段 27：PDF 参数与历史性能](docs/PHASE27_PDF_PARAMETERS.md)
- [阶段 26：中望 PDF 预设与 PMP 校准保护](docs/PHASE26_ZWCAD_PDF_PRESETS.md)
- [阶段 25：PDF 书签模板与历史一致性](docs/PHASE25_PDF_BOOKMARKS.md)
- [阶段 24：AutoCAD PDF 独立纸张配置](docs/PHASE24_AUTOCAD_MEDIA.md)
- [阶段 23：合并 PDF 保留图层显示状态](docs/PHASE23_PDF_LAYERS.md)
- [阶段 22：中望 PDF 独立纸张配置](docs/PHASE22_CUSTOM_PDF_MEDIA.md)
- [阶段 21：PDF 任务历史与失败页重试](docs/PHASE21_PDF_TASK_RECOVERY.md)
- [阶段 20：PDF 专项与可靠性修复](docs/PHASE20_PDF_FOCUS.md)
- [全面审查与功能矩阵](docs/AUDIT_2026-09-20.md)
- [第一阶段整改、验证与限制](docs/PHASE1_REMEDIATION.md)
- [第二阶段：选图、默认设置与构建包](docs/PHASE2_SELECTION_AND_RELEASE.md)
- [模型空间 DWG 拆分及验证边界](docs/PHASE8_DWG_SPLIT.md)
- [四边留白、比例预览与待验证项](docs/PHASE9_PAGE_MARGINS.md)
- [标题栏写回、预览与撤销](docs/PHASE10_TITLE_WRITE.md)
- [逐页取消与完整批次合并](docs/PHASE11_TASK_CANCELLATION.md)
- [模板命名与目录配置联动](docs/PHASE12_TEMPLATE_OUTPUT_SETTINGS.md)
- [模板打印范围](docs/PHASE13_TEMPLATE_CROP.md)
- [模板图框搜索与范围筛选](docs/PHASE14_TEMPLATE_DETECTION.md)
- [阶段 15：原生打印预览入口](docs/PHASE15_PREVIEW.md)
- [阶段 16：签名/印章图层打印开关](docs/PHASE16_SIGNATURE_STAMP_LAYERS.md)
- [阶段 17：透明 PNG 印章库与模板区域](docs/PHASE17_STAMP_LIBRARY.md)
- [阶段 18：印章授权与旧框架图片依赖修复](docs/PHASE18_STAMP_AUTHORIZATION.md)
- [阶段 19：印章尺寸、类别与独立有效期](docs/PHASE19_STAMP_DETAILS.md)
- [可搬移构建与安装说明](docs/PORTABLE_DEPLOYMENT.md)

核心计算可独立测试。解决方案仍同时编译 AutoCAD 2018 与 ZWCAD 2026 适配层（SDK 路径可配置），但发布包默认只含中望版：`deploy/Build-Release.ps1` 生成 `artifacts/FastBatchPlot-日期时间`，需要 AutoCAD 版时加 `-IncludeAutoCAD`。源码 `deploy/FastBatchPlot.bundle/Contents` 为历史产物，勿直接安装或分发。

```powershell
dotnet build FastBatchPlot.sln -c Release
dotnet test src/FastBatchPlot.Tests/FastBatchPlot.Tests.csproj -c Release
dotnet run --project src/FastBatchPlot.UiChecks/FastBatchPlot.UiChecks.csproj -c Release
dotnet run --project src/FastBatchPlot.FrameworkChecks/FastBatchPlot.FrameworkChecks.csproj -c Release
```

后两条运行独立假数据界面和 .NET Framework 核心检查，不连接 CAD。CAD 验证必须另有用户明确授权。

PDF 参数窗口、配置保存及历史重试已接通，能力按 AutoCAD / 中望区分。488 项核心、291 项离线 UI 检查通过；未做 CAD 原生打印验证。见 [阶段 27：PDF 参数与历史性能](docs/PHASE27_PDF_PARAMETERS.md)。

阶段 28 已修复超长图号自然排序并优化大批量图框排序；493 项核心、291 项离线 UI 通过。最新本轮包为 artifacts/FastBatchPlot-20260920-191618-079，已核验新产物并完成独立启动检查，未做 CAD 验证。详见 [打印顺序与性能](docs/PHASE28_PRINT_ORDER.md)。

阶段 29 补齐列表的自定义纸张尺寸与批量等比适配，可预览留白后的实际尺寸和比例。502 项核心、305 项离线 UI 通过；新包 artifacts/FastBatchPlot-20260920-192349-631 已完成产物核验及两宿主包的独立启动检查。未做 CAD 原生验收。见 [自定义纸张编辑](docs/PHASE29_CUSTOM_PAPER_EDITOR.md)。

阶段 30 修复活动 DWG 切换导致列表命名被污染：来源文件在采集时冻结，并进入任务历史；命名和书签支持文件名、布局。503 项核心、310 项离线 UI 通过。新包 artifacts/FastBatchPlot-20260920-193049-124 已构建核验，未做 CAD 原生验证。见 [来源命名与书签快照](docs/PHASE30_SOURCE_NAMING.md)。

阶段 31 补齐中望 PDF 独立光栅分辨率、图层和直线合并，并校验光栅不得高于矢量。字段语义经本机配置解析器、编辑器和原生配置静态核对；不修改用户驱动。验证与最新产物见 [中望 PDF 参数](docs/PHASE31_ZWCAD_PDF_PARAMETERS.md)。未连接或操作 CAD，真实出图效果仍待验。

阶段 32 已静态核实并开放 AutoCAD PDF 直线合并，开启/关闭/跟随驱动三态保留；两宿主均支持五项 PDF 参数。验证与产物见 [AutoCAD 直线合并](docs/PHASE32_AUTOCAD_LINE_MERGE.md)。未进行 CAD 原生验收。

阶段 33 已支持中望本地 PMP 私有副本继承，保留原纸张、校准及其他设置；AutoCAD PMP 继承仍待补齐。详见 [中望 PMP 继承](docs/PHASE33_ZWCAD_PMP_INHERITANCE.md)。未连接或操作 CAD。

阶段 34 已接入 AutoCAD 本地 PDF PMP 继承及显式参数覆盖同步，保留原纸张和校准；驱动接受情况仍待 CAD 原生验收。详见 [AutoCAD PMP 继承](docs/PHASE34_AUTOCAD_PMP_INHERITANCE.md)。

阶段 35 接入无留白外边线控制：“四边…”窗口可关闭边线并调整边缘裁切宽度，保持纸张、比例及内部内容位置。实现边界和验证见 [外边线控制](docs/PHASE35_OUTER_BORDER.md)；真实驱动效果仍待 CAD 验收。

阶段 36 补齐搜索/框选前的识别比例：0 自动，显式比例按范围生成精确纸张并支持非标尺寸；模板明确比例优先。见 [识别比例](docs/PHASE36_DETECTION_SCALE.md)。未做 CAD 原生验收。

阶段 37 新增搜索/框选识别报告，展示采集警告原文与图框排除原因；567 项核心、330 项离线 UI 检查通过，新包 `artifacts/FastBatchPlot-20260920-204406-681` 已核验并完成独立进程检查。见 [识别报告](docs/PHASE37_DETECTION_REPORT.md)。未做 CAD 原生验收。

阶段 38 补齐单张 PDF 快速输出：选中行、两点范围或图形集合，默认临时输出并打开，支持另存；复用参数、历史和来源校验，保留批量列表。569 项核心、341 项离线 UI 检查通过。新包 `artifacts/FastBatchPlot-20260920-205317-993` 已核验，见 [单张 PDF](docs/PHASE38_SINGLE_PDF.md)。未做 CAD 原生验收。

阶段 39 新增基础尺寸编辑、PDF 实际尺寸/比例列和表头全批设置，统一使用打印规划器即时校验；580 项核心、352 项离线 UI 检查通过。新包 `artifacts/FastBatchPlot-20260920-210047-739` 已核验，见 [纸张尺寸列](docs/PHASE39_PAPER_COLUMNS.md)。未做 CAD 原生验收。

阶段 40 补强外参来源核验，纳入已加载外参修改事件、嵌套外参与磁盘内容摘要；583 项核心、354 项离线 UI 检查通过。新包 `artifacts/FastBatchPlot-20260920-210750-032` 已核验，见 [外参来源核验](docs/PHASE40_XREF_REVISION.md)。字体/样式等其他依赖及原生验收仍待补齐。

阶段 41 增加 CTB/STB、PC3/PC5 和关联 PMP 内容核验及历史标记，阻止资源变化后的旧任务重试；588 项核心、359 项离线 UI 检查通过。新包 `artifacts/FastBatchPlot-20260920-211751-870` 已核验，见 [打印资源核验](docs/PHASE41_PLOT_RESOURCES.md)。字体等其他资源与 CAD 原生验收仍未完成。

阶段 42 增加 PDF 页后来源核验；空标记防护及边界测试由 Antigravity MCP 返回补丁，主智能体复核修正并独立验证。588 项核心、384 项离线 UI 检查通过，新包 `artifacts/FastBatchPlot-20260920-213046-884` 已核验。见 [页后核验与委派记录](docs/PHASE42_PAGE_CONSISTENCY.md)。

阶段 45 原生输出与来源一致性核验成功通过。中望 CAD 2026 原生 `PlotEngine` 成功出图并生成通过验收的真实 PDF，引入“事件监听 + 空间图元几何特征指纹”解决了已有图元修改不触发 CAD 事件的底层问题，并在反向测试中成功拒绝篡改；同时兼容漫游 PC5 预设。595 项核心测试、388 项离线 UI 自动化检查及独立 net48 进程发包核验全数通过，见 [阶段 45：中望原生输出攻克与来源指纹](docs/PHASE45_NATIVE_FIXES.md)。最新发布包为 `artifacts/FastBatchPlot-20260920-223911-272`。
