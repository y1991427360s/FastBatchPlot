# 项目约定

- 当前目标为 ZWCAD 2026 批量 PDF；AutoCAD 源码保留，不扩大到签章或其他历史目标。
- 原始 `.tk` 是字段区域与命名规则的权威来源；不得根据图面其他文字擅自改坐标。
- 字段可能位于与图框并列的属性块。核对原始对象、块变换、锚点与文字范围后再修改采集。
- CAD 实测结论必须依据实际加载程序集和运行结果；NETLOAD 不会替换已自动加载的同名程序集。更新先关闭 CAD、备份安装目录，再重启并核对路径/哈希。
- 用户测试 DWG 不保存，模板试选不保存；应用配置修改先备份。
- 核心测试与离线 UI 通过不能代替原生 CAD 验收。操作 CAD 的授权按当前会话判断。
- 构建：`dotnet build FastBatchPlot.sln -c Release`；测试：`dotnet test src/FastBatchPlot.Tests/FastBatchPlot.Tests.csproj -c Release`。
- 离线界面：`dotnet run --project src/FastBatchPlot.UiChecks/FastBatchPlot.UiChecks.csproj -c Release`。
- 发布：`deploy/Build-Release.ps1`，默认仅 ZWCAD。不要分发源码 deploy 下的历史 DLL。
- 当前事实以 README、docs/PDF_SCOPE.md 和 docs/PHASE47_TITLE_EXTRACTION.md、docs/PHASE48_FRAME_LIBRARY.md 为准；PHASE1—46 为阶段历史。
