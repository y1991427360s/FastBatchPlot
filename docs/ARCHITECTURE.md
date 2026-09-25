> 历史文档：2026-09-20 审查确认本文含功能与兼容性过度声明。当前状态以 [审查报告](AUDIT_2026-09-20.md) 和 [第一阶段整改](PHASE1_REMEDIATION.md) 为准，本文保留作历史对照。

# FastBatchPlot 系统架构与设计说明

## 1. 系统工程分层结构

```
FastBatchPlot.sln
├── src/
│   ├── FastBatchPlot.Core/          # 核心业务与算法库 (netstandard2.0 / net8.0)
│   │   ├── Models/                  # 图框、纸张、打印配置、标题栏元数据模型
│   │   ├── Paper/                   # 智能图幅推断与工程比例计算器
│   │   ├── Detection/               # 多段线识别过滤与图块属性提取器
│   │   ├── Naming/                  # 动态占位符文件命名模板引擎
│   │   ├── Pdf/                     # PDF 合并、书签构建与页面旋转服务
│   │   └── Export/                  # Excel CSV 目录与图幅统计导出
│   │
│   ├── FastBatchPlot.CadBridge/     # CAD 宿主抽象层 (解耦不同平台与版本)
│   │   ├── ICadHost.cs              # 统一的文档/编辑器/图框抓取接口
│   │   ├── ICadPlotter.cs           # 统一的原生 PlotEngine 打印管线接口
│   │   └── CadHostProvider.cs       # 全局运行时宿主定位服务
│   │
│   ├── FastBatchPlot.UI/            # 现代化图形用户界面 (net48 / net8.0-windows)
│   │   └── Views/BatchPlotForm.cs   # 高分屏自适应批打印主窗口与交互控制
│   │
│   ├── FastBatchPlot.ZWCAD/         # 中望CAD插件适配层 (对接 ZwSoft.ZwCAD.*)
│   │   ├── ZwCadAdapter.cs          # 中望CAD实体遍历与视口定位实现
│   │   ├── ZwCadPlotEngine.cs       # 中望CAD PlottingServices 原生打印管线
│   │   └── ZwCommands.cs            # BP / BATCHPLOT / BP_TRUE2INDEX 命令入口
│   │
│   ├── FastBatchPlot.AutoCAD/       # AutoCAD插件适配层 (对接 Autodesk.AutoCAD.*)
│   │   ├── AcadAdapter.cs           # AutoCAD 实体遍历与视口定位实现
│   │   ├── AcadPlotEngine.cs        # AutoCAD PlottingServices 原生打印管线
│   │   └── AcadCommands.cs          # BP / BATCHPLOT / BP_TRUE2INDEX 命令入口
│   │
│   ├── FastBatchPlot.Cli/           # 独立控制台工具 (兼容 Mpr2 并支持新功能)
│   │   └── Program.cs               # 命令行 PDF 合并与旋转实用工具
│   │
│   └── FastBatchPlot.Tests/         # 自动化单元测试套件 (xUnit + .NET 8)
│       ├── PaperSizeDetectorTests.cs
│       ├── FrameSorterTests.cs
│       ├── DrawingNameFormatterTests.cs
│       ├── PolylineDetectorTests.cs
│       ├── PdfMergerTests.cs
│       └── CatalogExporterTests.cs
│
├── config/                          # 图幅与出图配置
├── deploy/                          # 一键安装脚本与 LISP / Bundle 自动加载器
└── docs/                            # 技术文档与用户手册
```

## 2. 核心算法说明

### 2.1 智能图纸幅面与出图比例推断 (`PaperSizeDetector`)
1. **输入参数**：CAD 图形在模型空间或布局空间的包围盒宽度 $W$ 与高度 $H$。
2. **长短边归一化**：$L = \max(W, H)$，$S = \min(W, H)$，计算实际长宽比 $R = L / S$。
3. **标准图幅匹配**：
   - 内置 ISO 216 基础规格（A0: 1189x841, A1: 841x594, A2: 594x420, A3: 420x297, A4: 297x210）及建筑结构加长规格（A0+1/4, A1+1/2 等）。
   - 比较 $R$ 与各标准图幅的长宽比 $R_{std}$，过滤偏差大于 8% 的非匹配幅面。
4. **标准比例拟合 (`ScaleCalculator`)**：
   - 依据短边计算基准缩放比 $S / S_{std}$。
   - 在工程标准比例数组 $[1, 2, 5, 10, 20, 25, 30, 50, 75, 100, 150, 200, 250, 300, 500, 1000]$ 中寻找最贴近的离散值。
   - 当多个标准图幅几何误差均极小时（例如 A4 1:100 vs A2 1:50），算法通过工程常用权重优先收敛到最贴近 1:100 的方案。

### 2.2 嵌套边框过滤与内外框智能去重 (`PolylineFrameDetector`)
在工程图纸中，图框通常由两条封闭矩形线组成：外侧图廓裁切线与内侧装订边线（例如左边距内缩 25mm，其余三边内缩 5~10mm）。
- 算法按图框面积降序排序，先收录大图框。
- 当后续候选框被已收录图框包含，且两者面积比在 $80\% \sim 99\%$ 时，自动判定为嵌套内框并予以剔除，确保每张图纸精准只生成一个打印任务。

### 2.3 空间网格分行聚类排序算法 (`FrameSorter`)
建筑图纸在模型空间排列时，同一排图纸的基准线往往存在微小偏差（或不同图幅高度混排）。
- 算法引入动态行聚类容差（$\text{RowTolerance} = \max(10, 0.4 \times \bar{H})$）。
- 自顶向下聚类出逻辑行（Row Bands），每行内部依据 $X$ 坐标从左向右严格升序排序，实现真正符合人类直觉的排版出图顺序。
