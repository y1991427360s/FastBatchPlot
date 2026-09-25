# Core / Tests / CLI 审查证据

审查日期：2026-09-20。审查项目：`E:\366256\vibecoding\批打印-new`。

本次只读检查产品源码，没有连接、启动或操作 CAD，没有修改产品源码或既有测试。证据目录新增可重跑脚本。本报告中的 P1 表示应在正式交付前修复；P2 表示确定的正确性问题或重要补全项。

## 验证方式

纯核心现有测试实际执行：

```powershell
dotnet test src/FastBatchPlot.Tests/FastBatchPlot.Tests.csproj --no-restore --verbosity minimal
```

结果：31 个通过、0 个失败、0 个跳过；仅验证 `net8.0`，不覆盖部署到 CAD 的 `netstandard2.0` / `net48` 运行组合。测试全部通过并不代表以下问题已被覆盖。

复现六个问题（PowerShell 7，需事先构建 Core；优先加载 Release，缺失时加载 Debug）：

```powershell
pwsh -NoProfile -File docs/audit-evidence/reproduce-core.ps1
```

脚本只加载纯 Core DLL，不创建 CAD 对象、不修改图纸。复现输出：

```text
ACI 10=(255, 255, 255); ACI 30=(255, 255, 255); RGB(255,127,0) -> ACI 36
模型单位定义图块以 1 倍插入：识别 0 个图框
自交蝴蝶多段线：识别 1 个图框
有效 A3：单独 A3=1; 添加 440x350 外矩形后=0
图名“预算$&版”：输出“预算{DwgName}版”
1:2.5 出图比例：文件名输出“1-3”
```

## 已复现缺陷

### C01 / P1：将块插入比例强制当作出图比例，漏掉正常图块

- 位置：`src/FastBatchPlot.Core/Detection/BlockFrameDetector.cs:41-49`；`src/FastBatchPlot.Core/Paper/PaperSizeDetector.cs:69-71`。
- 触发：图框块本身按模型尺寸绘制，包围盒 `84100×59400`，以 `ScaleX=ScaleY=1` 插入；它是正常 A1、1:100 图框。
- 实际：插入比例 1 被作为硬性出图比例；所有候选纸张误差过大，图框被直接过滤，识别数为 0。
- 影响：常见“按实际尺寸制块再原比例插入”的图纸无法自动识别。现有唯一图块测试仅覆盖“块定义以纸张毫米尺寸绘制、插入比例等于出图比例”的一种建模习惯。
- 建议：插入比例仅为提示。先验证该提示能否吻合，否则自动推断；支持用户明确比例、标题栏比例、块定义尺寸的可解释优先级，并记录拒绝原因。

### C02 / P1：去重发生在标准图幅校验之前，外围杂框能吞掉有效图框

- 位置：`src/FastBatchPlot.Core/Detection/PolylineFrameDetector.cs:47-59`、`:119-125`。
- 触发：有效 A3 矩形 `[10,20]—[430,317]`，外侧还有 `[0,0]—[440,350]` 矩形。A3 与外矩形面积比约 0.810，外矩形自身不满足标准图幅比例。
- 实际：A3 单独识别为 1 张；同时输入外矩形后，先按嵌套面积比把 A3 删除，再因外框不匹配图幅而删除外框，最终 0 张。
- 影响：辅助边界线、外围统计框等非图框对象可导致正常图纸漏打。
- 建议：先形成有效候选及置信度，再执行重复/内外框裁决；不要让不合格候选删除合格候选。

### C03 / P1：ACI 调色板生成公式错误，会改变真实颜色转换结果

- 位置：`src/FastBatchPlot.Core/Common/AciColorHelper.cs:44-54`、`:68-72`。
- 触发：ACI 10、30，或真彩色 `RGB(255,127,0)`。
- 实际：ACI 10 与 30 都生成白色；橙色被映射到 ACI 36。原因是将类似 HSV 明度的参数送进 HSL 转换，`lightness=1` 时所有色相都变成白色。
- 预期：ACI 10 是红色、ACI 30 是橙色。ACI 1—9 被独立硬编码，因此当前颜色测试全部通过，无法发现后面 240 色的问题。
- 影响：`AcadCommands.cs:85` / `ZwCommands.cs:86` 的真彩色转索引色命令会产生错误色号，相关 CTB 线宽/颜色也可能随之改变。这里只验证纯算法，没有执行修改图纸的命令。
- 建议：使用权威固定 ACI 查表或准确实现标准调色板；增加 10—249 全色域、灰阶、已知橙色样本测试。

### C04 / P2：自交多段线被误认为矩形图框

