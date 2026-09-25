# 可搬移的 Release 发布与安装

当前发布目标仅为 **64 位 AutoCAD 2018（R22.0）和 ZWCAD 2026**。构建通过不等于已完成原生加载、真实打印或完整替代原软件的验收。其他 CAD 年份没有兼容性承诺。

## 构建发布包

需要 Windows、.NET SDK、.NET Framework 4.8 开发引用（项目通过 NuGet 获取）和合法安装的对应 CAD SDK。无需启动 CAD。

```powershell
.\deploy\Build-Release.ps1 -AutoCADSdkPath 'C:\SDK\AutoCAD2018' -ZWCADSdkPath 'C:\SDK\ZWCAD2026'
```

SDK 路径也可以通过 `AUTOCAD2018_SDK`、`ZWCAD2026_SDK` 环境变量，或者 `dotnet build -p:AutoCADSdkPath=... -p:ZWCADSdkPath=...` 传入。未指定时按项目中标准安装位置及当前机器兼容路径查找；发布包不包含 CAD SDK DLL。

打包脚本从源码重新构建 Release，拒绝项目程序集时间早于本次构建起点，复制运行依赖和卫星资源。输出为仓库 `artifacts\FastBatchPlot-时间戳`，可通过 `-OutputRoot` 改到其他盘。脚本不会安装、写注册表、启动或连接 CAD。必须看到 `PACKAGE_CREATED=` 才代表打包脚本完成；这不代表原生验收完成。

发布包结构：

- `AutoCAD2018\FastBatchPlot.bundle`：AutoCAD 专用程序集和运行依赖。
- `ZWCAD2026\Contents`：中望专用程序集和运行依赖。
- `Install-AutoCAD.ps1`、`Install-ZWCAD.ps1`、`Install-Common.ps1`：用户级安装与卸载。
- `manifest.json`：构建时间、目标版本、相对路径、文件大小及 SHA-256；清单不包含自身。

完整复制发布目录后即可换位置安装，不依赖项目源路径、Debug 目录或原构建机器。SHA-256 用于发现不完整复制或修改，不是数字签名或发布者身份认证。源码 `deploy\FastBatchPlot.bundle\Contents` 可能有历史构建残留，打包脚本完全不使用它们；不得直接分发源码 deploy 目录。

## 安装前预览与正式安装

在完整发布目录中执行，可先使用 `-WhatIf` 预览。安装器会先验证清单及目标文件，不允许缺少依赖、哈希变化或多出未登记文件。

```powershell
.\Install-AutoCAD.ps1 -TargetVersion 2018 -WhatIf
.\Install-ZWCAD.ps1 -TargetVersion 2026 -WhatIf
```

正式安装前自行保存图纸并关闭对应 CAD，去掉 `-WhatIf`。安装器不会替你退出或重启 CAD。

AutoCAD 安装到 `%APPDATA%\Autodesk\ApplicationPlugins\FastBatchPlot.bundle`，Bundle 限定 R22.0。中望安装到 `%LOCALAPPDATA%\FastBatchPlot\ZWCAD2026`，仅注册 `HKCU\Software\ZWSOFT\ZWCAD\2026\<语言>\Applications\FastBatchPlot`。不会写 HKLM、不会修改其他年份。中望必须已完成当前用户首次启动初始化，存在该年份语言配置。

目标目录或同名注册项已存在时会拒绝覆盖。新版安装器管理的目录可先卸载再装；历史安装缺少标记时应自行备份并核对旧目录/旧 LOADER，不能让安装器猜测并删除。用户打印设置不随卸载删除。

## 卸载

保留原发布包并在其中执行：

```powershell
.\Install-AutoCAD.ps1 -Uninstall -WhatIf
.\Install-ZWCAD.ps1 -Uninstall -WhatIf
```

核对后去掉 `-WhatIf`。卸载只移除有本安装器标记且位于固定用户安装根目录下的文件；中望仅移除 LOADER 正好指向该安装目录的注册项。不会删除其他版本 CAD、用户配置或旧版不明目录。

## 手动加载与验收边界

