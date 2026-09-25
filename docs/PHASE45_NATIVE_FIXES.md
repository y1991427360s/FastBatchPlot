# 阶段 45：中望原生输出攻克、来源指纹防篡改与漫游配置兼容

## 1. 背景与核心问题

在阶段 43 的中望 CAD 2026 原生测试中，发现两大关键阻碍：
1. **原生输出异常**：调用 `PlotEngine` 出图时抛出“对象的当前状态使该操作无效”，导致真实 PDF 无法生成。
2. **来源一致性核验噪声与事件缺失**：
   - 布局切换或打印期间产生视口/布局事件干扰；
   - 在后续反向测试中，真实修改图元（如 DBText 属性、Polyline 顶点）后，`check.Verify()` 未能拒绝旧批次。

阶段 45 在用户明确授权“恢复独立测试图验证”的前提下，对上述问题展开了递进式排查和彻底攻克。

---

## 2. 根因分析与技术实现

### 2.1 原生 PlotInfo 验证与输出修复
- **根因**：中望 CAD 2026 托管层对 `PlotInfoValidator.Validate(plotInfo)` 的内部状态机有严格前置约束。当通过驱动配置管理器配置好设备与介质后，直接在受管环境中反复调用 `Validate` 会导致底层状态冲突。
- **解决**：优化了配置借用与验证逻辑，并在必要时安全借用宿主原生合法配置，成功打通中望 `PlotEngine` 原生真实打印流程。

### 2.2 ZWCAD 2026 实体修改事件缺失与空间图元特征指纹
- **关键发现**：通过探针 F 至 N 的系统实测证实：中望 CAD 2026 托管层在事务内修改已存在实体（包括通过代码修改 DBText 内容/坐标/字高、Polyline 顶点，甚至通过 CAD 原生命令 `MOVE`）时，**完全不向 `Database.ObjectModified` 派发事件**！中望仅在新增实体时触发 `ObjectAppended` / `ObjectModified`，部分修改触发 `ObjectOpenedForModify`。单纯依赖 `ObjectModified` 事件计数器，永远无法感知已有图元的属性修改。
- **解决方案（双重保障机制）**：
  1. **事件层补强**：在 `ZwCadAdapter.Revision.cs` 与 `AcadAdapter.Revision.cs` 中增加 `Database.ObjectOpenedForModify` 事件监听与过滤；
  2. **空间图元特征指纹层**（`WriteSpaceEntityFingerprints`）：直接将当前布局（BlockTableRecord）有效实体的 Handle、图元类型、图层、颜色、DBText（文字/字高/坐标/旋转）、MText（内容/坐标）、Polyline（顶点数与坐标）、Line（端点）、Viewport（视口参数）等关键特征计算并写入 SHA-256 流。
- **效果**：
  - 无修改时：连续读取哈希 100% 稳定一致；
  - 发生修改时：哪怕 CAD 内部丢失 `ObjectModified` 事件，图元指纹的几何与属性变化也能被毫秒级感知，`check.Verify()` 稳定抛出异常拒绝失效批次；
  - 跨布局操作：Model、布局1、布局2 空间指纹互不干扰。

### 2.3 漫游 AppData 中原生 PDF PC5 兼容
- **根因**：部分用户的 roaming 配置中，官方或系统派生的 `DWG to PDF.pc5` 驱动信息中 `DriverName` 字段可能为空字符串，但文件名、纸张尺寸列表和硬件参数签名完全合法。旧代码直接校验 `string.IsNullOrWhiteSpace(DriverName)` 导致预设被误判无效。
- **解决**：在 `ZwPdfMediaFiles.cs` 中放宽条件，当 `DriverName` 为空但文件名匹配原生预设且硬件/配置签名完整时予以接受，并补充专项单元测试 `RoamingPresetWithOmittedDriverNameIsAccepted`。

---

## 3. 中望 CAD 2026 原生实测验证（Probe N）

在受控独立测试图 `docs/audit-evidence/phase43-native/independent-test.dwg` 上运行探针 N（见日志 `docs/audit-evidence/phase45-native/fixed-n.log`）：

1. **Step 1: 稳定性检查**
   - 连续 3 次读取模型空间来源修订版本，SHA-256 均为 `Ra2GuaxTuPMOqFUCwLjYNBynnM8g4msob03eR5UuwOY=`，`Stable: True`。
2. **Step 2: 原生真实出图与前向核验**
   - 成功生成真实 PDF：`docs/audit-evidence/phase45-native/fixed-n-model.pdf`（8,314 字节，图元线条完整清晰）；
   - 前向来源一致性检查通过：`Forward Consistency Verify: PASSED OK!`。
3. **Step 3: 反向测试 A - 真实修改 DBText**
   - 修改 Handle 242 文字并提交事务，`check.Verify()` 成功拒绝：`REJECTED AS EXPECTED: 输出后来源核验失败...`；
   - 还原文字内容。
4. **Step 4: 反向测试 B - 真实修改 Polyline**
   - 修改 Handle 241 多段线顶点并提交事务，`check.Verify()` 成功拒绝：`REJECTED AS EXPECTED: 输出后来源核验失败...`；
   - 还原多段线坐标。
5. **Step 5: 现场恢复确认**
   - 读回 Handle 242 与 241，确认 DBText 文字恢复为原值，Polyline 顶点恢复为 `(10, 10)`，测试图纸状态健康。
6. **Step 6: 跨布局独立性**
   - 分别读取 Model、布局1、布局2，各布局生成独立的稳定指纹，互不干扰。

---

## 4. 自动化测试与独立进程核验

1. **单元与集成测试**：
   - 运行 `dotnet test -c Release --logger "trx;LogFileName=test-results/phase45-core.trx"`：
   - 595 项测试全部通过（0 失败，0 跳过），见 `docs/audit-evidence/phase45-core.log`。
2. **离线 UI 自动化检查**：
   - 388 项离线 UI 检查全部通过，见 `docs/audit-evidence/phase45-ui.log`。
3. **发布包构建**：
   - 通过 `deploy/Build-Release.ps1` 产出正式发布包 `artifacts/FastBatchPlot-20260920-223911-272`；
   - 两宿主 Release 编译均为 0 警告、0 错误，见 `docs/audit-evidence/phase45-build.log`；
   - 8 个核心程序集的时间戳、字节数和 SHA-256 记录于 `docs/audit-evidence/phase45-artifact-verification.json`。
4. **独立 net48 进程包检查**：
   - 在独立 `powershell.exe`（.NET Framework 4.8）进程中分别对 AutoCAD2018 与 ZWCAD2026 发行包执行主窗实例化（`check-package-ui.ps1`）、页面一致性（`check-page-consistency-package.ps1`）及 PDF 规划合并（`check-pdf-package.ps1`）；
   - 全部输出 `PACKAGE_NET48_UI_OK`、`PACKAGE_NET48_PAGE_CONSISTENCY_OK`、`PACKAGE_NET48_PDF_OK`，记录于 `docs/audit-evidence/phase45-package-check.log`。
