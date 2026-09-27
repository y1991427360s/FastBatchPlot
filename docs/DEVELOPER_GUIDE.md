# 开发指南

需要 Windows、.NET SDK 8、ZWCAD 2026 SDK（ZwManaged.dll / ZwDatabaseMgd.dll）。设置 `ZWCAD2026_SDK` 或 MSBuild 属性 `ZWCADSdkPath`；本机默认 D:\ZWCAD2026。全解决方案还会编译 AutoCAD 项目，需要对应 SDK；仅开发中望可构建 ZWCAD 项目。

```powershell
dotnet build src/FastBatchPlot.ZWCAD/FastBatchPlot.ZWCAD.csproj -c Release -p:ZWCADSdkPath='D:\ZWCAD2026'
dotnet test src/FastBatchPlot.Tests/FastBatchPlot.Tests.csproj -c Release
dotnet run --project src/FastBatchPlot.UiChecks/FastBatchPlot.UiChecks.csproj -c Release
dotnet run --project src/FastBatchPlot.FrameworkChecks/FastBatchPlot.FrameworkChecks.csproj -c Release
./deploy/Build-Release.ps1 -ZWCADSdkPath 'D:\ZWCAD2026'
```

测试和假宿主 UI 不连接 CAD。新宿主能力需要在 CadBridge 定义接口，再实现原生适配；编译通过不等于原生兼容。

字段修复主要入口：Core/Templates/TitleTemplates.cs、ZWCAD/ZwCadAdapter.Templates.cs、UI/Views/TitleTemplateForm.cs（图框信息库管理）与 FrameEntryForm.cs（录入新图框）；列、纸张、命名规则和导入合并规则在 Core/Templates/FrameLibrary.cs。新增规则应覆盖歧义、邻字段和原配置保持不变的回归。先查原对象与变换，再改算法。

原生验证先核对实际加载 DLL 路径及 SHA256。新发布包的 NETLOAD 不能覆盖自动加载的旧 DLL，详见 [部署说明](PORTABLE_DEPLOYMENT.md)。结果须记录实际字段值、逐项比对、失败和限制，不能只记处理成功数。
