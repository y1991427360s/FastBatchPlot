# 阶段 32：AutoCAD PDF 直线合并

AutoCAD 参数面板已开放直线合并。开启写入 PC3 的 `res_color_mem/lines_overwrite=FALSE`，关闭写 `TRUE`，跟随驱动不更改该字段。中望仍使用阶段 31 核验的 0/1 反向值。两宿主现在均提供矢量、光栅、文字、图层和合并控制五项覆盖，保留各自驱动字段差异。

## 映射证据

本机 AutoCAD 2018 官方 General Documentation.pc3 明确包含 `lines_overwrite=TRUE`。配置模块 `plotcfg14.dll` 导出 `HT_Std_ResColorMem_Configuration::get_lines_overwrite`（RVA 0x799B0），读取对象偏移 0x6C；该方法位于虚表 0xAD198 的偏移 0xA0。编辑器 `pc3edit.dll` RVA 0x2A269 调用同一接口；随后 neg/sbb/add 根据返回值选择资源：TRUE 选 0x115，FALSE 选 0x116。中文资源分别为“直线覆盖”“直线合并”。另一路初始化在 RVA 0x2FB91 调用接口，通过 sete 将 FALSE 转为列表索引 1，列表按覆盖、合并顺序添加。由此确认反向映射，不仅根据字段英文猜测。

可复现脚本 `audit-evidence/inspect-acad-merge-mapping.py`，结果和源文件 SHA-256 位于 `phase32-acad-merge-static.json`。只读解析磁盘文件，未加载 DLL、启动编辑器或连接 CAD。

## 实现保护

只在用户明确覆盖时读写现有字段；缺失或非 TRUE/FALSE 值报错，且不创建私有文件。其他参数、分辨率、原 PC3/PMP 均保持。参数继续复用既有设置、任务快照、历史恢复和重试链路。

## 验证

- 515 项核心测试、315 项离线 UI 检查通过；.NET Framework 独立检查通过。
- 两宿主 Release 包构建成功，均 0 警告/0 错误：`artifacts/FastBatchPlot-20260920-195605-120`。
- 八个项目 DLL 的绝对路径、修改时间、大小、SHA-256 已保存；修改时间均不早于本轮构建开始。证据为 `audit-evidence/phase32-build-start.txt`、`phase32-build.log`、`phase32-artifacts.json`。
- 两套新包分别通过独立 Windows PowerShell 进程加载并验证主窗、参数面板、历史与 PDF，见 `phase32-package.log`。
- 五种 AutoCAD 官方预设分别执行跟随、合并、覆盖三态私有配置生成，原 PC3 哈希和修改时间不变，其他参数保持不变、临时文件清理通过。两套包均执行，共 30 次生成。记录为 `phase32-native-AutoCAD2018/sources.json` 和 `phase32-native-ZWCAD2026/sources.json`。
- 同时复验中望五预设与 AutoCAD General Documentation 的全部参数组合。测试记录为 `test-results/phase32-parameters.trx`、`phase32-ui.log`、`phase32-framework.log`。截图 `phase32-pdf-parameters-autocad.png` 已人工检查。

真实 PDF 交叠效果仍需明确 CAD 验证授权后验收。图章继续不在范围内，整体 PDF 功能对齐未宣告完成。
