# 阶段 41：打印样式与设备配置内容核验

## 问题与实现

此前恢复流程只检查设备和样式名称是否仍可用。同名 CTB/STB、PC3/PC5 或 PMP 内容改变时，旧页和重试页可能使用不同输出参数。

新增 ICadPlotResourceHost 能力，两宿主由 PlotConfigManager.Devices 获取所选配置的真实完整路径，要求唯一匹配。通过既有 PC3/PC5 解析器查找关联 PMP，复用已有路径、歧义和内容验证规则。打印样式读取当前 CAD Preferences.Files.PrinterStyleSheetPath 搜索路径，要求所选 CTB/STB 唯一匹配；None 不附加样式文件。仅读取偏好，不修改配置，不启动外部 CAD 实例。此路径读取代码尚待原生验收；默认支持文件搜索的 FindFileHint 并无样式专用枚举，因此没有用它代替样式路径。

核心 PlotResourceRevision 对规范化完整路径和各文件 SHA-256 汇总生成标记，不以长度/修改时间替代内容。路径变化、内容替换、丢失、同名歧义或并发写入都无法静默沿用旧标记。

任务历史新增可空 PlotResourceRevision，保持旧历史可读；新任务创建时记录、每页提交前比较、带 CAD 重试和重试后合并前核对。旧任务缺失此证据时，支持资源核验的宿主拒绝自动重试，要求新建完整批次。已完成单页经过原有哈希检查后独立重合并仍不依赖 CAD。保存逻辑的 DTO 副本同步包含新字段，已做往返测试。

## 验证

588 项核心测试、359 项离线 UI 检查及 .NET Framework 检查通过。覆盖同长度/同时间戳样式修改、列表顺序无关、缺失资源、同名样式歧义、两宿主关联 PMP 列表、历史字段兼容与保存、恢复前和页间资源变化阻止页面及混合合并。

两宿主 Release 均 0 警告/0 错误，新包 `artifacts/FastBatchPlot-20260920-211751-870`。八个项目 DLL 的绝对路径、修改时间、大小及 SHA-256 已核验，均为本轮构建后产物；两套包分别在独立 Windows PowerShell 进程完成资源摘要、纸张列和 PDF 检查。

证据：`audit-evidence/phase41-build-start.txt`、`phase41-build.log`、`phase41-artifacts.json`、`phase41-package.log`、`phase41-ui.log`、`phase41-framework.log`、`test-results/phase41-plot-resources.trx`。独立检查脚本 `check-plot-resources-package.ps1`。

## 尚未覆盖

字体、图片/PDF 底图、驱动二进制及驱动额外配置（例如 PDF.ini）尚未进入标记；整页输出期间未持有全部源资源读锁，外部并发替换的事务一致性尚未保证。当前强制核验要求 PC3/PC5，第三方不可解析配置不能据此承诺兼容。未连接或操作 CAD，样式目录偏好、真实驱动缓存和打印效果仍待明确授权后的原生验收。

图章继续排除，总目标保持未完成。