- 位置：`src/FastBatchPlot.Core/Common/GeometryUtils.cs:80-102`。
- 触发：顶点依次为 `(0,0),(420,0),(0,297),(420,297)`，闭合后是蝴蝶形自交多段线。
- 实际：对边及对角线恰好等长、首边平行坐标轴，因此通过矩形检查，最终生成 1 个 A3 图框。
- 建议：检查全部相邻边正交、顶点互异、非零面积和不自交；不能仅检查首边及若干长度。CAD 适配层还应另外验证圆弧段信息，当前候选模型只保留顶点。

### C05 / P2：属性内容被当作正则替换表达式，文件名失真

- 位置：`src/FastBatchPlot.Core/Naming/DrawingNameFormatter.cs:46-54`；同类写法存在于其余属性替换。
- 触发：图名为 `预算$&版`，模板 `{DwgName}`。
- 实际：输出 `预算{DwgName}版`；`$&` 被 Regex.Replace 解释为完整匹配文本。
- 影响：含 `$&`、`$'` 等有效字符串的名称可能变形，文件命名与原始图名不一致。
- 建议：属性替换统一用 MatchEvaluator 返回原始值；模板解析和文件名合法化分开处理。

### C06 / P2：非整数标准比例在命名与目录中被四舍五入

- 位置：`src/FastBatchPlot.Core/Naming/DrawingNameFormatter.cs:61`；`src/FastBatchPlot.Core/Export/CatalogExporter.cs:29`；`src/FastBatchPlot.Core/Models/PlotFrame.cs:57`。
- 触发：框的 `CalculatedScale=2.5`；该比例已列于 `ScaleCalculator.StandardScales`。
- 实际：命名 `{Scale}` 输出 `1-3`，CSV/模型显示也使用 `:0` 格式丢失小数。
- 影响：文件名及交付目录标出的比例与实际比例不符。
- 建议：统一使用比例值对象及保留必要小数的格式器，避免各处自行格式化。

## 其他源码确定的问题与边界

### C07 / P1：CLI 任务文件截断时可能静默少合并

- 位置：`src/FastBatchPlot.Cli/Program.cs:137-149`。
- 证据级别：源码逻辑确认，未另行生成 PDF 执行端到端复现。
- 声明 `count=2` 而文件只有第一组有效文件名/标题时，第二次循环读到 EOF，代码跳过空文件名，并将仅 1 个条目送入合并器；该 PDF 有效时仍返回成功。
- 建议：严格要求条目数和标题行完整，任务写入使用临时文件原子替换，读取前后检查任务版本/完整性；任何缺页都必须明确失败。

### C08 / P2：配置模型包含多个没有消费者的功能开关

- `PlotConfig.ExportFormat`、`PrintOuterBorderLine`、`AutoOrientation`、`BookmarkTemplate`、`PrintSignatures`、`PrintStamps` 的全仓库搜索只命中声明，未找到业务消费。
- 对应位置：`src/FastBatchPlot.Core/Models/PlotConfig.cs:24,36,41,56,61,66`。
- 不能把枚举或属性存在当成功能已完成。目录导出器注释宣称支持 HTML，但实际只提供 CSV；PDF 合并器注释宣称多级书签，实际逐文件生成一级书签。

### C09 / P2：按图块优先级排序实际只是字典排序

- 位置：`src/FastBatchPlot.Core/Detection/FrameSorter.cs:34-35`；对照 `src/FastBatchPlot.Core/Models/SortOrderRule.cs:24-26`。
- 声明行为是“目录优先、施工图、详图”；实现仅按 `SourceBlockName` 和图号字符串排序，没有优先级配置和语义映射。

## 架构与测试建议

1. 保留 Core 与 CAD 适配器分离的方向，但将 PDF I/O 从几何算法核心拆为独立服务；核心候选/结果采用明确失败原因、置信度、来源与单位，避免默认 A1/1:100 掩盖不确定性。
2. 建立图框识别的分阶段流程：抽取 → 几何校验 → 图幅候选 → 去重 → 排序 → 用户复核。不要把块插入比例、图纸单位和出图比例混成一个值。
3. `PaperSize.StandardSizes` 虽暴露为只读列表，但元素本身可变，且横向检测直接返回共享实例；应使用不可变模型或复制，防止编辑一个结果污染后续检测。
4. `RawPolylineCandidate.Bounds` 每次都重新遍历顶点，嵌套过滤双循环；排序分组反复遍历行/列统计，密集图框会重复计算。先缓存包围盒与分组统计，再按真实基准测试决定是否引入空间索引。
5. 测试目前 31 个，10 个集中于 ACI 基础色；没有 CLI 测试，没有针对上述失效路径、错误输入、PDF 文件占用/损坏/同名覆盖、取消、打印恢复的覆盖。
6. 优先加入以上六个真实缺陷的回归样例，再补带完整来源的标准/加长/非毫米单位/旋转/块定义习惯图框数据集。纯测试可立即执行；CAD 原生验收需用户另行明确授权。

本报告不作原版全部功能已识别的结论；复刻功能覆盖率须与原版安装包、配置、界面及说明逐项核对。
