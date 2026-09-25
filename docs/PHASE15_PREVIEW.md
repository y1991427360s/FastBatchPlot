# 阶段 15：原生打印预览入口（2026-09-20）

本阶段为两套宿主实现 `ICadPlotPreview`。预览使用与正式出图相同的图框范围、模板裁切范围、驱动介质、真实可打印区域、方向、比例、CTB 和原点计算，然后调用 AutoCAD 2018 / ZWCAD 2026 SDK 的 `PlotFactory.CreatePreviewEngine(0)`。

预览不创建 PDF、DWF 或其他输出文件，不提交实体打印机任务，也不允许从预览窗口直接转发打印。退出状态通过 `PreviewEndPlotInfo` 从 `EndPage` 读取；主窗口在调用期间隐藏，返回后恢复控件和焦点。预览只接受列表中一行图纸，配置先复制到任务快照，避免预览过程中改动列表。

本阶段完成了 SDK 元数据静态核对、两宿主 Release 编译，以及不加载 CAD 的独立 UI 回归。尚未在 AutoCAD 或 ZWCAD 原生进程中启动预览，原生显示、缩放、驱动兼容性仍需用户明确授权后验收。

验证记录见 `docs/audit-evidence/phase15-build.log`、`phase15-tests.log`、`phase15-ui.log` 和 `phase15-package-validation.log`。
