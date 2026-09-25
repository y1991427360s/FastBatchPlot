> 历史文档：2026-09-20 审查确认本文含功能与兼容性过度声明。当前状态以 [审查报告](AUDIT_2026-09-20.md) 和 [第一阶段整改](PHASE1_REMEDIATION.md) 为准，本文保留作历史对照。

# FastBatchPlot 开发者扩展指南

## 1. 编译与构建
系统基于 .NET SDK 8.0 构建，采用多目标框架编译体系：
```bash
cd "E:\366256\vibecoding\批打印-new"
dotnet build FastBatchPlot.sln
```

## 2. 运行单元测试
```bash
dotnet test src/FastBatchPlot.Tests/FastBatchPlot.Tests.csproj
```
测试集涵盖：
- 标准与加长图幅自动识别
- 空间分行聚类算法
- 自然图号排序
- 文件名占位符替换
- PDF 合并与书签大纲生成
- 目录 CSV 导出

## 3. 添加新的图纸幅面
在 `src/FastBatchPlot.Core/Models/PaperSize.cs` 的 `StandardSizes` 列表中增加新的幅面定义：
```csharp
new PaperSize("A1+1/3", 1121, 594, true),
```
重新编译即可在全系统中生效。

## 4. 适配新版本 CAD 或新 CAD 平台 (如浩辰CAD/BricsCAD)
只需在 `FastBatchPlot.CadBridge` 接口基础上新增适配项目，实现：
1. `ICadHost`：实现实体遍历与屏幕定位；
2. `ICadPlotter`：实现该平台下的原生打印引擎调用。
上层核心算法与用户界面（UI）100% 完全复用。
