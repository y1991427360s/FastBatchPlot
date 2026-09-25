# 阶段 34：AutoCAD PMP 继承

## 行为

PC3 关联合法本地 PDF PMP 时，复制原 PMP 并在 udm/media 的 size 和 description 两个集合中追加唯一纸张。索引取两个集合的最大值加一，保留旧索引和名称，不重新编号。原校准、能力、修改/删除规则及未知字段保留。任务 PC3 与 PMP 元数据改指向任务副本；原文件不写入。

PMP 可含 mod/udm 中的 PDF 参数覆盖。用户明确设置时，同步这些现存覆盖中的对应字段，避免 PC3 的新参数被原 PMP 旧值覆盖。仅更新明确选择的矢量、光栅、文字、图层和合并参数；未选择的参数保持原值。del 节删除了本次覆盖字段时明确报错，不猜测优先级。

代码分离为 AcadPdfMediaFiles.Pmp.cs，继续由 Core 处理文件，CAD 适配层接口不变。

## 校验

本地绝对路径或唯一的文件名引用可读取；文件名在 PC3 同目录与 PMP Files 子目录中必须唯一存在。路径/文件名不一致、网络或含糊路径、缺失文件、驱动型号不匹配、无效校准、重复或失联纸张描述、异常索引均在创建临时文件前报错。保持容器长度、压缩校验、GBK 编码和文件读锁保护。

原生 PMP 的 meta 中可保留构建机的旧自引用路径（本机 PublishToWeb PNG.pmp 即如此）；这是元数据，生成时更新为当前副本，不把它作为下一份 PMP 递归加载。该 PNG 文件仅用于了解容器层级，不能用于 PDF 驱动，驱动匹配检查会拒绝。

## 验证与边界

539 项核心测试、315 项离线 UI 检查通过；.NET Framework 独立检查通过。两宿主 Release 包均 0 警告/0 错误，产物为 `artifacts/FastBatchPlot-20260920-201355-121`。八个项目 DLL 的绝对路径、修改时间、大小和 SHA-256 已记录，时间均晚于构建起点。

两套包分别在独立 Windows PowerShell 进程中加载最新程序集，主窗、PDF 参数、历史、PDF 合并、五种 AutoCAD PDF 预设继承及六种原生预设参数检查均通过。原 PC3 哈希和修改时间保持不变，临时文件清理通过。

证据位于 `audit-evidence/phase34-build-start.txt`、`phase34-build.log`、`phase34-artifacts.json`、`phase34-package.log`、`phase34-ui.log`、`phase34-framework.log`、`test-results/phase34-acad-pmp.trx`，继承样本和源哈希在 `phase34-native-AutoCAD2018`、`phase34-native-ZWCAD2026`。

五种官方 PDF PC3 的检查使用程序离线生成的 PDF PMP 样本并设置非 1 校准，逐字段比较继承结果；不能把该样本说成 CAD 生成或验收的用户 PMP。真实驱动对自定义介质、校准与覆盖的处理仍需原生验收。

未启动、连接或操作 CAD。最外边线控制仍待补齐，图章继续排除，整体 PDF 对齐未宣告完成。