明确允许原生 CAD 验证后，可使用 CAD 的 `NETLOAD` 选择发布包中对应的 `FastBatchPlot.AutoCAD.dll` 或 `FastBatchPlot.ZWCAD.dll`。`LoadFastBatchPlot.lsp` 只定义辅助命令，不在载入 LISP 时执行 NETLOAD；不再内置开发机路径。CAD 的信任路径或签名策略仍由用户及组织策略决定，安装器不会降低这些策略。

尚需分别在 AutoCAD 2018 与 ZWCAD 2026 完成插件加载、BP 窗口、设备枚举、多图打印、失败/取消恢复及卸载验证。未进行这些验证之前只能称为“Release 构建包”，不能称为“已经安装验证”或“可完整替代原版”。



## PDF 任务历史

主窗口“PDF任务历史”可查看最近 200 条任务，新任务记录位于用户设置同级 `tasks` 目录。失败页重试复用原设置并保留校验通过的成功页；不会覆盖未知文件。重新打开 CAD、修改图纸或更换外部打印依赖后，请新建完整批次。跨 CAD 重启自动续打尚未实现。

本版未完成原生 CAD 验收，事件修订检查可能因宿主内部布局/视图操作而保守拒绝继续复用。不要把离线构建/测试成功等同于正式图纸验收。

## 中望 PDF 自动纸张

当驱动已有纸张无法完整匹配时，本版会尝试为已识别的中望原生 PDF 驱动生成独立临时 PC5/PMP，包含实际纸张尺寸及留白；不改现有打印配置和 CAD 搜索路径。驱动实际回读的配置路径、纸张尺寸、打印范围、比例或样式不符时停止该页，不换用相近纸张。清理失败会保留已生成 PDF，并提示临时配置的残留目录。

此路径已完成文件级与离线检查，但任意目录的独立 PC5/PMP 能否被真实驱动接受仍待 CAD 验收。AutoCAD 原生 PDF 的独立 PC3/PMP 自动纸张路径也已接入并通过文件级检查，但驱动接受情况同样待 CAD 验收，详见 PHASE24_AUTOCAD_MEDIA.md。PDF 参数的面板和文件映射已在阶段 27 接入，支持范围见下文；实际驱动输出效果仍待验证。

PDF 合并设置旁的“书签…”可编辑模板并预览当前勾选图纸。模板随默认设置保存，历史任务重试/重合并使用原任务模板。支持图号、图名、项目、序号、图幅、比例、标题栏日期和版次，阶段 30 新增来源 DWG 文件名和布局占位符，旧记录缺来源时使用 Drawing。

中望自动纸张已识别 DWG to PDF 与 ZWCAD PDF 的 General Documentation、High Quality Print、Smallest File、Web and Mobile 五种本机官方预设，并保持其原驱动选项。阶段 33 已支持中望本地 PMP 继承，在私有副本中保留原校准及纸张并追加本次纸张；缺失、歧义及链式引用仍停止生成。纸张驱动原生验收仍待完成。

## 阶段 27 参数构建包

最新本轮包为 artifacts/FastBatchPlot-20260920-190542-978。参数面板和两宿主参数文件生成已接入并完成独立进程检查，所有项目 DLL 修改时间晚于构建起点。未安装到 CAD，未完成原生验证。支持矩阵、测试与摘要见 PHASE27_PDF_PARAMETERS.md。

阶段 28 已修复超长图号自然排序并优化大批量图框排序；493 项核心、291 项离线 UI 通过。最新本轮包为 artifacts/FastBatchPlot-20260920-191618-079，已核验新产物并完成独立启动检查，未做 CAD 验证。详见 [打印顺序与性能](PHASE28_PRINT_ORDER.md)。

阶段 29 补齐列表的自定义纸张尺寸与批量等比适配，可预览留白后的实际尺寸和比例。502 项核心、305 项离线 UI 通过；新包 artifacts/FastBatchPlot-20260920-192349-631 已完成产物核验及两宿主包的独立启动检查。未做 CAD 原生验收。见 [自定义纸张编辑](PHASE29_CUSTOM_PAPER_EDITOR.md)。

阶段 30 修复活动 DWG 切换导致列表命名被污染：来源文件在采集时冻结，并进入任务历史；命名和书签支持文件名、布局。503 项核心、310 项离线 UI 通过。新包 artifacts/FastBatchPlot-20260920-193049-124 已构建核验，未做 CAD 原生验证。见 [来源命名与书签快照](PHASE30_SOURCE_NAMING.md)。

