# 阶段 33：中望已有 PMP 继承

## 问题与修改

原来只要 PC5 关联 PMP，即使它是合法的本地自定义纸张库，也会拒绝 PDF 参数覆盖或自动纸张。现在只读解析关联 PMP，在任务私有副本中追加唯一纸张，并将私有 PC5 指向该副本。原自定义纸张、校准系数、修改/删除/隐藏设置和未知键值都保留，不把原校准重置为 1。

文件服务位于 `ZwPdfMediaFiles.Pmp.cs`，CAD 适配层接口不变。没有切换 CAD 全局配置、改搜索路径或改写原 PC5/PMP。生成文件继续持读锁，任务结束清理。

## 路径和校验

- 完整引用只接受本地磁盘绝对路径。文件名引用检查 PC5 同目录及其 `PMP Files` 子目录；必须唯一存在，不猜测多个搜索结果。
- 相对多级路径、网络引用、缺失或同名歧义、链式 PMP/外部驱动引用明确报错。
- 校准必须为有限正数，计数必须为有效非负整数，自定义纸张条目必须与声明数量对应；追加索引存在冲突则停止，不覆盖原字段。
- 原生无 BOM GBK 读取和写入，保留中文名称；输入和输出限制 256KB。无自定义纸张的 PMP 可以新增 user 节。
- 校准数值按原设置保持；它对真实 PDF 尺寸的影响仍需原生验收。文件级保留不等于最终驱动效果验证。

## 验证

529 项核心测试、315 项离线 UI 检查通过；最终空值标注修正后，74 项中望配置专项测试再次通过。Release 包为 `artifacts/FastBatchPlot-20260920-200708-791`，两宿主均 0 警告/0 错误。全部八个项目 DLL 的绝对路径、修改时间、大小和 SHA-256 已核验，均晚于构建起点。

两套新包分别以独立 Windows PowerShell 进程加载最新程序集，验证主窗、PDF 参数、历史、PDF 合并，以及 72 种原生纸张继承；均通过。还复验五种中望原生预设与 AutoCAD General Documentation 的 PDF 参数写入，源文件哈希和时间不变。

证据：`audit-evidence/phase33-build-start.txt`、`phase33-build.log`、`phase33-artifacts.json`、`phase33-package.log`、`phase33-ui.log`、`phase33-framework.log`、`test-results/phase33-linked-pmp.trx`、`test-results/phase33-linked-pmp-final.trx`；生成的继承配置和源摘要位于 `phase33-native-AutoCAD2018`、`phase33-native-ZWCAD2026`。

原生样本为安装目录 `PMP Files/ZWPLOT_PDF.pmp`，包含 72 种纸张；测试使用原生 DWG to PDF.pc5 的临时副本关联该 PMP，避免改写用户原配置。逐行检查原字段保留，唯一变化是 userdef_num 从 72 到 73，并追加本次尺寸。

AutoCAD 的 PMP 继承结构不同，仍待实现；中望含驱动私有数据的 PC5 仍保持原有保护。外边线选项与实际 CAD 出图验收仍是剩余缺口。图章继续排除，目标未标记完成。
