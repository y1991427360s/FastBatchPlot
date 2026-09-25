# 部署与更新

当前目标：Windows 64 位、ZWCAD 2026、.NET Framework 4.8。仅默认打包中望；AutoCAD 源码保留。

## 构建

```powershell
./deploy/Build-Release.ps1 -ZWCADSdkPath 'D:\ZWCAD2026'
```

脚本从源码重建，生成 `artifacts/FastBatchPlot-日期时间`，包含安装脚本、运行依赖和许可。看到 `PACKAGE_CREATED=` 才表示打包完成。打包本身不证明 CAD 验收；不要安装源码 deploy/FastBatchPlot.bundle/Contents 中的历史产物。

## 安装或升级

1. 妥善处理打开的图纸并关闭 CAD；测试图纸按测试约定关闭不保存。
2. 备份 `%LOCALAPPDATA%\FastBatchPlot\ZWCAD2026`，运行发布包中的“一键安装.bat”。
3. 重启 CAD，运行 BP。安装位置为 `%LOCALAPPDATA%\FastBatchPlot\ZWCAD2026\Contents`。
4. 排查版本时核对实际加载程序集路径及哈希。CAD 可能先自动加载安装目录的旧版；此后 NETLOAD 新包同名 DLL 不会替换已加载版本。

卸载使用发布包“一键卸载.bat”；用户设置保留。搬到其他机器需在目标机安装，不能复制本机注册表路径。

## 用户数据

模板库：`%APPDATA%\FastBatchPlot\title-templates.json`。变更前备份，原 TK 作为权威输入，不以测试图其他位置文字改写区域。字段试选只有“保存并关闭”后才持久化。

错误日志：`%LOCALAPPDATA%\FastBatchPlot\logs\plot-errors.log`，主窗“更多”可打开。历史失败页可在满足当前来源检查的同一会话中重试；跨 CAD 重启自动续打未实现。

## 验证过的版本

2026-09-25 字段验收包：`FastBatchPlot-20260925-152931-280`。正式安装 DLL 与发布包哈希相同，60 张字段提取和空列表模板拾取验证见 [阶段 47](PHASE47_TITLE_EXTRACTION.md)。这不替代其余 PDF 原生验收项目，见 [工作范围](PDF_SCOPE.md)。

PC5/PMP 采用私有副本，不修改用户或其他插件的驱动配置。真实设备接受、临时文件清理及参数效果应按具体场景核验。
