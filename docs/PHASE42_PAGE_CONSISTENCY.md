# 阶段 42：PDF 输出后来源核验与 Antigravity 委派

## 修改

此前只在输出前或下一页/合并前核对来源，当前页输出期间变化仍可能先记成功。新增 CadPageConsistency：在输出前保存独立图框/配置快照及已知来源、打印资源标记，PDF 接口成功后再次核验。来源、外参、配置或宿主实例变化，以及核验接口失败，都在页面记成功前转为失败。不确定文件保留供检查，不记录成功摘要，不参与自动合并；后续重试不会自动覆盖该文件。原接口清理警告同时保留。

单张、批量、历史重试共用调度入口；即使未启用磁盘任务历史也执行这项核验。没有实现可选修订接口的兼容宿主只检查实例一致性，不能声称其拥有完整来源验证。声明支持接口但返回 null、空串或空白时立即拒绝；页后返回空标记也失败。

## Antigravity MCP 实际使用

用户要求将小功能交给 Antigravity 实现，主智能体复核测试。通过 Python MCP ClientSession/stdin-stdout 协议连接本机已配置的 antigravity_cli/server.py，未修改全局权限，也没有通过普通命令行冒充 MCP 调用。

首次任务 b809ef8497f94227ba7d42eecfc0dcc1 返回 SUCCESS 但空回复，stderr 表明无界面命令权限自动拒绝；对比两个文件没有改动，未将此认作完成。

改为直接提供源码、禁止调用工具的补丁生成任务 89f846b5d8684d7080369a85e41fc099。Antigravity 返回空标记防护及测试补丁。主智能体审查并落地，修正不标准补丁空行格式、测试假宿主可空返回声明，并独立补充外部图框/配置修改不污染快照的测试。没有开启 dangerously-skip-permissions。

证据：audit-evidence/phase42-antigravity-client.py、phase42-antigravity-before.json、phase42-antigravity-result.json、phase42-antigravity-patch-result.json、phase42-antigravity.patch。原始 CLI 日志位置记录在结果 JSON。

## 验证

588 项核心测试、384 项离线 UI 检查通过，.NET Framework 检查通过。包含构造/页后空标记、接口抛错、重复核验保持初始基准、无可选接口兼容、宿主切换、单张资源变化、历史当前页失败、无磁盘历史页后保护及警告保留。

两宿主 Release 均 0 警告/0 错误，新包 artifacts/FastBatchPlot-20260920-213046-884。八个项目 DLL 的绝对路径、时间、大小、SHA-256 已核验且时间不早于本轮构建起点。两套新包均在独立 Windows PowerShell 进程完成页后变更/空标记检查、单张 PDF 及 PDF 基础检查。独立脚本最初缺 netstandard 引用，修正引用路径后通过，未把失败当作通过。

证据：phase42-build-start.txt、phase42-build.log、phase42-artifacts.json、phase42-package.log、phase42-ui.log、phase42-framework.log、test-results/phase42-page-consistency.trx，均位于 audit-evidence。包检查为 check-page-consistency-package.ps1。

## 限制

前后标记比较不是完整事务锁，不能发现外部文件在输出中变化后又还原的所有情形；字体、图片底图和驱动额外依赖仍未全部覆盖。原生 CAD 临时操作可能产生保守的修改事件噪声，尚未验收，可能使当前页被拒绝而非误记成功。未启动、连接或操作 CAD。总目标未完成，图章继续排除。
