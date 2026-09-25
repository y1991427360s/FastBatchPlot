# 阶段 23：合并 PDF 保留图层显示状态

## 已复现的问题

旧合并器只调用 PdfSharpCore.AddPage。该 API 导入页面资源中的 OCG/OCMD，却不导入文档目录 OCProperties。原 PDF 默认关闭的图层在合并后会显示，属于输出内容改变，不能仅凭页数、纸张、书签正确就判断合并正确。

使用独立 pypdf 生成两份同名隐藏图层 PDF：正常内容为蓝色矩形，关闭图层为红色矩形。Poppler 渲染原图只显示蓝色，旧合并图出现红色；像素差区域为 `(160,110)-(230,180)`。证据在 `audit-evidence/phase23-layers`，生成脚本为 `phase23-layer-fixture.py`。这些是合成测试图，不是用户图纸。

## 修复与架构

- 新增纯文件服务 PdfLayerMerger，在所有源页导入后、源文档关闭前收集该文档的图层配置，最后写入合并文档目录。没有 CAD API、进程调用或私有反射。
- 通过 PdfInternals.MapExternalObject 复用页面资源已经导入的 OCG，保证目录中的图层状态控制的是页面实际引用的同一对象。OCMD 内引用的组也保持关联。该库版本未导入对象会抛 KeyNotFoundException，显式处理后再复制未使用但已登记的图层。
- 保留原图层名称、Usage、Intent、默认状态、显式 ON/OFF、Order 层级、Locked、RBGroups 和 AS 规则。同名图层不按名称去重，不会互相串改。
- 每份图纸在图层面板中有独立分组，组名来自该输入的书签名；新建中文字符串使用 PDF Unicode 编码。
- AS 规则没有指定组清单时，生成明确的来源组清单，防止原文档的规则扩大到其他图纸。
- 备用图层配置保留为“本图备用状态 + 其他图纸默认状态”，并保留其列表顺序。不会把不同来源的所有备用状态交叉相乘。
- 使用图层时输出 PDF 版本最低为 1.5。合并原有的书签、页序、取消、临时保存和原子提交流程保留。

## 明确边界

只支持能够明确合成的可选内容配置。缺失目录但页面引用图层、未知组引用、ON/OFF 重叠、BaseState=Unchanged、不同来源默认 Intent/ListMode 不兼容、未知配置扩展、无效或过深结构会明确拒绝合并。已有单页与既有合并目标保留；不以静默删除图层或改变状态换取成功。

这不等于已实现 CAD 驱动的“输出图层”开关，也不承诺 PDF 表单、数字签名、所有外部 PDF 文档级特性都能合并。分辨率、文字转轮廓、边线和 AutoCAD 自动纸张仍是独立未完成项。

## 验证结果

- Release 全方案重建 0 警告、0 错误；418 项核心测试通过，262 项离线界面检查通过。
- 新增 11 项图层回归：OCG/OCMD 引用一致、同名独立开关、显式状态覆盖、备用配置、未引用组、自动打印规则范围、异常时不覆盖成果等。
- .NET 8 CLI 实际合并后，Poppler 渲染两页与原单页逐像素一致。独立 pypdf 修改第一组为 ON 后再渲染，第一张红图形显示，第二张同名层仍不显示，确认图层仍可独立切换。
- 新构建包为 `artifacts/FastBatchPlot-20260920-173411-320`。打包清单及 8 个项目程序集的绝对路径、LastWriteTime、大小、SHA-256 均核验；`nativeAcceptance=pending`。
- 两套包各自在独立 Windows PowerShell/.NET Framework 进程运行 Core/UI、PDF 历史及图层合并检查，返回 PACKAGE_NET48_UI_OK、PACKAGE_NET48_HISTORY_OK、PACKAGE_NET48_PDF_OK、PACKAGE_NET48_PDF_LAYERS_OK。
- 两套包实际合并输出也通过独立 pypdf 对象引用检查、Poppler 像素比较及独立图层切换验证。结果见 `audit-evidence/phase23-layers/verification.json`，可重跑脚本为 `phase23-verify-layers.py`。
- 独立 net48 EXE 已启动完成生成介质、PDF、历史检查。未启动或连接 CAD，未加载 CAD 宿主 DLL，未安装插件或改现有配置。

测试 PDF 及 PNG 仅作为审查证据，不作为产品使用示例或真实 CAD 出图验收证明。