阶段 31 补齐中望 PDF 独立光栅分辨率、图层和直线合并，并校验光栅不得高于矢量。字段语义经本机配置解析器、编辑器和原生配置静态核对；不修改用户驱动。验证与最新产物见 [中望 PDF 参数](PHASE31_ZWCAD_PDF_PARAMETERS.md)。未连接或操作 CAD，真实出图效果仍待验。

阶段 32 已静态核实并开放 AutoCAD PDF 直线合并，开启/关闭/跟随驱动三态保留；两宿主均支持五项 PDF 参数。验证与产物见 [AutoCAD 直线合并](PHASE32_AUTOCAD_LINE_MERGE.md)。未进行 CAD 原生验收。

阶段 33 已支持中望本地 PMP 私有副本继承，保留原纸张、校准及其他设置；AutoCAD PMP 继承仍待补齐。详见 [中望 PMP 继承](PHASE33_ZWCAD_PMP_INHERITANCE.md)。未连接或操作 CAD。

阶段 34 已接入 AutoCAD 本地 PDF PMP 继承及显式参数覆盖同步，保留原纸张和校准；驱动接受情况仍待 CAD 原生验收。详见 [AutoCAD PMP 继承](PHASE34_AUTOCAD_PMP_INHERITANCE.md)。

阶段 35 接入无留白外边线控制：“四边…”窗口可关闭边线并调整边缘裁切宽度，保持纸张、比例及内部内容位置。实现边界和验证见 [外边线控制](PHASE35_OUTER_BORDER.md)；真实驱动效果仍待 CAD 验收。

阶段 36 补齐搜索/框选前的识别比例：0 自动，显式比例按范围生成精确纸张并支持非标尺寸；模板明确比例优先。见 [识别比例](PHASE36_DETECTION_SCALE.md)。未做 CAD 原生验收。

阶段 37 新增搜索/框选识别报告，展示采集警告原文与图框排除原因；567 项核心、330 项离线 UI 检查通过，新包 `artifacts/FastBatchPlot-20260920-204406-681` 已核验并完成独立进程检查。见 [识别报告](PHASE37_DETECTION_REPORT.md)。未做 CAD 原生验收。

阶段 38 补齐单张 PDF 快速输出：选中行、两点范围或图形集合，默认临时输出并打开，支持另存；复用参数、历史和来源校验，保留批量列表。569 项核心、341 项离线 UI 检查通过。新包 `artifacts/FastBatchPlot-20260920-205317-993` 已核验，见 [单张 PDF](PHASE38_SINGLE_PDF.md)。未做 CAD 原生验收。

阶段 39 新增基础尺寸编辑、PDF 实际尺寸/比例列和表头全批设置，统一使用打印规划器即时校验；580 项核心、352 项离线 UI 检查通过。新包 `artifacts/FastBatchPlot-20260920-210047-739` 已核验，见 [纸张尺寸列](PHASE39_PAPER_COLUMNS.md)。未做 CAD 原生验收。

阶段 40 补强外参来源核验，纳入已加载外参修改事件、嵌套外参与磁盘内容摘要；583 项核心、354 项离线 UI 检查通过。新包 `artifacts/FastBatchPlot-20260920-210750-032` 已核验，见 [外参来源核验](PHASE40_XREF_REVISION.md)。字体/样式等其他依赖及原生验收仍待补齐。

阶段 41 增加 CTB/STB、PC3/PC5 和关联 PMP 内容核验及历史标记，阻止资源变化后的旧任务重试；588 项核心、359 项离线 UI 检查通过。新包 `artifacts/FastBatchPlot-20260920-211751-870` 已核验，见 [打印资源核验](PHASE41_PLOT_RESOURCES.md)。字体等其他资源与 CAD 原生验收仍未完成。

阶段 42 增加 PDF 页后来源核验；空标记防护及边界测试由 Antigravity MCP 返回补丁，主智能体复核修正并独立验证。588 项核心、384 项离线 UI 检查通过，新包 `artifacts/FastBatchPlot-20260920-213046-884` 已核验。见 [页后核验与委派记录](PHASE42_PAGE_CONSISTENCY.md)。未做 CAD 原生验收。
