# FastBatchPlot

面向 ZWCAD 2026 的批量 PDF 打印插件，支持图框识别、标题栏提取、模板字段拾取、输出命名与 PDF 合并。AutoCAD 源码保留，但不是当前交付目标；签章功能不在当前范围。

## 当前状态（2026-09-26）

- “图框模板”改为仿原版的“图框信息库管理”：一行一个图框，只保留录入新图框、导出设置、导入设置、确定、取消；录入窗口按原版分为基本信息与信息框录入（不含签章）。见 [阶段 48](docs/PHASE48_FRAME_LIBRARY.md)。
- Release 构建 0 警告/0 错误，671 项核心测试、422 项离线 UI 检查通过。新的“读取图框线框范围”中望接口尚未在 CAD 内实测。

## 阶段 47 验证状态（2026-09-25）

- 解决漏读独立“图名图号”属性块的问题，保留原 `.tk` 字段区域。
- ZWCAD 实测 60 张，4.944 秒：57 张普通图纸图号/图名与原始属性一致；3 张目录按各自配置核对通过，无字段警告。
- 打印列表为空时，模板库选择模板后可直接拾取图号/图名区域。
- 当时 Release 构建 0 警告/0 错误，647 项核心测试、391 项离线 UI 检查通过。
- 本次是字段提取和拾取验收，不代表任意图纸、旋转/镜像或全部 PDF 驱动场景均已验收。

## 使用与开发

- [用户使用说明](docs/USER_MANUAL.md)
- [部署与更新](docs/PORTABLE_DEPLOYMENT.md)
- [开发指南](docs/DEVELOPER_GUIDE.md)
- [当前架构](docs/ARCHITECTURE.md)
- [当前范围与待验项目](docs/PDF_SCOPE.md)
- [字段提取实机验证记录](docs/PHASE47_TITLE_EXTRACTION.md)
- [图框信息库改版](docs/PHASE48_FRAME_LIBRARY.md)
- [阶段文档索引](docs/INDEX.md)

```powershell
dotnet build FastBatchPlot.sln -c Release
dotnet test src/FastBatchPlot.Tests/FastBatchPlot.Tests.csproj -c Release
dotnet run --project src/FastBatchPlot.UiChecks/FastBatchPlot.UiChecks.csproj -c Release
./deploy/Build-Release.ps1 -ZWCADSdkPath 'D:\ZWCAD2026'
```

构建需要 Windows、.NET SDK 8 和相应 CAD SDK。发布脚本默认只打包 ZWCAD。安装前关闭 CAD，并更新实际自动加载目录；已加载的程序集不能靠另一次 NETLOAD 替换。

源码仓库不包含 CAD SDK、测试 DWG、第三方原软件、旧二进制部署目录及临时诊断产物。测试夹具和许可证随源码保留。
